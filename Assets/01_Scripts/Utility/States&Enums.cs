using UnityEngine;

public enum HeroState
{
    Idle,           // 일반
    ConvertCombat,  // 무기 교대

    Walk,           // 걷기
    Run,            // 달리기
    Sprint,         // 전력질주

    Dodge,          // 회피
    DodgeCounter,   // 회피 반격 (극한 회피 이후에 발동되는 전용 상태)

    Attack,         // 일반 공격
    Skill,          // 스킬 공격
    Ult,            // 궁극기 공격

    SwapIn,         // 교대 In
    SwapOut,        // 교대 Out
    ParryIn,        // 패링 In
    ParryOut,       // 패링 교대 Out
    ParryCounter,   // 패링 반격
    QuickAssist,    // 빠른 지원 진입
    ChainAttack,    // 콤보 스킬

    Hit,            // Light / Heavy
    Knockback,      // Knockback 전용

    InActive,       // 오프필드
    Dead,           // 사망
}

public enum AnimMoveDirectionType
{
    Keep = 0,       // 기존의 AnimMoveDirection 유지

    Forward = 1,
    Backward = 2,
    Left = 3,
    Right = 4,

    Target = 5,
    TargetLeft = 6,
    TargetRight = 7
}

public enum EnemyState
{
    Idle,
    Chase,
    Orbit,
    Attack,
    Hit,
    Groggy,
    Dead,
}

public enum EnemyRank
{
    Normal,
    Elite,
    Boss
}



public enum HitType
{
    None = 0,
    Light = 1,
    Heavy = 2,
    Knockback = 3,
    Parry = 4,
}

public enum BuffType
{
    AttackBoost,
    ImpactBoost,
    CriticalBoost,
    CirticalHitDamageBoost,
}

public enum ChainAttackPhase
{
    Inactive,
    Selecting,
    Executing
}

public enum DefenseWindowType
{
    DodgeOnly = 1,
    DodgeAndParry = 2
}

public enum SwapDirection
{
    Next = 0,
    Prev = 1
}

public enum QuickAssistCloseReason
{
    Consumed,       // 소모
    Expired,        // 시간 만료
    TriggerHeroRecovered,   // Knockback 피격된 Hero 가 회복
    TriggerHeroDead,        // 피격 Hero 사망
    ContextInvalidated,     // 저장된 실행 정보가 더 이상 유효하지 않음
    Replaced                // 새로운 QuickAssistWindow 열림
}

public readonly struct QuickAssistContext
{
    public int SessionId { get; }
    public Hero TriggerHero { get; }
    public Hero AssistHero { get; }
    public Enemy AssistTarget { get; }
    public SwapDirection Direction { get; }

    public QuickAssistContext(
        int sessionId,
        Hero triggerHero,
        Hero assistHero,
        Enemy assistTarget,
        SwapDirection direction)
    {
        SessionId = sessionId;
        TriggerHero = triggerHero;
        AssistHero = assistHero;
        AssistTarget = assistTarget;
        Direction = direction;
    }
}