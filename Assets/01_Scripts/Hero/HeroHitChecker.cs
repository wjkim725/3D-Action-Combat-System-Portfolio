using System.Collections.Generic;
using System.Drawing;
using UnityEngine;

/// <summary>
/// 데이터 구조체(Combo, Hit) 정보를 기반으로 타격을 완벽하게 추출하고 검출하는 모듈
/// </summary>
public class HeroHitChecker : MonoBehaviour
{
    private Hero _hero;

    private void Awake()
    {
        _hero = GetComponent<Hero>();
    }


    #region [애니메이션 이벤트 수신 인터페이스]

    public void ExecuteAttackHitCheck(int combo, int hit)
    {
        ExecuteGenericHitRegistry(_hero.CombatData.normalAttackSequences, combo, hit, "Attack");
    }

    public void ExecuteChargeAttackHitCheck(int combo, int hit)
    {
        ExecuteGenericHitRegistry(_hero.CombatData.chargeAttackSequences, combo, hit, "ChargeAttack");
    }

    public void ExecuteDashAttackHitCheck(int combo, int hit)
    {
        ExecuteGenericHitRegistry(_hero.CombatData.dashAttackSequences, combo, hit, "DashAttack");
    }

    public void ExecuteSkillHitCheck(int combo, int hit)
    {
        ExecuteGenericHitRegistry(_hero.CombatData.skillAttackSequences, combo, hit, "Skill");
    }

    public void ExecuteExSkillHitCheck(int combo, int hit)
    {
        ExecuteGenericHitRegistry(_hero.CombatData.exSkillAttackSequences, combo, hit, "EXSkill");
    }

    /// <summary>
    /// 에너지 소모없는 강특 막타가 있는 Maria같은 Hero를 위한 HitCheck 메서드
    /// </summary>
    /// <param name="hit"></param>
    public void ExecuteExSkillFinaleHitCheck(int hit)
    {
        // 리스트가 아니므로 combo 인덱스 없이 바로 hits에 접근
        ExecuteGenericHitRegistrySingle(_hero.CombatData.exSkillFinisherSequence, hit, "ExSkillFinisher");
    }

    public void ExecuteUltHitCheck(int combo, int hit)
    {
        ExecuteGenericHitRegistry(_hero.CombatData.ultAttackSequences, combo, hit, "Ult");
    }

    public void ExecuteParryHitCheck(int combo, int hit)
    {
        //ExecuteGenericHitRegistry(_hero.CombatData.parrySequences, combo, hit, "Parry");
        // 패링은 공격시전자 단일 대상으로 그로기 처리되어야 하므로,
        // CombatManager가 책임을 가지도록 바뀜
    }
        
    public void ExecuteParryCounterHitCheck(int combo, int hit)
    {
        ExecuteGenericHitRegistry(_hero.CombatData.parryCounterSequences, combo, hit, "ParryCounter");
    }

    public void ExecuteDodgeCounterHitCheck(int combo, int hit)
    {
        ExecuteGenericHitRegistry(_hero.CombatData.dodgeCounterSequences, combo, hit, "DodgeCounter");
    }

    public void ExecuteChainAttackHitCheck(int combo, int hit)
    {
        ExecuteGenericHitRegistry(_hero.CombatData.chainAttackSequences, combo, hit, "ChainAttack");
    }

    public void ExecuteQuickAssistHitCheck(int combo, int hit)
    {
        ExecuteGenericHitRegistry(_hero.CombatData.quickAssistSequences, combo, hit, "QuickAssist");
    }

    #endregion


    #region [중심 코어 : 데이터 추출 및 물리 검출 통합 처리 엔진]

    /// <summary>
    /// 모든 형태의 공격 시퀀스 리스트에서 정확한 (Combo, Hit) 구조체 데이터를 추적하여 판정을 내리는 코어 메서드
    /// </summary>
    private void ExecuteGenericHitRegistry(List<HeroCombatDataSO.MotionSequenceData> sequenceList, int comboIndex, int hitIndex, string debugActionName)
    {
        // 1. 디펜시브 코드
        if (_hero.CombatData == null || sequenceList == null || sequenceList.Count == 0) return;

        // 2. 모션(Combo) 인덱스 안전 범위 보정
        if (comboIndex >= sequenceList.Count)
            comboIndex = sequenceList.Count - 1;

        HeroCombatDataSO.MotionSequenceData currentMotion = sequenceList[comboIndex];

        // 3. 다단히트(Hit) 인덱스 안전 범위 보정
        if (currentMotion.hits == null || currentMotion.hits.Count == 0)
        {
            Debug.LogWarning($"[{debugActionName}] {currentMotion.motionName}에 세부 Hit 데이터가 존재하지 않습니다.");
            return;
        }
        if (hitIndex >= currentMotion.hits.Count)
            hitIndex = currentMotion.hits.Count - 1;

        // 4. 완벽한 타격 타수별 구조체 데이터 조준 획득
        HeroCombatDataSO.HitData currentHitData = currentMotion.hits[hitIndex];

        // 5. CombatEventContext 인스턴스 생성 및 데이터 초기화
        CombatEventContext contextPack = new CombatEventContext(
            _hero,                                  // Attacker (IAttacker)
            currentHitData.damageMultiplier,        // 데미지 계수
            currentHitData.dazeMultiplier,          // 그로기 계수
            currentHitData.hitType,                 // 경직 타입
            currentHitData.canExtendGroggy,         // 그로기 연장 여부
            currentHitData.groggyExtendTime,        // 그로기 연장 시간
            currentHitData.payBackEnergy,           // 자원 페이백 에너지
            currentHitData.payBackDecibel,          // 자원 페이백 데시벨
            currentHitData.canTriggerChainAttack,   // 콤보스킬(ChainAttack) 발동가능 공격 여부    
            currentHitData.hitMarkData              // Hit Mark Data
        );

        Debug.Log($"[HitChecker 판정완료] {debugActionName} -> 모션: {currentMotion.motionName} | 타수: {hitIndex + 1}/{currentMotion.hits.Count}타 ({currentHitData.hitName})");

        // 6. 구체 물리 오버랩 및 타격 레포트 전송
        ExecuteOverlapSphereCheck(contextPack, currentMotion.hitRadius, currentMotion.hitForwardOffset);
    }

