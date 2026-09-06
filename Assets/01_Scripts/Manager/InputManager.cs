using System;
using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;


public class InputManager : MonoBehaviour
{
    // 싱글톤
    public static InputManager Instance { get; private set; }

    // 유니티가 자동 생성해준 클래스 <- 설정한 입력들을 클래스화
    private HeroActions _controls;

    // 외부에서 사용할수 있게 프로퍼티로 노출
    public Vector2 MoveInput { get; private set; }

    [Header("LockOn Settings")]
    private float _mouseDeltaThreshold = 50f;           // 하드 록온 변경점 (마우스 입력)
    private float _directionChangeThreshold = 0.5f;     // 소프트 록온 변경점 (키 입력)
    private bool _canSwitchTarget = true;       // 튕기기 입력 가능 여부 플래그
    private float _SwitchCoolTime = 0.15f;      // 최소 입력 제한 시간 (초)
    [HideInInspector]
    public bool IsLockOnActive;     // 외부에서 통제할 플래그 (취급주의)
    private Vector2 _lastMoveInput = Vector2.zero;  // 이전 프레임의 방향키 입력 값을 저장하여 입력의 변화율을 계산할 때 사용

    #region [외부 구독 이벤트 영역]

    // Hero 영역
    public event Action OnDashPerformed;
    public event Action OnDashStarted;
    public event Action OnDashCanceled;

    public event Action OnWalkPerformed;

    public event Action OnAttackPerformed; // 일반 클릭/연타용
    public event Action OnAttackStarted;   // 차징 시작용 (예약)
    public event Action OnAttackCanceled;  // 차징 해제용 (예약)

    public event Action OnSkillPerformed;
    public event Action OnSkillStarted;
    public event Action OnSkillCanceled;

    public event Action OnUltPerformed;
    public event Action OnUltStarted;
    public event Action OnUltCanceled;
    
    // Interaction 
    public event Action OnInteractionPerformed;

    // PartySwapManager 영역
    public event Action OnSwapNextPerformed;
    public event Action OnSwapPrevPerformed;

    // LockOnManager 영역
    public event Action OnLockOnPerformed;
    // 하드 록온 - LockOn 타겟 변경 - true 오른쪽 / false 왼쪽
    public event System.Action<bool> OnHardLockOnSwitchPerformed;
    // 소프트 록온 - LockOn 타겟 변경 - 방향키 입력값 전달
    public event System.Action<Vector2> OnSoftLockOnSwitchPerformed;

    // ChainAttackManager 영역
    public event Action OnChainPrevPerformed;
    public event Action OnChainNextPerformed;
    public event Action OnChainCancelPerformed;

    #endregion

    [Header("QTE Flag")]
    public bool IsChainQTEInputActive => _isChainQTEInputActive;
    private bool _isChainQTEInputActive;

    [Header("QTE Input Settings")]
    [SerializeField, Min(0f)] private float _chainQTEInputDelay = 0.4f;
    private Coroutine _chainQTEInputCoroutine;



    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        // 컨트롤 객체 생성
        _controls = new HeroActions();

        // 이벤트 구독 및 다이렉트 토스 (람다식 방식)
        // [Dash]
        _controls.GamePlay.Dash.started += ctx => OnDashStarted?.Invoke();
        _controls.GamePlay.Dash.performed += ctx => OnDashPerformed?.Invoke();
        _controls.GamePlay.Dash.canceled += ctx => OnDashCanceled?.Invoke();

        // [Walk]
        _controls.GamePlay.Walk.performed += ctx => OnWalkPerformed?.Invoke();

        // [Attack]
        _controls.GamePlay.Attack.started += ctx => OnAttackStarted?.Invoke();
        _controls.GamePlay.Attack.performed += ctx => OnAttackPerformed?.Invoke();
        _controls.GamePlay.Attack.canceled += ctx => OnAttackCanceled?.Invoke();

