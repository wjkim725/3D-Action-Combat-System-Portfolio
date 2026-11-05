using UnityEngine;

public class EnemyMovement : MonoBehaviour
{
    private Enemy _enemy;
    private Animator _animator;
    public CharacterController CharacterController {  get; private set; }

    [Header("Acceleration Settings")]
    [SerializeField] private float _acceleration = 10f;
    [SerializeField] private float _deceleration = 16f;
    [SerializeField] private float _stopSpeedThreshold = 0.05f;

    [Header("Speed Settings")]
    [SerializeField] private float _walkSpeed = 2f;      // Orbit 상태일때 걷는 속도
    [SerializeField] private float _runSpeed = 5f;       // Chasing 상태일때 뛰는 속도

    [Header("Rotation Settings")]
    [SerializeField] private float _movementRotationSpeed = 5f;
    private bool _isAttackRotationTracking;     // 공격 상태에서의 회전 허용
    
    [Header("Orbit Settigns")]
    [SerializeField] private float _repositionRadius = 3.6f;
    [SerializeField] private float _minRepositionAngle = 45f;
    [SerializeField] private float _maxRepositionAngle = 70f;
    [SerializeField] private float _repositionArrivalDistance = 0.2f;


    private int _repositionDirection = 1;

    private Vector3 _repositionPivot;
    private Vector3 _repositionDestination;
    public Vector3 RepositionDestination =>
        _repositionDestination;

    private bool _hasRepositionDestination;
    private bool _hasReachedRepositionDestination;
    public bool HasReachedRepositionDestination =>
        _hasReachedRepositionDestination;

    private Transform _targetTransform;

    public bool IsMovementStopped =>
        _currentMoveSpeed <= _stopSpeedThreshold;
    private float _currentMoveSpeed;
    public float CurrentMoveSpeed => _currentMoveSpeed;
    private Vector3 _lastMoveDirection;

    private float _gravity = 9.81f;
    private float _verticalVelocity = 0f;



    private void Awake()
    {
        _enemy = GetComponent<Enemy>();
        _animator = GetComponent<Animator>();
        CharacterController = GetComponent<CharacterController>();
    }

    private void Update()
    {
        // 중력은 바닥에 닿아있지 않다면 매 프레임 기본적으로 적용
        ApplyGravity();

        if (_targetTransform == null) return;

        switch (_enemy.CurrentState)
        {
            case EnemyState.Chase:
                ChaseAndRun();
                break;
            case EnemyState.Orbit:
                OrbitAndWalk();
                break;
        }
    }

    private void OnAnimatorMove()
    {
        if (_enemy.CurrentState != EnemyState.Attack &&
            _enemy.CurrentState != EnemyState.Dead) 
            return;

        transform.position += _animator.deltaPosition;
    }

    #region [공용 메서드 영역]

    /// <summary>
    /// 실제 이동을 즉시 멈추고 요청에 따라 이동 애니메이션 값을 초기화함.
    /// </summary>
    public void StopMovingImmediate(bool resetMovementAnimation = true)
    {
        _targetTransform = null;
        _currentMoveSpeed = 0f;

        CharacterController.Move(
            Vector3.zero
        );

        if (resetMovementAnimation)
        {
            _enemy.Animation.SetMovement(Vector3.zero);
        }
    }

    /// <summary>
    /// 목표 이동 속도를 향해 현재 속도를 가속하거나 감속한다.
    /// </summary>
    private void UpdateCurrentMoveSpeed(
        float targetMoveSpeed)
    {
        float speedChangeRate =
            targetMoveSpeed > _currentMoveSpeed
                ? _acceleration
                : _deceleration;

        _currentMoveSpeed = Mathf.MoveTowards(
            _currentMoveSpeed,
            targetMoveSpeed,
            speedChangeRate * Time.deltaTime
        );

        if (targetMoveSpeed <= 0f &&
            _currentMoveSpeed <= _stopSpeedThreshold)
        {
            _currentMoveSpeed = 0f;
        }
    }

    /// <summary>
    /// 지정한 방향으로 이동하고 타깃을 바라보며 상태에 맞는 이동 애니메이션 정보를 전달함.
    /// </summary>
    private void MoveForward(
    Vector3 direction)
    {
        direction.y = 0f;

        if (direction.sqrMagnitude < 0.0001f)
        {
            _enemy.Animation.SetMovement(Vector3.zero);
            return;
        }

        direction.Normalize();

        _lastMoveDirection = direction;

        CharacterController.Move(
            direction *
            _currentMoveSpeed *
            Time.deltaTime
        );

        if (_targetTransform != null)
        {
            RotateToTarget(
                _targetTransform.position
            );
        }

        // 추격은 기존 전진 모션을 유지하고 Orbit만 좌우 방향을 반영함.
        Vector3 localMoveVelocity = Vector3.forward * _currentMoveSpeed;

        if (_enemy.CurrentState == EnemyState.Orbit)
        {
            Vector3 localMoveDirection =
                transform.InverseTransformDirection(direction);
            localMoveVelocity = localMoveDirection * _currentMoveSpeed;
        }

        _enemy.Animation.SetMovement(localMoveVelocity);
    }

    /// <summary>
    /// 공격 외 상태에서 중력을 실제 이동에 적용하여 공중에 남지 않도록 함.
    /// </summary>
    private void ApplyGravity()
    {
        if (!CharacterController.enabled) return;

        // 공격 중에는 루트 모션이 높이를 제어하며 중단 전 낙하 속도 누적을 방지함.
        if (_enemy.CurrentState == EnemyState.Attack)
        {
            _verticalVelocity = 0f;
            return;
        }

        if (CharacterController.isGrounded)
        {
            _verticalVelocity = -0.5f; // 바닥에 안정적으로 붙어있도록 살짝 음수 유지
        }
        else
        {
            _verticalVelocity -= _gravity * Time.deltaTime; // 공중이라면 중력 가속도 적용
        }

        CharacterController.Move(
            Vector3.up * _verticalVelocity * Time.deltaTime
        );
    }

