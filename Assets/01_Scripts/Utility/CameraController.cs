using Unity.Cinemachine;
using UnityEngine;

public class CameraController : MonoBehaviour
{
    [Header("Main Gameplay VCam")]
    [SerializeField] private Transform _gameplayVCam;   // Transform을 직접 제어할 Gameplay CM 카메라
    private CinemachineCamera _gameplayCamera;          // CM 컴포넌트 캐싱용
    private Hero _currentHero;                          // Hero가 쏴주는 이벤트를 받기 위한 참조


    [Header("Viewport")]
    [SerializeField] private Camera _mainCamera; // 실제 게임 화면을 출력하는 Main Camera
    [SerializeField] private float _followTargetViewportX; // FollowTarget의 현재 Viewport X 좌표

    [Header("Free Camera Dead Zone Runtime")]
    [SerializeField] private float _currentFreeDeadZoneLeft = 0.5f;
    [SerializeField] private float _currentFreeDeadZoneRight = 0.5f;

    [Header("Free Camera Horizontal Dead Zone")]
    [SerializeField, Range(0f, 1f)]
    private float _freeDeadZoneLeft = 0.48f;

    [SerializeField, Range(0f, 1f)]
    private float _freeDeadZoneRight = 0.52f;

    [Header("Lock On - Free Camera Dead Zone Transition")]
    [SerializeField] private float _lockOnExitDeadZoneRecoverySpeed = 0.25f;
    private float _targetFreeDeadZoneLeft = 0.5f;
    private float _targetFreeDeadZoneRight = 0.5f;

    [Header("Hard Lock Horizontal Dead Zone")]
    [SerializeField, Range(0f, 0.5f)]
    private float _hardLockDeadZoneLeft = 0.35f;

    [SerializeField, Range(0.5f, 1f)]
    private float _hardLockDeadZoneRight = 0.65f;

    [Header("VisibleCursor")]
    [SerializeField] private bool _isVisibleCursor;

    [Header("Position")]
    [SerializeField] private Transform _followTarget;       // 현재 카메라가 따라갈 Hero의 CamPivot
    [SerializeField] private float _baseFollowSpeed = 40f;  // 평상시 추적 속도
    [SerializeField] private float _swapFollowSpeed = 70f;  // 교대 직후 추적 속도
    [SerializeField] private float _swapFollowSpeedRecoverySmoothness = 1f;     // 추적 속도 복구 정도
    private float _currentFollowSpeed;      // 현재 적용 중인 추적 속도

    [Header("Rotation")]
    [SerializeField] private float _rotationSensitivity = 300f;
    [SerializeField] private float _rotationClamp = 25f;
    private float _rotationX;               // 상하 회전 누적값
    private float _rotationY;               // 좌우 회전 누적값


    [Header("카메라 위치 및 충돌 설정")]
    [SerializeField] private Vector3 _cameraLocalOffset = new Vector3(0f, 0.3f, -3.5f); // 기본 카메라 방향과 거리
    [SerializeField] private float _minimumDistance = 1.2f;       // 충돌 시 카메라 최소 접근 거리
    [SerializeField] private float _collisionSmoothness = 10f;  // 충돌 위치 보간 속도
    [SerializeField] private float _collisionPadding = 0.1f;    // 벽과 카메라 사이 여유 거리
    [SerializeField] private LayerMask _collisionMask = ~0;     // 카메라 충돌을 검사할 레이어
    private float _baseCameraDistance;      // 비록온 상태의 기본 카메라 거리
    private Vector3 _cameraDirection;       // Offset에서 계산된 카메라 이동 방향


    [Header("Lock On")]
    [SerializeField] private Transform _lockOnTarget;
    [SerializeField] private float _lockOnRotationSmoothness = 4f;

    [Header("Hard Lock Dynamic Distance")]
    [SerializeField, Min(0f)] private float _combatPullBackStartDistance = 3f;
    [SerializeField, Min(0f)] private float _combatPullBackEndDistance = 8f;
    [SerializeField, Min(0f)] private float _maximumCombatCameraDistance = 4.8f;
    [SerializeField, Min(0f)] private float _combatCameraPullBackSpeed = 4f;
    [SerializeField, Min(0f)] private float _combatCameraRecoverySpeed = 2.5f;

