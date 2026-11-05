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
    ChargeAttack = 22,   // 강공격 (평꾹)
    Skill = 8,      // 스킬 공격
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

    InActive,       // 오프필드 대기
    AfterShock,     // 여진 공격

    Dead,           // 사망
    Entry = 23,     // 전투씬 입장 (기존 직렬화 상태 번호 유지)
}

public enum HeroCombatAttackType
{
    None = 0,
    NormalAttack,
    ChargeAttack,
    DashAttack,
    Skill,
    EXSkill,
    Ult,
    AfterShock,
    DodgeCounter,
    ParryCounter,
    QuickAssist,
    ChainAttack,
}

public enum HeroMechanicResourceType
{
    None,   // 표시 없음
    Stack,  // 스택형
    Gauge,  // 게이지형
    Toggle, // On/Off 토글형
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

/// <summary>
/// 적의 자율 공격과 이동 허용 범위를 구분함.
/// 피격, 그로기와 사망 반응은 모든 모드에서 유지함.
/// </summary>
public enum EnemyAIMode
{
    Normal = 0,
    Passive = 1,
    Stationary = 2
}

public enum EnemyState
{
    Idle,           // 기본 대기 상태
    CombatIdle,     // Hero와 전투 중 의도적으로 가만히 서 있는 행동
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

/// <summary>
/// 전투 대기 행동
/// 다음에 다음에 무엇을 실행할지 기억하는 값
/// </summary>
public enum EnemyCombatWaitType
{
    Orbit,
    CombatIdle
}

/// <summary>
/// 피격의 경직 정도
/// </summary>
public enum HitType
{
    None = 0,
    Light = 1,
    Heavy = 2,
    Knockback = 3,
    Parry = 4,
}
/// <summary>
/// 피격자의 로컬 좌표를 기준으로 한 반응 방향.
/// Front는 좌우 편향이 없는 중앙 피격 반응을 의미한다.
/// </summary>
public enum HitReactionDirection
{
    Front = 0,
    Left = 1,
    Right = 2
}
/// <summary>
/// 공격별 피격 반응 방향 계산 방식
/// </summary>
public enum HitReactionDirectionMode
{
    Auto = 0,                  // 판정 실행 객체가 기본 계산 방식을 선택
    AttackMotion = 1,          // 무기 궤적, 투사체 속도 등 공격 진행 방향
    RadialFromOrigin = 2,      // 폭발·장판 중심에서 피격 대상을 향하는 방향
    AttackerToTarget = 3,      // 공격자에서 피격 대상을 향하는 방향
    FixedFront = 4             // 방향 계산 없이 Front 반응으로 고정
}
/// <summary>
/// 공격이 대상을 타격하는 판정 방식
/// </summary>
public enum HitDetectionType
{
    MeleeOverlap = 0,           // 공격 순간 무기 또는 캐릭터 주변의 범위를 판정
    ProjectileCollision = 1,    // 발사한 투사체가 대상과 충돌할 때 판정
    PlacedArea = 2,            // 지정 위치에 설치한 공격이 발동할 때 범위를 판정
}
/// <summary>
/// Enemy가 차단할 수 있는 피격 경직의 최대 등급.
/// 기본 경직 저항과 공격 중 슈퍼아머에 공통으로 사용한다.
/// </summary>
public enum HitReactionResistanceLevel
{
    /// <summary>
    /// 경직 저항 없음.
    /// Light와 Heavy 경직을 모두 허용한다.
    /// </summary>
    None = 0,

    /// <summary>
    /// Light 이하의 경직을 차단한다.
    /// Heavy 경직은 허용한다.
    /// </summary>
    Light = 1,

    /// <summary>
    /// Light와 Heavy 경직을 모두 차단한다.
    /// 추후 Knockback 등 더 높은 경직은 허용할 수 있다.
    /// </summary>
    Heavy = 2,

    /// <summary>
    /// 일반적인 모든 피격 경직을 차단한다.
    /// 사망과 Groggy 같은 강제 상태 전환은 별도로 처리한다.
    /// </summary>
    Max = 3
}
/// <summary>
/// 타격당한 적의 오브젝트 위에 남는 잔흔 VFX 타입
/// </summary>
public enum HitMarkType
{
    Decal = 0,
    Arrow = 1   // 오브젝트
}


public enum BuffValueMode
{
    Flat,
    Percent,
}

public enum BuffDurationMode
{
    Timed,
    External,
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
    Next,
    Prev
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
    public UnityEngine.Vector3 TargetPosition { get; }
    public UnityEngine.Vector3 TargetForward { get; }
    public SwapDirection Direction { get; }

    public QuickAssistContext(
        int sessionId,
        Hero triggerHero,
        Hero assistHero,
        Enemy assistTarget,
        SwapDirection direction,
        UnityEngine.Vector3 targetPosition,
        UnityEngine.Vector3 targetForward)
    {
        SessionId = sessionId;
        TriggerHero = triggerHero;
        AssistHero = assistHero;
        AssistTarget = assistTarget;
        TargetPosition = targetPosition;
        TargetForward = targetForward;
        Direction = direction;
    }
}
