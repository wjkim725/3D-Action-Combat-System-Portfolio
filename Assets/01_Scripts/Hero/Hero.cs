using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.Timeline;

public delegate void CombatStateChangedHandler(bool isCombat);

/// <summary>
/// 직접적인 Hero 상태 변경과 변경에 따른 변화는 여기서 직접 처리 (뇌 역할)
/// </summary>
public class Hero : MonoBehaviour, IDamageable, IAttacker
{
    private enum ParryResolveState { Preparing, Ready, Impact, NoAttack }

    // IsCombat 프로퍼티의 값이 변경될 때 사용될 이벤트
    public event CombatStateChangedHandler OnCombatStateChanged;
    // Hero의 사망/부활 처리 신호를 줄 이벤트
    public event System.Action<bool> OnDeathStateChanged;
    // Hero의 Sprint 진입/탈출 이벤트
    public event System.Action<bool> OnSprintStateChanged;
    // 회피 상태 진입 처리가 끝난 뒤 실제 회피 시작을 전달함. 입력 거절 시에는 발생하지 않음.
    public event System.Action<Hero> OnDodgeStarted;
    /// <summary>
    /// Hero의 한 번의 타격 판정이 한 대상 이상에게 피해를 적용하여
    /// 전투 자원 반환이 처리될 때 발생함.
    /// 같은 판정으로 여러 대상을 맞혀도 한 번만 발생하며,
    /// 다단히트 공격은 각 타격 판정이 성공할 때마다 발생할 수 있음.
    /// 공격 생성 시점의 공격 종류를 전달하며 캐릭터 전용 기믹에서 사용함.
    /// </summary>
    public event System.Action<HeroCombatAttackType> OnAttackHitConfirmed;


    #region [프로퍼티 영역]

    // 컴포넌트 및 스크립트 참조
    public IHeroMechanic Mechanic { get; private set; }
    public HeroAnimation Animation { get; private set; }
    public HeroAnimationEventHandler AnimationEventHandler { get; private set; }
    public HeroMovement Movement { get; private set; }
    public HeroHitChecker HitChecker { get; private set; }
    public HeroStatus Status { get; private set; }
    public HeroSkillSystem SkillSystem { get; private set; }
    public HeroActionRegistry ActionRegistry { get; private set; }
    public HeroSwapSystem SwapSystem { get; private set; }
    public HeroWeapon Weapon { get; private set; }
    public HeroVFX VFX { get; private set; }
    public HeroHitReaction HitReaction { get; private set; }
    public HeroAudio Audio { get; private set; }



    public HeroCombatDataSO CombatData => _combatData;
    public Transform CamPivot => _camPivot;
    public Transform EntryCameraAnchor => _entryCameraAnchor;
    public Light EntryKeyLight => _entryKeyLight;
    public Texture2D Portrait => _portrait;
    public Renderer[] BodyRenderers => _bodyRenderers;
    public Transform ParryCameraAnchorLeft => _parryCameraAnchorLeft;
    public Transform ParryCameraAnchorRight => _parryCameraAnchorRight;
    public HeroState CurrentState => _currentState;
    public bool IsParryImpactActive =>
        CurrentState == HeroState.ParryIn &&
        _parryResolveState == ParryResolveState.Impact;
    public Vector3 HeroAxis => _heroAxis;
    public LayerMask EnemyLayerMask => _enemyLayerMask;

    // 전투 연출 관련 프로퍼티
    public CinemachineCamera UltVcam => _ultVcam;
    public float UltCutsceneDuration => _ultCutsceneDuration;
    public TimelineAsset UltTimelineAsset => _ultTimelineAsset;

    // 입력 가능 여부 필드 및 프로퍼티
    private bool _isInputEnabled = true;
    public bool IsInputEnabled => _isInputEnabled;
    private bool _isEntryTimelineControlled;
    public bool CanReceiveInput =>
        _isInputEnabled && !IsInHitReaction;

    // 피격 상태 여부 프로퍼티
    public bool IsInHitReaction =>
        _currentState == HeroState.Hit || _currentState == HeroState.Knockback;

    // 현재 전투 여부 판단 프로퍼티
    public bool IsCombat
    {
        get => _isCombat;
        set
        {
            if (_isCombat == value) return;     // 전투 상태 바뀔때만 작동
            _isCombat = value;
            // 입장 중에는 판정값만 갱신하고 연출 자세는 유지함.
            if (_currentState == HeroState.Entry) return;
            Animation.SetIsCombat(_isCombat);

            if (CanReceiveInput)
            {
                OnCombatStateChanged?.Invoke(_isCombat);    // 이벤트 발생
            }
        }
    }

    public Enemy CounterTarget => _counterTarget;

    #endregion


    #region [인터페이스 구현 영역]

    // IDamageable
    IStatus IDamageable.Status => Status;
    public Transform DamageTextAnchor =>
        _camPivot != null ? _camPivot : transform;
    // IAttacker
    public Transform AttackerTransform => this.transform;
    IStatus IAttacker.Status => Status;

    #endregion


    // 해당 Hero의 각종 데이터 SO
    [Header("Combat Data Asset")]
    [SerializeField] private HeroCombatDataSO _combatData;

    [Header("Camera Pivot")]
    [SerializeField] private Transform _camPivot;

    [Header("Entry Presentation")]
    [SerializeField] private Transform _entryCameraAnchor;
    [SerializeField] private Light _entryKeyLight;

    [Header("Parry Camera Anchors")]
    [SerializeField]
    private Transform _parryCameraAnchorLeft;
    [SerializeField]
    private Transform _parryCameraAnchorRight;

    [Header("Hero Portrait Data")]
    [SerializeField] private Texture2D _portrait;

    [Header("Hero Renderer")]
    [SerializeField] private Renderer[] _bodyRenderers;

    [Header("Hero State")]
    [SerializeField] private HeroState _currentState;   // 현재 상태
    private HeroState _toState;                         // 변환될 상태 필드
    private HeroState _prevState;                       // 변환 이전의 상태 필드



    [Header("Enemy Layer Setting")]
    [SerializeField] private LayerMask _enemyLayerMask;

    [Header("Hero Axis")]
    private Vector3 _heroAxis = Vector3.zero;   // Hero 이동 축

    [Header("Hero Combat Presentation Settings")]
    [SerializeField] private CinemachineCamera _ultVcam;        // 궁극기 숏컷신
    [SerializeField] private float _ultCutsceneDuration = 1.5f; // 궁극기 연출 지속 시간 (초 단위)
    [SerializeField] private TimelineAsset _ultTimelineAsset;   // 궁극기 카메라 애니메이션 타임라인

    [Header("CombatModeConvert Settings")]
    [SerializeField] private bool _isCombat = false;