    [Header("Hard Lock Dynamic Distance Runtime")]
    [SerializeField] private float _heroEnemyDistance;
    [SerializeField] private float _currentCameraDistance;
    [SerializeField] private float _targetCameraDistance;

    [Header("Combat Focus")]
    [SerializeField] private Transform _combatFocusPoint;
    [SerializeField, Range(0f, 1f)] private float _enemyFocusWeight = 0.6f;

    [Header("Presentation Focus")]
    private Transform _presentationLookTarget;
    [SerializeField] private float _presentationRotationSmoothness = 10f;

    [Header("Sprint FOV")]
    [SerializeField] private float _sprintFOV = 59f;
    [SerializeField] private float _sprintFOVTransitionSpeed = 12f;
    [SerializeField] private float _sprintFOVRecoverySpeed = 8f;
    private float _baseFOV;
    private float _targetFOV;


    private bool _isLockOnEventSubscribed;  // 락온 이벤트 구독 여부

    private void Start()
    {
        if (_mainCamera == null)
        {
            _mainCamera = Camera.main;
        }

        // 필수 참조가 없으면 CameraController 비활성화
        if (!ValidateReferences()) return;

        _currentFollowSpeed = _baseFollowSpeed;
        _rotationX = NormalizeAngle(transform.localEulerAngles.x);
        _rotationY = transform.localEulerAngles.y;

        InitializeGameplayCamera();
        InitializeCameraOffset();
        InitializeFreeDeadZone();
        InitializeHardLockDeadZone();
        InitializeDynamicDistance();
        InitializeCursor();

        SubscribeLockOnEvent();
        SubscribePartyStateEvent();
    }

    private void Update()
    {
        // 자유 시점 입력과 연출용 회전 처리
        // 하드 록온 회전은 최종 카메라 위치가 결정된 LateUpdate에서 처리
        UpdateRotation();
    }

    private void LateUpdate()
    {
        if (_followTarget == null) return;

        // 출력 중인 연출 카메라와 무관하게 Gameplay 구도로 타겟 위치를 계산함.
        UpdateFollowTargetViewportX();

        if (_lockOnTarget == null)
        {
            UpdateLockOnExitDeadZoneRecovery();
        }

        // Viewport X 초과 여부를 이용한 카메라 위치 갱신
        UpdateFollowPosition();

        UpdateSwapFollowSpeedRecovery();
        UpdateDynamicCameraDistance();
        UpdateCameraCollision();
        UpdateGameplayFOV();

        // 위치·거리·충돌이 모두 반영된 GameplayVCam 위치에서
        // CombatFocusPoint를 마지막으로 조준
        if (_lockOnTarget != null &&
            _presentationLookTarget == null)
        {
            UpdateLockOnRotation();
        }
    }

    private void OnDestroy()
    {
        UnsubscribeHeroEvents(_currentHero);
        UnsubscribeLockOnEvent();
        UnsubscribePartyStateEvent();
    }


    #region [헬퍼 메서드 영역]

    /// <summary>
    /// 실행에 필요한 참조가 할당되었는지 검사
    /// </summary>
    private bool ValidateReferences()
    {
        if (_gameplayVCam == null)
        {
            Debug.LogError("CameraController: GamePlay_VCam이 할당되지 않았습니다.");
            enabled = false;
            return false;
        }

        if (_mainCamera == null)
        {
            Debug.LogError("CameraController: Main Camera가 존재하지 않습니다.");
            enabled = false;
            return false;
        }

        return true;
    }

    /// <summary>
    /// Gameplay VCam의 CinemachineCamera 컴포넌트 캐싱
    /// 및 CM 컴포넌트 FOv 관련 필드 초기화
    /// </summary>
    private void InitializeGameplayCamera()
    {
        if (_gameplayVCam == null) return;

        if (!_gameplayVCam.TryGetComponent(out _gameplayCamera))
        {
            Debug.LogError(
                "CameraController: GamePlay_VCam에서 CinemachineCamera를 찾을 수 없습니다."
            );
            return;
        }

        _baseFOV = _gameplayCamera.Lens.FieldOfView;
        _targetFOV = _baseFOV;
    }

