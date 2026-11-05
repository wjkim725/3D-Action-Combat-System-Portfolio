using UnityEngine;

public class HeroMovement : MonoBehaviour
{
    private Hero _hero;
    private HeroAnimation _heroAnimation;

    [Header("Speed Settings")]
    [SerializeField] private float _walkSpeed = 2.5f;
    [SerializeField] private float _runSpeed = 6f;
    [SerializeField] private float _sprintSpeed = 12f;

    [Header("Acceleration Settings")]
    [SerializeField] private float _acceleration = 25f;
    [SerializeField] private float _deceleration = 50f;
    [SerializeField] private float _stopSpeedThreshold = 0.05f;

    [Header("Rotation Settings")]
    [SerializeField] private float _movementRotationSpeed = 360f;
    [Range(0f, 1f)]
    [SerializeField] private float _minimumTurnSpeedMultiplier = 0.6f;  // 급 회전 시 최소 속도 %
    [SerializeField] private float _combatRotationSpeed = 900f;

    private float _currentMoveSpeed;

    [Header("Gravity Settings")]
    [SerializeField] private float _gravity = 9.81f;
    private float _verticalVelocity = 0f;

    [Header("Movement Settings")]
    private bool _isWalkMode = false;
    private bool _isDashActive = false;

    [Header("Dodge Counter Rotation")]
    [Tooltip("회피반격 중 타깃을 향하는 최대 회전 속도(도/초). ")]
    [SerializeField, Min(0f)]
    private float _dodgeCounterRotationSpeed = 720f;

    [Header("Dodge State Settings")]
    private float _dodgeSpeed = 10f;
    private const float _dodgeDuration = 0.4f;
    private float _dodgeDurationTimer = 0f;

    [Header("Dodge CoolDown Settings")]
    private int _currentDodgeCount = 0;
    private const int _maxDodgeCount = 2;
    private float _lastDodgeTime = 0f;
    private const float _dodgeCooldown = 0.8f;
    private float _dodgeCooldownTimer = 0f;

    [Header("Hero Axis")]
    private Vector3 _dashDirection = Vector3.zero;
    private Vector3 _animMoveLocalDirection = Vector3.forward;

    [Header("Magnetic Speed Settings")]
    [SerializeField] private float _magneticSpeed = 5.5f;

    [Header("Directional Anim Movement Settings")]
    [SerializeField] private float _directionalAnimMoveSpeed = 4f;
    private Vector3? _fixedAnimMoveDirection;
    private float _fixedAnimMoveMultiplier = 1f;
    /// <summary>
    /// 타겟 방향에 좌/우 측면 방향을 얼마나 반영할지 결정하는 가중치
    /// 값이 클수록 타겟 정면에서 더 크게 비껴가는 진행 방향 생성
    /// </summary>
    [SerializeField] private float _targetSideDirectionWeight = 0.7f;

    [Header("Swap Movement Settings")]
    private float _swapInfriction = 3f;
    private float _currentSwapInSpeed = 0f;
    private const float _maxSwapInSpeed = 12f;

    private float _swapOutfriction = 3f;
    private float _currentSwapOutSpeed = 0f;
    private const float _maxSwapOutSpeed = 5f;

    private float _parryAssistFriction = 4f;
    private float _currentParryAssistSpeed = 0f;
    private float _maxParryAssistSpeed = 15f;

    #region [프로퍼티 영역]

    public CharacterController CharacterController { get; private set; }
    public float CurrentMoveSpeed => _currentMoveSpeed;
    public bool IsStandardMovementStopped =>
        _currentMoveSpeed <= _stopSpeedThreshold;
    public bool IsWalkMode => _isWalkMode;
    public bool IsDashActive => _isDashActive;
    public bool CanDodge => _dodgeCooldownTimer <= 0f;

    /// <summary>
    /// 회피 쿨타임의 남은 시간을 초 단위로 반환함.
    /// </summary>
    public float DodgeCooldownRemaining => Mathf.Max(0f, _dodgeCooldownTimer);

    /// <summary>
    /// 회피 쿨타임의 전체 시간을 초 단위로 반환함.
    /// </summary>
    public float DodgeCooldownDuration => _dodgeCooldown;

    public bool IsDodgeFinished => _dodgeDurationTimer <= 0f;

    // 애니메이션 이동 방향
    public Vector3 DashDirection { get => _dashDirection; set => _dashDirection = value; }

    public float MaxSwapInSpeed => _maxSwapInSpeed;
    public float MaxSwapOutSpeed => _maxSwapOutSpeed;
    public float MaxParryAssistSpeed => _maxParryAssistSpeed;