    [Header("Charging Input Settings")]
    private float _buttonHoldTime = 0.2f;           // 이 시간만큼 누르고 있으면 차징 상태로 인정
    private float _attackButtonPressStartTime;      // 공격 버튼을 누르기 시작한 시간
    private float _skillButtonPressStartTime;       // 스킬 버튼을 누르기 시작한 시간

    [Header("Counter Target")]
    [SerializeField]
    private Enemy _counterTarget;
    /// <summary>
    /// 이 변수의 시간보다 DefenseWindow가 열린후 생기는 선딜이 길면 문제 발생할수도 있음.
    /// 해당 이슈 발생시 다시 확인
    /// </summary>
    private float _parryReadyMaxDuration = 0.8f;    // Parry_Ready 이후 Impact 발동 안되는 경우 탈출 시간
    private float _parryReadyTimer;
    private ParryResolveState _parryResolveState = ParryResolveState.Preparing;

    [Header("Sprint Transition Settings")]
    [SerializeField] private float _sprintTransitionDuration = 1.5f;
    private float _runDurationTimer = 0f;


    private void Awake()
    {
        Mechanic = GetComponent<IHeroMechanic>();
        Animation = GetComponent<HeroAnimation>();
        AnimationEventHandler = GetComponent<HeroAnimationEventHandler>();
        Movement = GetComponent<HeroMovement>();
        HitChecker = GetComponent<HeroHitChecker>();
        Status = GetComponent<HeroStatus>();
        SkillSystem = GetComponent<HeroSkillSystem>();
        ActionRegistry = GetComponent<HeroActionRegistry>();
        SwapSystem = GetComponent<HeroSwapSystem>();
        Weapon = GetComponent<HeroWeapon>();
        VFX = GetComponent<HeroVFX>();
        HitReaction = GetComponent<HeroHitReaction>();
        Audio = GetComponent<HeroAudio>();

        
        if(Mechanic == null)
        {
            Debug.LogError(
                $"{name}: Hero의 Mechanic이 할당되지 않았습니다.",
                this
            );
        }
        if (_camPivot == null)
        {
            Debug.LogError(
                $"{name}: Hero의 CamPivot이 할당되지 않았습니다.",
                this
            );
        }
        
    }

    void Start()
    {
        if (InputManager.Instance == null)
        {
            Debug.LogError("InputManager Instance Error");
            return;
        }

        SubscribeInputEvents();
        // 먼저 적용된 입장·대기 상태와 무기 표시를 초기 Idle로 덮어쓰지 않음.
        if (_currentState == HeroState.Entry || _currentState == HeroState.InActive) return;

        // 시작 시 물리 연산 없이 안전하게 등 무기 노출 상태로 초기화
        if (Weapon != null)
        {
            Weapon.ChangeWeaponDisplayState(HeroWeapon.WeaponDisplayState.OutCombat);
        }

        EnterState(HeroState.Idle, false);
    }

    void Update()
    {
        UpdateInputAxis();          // 축 입력 관리
        UpdateMovementState();      // 이동 상태 관리 -> 상태만 변환 / 실질적 이동은 HeroMovement
        UpdateChargeCheck();
        Movement.UpdateDodgeCoolDownTimer();        // Dodge 타이머 관리

        UpdateState();
        Mechanic?.UpdateState();                    // 전용 메카닉 Update
        Movement.ApplyGravity();                   // 상태별 이동 이후 접지 및 낙하 처리
    }

    private void OnDisable()
    {
        CombatManager.Instance?.CancelParryAssistRequest(this);
    }

    private void OnDestroy()
    {
        UnsubscribeInputEvents();
    }

    #region [초기화 및 이벤트 구독 영역]

    private void SubscribeInputEvents()
    {
        var input = InputManager.Instance;

        input.OnDashPerformed += HandleDashPerformedInput;
        input.OnDashStarted += HandleDashStartedInput;
        input.OnDashCanceled += HandleDashCanceledInput;

        input.OnWalkPerformed += HandleWalkPerformedInput;

        input.OnAttackPerformed += HandleAttackPerformedInput;
        input.OnAttackStarted += HandleAttackStartedInput;
        input.OnAttackCanceled += HandleAttackCanceledInput;

        input.OnSkillPerformed += HandleSkillPerformedInput;
        input.OnSkillStarted += HandleSkillStartedInput;
        input.OnSkillCanceled += HandleSkillCanceledInput;

        input.OnUltPerformed += HandleUltPerformedInput;
        input.OnUltStarted += HandleUltStartedInput;
        input.OnUltCanceled += HandleUltCanceledInput;

        input.OnInteractionPerformed += HandleInteractionPerformedInput;

        // 교대 입력은 이제 PartySwapManager에서 전역으로 처리
        //input.OnSwapNextPerformed += HandleSwapNextPerformedInput;
        //input.OnSwapPrevPerformed += HandleSwapPrevPerformedInput;

        OnCombatStateChanged += HandleCombatTransition;
    }

    private void UnsubscribeInputEvents()
    {
        if (InputManager.Instance == null) return;

        var input = InputManager.Instance;

        input.OnDashPerformed -= HandleDashPerformedInput;
        input.OnDashStarted -= HandleDashStartedInput;
        input.OnDashCanceled -= HandleDashCanceledInput;

        input.OnWalkPerformed -= HandleWalkPerformedInput;

        input.OnAttackPerformed -= HandleAttackPerformedInput;
        input.OnAttackStarted -= HandleAttackStartedInput;
        input.OnAttackCanceled -= HandleAttackCanceledInput;

        input.OnSkillPerformed -= HandleSkillPerformedInput;
        input.OnSkillStarted -= HandleSkillStartedInput;
        input.OnSkillCanceled -= HandleSkillCanceledInput;

        input.OnUltPerformed -= HandleUltPerformedInput;
        input.OnUltStarted -= HandleUltStartedInput;
        input.OnUltCanceled -= HandleUltCanceledInput;

        input.OnInteractionPerformed -= HandleInteractionPerformedInput;

        //input.OnSwapNextPerformed -= HandleSwapNextPerformedInput;
        //input.OnSwapPrevPerformed -= HandleSwapPrevPerformedInput;

        OnCombatStateChanged -= HandleCombatTransition;
    }

    #endregion

    #region [상태 머신 제어 영역]