    /// <summary>
    /// LocalOffset을 이용해 카메라의 기본 방향과 거리를 계산
    /// </summary>
    private void InitializeCameraOffset()
    {
        // Offset이 0이면 캐릭터 내부에 카메라가 들어가므로 기본값 적용
        if (_cameraLocalOffset.sqrMagnitude < 0.0001f)
        {
            _cameraLocalOffset = new Vector3(0f, 0.5f, -5.2f);
        }

        _cameraDirection = _cameraLocalOffset.normalized;
        _baseCameraDistance = Mathf.Max(
            _cameraLocalOffset.magnitude,
            _minimumDistance
        );

        _maximumCombatCameraDistance = Mathf.Max(
            _maximumCombatCameraDistance,
            _baseCameraDistance
        );

        _currentCameraDistance = _baseCameraDistance;
        _targetCameraDistance = _baseCameraDistance;
        _gameplayVCam.localPosition =
            _cameraDirection * _currentCameraDistance;
    }

    /// <summary>
    /// 자유 카메라 Dead Zone 런타임값 초기화
    /// </summary>
    private void InitializeFreeDeadZone()
    {
        _freeDeadZoneLeft = Mathf.Clamp01(
            _freeDeadZoneLeft
        );

        _freeDeadZoneRight = Mathf.Clamp(
            _freeDeadZoneRight,
            _freeDeadZoneLeft,
            1f
        );

        _currentFreeDeadZoneLeft =
            _freeDeadZoneLeft;

        _currentFreeDeadZoneRight =
            _freeDeadZoneRight;

        _targetFreeDeadZoneLeft =
            _freeDeadZoneLeft;

        _targetFreeDeadZoneRight =
            _freeDeadZoneRight;
    }

    /// <summary>
    /// 하드 록온 Dead Zone 범위 보정
    /// </summary>
    private void InitializeHardLockDeadZone()
    {
        _hardLockDeadZoneLeft = Mathf.Clamp(
            _hardLockDeadZoneLeft,
            0f,
            0.5f
        );

        _hardLockDeadZoneRight = Mathf.Clamp(
            _hardLockDeadZoneRight,
            0.5f,
            1f
        );
    }

    /// <summary>
    /// 하드 록온 동적 거리 설정 보정
    /// </summary>
    private void InitializeDynamicDistance()
    {
        _combatPullBackEndDistance = Mathf.Max(
            _combatPullBackEndDistance,
            _combatPullBackStartDistance + 0.01f
        );

        _maximumCombatCameraDistance = Mathf.Max(
            _maximumCombatCameraDistance,
            _baseCameraDistance
        );
    }

    /// <summary>
    /// 게임 시작 시 커서 표시 및 잠금 상태 설정
    /// </summary>
    private void InitializeCursor()
    {
        Cursor.lockState = _isVisibleCursor ? CursorLockMode.None : CursorLockMode.Locked;
        Cursor.visible = _isVisibleCursor;
    }

    /// <summary>
    /// 교대 시 낮아진 추적 속도를 기본 속도로 서서히 복구
    /// </summary>
    private void UpdateSwapFollowSpeedRecovery()
    {
        if (_currentFollowSpeed >= _baseFollowSpeed) return;

        _currentFollowSpeed = Mathf.Lerp(
            _currentFollowSpeed,
            _baseFollowSpeed,
            _swapFollowSpeedRecoverySmoothness * Time.deltaTime
        );

        if (_baseFollowSpeed - _currentFollowSpeed < 0.1f)
        {
            _currentFollowSpeed = _baseFollowSpeed;
        }
    }

    /// <summary>
    /// 0~360도 각도를 -180~180도 범위로 보정
    /// </summary>
    private float NormalizeAngle(float angle)
    {
        return angle > 180f ? angle - 360f : angle;
    }

