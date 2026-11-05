using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 데이터 구조체(Combo, Hit) 정보를 기반으로 타격을 완벽하게 추출하고 검출하는 모듈
/// </summary>
public class HeroHitChecker : MonoBehaviour
{
    private Hero _hero;

    [SerializeField] private MeleeHitDetector _meleeHitDetector;
    [SerializeField] private ProjectileLauncher _projectileLauncher;
    [SerializeField] private AreaHitDetector _areaHitDetector;

    // 하나의 모션에서 Hit Impulse 를 한번이라도 발동시켰나 기록용 플래그
    private bool _hasPlayedHitImpulseForCurrentMotion;



    private void Awake()
    {
        _hero = GetComponent<Hero>();
    }


    #region [애니메이션 이벤트 수신 인터페이스]

    /// <summary>
    /// NormalAttack 공격 종류를 지정하여 타격 판정을 실행함.
    /// </summary>
    public void ExecuteAttackHitCheck(int combo, int hit)
    {
        ExecuteGenericHitRegistry(_hero.CombatData.normalAttackSequences, combo, hit, "Attack", HeroCombatAttackType.NormalAttack);
    }

    /// <summary>
    /// ChargeAttack 공격 종류를 지정하여 타격 판정을 실행함.
    /// </summary>
    public void ExecuteChargeAttackHitCheck(int combo, int hit)
    {
        ExecuteGenericHitRegistry(_hero.CombatData.chargeAttackSequences, combo, hit, "ChargeAttack", HeroCombatAttackType.ChargeAttack);
    }

    /// <summary>
    /// 평꾹 막타 데이터를 사용하여 ChargeAttack 종류로 타격을 실행함.
    /// </summary>
    public void ExecuteChargeAttackFinisherHitCheck(int hit)
    {
        ExecuteGenericHitRegistrySingle(
            _hero.CombatData.chargeAttackFinisherSequence, hit,
            "ChargeAttackFinisher", HeroCombatAttackType.ChargeAttack);
    }

    /// <summary>
    /// DashAttack 공격 종류를 지정하여 타격 판정을 실행함.
    /// </summary>
    public void ExecuteDashAttackHitCheck(int combo, int hit)
    {
        ExecuteGenericHitRegistry(_hero.CombatData.dashAttackSequences, combo, hit, "DashAttack", HeroCombatAttackType.DashAttack);
    }

    /// <summary>
    /// Skill 공격 종류를 지정하여 타격 판정을 실행함.
    /// </summary>
    public void ExecuteSkillHitCheck(int combo, int hit)
    {
        ExecuteGenericHitRegistry(_hero.CombatData.skillAttackSequences, combo, hit, "Skill", HeroCombatAttackType.Skill);
    }

    /// <summary>
    /// EXSkill 공격 종류를 지정하여 타격 판정을 실행함.
    /// </summary>
    public void ExecuteExSkillHitCheck(int combo, int hit)
    {
        ExecuteGenericHitRegistry(_hero.CombatData.exSkillAttackSequences, combo, hit, "EXSkill", HeroCombatAttackType.EXSkill);
    }

    /// <summary>
    /// 에너지 소모 없는 강특 막타를 EXSkill 종류로 판정함.
    /// </summary>
    /// <param name="hit"></param>
    public void ExecuteExSkillFinaleHitCheck(int hit)
    {
        // 리스트가 아니므로 combo 인덱스 없이 바로 hits에 접근
        ExecuteGenericHitRegistrySingle(_hero.CombatData.exSkillFinisherSequence, hit, "ExSkillFinisher", HeroCombatAttackType.EXSkill);
    }

    /// <summary>
    /// Ult 공격 종류를 지정하여 타격 판정을 실행함.
    /// </summary>
    public void ExecuteUltHitCheck(int combo, int hit)
    {
        ExecuteGenericHitRegistry(_hero.CombatData.ultAttackSequences, combo, hit, "Ult", HeroCombatAttackType.Ult);
    }