    /// <summary>새 Hero 상태에 필요한 공통 동작과 애니메이션 진입을 처리한다.</summary>
    private void EnterState(HeroState newState, bool isComboConnection = false)
    {
        _currentState = newState;

        switch (_currentState)
        {
            case HeroState.Entry:
                DisableInput();
                Movement.ResetStandardMovement();
                ActionRegistry.SetIsInvincible(true);
                AnimationEventHandler.AttachToHand();
                Animation.PlayEntry();
                break;
            case HeroState.Idle:
                ActionRegistry.SetIsInvincible(false);

                /*
                 * 이동 속도가 남아있다면 감속 완료 후
                 * Animator Idle 전환 처리
                 */
                Animation.SetIsIdle(Movement.IsStandardMovementStopped);

                Movement.SetIsDashActive(false);    // ResetCombo로 이동할지 고민
                ActionRegistry.ResetCombo();
                ActionRegistry.SetCanPerfectDodgeCounter(false);
                Animation.ClearEveryTrigger();
                //VFX.StopSwordTrail();

                break;
            case HeroState.ConvertCombat:
                if (_prevState == HeroState.Dodge) return;
                Animation.SetMoveSpeed(Movement.CurrentMoveSpeed);

                // 무기 오브젝트를 따로 관리하지 않는 경우 무기 넣거나 빼는 모션 재생 X
                if (Weapon == null) return;

                if (_isCombat)
                    Animation.DrawWeapon();
                else
                    Animation.DisarmWeapon();
                break;
            case HeroState.Walk:
                Movement.SetIsDashActive(false);
                Animation.SetMoveSpeed(Movement.CurrentMoveSpeed);
                break;
            case HeroState.Run:
                _runDurationTimer = 0f;
                Animation.SetMoveSpeed(Movement.CurrentMoveSpeed);
                break;
            case HeroState.Sprint:
                OnSprintStateChanged?.Invoke(true);
                Animation.SetMoveSpeed(Movement.CurrentMoveSpeed);
                break;
            case HeroState.Dodge:
                ActionRegistry.ClearDodgeCounter();
                ActionRegistry.SetIsInvincible(true);

                Movement.InitializeDodgeCoolDownFields();
                Movement.InitializeDodgeDurationTimer();

                // dodge 직전에 입력값 업데이트 후 회전 적용
                UpdateInputAxis();
                Movement.SetDodgeRotationDirect(_heroAxis);

                // 이동입력 없을 시 전방 Dodge
                if (_heroAxis.magnitude < 0.1f) _heroAxis = transform.forward;

                Movement.DashDirection = _heroAxis;
                Animation.SetMoveSpeed(0f);
                Animation.CrossFadeToDodge(0.1f, Movement.DashDirection);

                // Dodge 오디오 재생
                Audio.PlayDodge();
                OnDodgeStarted?.Invoke(this);
                break;
            case HeroState.DodgeCounter:
                ActionRegistry.ResetCombo();
                ActionRegistry.CloseCancelWindow();
                ActionRegistry.SetIsInvincible(true);

                // 극한회피에 성공한 공격자를 반격 대상으로 지정
                LockOnManager.Instance?.TrySetCounterAttackTarget(
                    CounterTarget
                );

                Animation.TriggerDodgeCounter();
                break;
            case HeroState.Attack:
                //ActionRegistry.SetIsSuperArmorActive(true);

                // 타겟을 향해 회전 작업
                Movement.RefreshCombatDirection(_heroAxis);

                // 평타 파트도 마찬가지로 분기 가능
                if (!isComboConnection)
                {
                    Animation.TriggerAttack(); // 최초 1타 발동
                }
                else
                {
                    Animation.TriggerAttack(); // Combo 연타 발동
                }


                break;
            case HeroState.ChargeAttack:
                ActionRegistry.ResetCombo();
                Movement.RefreshCombatDirection(_heroAxis);
                Animation.SetIsDashAttack(false);
                Animation.SetIsAttackCharging(true);
                Animation.TriggerChargeAttack();
                break;
            case HeroState.Skill:
                ActionRegistry.SetIsSuperArmorActive(true);
                Movement.RefreshCombatDirection(_heroAxis);

                if (!isComboConnection)
                {
                    // 신규 스킬 진입 시 이전 행동의 콤보 정보를 초기화함.
                    ActionRegistry.ResetCombo();
                    // 1타 최초 발동: 에너지를 판별하여 일반/EX 스킬 결정
                    SkillSystem.SkillLogic();
                }
                else
                {
                    // 2타 이후 연타 연계: 첫타 기믹을 유지하며 다음 콤보 발동
                    SkillSystem.OnSkillComboExecuted();
                }

                break;
            case HeroState.Ult:
                // 궁극기 진입 시 이전 행동에서 열린 빠른지원 요청을 취소함.
                // -> 이거 안막으면 빠지 요청 커맨드 - 궁 - 교대라는 잡기술 생김.
                QuickAssistManager.Instance?.CloseWindow(
                    QuickAssistCloseReason.ContextInvalidated);

                // Combo & Hit Index 오염 방어
                ActionRegistry.ResetCombo();
                // Attack으로의 전이 방어
                ActionRegistry.CloseCancelWindow();
                ActionRegistry.SetIsInvincible(true);

                // 패링 직후 궁극기 예외처리
                bool isFromParry = _prevState == HeroState.ParryIn || _prevState == HeroState.ParryCounter;

                if (!isFromParry)
                {   // 일반적인 궁극기 시전시 일반적인 타겟탐색 로직 작동
                    Movement.RefreshCombatDirection(_heroAxis);
                }

                Animation.TriggerUlt();
                break;
            case HeroState.SwapIn:
                if (isComboConnection)
                {
                    Animation.ClearEveryTrigger();
                }

                ForceSyncWeapon();
                Animation.TriggerSwapIn();

                break;
            case HeroState.SwapOut:
                SwapSystem.SwitchHeroCollider(false);
                Animation.TriggerSwapOut();

                break;
            case HeroState.ParryIn:
                //VFX.StopSwordTrail();

                ActionRegistry.ClearParryCounter();
                Animation.ResetTriggerParryCounter();

                ActionRegistry.CloseCancelWindow();
                ActionRegistry.SetIsInvincible(true);
                ForceSyncWeapon();

                // 패링지원 등장 카메라 연출
                CombatPresentationManager.Instance?.PlayParryCamera(this, CounterTarget);

                // MagneticTarget 을 패링 대상과 동기화
                LockOnManager.Instance?.TrySetCounterAttackTarget(CounterTarget);

                // 애니메이션 재생 및 타이머 On
                _parryReadyTimer = 0f;
                _parryResolveState = ParryResolveState.Preparing;
                Animation.CrossFadeToParryPrepare(0.1f);

                break;
            case HeroState.ParryOut:
                break;
            case HeroState.ParryCounter:
                // ParryIn 탈출 시 정리한 Animator 반격 전이 조건을 다시 설정함.
                ActionRegistry.SetIsParryCounterResereved(true);
                ActionRegistry.SetIsInvincible(true);
                Animation.TriggerParryCounter();

                break;
            case HeroState.QuickAssist:
                LockOnManager.Instance?.TrySetCounterAttackTarget(CounterTarget);

                ActionRegistry.ResetActionRegistry();
                ActionRegistry.SetIsInvincible(true);
                Animation.TriggerQuickAssist();
                break;
            case HeroState.ChainAttack:
                ActionRegistry.SetIsInvincible(true);
                Animation.TriggerChain();
                break;
            case HeroState.Hit:
                HitReaction.OnEnterReactionState();
                break;
            case HeroState.Knockback:
                HitReaction.OnEnterReactionState();

                QuickAssistManager.Instance?.TryOpen(
                    this,
                    HitReaction.CurrentReactionAttacker,
                    SwapDirection.Next
                    );
                break;
            case HeroState.InActive:
                VFX.StopSwordTrailImmediate();

                // 완전 퇴장 시 이동 및 랜덤 Idle 정보를 정리함.
                Animation.SetMoveSpeed(0f);
                Animation.SetIsIdle(true);
                Animation.ClearIdleLogicTimers();
                Animation.CrossFadeToIdle(0.1f);

                SwapSystem.SetInActiveEntrance();
                break;
            case HeroState.Dead:
                VFX.StopSwordTrailImmediate();

                DisableInput();
                ActionRegistry.ResetActionRegistry();

                int deathType = Random.Range(0, 2);
                Animation.SetIsDeathType(deathType);
                Animation.TriggerDeath();

                // 사망 이벤트 발동
                OnDeathStateChanged?.Invoke(false);
                break;

        }
    }

