using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.Timeline;

public delegate void CombatStateChangedHandler(bool isCombat);

/// <summary>
/// 직접적인 Hero 상태 변경과 변경에 따른 변화는 여기서 직접 처리 (뇌 역할)
/// </summary>
public class Hero : MonoBehaviour, IDamageable, IAttacker
{
    private enum ParryResolveState { Waiting, Impact, NoAttack }

    // IsCombat 프로퍼티의 값이 변경될 때 사용될 이벤트
    public event CombatStateChangedHandler OnCombatStateChanged;
    // Hero의 사망/부활 처리 신호를 줄 이벤트
    public event System.Action<bool> OnDeathStateChanged;


    #region [프로퍼티 영역]

    // 컴포넌트 및 스크립트 참조
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



    public HeroCombatDataSO CombatData => _combatData;
    public Texture2D Portrait => _portrait;
    public Renderer[] BodyRenderers => _bodyRenderers;
    public HeroState CurrentState => _currentState;
    public Vector3 HeroAxis => _heroAxis;
    public LayerMask EnemyLayerMask => _enemyLayerMask;

    // 전투 연출 관련 프로퍼티
    public CinemachineCamera UltVcam => _ultVcam;
    public float UltCutsceneDuration => _ultCutsceneDuration;
    public TimelineAsset UltTimelineAsset => _ultTimelineAsset;
    public Transform ParryCameraAnchor => _parryCameraAnchor;

    // 입력 가능 여부 필드 및 프로퍼티
    private bool _isInputEnabled = true;
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
    // IAttacker
    public Transform AttackerTransform => this.transform;
    IStatus IAttacker.Status => Status;

    #endregion


    // 해당 Hero의 각종 데이터 SO
    [Header("Combat Data Asset")]
    [SerializeField] private HeroCombatDataSO _combatData;

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
    [SerializeField] private Transform _parryCameraAnchor;

    [Header("CombatModeConvert Settings")]
    [SerializeField] private bool _isCombat = false;


    [Header("Charging Input Settings")]
    private float _buttonHoldTime = 0.3f;           // 이 시간만큼 누르고 있으면 차징 상태로 인정
    private float _buttonPressStartTime;            // (차징 가능한) 키를 누르기 시작한 시점의 시간

    [Header("Counter Target")]
    [SerializeField]
    private Enemy _counterTarget;
    /// <summary>
    /// 이 변수의 시간보다 DefenseWindow가 열린후 생기는 선딜이 길면 문제 발생할수도 있음.
    /// 해당 이슈 발생시 다시 확인
    /// </summary>
    private float _parryReadyMaxDuration = 1.1f;    // Parry_Ready 이후 Impact 발동 안되는 경우 탈출 시간
    private float _parryReadyTimer;
    private ParryResolveState _parryResolveState = ParryResolveState.Waiting;


    private void Awake()
    {
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
    }