    public void ExecuteParryHitCheck(int combo, int hit)
    {
        //ExecuteGenericHitRegistry(_hero.CombatData.parrySequences, combo, hit, "Parry");
        // 패링은 공격시전자 단일 대상으로 그로기 처리되어야 하므로,
        // CombatManager가 책임을 가지도록 바뀜
    }

    /// <summary>
    /// ParryCounter 공격 종류를 지정하여 타격 판정을 실행함.
    /// </summary>
    public void ExecuteParryCounterHitCheck(int combo, int hit)
    {
        ExecuteGenericHitRegistry(_hero.CombatData.parryCounterSequences, combo, hit, "ParryCounter", HeroCombatAttackType.ParryCounter);
    }

    /// <summary>
    /// DodgeCounter 공격 종류를 지정하여 타격 판정을 실행함.
    /// </summary>
    public void ExecuteDodgeCounterHitCheck(int combo, int hit)
    {
        ExecuteGenericHitRegistry(_hero.CombatData.dodgeCounterSequences, combo, hit, "DodgeCounter", HeroCombatAttackType.DodgeCounter);
    }

    /// <summary>
    /// ChainAttack 공격 종류를 지정하여 타격 판정을 실행함.
    /// </summary>
    public void ExecuteChainAttackHitCheck(int combo, int hit)
    {
        ExecuteGenericHitRegistry(_hero.CombatData.chainAttackSequences, combo, hit, "ChainAttack", HeroCombatAttackType.ChainAttack);
    }

    /// <summary>
    /// QuickAssist 공격 종류를 지정하여 타격 판정을 실행함.
    /// </summary>
    public void ExecuteQuickAssistHitCheck(int combo, int hit)
    {
        ExecuteGenericHitRegistry(_hero.CombatData.quickAssistSequences, combo, hit, "QuickAssist", HeroCombatAttackType.QuickAssist);
    }

    /// <summary>
    /// AfterShock 공격 종류를 지정하여 타격 판정을 실행함.
    /// </summary>
    public void ExecuteAfterShockHitCheck(int combo, int hit)
    {
        ExecuteGenericHitRegistry(_hero.CombatData.AfterShockSequences, combo, hit, "AfterShock", HeroCombatAttackType.AfterShock);
    }

    #endregion


    #region [타격 데이터 추출]

