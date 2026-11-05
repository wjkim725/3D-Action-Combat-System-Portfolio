using UnityEngine;

[RequireComponent(typeof(Hero))]
public class MeleeHitDetector : MonoBehaviour
{
    private Hero _hero;

    [Header("Melee Impact Locator")]
    [SerializeField]
    private MeleeImpactLocator _meleeImpactLocator;

    private void Awake()
    {
        _hero = GetComponent<Hero>();
    }

    public bool Execute(
    CombatEventContext context,
    float radius,
    float forwardOffset)
    {
        if (context == null)
        {
            Debug.LogError($"[{name}] CombatEventContext가 전달되지 않았습니다.");
            return false;
        }

        Vector3 hitPosition =
            transform.position +
            transform.forward * forwardOffset;

        Collider[] hitEnemies = Physics.OverlapSphere(
            hitPosition,
            radius,
            _hero.EnemyLayerMask);

        bool isHitSuccess = false;

        foreach (Collider enemyCollider in hitEnemies)
        {
            if (!enemyCollider.TryGetComponent(
                    out IDamageable damageableTarget))
            {
                continue;
            }

            context.Target = damageableTarget;
            context.HasImpactData = false;
            context.ClearReactionDirection();

            TryPopulateImpactData(
                context,
                enemyCollider);

            ResolveReactionDirection(
                context,
                enemyCollider,
                hitPosition);

            isHitSuccess |=
                CombatManager.Instance.ProcessHit(context);
        }

        // 한 번의 타격 이벤트로 여러 대상을 맞혀도 자원 반환은 한 번만 처리
        if (isHitSuccess)
        {
            CombatManager.Instance.PayBackCombatResource(context);
        }

        return isHitSuccess;
    }

    /// <summary>
    /// 근접 무기 위치를 기준으로 타격 연출에 필요한 공간 정보를 구성
    /// </summary>
    private bool TryPopulateImpactData(
        CombatEventContext context,
        Collider targetCollider)
    {
        if (_meleeImpactLocator == null ||
            targetCollider == null)
        {
            return false;
        }

        if (!_meleeImpactLocator.TryGetImpactSurface(
                targetCollider,
                out Vector3 impactPoint,
                out Vector3 surfaceNormal))
        {
            return false;
        }

        context.ImpactPoint = impactPoint;
        context.ImpactDirection =
            _meleeImpactLocator.GetImpactDirection(targetCollider);
        context.SurfaceNormal = surfaceNormal;
        context.SlashDirection =
            _meleeImpactLocator.GetImpactMotionDirection(surfaceNormal);
        context.HasImpactData = true;

        Debug.DrawRay(
            impactPoint,
            surfaceNormal,
            Color.green,
            0.5f);

        return true;
    }

    /// <summary>
    /// 공격 데이터에 지정된 계산 방식에 따라 피격 반응용 월드 방향을 결정
    /// Auto는 근접 공격의 기본값인 무기 궤적 방향을 사용
    /// </summary>
    private void ResolveReactionDirection(
        CombatEventContext context,
        Collider targetCollider,
        Vector3 overlapOrigin)
    {
        switch (context.ReactionDirectionMode)
        {
            case HitReactionDirectionMode.Auto:
            case HitReactionDirectionMode.AttackMotion:
                if (context.HasImpactData &&
                    context.SlashDirection.sqrMagnitude >= 0.0001f)
                {
                    context.SetReactionDirection(
                        context.SlashDirection);
                    return;
                }

                SetAttackerToTargetDirection(
                    context,
                    targetCollider);
                break;

            case HitReactionDirectionMode.RadialFromOrigin:
                context.SetReactionDirection(
                    targetCollider.bounds.center - overlapOrigin);
                break;

            case HitReactionDirectionMode.AttackerToTarget:
                SetAttackerToTargetDirection(
                    context,
                    targetCollider);
                break;

            case HitReactionDirectionMode.FixedFront:
                context.ClearReactionDirection();
                break;

            default:
                Debug.LogWarning(
                    $"[{name}] 지원하지 않는 HitReactionDirectionMode: " +
                    $"{context.ReactionDirectionMode}");

                context.ClearReactionDirection();
                break;
        }
    }

    /// <summary>
    /// 공격자 위치에서 피격 대상 중심을 향하는 월드 방향을 설정
    /// </summary>
    private void SetAttackerToTargetDirection(
        CombatEventContext context,
        Collider targetCollider)
    {
        context.SetReactionDirection(
            targetCollider.bounds.center - transform.position);
    }

    
}
