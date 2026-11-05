using UnityEngine;

/// <summary>
/// Eve의 개별 공격 클립과 공통 Hero 콤보 및 종료 처리를 연결함.
/// 스택 획득과 소비, 공격 효과는 후속 전투 구현에서 구성함.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Hero))]
public class EveMechanic : MonoBehaviour, IHeroMechanic
{
    private Hero _hero;
    private HeroAnimationEventHandler _animationEvents;

    [Header("Optional Parry Shield")]
    [SerializeField] private ParticleSystem _parryShield;
    [SerializeField] private ParticleSystem _parryShieldBreak;
    private bool _hasBrokenParryShield;



    #region [공통 계약 영역]

    public Hero Hero => _hero;
    public HeroState CurrentState => _hero.CurrentState;
    public Animator Animator => _hero.Animation.Animator;
    public HeroMechanicResourceType ResourceType => HeroMechanicResourceType.None;
    public float CurrentResource => 0f;
    public float MaxResource => 0f;
    public bool IsCombatRotationLocked => false;
    public bool IsChargeAttackFinisherActive => false;

    #endregion

    #region [초기화 영역]

    private void Awake()
    {
        _hero = GetComponent<Hero>();
        _animationEvents = GetComponent<HeroAnimationEventHandler>();
    }

    private void OnDisable()
    {
        StopParryShield();
        _parryShieldBreak?.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
    }

    #endregion

    #region [FSM 제어 영역]

    /// <summary>
    /// Hero가 새 상태에 진입할 때 Eve 전용 처리를 실행함.
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
            case HeroState.ChargeAttack:
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
                _hasBrokenParryShield = false;
                StopParryShield();
                _parryShieldBreak?.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
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
    /// 현재 상태에 따른 Eve 전용 갱신 진입점을 제공함.
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
            case HeroState.ChargeAttack:
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
    /// Hero가 기존 상태를 벗어날 때 Eve 전용 처리를 정리함.
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
            case HeroState.ChargeAttack:
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
                StopParryShield();
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


    #region [공격 모션 이벤트 영역]

    /// <summary>
    /// 궁극기 Burst 클립의 지정 프레임에서 정방향 빠른지원 창을 요청함.
    /// </summary>
    public void OnEveUltQuickAssistWindowOpen(AnimationEvent animationEvent)
    {
        if (CurrentState != HeroState.Ult || _hero.Status.IsDead) return;
        if (animationEvent == null || !animationEvent.isFiredByAnimator ||
            !CanProcessAnimationEvent(animationEvent)) return;
        if (animationEvent.animatorClipInfo.clip == null ||
            animationEvent.animatorClipInfo.clip.name != "Ult_Burst") return;
        if (PartyStateManager.Instance == null || PartyStateManager.Instance.CurrentHero != _hero) return;
        if (QuickAssistManager.Instance == null || LockOnManager.Instance == null) return;

        Transform target = LockOnManager.Instance.MagneticTarget;
        Enemy enemy = target != null ? target.GetComponentInParent<Enemy>() : null;
        if (enemy == null || !enemy.isActiveAndEnabled || enemy.Status == null || enemy.Status.IsDead) return;

        QuickAssistManager.Instance.TryOpen(_hero, enemy, SwapDirection.Next);
    }

    /// <summary>
    /// 현재 클립의 첫 타격만 공격 종류에 맞는 공통 HitFrame 처리로 전달함.
    /// </summary>
    public void OnEveExecuteFrame(AnimationEvent animationEvent)
    {
        if (animationEvent == null || !CanProcessAnimationEvent(animationEvent)) return;
        // Eve는 모션당 한 번만 타격하며 콤보 진입 시 공통 Registry가 타격 인덱스를 초기화함.
        if (_hero.ActionRegistry.CurrentHitIndex > 0) return;

        switch ((HeroCombatAttackType)animationEvent.intParameter)
        {
            case HeroCombatAttackType.NormalAttack:
                _animationEvents.OnAttackExecuteFrame();
                break;
            case HeroCombatAttackType.DashAttack:
                _animationEvents.OnDashAttackExecuteFrame();
                break;
            case HeroCombatAttackType.ChargeAttack:
                _animationEvents.OnChargeAttackExecuteFrame(animationEvent);
                break;
            case HeroCombatAttackType.Skill:
                _animationEvents.OnSkillExecuteFrame();
                break;
            case HeroCombatAttackType.EXSkill:
                _animationEvents.OnExSkillExecuteFrame();
                break;
            case HeroCombatAttackType.Ult:
                _animationEvents.OnUltExecuteFrame();
                break;
            case HeroCombatAttackType.DodgeCounter:
                _animationEvents.OnDodgeCounterExecuteFrame();
                break;
            case HeroCombatAttackType.ParryCounter:
                _animationEvents.OnParryCounterExecuteFrame();
                break;
            case HeroCombatAttackType.QuickAssist:
                _animationEvents.OnQuickAssistExecuteFrame();
                break;
            case HeroCombatAttackType.ChainAttack:
                _animationEvents.OnChainAttackExecuteFrame();
                break;
        }
    }