    /// <summary>
    /// 현재 조작 Hero와 카메라 FollowTarget 갱신
    /// </summary>
    public void SetFollowTarget(Hero hero)
    {
        if (hero == null) return;
        if (_currentHero == hero) return;

        UnsubscribeHeroEvents(_currentHero);

        _currentHero = hero;
        _followTarget = hero.CamPivot != null
            ? hero.CamPivot
            : hero.transform;

        SubscribeHeroEvents(_currentHero);
    }

    /// <summary>
    /// 입장 연출의 마지막 구도와 이어지도록 첫 Hero의 후방으로 전투 카메라를 준비함.
    /// </summary>
    public void PrepareEntryView(Hero hero)
    {
        SetFollowTarget(hero);
        _rotationX = 10f;
        _rotationY = hero.transform.eulerAngles.y;
        transform.SetPositionAndRotation(_followTarget.position, Quaternion.Euler(_rotationX, _rotationY, 0f));
    }

    /// <summary>
    /// 교대 직후 카메라 추적 속도를 높여 부드러운 전환 연출 적용
    /// </summary>
    public void TriggerSwapSmoothing()
    {
        _currentFollowSpeed = _swapFollowSpeed;
    }

    /// <summary>
    /// 지정된 Dead Zone을 기준으로 _followTargetViewportX 초과 범위 반환
    /// </summary>
    /// <returns>
    /// 음수 : 왼쪽 경계 초과 / 0 : DeadZone 내부 / 양수 : 오른쪽 경계 초과
    /// </returns>
    private float GetHorizontalViewportOverflow(
        float deadZoneLeft,
        float deadZoneRight
    )
    {
        if (_followTargetViewportX <
            deadZoneLeft)
        {
            return
                _followTargetViewportX -
                deadZoneLeft;
        }

        if (_followTargetViewportX >
            deadZoneRight)
        {
            return
                _followTargetViewportX -
                deadZoneRight;
        }

        return 0f;
    }

    /// <summary>
    /// 화면상 초과량을 Gameplay 카메라 기준의 월드 좌우 보정량으로 변환함.
    /// </summary>
    private Vector3 GetHorizontalWorldCorrection(float horizontalOverflow)
    {
        if (Mathf.Approximately(horizontalOverflow, 0f))
        {
            return Vector3.zero;
        }

        Vector3 targetDelta = _followTarget.position - _gameplayVCam.position;
        float depth = Vector3.Dot(targetDelta, _gameplayVCam.forward);
        if (depth <= 0f) return Vector3.zero;

        float correctionDistance = horizontalOverflow * GetGameplayViewWidth(depth);
        return _gameplayVCam.right * correctionDistance;
    }

    /// <summary>
    /// 하드 록온 해제 당시 Hero의 화면 위치를 임시 경계로 사용해
    /// 자유 카메라 Dead Zone 복귀 중 순간 이동 방지
    /// </summary>
    private void BeginLockOnExitDeadZoneRecovery()
    {
        UpdateFollowTargetViewportX();

        _currentFreeDeadZoneLeft = Mathf.Min(
            _followTargetViewportX,
            _freeDeadZoneLeft
        );

        _currentFreeDeadZoneRight = Mathf.Max(
            _followTargetViewportX,
            _freeDeadZoneRight
        );

        _targetFreeDeadZoneLeft =
            _freeDeadZoneLeft;

        _targetFreeDeadZoneRight =
            _freeDeadZoneRight;
    }

    /// <summary>
    /// 락온 해제 시 자유 카메라 Dead Zone을 기본 범위로 점진적 복귀
    /// </summary>
    private void UpdateLockOnExitDeadZoneRecovery()
    {
        _currentFreeDeadZoneLeft = Mathf.MoveTowards(
            _currentFreeDeadZoneLeft,
            _targetFreeDeadZoneLeft,
            _lockOnExitDeadZoneRecoverySpeed * Time.deltaTime
        );

        _currentFreeDeadZoneRight = Mathf.MoveTowards(
            _currentFreeDeadZoneRight,
            _targetFreeDeadZoneRight,
            _lockOnExitDeadZoneRecoverySpeed * Time.deltaTime
        );
    }