    private void UpdateState()
    {
        switch (_currentState)
        {
            case HeroState.Entry:
                if (!_isEntryTimelineControlled && Animation.IsEntryComplete) CompleteEntry();
                break;
            case HeroState.Idle:
                if (!CanReceiveInput) return;

                /*
                 * 입력 해제 이후 남아있는 속도의
                 * 감속 이동 처리
                 */
                Movement.ExecuteStandardMovement(_heroAxis, _currentState);
                bool isMovementStopped = Movement.IsStandardMovementStopped;

                Animation.SetIsIdle(isMovementStopped);

                // Idle 상태에서 입력이 없을때 무작위 Idle 애니메이션이 출력되는 메서드
                if (isMovementStopped)
                {
                    Animation.UpdateIdleLogic();
                }

                break;
            case HeroState.ConvertCombat:
                Movement.ExecuteStandardMovement(_heroAxis, _currentState);

                Animation.SetIsIdle(Movement.IsStandardMovementStopped);
                break;
            case HeroState.Walk:
                // 일반 이동 상태군: HeroMovement에 축과 상태를 넘겨 실질적인 물리 이동 처리
                Movement.ExecuteStandardMovement(_heroAxis, _currentState);
                break;
            case HeroState.Run:
                Movement.ExecuteStandardMovement(_heroAxis, _currentState);

                UpdateSprintTransitionTimer();
                break;
            case HeroState.Sprint:
                Movement.ExecuteStandardMovement(_heroAxis, _currentState);
                break;

            case HeroState.Dodge:
                // 회피 상태: 고정 대시 처리 진행 및 타이머 관리 후 상태 탈출 통제
                if (!Movement.IsDodgeFinished)
                {
                    Movement.ExecuteDodgeMovement();
                    Movement.ExecuteDodgeDurationTimer();
                }

                if (Movement.IsDodgeFinished)
                {
                    // 예약된 반격은 대시 이동 종료 후에도 구르기 종료 자세까지 기다림.
                    if (ActionRegistry.IsDodgeCounterReserved)
                    {
                        AnimatorStateInfo dodgeState =
                            Animation.Animator.GetCurrentAnimatorStateInfo(0);

                        if (dodgeState.IsName("Dodge_blend") &&
                            dodgeState.normalizedTime < 0.9f)
                        {
                            break;
                        }
                    }

                    if (ActionRegistry.ConsumeReservedDodgeCounter())
                    {
                        ChangeState(HeroState.DodgeCounter);
                        break;
                    }

                    if (_heroAxis.magnitude > 0.1f)
                    {
                        ChangeState(HeroState.Sprint);
                    }
                    else
                    {
                        ChangeState(HeroState.Idle);
                    }
                }
                break;
            case HeroState.DodgeCounter:
                Movement.UpdateDodgeCounterRotation(CounterTarget);
                Movement.ExecuteCombatAnimMovement();
                break;
            case HeroState.Attack:
                Movement.UpdateCombatRotation(_heroAxis);

                // 패링 반격 시전 전 이동방지 예외처리
                if (ActionRegistry.IsParryCounterReserved) break;

                // 강공격 전이 전 이동방지 예외처리
                if (Animation.IsAttackCharging) break;

                Movement.ExecuteCombatAnimMovement();
                break;
            case HeroState.ChargeAttack:
                if (Mechanic?.IsCombatRotationLocked != true)
                    Movement.UpdateCombatRotation(_heroAxis);

                // Charge중 이동 차단
                // if (ActionRegistry.IsAttackButtonHolding) break;
                Movement.ExecuteCombatAnimMovement();
                break;
            case HeroState.Skill:
                Movement.UpdateCombatRotation(_heroAxis);
                Movement.ExecuteCombatAnimMovement();
                break;
            case HeroState.Ult:
                Movement.ExecuteCombatAnimMovement();
                break;
            case HeroState.SwapIn:
                Movement.ExecuteCombatAnimMovement();
                break;
            case HeroState.SwapOut:
                Movement.ExecuteCombatAnimMovement();
                break;
            case HeroState.ParryIn:
                Movement.UpdateParryRotation(CounterTarget);

                switch (_parryResolveState)
                {
                    case ParryResolveState.Preparing:
                        break;
                    case ParryResolveState.Ready:
                        UpdateParryReadyTimer();
                        break;
                    case ParryResolveState.Impact:
                        // 공격을 쳐낸 이후로는 뒤로 밀려남
                        Movement.ExecuteCombatAnimMovement();
                        break;
                    case ParryResolveState.NoAttack:
                        break;
                }
                break;
            case HeroState.ParryOut:
                break;
            case HeroState.ParryCounter:
                Movement.UpdateParryRotation(CounterTarget);
                Movement.ExecuteCombatAnimMovement();
                break;
            case HeroState.QuickAssist:
                Movement.UpdateCombatRotation(_heroAxis);
                Movement.ExecuteCombatAnimMovement();
                break;
            case HeroState.ChainAttack:
                Movement.ExecuteCombatAnimMovement();
                break;
            case HeroState.Hit:
                break;
            case HeroState.Knockback:
                break;
            case HeroState.InActive:
                break;
            case HeroState.Dead:
                break;
        }
    }