    /// <summary>
    /// 애니메이션 이벤트의 Combo/Hit 인덱스로 모션과 타격 데이터를 선택함.
    /// 일반 공격은 해당 Hit의 판정 또는 발사를 요청하고,
    /// 설치형 모션은 최초 이벤트에서 전체 타격 일정을 가진 설치기 생성을 요청함.
    /// </summary>
    private void ExecuteGenericHitRegistry(List<HeroCombatDataSO.MotionSequenceData> sequenceList, int comboIndex, int hitIndex, string debugActionName,
        HeroCombatAttackType attackType)
    {
        if (_hero.CombatData == null || sequenceList == null || sequenceList.Count == 0 ||
            comboIndex < 0 || hitIndex < 0) return;

        // 모션(Combo) 인덱스 안전 범위 보정
        if (comboIndex >= sequenceList.Count) comboIndex = sequenceList.Count - 1;

        HeroCombatDataSO.MotionSequenceData currentMotion = sequenceList[comboIndex];

        // 모션에 타격 데이터가 없으면 경고를 출력하고 처리를 중단함.
        if (currentMotion.hits == null || currentMotion.hits.Count == 0)
        {
            Debug.LogWarning($"[{debugActionName}] {currentMotion.motionName}에 세부 Hit 데이터가 존재하지 않습니다.");
            return;
        }

        /// <summary>
        /// PlacedArea 우선 처리
        /// 첫 Hit가 PlacedArea이면 모션 전체를 설치형으로 처리함.
        /// PlacedArea 방식의 공격은 PlacedAreaController 컴포넌트를 통해 자체적으로 전달받은 데이터를 기준으로 판정 요청.
        /// 전체 Hit의 방식 검증과 타격 일정 구성은 ExecutePlacedAreaMotion에서 수행함.
        /// hits가 하나일 때 후속 hitIndex를 먼저 보정하면 0이 되어 설치기를 다시 생성하므로,
        /// 인덱스 보정 전에 원래 이벤트 인덱스로 설치 여부를 판단함.
        /// </summary>
        if (currentMotion.hits[0].hitDetectionType == HitDetectionType.PlacedArea)
        {
            // 최초 이벤트에서 전체 타격 일정 전달을 요청함.
            // 이후 타격은 PlacedAreaController가 시간에 따라 실행하므로 후속 로직은 무시.
            if (hitIndex == 0) ExecutePlacedAreaMotion(currentMotion, attackType, comboIndex);
            return;
        }

        /// <summary>
        /// MeleeOverlap & Projectile 차선 처리
        /// </summary>
        if (hitIndex >= currentMotion.hits.Count) hitIndex = currentMotion.hits.Count - 1;

        // 새 모션의 첫 HitFrame에서 Impulse 장부 초기화
        if (hitIndex == 0)
        {
            _hasPlayedHitImpulseForCurrentMotion = false;
        }

        // 타격 타수별 구조체 데이터 초기화
        HeroCombatDataSO.HitData currentHitData = currentMotion.hits[hitIndex];

        // CombatEventContext 인스턴스 생성 및 데이터 초기화
        CombatEventContext contextPack = CreateHitContext(currentMotion, currentHitData, attackType, comboIndex);

        Debug.Log($"[HitChecker 판정완료] {debugActionName} -> 모션: {currentMotion.motionName} | 타수: {hitIndex + 1}/{currentMotion.hits.Count}타 ({currentHitData.hitName})");

        // HitData에 설정된 물리 판정 방식으로 전달
        bool isHitSuccess = ExecuteHitDetection(
            contextPack,
            currentHitData,
            currentMotion);

        // 근접 공격은 실제 타격 성공 시 Impulse를 요청
        // 투사체는 ExecuteHitDetection 내부에서 발사 성공 시 요청
        // 설치형은 별도 모션 경로에서 처리되어 이 위치에 도달하지 않음.
        TryRequestHitImpulse(
            isHitSuccess,
            currentHitData.hitType);
        // 해당 Camera Hit Impulse 연출처리 콜백으로 했으면 더 좋았을듯?

    }

    /// <summary>
    /// 단일 모션의 타격 데이터를 지정된 공격 종류로 판정함.
    /// </summary>
    /// <param name="motionData"></param>
    /// <param name="hitIndex"></param>
    /// <param name="debugActionName"></param>
    private void ExecuteGenericHitRegistrySingle(HeroCombatDataSO.MotionSequenceData motionData, int hitIndex, string debugActionName,
        HeroCombatAttackType attackType)
    {
        // 1. 방어 코드: 데이터 널 체크 및 히트 인덱스 범위 초과 검사
        if (motionData.hits == null || motionData.hits.Count == 0 || hitIndex < 0)
        {
            Debug.LogWarning($"[{debugActionName}] 판정 데이터가 비어있거나 Hit 인덱스가 범위를 벗어났습니다. (HitIndex: {hitIndex})");
            return;
        }

        if (motionData.hits[0].hitDetectionType == HitDetectionType.PlacedArea)
        {
            if (hitIndex == 0) ExecutePlacedAreaMotion(motionData, attackType);
            return;
        }

        if (hitIndex >= motionData.hits.Count) return;

        if (hitIndex == 0)
        {
            _hasPlayedHitImpulseForCurrentMotion = false;
        }

        // 2. 현재 타격 프레임에 매칭되는 세부 HitData 추출
        var currentHitData = motionData.hits[hitIndex];

        // 3. 전투 이벤트 콘텍스트 팩토리 조립
        CombatEventContext contextPack = CreateHitContext(motionData, currentHitData, attackType);

        // 4. 디버그 로그 출력 (현재 몇 타 중 몇 타가 터지는지 시각화)
        Debug.Log($"[HitChecker 판정완료] {debugActionName} -> 모션: {motionData.motionName} | 타수: {hitIndex + 1}/{motionData.hits.Count}타 ({currentHitData.hitName})");

        // 5. HitData에 설정된 물리 판정 방식으로 전달
        bool isHitSuccess = ExecuteHitDetection(
            contextPack,
            currentHitData,
            motionData);

        // 근접 공격은 실제 타격 성공 시 Impulse를 요청함.
        // 투사체는 ExecuteHitDetection 내부에서 발사 성공 시 요청함.
        // 설치형은 별도 모션 경로에서 처리되어 이 위치에 도달하지 않음.
        TryRequestHitImpulse(
            isHitSuccess,
            currentHitData.hitType);
    }

