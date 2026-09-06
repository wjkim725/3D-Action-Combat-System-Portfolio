using UnityEngine;

public class CameraController : MonoBehaviour
{
    [Header("커서 설정")]
    [SerializeField] private bool _isVisibleCursor;

    [Header("추적 설정")]
    [SerializeField] private Transform _followTarget;       // 현재 카메라가 따라갈 Hero의 CamPivot
    [SerializeField] private float _baseFollowSpeed = 60f;  // 평상시 추적 속도
    [SerializeField] private float _swapFollowSpeed = 10f;  // 교대 직후 느려진 추적 속도
    [SerializeField] private float _followDamping = 1f;     // 추적 속도 복구 정도

    [Header("회전 설정")]
    [SerializeField] private float _sensitivity = 300f;     // 마우스 회전 감도
    [SerializeField] private float _clampAngle = 25f;       // 카메라 상하 회전 제한

    [Header("카메라 위치 및 충돌 설정")]
    [SerializeField] private Transform _gameplayVCam; // Transform을 직접 제어할 Gameplay CM 카메라
    [SerializeField] private Vector3 _cameraLocalOffset = new Vector3(0f, 0.5f, -5.2f); // 기본 카메라 방향과 거리
    [SerializeField] private float _minimumDistance = 2f;       // 충돌 시 카메라 최소 접근 거리
    [SerializeField] private float _collisionSmoothness = 10f;  // 충돌 위치 보간 속도
    [SerializeField] private float _collisionPadding = 0.1f;    // 벽과 카메라 사이 여유 거리
    [SerializeField] private LayerMask _collisionMask = ~0;     // 카메라 충돌을 검사할 레이어

    [Header("락온 설정")]
    [SerializeField] private Transform _lockOnTarget;               // 현재 락온 대상
    [SerializeField] private float _lockOnRotationSmoothness = 4f;  // 락온 회전 보간 속도
    [SerializeField] private float _lockOnPitchOffset = 0.5f;      // 락온 대상 조준 높이 보정

    [Header("Presentation Focus")]
    [SerializeField] private float _presentationRotationSmoothness = 10f;
    private Transform _presentationLookTarget;

    private Vector3 _cameraDirection;       // Offset에서 계산된 카메라 이동 방향
    private float _desiredCameraDistance;   // 충돌이 없을 때의 기본 카메라 거리
    private float _currentFollowSpeed;      // 현재 적용 중인 추적 속도
    private float _rotationX;               // 상하 회전 누적값
    private float _rotationY;               // 좌우 회전 누적값
    private bool _isLockOnEventSubscribed;  // 락온 이벤트 구독 여부

    private void Start()
    {
        // 필수 참조가 없으면 CameraController 비활성화
        if (!ValidateReferences()) return;

        _currentFollowSpeed = _baseFollowSpeed;
        _rotationX = NormalizeAngle(transform.localEulerAngles.x);
        _rotationY = transform.localEulerAngles.y;

        InitializeCameraOffset();
        InitializeCursor();
        SubscribeLockOnEvent();
    }

    private void Update()
    {
        // 자유 시점 또는 락온 시점 회전 처리
        UpdateRotation();
    }

    private void LateUpdate()
    {
        if (_followTarget == null) return;

        // Hero 이동이 끝난 뒤 카메라 위치와 충돌 처리
        UpdateFollowPosition();
        RecoverFollowSpeed();
        UpdateCameraCollision();
    }

    private void OnDestroy()
    {
        // 등록했던 락온 이벤트 해제
        if (_isLockOnEventSubscribed && LockOnManager.Instance != null)
        {
            LockOnManager.Instance.OnLockOnTargetChanged -= HandleLockOnTargetChanged;
        }
    }

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

        if (_followTarget == null)
        {
            Debug.LogError("CameraController: FollowTarget이 할당되지 않았습니다.");
            enabled = false;
            return false;
        }

        return true;
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
        _desiredCameraDistance = Mathf.Max(_cameraLocalOffset.magnitude, _minimumDistance);
        _gameplayVCam.localPosition = _cameraDirection * _desiredCameraDistance;
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
    /// 락온 유무에 따라 카메라 회전 방식 분기
    /// </summary>
    private void UpdateRotation()
    {
        // 연출용 회전 처리
        if (_presentationLookTarget != null)
        {
            UpdatePresentationRotation();
            return;
        }

        // 소프트 록온 회전 처리
        if (_lockOnTarget == null)
        {
            UpdateFreeRotation();
            return;
        }

        // 하드 록온 회전 처리
        UpdateLockOnRotation();
    }

    /// <summary>
    /// 마우스 입력을 이용한 일반 카메라 회전
    /// </summary>
    private void UpdateFreeRotation()
    {
        _rotationX -= Input.GetAxis("Mouse Y") * _sensitivity * Time.deltaTime;
        _rotationY += Input.GetAxis("Mouse X") * _sensitivity * Time.deltaTime;
        _rotationX = Mathf.Clamp(_rotationX, -_clampAngle, _clampAngle);

        transform.rotation = Quaternion.Euler(_rotationX, _rotationY, 0f);
    }

