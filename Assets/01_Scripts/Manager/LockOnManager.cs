using System;
using UnityEngine;

public class LockOnManager : MonoBehaviour
{
    public static LockOnManager Instance { get; private set; }

    // 하드 락온 활성화·해제 및 실제 대상 변경 시 카메라에 전달
    public event Action<Transform> OnLockOnTargetChanged;

    [Header("타겟 탐색 설정")]
    [SerializeField] private LayerMask _enemyLayerMask;
    [SerializeField] private float _targetSearchRange = 20f;

    [Header("자력 유도 설정")]
    [SerializeField] private float _magneticAssistRange = 10f;
    [SerializeField] private float _magneticStopDistance = 2.5f;

    [Header("소프트 록온 설정")]
    [SerializeField, Range(-1f, 1f)] private float _softTargetDirectionThreshold = 0.5f;

    public bool IsHardLockActive => _isHardLockActive;
    public float MaxMagneticRange => _magneticAssistRange;
    public float MinStopDistance => _magneticStopDistance;
    public float LockOnRange => _targetSearchRange;

    // 소프트·하드 록온에서 공통으로 사용하는 현재 전투 타겟
    public Transform MagneticTarget { get; private set; }

    private const float DirectionScoreWeight = 10f;
    private const float DistanceScoreWeight = 0.2f;
    private const float BossPriorityBonus = 5f;

    private bool _isHardLockActive;
    private bool _isInputEventSubscribed;
    private Hero _activeHero;
    // 사망 처리 받을 Enemy 필드
    // -> 해당 Enemy 가 Magnetic Target 인지 필터링
    private Enemy _subscribedMagneticTargetEnemy;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void Start()
    {
        SubscribeInputEvents();
    }

    private void OnDestroy()
    {
        UnsubscribeInputEvents();
        UnsubscribeMagneticTargetDeathEvent();
    }


    #region [Input Handler]

    /// <summary>
    /// 하드 락온 버튼 입력(휠)에 따라 활성화 또는 해제
    /// </summary>
    private void HandleLockOnPerformedInput()
    {
        if (_activeHero == null) return;

        if (_isHardLockActive)
        {
            ClearLockOn();
            return;
        }

        TryEnableHardLock();
    }

    /// <summary>
    /// 하드 락온 중 마우스 입력 방향의 다음 타겟으로 즉시 변경
    /// Hero의 몸 회전은 HeroMovement에서 매 프레임 보간 처리
    /// </summary>
    private void HandleHardLockOnSwitch(bool toRight)
    {
        if (!_isHardLockActive || _activeHero == null) return;

        Transform nextTarget = FindNextHardLockTarget(MagneticTarget, toRight);
        if (nextTarget == null || nextTarget == MagneticTarget) return;

        SetMagneticTarget(nextTarget);
        _activeHero.Movement.UpdateAnimMoveDirToTarget(_activeHero.HeroAxis);

        OnLockOnTargetChanged?.Invoke(MagneticTarget);
    }

    /// <summary>
    /// 소프트 록온 중 이동 입력 방향에 있는 적으로 타겟 변경
    /// 회피 입력 중에는 의도하지 않은 타겟 변경을 차단
    /// </summary>
    private void HandleSoftLockOnSwitch(Vector2 moveInput)
    {
        if (!CanSwitchSoftTarget()) return;

        Vector3 inputDirection = ConvertMoveInputToWorldDirection(moveInput);
        Transform previousTarget = MagneticTarget;

        RefreshDirectionalSoftLockTarget(inputDirection);

        if (MagneticTarget == previousTarget) return;

        // 타겟 변경 시 자력 유도 방향만 즉시 갱신
        // Hero의 몸 회전은 HeroMovement에서 보간 처리
        _activeHero.Movement.UpdateAnimMoveDirToTarget(_activeHero.HeroAxis);
    }

    #endregion


    #region [하드 록온 관리]

    /// <summary>
    /// 우선순위 타겟을 찾아 하드 락온 활성화
    /// </summary>
    private void TryEnableHardLock()
    {
        Transform target = IsValidTarget(MagneticTarget) ? MagneticTarget : GetPriorityTarget();
        if (target == null) return;

        SetMagneticTarget(target);
        SetHardLockState(true);
        _activeHero.Movement.UpdateAnimMoveDirToTarget(_activeHero.HeroAxis);

        OnLockOnTargetChanged?.Invoke(MagneticTarget);
    }