    /// <summary>
    /// 모션의 공통 범위와 개별 타격 설정으로 독립적인 전투 정보를 생성함.
    /// </summary>
    private CombatEventContext CreateHitContext(
        HeroCombatDataSO.MotionSequenceData motionData,
        HeroCombatDataSO.HitData hitData,
        HeroCombatAttackType attackType,
        int comboIndex = -1)
    {
        return new CombatEventContext(
            _hero, hitData.hitDetectionType, motionData.hitRadius,
            hitData.damageMultiplier, hitData.dazeMultiplier, hitData.hitType,
            hitData.canExtendGroggy, hitData.groggyExtendTime,
            hitData.payBackEnergy, hitData.payBackDecibel, hitData.canTriggerChainAttack,
            hitData.hitMarkData, hitData.hitReactionDirectionMode, attackType,
            hitImpactSFX: hitData.hitImpactSFX,
            comboIndex: attackType == HeroCombatAttackType.NormalAttack ? comboIndex : -1);
    }

    #endregion


    #region [설치형 공격 실행]

    /// <summary>
    /// 전체 타격 정보와 누적 실행 시간을 구성하여 설치기 하나를 생성 요청함.
    /// </summary>
    private void ExecutePlacedAreaMotion(
        HeroCombatDataSO.MotionSequenceData motionData,
        HeroCombatAttackType attackType,
        int comboIndex = -1)
    {
        if (_areaHitDetector == null)
        {
            Debug.LogError($"[{name}] AreaHitDetector가 연결되지 않았습니다.", this);
            return;
        }

        float hitTime = motionData.placedAreaFirstHitDelay;
        if (!IsValidPlacedAreaTime(hitTime))
        {
            Debug.LogError($"[{name}] {motionData.motionName}의 최초 타격 지연이 올바르지 않습니다.", this);
            return;
        }

        int hitCount = motionData.hits.Count;
        var hitContexts = new List<CombatEventContext>(hitCount);
        var hitTimes = new List<float>(hitCount);
        for (int i = 0; i < hitCount; i++)
        {
            HeroCombatDataSO.HitData hitData = motionData.hits[i];
            if (hitData.hitDetectionType != HitDetectionType.PlacedArea)
            {
                Debug.LogError($"[{name}] {motionData.motionName}의 {i + 1}타가 설치형이 아닙니다.", this);
                return;
            }

            if (i > 0)
            {
                if (!IsValidPlacedAreaTime(hitData.placedAreaHitInterval))
                {
                    Debug.LogError($"[{name}] {motionData.motionName}의 {i + 1}타 간격이 올바르지 않습니다.", this);
                    return;
                }
                hitTime += hitData.placedAreaHitInterval;
            }

            if (!IsValidPlacedAreaTime(hitTime))
            {
                Debug.LogError($"[{name}] {motionData.motionName}의 누적 타격 시간이 올바르지 않습니다.", this);
                return;
            }

            hitContexts.Add(CreateHitContext(motionData, hitData, attackType, comboIndex));
            hitTimes.Add(hitTime);
        }

        _areaHitDetector.Place(motionData, hitContexts, hitTimes);
    }

