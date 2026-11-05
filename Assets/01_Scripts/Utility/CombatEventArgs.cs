using UnityEngine;

// HitChecker -> CombatManager 로 전달될때 쓰일 데이터 클래스
public class CombatEventContext
{
    // 공격자 인스턴스
    public IAttacker Attacker { get; private set; }
    // 공격 생성 시 확정한 판정 방식. 적중 시 공격자의 현재 상태와 무관하게 유지.
    public HitDetectionType HitDetectionType { get; private set; }
    // 판정 생성 시 확정한 모션 반경. 투사체는 0이면 단일 타격, 양수이면 폭발 반경으로 사용함.
    public float HitRadius { get; private set; }
    // 공격 생성 시 확정한 Hero 공격 종류. 적중 시점의 상태와 무관하게 유지함.
    public HeroCombatAttackType AttackType { get; private set; }
    // 공격 생성 시점의 콤보 모션 인덱스. 해당 없는 공격은 -1을 사용함.
    public int ComboIndex { get; private set; }
    // 피격자 인스턴스 - Target은 루프 돌면서 갈아끼우므로 set
    public IDamageable Target { get; set; }


    // 타격 지점 기반 공간 정보
    public Vector3 ImpactPoint { get; set; }
    public Vector3 ImpactDirection { get; set; }
    public Vector3 SurfaceNormal { get; set; }
    public Vector3 SlashDirection { get; set; }
    public bool HasImpactData { get; set; }

    // 피격 반응 방향 정보
    public HitReactionDirectionMode ReactionDirectionMode { get; private set; }
    public Vector3 ReactionDirection { get; private set; }
    public bool HasReactionDirection { get; private set; }

    public HitMarkDataSO HitMarkData { get; private set; }
    // 대상에게 실제 피해 적용이 성공했을 때 재생할 효과음.
    public AudioCueSO HitImpactSFX { get; private set; }

    // 동일한 타격에서 적중음 재생 요청이 수락됐는지 기록함.
    public bool HasPlayedHitImpactSFX { get; private set; }

    /// <summary>
    /// 해당 타격의 적중음 재생 요청이 수락됐음을 기록함.
    /// </summary>
    public void MarkHitImpactSFXPlayed()
    {
        HasPlayedHitImpactSFX = true;
    }


    // 공격 데이터(SO)에서 가져온 순수 계수 및 기믹 정보
    public float DamageMultiplier { get; private set; } // 데미지 계수
    public float DazeMultiplier { get; private set; }   // 그로기 계수
    public HitType HitType { get; private set; }        // 경직 타입
    public bool CanExtendGroggy { get; private set; }   // 그로기 연장 기믹 유무
    public float GroggyExtendTime { get; private set; } // 그로기 연장 시간
    public float PayBackEnergy { get; private set; }    // 에너지 페이백
    public float PayBackDecibel { get; private set; }   // 데시벨 페이백
    // 이 타격이 그로기 상태의 적에게 Chain Attack을 열 수 있는지 여부
    public bool CanTriggerChainAttack { get; private set; }

    /// <summary>
    /// 공격 종류와 판정 방식 및 전투 데이터를 저장함.
    /// </summary>
    public CombatEventContext(
        IAttacker attacker,
        HitDetectionType hitDetectionType,
        float hitRadius,
        float damageMultiplier,
        float dazeMultiplier,
        HitType hitType,
        bool canExtendGroggy,
        float groggyExtendTime,
        float payBackEnergy,
        float payBackDecibel,
        bool canTriggerChainAttack,
        HitReactionDirectionMode hitReactionDirectionMode =
            HitReactionDirectionMode.Auto,
        HeroCombatAttackType attackType = HeroCombatAttackType.None,
        AudioCueSO hitImpactSFX = null,
        int comboIndex = -1)
    {
        Attacker = attacker;
        AttackType = attackType;
        ComboIndex = comboIndex;
        HitDetectionType = hitDetectionType;
        HitRadius = hitRadius;
        DamageMultiplier = damageMultiplier;
        DazeMultiplier = dazeMultiplier;
        HitType = hitType;
        CanExtendGroggy = canExtendGroggy;
        GroggyExtendTime = groggyExtendTime;
        PayBackEnergy = payBackEnergy;
        PayBackDecibel = payBackDecibel;
        CanTriggerChainAttack = canTriggerChainAttack;
        ReactionDirectionMode = hitReactionDirectionMode;
        HitImpactSFX = hitImpactSFX;
    }