    #endregion


    #region[Update Method 영역]

    /// <summary>
    /// 자유 시점과 연출용 카메라 회전 방식 분기
    /// 하드 록온 회전은 LateUpdate 마지막에 별도 처리
    /// </summary>
    private void UpdateRotation()
    {
        // 연출용 회전 처리
        if (_presentationLookTarget != null)
        {
            UpdatePresentationRotation();
            return;
        }

        // 자유 시점 또는 비록온 회전 처리
        if (_lockOnTarget == null)
        {
            UpdateFreeRotation();
        }
    }

    /// <summary>
    /// 마우스 입력을 이용한 일반 카메라 회전
    /// </summary>
    private void UpdateFreeRotation()
    {
        if (_currentHero != null && _currentHero.CurrentState == HeroState.Entry) return;
        if (InputManager.Instance != null && InputManager.Instance.IsSettingsInputActive) return;

        float sensitivityMultiplier = SettingsManager.Instance != null
            ? SettingsManager.Instance.MouseSensitivity : 1f;
        float sensitivity = _rotationSensitivity * sensitivityMultiplier;
        _rotationX -= Input.GetAxis("Mouse Y") * sensitivity * Time.deltaTime;
        _rotationY += Input.GetAxis("Mouse X") * sensitivity * Time.deltaTime;
        _rotationX = Mathf.Clamp(_rotationX, -_rotationClamp, _rotationClamp);

        transform.rotation = Quaternion.Euler(_rotationX, _rotationY, 0f);
    }

    /// <summary>
    /// 전투 중심점을 바라보도록 Gameplay 카메라를 회전함.
    /// </summary>
    private void UpdateLockOnRotation()
    {
        if (!TryGetLockOnBaseRotation(out Quaternion baseRotation))
        {
            return;
        }

        Vector3 baseEuler = baseRotation.eulerAngles;

        float targetPitch = Mathf.Clamp(
            NormalizeAngle(baseEuler.x),
            -_rotationClamp,
            _rotationClamp
        );

        float targetYaw = baseEuler.y;

        Quaternion targetRotation = Quaternion.Euler(
            targetPitch,
            targetYaw,
            0f
        );

        transform.rotation = Quaternion.Slerp(
            transform.rotation,
            targetRotation,
            _lockOnRotationSmoothness * Time.deltaTime
        );
    }

    /// <summary>
    /// Sprint 상태에 따라 Gameplay VCam의 FOV를 목표값으로 보간
    /// </summary>
    private void UpdateGameplayFOV()
    {
        if (_gameplayCamera == null) return;

        float currentFOV =
            _gameplayCamera.Lens.FieldOfView;

        float transitionSpeed =
            _targetFOV > currentFOV
                ? _sprintFOVTransitionSpeed
                : _sprintFOVRecoverySpeed;

        _gameplayCamera.Lens.FieldOfView =
            Mathf.MoveTowards(
                currentFOV,
                _targetFOV,
                transitionSpeed * Time.deltaTime
            );
    }

    /// <summary>
    /// 현재 Hero와 LockOnTarget의 전투 중심점을 바라보는
    /// 기본 하드 록온 회전 계산
    /// </summary>
    private bool TryGetLockOnBaseRotation(
        out Quaternion baseRotation
    )
    {
        Vector3 cameraPosition =
            _gameplayVCam != null
                ? _gameplayVCam.position
                : transform.position;

        return TryGetLockOnBaseRotation(
            cameraPosition,
            out baseRotation
        );
    }