    #endregion


    #region [Orbit 영역]

    public void StartOrbitingProcess(Transform target)
    {
        if (target == null)
        {
            return;
        }

        if (_hasRepositionDestination)
        {
            ResumeOrbiting(target);
            return;
        }

        BeginOrbiting(target);
    }

    /// <summary>
    /// 새로운 Reposition 행동을 시작.
    /// 진입 시점의 Hero 위치를 기준으로 방향과 목적지를 생성.
    /// </summary>
    public void BeginOrbiting(
        Transform target)
    {
        if (target == null)
        {
            return;
        }

        _targetTransform = target;

        _repositionDirection =
            Random.Range(0, 2) == 0
                ? 1 : -1;

        CalculateRepositionDestination(target);

        _hasRepositionDestination = true;
        _hasReachedRepositionDestination = false;
    }

    /// <summary>
    /// 중단된 Reposition 행동을 기존 목적지 기준으로 재개.
    /// 방향과 목적지는 다시 계산하지 않음.
    /// </summary>
    public void ResumeOrbiting(
        Transform target)
    {
        if (target == null)
        {
            return;
        }

        _targetTransform = target;

        if (!_hasRepositionDestination)
        {
            BeginOrbiting(target);
            return;
        }

        _hasReachedRepositionDestination = false;
    }

    /// <summary>
    /// Reposition 시작 시점의 Hero 위치를 중심으로 저장하고,
    /// Enemy의 현재 방사 방향을 좌우로 회전시켜 목적지를 계산
    /// </summary>
    private void CalculateRepositionDestination(
        Transform target)
    {
        _repositionPivot =
            target.position;

        Vector3 radialDirection =
            transform.position -
            _repositionPivot;

        radialDirection.y = 0f;

        if (radialDirection.sqrMagnitude <
            0.0001f)
        {
            radialDirection =
                -target.forward;

            radialDirection.y = 0f;
        }

        radialDirection.Normalize();

        float repositionAngle =
            Random.Range(
                _minRepositionAngle,
                _maxRepositionAngle
            );

        Vector3 destinationDirection =
            Quaternion.AngleAxis(
                repositionAngle *
                _repositionDirection,
                Vector3.up
            ) * radialDirection;

        _repositionDestination =
            _repositionPivot +
            destinationDirection *
            _repositionRadius;

        _repositionDestination.y =
            transform.position.y;
    }

    private void OrbitAndWalk()
    {
        if (_targetTransform == null ||
        !_hasRepositionDestination)
        {
            return;
        }

        Vector3 directionToDestination =
            _repositionDestination -
            transform.position;

        directionToDestination.y = 0f;

        float remainingDistance =
            directionToDestination.magnitude;

        if (remainingDistance <=
            _repositionArrivalDistance)
        {
            _currentMoveSpeed = 0f;
            _hasReachedRepositionDestination = true;

            _enemy.Animation.SetMovement(Vector3.zero);
            return;
        }

        /*
         * 목적지까지 남은 거리로 정지 가능한 최대 속도를 계산한다.
         * 목적지에서 멀면 WalkSpeed를 유지하고,
         * 가까워지면 목표 속도를 낮춰 감속한다.
         */
        float stoppingDistance =
            Mathf.Max(
                0f,
                remainingDistance -
                _repositionArrivalDistance
            );

        float stoppingSpeed =
            Mathf.Sqrt(
                2f *
                _deceleration *
                stoppingDistance
            );

        float targetMoveSpeed =
            Mathf.Min(
                _walkSpeed,
                stoppingSpeed
            );

        UpdateCurrentMoveSpeed(
            targetMoveSpeed
        );

        MoveForward(
            directionToDestination
        );
    }

    /// <summary>
    /// 현재 Reposition 세션을 종료하고
    /// 저장된 방향과 목적지를 초기화
    /// BTAction_Orbit 종료단계에서 호출
    /// </summary>
    public void CompleteOrbiting()
    {
        _hasRepositionDestination = false;
        _hasReachedRepositionDestination = false;

        _repositionPivot = Vector3.zero;
        _repositionDestination = Vector3.zero;

        _targetTransform = null;
    }


    #endregion


    #region [Chase 영역]

    public void StartMoving(Transform target)
    {
        _targetTransform = target;
    }

    private void ChaseAndRun()
    {
        if (_targetTransform == null)
        {
            return;
        }

        UpdateCurrentMoveSpeed(
            _runSpeed
        );

        Vector3 directionToTarget =
            _targetTransform.position -
            transform.position;

        MoveForward(
            directionToTarget
        );
    }

    #endregion


    #region [Rotation 영역]

    public void RotateToTarget(Vector3 targetPosition)
    {
        Vector3 direction = (targetPosition - transform.position);
        direction.y = 0f;

        if (direction.sqrMagnitude > 0.001f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(direction);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, _movementRotationSpeed * Time.deltaTime);
        }
    }

    public void BeginAttackRotationTracking()
    {
        _isAttackRotationTracking = true;
    }

    public void StopAttackRotationTracking()
    {
        _isAttackRotationTracking = false;
    }

    public void RotateToTargetDuringAttack(
        Vector3 targetPosition)
    {
        if (!_isAttackRotationTracking) return;

        RotateToTarget(targetPosition);
    }

    #endregion



}
