using System.Collections.Generic;
using UnityEngine;


public class Enemy : MonoBehaviour, IDamageable, IAttacker
{
    // Enemy 사망 이벤트 선언
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
    

    // IDamageable
    IStatus IDamageable.Status => Status;
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
            Movement.RotateToTarget(_heroTransform.position);
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
        if (Groggy != null)
        {
            Groggy.OnGroggyStart -= HandleGroggyStart;
            Groggy.OnGroggyEnd -= HandleGroggyEnd;
        }
    }


    private void EnterState(EnemyState newState)
    {
        _currentState = newState;

        switch (CurrentState)
        {
            case EnemyState.Idle:
                _isAIControlSuspended = false;   
                Animation.SetIsIdle(true);
                Movement.StopMoving(); // 대기 시 정지
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
                    Movement.StartOrbiting(_heroTransform);
                }
                break;
            case EnemyState.Attack:
                Movement.StopMoving(); // 공격 시작 시 관성 제거 및 정지
                if (_currentAttackPattern != null)
                {
                    Animation.TriggerAttack(_currentAttackPattern.Data.animationTriggerName);
                }
                break;
            case EnemyState.Hit:
                _isAIControlSuspended = true;
                Movement.StopMoving();
                //Animation.PlayHitReaction(_currentHitType);
                break;
            case EnemyState.Groggy:
                _isAIControlSuspended = true;
                Movement.StopMoving();
                Animation.SetIsGroggy(true);

                Debug.Log($"[Groggy 진입] AI Suspended = {_isAIControlSuspended}");

                //ResetAttackSequence(); // 피격/그로기 시 진행 중인 공격 대장부 초기화
                break;
            case EnemyState.Dead:
                OnDied?.Invoke(this);

                _isAIControlSuspended = true;
                Movement.StopMoving();
                
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

    private void ExitState(EnemyState currentState)
    {
        switch (CurrentState)
        {
            case EnemyState.Idle:
                Animation.SetIsIdle(false);
                break;
            case EnemyState.Chase:
            case EnemyState.Orbit:
                Movement.StopMoving();
                break;
            case EnemyState.Attack:
                if (CombatManager.Instance != null)
                {
                    CombatManager.Instance.CloseDefenseWindow((IAttacker)this);
                }

                if (_currentAttackPattern != null)
                {
                    Animation.ResetTriggerAttack(_currentAttackPattern.Data.animationTriggerName);
                }
                break;
            case EnemyState.Hit:
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

    public void ChangeState(EnemyState newState)
    {
        if (CurrentState == newState) return;

        // Groggy 중에는 Dead 외의 상태 전환 차단
        if (Status.IsGroggy &&
            CurrentState == EnemyState.Groggy &&
            newState != EnemyState.Dead)
        {
            return;
        }

        ExitState(CurrentState);
        EnterState(newState);
        Debug.Log($"{gameObject.name} 상태 변경 : {newState}");
    }





    public void ExecuteAttack(EnemyAttackPattern pattern)
    {
        if (CurrentState == EnemyState.Attack ||
            Status.IsGroggy ||
            Status.IsDead)
        {
            return;
        }

        if (pattern == null)
            return;

        _currentAttackPattern = pattern;

        Pattern.MarkPatternExecuted(pattern);

        _isAIControlSuspended = true;

        ChangeState(EnemyState.Attack);
    }

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
            HandleHitReaction(hitInfo);
        }

        return true;
    }

    // 타격당할 때 경직도 처리 메서드
    private void HandleHitReaction(HitInfo hitInfo)
    {
        if (Status.IsGroggy || Status.IsDead) return;

        switch (hitInfo.hitType)
        {
            case HitType.None:
                Debug.Log("경직 없음");
                break;
            case HitType.Light:
                Debug.Log("약경직 발동");
                break;
            case HitType.Heavy:
                Debug.Log("강경직 발동");
                break;
            case HitType.Knockback:
                Debug.Log("넉백 발동");
                break;
            case HitType.Parry:
                Debug.Log("패링 발동");
                break;
            default:
                break;
        }
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

    private void HandleGroggyEnd()
    {
        ChangeState(EnemyState.Idle);
    }

    public void OnHitSuccess(float payBackEnegy, float payBackDecibel)
    {
        Debug.Log($"{gameObject.name} : OnHitSuccess");
    }
}