    #endregion


    private void Awake()
    {
        _hero = GetComponent<Hero>();
        CharacterController = GetComponent<CharacterController>();
        _heroAnimation = GetComponent<HeroAnimation>();
    }

    /// <summary>
    /// 특정 클립에서 발생하는 루트 모션 이동이
    /// 원치 않는 루트 모션 이동을 차단하도록 엔진 연산을 가로채서 증발시키는 콜백 함수
    /// </summary>
    private void OnAnimatorMove()
    {
        // Animation의 Root Transform 데이터를 받지 않고 코드로 제어하는 경우
        //if (_hero.ActionRegistry.IsAnimMoveActive)
        //{
        //    // 내부 애니메이션의 간섭을 차단
        //    return;
        //}

        // Root 모션을 사용해야 되는 경우
        //if (CharacterController != null && CharacterController.enabled)
        //{
        //    Vector3 rootMotionVelocity = _hero.Animation.Animator.deltaPosition;
        //    rootMotionVelocity.y = 0;
        //    CharacterController.Move(rootMotionVelocity);
        //}
    }

    private void OnDisable()
    {
        ResetGravity();
    }

    #region [중력 처리 영역]

    /// <summary>
    /// Hero 본체 Update에서 매 프레임 호출하여 접지와 낙하 이동을 처리함.
    /// </summary>
    public void ApplyGravity()
    {
        if (CharacterController == null || !CharacterController.enabled)
        {
            ResetGravity();
            return;
        }

        if (CharacterController.isGrounded)
        {
            _verticalVelocity = -0.5f; // 바닥에 안정적으로 붙어있도록 살짝 음수 유지
        }
        else
        {
            _verticalVelocity -= _gravity * Time.deltaTime;
        }

        CollisionFlags collisionFlags = CharacterController.Move(
            Vector3.up * _verticalVelocity * Time.deltaTime
        );

        // 같은 프레임의 수평 Move가 접지 정보를 바꾸더라도 착지한 낙하 속도는 남기지 않음.
        if ((collisionFlags & CollisionFlags.Below) != 0)
        {
            _verticalVelocity = -0.5f;
        }
    }

    /// <summary>
    /// 교대 및 순간이동 이전에 누적된 낙하 속도를 초기화함.
    /// </summary>
    public void ResetGravity()
    {
        _verticalVelocity = 0f;
    }

    #endregion


    #region [일반 이동 처리 영역]

    /// <summary>
    /// 일반 이동 상태(Idle, Walk, Run, Sprint)일 때 호출되어 물리 이동 및 회전을 처리하는 메서드
    /// </summary>
    public void ExecuteStandardMovement(Vector3 heroAxis, HeroState currentState)
    {
        if (CharacterController == null || !CharacterController.enabled) return;

        float targetMoveSpeed = GetTargetMoveSpeed(heroAxis, currentState);

        UpdateCurrentMoveSpeed(targetMoveSpeed);
        MoveStandard(heroAxis);
    }

    /// <summary>
    /// 현재 상태와 입력 여부에 따른 목표 이동 속도 반환
    /// </summary>
    private float GetTargetMoveSpeed(Vector3 heroAxis, HeroState currentState)
    {
        switch (currentState)
        {
            case HeroState.Idle:
                return 0f;

            case HeroState.Walk:
                return _walkSpeed;

            case HeroState.Run:
                return _runSpeed;

            case HeroState.Sprint:
                return _sprintSpeed;

            case HeroState.ConvertCombat:
                if (heroAxis.sqrMagnitude < 0.01f)
                {
                    return 0f;
                }

                if (_isWalkMode)
                {
                    return _walkSpeed;
                }

                if (_isDashActive)
                {
                    return _sprintSpeed;
                }

                return _runSpeed;

            default:
                //_currentMoveSpeed = 0f;
                return 0f;
        }
    }

    /// <summary>
    /// 목표 이동 속도를 향한 가속 및 감속 처리
    /// </summary>
    private void UpdateCurrentMoveSpeed(float targetMoveSpeed)
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