    /// <summary>
    /// 보조 헬퍼 (List가 아닌 단일 MotionSequenceData 처리를 위해 하나 뚫어둠)
    /// </summary>
    /// <param name="motionData"></param>
    /// <param name="hitIndex"></param>
    /// <param name="debugActionName"></param>
    private void ExecuteGenericHitRegistrySingle(HeroCombatDataSO.MotionSequenceData motionData, int hitIndex, string debugActionName)
    {
        // 1. 방어 코드: 데이터 널 체크 및 히트 인덱스 범위 초과 검사
        if (motionData.hits == null || hitIndex >= motionData.hits.Count)
        {
            Debug.LogWarning($"[{debugActionName}] 판정 데이터가 비어있거나 Hit 인덱스가 범위를 벗어났습니다. (HitIndex: {hitIndex})");
            return;
        }

        // 2. 현재 타격 프레임에 매칭되는 세부 HitData 추출
        var currentHitData = motionData.hits[hitIndex];

        // 3. 전투 이벤트 콘텍스트 팩토리 조립
        CombatEventContext contextPack = new CombatEventContext(
            _hero,                                  // Attacker (IAttacker)
            currentHitData.damageMultiplier,        // 데미지 계수
            currentHitData.dazeMultiplier,          // 그로기 계수
            currentHitData.hitType,                 // 경직 타입
            currentHitData.canExtendGroggy,         // 그로기 연장 여부
            currentHitData.groggyExtendTime,        // 그로기 연장 시간
            currentHitData.payBackEnergy,           // 자원 페이백 에너지
            currentHitData.payBackDecibel,          // 자원 페이백 데시벨
            currentHitData.canTriggerChainAttack,   // 콤보스킬(ChainAttack) 발동가능 공격 여부
            currentHitData.hitMarkData              // Hit Mark Data
        );

        // 4. 디버그 로그 출력 (현재 몇 타 중 몇 타가 터지는지 시각화)
        Debug.Log($"[HitChecker 판정완료] {debugActionName} -> 모션: {motionData.motionName} | 타수: {hitIndex + 1}/{motionData.hits.Count}타 ({currentHitData.hitName})");

        // 5. 구체 물리 오버랩 연산 및 타격 레포트 전송 호출
        // SO 구조 분리를 통해 단발성 막타도 motionData 내부의 고유한 반경(Radius)과 오프셋을 온전히 적용
        ExecuteOverlapSphereCheck(contextPack, motionData.hitRadius, motionData.hitForwardOffset);
    }

    /// <summary>
    /// 순수 3D 물리 공간 검출 및 타격 전달 처리
    /// + 타격 VFX를 위한 타격 포인트 및 방향 계산
    /// </summary>
    private void ExecuteOverlapSphereCheck(CombatEventContext context, float radius, float forwardOffset)
    {
        Vector3 hitPosition = transform.position + (transform.forward * forwardOffset); 
        Collider[] hitEnemies = Physics.OverlapSphere(hitPosition, radius, _hero.EnemyLayerMask); 

        bool isHitSuccess = false;

        foreach (Collider enemyCollider in hitEnemies)
        {
            if (enemyCollider.TryGetComponent<IDamageable>(out var damageableTarget))
            {
                context.Target = damageableTarget;
                context.HasImpactData = false;

                // VFX를 위한 Data 전달
                if (_hero.VFX != null &&
                    _hero.VFX.TryGetMeleeImpactData(
                        enemyCollider,
                        out Vector3 impactPoint,
                        out Vector3 impactDirection,
                        out Vector3 surfaceNormal,
                        out Vector3 slashDirection))
                {
                    context.ImpactPoint = impactPoint;
                    context.ImpactDirection = impactDirection;
                    context.SurfaceNormal = surfaceNormal;
                    context.SlashDirection = slashDirection;
                    context.HasImpactData = true;
                }

                // 실제 타겟 처리
                isHitSuccess |= CombatManager.Instance.ProcessHit(context);
            }
        }

        // 단 한 명이라도 적이 맞았다면 자원 반환 가동
        // 타격당한 적의 수와 상관없이 딱 한번만 페이백 해주기 위해
        // CombatManager에서 호출안하고 HitChecker에서 호출
        if (isHitSuccess) 
        {
            CombatManager.Instance.PayBackCombatResource(context); 
        }
    }

    #endregion

    
}