    /// <summary>
    /// 현재 평타 클립에서 다음 클릭을 예약할 수 있는 구간을 엶.
    /// </summary>
    public void OnEveComboInputStart(AnimationEvent animationEvent)
    {
        if (CurrentState != HeroState.Attack || !CanProcessAnimationEvent(animationEvent)) return;
        _animationEvents.OnComboInputStart();
    }

    /// <summary>
    /// 예약된 평타를 다음 개별 클립으로 연결하며 대시 공격은 2단으로 연결함.
    /// </summary>
    public void OnEveCheckCombo(AnimationEvent animationEvent)
    {
        if (CurrentState != HeroState.Attack || !CanProcessAnimationEvent(animationEvent)) return;

        if (_hero.Animation.IsDashAttack && _hero.ActionRegistry.IsComboReserved)
        {
            _hero.ActionRegistry.SetComboIndex(1);
            _hero.ActionRegistry.SetIsComboReserved(false);
            _hero.ActionRegistry.SetIsComboLinkable(false);
            _hero.ActionRegistry.CloseCancelWindow();
            _hero.ChangeState(HeroState.Attack, true);
            return;
        }

        // 다음 클립의 입력 구간이 열리기 전까지 이전 예약 창을 닫음.
        _hero.ActionRegistry.SetIsComboLinkable(false);
        _animationEvents.OnCheckCombo();
    }

    /// <summary>
    /// 이벤트에 지정된 Hero 상태와 현재 재생 클립이 일치할 때 후딜 캔슬을 허용함.
    /// </summary>
    public void OnEveCancelWindowOpen(AnimationEvent animationEvent)
    {
        if (!CanProcessAnimationEvent(animationEvent)) return;
        if (CurrentState != (HeroState)animationEvent.intParameter) return;

        _hero.ActionRegistry.OpenCancelWindow();
        if (CurrentState == HeroState.Skill)
        {
            _hero.SkillSystem.SetIsExSkillMode(false);
        }
    }

    #endregion


    #region [공통 판정 연결 영역]

    /// <summary>
    /// 전용 자동회피를 사용하지 않고 공통 피격 판정에 처리를 맡김.
    /// </summary>
    public bool TryEvadeHit(CombatEventContext context)
    {
        return false;
    }

    /// <summary>
    /// 직접 호출 또는 현재 재생 대상으로 유지되는 기본 레이어 상태의 이벤트만 허용함.
    /// </summary>
    public bool CanProcessAnimationEvent(AnimationEvent animationEvent)
    {
        if (animationEvent == null || !animationEvent.isFiredByAnimator) return true;
        int sourceHash = animationEvent.animatorStateInfo.fullPathHash;
        // 전이 중에는 빠져나가는 모션의 종료가 새 공격을 중단하지 않도록 도착 상태만 허용함.
        if (Animator.IsInTransition(0))
        {
            return sourceHash == Animator.GetNextAnimatorStateInfo(0).fullPathHash;
        }

        return sourceHash == Animator.GetCurrentAnimatorStateInfo(0).fullPathHash;
    }

    #endregion


    #region [패링 방벽 연출]

    /// <summary>
    /// 현재 Ready 클립의 이벤트로 방벽을 생성하며 반복 재생은 무시함.
    /// </summary>
    public void OnEveParryShieldReady(AnimationEvent animationEvent)
    {
        if (CurrentState != HeroState.ParryIn || animationEvent == null ||
            !animationEvent.isFiredByAnimator || !CanProcessAnimationEvent(animationEvent)) return;
        if (animationEvent.animatorClipInfo.clip.name != "Parry_Ready") return;
        if (_hasBrokenParryShield || _parryShield == null || _parryShield.isPlaying) return;
        PlayParryShield();
    }

    /// <summary>
    /// 현재 Impact 또는 NoAttack 클립에서 패링당 한 번 방벽을 파괴함.
    /// </summary>
    public void OnEveParryShieldBreak(AnimationEvent animationEvent)
    {
        if (CurrentState != HeroState.ParryIn || animationEvent == null ||
            !animationEvent.isFiredByAnimator || !CanProcessAnimationEvent(animationEvent)) return;
        string clipName = animationEvent.animatorClipInfo.clip.name;
        if (clipName != "Parry_Impact" && clipName != "Parry_End") return;
        if (_hasBrokenParryShield) return;
        _hasBrokenParryShield = true;
        BreakParryShield();
    }

    /// <summary>
    /// 이전 파편을 비우고 준비 상태의 방벽을 재생함.
    /// </summary>
    private void PlayParryShield()
    {
        if (_parryShield == null) return;
        StopParryShield();
        _parryShieldBreak?.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        _parryShield.Play(true);
    }

    /// <summary>
    /// 준비 방벽을 제거하고 성공 또는 대기 만료 시 파편을 재생함.
    /// </summary>
    private void BreakParryShield()
    {
        if (_parryShield == null) return;
        StopParryShield();
        _parryShieldBreak?.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        _parryShieldBreak?.Play(true);
    }

    /// <summary>
    /// 상태 중단 시 방벽을 비우며 이미 발생한 파편은 수명을 마치게 함.
    /// </summary>
    private void StopParryShield()
    {
        _parryShield?.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
    }

    

    #endregion
}