    /// <summary>
    /// 공격 종류와 판정 방식 및 전투 데이터에 HitMark 정보를 함께 저장함.
    /// </summary>
    public CombatEventContext(
        IAttacker attacker,
        HitDetectionType hitDetectionType,
        float hitRadius,
        float damageMultiplier,
        float dazeMultiplier,
        HitType hitType,
        bool canExtendGroggy,
        float groggyExtendTime,
        float payBackEnergy,
        float payBackDecibel,
        bool canTriggerChainAttack,
        HitMarkDataSO hitMarkData,
        HitReactionDirectionMode hitReactionDirectionMode =
            HitReactionDirectionMode.Auto,
        HeroCombatAttackType attackType = HeroCombatAttackType.None,
        AudioCueSO hitImpactSFX = null,
        int comboIndex = -1)
        : this(
            attacker,
            hitDetectionType,
            hitRadius,
            damageMultiplier,
            dazeMultiplier,
            hitType,
            canExtendGroggy,
            groggyExtendTime,
            payBackEnergy,
            payBackDecibel,
            canTriggerChainAttack,
            hitReactionDirectionMode,
            attackType,
            hitImpactSFX,
            comboIndex)
    {
        HitMarkData = hitMarkData;
    }

    /// <summary>
    /// 판정 실행 객체가 충돌 순간 계산한 월드 방향을 저장
    /// </summary>
    public void SetReactionDirection(Vector3 direction)
    {
        if (direction.sqrMagnitude < 0.0001f)
        {
            ClearReactionDirection();
            return;
        }

        ReactionDirection = direction.normalized;
        HasReactionDirection = true;
    }

    /// <summary>
    /// Context를 다른 대상에게 재사용하기 전에 방향 정보를 초기화
    /// </summary>
    public void ClearReactionDirection()
    {
        ReactionDirection = Vector3.zero;
        HasReactionDirection = false;
    }
}


// CombatManager에서 연산을 끝내고 IDamageable 에게 전달될 데이터 구조체
public struct HitInfo
{
    public IAttacker attacker;      // 공격 주체
    public float damage;            // 최종 데미지
    public bool isCrit;             // 치명타 여부
    public float dazeAmount;        // 그로기 누적량
    public HitType hitType;         // 경직 타입
    public bool canExtendGroggy;    // 그로기 연장 기믹 유무
    public float groggyExtendTime;  // 그로기 연장 수치
    public float payBackEnergy;     // 타격 성공시 채워줄 에너지
    public float payBackDecibel;    // 타격 성공시 채워줄 데시벨
    public Vector3 reactionDirection;   // 피격 반응에 사용할 월드 방향
    public bool hasReactionDirection;   // 유효한 피격 방향 존재 여부

    // CanTriggerChainAttack 는 CombatManager / ChainAttackManager - ProcessHit 단에서 사용되고
    // Enemy or Hero - TakeDamage 에서는 사용되지 않으므로 선언 X
    // 피격자에게 처리를 요구하는 데이터가 아님.
}

/// <summary>
/// Enemy의 전투 타격 정보
/// -> 사용X / CombatEventContext 의 속성을 비워서 사용
/// </summary>
//public class CombatEventContextByEnemy
//{
//    // 공격자 인스턴스
//    public IAttacker Attacker { get; private set; }
//    // 피격자 인스턴스 - Target은 루프 돌면서 갈아끼우므로 set
//    public IDamageable Target { get; set; }

//    // SO에서 가져올 공격의 '순수 계수 데이터'
//    public float DamageMultiplier { get; private set; } // 데미지 계수
//    public float DazeMultiplier { get; private set; }   // 그로기 계수
//    public HitType HitType { get; private set; }        // 경직 타입
//    public bool CanExtendGroggy { get; private set; }   // 그로기 연장 기믹 유무
//    public float GroggyExtendTime { get; private set; } // 그로기 연장 시간
//    public float PayBackEnergy { get; private set; }    // 에너지 페이백
//    public float PayBackDecibel { get; private set; }   // 데시벨 페이백

//    public CombatEventContextByEnemy(IAttacker attacker, float damageMultiplier, float dazeMultiplier,
//                              HitType hitType, bool canExtendGroggy, float groggyExtendTime,
//                              float payBackEnergy, float payBackDecibel)
//    {
//        Attacker = attacker;
//        DamageMultiplier = damageMultiplier;
//        DazeMultiplier = dazeMultiplier;
//        HitType = hitType;
//        CanExtendGroggy = canExtendGroggy;
//        GroggyExtendTime = groggyExtendTime;
//        PayBackEnergy = payBackEnergy;
//        PayBackDecibel = payBackDecibel;
//    }
//}
