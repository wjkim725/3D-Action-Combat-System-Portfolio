using UnityEngine;

[RequireComponent(typeof(Enemy))]
public abstract class EnemyAI : MonoBehaviour
{
    [Header("Chase Range")]
    [SerializeField, Min(0f)]
    private float _chaseEnterDistance = 15f;

    [SerializeField, Min(0f)]
    private float _chaseExitDistance = 17f;

    [Header("Orbit Range")]
    [SerializeField, Min(0f)]
    private float _orbitEnterDistance = 4f;

    [SerializeField, Min(0f)]
    private float _orbitExitDistance = 6f;

    [Header("Orbit Cooldown")]
    [SerializeField, Min(0f)]
    private float _orbitCooldownTime = 2f;

    private float _lastOrbitEndedTime = float.NegativeInfinity;

    public bool IsOrbitCooldownActive =>
        Time.time < _lastOrbitEndedTime + _orbitCooldownTime;

    private Enemy _enemy;
    private BTNode _btRoot;
    private EnemyBlackboard _blackboard;
    private EnemySensor _sensor;
    private EnemyDecision _decision;
    private BTAction_Orbit _passiveOrbitAction;

    // 자식 AI는 같은 Blackboard를 사용하여 트리를 조립함.
    protected EnemyBlackboard Blackboard => _blackboard;

    #region [생명주기]

    protected void Awake()
    {
        ValidateDecisionRanges();
        _enemy = GetComponent<Enemy>();

        _blackboard = new EnemyBlackboard();
        _blackboard.Owner = _enemy;

        _sensor = new EnemySensor(_blackboard);
        _decision = new EnemyDecision(
            _blackboard,
            _chaseEnterDistance,
            _chaseExitDistance,
            _orbitEnterDistance,
            _orbitExitDistance
        );
    }

    protected void Start()
    {
        _btRoot = SetupBehaviorTree();
        _passiveOrbitAction = new BTAction_Orbit(_blackboard);

        Debug.Assert(
            _btRoot != null,
            $"{name}: AI 행동 트리가 구성되지 않았습니다.",
            this
        );
    }

    protected void Update()
    {
        if (_enemy.Status.IsDead ||
            _enemy.CurrentState == EnemyState.Dead)
        {
            return;
        }

        // 행동 실행이 중단된 동안에도 감지·판단 정보는 갱신함.
        _sensor.Tick();
        _decision.Tick();

        if (_btRoot == null || _enemy.IsAIControlSuspended)
        {
            return;
        }

        switch (_enemy.AIMode)
        {
            case EnemyAIMode.Normal:
                _btRoot.Evaluate();
                break;
            case EnemyAIMode.Passive:
                EvaluatePassiveBehavior();
                break;
            case EnemyAIMode.Stationary:
                _enemy.ChangeState(EnemyState.Idle);
                break;
        }
    }

    protected void OnValidate()
    {
        ValidateDecisionRanges();
    }

    #endregion

    #region [행동 트리 구성]

    /// <summary>
    /// 모드 변경 시 중단된 행동 노드와 패턴 선택 정보를 초기화함.
    /// Orbit 대기 시간은 유지하여 모드 변경으로 대기를 건너뛰지 않도록 함.
    /// </summary>
    public void ResetBehavior()
    {
        if (_blackboard == null) return;

        _blackboard.SelectedAttackPattern = null;
        _blackboard.CanAttack = false;
        _blackboard.NextCombatWaitType = EnemyCombatWaitType.Orbit;

        if (_btRoot == null) return;
        _btRoot = SetupBehaviorTree();
        _passiveOrbitAction = new BTAction_Orbit(_blackboard);
    }

    /// <summary>
    /// 공격하지 않고 추적하거나 Orbit을 수행하며 이동 완료 후 Idle로 대기함.
    /// 피격 중에는 호출되지 않아 중단된 Orbit의 목적지와 진행 시간을 유지함.
    /// </summary>
    private void EvaluatePassiveBehavior()
    {
        if (!_blackboard.HasTarget || !_blackboard.IsInChaseRange)
        {
            _enemy.ChangeState(EnemyState.Idle);
            return;
        }

        if (!_blackboard.IsInOrbitDistance)
        {
            _enemy.ChangeState(EnemyState.Chase);
            return;
        }

        if (IsOrbitCooldownActive)
        {
            _enemy.ChangeState(EnemyState.Idle);
            return;
        }

        if (_passiveOrbitAction.Evaluate() == NodeState.Success)
        {
            _enemy.ChangeState(EnemyState.Idle);
        }
    }

    /// <summary>
    /// Orbit 완료 시 공격 쿨타임과 독립된 다음 이동 대기 시간을 시작함.
    /// 피격으로 잠시 중단된 경우에는 호출하지 않음.
    /// </summary>
    public void MarkOrbitEnded()
    {
        _lastOrbitEndedTime = Time.time;
    }

    /// <summary>
    /// 공통 Blackboard를 사용하는 적 전용 행동 트리를 구성함.
    /// </summary>
    protected abstract BTNode SetupBehaviorTree();

    #endregion

    #region [판단 설정 검증]

    /// <summary>
    /// 판단 거리를 음수 이상으로 보정하고 이탈 거리가 진입 거리보다 작아지지 않도록 처리함.
    /// </summary>
    private void ValidateDecisionRanges()
    {
        _chaseEnterDistance = Mathf.Max(0f, _chaseEnterDistance);
        _chaseExitDistance = Mathf.Max(_chaseEnterDistance, _chaseExitDistance);
        _orbitEnterDistance = Mathf.Max(0f, _orbitEnterDistance);
        _orbitExitDistance = Mathf.Max(_orbitEnterDistance, _orbitExitDistance);
        _orbitCooldownTime = Mathf.Max(0f, _orbitCooldownTime);
    }

    #endregion
}
