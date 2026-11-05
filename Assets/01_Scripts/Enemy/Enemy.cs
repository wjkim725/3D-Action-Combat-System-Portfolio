using System.Collections.Generic;
using UnityEngine;


public class Enemy : MonoBehaviour, IDamageable, IAttacker
{
    public event System.Action<Enemy> OnDied;

    public EnemyStatus Status { get; private set; }
    public EnemyGroggy Groggy { get; private set; }
    public EnemyAnimation Animation { get; private set; }
    public EnemyAnimationEventHandler AnimationEventHandler { get; private set; }
    public EnemyMovement Movement { get; private set; }
    public EnemyPattern Pattern { get; private set; }
    public EnemyHitChecker HitChecker { get; private set; }
    public EnemyAI AI { get; private set; }
    public EnemyActionRegistry ActionRegistry { get; private set; }
    public EnemyVFX VFX { get; private set; }
    public EnemyHitReaction HitReaction { get; private set; }
    public EnemyAudio Audio { get; private set; }
    

    // IDamageable
    IStatus IDamageable.Status => Status;
    public Transform DamageTextAnchor =>
        _camPivot != null ? _camPivot : transform;
    // IAttacker
    IStatus IAttacker.Status => Status;
    public Transform AttackerTransform => this.transform;


    public EnemyRank Rank = EnemyRank.Elite;

    public LayerMask HeroLayerMask => _heroLayerMask;
    public EnemyState CurrentState => _currentState;
    public Transform HeroTransform => _heroTransform;

    // 공격 중 AI 판단을 잠시 끌 플래그 - 애니메이션 이벤트나 EnterState-Idle 로 제어
    public bool IsAIControlSuspended => _isAIControlSuspended;
    public EnemyAttackPattern CurrentAttackPattern => _currentAttackPattern;
    public List<EnemyPatternDataSO> PatternDataList => _patternDataList;

    [Header("AI Behavior")]
    [SerializeField] private EnemyAIMode _aiMode = EnemyAIMode.Normal;

    public EnemyAIMode AIMode => _aiMode;

    // 패턴 데이터 에셋 : 패턴들을 리스트로 관리
    [SerializeField]
    private List<EnemyPatternDataSO> _patternDataList = new();
    [SerializeField]
    private LayerMask _heroLayerMask;
    [SerializeField]
    private EnemyState _currentState = EnemyState.Idle;
    [SerializeField]
    private Transform _heroTransform;
    [SerializeField]
    private bool _isAIControlSuspended = false;

    private EnemyAttackPattern _currentAttackPattern;

    // Enemy HUD 연결 필요
    [SerializeField] private GameObject _enemyHUD;

    // Camera Pivot
    [SerializeField] private Transform _camPivot;
    public Transform CamPivot =>
        _camPivot != null ? _camPivot : transform;



    private void Awake()
    {
        Status = GetComponent<EnemyStatus>();
        Groggy = GetComponent<EnemyGroggy>();
        Animation = GetComponent<EnemyAnimation>();
        AnimationEventHandler = GetComponent<EnemyAnimationEventHandler>();
        Movement = GetComponent<EnemyMovement>();
        Pattern = GetComponent<EnemyPattern>();
        HitChecker = GetComponent<EnemyHitChecker>();

        AI = GetComponent<EnemyAI>();
        ActionRegistry = GetComponent<EnemyActionRegistry>();
        VFX = GetComponent<EnemyVFX>();
        HitReaction = GetComponent<EnemyHitReaction>();
        Audio = GetComponent<EnemyAudio>();

        if (_camPivot == null)
        {
            Debug.LogError(
                $"{name}: Enemy의 CamPivot이 할당되지 않았습니다.",
                this
            );
        }
    }

    private void Start()
    {
        // 타겟 갱신
        if (PartyStateManager.Instance != null)
            _heroTransform = PartyStateManager.Instance.CurrentHero.transform;

        
    }

    private void Update()
    {
        // 실시간 타겟 갱신
        if (PartyStateManager.Instance != null)
            _heroTransform = PartyStateManager.Instance.CurrentHero.transform;

        // 공격 중일 때 뇌는 정지하지만, 몸은 플레이어를 계속 주시/회전해야 하므로 육체 전용 Update만 처리
        if (CurrentState == EnemyState.Attack && _heroTransform != null)
        {
            Movement.RotateToTargetDuringAttack(_heroTransform.position);
        }
    }





    private void OnEnable()
    {
        // 그로기 컴포넌트 이벤트 구독
        if (Groggy != null)
        {
            Groggy.OnGroggyStart += HandleGroggyStart;
            Groggy.OnGroggyEnd += HandleGroggyEnd;
        }
    }

    private void OnDisable()
    {
        CombatManager.Instance?.CloseDefenseWindow(this);
        if (Groggy != null)
        {
            Groggy.OnGroggyStart -= HandleGroggyStart;
            Groggy.OnGroggyEnd -= HandleGroggyEnd;
        }
    }


