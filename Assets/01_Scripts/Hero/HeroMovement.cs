using UnityEngine;

public class HeroMovement : MonoBehaviour
{
    private Hero _hero;
    private HeroAnimation _heroAnimation;

    [Header("Speed Settings")]
    [SerializeField] private float _walkSpeed = 1.5f;
    [SerializeField] private float _runSpeed = 3.5f;
    [SerializeField] private float _sprintSpeed = 7.0f;
    [SerializeField] private float _movementRotationSpeed = 15f;
    [SerializeField] private float _combatRotationSpeed = 900f;
    private float _currentMoveSpeed;

    [Header("Movement Settings")]
    private bool _isWalkMode = false;
    private bool _isDashActive = false;

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
    private Vector3 _animMoveDirection = Vector3.zero;

    [Header("Magnetic Speed Settings")]
    [SerializeField] private float _magneticSpeed = 5.5f;

    [Header("Directional Anim Movement Settings")]
    [SerializeField] private float _directionalAnimMoveSpeed = 4f;
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
    private const float _maxSwapOutSpeed = 10f;

    private float _parryAssistFriction = 4f;
    private float _currentParryAssistSpeed = 0f;
    private float _maxParryAssistSpeed = 15f;

    [Header("Parry Counter Settings")]
    [SerializeField] private float _parryDashSpeed = 15f;
    [SerializeField] private float _parryStoppingDistance = 1.5f;
    [SerializeField] private float _parryApproachEase = 8f;


    #region [프로퍼티 영역]

    public CharacterController CharacterController { get; private set; }
    public float CurrentMoveSpeed => _currentMoveSpeed;
    public bool IsWalkMode => _isWalkMode;
    public bool IsDashActive => _isDashActive;
    public bool CanDodge => _dodgeCooldownTimer <= 0f;
    public bool IsDodgeFinished => _dodgeDurationTimer <= 0f;

    // 애니메이션 이동 방향
    public Vector3 DashDirection { get => _dashDirection; set => _dashDirection = value; }
    public Vector3 AnimMoveDirection => _animMoveDirection;

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



    #region [일반 이동 처리 영역]

    /// <summary>
    /// 일반 이동 상태(Idle, Walk, Run, Sprint)일 때 호출되어 물리 이동 및 회전을 처리하는 메서드
    /// </summary>
    public void ExecuteStandardMovement(Vector3 heroAxis, HeroState currentState)
    {
        if (CharacterController == null || !CharacterController.enabled) return;

        switch (currentState)
        {
            case HeroState.Idle:
                _currentMoveSpeed = 0f;
                _heroAnimation.SetMoveSpeed(_currentMoveSpeed);
                break;

            case HeroState.Walk:
                _currentMoveSpeed = _walkSpeed;
                move(heroAxis);
                break;

            case HeroState.Run:
                _currentMoveSpeed = _runSpeed;
                move(heroAxis);
                break;

            case HeroState.Sprint:
                _currentMoveSpeed = _sprintSpeed;
                move(heroAxis);
                break;

            case HeroState.ConvertCombat:
                move(heroAxis);
                break;

            default:
                //_currentMoveSpeed = 0f;
                break;
        }
    }

    private void move(Vector3 heroAxis)
    {
        if (CharacterController == null || !CharacterController.enabled)
        {
            Debug.Log("CharactorController 비활성화");
            return;
        }

        _heroAnimation.SetMoveSpeed(_currentMoveSpeed);
        CharacterController.Move(heroAxis * _currentMoveSpeed * Time.deltaTime);

        if (heroAxis.magnitude > 0.1f)
        {
            rotate(heroAxis);
        }
    }

    private void rotate(Vector3 heroAxis)
    {
        if (heroAxis.sqrMagnitude < 0.01f) return;

        Quaternion targetRotation = Quaternion.LookRotation(heroAxis);
        transform.rotation = Quaternion.Slerp(
            transform.rotation,
            targetRotation,
            _movementRotationSpeed * Time.deltaTime
        );
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

        _currentMoveSpeed = _dodgeSpeed;
        dodge();
    }