    /// <summary>
    /// 지정한 카메라 월드 위치에서 CombatFocusPoint를 바라보는
    /// 기본 하드 록온 회전 계산
    /// </summary>
    private bool TryGetLockOnBaseRotation(
        Vector3 cameraPosition,
        out Quaternion baseRotation
    )
    {
        baseRotation = Quaternion.identity;

        if (_lockOnTarget == null ||
            _combatFocusPoint == null)
        {
            return false;
        }

        UpdateCombatFocusPoint();

        Vector3 targetDirection =
            _combatFocusPoint.position -
            cameraPosition;

        if (targetDirection.sqrMagnitude <= Mathf.Epsilon)
        {
            return false;
        }

        baseRotation = Quaternion.LookRotation(
            targetDirection.normalized,
            Vector3.up
        );

        return true;
    }

    /// <summary>
    /// 연출 중 Gameplay 카메라를 지정된 대상으로 미리 정렬
    /// </summary>
    private void UpdatePresentationRotation()
    {
        Vector3 targetPosition = _presentationLookTarget.position;
        Vector3 targetDirection = targetPosition - transform.position;

        if (targetDirection.sqrMagnitude < 0.0001f) return;

        Quaternion targetRotation = Quaternion.LookRotation(targetDirection.normalized);
        Vector3 targetEuler = targetRotation.eulerAngles;

        float targetPitch = NormalizeAngle(targetEuler.x);
        targetPitch = Mathf.Clamp(targetPitch, -_rotationClamp, _rotationClamp);
        targetRotation = Quaternion.Euler(targetPitch, targetEuler.y, 0f);

        float rotationLerp = 1f - Mathf.Exp(-_presentationRotationSmoothness * Time.unscaledDeltaTime);

        transform.rotation = Quaternion.Slerp(
            transform.rotation,
            targetRotation,
            rotationLerp
        );
    }

    /// <summary>
    /// 카메라 상태에 따라 FollowTarget 추종 방식을 선택함.
    /// </summary>
    private void UpdateFollowPosition()
    {
        if (_presentationLookTarget != null)
        {
            // 화면에는 연출 VCam이 출력되고 있으므로
            // 숨겨진 Gameplay 카메라는 현재 Hero 위치에 즉시 준비
            transform.position = _followTarget.position;
            return;
        }

        if (_lockOnTarget != null)
        {
            UpdateHardLockFollowPosition();
            return;
        }

        UpdateFreeFollowPosition();
    }

    /// <summary>
    /// 비록온 상태에서 Hero의 수평 Dead Zone을 적용해 카메라 루트 추종
    /// </summary>
    private void UpdateFreeFollowPosition()
    {
        UpdateDeadZoneFollowPosition(
            _currentFreeDeadZoneLeft,
            _currentFreeDeadZoneRight
        );
    }

    /// <summary>
    /// 하드 록온 상태에서 더 넓은 Dead Zone을 적용해
    /// Hero가 화면 안에서 좌우로 이동할 수 있도록 카메라 루트 추종
    /// </summary>
    private void UpdateHardLockFollowPosition()
    {
        UpdateDeadZoneFollowPosition(
            _hardLockDeadZoneLeft,
            _hardLockDeadZoneRight
        );
    }

    /// <summary>
    /// Gameplay 구도의 Dead Zone을 벗어난 Hero를 따라 카메라 루트 위치를 보정함.
    /// </summary>
    private void UpdateDeadZoneFollowPosition(
        float deadZoneLeft,
        float deadZoneRight
    )
    {
        Vector3 followDelta =
            _followTarget.position - transform.position;

        Vector3 cameraRight =
            _gameplayVCam.right;

        // FollowTarget 이동량 중 화면 좌우 방향 성분 분리
        Vector3 horizontalDelta =
            Vector3.Project(followDelta, cameraRight);

        // Y축과 카메라 앞뒤 방향은 기존처럼 항상 추종
        Vector3 continuousFollowDelta =
            followDelta - horizontalDelta;

        float horizontalOverflow =
            GetHorizontalViewportOverflow(
                deadZoneLeft,
                deadZoneRight
            );

        Vector3 horizontalCorrection =
            GetHorizontalWorldCorrection(horizontalOverflow);

        Vector3 targetPosition =
            transform.position
            + continuousFollowDelta
            + horizontalCorrection;

        transform.position = Vector3.MoveTowards(
            transform.position,
            targetPosition,
            _currentFollowSpeed * Time.deltaTime
        );
    }