    /// <summary>상태 이탈 시 차징 신호와 액션 정보를 정리한다.</summary>
    private void ExitState(HeroState currentState, bool preserveAnimMove = false)
    {
        Animation.ClearChargingParameters();

        // 상태 전이 직전에 캔슬 윈도우는 무조건 닫는 안전장치
        ActionRegistry.CloseCancelWindow();

        // 애니메이션 이벤트 종료 누락 또는 상태 강제 전환 시 이동 플래그 정리
        if (!preserveAnimMove)
        {
            ActionRegistry.DeactivateAnimMove();
            Movement.ClearFixedAnimMovement();
        }

        // 애니메이터 트리거 큐는 무조건 비워주는 안전장치
        Animation.ClearEveryTrigger();

        // 퀵스왑 윈도우 열어주는 안전장치
        SwapSystem.SetIsQuickSwapWindowOpen(true);

        // VFX SwordTrail 정지
        VFX.StopSwordTrail();

        // 슈퍼아머 비활성화
        ActionRegistry.SetIsSuperArmorActive(false);

        switch (currentState)
        {
            case HeroState.Idle:
                Animation.SetIsIdle(false);
                Animation.ClearIdleLogicTimers();
                break;
            case HeroState.ConvertCombat:
                Animation.SetUpperBodyLayerWeight(0f);
                Animation.SetIsIdle(false);
                break;
            case HeroState.Walk:
                break;
            case HeroState.Run:
                break;
            case HeroState.Sprint:
                OnSprintStateChanged?.Invoke(false);
                break;
            case HeroState.Dodge:
                ActionRegistry.SetIsInvincible(false);
                ActionRegistry.ClearDodgeCounter();
                break;
            case HeroState.DodgeCounter:
                ActionRegistry.SetIsInvincible(false);
                ClearCounterTarget();
                break;
            case HeroState.Attack:
                // 공격 상태 종료 시 대시 공격 분류를 해제함.
                Animation.SetIsDashAttack(false);
                ActionRegistry.SetIsInvincible(false);
                break;
            case HeroState.ChargeAttack:
                Animation.SetIsAttackCharging(false);
                ActionRegistry.SetIsInvincible(false);
                break;
            case HeroState.Skill:
                ActionRegistry.SetIsInvincible(false);
                break;
            case HeroState.Ult:
                ActionRegistry.SetIsInvincible(false);
                break;
            case HeroState.SwapIn:
                ActionRegistry.SetIsInvincible(false);
                break;
            case HeroState.SwapOut:
                Animation.ClearEveryTrigger();
                ActionRegistry.ResetCombo();
                break;
            case HeroState.ParryIn:
                // ParryIn 탈출시점에 CombatManager의 패링판정 대기 필드들 초기화
                CombatManager.Instance?.CancelParryAssistRequest(this);
                // 연속 패링 교대에서는 다음 Hero가 기존 카메라를 이어받음.
                if (!SwapSystem.IsParryCameraHandoffActive)
                {
                    CombatPresentationManager.Instance?.TryStopParryCamera(this);
                }

                ActionRegistry.ClearParryCounter();
                Animation.ResetTriggerParryCounter();
                _parryReadyTimer = 0f;
                ActionRegistry.SetIsInvincible(false);
                _parryResolveState = ParryResolveState.Preparing;
                break;
            case HeroState.ParryOut:
                break;
            case HeroState.ParryCounter:
                ActionRegistry.SetIsParryCounterResereved(false);
                ActionRegistry.SetIsParryCounterWindowOpen(false);

                ActionRegistry.SetIsInvincible(false);

                ClearCounterTarget();
                break;
            case HeroState.QuickAssist:
                ActionRegistry.SetIsInvincible(false);
                ClearCounterTarget();
                break;
            case HeroState.ChainAttack:
                ActionRegistry.SetIsInvincible(false);
                // 후딜 캔슬로 종료 이벤트가 생략되어도 담당 Hero의 체인 실행을 정리함.
                ChainAttackManager.Instance?.CompleteChainExecution(this);
                break;
            case HeroState.Hit:
                HitReaction.OnExitReactionState();
                break;
            case HeroState.Knockback:
                HitReaction.OnExitReactionState();
                break;
            case HeroState.InActive:
                Animation.SetIsIdle(false);
                break;
            case HeroState.Dead:
                break;
        }
    }

    /// <summary>
    /// [주의] 외부 모듈(Movement, Combat 등)은 원칙적으로 이 메서드를 직접 호출 XXX
    /// 이 메서드는 Hero 본체의 입력 핸들러, 애니메이션 이벤트, 또는 피격/CC기 시스템에서만 제한적으로 호출
    /// [추가] 현재 HeroAnimationEventHandler에서 ChangeState 하는 메서드 몇개 있음. 리팩토링 필요
    /// 이게 합리적이라면 하단의 UpdateMovementState 메서드도 HeroMovement로 빠져도 되나?
    /// [결론] 처음 생각대로 Hero 본체에서만 직접적인 상태전환 가능하되,
    /// 외부에서 상태전환이 필요하다 판단되면 본체로 신호를 쏴주는 메서드를 추가해줄 예정
    /// </summary>
    /// <param name="isComboConnection">
    /// 자가변환을 위한 예외처리, true일때 자가변환 허용 -> Combo 시스템 분기처리 가능
    /// </param>
    public void ChangeState(HeroState newState, bool isComboConnection = false)
    {
        if (_currentState == newState && !isComboConnection) return;

        bool isLeavingStandardLocomotion =
            IsStandardLocomotionState(_currentState) && !IsStandardLocomotionState(newState);

        /*
         * 공격, 회피, 교대 등 전용 이동 상태 진입 시
         * 기본 Locomotion 속도 초기화
         */
        if (isLeavingStandardLocomotion)
        {
            Movement.ResetStandardMovement();
        }

        // 기존 상태 종료
        bool preserveAnimMove = isComboConnection && _currentState == newState;
        Mechanic?.ExitState(_currentState, preserveAnimMove);       // 전용 메카닉 FSM Enter
        ExitState(_currentState, preserveAnimMove);

        _currentState = newState;

        EnterState(_currentState, isComboConnection);
        Mechanic?.EnterState(_currentState, isComboConnection);     // 전용 메카닉 FSM Exit

        Debug.Log($"[{gameObject.name} 상태 변경] : {_currentState} ");
    }

    private static bool IsStandardLocomotionState(HeroState state)
    {
        return state == HeroState.Idle ||
               state == HeroState.Walk ||
               state == HeroState.Run ||
               state == HeroState.Sprint ||
               state == HeroState.ConvertCombat;
    }

    #endregion

    #region [내부 상태 업데이트 함수 영역]