        // [Skill]
        _controls.GamePlay.Skill.started += ctx => OnSkillStarted?.Invoke();
        _controls.GamePlay.Skill.performed += ctx => OnSkillPerformed?.Invoke();
        _controls.GamePlay.Skill.canceled += ctx => OnSkillCanceled?.Invoke();

        // [Ult]
        _controls.GamePlay.Ult.started += ctx => OnUltStarted?.Invoke();
        _controls.GamePlay.Ult.performed += ctx => OnUltPerformed?.Invoke();
        _controls.GamePlay.Ult.canceled += ctx => OnUltCanceled?.Invoke();

        // [Swap]
        _controls.GamePlay.SwapNext.performed += ctx => OnSwapNextPerformed?.Invoke();
        _controls.GamePlay.SwapPrev.performed += ctx => OnSwapPrevPerformed?.Invoke();

        // [Interaction]
        _controls.GamePlay.Interaction.performed += ctx => OnInteractionPerformed?.Invoke();

        // [LockOn]
        _controls.GamePlay.LockOn.performed += ctx => OnLockOnPerformed?.Invoke();

        // [LockOnSwitch]
        // HeroLockOn에서 구독

        // [Chain QTE]
        _controls.ChainQTE.SelectPrev.performed += ctx => OnChainPrevPerformed?.Invoke();
        _controls.ChainQTE.SelectNext.performed += ctx => OnChainNextPerformed?.Invoke();
        _controls.ChainQTE.Cancel.performed += ctx => OnChainCancelPerformed?.Invoke();
    }

    private void OnEnable()
    {
        _controls.GamePlay.Enable();
        _controls.ChainQTE.Disable();
    }
    private void OnDisable()
    {
        _controls.Disable();
    }

    private void Update()
    {
        // ChainAttack QTE 진입 시 InputManager 자체 입력 차단
        if (_isChainQTEInputActive)
        {
            MoveInput = Vector2.zero;
            _lastMoveInput = Vector2.zero;
            return;
        }

        // 이동은 update에서 value 받아옴.
        MoveInput = _controls.GamePlay.Move.ReadValue<Vector2>();

        // 하드 락온 상태일 때만 마우스 델타값 감시
        if (IsLockOnActive)
        {
            CheckMouseFlip();
        }
        // 소프트 락온 상태일 때만 방향키 꺾기 실시간 감시 연산
        else
        {
            CheckDirectionalFlip();
        }

        // SoftLockOnSwitch를 위해 직전 프레임 방향키 입력값 저장
        _lastMoveInput = MoveInput;
    }

    // --- 유틸리티 메서드 (외부에서 상태 체크용) ---
    // 대시 버튼이 현재 물리적으로 눌려있는지 확인 (전력질주 로직용)
    public bool IsDashPressed() => _controls.GamePlay.Dash.IsPressed();

    // 아무런 입력이 있는지 확인
    public bool HasAnyInput()
    {
        if (MoveInput.sqrMagnitude > 0.01f) return true;

        // 주요 액션 버튼 중 하나라도 눌려있으면 true
        if (_controls.GamePlay.Attack.IsPressed() || _controls.GamePlay.Dash.IsPressed()
            || _controls.GamePlay.Skill.IsPressed()) 
            return true;

        return false;
    }

    // 하드 록온 상태에서 마우스를 꺾는 순간을 감지하여 이벤트를 발생시키는 메서드
    private void CheckMouseFlip()
    {
        // 이미 입력이 한 번 들어가서 잠긴 상태
        if (!_canSwitchTarget) return;

        Vector2 mouseDelta = Mouse.current.delta.ReadValue();

        // 오른쪽으로 임계값 이상 튕겼을 때
        if (mouseDelta.x > _mouseDeltaThreshold)
        {
            _canSwitchTarget = false;
            OnHardLockOnSwitchPerformed?.Invoke(true);
            StartCoroutine(ResetFlipCooldown());
        }
        // 왼쪽으로 임계값 이상 튕겼을 때
        else if (mouseDelta.x < -_mouseDeltaThreshold)
        {
            _canSwitchTarget = false;
            OnHardLockOnSwitchPerformed?.Invoke(false);
            StartCoroutine(ResetFlipCooldown());
        }
    }

    // 쿨타임이 지나면 타겟 바꾸기가 가능해지도록 해주는 코루틴
    private IEnumerator ResetFlipCooldown()
    {
        yield return new WaitForSeconds(_SwitchCoolTime);
        _canSwitchTarget = true;
    }

    // 소프트 록온 상태에서 방향키를 꺾는 순간을 감지하여 이벤트를 발생시키는 메서드
    private void CheckDirectionalFlip()
    {
        // 이미 입력이 한 번 들어가서 잠긴 상태
        if (!_canSwitchTarget) return;

        // 현재 입력과 직전 프레임 입력이 둘 다 유효할 때만 방향 전환 연산 수행
        if (MoveInput.sqrMagnitude > 0.1f && _lastMoveInput.sqrMagnitude > 0.1f)
        {
            // 두 벡터의 방향 일치도 계산 (내적값: 1이면 완전 일치, 0이면 직각, -1이면 정반대)
            float dot = Vector2.Dot(MoveInput.normalized, _lastMoveInput.normalized);

            // 유저가 가고자 하는 방향을 의도적으로 확 바꿨을 때
            // (지정된 각도 임계값보다 일치도가 낮아졌을 때)
            if (dot < _directionChangeThreshold)
            {
                _canSwitchTarget = false;
                OnSoftLockOnSwitchPerformed?.Invoke(MoveInput);
                StartCoroutine(ResetFlipCooldown());
            }
        }
        // 만약 완전 대기 상태(입력 없음)에서
        // 최초로 방향키를 탁 누르는 순간에도 즉시 타겟을 잡아주기 위한 예외 처리
        else if (MoveInput.sqrMagnitude > 0.1f && _lastMoveInput.sqrMagnitude <= 0.1f)
        {
            _canSwitchTarget = false;
            OnSoftLockOnSwitchPerformed?.Invoke(MoveInput);
            StartCoroutine(ResetFlipCooldown());
        }
    }

    /// <summary>
    /// Chain QTE 전용 입력 모드 진입
    /// 일반 입력은 즉시 차단하고 QTE 입력은 지연 활성화
    /// </summary>
    public void EnterChainQTEInputMode()
    {
        if (_isChainQTEInputActive) return;

        _isChainQTEInputActive = true;

        // Hero·PartySwapManager 일반 전투 입력 즉시 차단
        _controls.GamePlay.Disable();

        // 기존 QTE 입력도 잠금 상태로 초기화
        _controls.ChainQTE.Disable();

        MoveInput = Vector2.zero;
        _lastMoveInput = Vector2.zero;

        if (_chainQTEInputCoroutine != null)
        {
            StopCoroutine(_chainQTEInputCoroutine);
        }

        _chainQTEInputCoroutine =
            StartCoroutine(EnableChainQTEInputRoutine());
    }

    /// <summary>
    /// 입력 가드 시간 이후 Chain QTE 입력 활성화
    /// </summary>
    private IEnumerator EnableChainQTEInputRoutine()
    {
        yield return new WaitForSecondsRealtime(_chainQTEInputDelay);

        // 대기 중 Selecting이 종료된 경우 활성화 차단
        if (!_isChainQTEInputActive)
        {
            _chainQTEInputCoroutine = null;
            yield break;
        }

        _controls.ChainQTE.Enable();
        _chainQTEInputCoroutine = null;
    }

    public void ExitChainQTEInputMode()
    {
        if (!_isChainQTEInputActive) return;

        if (_chainQTEInputCoroutine != null)
        {
            StopCoroutine(_chainQTEInputCoroutine);
            _chainQTEInputCoroutine = null;
        }

        // QTE 입력 차단 후 일반 입력 복구
        _controls.ChainQTE.Disable();
        _controls.GamePlay.Enable();

        _isChainQTEInputActive = false;
    }
}