    /// <summary>
    /// 락온 대상을 바라보도록 카메라 회전
    /// </summary>
    private void UpdateLockOnRotation()
    {
        Vector3 targetPosition = _lockOnTarget.position + Vector3.up * _lockOnPitchOffset;
        Vector3 targetDirection = targetPosition - transform.position;

        if (targetDirection.sqrMagnitude < 0.0001f) return;

        Quaternion targetRotation = Quaternion.LookRotation(targetDirection.normalized);
        Vector3 targetEuler = targetRotation.eulerAngles;

        // 락온 중에도 상하 회전 제한 적용
        float targetPitch = NormalizeAngle(targetEuler.x);
        targetPitch = Mathf.Clamp(targetPitch, -_clampAngle, _clampAngle);
        targetRotation = Quaternion.Euler(targetPitch, targetEuler.y, 0f);

        transform.rotation = Quaternion.Slerp(
            transform.rotation,
            targetRotation,
            _lockOnRotationSmoothness * Time.deltaTime
        );
    }

    /// <summary>
    /// 연출 중 Gameplay 카메라를 지정된 대상으로 미리 정렬
    /// </summary>
    private void UpdatePresentationRotation()
    {
        Vector3 targetPosition = _presentationLookTarget.position + Vector3.up * _lockOnPitchOffset;
        Vector3 targetDirection = targetPosition - transform.position;

        if (targetDirection.sqrMagnitude < 0.0001f) return;

        Quaternion targetRotation = Quaternion.LookRotation(targetDirection.normalized);
        Vector3 targetEuler = targetRotation.eulerAngles;

        float targetPitch = NormalizeAngle(targetEuler.x);
        targetPitch = Mathf.Clamp(targetPitch, -_clampAngle, _clampAngle);
        targetRotation = Quaternion.Euler(targetPitch, targetEuler.y, 0f);

        float rotationLerp = 1f - Mathf.Exp(-_presentationRotationSmoothness * Time.unscaledDeltaTime);

        transform.rotation = Quaternion.Slerp(
            transform.rotation,
            targetRotation,
            rotationLerp
        );
    }

    /// <summary>
    /// 현재 FollowTarget 위치로 카메라 루트 이동
    /// </summary>
    private void UpdateFollowPosition()
    {
        if (_presentationLookTarget != null)
        {
            // 화면에는 Parry_VCam이 출력되고 있으므로
            // 숨겨진 Gameplay 카메라는 현재 Hero 위치에 즉시 준비
            transform.position = _followTarget.position;
            return;
        }

        transform.position = Vector3.MoveTowards(
            transform.position,
            _followTarget.position,
            _currentFollowSpeed * Time.deltaTime
        );
    }

    /// <summary>
    /// 교대 시 낮아진 추적 속도를 기본 속도로 서서히 복구
    /// </summary>
    private void RecoverFollowSpeed()
    {
        if (_currentFollowSpeed >= _baseFollowSpeed) return;

        _currentFollowSpeed = Mathf.Lerp(
            _currentFollowSpeed,
            _baseFollowSpeed,
            _followDamping * Time.deltaTime
        );

        if (_baseFollowSpeed - _currentFollowSpeed < 0.1f)
        {
            _currentFollowSpeed = _baseFollowSpeed;
        }
    }

    /// <summary>
    /// 카메라와 캐릭터 사이의 벽을 감지해 카메라 거리를 조절
    /// </summary>
    private void UpdateCameraCollision()
    {
        // 충돌이 없을 때 카메라가 위치할 월드 좌표
        Vector3 desiredWorldPosition =
            transform.TransformPoint(_cameraDirection * _desiredCameraDistance);

        float targetDistance = _desiredCameraDistance;

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
                _desiredCameraDistance
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

    /// <summary>
    /// 락온 대상이 변경될 때 호출
    /// </summary>
    private void HandleLockOnTargetChanged(Transform newTarget)
    {
        _lockOnTarget = newTarget;

        // 락온 해제 시 현재 시점과 마우스 회전값을 동기화
        if (_lockOnTarget != null) return;

        Vector3 currentEuler = transform.rotation.eulerAngles;
        _rotationX = NormalizeAngle(currentEuler.x);
        _rotationY = currentEuler.y;
    }

    /// <summary>
    /// 0~360도 각도를 -180~180도 범위로 보정
    /// </summary>
    private float NormalizeAngle(float angle)
    {
        return angle > 180f ? angle - 360f : angle;
    }

    /// <summary>
    /// 교대된 Hero의 CamPivot으로 추적 대상 변경
    /// </summary>
    public void SetFollowTarget(Transform target)
    {
        if (target == null) return;
        _followTarget = target;
    }

    /// <summary>
    /// 교대 직후 카메라 추적 속도를 낮춰 부드러운 전환 연출 적용
    /// </summary>
    public void TriggerSwapSmoothing()
    {
        _currentFollowSpeed = _swapFollowSpeed;
    }

    /// <summary>
    /// 연출 종료 후 복귀할 Gameplay 카메라 시점을 대상 방향으로 준비
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
}