    private void UpdateInputAxis()
    {
        if (!CanReceiveInput) return;

        Vector2 input = InputManager.Instance.MoveInput;

        Transform cameraTransform = Camera.main.transform;
        Vector3 forward = cameraTransform.forward;
        Vector3 right = cameraTransform.right;

        forward.y = 0f;
        right.y = 0f;

        _heroAxis = ((forward.normalized * input.y) + (right.normalized * input.x)).normalized;
    }

    /// <summary>
    /// 이동류 상태에서 이동류 상태로 전이 시켜주는 헬퍼 메서드
    /// </summary>
    private void UpdateMovementState()
    {
        if (!CanReceiveInput) return;

        if (_currentState == HeroState.Idle || _currentState == HeroState.Run ||
            _currentState == HeroState.Walk || _currentState == HeroState.Sprint)
        {
            if (_heroAxis.magnitude > 0.1f)
            {
                if (Movement.IsWalkMode)
                {
                    ChangeState(HeroState.Walk);
                }
                else if (Movement.IsDashActive)
                {
                    ChangeState(HeroState.Sprint);
                }
                else
                {
                    ChangeState(HeroState.Run);
                }
            }
            else
            {
                ChangeState(HeroState.Idle);
            }
        }
    }

    /// <summary>
    /// 현재 상태와 버튼 유지 시간을 기준으로 차징 신호 및 평꾹 진입을 처리함.
    /// </summary>
    private void UpdateChargeCheck()
    {
        // E 차징 먼저 처리
        bool isSkillCharging =
            CanReceiveInput &&
            CurrentState == HeroState.Skill &&
            ActionRegistry.IsSkillButtonHolding &&
            Time.time - _skillButtonPressStartTime >= _buttonHoldTime;

        Animation.SetIsSkillCharging(isSkillCharging);

        // 평꾹 상태 이탈 또는 입력 차단 시 공격 차징 신호를 해제함.
        if (CurrentState != HeroState.ChargeAttack || !CanReceiveInput)
        {
            Animation.SetIsAttackCharging(false);
        }

        if (!CanReceiveInput) return;
        if (CurrentState != HeroState.Attack || Animation.GetIsDashAttack()) return;
        if (!ActionRegistry.IsAttackButtonHolding) return;

        float elapsedTime = Time.time - _attackButtonPressStartTime;
        if (elapsedTime < _buttonHoldTime) return;

        ChangeState(HeroState.ChargeAttack);
    }

    private void UpdateSprintTransitionTimer()
    {
        if (CurrentState != HeroState.Run) return;

        _runDurationTimer += Time.deltaTime;

        if (_runDurationTimer >= _sprintTransitionDuration)
        {
            Movement.SetIsDashActive(true);
            ChangeState(HeroState.Sprint);
        }
    }

    #endregion


    #region [Input Event Handler 영역]

    /// <summary>현재 액션의 회피 허용 조건에 따라 회피를 요청한다.</summary>
    private void HandleDashStartedInput()
    {
        if (!CanReceiveInput) return;
        if (ActionRegistry.IsManualDodgeBlocked) return;

        // 궁극기 중 차단
        if (CurrentState == HeroState.Ult) return;

        // 강특 시전중 차단
        if (CurrentState == HeroState.Skill && SkillSystem.IsExSkillMode == true) return;

        // Dodge 쿨타임 중이면 차단
        if (!Movement.CanDodge) return;

        // HeroMovement - Movement Settings 필드 관리
        Movement.SetIsWalkMode(false);
        Movement.SetIsDashActive(true);  // Performed로 이사하는거 고려중 -> 조작감 부분이라 내 취향껏 가면될듯.

        bool isBaseState = CurrentState == HeroState.Idle || CurrentState == HeroState.Walk ||
                            CurrentState == HeroState.Run || CurrentState == HeroState.Sprint ||
                            CurrentState == HeroState.Attack || CurrentState == HeroState.ChargeAttack ||
                            CurrentState == HeroState.Skill;

        if (isBaseState || ActionRegistry.IsCancelWindowOpen)
        {
            ChangeState(HeroState.Dodge);
        }
    }
    private void HandleDashPerformedInput()
    {
        if (!CanReceiveInput) return;

        // 회피 꾹 기믹 생기면 추가될지도
    }
    private void HandleDashCanceledInput()
    {
        if (!CanReceiveInput) return;

        // 회피 꾹 기믹 생기면 추가될지도
    }

    private void HandleWalkPerformedInput()
    {
        if (!CanReceiveInput) return;

        Movement.SetIsWalkMode(!Movement.IsWalkMode);
    }

    /// <summary>
    /// 공격 입력 시작을 기록하고 일반 공격 또는 연계를 요청한다.
    /// </summary>
    private void HandleAttackStartedInput()
    {
        if (!CanReceiveInput) return;
        if (CurrentState == HeroState.DodgeCounter) return;
        if (CurrentState == HeroState.ChargeAttack && !ActionRegistry.IsCancelWindowOpen) return;

        // 수동 차징 타이머 기록 시작
        _attackButtonPressStartTime = Time.time;
        ActionRegistry.SetIsAttackButtonHolding(true);

        // 해당 입력으로 전이될 상태
        _toState = HeroState.Attack;

        // 언제든지 해당 상태로 전이 가능한 기본 상태
        bool isBaseState = CurrentState == HeroState.Idle || CurrentState == HeroState.Walk ||
                            CurrentState == HeroState.Run || CurrentState == HeroState.Sprint;
        // 대쉬 공격이 발동될 상태
        bool canDashAttack = CurrentState == HeroState.Sprint || CurrentState == HeroState.Dodge;

        // 콤보 연계 분기
        // 기존 상태에서 같은 입력을 받으면 콤보연계 처리
        if (_currentState == _toState || (Animation.GetIsDashAttack() && _currentState == HeroState.Attack))
        {
            if (ActionRegistry.IsComboLinkable && !ActionRegistry.IsComboReserved)
            {
                ActionRegistry.SetIsComboReserved(true);
            }
        }
        else if (CurrentState == HeroState.ParryIn)    // 패링 반격
        {
            // 패링 실패 후 복귀 중에는 반격 입력을 수락하지 않음.
            if (_parryResolveState == ParryResolveState.NoAttack) return;

            ActionRegistry.SetIsParryCounterResereved(true);

            // 성공 전에는 예약만 유지하고 성공 후 열린 입력 창에서 즉시 반격함.
            if (IsParryImpactActive && ActionRegistry.IsParryCounterWindowOpen)
            {
                ActionRegistry.SetIsParryCounterWindowOpen(false);
                ChangeState(HeroState.ParryCounter);
            }

            return;
        }
        else if (CurrentState == HeroState.Dodge)
        {
            if (ActionRegistry.TryReserveDodgeCounter())
            {
                return;
            }
            else
            {
                // TODO : 대시 공격 여기로 이전
            }
        }
        else if (isBaseState || ActionRegistry.IsCancelWindowOpen)    // 기본 상태 혹은 캔슬 윈도우가 열려있을 때
        {
            ChangeState(_toState);

            ActionRegistry.ResetComboIndex();
            ActionRegistry.SetIsComboReserved(false);
            ActionRegistry.SetIsComboLinkable(false);

            // DashAttack 예외처리 - 애니메이터 파라미터 켜서 Attack_System에서 Dash Attack 나가는 로직
            if (canDashAttack)
            {
                Animation.SetIsDashAttack(true);
            }
            else
            {
                Animation.SetIsDashAttack(false);
            }
        }

    }
    /// <summary>
    /// 수락된 공격 유지 입력의 캔슬 윈도우를 닫는다.
    /// </summary>
    private void HandleAttackPerformedInput()
    {
        if (!CanReceiveInput) return;

        // Started에서 수락한 입력만 유지하며, 거절된 재입력을 되살리지 않는다.
        if (!ActionRegistry.IsAttackButtonHolding) return;
        ActionRegistry.CloseCancelWindow();
    }
    /// <summary>입력 차단 여부와 관계없이 공격 버튼 해제를 정리한다.</summary>
    private void HandleAttackCanceledInput()
    {
        ActionRegistry.SetIsAttackButtonHolding(false);
        Animation.SetIsAttackCharging(false);
    }

