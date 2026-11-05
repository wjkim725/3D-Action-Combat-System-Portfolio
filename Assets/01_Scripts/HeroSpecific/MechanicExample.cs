using UnityEngine;

/// <summary>
/// 신규 Hero 추가시, 전용 Mechanic 스크립트 복붙용
/// </summary>
public class MechanicExample : MonoBehaviour, IHeroMechanic
{
    public Hero Hero => _hero;
    public HeroState CurrentState => _hero.CurrentState;
    public Animator Animator => _hero.Animation.Animator;
    public HeroMechanicResourceType ResourceType => _resourceType;
    public float CurrentResource => _currentResource;
    public float MaxResource => _maxResource;
    public float RequiredResource => _requiredResource;
    public bool IsCombatRotationLocked => false;
    public bool IsChargeAttackFinisherActive => false;

    /// <summary>
    /// 기본 예제에서는 전용 회피를 처리하지 않음.
    /// </summary>
    public bool TryEvadeHit(CombatEventContext context) => false;

    /// <summary>
    /// 기본 예제에서는 애니메이션 이벤트를 제한하지 않음.
    /// </summary>
    public bool CanProcessAnimationEvent(AnimationEvent animationEvent) => true;

    private Hero _hero;

    [Header("Mechanic Resource Settings")]
    [SerializeField] private HeroMechanicResourceType _resourceType;
    [SerializeField] private float _currentResource;
    [SerializeField] private float _maxResource;
    [SerializeField] private float _requiredResource;



    private void Awake()
    {
        _hero = GetComponent<Hero>();
    }

    #region [FSM 제어 영역]

    /// <summary>
    /// Hero가 새 상태에 진입할 때 Erika 전용 처리를 실행한다.
    /// </summary>
    public void EnterState(HeroState newState, bool isComboConnection)
    {
        switch (newState)
        {
            case HeroState.Idle:
                break;
            case HeroState.ConvertCombat:
                break;
            case HeroState.Walk:
                break;
            case HeroState.Run:
                break;
            case HeroState.Sprint:
                break;
            case HeroState.Dodge:
                break;
            case HeroState.DodgeCounter:
                break;
            case HeroState.Attack:
                break;
            case HeroState.Skill:
                break;
            case HeroState.Ult:
                break;
            case HeroState.SwapIn:
                break;
            case HeroState.SwapOut:
                break;
            case HeroState.ParryIn:
                break;
            case HeroState.ParryOut:
                break;
            case HeroState.ParryCounter:
                break;
            case HeroState.QuickAssist:
                break;
            case HeroState.ChainAttack:
                break;
            case HeroState.Hit:
                break;
            case HeroState.Knockback:
                break;
            case HeroState.InActive:
                break;
            case HeroState.AfterShock:
                break;
            case HeroState.Dead:
                break;
        }
    }

    /// <summary>
    /// 매프레임 Erika 전용 처리를 실행한다.
    /// </summary>
    public void UpdateState()
    {
        switch (CurrentState)
        {
            case HeroState.Idle:
                break;
            case HeroState.ConvertCombat:
                break;
            case HeroState.Walk:
                break;
            case HeroState.Run:
                break;
            case HeroState.Sprint:
                break;
            case HeroState.Dodge:
                break;
            case HeroState.DodgeCounter:
                break;
            case HeroState.Attack:
                break;
            case HeroState.Skill:
                break;
            case HeroState.Ult:
                break;
            case HeroState.SwapIn:
                break;
            case HeroState.SwapOut:
                break;
            case HeroState.ParryIn:
                break;
            case HeroState.ParryOut:
                break;
            case HeroState.ParryCounter:
                break;
            case HeroState.QuickAssist:
                break;
            case HeroState.ChainAttack:
                break;
            case HeroState.Hit:
                break;
            case HeroState.Knockback:
                break;
            case HeroState.InActive:
                break;
            case HeroState.AfterShock:
                break;
            case HeroState.Dead:
                break;
        }
    }

    /// <summary>
    /// Hero가 기존 상태를 벗어날 때 Erika 전용 정보를 정리한다.
    /// </summary>
    public void ExitState(HeroState currentState, bool preserveAnimMove = false)
    {
        switch (currentState)
        {
            case HeroState.Idle:
                break;
            case HeroState.ConvertCombat:
                break;
            case HeroState.Walk:
                break;
            case HeroState.Run:
                break;
            case HeroState.Sprint:
                break;
            case HeroState.Dodge:
                break;
            case HeroState.DodgeCounter:
                break;
            case HeroState.Attack:
                break;
            case HeroState.Skill:
                break;
            case HeroState.Ult:
                break;
            case HeroState.SwapIn:
                break;
            case HeroState.SwapOut:
                break;
            case HeroState.ParryIn:
                break;
            case HeroState.ParryOut:
                break;
            case HeroState.ParryCounter:
                break;
            case HeroState.QuickAssist:
                break;
            case HeroState.ChainAttack:
                break;
            case HeroState.Hit:
                break;
            case HeroState.Knockback:
                break;
            case HeroState.InActive:
                break;
            case HeroState.AfterShock:
                break;
            case HeroState.Dead:
                break;
        }
    }

    #endregion


    #region [애니메이션 제어 영역]


    #endregion


    #region [애니메이션 이벤트 핸들러 영역]


    #endregion


    #region [전용 Resource 관리 영역]


    #endregion


    #region [Movement 영역]


    #endregion


    #region [Rotation 영역]


    #endregion

}