    /// <summary>
    /// 타겟 깊이에서 원근 Gameplay 카메라 화면의 월드 너비를 반환함.
    /// </summary>
    private float GetGameplayViewWidth(float depth)
    {
        float halfFOV = _gameplayCamera.Lens.FieldOfView * 0.5f * Mathf.Deg2Rad;
        return 2f * depth * Mathf.Tan(halfFOV) * _mainCamera.aspect;
    }

    /// <summary>
    /// Gameplay 카메라 자체 구도를 기준으로 타겟의 화면상 X 위치를 계산함.
    /// </summary>
    private void UpdateFollowTargetViewportX()
    {
        Vector3 targetDelta = _followTarget.position - _gameplayVCam.position;
        float depth = Vector3.Dot(targetDelta, _gameplayVCam.forward);
        if (depth <= 0f)
        {
            _followTargetViewportX = 0.5f;
            return;
        }

        float horizontalDistance = Vector3.Dot(targetDelta, _gameplayVCam.right);
        float viewWidth = GetGameplayViewWidth(depth);
        _followTargetViewportX = 0.5f + horizontalDistance / Mathf.Max(viewWidth, 0.001f);
    }

    /// <summary>
    /// CombatFoucsPoint Update마다 계산
    /// Hero - LockOnTarget 사이의 중간점 실시간 계산
    /// </summary>
    private void UpdateCombatFocusPoint()
    {
        if (_followTarget == null ||
            _lockOnTarget == null ||
            _combatFocusPoint == null)
        {
            return;
        }

        _combatFocusPoint.position = Vector3.Lerp(
            _followTarget.position,
            _lockOnTarget.position,
            _enemyFocusWeight
        );
    }

    /// <summary>
    /// 하드 록온 중 Hero-Enemy 거리에 따라
    /// Gameplay 카메라의 목표 거리를 계산하고 보간
    /// </summary>
    private void UpdateDynamicCameraDistance()
    {
        if (_lockOnTarget == null)
        {
            _heroEnemyDistance = 0f;
            _targetCameraDistance =
                _baseCameraDistance;
        }
        else
        {
            _heroEnemyDistance = Vector3.Distance(
                _followTarget.position,
                _lockOnTarget.position
            );

            float distanceRatio = Mathf.InverseLerp(
                _combatPullBackStartDistance,
                _combatPullBackEndDistance,
                _heroEnemyDistance
            );

            _targetCameraDistance = Mathf.Lerp(
                _baseCameraDistance,
                _maximumCombatCameraDistance,
                distanceRatio
            );
        }

        float distanceSpeed =
            _targetCameraDistance >
            _currentCameraDistance
                ? _combatCameraPullBackSpeed
                : _combatCameraRecoverySpeed;

        _currentCameraDistance = Mathf.MoveTowards(
            _currentCameraDistance,
            _targetCameraDistance,
            distanceSpeed * Time.deltaTime
        );
    }

    /// <summary>
    /// 카메라와 캐릭터 사이의 벽을 감지해 카메라 거리를 조절
    /// </summary>
    private void UpdateCameraCollision()
    {
        // 충돌이 없을 때 카메라가 위치할 월드 좌표
        Vector3 desiredWorldPosition =
            transform.TransformPoint(
                _cameraDirection *
                _currentCameraDistance
            );

        float targetDistance =
            _currentCameraDistance;

        // 캐릭터 피벗에서 카메라 위치까지 벽 검사
        if (Physics.Linecast(
            transform.position,
            desiredWorldPosition,
            out RaycastHit hit,
            _collisionMask,
            QueryTriggerInteraction.Ignore))
        {
            targetDistance = Mathf.Clamp(
                hit.distance - _collisionPadding,
                _minimumDistance,
                _currentCameraDistance
            );
        }

        Vector3 targetLocalPosition = _cameraDirection * targetDistance;

        // 벽 충돌 시 Cinemachine Camera를 부드럽게 앞으로 이동
        _gameplayVCam.localPosition = Vector3.Lerp(
            _gameplayVCam.localPosition,
            targetLocalPosition,
            Time.deltaTime * _collisionSmoothness
        );
    }

