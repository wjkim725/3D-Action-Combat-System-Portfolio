public class EnemyDecision
{
    private readonly EnemyBlackboard _blackboard;
    private readonly float _chaseEnterDistance;
    private readonly float _chaseExitDistance;
    private readonly float _orbitEnterDistance;
    private readonly float _orbitExitDistance;

    private bool _isChasing;
    private bool _isOrbiting;

    #region [초기화]

    /// <summary>
    /// AI가 소유한 Blackboard와 개별 거리 판단 기준을 전달받음.
    /// </summary>
    public EnemyDecision(
        EnemyBlackboard blackboard,
        float chaseEnterDistance,
        float chaseExitDistance,
        float orbitEnterDistance,
        float orbitExitDistance)
    {
        _blackboard = blackboard;
        _chaseEnterDistance = chaseEnterDistance;
        _chaseExitDistance = chaseExitDistance;
        _orbitEnterDistance = orbitEnterDistance;
        _orbitExitDistance = orbitExitDistance;
    }

    #endregion

    #region [판단 갱신]

    /// <summary>
    /// 현재 거리를 기준으로 이동 범위와 실행 가능한 공격 패턴을 갱신함.
    /// </summary>
    public void Tick()
    {
        UpdateChaseState();
        UpdateOrbitState();
        UpdateAttackState();
    }

    /// <summary>
    /// 추격 진입·이탈 거리 사이에서 기존 판단을 유지하며 추격 범위를 갱신함.
    /// </summary>
    private void UpdateChaseState()
    {
        float distance = _blackboard.DistanceToTarget;

        if (!_isChasing)
        {
            if (distance <= _chaseEnterDistance)
                _isChasing = true;
        }
        else
        {
            if (distance >= _chaseExitDistance)
                _isChasing = false;
        }

        _blackboard.IsInChaseRange = _isChasing;
    }

    /// <summary>
    /// 주변 이동 진입·이탈 거리 사이에서 기존 판단을 유지하며 범위를 갱신함.
    /// </summary>
    private void UpdateOrbitState()
    {
        float distance = _blackboard.DistanceToTarget;

        if (!_isOrbiting)
        {
            if (distance <= _orbitEnterDistance)
                _isOrbiting = true;
        }
        else
        {
            if (distance >= _orbitExitDistance)
                _isOrbiting = false;
        }

        _blackboard.IsInOrbitDistance = _isOrbiting;
    }

    /// <summary>
    /// 기존 패턴 선택 결과를 Blackboard의 공격 가능 여부에 반영함.
    /// </summary>
    private void UpdateAttackState()
    {
        if (_blackboard.Owner.AIMode != EnemyAIMode.Normal)
        {
            _blackboard.SelectedAttackPattern = null;
            _blackboard.CanAttack = false;
            return;
        }

        _blackboard.SelectedAttackPattern =
            _blackboard.Owner.Pattern.GetAttackPattern(
                _blackboard.DistanceToTarget);

        _blackboard.CanAttack =
            _blackboard.SelectedAttackPattern != null;
    }

    #endregion
}