    /// <summary>스킬 입력 시작 시간을 공격과 독립적으로 기록한다.</summary>
    private void HandleSkillStartedInput()
    {
        if (!CanReceiveInput) return;

        // 수동 차징 타이머 기록 시작
        _skillButtonPressStartTime = Time.time;
        ActionRegistry.SetIsSkillButtonHolding(true);

        // 해당 입력으로 전이될 상태
        _toState = HeroState.Skill;

        // 언제든지 해당 상태로 전이 가능한 기본 상태
        bool isBaseState = CurrentState == HeroState.Idle || CurrentState == HeroState.Walk ||
                            CurrentState == HeroState.Run || CurrentState == HeroState.Sprint;

        // 콤보 연계 분기
        if (_currentState == _toState)      // 기존 상태에서 같은 입력을 받으면 콤보연계 처리
        {
            if (ActionRegistry.IsComboLinkable && !ActionRegistry.IsComboReserved)
            {
                ActionRegistry.SetIsComboReserved(true);
            }
        }
        else if (isBaseState || ActionRegistry.IsCancelWindowOpen)
        {
            // 이제 ExitState에서 통상적으로 처리
            // Animation.ClearEveryTrigger();

            ChangeState(_toState);
        }
    }
    /// <summary>스킬 버튼 유지 여부를 기록한다.</summary>
    private void HandleSkillPerformedInput()
    {
        if (!CanReceiveInput) return;

        ActionRegistry.SetIsSkillButtonHolding(true);
    }
    /// <summary>스킬 입력 해제는 공격 차징에 영향을 주지 않는다.</summary>
    private void HandleSkillCanceledInput()
    {
        ActionRegistry.SetIsSkillButtonHolding(false);
        Animation.SetIsSkillCharging(false);
    }

    private void HandleUltStartedInput()
    {
        if (!CanReceiveInput) return;
        if (CurrentState == HeroState.Ult) return;
        if (CurrentState == HeroState.ChainAttack) return;  // Chain공격중 Ult 시전 차단

        if (!Status.TryConsumeUltDecibel())
        {
            Debug.Log($"[{gameObject.name} Ult 발동 실패: 데시벨 부족]");
            return;
        }

        _toState = HeroState.Ult;
        ChangeState(_toState);

    }
    private void HandleUltPerformedInput()
    {
        if (!CanReceiveInput) return;
    }
    private void HandleUltCanceledInput()
    {
        if (!CanReceiveInput) return;
    }

    private void HandleInteractionPerformedInput()
    {
        if (!CanReceiveInput) return;

        Animation.TriggerTest();
    }

    /// <summary>
    /// 전투 여부 변경 시 무기 표시와 선택적 무기 전환 모션을 동기화함.
    /// </summary>
    private void HandleCombatTransition(bool isCombat)
    {
        if (_currentState == HeroState.Entry) return;
        // 하드 록온 상태가 아니라면 소프트 록온 타겟을 null
        if (!_isCombat && !LockOnManager.Instance.IsHardLockActive)
        {
            LockOnManager.Instance.RefreshSoftLockTarget();
        }

        // 무기 전환 모션을 생략하는 Hero는 현재 행동을 유지
        if (!Animation.UseWeaponTransitionAnimation)
        {
            ForceSyncWeapon();
            return;
        }

        // 기본 이동 상태
        bool isBaseState = CurrentState == HeroState.Idle || CurrentState == HeroState.Walk ||
                            CurrentState == HeroState.Run || CurrentState == HeroState.Sprint ||
                            CurrentState == HeroState.SwapIn;

        // 기본 이동상태 제외 강제 무기 위치 동기화 (무기 집어넣고 빼는 애니메이션 재생 X)
        if (!isBaseState)
        {
            ForceSyncWeapon();
            return;
        }

        ChangeState(HeroState.ConvertCombat);
    }

    #endregion



    #region [유틸리티 복합 가공 연산 메서드]

    /// <summary>
    /// 애니메이션 전환 과정에서 DrawSword/SheathSword 애니메이션이 씹혔을 경우 
    /// 무기위치 싱크 맞춰주는 헬퍼 메서드
    /// </summary>
    private void ForceSyncWeapon()
    {
        if (_currentState == HeroState.Entry || IsCombat) AnimationEventHandler.AttachToHand();
        else AnimationEventHandler.AttachToBack();
    }

    // Animation 요소들 리셋 및 Idle로 명시적 변환
    /// <summary>공격 종료 정보를 정리하고 대기 상태로 돌아간다.</summary>
    public void BackToIdle()
    {
        if (_currentState == HeroState.Entry) return;
        // 애니메이터 파라미터 초기화 과정 및 Idle로 전환
        ChangeState(HeroState.Idle);

        Animation.SetComboIndex(0);
        Animation.ClearChargingParameters();
        Animation.SetIsDashAttack(false);

        Animation.ClearEveryTrigger();

        Animation.CrossFadeToIdle(0.1f);
    }

    /// <summary>
    /// Timeline이 입장 종료 시점을 제어하는 동안 모션의 자동 종료를 보류함.
    /// </summary>
    public void SetEntryTimelineControlled(bool isControlled)
    {
        _isEntryTimelineControlled = isControlled;
    }