    /// <summary>
    /// 새 상태에 필요한 이동, 애니메이션과 생명주기 처리를 수행함.
    /// </summary>
    private void EnterState(EnemyState newState)
    {
        _currentState = newState;

        switch (CurrentState)
        {
            case EnemyState.Idle:
                _isAIControlSuspended = false;
                Animation.SetIsIdle(true);
                Movement.StopMovingImmediate(); // 대기 시 정지
                break;
            case EnemyState.CombatIdle:
                _isAIControlSuspended = true;
                Movement.StopMovingImmediate();
                HitReaction.ActivateSuperArmor(HitReactionResistanceLevel.Light);
                Animation.TriggerCombatIdle();

                break;
            case EnemyState.Chase:
                if (_heroTransform != null)
                {
                    Movement.StartMoving(_heroTransform);
                }
                break;
            case EnemyState.Orbit:
                if (_heroTransform != null)
                {
                    Movement.StartOrbitingProcess(_heroTransform);
                }
                break;
            case EnemyState.Attack:
                // 방향 이동은 마지막 자세를 유지한 채 공격으로 블렌딩함.
                Movement.StopMovingImmediate(
                    resetMovementAnimation: !Animation.UsesDirectionalMovement);
                Movement.BeginAttackRotationTracking(); // Hero 추적용 회전 허용

                if (_currentAttackPattern != null && _currentAttackPattern.Data != null)
                {
                    HitReaction.ActivateSuperArmor(_currentAttackPattern.Data.superArmorLevel);

                    Animation.TriggerAttack(_currentAttackPattern.Data.animationTriggerName);
                }
                else
                {
                    /*
                     * 유효한 패턴 데이터가 없다면
                     * 이전 슈퍼아머가 남지 않도록 초기화
                     */
                    HitReaction.DeactivateSuperArmor();
                }
                break;
            case EnemyState.Hit:
                _isAIControlSuspended = true;
                Movement.StopMovingImmediate();
                break;
            case EnemyState.Groggy:
                _isAIControlSuspended = true;
                Movement.StopMovingImmediate();
                Animation.SetIsGroggy(true);

                // 현재 별도의 GroggySFX가 없으니 피격음으로 대체
                Audio?.PlayHit();

                Debug.Log($"[Groggy 진입] AI Suspended = {_isAIControlSuspended}");

                //ResetAttackSequence(); // 피격/그로기 시 진행 중인 공격 대장부 초기화
                break;
            case EnemyState.Dead:
                _isAIControlSuspended = true;
                Movement.StopMovingImmediate();
                Groggy?.CancelGroggyOnDeath();

                // 현재 별도의 DeadSFX가 없으니 피격음으로 대체
                Audio?.PlayHit();

                OnDied?.Invoke(this);

                // 사망시 HUD 비활성화
                if (_enemyHUD != null)
                {
                    _enemyHUD.SetActive(false);
                }

                Animation.TriggerDeath();
                //Destroy 는 애니메이션 이벤트 처리

                break;
        }
    }

    /// <summary>
    /// 이전 상태를 정리하고 공격 상태 종료 시 개별·전역 쿨타임을 시작함.
    /// </summary>
    private void ExitState(EnemyState currentState, EnemyState nextState)
    {
        switch (CurrentState)
        {
            case EnemyState.Idle:
                Animation.SetIsIdle(false);
                break;
            case EnemyState.CombatIdle:
                HitReaction.DeactivateSuperArmor();
                Animation.ResetTriggerCombatIdle();
                break;
            case EnemyState.Chase:
            case EnemyState.Orbit:
                Movement.StopMovingImmediate(
                    resetMovementAnimation:
                        nextState != EnemyState.Attack ||
                        !Animation.UsesDirectionalMovement);
                break;
            case EnemyState.Attack:
                /*
                 * 정상 종료, 피격, Groggy, 사망 등
                 * 어떤 이유로 Attack 상태를 벗어나더라도
                 * 활성 슈퍼아머를 먼저 해제
                 */
                HitReaction.DeactivateSuperArmor();

                Movement.StopAttackRotationTracking();

                if (CombatManager.Instance != null)
                {
                    CombatManager.Instance.CloseDefenseWindow((IAttacker)this);
                }

                if (_currentAttackPattern != null && _currentAttackPattern.Data != null)
                {
                    Animation.ResetTriggerAttack(_currentAttackPattern.Data.animationTriggerName);
                }

                Pattern.MarkPatternEnded(_currentAttackPattern);
                break;
            case EnemyState.Hit:
                Animation.ResetTriggerHit();
                HitReaction.OnExitReactionState();
                break;
            case EnemyState.Groggy:
                _isAIControlSuspended = false;

                Animation.SetIsGroggy(false);
                break;
            case EnemyState.Dead:
                Animation.ResetTriggerDeath();
                break;
        }
    }