    void Start()
    {
        if (InputManager.Instance == null)
        {
            Debug.LogError("InputManager Instance Error");
            return;
        }

        SubscribeInputEvents();

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

    private void EnterState(HeroState newState, bool isComboConnection)
    {
        _currentState = newState;

        switch (_currentState)
        {
            case HeroState.Idle:
                ActionRegistry.SetIsInvincible(false);
                Animation.SetIsIdle(true);
                Animation.SetMoveSpeed(0f);
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
                    Animation.DrawSword();
                else
                    Animation.SheathSword();
                break;
            case HeroState.Walk:
                Animation.SetMoveSpeed(Movement.CurrentMoveSpeed);
                break;
            case HeroState.Run:
                Animation.SetMoveSpeed(Movement.CurrentMoveSpeed);
                break;
            case HeroState.Sprint:
                Animation.SetMoveSpeed(Movement.CurrentMoveSpeed);
                break;
            case HeroState.Dodge:
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
                break;
            case HeroState.DodgeCounter:
                ActionRegistry.CloseCancelWindow();
                ActionRegistry.SetIsInvincible(true);

                // 극한회피에 성공한 공격자를 반격 대상으로 지정
                LockOnManager.Instance?.TrySetCounterAttackTarget(
                    CounterTarget
                );

                // 변경된 MagneticTarget을 기준으로 이동 방향 계산
                Movement.UpdateAnimMoveDirToTarget(_heroAxis);

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
            case HeroState.Skill:
                ActionRegistry.SetIsSuperArmorActive(true);

                Movement.RefreshCombatDirection(_heroAxis);

                if (!isComboConnection)
                {
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
                ActionRegistry.SetIsInvincible(true);

                // 패링 직후 궁극기 예외처리
                bool isFromParry = _prevState == HeroState.ParryIn || _prevState == HeroState.ParryCounter;

                if (isFromParry)
                {   // 패링 직후에는 패링 대상을 유지
                    Movement.UpdateAnimMoveDirToTarget(_heroAxis);
                }
                else
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

                ActionRegistry.CloseCancelWindow();
                ActionRegistry.SetIsInvincible(true);
                ForceSyncWeapon();

                // 패링지원 등장 카메라 연출
                CombatPresentationManager.Instance?.PlayParryCamera(this, CounterTarget);
                
                // MagneticTarget 을 패링 대상과 동기화
                LockOnManager.Instance?.TrySetCounterAttackTarget(CounterTarget);

                _parryReadyTimer = _parryReadyMaxDuration;
                _parryResolveState = ParryResolveState.Waiting;

                // 패링 애니메이션
                Animation.CrossFadeToParryReady(0.1f);

                break;
            case HeroState.ParryOut:
                break;
            case HeroState.ParryCounter:
                ActionRegistry.SetIsInvincible(true);
                Movement.UpdateAnimMoveDirToTarget(_heroAxis);
                Animation.TriggerParryCounter();

                break;
            case HeroState.QuickAssist:
                LockOnManager.Instance?.TrySetCounterAttackTarget(CounterTarget);

                ActionRegistry.ResetActionRegistry();
                ActionRegistry.SetIsInvincible(true);
                Movement.UpdateAnimMoveDirToTarget(_heroAxis);
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

                Animation.SetMoveSpeed(0f);
                SwapSystem.SetInActiveEntrance();
                //Weapon.ChangeWeaponDisplayState(HeroWeapon.WeaponDisplayState.Hide);
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
            case HeroState.Idle:
                // Idle 상태에서 입력이 없을때 무작위 Idle 애니메이션이 출력되는 메서드
                if (!CanReceiveInput) return;
                Animation.UpdateIdleLogic();
                break;
            case HeroState.ConvertCombat:
                Movement.ExecuteStandardMovement(_heroAxis, _currentState);
                break;
            case HeroState.Walk:
                // 일반 이동 상태군: HeroMovement에 축과 상태를 넘겨 실질적인 물리 이동 처리
                Movement.ExecuteStandardMovement(_heroAxis, _currentState);
                break;
            case HeroState.Run:
                Movement.ExecuteStandardMovement(_heroAxis, _currentState);
                break;
            case HeroState.Sprint:
                Movement.ExecuteStandardMovement(_heroAxis, _currentState);
                break;

            case HeroState.Dodge:
                // 회피 상태: 고정 대시 처리 진행 및 타이머 관리 후 상태 탈출 통제
                Movement.ExecuteDodgeMovement();
                Movement.ExecuteDodgeDurationTimer();

                if (Movement.IsDodgeFinished)
                {
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
                break;
            case HeroState.Attack:
                Movement.UpdateCombatRotation(_heroAxis);

                // 차지 중 이동방지 예외처리
                if (ActionRegistry.IsButtonHolding) break;
                // 패링 반격 시전 전 이동방지 예외처리
                if (ActionRegistry.IsParryCounterReserved) break;

                Movement.ExecuteMagneticMovement();
                break;
            case HeroState.Skill:
                Movement.UpdateCombatRotation(_heroAxis);
                Movement.ExecuteMagneticMovement();
                break;
            case HeroState.Ult:
                Movement.ExecuteMagneticMovement();
                break;
            case HeroState.SwapIn:
                Movement.ExecuteSwapInMovement();
                break;
            case HeroState.SwapOut:
                Movement.ExecuteSwapOutMovement();
                break;
            case HeroState.ParryIn:
                Movement.UpdateParryRotation(CounterTarget);

                switch (_parryResolveState)
                {
                    case ParryResolveState.Waiting:
                        UpdateParryReadyTimer();
                        break;

                    case ParryResolveState.Impact:
                        // 공격을 쳐낸 이후로는 뒤로 밀려남
                        Movement.ExecuteParryAssistMovement();
                        break;

                    case ParryResolveState.NoAttack:
                        break;
                }

                break;
            case HeroState.ParryOut:
                break;
            case HeroState.ParryCounter:
                Movement.UpdateParryRotation(CounterTarget);
                Movement.UpdateParryCounterMovement(CounterTarget);
                break;
            case HeroState.QuickAssist:
                Movement.UpdateCombatRotation(_heroAxis);
                Movement.ExecuteMagneticMovement();
                break;
            case HeroState.ChainAttack:
                Movement.ExecuteDirectionalAnimMovement();
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

    private void ExitState(HeroState currentState)
    {
        // 상태 전이 직전에 캔슬 윈도우는 무조건 닫는 안전장치
        ActionRegistry.CloseCancelWindow();

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
                break;
            case HeroState.Walk:
                break;
            case HeroState.Run:
                break;
            case HeroState.Sprint:
                break;
            case HeroState.Dodge:
                ActionRegistry.SetIsInvincible(false);
                break;
            case HeroState.DodgeCounter:
                ActionRegistry.SetIsInvincible(false);
                ClearCounterTarget();
                break;
            case HeroState.Attack:
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
                // ParryIn 탈출 타이밍에 패링 연출 카메라도 종료
                CombatPresentationManager.Instance?.StopParryCamera();

                ActionRegistry.SetIsInvincible(false);
                _parryResolveState = ParryResolveState.Waiting;
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
                break;
            case HeroState.Hit:
                HitReaction.OnExitReactionState();
                break;
            case HeroState.Knockback:
                HitReaction.OnExitReactionState();
                break;
            case HeroState.InActive:
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

        _prevState = _currentState;
        ExitState(_currentState);
        _currentState = newState;
        EnterState(_currentState, isComboConnection);

        Debug.Log($"[{gameObject.name} 상태 변경] : {_currentState} ");
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

    private void UpdateChargeCheck()
    {
        if (!CanReceiveInput) return;

        // 물리적으로 입력을 누르고 있는 상태라면 누적 시간을 계산
        if (ActionRegistry.IsButtonHolding)
        {
            float elapsedTime = Time.time - _buttonPressStartTime;

            // 0.4초 이상 꾹 누르고 있고
            if (elapsedTime >= _buttonHoldTime)
            {
                // 애니메이터에 차징 신호전달
                Animation.SetIsCharging(true);
            }
        }
    }

    #endregion


    #region [Input Event Handler 영역]

    private void HandleDashStartedInput()
    {
        if (!CanReceiveInput) return;

        // 궁극기 중에는 대쉬 차단
        if (CurrentState == HeroState.Ult) return;

        // Dodge 쿨타임 중이면 대시 차단
        if (!Movement.CanDodge) return;

        // HeroMovement - Movement Settings 필드 관리
        Movement.SetIsWalkMode(false);
        Movement.SetIsDashActive(true);  // Performed로 이사하는거 고려중 -> 조작감 부분이라 내 취향껏 가면될듯.

        bool isBaseState = CurrentState == HeroState.Idle || CurrentState == HeroState.Walk ||
                            CurrentState == HeroState.Run || CurrentState == HeroState.Sprint ||
                            CurrentState == HeroState.Attack || CurrentState == HeroState.Skill;

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
        //if (CurrentState == HeroState.Ult) return;
        //if (_dodgeCooldownTimer > 0f) return;
    }

    private void HandleWalkPerformedInput()
    {
        if (!CanReceiveInput) return;

        Movement.SetIsWalkMode(!Movement.IsWalkMode);
    }

    private void HandleAttackStartedInput()
    {
        if (!CanReceiveInput) return;
        if (CurrentState == HeroState.DodgeCounter) return;

        // 수동 차징 타이머 기록 시작
        _buttonPressStartTime = Time.time;
        ActionRegistry.SetIsButtonHolding(true);

        // 해당 입력으로 전이될 상태
        _toState = HeroState.Attack;

        // 언제든지 해당 상태로 전이 가능한 기본 상태
        bool isBaseState = CurrentState == HeroState.Idle || CurrentState == HeroState.Walk ||
                            CurrentState == HeroState.Run || CurrentState == HeroState.Sprint;
        // 대쉬 공격이 발동될 상태
        bool canDashAttack = CurrentState == HeroState.Sprint || CurrentState == HeroState.Dodge;

        // 콤보 연계 분기
        // 기존 상태에서 같은 입력을 받으면 콤보연계 처리
        if (_currentState == _toState || (Animation.GetIsDashAttack() && _currentState == HeroState.Attack) )      
        {
            if (ActionRegistry.IsComboLinkable && !ActionRegistry.IsComboReserved)
            {
                Movement.UpdateAnimMoveDirToTarget(_heroAxis);
                ActionRegistry.SetIsComboReserved(true);
            }
        }
        else if (CurrentState == HeroState.ParryIn)    // 패링 반격
        {
            if (ActionRegistry.IsParryCounterWindowOpen)     // 패링 모션이후 Idle로 돌아가는 모션에 입력에도 패링반격 허용
            {
                ActionRegistry.SetIsParryCounterWindowOpen(false);
                ActionRegistry.SetIsParryCounterResereved(true);

                ChangeState(HeroState.ParryCounter);
                return;
            }
            else   // 일반적인 패링 반격
            {
                //Movement.UpdateAnimMoveDirToTarget(_heroAxis);
                ActionRegistry.SetIsParryCounterResereved(true);
            }
        }
        else if (CurrentState == HeroState.Dodge)
        {
            if (ActionRegistry.ConsumeDodgeCounter())
            {
                ChangeState(HeroState.DodgeCounter);
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
    private void HandleAttackPerformedInput()
    {
        if (!CanReceiveInput) return;

        // 이제 차징 진입은 Update문에서 정교하게 누적 시간 초과를 계산하여 판단
        ActionRegistry.SetIsButtonHolding(true);
        ActionRegistry.CloseCancelWindow();
    }
    private void HandleAttackCanceledInput()
    {
        if (!CanReceiveInput) return;

        ActionRegistry.SetIsButtonHolding(false);
        Animation.SetIsCharging(false);
    }

    private void HandleSkillStartedInput()
    {
        if (!CanReceiveInput) return;

        // 수동 차징 타이머 기록 시작
        _buttonPressStartTime = Time.time;
        ActionRegistry.SetIsButtonHolding(true);

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
                Movement.UpdateAnimMoveDirToTarget(_heroAxis);
                ActionRegistry.SetIsComboReserved(true);
            }
        }
        else if (isBaseState || ActionRegistry.IsCancelWindowOpen)
        {
            // 이제 ExitState에서 통상적으로 처리
            // Animation.ClearEveryTrigger();

            ChangeState(_toState);

            ActionRegistry.ResetComboIndex();
            ActionRegistry.SetIsComboReserved(false);
            ActionRegistry.SetIsComboLinkable(false);
        }
    }
    private void HandleSkillPerformedInput()
    {
        if (!CanReceiveInput) return;

        ActionRegistry.SetIsButtonHolding(true);
    }
    private void HandleSkillCanceledInput()
    {
        if (!CanReceiveInput) return;

        ActionRegistry.SetIsButtonHolding(false);
        Animation.SetIsCharging(false);
    }

    private void HandleUltStartedInput()
    {
        if (!CanReceiveInput) return;

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
    }

    // IsCombat 프로퍼티의 값 변경이 일어날때 호출될 핸들러 함수
    private void HandleCombatTransition(bool isCombat)
    {
        // 하드 록온 상태가 아니라면 소프트 록온 타겟을 null
        if (!_isCombat && !LockOnManager.Instance.IsHardLockActive)
        {
            LockOnManager.Instance.RefreshSoftLockTarget();
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
        if (IsCombat) AnimationEventHandler.AttachToHand();
        else AnimationEventHandler.AttachToBack();
    }

    // Animation 요소들 리셋 및 Idle로 명시적 변환
    public void BackToIdle()
    {
        // 애니메이터 파라미터 초기화 과정 및 Idle로 전환
        ChangeState(HeroState.Idle);

        Animation.SetComboIndex(0);
        Animation.SetIsCharging(false);
        Animation.SetIsDashAttack(false);

        Animation.ClearEveryTrigger();

        Animation.CrossFadeToIdle(0.1f);
    }

    public void EnableInput()
    {
        _isInputEnabled = true;
    }

    public void DisableInput()
    {
        _isInputEnabled = false;
    }

    public void SetCounterTarget(IAttacker enemy)
    {
        if(enemy is Enemy target)
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
        if (_parryResolveState != ParryResolveState.Waiting) return;

        _parryResolveState = ParryResolveState.NoAttack;
        Animation.ResetTriggerParry(); 
        Animation.ResetTriggerParryImpact();
        Animation.CrossFadeToParryAfterNoAttack(0.1f);
    }
    /// <summary>
    /// CombatManager - ProcessHit 에서 호출될
    /// Parry_Impact 재생 메서드
    /// </summary>
    public void ResolveParryImpact(Enemy attacker)
    {
        if (CurrentState != HeroState.ParryIn) return;
        if (_parryResolveState != ParryResolveState.Waiting) return;
        if (attacker == null) return;

        _parryResolveState = ParryResolveState.Impact;

        SetCounterTarget(attacker);
        ActionRegistry.ActivateAnimMove();
        Movement.SetCurrentParryAssistSpeed(Movement.MaxParryAssistSpeed);

        Animation.ResetTriggerParry();
        Animation.TriggerImpactParry();
    }

    public void TriggerDeath() 
    {
        if (CurrentState == HeroState.Dead) return;

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
        if (ActionRegistry.IsInvincible) return false;
        if (ActionRegistry.IsOffFieldActionActive) return false;    // 잔상 시퀀스의 캐릭터의 피격 차단

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
    }

    #endregion




}