        if (_currentMoveSpeed <= _stopSpeedThreshold &&
            targetMoveSpeed <= 0f)
        {
            _currentMoveSpeed = 0f;
        }
    }

    private void MoveStandard(Vector3 heroAxis)
    {
        if (CharacterController == null || !CharacterController.enabled)
        {
            Debug.Log("CharactorController 비활성화");
            return;
        }

        bool hasMoveInput = heroAxis.sqrMagnitude >= 0.01f;

        Vector3 desiredDirection = hasMoveInput
            ? heroAxis.normalized
            : Vector3.zero;

        if (hasMoveInput)
        {
            RotateTowardsMoveDirection(desiredDirection);
        }

        float turnSpeedMultiplier =
            CalculateTurnSpeedMultiplier(
                desiredDirection,
                hasMoveInput
            );

        float actualMoveSpeed =
            _currentMoveSpeed * turnSpeedMultiplier;

        /*
         * 입력이 없더라도 현재 속도가 남아있다면
         * 마지막 몸 방향으로 감속 이동 처리
         */
        Vector3 moveDirection = transform.forward;

        CharacterController.Move(
            moveDirection *
            actualMoveSpeed *
            Time.deltaTime
        );

        /*
         * 회전 감속까지 반영된 실제 이동 속도를
         * Animator MoveSpeed에 전달
         */
        _heroAnimation.SetMoveSpeed(actualMoveSpeed);
    }

    /// <summary>
    /// 입력 방향을 향한 초당 각도 기반 회전 처리
    /// </summary>
    private void RotateTowardsMoveDirection(Vector3 desiredDirection)
    {
        if (desiredDirection.sqrMagnitude < 0.01f) return;

        desiredDirection.y = 0f;

        Quaternion targetRotation =
            Quaternion.LookRotation(desiredDirection);

        transform.rotation = Quaternion.RotateTowards(
            transform.rotation,
            targetRotation,
            _movementRotationSpeed * Time.deltaTime
        );
    }

    /// <summary>
    /// 입력 방향과 몸 방향의 각도 차이에 따른 이동 속도 배율 계산
    /// </summary>
    private float CalculateTurnSpeedMultiplier(
        Vector3 desiredDirection,
        bool hasMoveInput
    )
    {
        /*
         * 입력이 없는 감속 구간에서는
         * 기존 진행 속도를 그대로 사용
         */
        if (!hasMoveInput)
        {
            return 1f;
        }

        float directionAlignment = Vector3.Dot(
            transform.forward,
            desiredDirection
        );

        /*
         * 반대 방향 -1, 동일 방향 1을
         * 0~1 범위로 변환
         */
        float alignmentRatio = Mathf.InverseLerp(
            -1f,
            1f,
            directionAlignment
        );

        return Mathf.Lerp(
            _minimumTurnSpeedMultiplier,
            1f,
            alignmentRatio
        );
    }

    /// <summary>
    /// 기본 Locomotion 이동 속도 초기화
    /// </summary>
    public void ResetStandardMovement()
    {
        _currentMoveSpeed = 0f;
        _heroAnimation.SetMoveSpeed(0f);
    }

    #endregion


    #region [Dodge 처리 영역]

    /// <summary>
    /// Dodge 시전됐을 때 Dodge 지속시간 세팅
    /// </summary>
    public void InitializeDodgeDurationTimer()
    {
        _dodgeDurationTimer = _dodgeDuration;
    }

    /// <summary>
    /// Dodge가 시전됐을 때 DodgeCount와 DodgeCoolDown 관리
    /// </summary>
    public void InitializeDodgeCoolDownFields()
    {
        _currentDodgeCount++;
        _lastDodgeTime = Time.time;

        if (_currentDodgeCount >= _maxDodgeCount)
        {
            _dodgeCooldownTimer = _dodgeCooldown;
            _currentDodgeCount = 0;
        }
    }

    /// <summary>
    /// Dodge 쿨타임 관리 메서드
    /// Hero 본체 Update에서 돌아감.
    /// </summary>
    public void UpdateDodgeCoolDownTimer()
    {
        if (_dodgeCooldownTimer > 0f)
        {
            _dodgeCooldownTimer -= Time.deltaTime;
        }

        if (_currentDodgeCount > 0 && _dodgeCooldownTimer <= 0f)
        {
            if (Time.time - _lastDodgeTime > _dodgeCooldown)
            {
                _currentDodgeCount = 0;
            }
        }
    }

    /// <summary>
    /// UpdateState:Dodge에서 호출되어 DodgeDurationTimer 관리
    /// </summary>
    public void ExecuteDodgeDurationTimer()
    {
        _dodgeDurationTimer -= Time.deltaTime;
    }

    /// <summary>
    /// 회피(Dodge) 상태일 때 호출되어 입력 축에 영향받지 않는 고정 대시 물리 이동을 처리하는 메서드
    /// </summary>
    public void ExecuteDodgeMovement()
    {
        if (CharacterController == null || !CharacterController.enabled) return;

        dodge();
    }

    /// <summary>
    /// 회피반격 중 지정된 각속도로 반격 타깃을 향해 회전함.
    /// </summary>
    public void UpdateDodgeCounterRotation(Enemy target)
    {
        if (target == null) return;

        Vector3 lookDirection = target.transform.position - transform.position;
        lookDirection.y = 0f;

        if (lookDirection.sqrMagnitude < 0.0001f) return;

        transform.rotation = Quaternion.RotateTowards(
            transform.rotation,
            Quaternion.LookRotation(lookDirection.normalized),
            _dodgeCounterRotationSpeed * Time.deltaTime);
    }

    private void dodge()
    {
        if (CharacterController == null || !CharacterController.enabled) return;

        CharacterController.Move(_dashDirection * _dodgeSpeed * Time.deltaTime);
    }

    /// <summary>
    /// Dodge 진입 시 즉시 회전 처리를 돕는 메서드
    /// </summary>
    public void SetDodgeRotationDirect(Vector3 direction)
    {
        Quaternion targetRotation = Quaternion.LookRotation(direction);
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, 100);
    }

    #endregion


    #region [전투 애니메이션 이동 처리 영역]

    /// <summary>
    /// 애니메이션 이동이 허용된 구간에 현재 상태의 이동을 실행한다.
    /// 교대·패링은 전용 처리로 분기하고, 나머지는 추적 여부와 방향에 따라 이동한다.
    /// 회전이나 이동 허용 플래그의 설정은 이 메서드에서 수행하지 않는다.
    /// </summary>
    public void ExecuteCombatAnimMovement()
    {
        LockOnManager lockOnManager = LockOnManager.Instance;
        Transform target = lockOnManager != null ? lockOnManager.MagneticTarget : null;

        ExecuteCombatAnimMovement(target);
    }

    /// <summary>
    /// 전달받은 대상을 기준으로 전투 애니메이션 이동을 실행한다.
    /// 대상이 없으면 다른 대상을 탐색하지 않고 기존 대상 없는 이동 규칙을 적용한다.
    /// </summary>
    public void ExecuteCombatAnimMovement(Transform target)
    {
        if (CharacterController == null || !CharacterController.enabled) return;
        if (!_hero.ActionRegistry.IsAnimMoveActive) return;

        // 전용 상태는 아래의 공통 이동 계수·자력 접근 경로를 사용하지 않는다.
        switch (_hero.CurrentState)
        {
            case HeroState.SwapIn:
                ExecuteSwapInAnimMovement();
                return;

            case HeroState.SwapOut:
                ExecuteSwapOutAnimMovement();
                return;

            case HeroState.ParryIn:
                ExecuteParryAssistMovement();
                return;
        }

        // 고정 방향값 존재시 해당방향으로 고정 이동 시전.
        if (_fixedAnimMoveDirection.HasValue)
        {
            ExecuteDirectionalAnimMovement(
                _fixedAnimMoveDirection.Value, _fixedAnimMoveMultiplier);
            return;
        }

        // 추적 대상 기준 또는 캐릭터 로컬 기준의 방향을 월드 방향으로 변환한다.
        Vector3 moveDirection = ResolveAnimMoveWorldDirection(target);
        if (moveDirection.sqrMagnitude < 0.01f) return;

        // 현재 상태·콤보의 CombatData 이동 계수. 대응 데이터가 없으면 1을 사용한다.
        float moveMultiplier = GetCurrentCombatMoveMultiplier();

        if (_hero.ActionRegistry.IsTargetTrackingActive)
        {
            // true에는 실제 이동뿐 아니라 최소 거리 도달로 정지한 경우도 포함된다.
            if (TryExecuteMagneticApproach(target, moveDirection, moveMultiplier))
            {
                return;
            }

            if (target == null || LockOnManager.Instance == null)
            {
                ExecuteTrackingFallback(moveDirection, moveMultiplier);
                return;
            }
        }

        // 추적 비활성 또는 대상에 접근하지 않는 방향이면 일반 방향 이동을 사용한다.
        ExecuteDirectionalAnimMovement(
            moveDirection,
            moveMultiplier
        );
    }

    /// <summary>
    /// 전달받은 대상 쪽으로 향하는 이동에 기존 록온 설정의 거리별 자력 접근 속도를 적용한다.
    /// 대상이 없거나 접근 방향이 아니면 false, 이동 또는 근접 정지를 처리하면 true를 반환한다.
    /// </summary>
    private bool TryExecuteMagneticApproach(
        Transform target,
        Vector3 moveDirection,
        float moveMultiplier)
    {
        LockOnManager lockOnManager = LockOnManager.Instance;

        if (lockOnManager == null || target == null)
        {
            return false;
        }

        Vector3 targetDirection = GetTargetDirection(target);
        bool isApproachingTarget =
            Vector3.Dot(moveDirection, targetDirection) > 0.5f;

        // Tracking 중이어도 후방 및 측면 이동에는 접근용 정지 거리 적용 X
        if (!isApproachingTarget)
        {
            return false;
        }

        // 방향 판정은 수평 기준이지만, 여기의 거리는 높이 차이를 포함한 3차원 거리다.
        float distance = Vector3.Distance(
            transform.position,
            target.position
        );

        // 이동 전 거리 기준으로 정지한다. 이번 프레임의 이동량을 남은 거리로 제한하지는 않는다.
        if (distance <= lockOnManager.MinStopDistance)
        {
            return true;
        }

        float finalSpeed = _magneticSpeed;

        // 범위 밖 가속을 우선 적용하고, 범위 안에서만 하드/소프트 록온 속도를 구분한다.
        if (distance > lockOnManager.MaxMagneticRange)
        {
            finalSpeed = _magneticSpeed * 1.4f;
        }
        else if (lockOnManager.IsHardLockActive)
        {
            float remainingDistance =
                distance - lockOnManager.MinStopDistance;

            finalSpeed = _magneticSpeed + (remainingDistance * 1.5f);
            finalSpeed = Mathf.Min(finalSpeed, _sprintSpeed * 2.0f);
        }
        else
        {
            float t = Mathf.Clamp01(
                (distance - lockOnManager.MinStopDistance) /
                (lockOnManager.MaxMagneticRange - lockOnManager.MinStopDistance)
            );

            float curveSpeed = Mathf.Lerp(1.4f, 2.0f, t);
            finalSpeed = _magneticSpeed * curveSpeed;
        }

        finalSpeed *= moveMultiplier;

        // 시간 배율에 따라 이동하며 일시정지 시에는 이동량이 0이 된다.
        CharacterController.Move(
            moveDirection * finalSpeed * Time.deltaTime
        );

        return true;
    }

    /// <summary>
    /// 지정된 월드 방향으로 기본 애니메이션 이동 속도와 모션 계수를 적용한다.
    /// deltaTime을 사용하므로 시간 배율의 영향을 받으며 대상 정지 거리는 검사하지 않는다.
    /// </summary>
    private void ExecuteDirectionalAnimMovement(
        Vector3 moveDirection,
        float moveMultiplier)
    {
        float finalSpeed =
            _directionalAnimMoveSpeed * moveMultiplier;

        CharacterController.Move(
            moveDirection * finalSpeed * Time.deltaTime
        );
    }

    /// <summary>
    /// 추적 대상이나 록온 설정을 사용할 수 없을 때 대체 이동을 수행한다.
    /// </summary>
    private void ExecuteTrackingFallback(
        Vector3 moveDirection,
        float moveMultiplier)
    {
        float finalSpeed =
            _magneticSpeed * moveMultiplier;

        CharacterController.Move(
            moveDirection * finalSpeed * Time.deltaTime
        );
    }

    /// <summary>
    /// 교대 진입 속도를 0으로 감쇠시키며 캐릭터 로컬 기준 방향으로 이동한다.
    /// 초기 속도는 BeginAnimMovement에서 설정하며 공통 CombatData 계수는 사용하지 않는다.
    /// </summary>
    private void ExecuteSwapInAnimMovement()
    {
        Vector3 moveDirection = ResolveHeroLocalDirection();
        if (moveDirection.sqrMagnitude < 0.01f) return;

        _currentSwapInSpeed = Mathf.Lerp(
            _currentSwapInSpeed,
            0f,
            Time.deltaTime * _swapInfriction
        );

        CharacterController.Move(
            moveDirection * _currentSwapInSpeed * Time.deltaTime
        );
    }

    /// <summary>
    /// 교대 퇴장 시 전방 이동은 감속하고, 그 외 방향은 최대 퇴장 속도로 보간한다.
    /// 캐릭터 로컬 기준 방향을 사용하며 공통 CombatData 계수는 사용하지 않는다.
    /// </summary>
    private void ExecuteSwapOutAnimMovement()
    {
        Vector3 moveDirection = ResolveHeroLocalDirection();
        if (moveDirection.sqrMagnitude < 0.01f) return;

        float dot = Vector3.Dot(
            moveDirection,
            transform.forward
        );

        if (dot > 0.5f)
        {
            // 전방으로 튀어나오는 퇴장 모션
            _currentSwapOutSpeed = Mathf.Lerp(
                _currentSwapOutSpeed,
                0f,
                Time.deltaTime * _swapOutfriction
            );
        }
        else
        {
            // 전방 판정을 통과하지 않은 방향(후방·측면 등)의 퇴장 모션
            _currentSwapOutSpeed = Mathf.Lerp(
                _currentSwapOutSpeed,
                _maxSwapOutSpeed,
                Time.deltaTime * _swapOutfriction
            );
        }

        CharacterController.Move(
            moveDirection * _currentSwapOutSpeed * Time.deltaTime
        );
    }

    #endregion


    #region [패링 이동 처리 영역]

    private void ExecuteParryAssistMovement()
    {
        if (CharacterController == null || !CharacterController.enabled) return;
        if (!_hero.ActionRegistry.IsAnimMoveActive) return;

        _currentParryAssistSpeed = Mathf.Lerp(
            _currentParryAssistSpeed,
            0f,
            Time.deltaTime * _parryAssistFriction
        );

        CharacterController.Move(
            -transform.forward *
            _currentParryAssistSpeed *
            Time.deltaTime
        );
    }

    /// <summary>
    /// ParryIn / ParryCounter 상태에서 매 프레임 호출되는 타겟 회전 메서드
    /// </summary>
    public void UpdateParryRotation(Enemy target)
    {
        if (target == null) return;

        Vector3 lookDirection =
            (target.transform.position - transform.position).normalized;

        lookDirection.y = 0f;

        if (lookDirection != Vector3.zero)
        {
            transform.rotation = Quaternion.Slerp(
                transform.rotation,
                Quaternion.LookRotation(lookDirection),
                _movementRotationSpeed * Time.deltaTime
            );
        }
    }

    #endregion


    #region [전투 회전 및 방향 갱신 영역]

    /// <summary>
    /// 타겟이 있으면 타겟 방향으로, 없으면 입력 방향으로 즉시 회전
    /// 당장은 사용하지 않지만 추후 체인 공격 등에 사용 가능
    /// </summary>
    public void RotateToTargetImmediately(Transform target, Vector3 heroAxis)
    {
        Vector3 targetDirection;

        if (target != null)
        {
            targetDirection = target.position - transform.position;
        }
        else
        {
            targetDirection = heroAxis;
        }

        targetDirection.y = 0f;

        if (targetDirection.sqrMagnitude < 0.01f) return;

        transform.rotation = Quaternion.LookRotation(targetDirection.normalized);
    }

    /// <summary>
    /// 전투 도중 변경된 록온 타겟 방향으로 부드럽게 회전
    /// 타겟이 없다면 방향키 입력 방향으로 회전
    /// </summary>
    public void UpdateCombatRotation(Vector3 heroAxis)
    {
        LockOnManager lockOnManager = LockOnManager.Instance;

        Transform target = lockOnManager != null
            ? lockOnManager.MagneticTarget : null;

        Vector3 targetDirection = target != null
            ? target.position - transform.position : heroAxis;

        targetDirection.y = 0f;

        if (targetDirection.sqrMagnitude < 0.01f) return;

        Quaternion targetRotation = Quaternion.LookRotation(targetDirection.normalized);

        transform.rotation = Quaternion.RotateTowards(
            transform.rotation,
            targetRotation,
            _combatRotationSpeed * Time.deltaTime
        );
    }

    /// <summary>
    /// 공격 시작 단계에서 록온 모드에 따라 전투 타겟을 갱신
    /// 실제 애니메이션 이동 방향은 이동 중 매 프레임 계산
    /// </summary>
    public void RefreshCombatDirection(Vector3 heroAxis)
    {
        LockOnManager lockOnManager = LockOnManager.Instance;

        if (lockOnManager == null) return;

        if (!lockOnManager.IsHardLockActive)
        {
            lockOnManager.RefreshDirectionalSoftLockTarget(heroAxis);
        }

    }

    #endregion


    #region [궁극기 이동 처리 영역]

    /// <summary>
    /// Ult 컷신 중 타겟 앞 전용 위치로 순간이동시키는 메서드
    /// </summary>
    public void ExecuteUltMovement(Transform target)
    {
        if (target == null || !_hero.CombatData.ultTeleportAvailable) return;

        float attackDistance = _hero.CombatData.ultTeleportPosFromTarget;

        Vector3 directionToHero = transform.position - target.position;

        directionToHero.y = 0f;

        if (directionToHero.magnitude < 0.01f)
        {
            directionToHero = target.forward;
        }

        Vector3 spawnDirection = directionToHero.normalized;

        Vector3 targetPosition = target.position + (spawnDirection * attackDistance);

        targetPosition.y = 0f;
        ResetGravity();

        if (TryGetComponent<CharacterController>(out var controller))
        {
            controller.enabled = false;
            transform.position = targetPosition;
            controller.enabled = true;

            // TODO: targetPosition이 기존 거리보다 멀어지면 기존 Position 사용
        }
        else
        {
            transform.position = targetPosition;
        }

        Vector3 lookDirection = (target.position - transform.position).normalized;

        lookDirection.y = 0f;

        if (lookDirection != Vector3.zero)
        {
            transform.rotation = Quaternion.LookRotation(lookDirection);
        }
    }

    /// <summary>
    /// CharacterController의 활성 상태를 보존하면서 월드 위치와 회전을 적용하고 낙하 속도를 초기화함.
    /// 배치 가능 여부는 호출자가 미리 검사함.
    /// </summary>
    public void SetWorldPose(Vector3 position, Quaternion rotation)
    {
        ResetGravity();
        bool wasEnabled = CharacterController != null && CharacterController.enabled;
        if (wasEnabled) CharacterController.enabled = false;
        transform.SetPositionAndRotation(position, rotation);
        if (wasEnabled) CharacterController.enabled = true;
    }

    #endregion


    #region [CombatDataSO 이동속도 계수 접근]

    /// <summary>
    /// 현재 캐릭터의 전투 상태 및 평타 콤보 수에 맞는
    /// 자력 유도 속도 배율을 CombatDataSO에서 가져와 반환
    /// </summary>
    private float GetCurrentCombatMoveMultiplier()
    {
        if (_hero == null || _hero.CombatData == null) return 1.0f;

        var data = _hero.CombatData;

        System.Collections.Generic.List<HeroCombatDataSO.MotionSequenceData>
            targetSequenceList = null;

        switch (_hero.CurrentState)
        {
            case HeroState.Attack:
                if (_heroAnimation.GetIsDashAttack())
                {
                    targetSequenceList = data.dashAttackSequences;
                }
                else
                {
                    targetSequenceList = data.normalAttackSequences;
                }
                break;

            case HeroState.ChargeAttack:
                // 막타는 반복 공격과 별도 이동 계수를 사용함.
                if (_hero.Mechanic?.IsChargeAttackFinisherActive == true)
                    return data.chargeAttackFinisherSequence.magneticSpeedMultiplier;

                targetSequenceList = data.chargeAttackSequences;
                break;

            case HeroState.Skill:
                if (_hero.SkillSystem.IsExSkillMode)
                {
                    targetSequenceList = data.exSkillAttackSequences;
                }
                else
                {
                    targetSequenceList = data.skillAttackSequences;
                }

                break;

            case HeroState.Ult:
                targetSequenceList = data.ultAttackSequences;
                break;
            case HeroState.AfterShock:
                targetSequenceList = data.AfterShockSequences;
                break;
            case HeroState.ChainAttack:
                targetSequenceList = data.chainAttackSequences;
                break;
            case HeroState.QuickAssist:
                targetSequenceList = data.quickAssistSequences;
                break;
            case HeroState.DodgeCounter:
                targetSequenceList = data.dodgeCounterSequences;
                break;
            case HeroState.ParryCounter:
                targetSequenceList = data.parryCounterSequences;
                break;
            default:
                return 1.0f;
        }

        if (targetSequenceList == null || targetSequenceList.Count == 0)
        {
            return 1.0f;
        }

        int comboIndex = _hero.ActionRegistry.CurrentComboIndex;

        if (comboIndex >= targetSequenceList.Count)
        {
            comboIndex = targetSequenceList.Count - 1;
        }

        return targetSequenceList[comboIndex].magneticSpeedMultiplier;
    }

    #endregion



    #region [AnimMoveDirection 관련 영역]

    /// <summary>
    /// 애니메이션 이동에 사용할 수평 월드 방향과 이동 배율을 고정함.
    /// </summary>
    public void ConfigureFixedAnimMovement(Vector3 worldDirection, float moveMultiplier)
    {
        worldDirection.y = 0f;
        if (worldDirection.sqrMagnitude < 0.0001f)
        {
            ClearFixedAnimMovement();
            return;
        }

        _fixedAnimMoveDirection = worldDirection.normalized;
        _fixedAnimMoveMultiplier = Mathf.Max(0f, moveMultiplier);
    }

    /// <summary>
    /// 고정 이동 설정을 해제하여 기존 상태별 이동 규칙으로 복원함.
    /// </summary>
    public void ClearFixedAnimMovement()
    {
        _fixedAnimMoveDirection = null;
        _fixedAnimMoveMultiplier = 1f;
    }

    public void BeginAnimMovement(AnimMoveDirectionType directionType)
    {
        SetAnimMoveLocalDirection(directionType);

        switch (_hero.CurrentState)
        {
            case HeroState.SwapIn:
                _currentSwapInSpeed = _maxSwapInSpeed;
                break;
            case HeroState.SwapOut:
                _currentSwapOutSpeed = _maxSwapOutSpeed;
                break;
        }
    }

    private void SetAnimMoveLocalDirection(AnimMoveDirectionType directionType)
    {
        if (directionType == AnimMoveDirectionType.Keep) return;

        _animMoveLocalDirection = directionType switch
        {
            AnimMoveDirectionType.Forward => Vector3.forward,
            AnimMoveDirectionType.Backward => Vector3.back,
            AnimMoveDirectionType.Left => Vector3.left,
            AnimMoveDirectionType.Right => Vector3.right,
            AnimMoveDirectionType.Target => Vector3.forward,
            AnimMoveDirectionType.TargetLeft =>
                new Vector3(-_targetSideDirectionWeight, 0f, 1f).normalized,
            AnimMoveDirectionType.TargetRight =>
                new Vector3(_targetSideDirectionWeight, 0f, 1f).normalized,
            _ => Vector3.forward
        };
    }

    /// <summary>
    /// 추적 중에는 전달받은 대상의 방향축을, 그 외에는 캐릭터의 로컬 방향축을 사용한다.
    /// </summary>
    private Vector3 ResolveAnimMoveWorldDirection(Transform target)
    {
        if (_hero.ActionRegistry.IsTargetTrackingActive &&
            TryGetTargetBasis(target, out Vector3 targetForward, out Vector3 targetRight))
        {
            return ResolveDirectionAgainstBasis(targetForward, targetRight);
        }

        return ResolveHeroLocalDirection();
    }

    private Vector3 ResolveHeroLocalDirection()
    {
        Vector3 direction =
            transform.forward * _animMoveLocalDirection.z +
            transform.right * _animMoveLocalDirection.x;

        direction.y = 0f;
        return direction.sqrMagnitude > 0.01f
            ? direction.normalized
            : Vector3.zero;
    }

    /// <summary>
    /// 전달받은 대상을 기준으로 수평 전방·우측 방향축을 구한다.
    /// </summary>
    private bool TryGetTargetBasis(
        Transform target,
        out Vector3 targetForward,
        out Vector3 targetRight)
    {
        targetForward = Vector3.zero;
        targetRight = Vector3.zero;

        if (target == null)
        {
            return false;
        }

        targetForward =
            target.position - transform.position;
        targetForward.y = 0f;

        if (targetForward.sqrMagnitude < 0.01f)
        {
            return false;
        }

        targetForward.Normalize();
        targetRight = Vector3.Cross(Vector3.up, targetForward).normalized;
        return true;
    }

    private Vector3 ResolveDirectionAgainstBasis(
        Vector3 forward,
        Vector3 right)
    {
        Vector3 direction =
            forward * _animMoveLocalDirection.z +
            right * _animMoveLocalDirection.x;

        direction.y = 0f;
        return direction.sqrMagnitude > 0.01f
            ? direction.normalized
            : Vector3.zero;
    }

    /// <summary>
    /// 전달받은 대상의 수평 방향을 구하며 대상이 없으면 캐릭터 전방을 반환한다.
    /// </summary>
    private Vector3 GetTargetDirection(Transform target)
    {
        if (target == null)
        {
            return transform.forward;
        }

        Vector3 direction = target.position - transform.position;

        direction.y = 0f;

        return direction.normalized;
    }

    #endregion


    #region [ Setter 영역]

    public void SetIsDashActive(bool isDashActive) => _isDashActive = isDashActive;

    public void SetIsWalkMode(bool isWalkMode) => _isWalkMode = isWalkMode;

    public void SetCurrentParryAssistSpeed(float speed) => _currentParryAssistSpeed = speed;

    #endregion
}