    #endregion


    #region [Presentation Camera 헬퍼 메서드]

    /// <summary>
    /// 연출 중 Gameplay 카메라가 바라볼 대상을 설정함.
    /// </summary>
    public void BeginPresentationFocus(Transform target)
    {
        if (target == null) return;

        _presentationLookTarget = target;

        if (_followTarget != null)
        {
            transform.position = _followTarget.position;
        }
    }

    /// <summary>
    /// 연출용 시점 고정을 해제하고 현재 회전을 자유 시점 입력값과 동기화
    /// </summary>
    public void EndPresentationFocus()
    {
        _presentationLookTarget = null;

        Vector3 currentEuler = transform.rotation.eulerAngles;
        _rotationX = NormalizeAngle(currentEuler.x);
        _rotationY = currentEuler.y;
    }

    #endregion


    #region [이벤트 영역]

    /// <summary>
    /// LockOnManager의 타겟 변경 이벤트 구독
    /// </summary>
    private void SubscribeLockOnEvent()
    {
        if (LockOnManager.Instance == null)
        {
            Debug.LogError("CameraController: LockOnManager가 존재하지 않습니다.");
            return;
        }

        LockOnManager.Instance.OnLockOnTargetChanged += HandleLockOnTargetChanged;
        _isLockOnEventSubscribed = true;
    }
    /// <summary>
    /// LockOnManager의 타겟 변경 이벤트 구독
    /// </summary>
    private void UnsubscribeLockOnEvent()
    {
        // 등록했던 락온 이벤트 해제
        if (_isLockOnEventSubscribed && LockOnManager.Instance != null)
        {
            LockOnManager.Instance.OnLockOnTargetChanged -= HandleLockOnTargetChanged;
        }
    }

    /// <summary>
    /// 락온 대상을 갱신하고 해제 시 자유 시점으로 전환함.
    /// </summary>
    private void HandleLockOnTargetChanged(Transform newTarget)
    {
        bool wasHardLockActive =
            _lockOnTarget != null;

        _lockOnTarget = newTarget;

        if (wasHardLockActive &&
            _lockOnTarget == null)
        {
            BeginLockOnExitDeadZoneRecovery();
        }

        // 락온 해제 시 현재 시점과 마우스 회전값을 동기화
        if (_lockOnTarget != null) return;

        Vector3 currentEuler = transform.rotation.eulerAngles;

        _rotationX = NormalizeAngle(currentEuler.x);

        _rotationY = currentEuler.y;
    }


    /// <summary>
    /// Hero의 이벤트 구독
    /// </summary>
    private void SubscribeHeroEvents(Hero hero)
    {
        if (hero == null) return;

        hero.OnSprintStateChanged += HandleSprintStateChanged;
    }
    /// <summary>
    /// Hero의 이벤트 구독 해제
    /// </summary>
    private void UnsubscribeHeroEvents(Hero hero)
    {
        if (hero == null) return;

        hero.OnSprintStateChanged -= HandleSprintStateChanged;
    }

    private void HandleSprintStateChanged(bool isSprinting)
    {
        _targetFOV = isSprinting ? _sprintFOV : _baseFOV;
    }

    /// <summary>
    /// PartyStateManager의 CurrentHero 변경 이벤트 구독
    /// </summary>
    private void SubscribePartyStateEvent()
    {
        if (PartyStateManager.Instance == null) return;

        PartyStateManager.Instance.OnCurrentHeroChanged += HandleCurrentHeroChanged;
    }
    /// <summary>
    /// PartyStateManager의 CurrentHero 변경 이벤트 구독 해제
    /// </summary>
    private void UnsubscribePartyStateEvent()
    {
        if (PartyStateManager.Instance == null) return;

        PartyStateManager.Instance.OnCurrentHeroChanged -= HandleCurrentHeroChanged;
    }
    private void HandleCurrentHeroChanged(Hero hero)
    {
        SetFollowTarget(hero);
    }




    #endregion




}