    /// <summary>
    /// 하드 락온 해제 및 현재 타겟 초기화
    /// </summary>
    public void ClearLockOn()
    {
        if (!_isHardLockActive) return;

        SetHardLockState(false);
        SetMagneticTarget(null);

        OnLockOnTargetChanged?.Invoke(null);
    }

    /// <summary>
    /// LockOnManager와 InputManager의 하드 락온 상태 동기화
    /// </summary>
    private void SetHardLockState(bool isActive)
    {
        _isHardLockActive = isActive;

        if (InputManager.Instance != null)
        {
            InputManager.Instance.IsLockOnActive = isActive;
        }
    }

    #endregion


    #region [활성 Hero 관리]

    /// <summary>
    /// 교대 후 현재 조작 중인 Hero 갱신
    /// </summary>
    public void RegisterActiveHero(Hero newHero)
    {
        _activeHero = newHero;
    }

    #endregion


    #region [타겟 갱신]

    /// <summary>
    /// 현재 Hero 주변의 우선순위 타겟으로 소프트 록온 갱신
    /// 전투 시작이나 파티 초기화 시 사용
    /// </summary>
    public void RefreshSoftLockTarget()
    {
        if (_isHardLockActive || _activeHero == null) return;

        SetMagneticTarget(GetPriorityTarget());
    }

    /// <summary>
    /// 입력 방향을 기준으로 소프트 록온 타겟 갱신
    /// </summary>
    public void RefreshDirectionalSoftLockTarget(Vector3 inputDirection)
    {
        if (_isHardLockActive || _activeHero == null) return;

        Transform target = FindDirectionalTarget(inputDirection);
        if (target == null) return;

        SetMagneticTarget(target);
    }

    public bool TrySetCounterAttackTarget(Enemy target)
    {
        if (!IsValidTarget(target))
        {
            return false;
        }

        SetMagneticTarget(target.transform);

        // 하드 락온 상태라면 카메라 락온 대상까지 변경
        if (_isHardLockActive)
        {
            OnLockOnTargetChanged?.Invoke(MagneticTarget);
        }

        return true;
    }


    /// <summary>
    /// 현재 전투 타겟 변경
    /// </summary>
    private void SetMagneticTarget(Transform target)
    {
        if (MagneticTarget == target) return;

        UnsubscribeMagneticTargetDeathEvent();

        MagneticTarget = target;

        if (MagneticTarget == null)
        {
            return;
        }

        // 현재 MagneticTarget의 Enemy 컴포넌트를 찾아 사망 이벤트 구독
        _subscribedMagneticTargetEnemy =
        MagneticTarget.GetComponentInParent<Enemy>();
        
        if (_subscribedMagneticTargetEnemy != null)
        {
            _subscribedMagneticTargetEnemy.OnDied +=
                HandleMagneticTargetDied;
        }
    }

    private void UnsubscribeMagneticTargetDeathEvent()
    {
        if (_subscribedMagneticTargetEnemy == null)
        {
            return;
        }

        _subscribedMagneticTargetEnemy.OnDied -=
            HandleMagneticTargetDied;

        _subscribedMagneticTargetEnemy = null;
    }

    private void HandleMagneticTargetDied(Enemy deadEnemy)
    {
        if (deadEnemy == null)
        {
            return;
        }

        if (MagneticTarget != deadEnemy.transform)
        {
            return;
        }

        bool wasHardLockActive = _isHardLockActive;

        Transform nextTarget = GetPriorityTarget();

        SetMagneticTarget(nextTarget);

        if (nextTarget == null && wasHardLockActive)
        {
            SetHardLockState(false);
        }

        if (_activeHero != null &&
            _activeHero.Movement != null)
        {
            _activeHero.Movement.UpdateAnimMoveDirToTarget(
                _activeHero.HeroAxis
            );
        }

        if (wasHardLockActive)
        {
            OnLockOnTargetChanged?.Invoke(MagneticTarget);
        }
    }

    #endregion


    #region [타겟 탐색]

