using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 현재 실행 중인 Enemy 패턴의 Motion/Hit 데이터를 추출하고
/// 플레이어 대상 물리 검출 및 데미지 전달을 수행하는 컴포넌트
/// </summary>
public class EnemyHitChecker : MonoBehaviour
{
    private Enemy _enemy;
    private bool _hasPlayedHitImpulseForCurrentMotion;

    private void Awake()
    {
        _enemy = GetComponent<Enemy>();
    }

    #region [애니메이션 이벤트 수신 인터페이스]

    public void ExecuteAttackHitCheck(int motionIndex, int hitIndex)
    {
        EnemyAttackPattern currentPattern = _enemy.CurrentAttackPattern;

        if (currentPattern == null || currentPattern.Data == null)
        {
            Debug.LogWarning($"{gameObject.name}: 현재 실행 중인 공격 패턴이 없습니다.");
            return;
        }

        EnemyCombatDataSO combatData = currentPattern.Data.combatData;

        if (combatData == null)
        {
            Debug.LogWarning($"[{currentPattern.Data.patternName}] CombatData가 없습니다.");
            return;
        }

        ExecuteHitRegistry(
            combatData.motionSequences,
            motionIndex,
            hitIndex,
            currentPattern.Data.patternName
        );
    }

    #endregion

    #region [데이터 추출 및 물리 검출]

    private void ExecuteHitRegistry(
        List<EnemyCombatDataSO.MotionSequenceData> sequenceList,
        int motionIndex,
        int hitIndex,
        string patternName)
    {
        if (sequenceList == null || sequenceList.Count == 0)
        {
            Debug.LogWarning($"[{patternName}] MotionSequence 데이터가 없습니다.");
            return;
        }

        if (motionIndex < 0 || motionIndex >= sequenceList.Count)
        {
            Debug.LogWarning(
                $"[{patternName}] MotionIndex가 범위를 벗어났습니다. " +
                $"Index: {motionIndex}, Count: {sequenceList.Count}");
            return;
        }

        EnemyCombatDataSO.MotionSequenceData currentMotion =
            sequenceList[motionIndex];

        if (currentMotion.hits == null || currentMotion.hits.Count == 0)
        {
            Debug.LogWarning(
                $"[{patternName}] {currentMotion.motionName}에 Hit 데이터가 없습니다.");
            return;
        }

        if (hitIndex < 0 || hitIndex >= currentMotion.hits.Count)
        {
            Debug.LogWarning(
                $"[{patternName}] {currentMotion.motionName}의 HitIndex가 범위를 벗어났습니다. " +
                $"Index: {hitIndex}, Count: {currentMotion.hits.Count}");
            return;
        }

        if (hitIndex == 0)
        {
            _hasPlayedHitImpulseForCurrentMotion = false;
        }

        EnemyCombatDataSO.HitData currentHitData =
            currentMotion.hits[hitIndex];

        // CombatEventContext 클래스를 그대로 사용하되,
        // Enemy의 공격자, 판정 범위, 데미지, 경직과 타격음을 전달함.
        CombatEventContext contextPack = new CombatEventContext(
            _enemy,
            HitDetectionType.MeleeOverlap, // 현재 Enemy 판정은 근접 OverlapSphere 방식
            currentMotion.hitRadius,      // 판정 생성 시 확정한 모션 반경
            currentHitData.damageMultiplier,
            0f,
            currentHitData.hitType,
            false,
            0f,
            0f,
            0f,
            false,
            hitImpactSFX: currentHitData.hitImpactSFX
        );

        Debug.Log(
            $"[Enemy HitChecker] {patternName} -> " +
            $"모션: {currentMotion.motionName} | " +
            $"타수: {hitIndex + 1}/{currentMotion.hits.Count} " +
            $"({currentHitData.hitName})");

        bool isHitSuccess = ExecuteOverlapSphereCheck(
            contextPack,
            currentMotion.hitRadius,
            currentMotion.hitForwardOffset
        );

        TryRequestHitImpulse(
            isHitSuccess,
            currentHitData.hitType
        );
    }

    /// <summary>
    /// 현재 모션에서 최초 유효 타격이 발생했을 때,
    /// 일반 Hit Impulse 연출을 한 번만 요청합니다.
    /// </summary>
    private void TryRequestHitImpulse(
        bool isHitSuccess,
        HitType hitType)
    {
        if (!isHitSuccess) return;
        if (_hasPlayedHitImpulseForCurrentMotion) return;

        if (hitType == HitType.None ||
            hitType == HitType.Parry)       // Parry는 전용 Impulse 연출 존재
        {
            return;
        }

        CombatPresentationManager presentationManager =
            CombatPresentationManager.Instance;

        if (presentationManager == null) return;

        presentationManager.PlayHitImpulse(hitType);

        _hasPlayedHitImpulseForCurrentMotion = true;
    }

    private bool ExecuteOverlapSphereCheck(
        CombatEventContext context,
        float radius,
        float forwardOffset)
    {
        Vector3 hitPosition =
            transform.position +
            transform.forward * forwardOffset;

        Collider[] hitTargets = Physics.OverlapSphere(
            hitPosition,
            radius,
            _enemy.HeroLayerMask
        );

        bool isHitSuccess = false;

        foreach (Collider targetCollider in hitTargets)
        {
            if (!targetCollider.TryGetComponent(
                    out IDamageable damageableTarget))
            {
                continue;
            }

            Vector3 impactPoint = default;
            Vector3 impactDirection = default;

            bool hasImpactData =
                _enemy.VFX != null &&
                _enemy.VFX.TryGetMeleeImpactData(
                    targetCollider,
                    out impactPoint,
                    out impactDirection
                );

            // Locator가 없거나 표면 탐색에 실패했을 경우
            // 기존 계산 방식을 예비 처리로 사용
            if (!hasImpactData)
            {
                impactPoint =
                    targetCollider.ClosestPoint(
                        hitPosition
                    );

                impactDirection =
                    targetCollider.bounds.center -
                    hitPosition;

                if (impactDirection.sqrMagnitude < 0.0001f)
                {
                    impactDirection = transform.forward;
                }
                else
                {
                    impactDirection.Normalize();
                }

                hasImpactData = true;
            }

            context.Target = damageableTarget;
            context.HasImpactData = hasImpactData;
            context.ImpactPoint = impactPoint;
            context.ImpactDirection = impactDirection;

            isHitSuccess |=
                CombatManager.Instance.ProcessHit(context);
        }

        return isHitSuccess;
    }

    #endregion
}