    /// <summary>
    /// 입장 모션을 종료하고 최신 전투 판정에 맞춰 무기와 대기 자세, 입력을 복원함.
    /// </summary>
    public void CompleteEntry()
    {
        _isEntryTimelineControlled = false;
        if (_currentState != HeroState.Entry) return;
        ChangeState(HeroState.Idle);
        Animation.SetIsCombat(_isCombat);
        ForceSyncWeapon();
        Animation.CrossFadeToIdle(0.15f);
        EnableInput();
    }

    public void EnableInput()
    {
        _isInputEnabled = true;
    }

    /// <summary>입력을 차단하고 해제 이벤트를 받지 못할 홀딩 정보를 정리한다.</summary>
    public void DisableInput()
    {
        _isInputEnabled = false;
        ActionRegistry.SetIsAttackButtonHolding(false);
        ActionRegistry.SetIsSkillButtonHolding(false);
        Animation.ClearChargingParameters();
    }

    public void SetCounterTarget(IAttacker enemy)
    {
        if (enemy is Enemy target)
        {
            if (_counterTarget == target) return;

            _counterTarget = target;
            Debug.Log($"Hero_CounterTarget : {_counterTarget}");
        }
        else
        {
            Debug.Log("SetCounterTarget : Casting Fail");
        }
    }

    // ParryCounter - ExitState에서 호출
    public void ClearCounterTarget()
    {
        _counterTarget = null;

        // 필요하다면 매니저의 인스턴스도 비워줌
        //if (CombatManager.Instance != null)
        //{
        //    CombatManager.Instance.ClearActiveAttacker();
        //}
    }

    public void HandleParryPrepareEnd()
    {
        if (_currentState != HeroState.ParryIn ||
            _parryResolveState != ParryResolveState.Preparing)
        {
            return;
        }

        _parryResolveState = ParryResolveState.Ready;
        _parryReadyTimer = _parryReadyMaxDuration;

        Animation.CrossFadeToParryReady(0.05f);
    }
    /// <summary>
    /// ParryIn-UpdateState 에서 호출
    /// Parry_Ready 애니메이션에서 Parry_After_NoAttack으로 탈출시켜주는 보조 메서드
    /// </summary>
    private void UpdateParryReadyTimer()
    {
        _parryReadyTimer -= Time.unscaledDeltaTime;

        if (_parryReadyTimer <= 0f)
        {
            ResolveParryNoAttack();
        }
    }
    /// <summary>
    /// Parry_Ready 애니메이션에서 Parry_After_NoAttack
    /// CrossFade 시켜주는 메서드
    /// </summary>
    private void ResolveParryNoAttack()
    {
        if (CurrentState != HeroState.ParryIn) return;
        if (_parryResolveState != ParryResolveState.Ready) return;

        _parryResolveState = ParryResolveState.NoAttack;
        CombatManager.Instance?.CancelParryAssistRequest(this);
        Animation.CrossFadeToParryAfterNoAttack(0.1f);
    }
    /// <summary>
    /// 준비 중인 패링만 Impact 상태로 전환하고 성공 여부를 반환함.
    /// </summary>
    public bool TryResolveParryImpact(Enemy attacker)
    {
        if (CurrentState != HeroState.ParryIn) return false;
        if (attacker == null) return false;

        if (_parryResolveState != ParryResolveState.Preparing &&
            _parryResolveState != ParryResolveState.Ready)
        {
            return false;
        }

        _parryResolveState = ParryResolveState.Impact;

        SetCounterTarget(attacker);
        ActionRegistry.ActivateAnimMove();
        Movement.SetCurrentParryAssistSpeed(
            Movement.MaxParryAssistSpeed
        );

        Animation.CrossFadeToParryImpact(0.02f);
        return true;
    }

    /// <summary>
    /// 사망 전용 상태로 전환하고 활성 버프를 정리함.
    /// </summary>
    public void TriggerDeath()
    {
        if (CurrentState == HeroState.Dead) return;

        Status.ClearBuffs();
        ChangeState(HeroState.Dead);
    }

    public void Revive(float hpAmount)
    {
        if (!Status.IsDead || CurrentState != HeroState.Dead) return;

        Status.SetCurrentHP(hpAmount);
        ActionRegistry.ResetActionRegistry();

        // 우선 오프필드 상태로 부활
        ChangeState(HeroState.InActive);

        // 부활 했다는 신호를 쏴줄 이벤트 작동
        OnDeathStateChanged?.Invoke(true);
    }

    #endregion


    #region [인터페이스 구현부 및 시스템 콜백]

    public bool TakeDamage(HitInfo hitInfo)
    {
        if (Status.IsDead) return false;
        
        // 무적 및 잔상공격 피격허용 X
        if (ActionRegistry.IsInvincibilityActive) return false;
        if (ActionRegistry.IsOffFieldActionActive) return false;    // 잔상 시퀀스의 캐릭터의 피격 차단

        if (CurrentState == HeroState.SwapOut) return false;    // SwapOut 중에는 명시적으로 피격 차단
        if (CurrentState == HeroState.InActive) return false;   // InActive 중에는 명시적으로 피격 차단

        Status.ModifyHP(-hitInfo.damage);

        Debug.Log($"[{gameObject.name}] 피격당함! " +
                  $"| 받은 대미지: {hitInfo.damage} " +
                  $"| 남은 체력: {Status.CurrentHP}/{Status.TotalMaxHP} " +
                  $"| 반응 경직: {hitInfo.hitType}");

        // 타격 방향 처리
        if (hitInfo.attacker != null)
        {
            Vector3 hitDirection = (transform.position - hitInfo.attacker.AttackerTransform.position).normalized;
        }

        // 사망 분기 처리
        if (Status.IsDead)
        {
            TriggerDeath();
            return true;
        }

        // 경직 처리
        HitReaction.TryStartReaction(hitInfo);

        return true;
    }

    public void OnHitSuccess(float payBackEnegy, float payBackDecibel)
    {
        Status.ModifyEnergy(payBackEnegy);
        Status.ModifyDecibel(payBackDecibel);

        // 타격 성공시 데시벨 획득량을 파티원에게 분배 -> 현재 50%
        PartyStateManager.Instance?.ShareDecibel(this, payBackDecibel);
    }

    /// <summary>
    /// CombatManager의 자원 반환 경로에서 타격 성공을 구독자에게 알림.
    /// 대상별 피해 적용 알림이 아니므로 ProcessHit에서 직접 호출하지 않음.
    /// </summary>
    public void NotifyAttackHitConfirmed(HeroCombatAttackType attackType)
    {
        OnAttackHitConfirmed?.Invoke(attackType);
    }

    #endregion




}