    /// <summary>
    /// 사망 및 그로기 전환 제한을 확인한 뒤 상태를 변경함.
    /// </summary>
    public void ChangeState(EnemyState newState)
    {
        if (CurrentState == newState) return;

        if (_aiMode != EnemyAIMode.Normal &&
            (newState == EnemyState.Attack || newState == EnemyState.CombatIdle)) return;

        if (_aiMode == EnemyAIMode.Stationary &&
            (newState == EnemyState.Chase || newState == EnemyState.Orbit)) return;

        if ((Status.IsDead || CurrentState == EnemyState.Dead) &&
            newState != EnemyState.Dead)
        {
            return;
        }

        // Groggy 중에는 Dead 외의 상태 전환 차단
        if (Status.IsGroggy &&
            CurrentState == EnemyState.Groggy &&
            newState != EnemyState.Dead)
        {
            return;
        }

        ExitState(CurrentState, newState);
        EnterState(newState);
        Debug.Log($"{gameObject.name} 상태 변경 : {newState}");
    }


    // CombatIdle 탈출하고 Idle 로 전환
    // CombatIdle 애니메이션 종료 이벤트 핸들러에 배치
    public void CompleteCombatIdle()
    {
        if (CurrentState != EnemyState.CombatIdle) return;

        Debug.Log("CompleteCombatIdle");

        ChangeState(EnemyState.Idle);

        Debug.Log(
        $"[CombatIdle 종료] " +
        $"State: {CurrentState}, " +
        $"AI Suspended: {_isAIControlSuspended}"
    );
    }

    /// <summary>
    /// 실행할 패턴을 저장하고 AI 행동 실행을 중단한 뒤 공격 상태로 전환함.
    /// </summary>
    public void ExecuteAttack(EnemyAttackPattern pattern)
    {
        if (_aiMode != EnemyAIMode.Normal ||
            CurrentState == EnemyState.Attack ||
            Status.IsGroggy ||
            Status.IsDead)
        {
            return;
        }

        if (pattern == null)
            return;

        _currentAttackPattern = pattern;

        _isAIControlSuspended = true;

        ChangeState(EnemyState.Attack);
    }

    #region [AI 행동 모드]

    /// <summary>
    /// 행동 모드를 변경하고 기존 자율 이동과 공격을 정리함.
    /// 피격, 그로기와 사망 상태는 중단하지 않으며 비활성 소환체에도 설정할 수 있음.
    /// </summary>
    public void SetAIMode(EnemyAIMode mode)
    {
        if (_aiMode == mode) return;
        _aiMode = mode;

        // 비활성 프리팹은 Awake 이전에 모드만 지정할 수 있음.
        if (Status == null) return;

        if (CurrentState != EnemyState.Hit &&
            CurrentState != EnemyState.Groggy &&
            CurrentState != EnemyState.Dead && !Status.IsDead)
        {
            ChangeState(EnemyState.Idle);
        }

        Movement.StopMovingImmediate();
        Movement.CompleteOrbiting();
        ActionRegistry.ResetPatternSequence();
        AI.ResetBehavior();
    }

    #endregion

    // Idamageable 인터페이스 메서드 구현
    public bool TakeDamage(HitInfo hitInfo)
    {
        if (Status.IsDead) return false;

        // 피격자 데미지 및 현재 체력 처리
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

        // 그로기 및 경직 처리
        if (Groggy != null)
        {
            Groggy.GroggyLogic(hitInfo);
        }

        // 일반 피격 반응 처리
        // 메서드 내부 규칙으로 그로기 상태에서는 피격반응 X
        if (HitReaction != null)
        {
            HitReaction.TryStartReaction(hitInfo);
        }

        return true;
    }

    
    /// <summary>
    /// EnemyStatus는 HP와 사망 여부만 관리하고,
    /// 사망에 따른 상태 전환과 오브젝트 생명주기는
    /// 하위 컴포넌트를 통합 관리하는 Enemy 본체에서 처리
    /// </summary>
    private void TriggerDeath()
    {
        if (CurrentState == EnemyState.Dead) return;

        Debug.Log($"{gameObject.name} 죽음");

        ChangeState(EnemyState.Dead);
    }

    /// <summary>
    /// 사망 애니메이션 종료 후 Enemy 오브젝트 제거
    /// </summary>
    public void CompleteDeath()
    {
        if (CurrentState != EnemyState.Dead) return;

        Destroy(gameObject);
    }

    private void HandleGroggyStart()
    {
        ChangeState(EnemyState.Groggy);
    }

    /// <summary>
    /// 살아 있는 적의 정상 그로기 종료 시 대기 상태로 복귀함.
    /// </summary>
    private void HandleGroggyEnd()
    {
        if (Status.IsDead || CurrentState != EnemyState.Groggy) return;

        ChangeState(EnemyState.Idle);
    }

    public void OnHitSuccess(float payBackEnegy, float payBackDecibel)
    {
        Debug.Log($"{gameObject.name} : OnHitSuccess");
    }
}