    /// <summary>
    /// 등급이 높고 가까운 적을 우선 타겟으로 반환
    /// </summary>
    public Transform GetPriorityTarget()
    {
        if (_activeHero == null) return null;

        Collider[] targets = FindTargetsInRange();
        if (targets.Length == 0) return null;

        Transform bestTarget = null;
        EnemyRank highestRank = default;
        float closestDistance = float.MaxValue;
        bool hasCandidate = false;

        foreach (Collider targetCollider in targets)
        {
            Enemy enemy = GetEnemy(targetCollider);
            if (!IsValidTarget(enemy)) continue;

            float distance = Vector3.Distance(_activeHero.transform.position, enemy.transform.position);
            bool hasHigherRank = !hasCandidate || enemy.Rank > highestRank;
            bool isCloserSameRank = enemy.Rank == highestRank && distance < closestDistance;

            if (!hasHigherRank && !isCloserSameRank) continue;

            hasCandidate = true;
            highestRank = enemy.Rank;
            closestDistance = distance;
            bestTarget = enemy.transform;
        }

        return bestTarget;
    }

    /// <summary>
    /// 현재 Hero 이동 방향의 우선 타겟 반환
    /// </summary>
    public Transform GetPriorityTargetDirectional()
    {
        if (_activeHero == null) return null;

        return FindDirectionalTarget(_activeHero.HeroAxis);
    }

    /// <summary>
    /// 입력 방향과 거리를 점수화해 소프트 록온 타겟 선정
    /// </summary>
    private Transform FindDirectionalTarget(Vector3 inputDirection)
    {
        if (_activeHero == null) return null;

        inputDirection.y = 0f;

        // 유효한 방향이 없다면 기존 타겟이나 기본 우선 타겟 사용
        if (inputDirection.sqrMagnitude < 0.01f)
        {
            return IsValidTarget(MagneticTarget) ? MagneticTarget : GetPriorityTarget();
        }

        Collider[] targets = FindTargetsInRange();
        Transform bestTarget = null;
        float highestScore = float.MinValue;

        foreach (Collider targetCollider in targets)
        {
            Enemy enemy = GetEnemy(targetCollider);
            if (!IsValidTarget(enemy)) continue;

            Vector3 directionToEnemy = enemy.transform.position - _activeHero.transform.position;
            directionToEnemy.y = 0f;

            if (directionToEnemy.sqrMagnitude < 0.01f) continue;

            float directionDot = Vector3.Dot(inputDirection.normalized, directionToEnemy.normalized);
            if (directionDot <= _softTargetDirectionThreshold) continue;

            float distance = directionToEnemy.magnitude;
            float score = directionDot * DirectionScoreWeight - distance * DistanceScoreWeight;

            if (enemy.Rank == EnemyRank.Boss) score += BossPriorityBonus;
            if (score <= highestScore) continue;

            highestScore = score;
            bestTarget = enemy.transform;
        }

        // 입력 방향에 적이 없다면 기존 타겟 유지
        if (bestTarget != null) return bestTarget;
        return IsValidTarget(MagneticTarget) ? MagneticTarget : GetPriorityTarget();
    }

    /// <summary>
    /// 기준 타겟의 화면상 좌우에서 가장 가까운 적 반환
    /// </summary>
    private Transform FindNextHardLockTarget(Transform referenceTarget, bool toRight)
    {
        if (_activeHero == null || !IsValidTarget(referenceTarget)) return null;

        Camera mainCamera = Camera.main;
        if (mainCamera == null) return null;

        Collider[] targets = FindTargetsInRange();
        if (targets.Length <= 1) return referenceTarget;

        Vector3 currentScreenPosition = mainCamera.WorldToScreenPoint(referenceTarget.position);
        Transform bestTarget = null;
        float shortestScreenDistance = float.MaxValue;

        foreach (Collider targetCollider in targets)
        {
            Enemy enemy = GetEnemy(targetCollider);
            if (!IsValidTarget(enemy)) continue;
            if (enemy.transform == referenceTarget) continue;

            Vector3 enemyScreenPosition = mainCamera.WorldToScreenPoint(enemy.transform.position);

            // 카메라 뒤에 있는 적은 변경 대상에서 제외
            if (enemyScreenPosition.z < 0f) continue;

            float horizontalDifference = enemyScreenPosition.x - currentScreenPosition.x;

            if (toRight && horizontalDifference <= 0f) continue;
            if (!toRight && horizontalDifference >= 0f) continue;

            float screenDistance = Vector2.Distance(currentScreenPosition, enemyScreenPosition);
            if (screenDistance >= shortestScreenDistance) continue;

            shortestScreenDistance = screenDistance;
            bestTarget = enemy.transform;
        }

        return bestTarget != null ? bestTarget : referenceTarget;
    }

