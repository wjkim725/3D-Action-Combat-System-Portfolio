using UnityEngine;

// HitChecker -> CombatManager 로 전달될때 쓰일 데이터 클래스
public class CombatEventContext
{
    // 공격자 인스턴스
    public IAttacker Attacker { get; private set;}  
    // 피격자 인스턴스 - Target은 루프 돌면서 갈아끼우므로 set
    public IDamageable Target { get; set; }


    // 연출용 타격 위치 및 방향
    public Vector3 ImpactPoint { get; set; }
    public Vector3 ImpactDirection { get; set; }
    public Vector3 SurfaceNormal { get; set; }
    public Vector3 SlashDirection { get; set; }
    public bool HasImpactData { get; set; }
    public HitMarkDataSO HitMarkData { get; private set; }


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

    // 공통 전투 데이터 생성자
    public CombatEventContext(
        IAttacker attacker,
        float damageMultiplier,
        float dazeMultiplier,
        HitType hitType,
        bool canExtendGroggy,
        float groggyExtendTime,
        float payBackEnergy,
        float payBackDecibel,
        bool canTriggerChainAttack)
    {
        Attacker = attacker;
        DamageMultiplier = damageMultiplier;
        DazeMultiplier = dazeMultiplier;
        HitType = hitType;
        CanExtendGroggy = canExtendGroggy;
        GroggyExtendTime = groggyExtendTime;
        PayBackEnergy = payBackEnergy;
        PayBackDecibel = payBackDecibel;
        CanTriggerChainAttack = canTriggerChainAttack;
    }

    // HitMark 데이터를 사용하는 공격용 확장 생성자
    public CombatEventContext(
        IAttacker attacker,
        float damageMultiplier,
        float dazeMultiplier,
        HitType hitType,
        bool canExtendGroggy,
        float groggyExtendTime,
        float payBackEnergy,
        float payBackDecibel,
        bool canTriggerChainAttack,
        HitMarkDataSO hitMarkData)
        : this(
            attacker,
            damageMultiplier,
            dazeMultiplier,
            hitType,
            canExtendGroggy,
            groggyExtendTime,
            payBackEnergy,
            payBackDecibel,
            canTriggerChainAttack)
    {
        HitMarkData = hitMarkData;
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