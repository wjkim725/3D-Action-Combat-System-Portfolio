using System.Collections.Generic;
using UnityEngine;

public static class AreaHitUtility
{
    #region [범위 타격 판정]

    /// <summary>
    /// 직접 접촉 대상과 범위 안의 적을 중복 없이 타격하고 성공 여부를 반환함. 자원 반환은 호출자가 처리함.
    /// </summary>
    public static bool Execute(
        CombatEventContext context,
        Vector3 origin,
        float radius,
        LayerMask targetLayerMask,
        Vector3 fallbackDirection,
        Collider directContact = null)
    {
        if (context == null || context.Attacker == null ||
            context.Attacker.AttackerTransform == null || radius < 0f ||
            float.IsNaN(radius) || float.IsInfinity(radius)) return false;

        CombatManager combatManager = CombatManager.Instance;
        if (combatManager == null)
        {
            Debug.LogError("[AreaHitUtility] CombatManager가 없습니다.");
            return false;
        }

        Collider[] candidates = Physics.OverlapSphere(origin, radius, targetLayerMask);
        HashSet<IDamageable> hitTargets = new HashSet<IDamageable>();
        bool isHitSuccess = false;

        // 직접 접촉 대상은 표면 경계의 오차로 누락되지 않게 같은 중복 검사에 먼저 전달함.
        if (directContact != null)
        {
            isHitSuccess |= TryHitTarget(context, directContact, origin, targetLayerMask,
                fallbackDirection, hitTargets, combatManager);
        }

        foreach (Collider candidate in candidates)
        {
            isHitSuccess |= TryHitTarget(context, candidate, origin, targetLayerMask,
                fallbackDirection, hitTargets, combatManager);
        }

        return isHitSuccess;
    }

    /// <summary>
    /// 자기 자신·아군·사망 대상과 중복 Collider를 제외하고 대상별 공간 정보를 구성하여 타격함.
    /// </summary>
    private static bool TryHitTarget(
        CombatEventContext context, Collider candidate, Vector3 origin,
        LayerMask targetLayerMask, Vector3 fallbackDirection,
        HashSet<IDamageable> hitTargets, CombatManager combatManager)
    {
        if (candidate == null ||
            (targetLayerMask.value & (1 << candidate.gameObject.layer)) == 0) return false;

        IDamageable target = candidate.GetComponentInParent<IDamageable>();
        if (target == null || ReferenceEquals(target, context.Attacker) ||
            target.Status == null || target.Status.IsDead ||
            (context.Attacker is Hero && target is Hero) ||
            (context.Attacker is Enemy && target is Enemy) ||
            !hitTargets.Add(target)) return false;

        context.Target = target;
        context.HasImpactData = false;
        context.ClearReactionDirection();
        PopulateImpactData(context, candidate, origin, fallbackDirection);
        ResolveReactionDirection(context, candidate, origin);
        return combatManager.ProcessHit(context);
    }

    #endregion

    #region [타격 공간 정보 및 피격 방향]

    /// <summary>
    /// 범위 중심을 향한 대상 표면에서 타격 위치와 법선을 구해 연출 정보를 구성함.
    /// </summary>
    private static void PopulateImpactData(
        CombatEventContext context,
        Collider targetCollider,
        Vector3 overlapOrigin,
        Vector3 fallbackDirection)
    {
        Vector3 targetCenter = targetCollider.bounds.center;
        Vector3 impactDirection = targetCenter - overlapOrigin;

        if (impactDirection.sqrMagnitude < 0.0001f)
        {
            impactDirection = fallbackDirection;
        }

        impactDirection.Normalize();

        // 범위 중심이 Collider 내부에 있어도 표면을 찾도록 경계 밖에서 검사
        float rayDistance = targetCollider.bounds.extents.magnitude + 0.1f;
        Vector3 rayOrigin = targetCenter - impactDirection * rayDistance;
        Ray impactRay = new Ray(rayOrigin, impactDirection);

        context.ImpactPoint = targetCollider.ClosestPoint(rayOrigin);
        context.ImpactDirection = impactDirection;
        context.SurfaceNormal = -impactDirection;

        if (targetCollider.Raycast(impactRay, out RaycastHit surfaceHit, rayDistance * 2f))
        {
            context.ImpactPoint = surfaceHit.point;
            context.SurfaceNormal = surfaceHit.normal;
        }

        // 범위 공격에는 무기 궤적이 없으므로 표면 위 방향을 연출 기준으로 사용
        Vector3 slashDirection = Vector3.ProjectOnPlane(
            Vector3.up,
            context.SurfaceNormal);

        if (slashDirection.sqrMagnitude < 0.0001f)
        {
            slashDirection = Vector3.ProjectOnPlane(
                fallbackDirection,
                context.SurfaceNormal);
        }

        context.SlashDirection = slashDirection.normalized;
        context.HasImpactData = true;
    }

    /// <summary>
    /// Auto는 범위 중심에서 바깥쪽 방향을 사용하며, 무기 궤적이 없는 AttackMotion도 동일하게 처리함.
    /// </summary>
    private static void ResolveReactionDirection(
        CombatEventContext context,
        Collider targetCollider,
        Vector3 overlapOrigin)
    {
        switch (context.ReactionDirectionMode)
        {
            case HitReactionDirectionMode.Auto:
            case HitReactionDirectionMode.AttackMotion:
            case HitReactionDirectionMode.RadialFromOrigin:
                context.SetReactionDirection(
                    targetCollider.bounds.center - overlapOrigin);
                break;

            case HitReactionDirectionMode.AttackerToTarget:
                context.SetReactionDirection(
                    targetCollider.bounds.center - context.Attacker.AttackerTransform.position);
                break;

            case HitReactionDirectionMode.FixedFront:
                context.ClearReactionDirection();
                break;

            default:
                Debug.LogWarning(
                    $"[AreaHitUtility] 지원하지 않는 HitReactionDirectionMode: " +
                    $"{context.ReactionDirectionMode}");

                context.ClearReactionDirection();
                break;
        }
    }

    #endregion
}