    /// <summary>
    /// 활성 Hero 주변의 Enemy Layer 콜라이더 탐색
    /// </summary>
    private Collider[] FindTargetsInRange()
    {
        if (_activeHero == null) return Array.Empty<Collider>();

        return Physics.OverlapSphere(_activeHero.transform.position, _targetSearchRange, _enemyLayerMask);
    }

    /// <summary>
    /// 자식 콜라이더에서도 Enemy 본체 탐색
    /// </summary>
    private Enemy GetEnemy(Collider targetCollider)
    {
        if (targetCollider == null) return null;

        return targetCollider.GetComponentInParent<Enemy>();
    }

    /// <summary>
    /// 사망하지 않은 Enemy인지 검사
    /// </summary>
    public bool IsValidTarget(Enemy enemy)
    {
        return enemy != null && enemy.Status != null && !enemy.Status.IsDead;
    }

    /// <summary>
    /// Transform이 유효한 Enemy 타겟인지 검사
    /// </summary>
    private bool IsValidTarget(Transform target)
    {
        if (target == null) return false;

        Enemy enemy = target.GetComponentInParent<Enemy>();
        return IsValidTarget(enemy);
    }

    #endregion


    #region [소프트 록온 보조]

    /// <summary>
    /// 현재 상태에서 소프트 록온 타겟 변경이 가능한지 검사
    /// </summary>
    private bool CanSwitchSoftTarget()
    {
        if (_isHardLockActive || _activeHero == null) return false;
        if (InputManager.Instance == null) return false;

        // 방향키와 회피 입력이 겹칠 때 타겟 변경 차단
        bool isDodging = InputManager.Instance.IsDashPressed() ||
                         _activeHero.CurrentState == HeroState.Dodge;

        if (isDodging) return false;

        return IsCombatActionState();
    }

    /// <summary>
    /// 현재 Hero가 공격 모션을 실행 중인지 검사
    /// </summary>
    private bool IsCombatActionState()
    {
        if (_activeHero == null) return false;

        return _activeHero.CurrentState == HeroState.Attack ||
               _activeHero.CurrentState == HeroState.Skill ||
               _activeHero.CurrentState == HeroState.Ult;
    }

    /// <summary>
    /// 이동 입력을 카메라 기준 월드 방향으로 변환
    /// </summary>
    private Vector3 ConvertMoveInputToWorldDirection(Vector2 moveInput)
    {
        Camera mainCamera = Camera.main;

        if (mainCamera == null)
        {
            return _activeHero != null ? _activeHero.HeroAxis : Vector3.zero;
        }

        Vector3 forward = mainCamera.transform.forward;
        Vector3 right = mainCamera.transform.right;

        forward.y = 0f;
        right.y = 0f;

        return (forward.normalized * moveInput.y + right.normalized * moveInput.x).normalized;
    }

    #endregion


    #region [입력 이벤트 구독]

    private void SubscribeInputEvents()
    {
        if (_isInputEventSubscribed) return;

        InputManager input = InputManager.Instance;

        if (input == null)
        {
            Debug.LogError("LockOnManager: InputManager가 존재하지 않습니다.");
            return;
        }

        input.OnLockOnPerformed += HandleLockOnPerformedInput;
        input.OnHardLockOnSwitchPerformed += HandleHardLockOnSwitch;
        input.OnSoftLockOnSwitchPerformed += HandleSoftLockOnSwitch;

        _isInputEventSubscribed = true;
    }

    private void UnsubscribeInputEvents()
    {
        if (!_isInputEventSubscribed || InputManager.Instance == null) return;

        InputManager input = InputManager.Instance;

        input.OnLockOnPerformed -= HandleLockOnPerformedInput;
        input.OnHardLockOnSwitchPerformed -= HandleHardLockOnSwitch;
        input.OnSoftLockOnSwitchPerformed -= HandleSoftLockOnSwitch;

        _isInputEventSubscribed = false;
    }

    #endregion
}