    /// <summary>
    /// 설치형 타격 시간이 유한한 0 이상의 값인지 확인함.
    /// </summary>
    private bool IsValidPlacedAreaTime(float value)
    {
        return value >= 0f && !float.IsNaN(value) && !float.IsInfinity(value);
    }

    #endregion


    #region [판정 방식별 실행]

    /// <summary>
    /// 개별 Hit의 판정 방식에 따라 근접 판정 또는 투사체 발사를 요청함.
    /// 설치형은 상위 Registry에서 모션 전체를 별도로 처리하므로 이 경로에서는 허용하지 않음.
    /// </summary>
    /// <returns>근접 판정의 실제 적중 성공 여부를 반환함. 투사체 발사와 오류 경로는 false를 반환함.</returns>
    private bool ExecuteHitDetection(
        CombatEventContext context,
        HeroCombatDataSO.HitData hitData,
        HeroCombatDataSO.MotionSequenceData motionData)
    {
        switch (hitData.hitDetectionType)
        {
            case HitDetectionType.MeleeOverlap:
                // 연출 성공 여부는 물리 판정 및 적중 반환값에 영향을 주지 않음.
                if (hitData.hitVFXPrefab != null)
                {
                    Vector3 position = transform.position + transform.forward * motionData.hitForwardOffset
                        + transform.rotation * hitData.hitVFXOffset;
                    ParticleVFXManager.Instance?.PlayAttackVFX(
                        hitData.hitVFXPrefab, position, transform.rotation);
                }

                if (_meleeHitDetector == null)
                {
                    Debug.LogError(
                        $"[{name}] MeleeHitDetector가 연결되지 않았습니다.");
                    return false;
                }

                return _meleeHitDetector.Execute(
                    context,
                    motionData.hitRadius,
                    motionData.hitForwardOffset);

            case HitDetectionType.ProjectileCollision:
                if (_projectileLauncher == null)
                {
                    Debug.LogError(
                        $"[{name}] ProjectileLauncher가 연결되지 않았습니다.");
                    return false;
                }

                // 투사체는 발사 성공 프레임에 역경직과 온필드 카메라 흔들림을 요청한다.
                if (_projectileLauncher.Launch(context))
                {
                    CombatManager.Instance?.TriggerHeroHitStop(_hero);
                    TryRequestHitImpulse(true, hitData.hitType);
                }

                // 이 반환값은 발사 성공이 아니라 실제 피해 적용 성공을 의미한다.
                // 투사체의 발사 연출은 위에서 처리했으므로,
                // 바깥쪽 타격 성공 기반 Impulse 요청에는 false를 전달한다.
                return false;

            case HitDetectionType.PlacedArea:
                // 정상적인 설치형 모션은 상위 Registry에서 처리되어 이 분기에 도달하지 않음.
                // 첫 Hit는 일반 타격인데 후속 Hit만 PlacedArea인 혼합 구성을 오류로 차단함.
                // 여기서 설치기를 생성하면 모션 전체 일정이 개별 Hit마다 중복 등록될 수 있음.
                Debug.LogError($"[{name}] 설치형 타격은 모션 전체가 PlacedArea여야 합니다.", this);
                return false;

            default:
                Debug.LogError(
                    $"[{name}] 지원하지 않는 HitDetectionType: " +
                    $"{hitData.hitDetectionType}");

                return false;
        }
    }


    #endregion

    /// <summary>
    /// 유효 타격 또는 투사체 발사 성공 시 온필드 Hero의 Hit Impulse를 요청한다.
    /// 현재 모션에서 한 번만 발생하며 오프필드 공격은 제외한다.
    /// </summary>
    private void TryRequestHitImpulse(
        bool canPlayImpulse,
        HitType hitType)
    {
        if (!canPlayImpulse) return;
        if (_hasPlayedHitImpulseForCurrentMotion) return;

        PartyStateManager partyStateManager =
            PartyStateManager.Instance;

        if (partyStateManager == null ||
            partyStateManager.CurrentHero != _hero)
        {
            return;
        }

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

}