    /// <summary>
    /// DodgeCounter 상태에서 호출되는 매 프레임 타겟 회전 메서드
    /// </summary>
    public void UpdateDodgeCounterRotation(Enemy target)
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
                _movementRotationSpeed* Time.deltaTime
            );
}
    }

    private void dodge()
    {
        if (CharacterController == null || !CharacterController.enabled) return;

        CharacterController.Move(_dashDirection * _currentMoveSpeed * Time.deltaTime);
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


    #region [연출용 고정방향 비자력 이동 처리]

    public void ExecuteDirectionalAnimMovement()
    {
        if (CharacterController == null || !CharacterController.enabled) return;
        if (!_hero.ActionRegistry.IsAnimMoveActive) return;
        if (_animMoveDirection.sqrMagnitude < 0.01f) return;

        float moveMultiplier = GetCurrentCombatMoveMultiplier();
        float finalSpeed = _directionalAnimMoveSpeed * moveMultiplier;

        CharacterController.Move(
            _animMoveDirection *
            finalSpeed *
            Time.deltaTime
        );
    }

    #endregion


    #region [전투 상태 자력 이동 처리 영역]

    /// <summary>
    /// 록온 상태에 따라 자력 유도 물리 이동을 실행하는 메서드
    /// </summary>
    public void ExecuteMagneticMovement()
    {
        if (CharacterController == null || !CharacterController.enabled) return;
        if (!_hero.ActionRegistry.IsAnimMoveActive) return;

        Vector3 moveDir = Vector3.zero;
        float finalSpeed = _magneticSpeed;

        // 현재 공격 종류 및 타수에 맞는 SO 배율 추출
        float magneticMultiplier = GetCurrentCombatMoveMultiplier();

        if (LockOnManager.Instance.MagneticTarget != null)
        {
            float distance = Vector3.Distance(
                transform.position,
                LockOnManager.Instance.MagneticTarget.position
            );

            // 최소 정지 거리 이하에서는 적과 겹치지 않도록 정지
            if (distance <= LockOnManager.Instance.MinStopDistance) return;

            // 자력 유도 최대 범위 밖에서는 기존 애니메이션 이동 방향 사용
            if (distance > LockOnManager.Instance.MaxMagneticRange)
            {
                moveDir = _animMoveDirection;
                finalSpeed = _magneticSpeed * 1.4f;
            }
            else
            {
                moveDir = (
                    LockOnManager.Instance.MagneticTarget.position -
                    transform.position
                ).normalized;

                moveDir.y = 0f;

                if (LockOnManager.Instance.IsHardLockActive)
                {
                    float remainingDistance =
                        distance - LockOnManager.Instance.MinStopDistance;

                    finalSpeed = _magneticSpeed + (remainingDistance * 1.5f);

                    finalSpeed = Mathf.Min(finalSpeed, _sprintSpeed * 2.0f);
                }
                else
                {
                    float t = Mathf.Clamp01(
                        (distance - LockOnManager.Instance.MinStopDistance) /
                        (LockOnManager.Instance.MaxMagneticRange - LockOnManager.Instance.MinStopDistance)
                    );

                    float curveSpeed = Mathf.Lerp(1.4f, 2.0f, t);
                    finalSpeed = _magneticSpeed * curveSpeed;
                }
            }
        }
        else
        {
            // 타겟이 없다면 현재 지향 방향으로 이동
            moveDir = _animMoveDirection;
            finalSpeed = _magneticSpeed * 1.4f;
        }

        finalSpeed *= magneticMultiplier;

        // CombatPresentationManager의 시간 감속과 관계없이 이동 처리
        CharacterController.Move(moveDir * finalSpeed * Time.unscaledDeltaTime);

        // 추후 Dodge에도 unscaledDeltaTime 적용 여부 검토
    }

    

    #endregion


    #region [교대 이동 처리 영역]

    public void ExecuteSwapInMovement()
    {
        if (CharacterController == null || !CharacterController.enabled) return;
        if (!_hero.ActionRegistry.IsAnimMoveActive) return;

        _currentSwapInSpeed = Mathf.Lerp(
            _currentSwapInSpeed,
            0f,
            Time.deltaTime * _swapInfriction
        );

        CharacterController.Move(
            _animMoveDirection * _currentSwapInSpeed * Time.deltaTime
        );
    }

    public void ExecuteSwapOutMovement()
    {
        if (CharacterController == null || !CharacterController.enabled) return;
        if (!_hero.ActionRegistry.IsAnimMoveActive) return;

        float dot = Vector3.Dot(
            _animMoveDirection,
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
            // 뒤로 빠지는 퇴장 모션
            _currentSwapOutSpeed = Mathf.Lerp(
                _currentSwapOutSpeed,
                _maxSwapOutSpeed,
                Time.deltaTime * _swapOutfriction
            );
        }

        CharacterController.Move(
            _animMoveDirection * _currentSwapOutSpeed * Time.deltaTime
        );
    }

    /// <summary>
    /// Swap 애니메이션 이동 구간 시작 처리
    /// 이동 방향 및 Swap 속도 장부 초기화
    /// </summary>
    public void BeginSwapAnimMovement(AnimMoveDirectionType directionType)
    {
        SetAnimMoveDirection(directionType);

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

    #endregion


    #region [패링 이동 처리 영역]

    public void ExecuteParryAssistMovement()
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

    /// <summary>
    /// ParryCounter 상태에서 매 프레임 호출되는 전용 타겟 추적 돌진 메서드
    /// </summary>
    public void UpdateParryCounterMovement(Enemy target)
    {
        if (!_hero.ActionRegistry.IsAnimMoveActive) return;

        Vector3 currentPos = transform.position;
        Vector3 targetPos = target.transform.position;

        currentPos.y = 0f;
        targetPos.y = 0f;

        float currentDistance = Vector3.Distance(currentPos, targetPos);

        if (currentDistance <= _parryStoppingDistance)
        {
            UpdateAnimMoveDirToTarget(transform.forward);
            return;
        }

        float remainDistance = currentDistance - _parryStoppingDistance;

        float targetSpeed = Mathf.Min(_parryDashSpeed, remainDistance * _parryApproachEase);
        
        if (CharacterController != null && CharacterController.enabled)
        {
            Vector3 moveVelocity = transform.forward * targetSpeed;

            CharacterController.Move(moveVelocity * Time.deltaTime);
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
    /// 공격 시작 단계에서 록온 모드에 따라
    /// 전투 타겟과 애니메이션 이동 방향을 갱신
    /// </summary>
    public void RefreshCombatDirection(Vector3 heroAxis)
    {
        LockOnManager lockOnManager = LockOnManager.Instance;

        if (lockOnManager == null) return;

        if (!lockOnManager.IsHardLockActive)
        {
            lockOnManager.RefreshDirectionalSoftLockTarget(heroAxis);
        }

        UpdateAnimMoveDirToTarget(heroAxis);
    }

    /// <summary>
    /// 추적 대상이 있으면 해당 대상을 향해,
    /// 없다면 입력 방향을 기준으로 애니메이션 이동 방향 초기화
    /// </summary>
    public void UpdateAnimMoveDirToTarget(Vector3 heroAxis)
    {
        if (LockOnManager.Instance.MagneticTarget != null)
        {
            _animMoveDirection = (
                LockOnManager.Instance.MagneticTarget.position -
                transform.position
            ).normalized;
        }
        else
        {
            _animMoveDirection = heroAxis.magnitude > 0.1f
                ? heroAxis : transform.forward;
        }

        _animMoveDirection.y = 0f;
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
                else if (_hero.ActionRegistry.IsButtonHolding)
                {
                    targetSequenceList = data.chargeAttackSequences;
                }
                else
                {
                    targetSequenceList = data.normalAttackSequences;
                }
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
            case HeroState.ChainAttack:
                targetSequenceList = data.chainAttackSequences;
                break;
            case HeroState.QuickAssist:
                targetSequenceList = data.quickAssistSequences;
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

    public void SetAnimMoveDirection(AnimMoveDirectionType directionType)
    {
        if (directionType == AnimMoveDirectionType.Keep) return;

        Vector3 direction = directionType switch
        {
            AnimMoveDirectionType.Forward => transform.forward,
            AnimMoveDirectionType.Backward => -transform.forward,
            AnimMoveDirectionType.Left => -transform.right,
            AnimMoveDirectionType.Right => transform.right,
            AnimMoveDirectionType.Target => GetTargetDirection(),
            AnimMoveDirectionType.TargetLeft => GetTargetSideDirection(-1f),
            AnimMoveDirectionType.TargetRight => GetTargetSideDirection(1f),
            _ => transform.forward
        };

        direction.y = 0f;

        if (direction.sqrMagnitude > 0.01f)
            _animMoveDirection = direction.normalized;
    }

    private Vector3 GetTargetDirection()
    {
        LockOnManager lockOnManager = LockOnManager.Instance;

        if (lockOnManager == null || lockOnManager.MagneticTarget == null)
        {
            return transform.forward;
        }

        Vector3 direction = lockOnManager.MagneticTarget.position - transform.position;

        direction.y = 0f;

        return direction.normalized;
    }

    private Vector3 GetTargetSideDirection(float sideSign)
    {
        Vector3 targetDirection = GetTargetDirection();

        Vector3 sideDirection =
            Vector3.Cross(Vector3.up, targetDirection).normalized * sideSign;

        return (targetDirection + sideDirection * _targetSideDirectionWeight).normalized;
    }

    #endregion


    #region [ Setter 영역]

    public void SetIsDashActive(bool isDashActive) => _isDashActive = isDashActive;

    public void SetIsWalkMode(bool isWalkMode) => _isWalkMode = isWalkMode;

    public void SetCurrentParryAssistSpeed(float speed) => _currentParryAssistSpeed = speed;

    #endregion
}