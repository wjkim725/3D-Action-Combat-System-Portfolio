using System;
using System.Collections.Generic;
using UnityEngine;

public class ChainAttackManager : MonoBehaviour
{
    public static ChainAttackManager Instance { get; private set; }

    public event Action OnSelectionOpened;
    public event Action OnSelectionClosed;

    public ChainAttackPhase CurrentPhase { get; private set; } = ChainAttackPhase.Inactive;
    public bool IsSelectionOpen => CurrentPhase == ChainAttackPhase.Selecting;


    #region [Selecting Phase 장부]

    // Chain Attack 피격 대상
    public Enemy ChainTarget { get; private set; }

    // Chain Attack QTE를 발동한 Hero
    public Hero TriggerHero { get; private set; }

    // 정방향·역방향 QTE 후보
    public Hero NextHeroCandidate { get; private set; }
    public Hero PrevHeroCandidate { get; private set; }

    // 현재 연속 Chain 과정에서 실행한 Hero 기록
    private readonly HashSet<Hero> _usedHeroes = new();

    #endregion


    #region [Executing Phase 장부]

    // 현재 Chain Attack 실행 대상으로 선택된 Hero
    public Hero CurrentChainHero { get; private set; }

    #endregion


    [Header("Selection Timer Settings")]
    [SerializeField, Min(0.1f)] private float _selectionDuration = 2.4f;
    private float _currentSelectionTime;

    public float SelectionDuration => _selectionDuration;
    public float CurrentSelectionTime => _currentSelectionTime;
    public float SelectionTimeRatio =>
        _selectionDuration > 0f ? Mathf.Clamp01(_currentSelectionTime / _selectionDuration) : 0f;




    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    private void OnDestroy()
    {
        UnsubscribeInputEvents();
        UnbindChainTarget();

        // Selecting Phase에서 씬이 끝나는 경우 예외처리 
        if (Instance == this)
        {
            CombatPresentationManager.Instance?.EndChainSelectionPresentation();

            InputManager.Instance?.ExitChainQTEInputMode();

            Instance = null;
        }
    }

    private void Start()
    {
        if (InputManager.Instance == null)
        {
            Debug.LogError("InputManager Instance Error");
            return;
        }

        SubscribeInputEvents();
    }

    private void Update()
    {
        UpdateSelectionTimer();
    }

    #region [Selecting Phase 메서드 영역]

    /// <summary>
    /// Chain Attack Selecting Phase 진입 시도
    /// </summary>
    public bool TryOpenChainSelection(Enemy target, Hero triggerHero)
    {
        if (!CanOpenChainSelection(target, triggerHero)) return false;

        // TriggerHero 등록
        TriggerHero = triggerHero;

        // QTE 후보 재정립
        if (!TryInitializeHeroCandidates())
        {
            // ChainAttack 시전 가능 캐릭터가 없으면 장부 비우고 Inactive
            ResetChainSession();
            return false;
        }

        // QTE 기회 소비 - Enemy.Groggy.ConsumedChainAttackCount++
        if (!target.Groggy.TryConsumeChainAttackCount())
        {
            // Enemy.Groggy.CanConsumeChainAttack 0이면 장부 비우고 Inactive
            ResetChainSession();
            return false;
        }

        // 기존 ChainTarget 이벤트 해제 후 새 대상 ChainTarget 등록
        BindChainTarget(target);

        // Selecting Phase 진입 + 타이머 / 연출 시작
        EnterSelectionPhase();

        Debug.Log(
            $"[Chain QTE 개방] 대상: {target.name} | " +
            $"역방향: {GetHeroName(PrevHeroCandidate)} | " +
            $"정방향: {GetHeroName(NextHeroCandidate)}"
        );

        return true;
    }

    /// <summary>
    /// Chain QTE 개방 기본 조건 검사
    /// </summary>
    private bool CanOpenChainSelection(Enemy target, Hero triggerHero)
    {
        // Selection QTE 중에는 개방 X
        if (IsSelectionOpen) return false;

        // Executing 중 타겟이 바뀌는 상황 차단 <- 고민필요
        if (CurrentPhase == ChainAttackPhase.Executing && ChainTarget != target)
        {
            return false;
        }

        if (target == null || triggerHero == null) return false;
        if (target.Groggy == null) return false;
        if (triggerHero.Status == null || triggerHero.Status.IsDead) return false;
        if (!target.Groggy.CanConsumeChainAttack) return false;

        return true;
    }

    /// <summary>
    /// 정방향·역방향 QTE 후보 초기화
    /// 로직 오류있나? 찝찝함 추후 리팩토링 
    /// </summary>
    private bool TryInitializeHeroCandidates()
    {
        NextHeroCandidate = null;
        PrevHeroCandidate = null;

        PartyStateManager partyStateManager = PartyStateManager.Instance;

        if (partyStateManager == null) return false;

        Hero nextHero = partyStateManager.GetNextAliveHero();
        Hero prevHero = partyStateManager.GetPrevAliveHero();

        if (CanSelectHero(nextHero))
        {
            NextHeroCandidate = nextHero;
        }

        if (CanSelectHero(prevHero))
        {
            PrevHeroCandidate = prevHero;
        }

        // 2인 파티라면 Next와 Prev가 같은 Hero일 수 있으므로 한쪽만 유지
        if (NextHeroCandidate != null &&
            NextHeroCandidate == PrevHeroCandidate)
        {
            PrevHeroCandidate = null;
        }

        return NextHeroCandidate != null || PrevHeroCandidate != null;
    }

    /// <summary>
    /// Hero의 Chain Attack 선택 가능 여부 검사
    /// </summary>
    private bool CanSelectHero(Hero hero)
    {
        if (hero == null || hero.Status == null) return false;
        if (hero == TriggerHero) return false;
        if (hero.Status.IsDead) return false;
        if (_usedHeroes.Contains(hero)) return false;

        return true;
    }

    /// <summary>
    /// QTE 선택 취소
    /// 현재 Enemy의 소비 횟수는 복구하지 않음
    /// QTE 취소 입력 및 타이머 오버시 호출
    /// </summary>
    public void CancelSelectionPhase()
    {
        if (!IsSelectionOpen) return;

        CloseSelectionPhase();
    }

    /// <summary>
    /// Selecting Phase 종료 
    /// + UI 종료 이벤트 전달
    /// + 전용 연출 종료
    /// </summary>
    private void CloseSelectionPhase()
    {
        bool wasSelectionOpen = IsSelectionOpen;

        if (wasSelectionOpen)
        {
            // QTE 전용 입력 모드 종료 및 일반 전투 입력 모드 전환
            InputManager.Instance?.ExitChainQTEInputMode();

            // Selecting Phase 연출 종료
            CombatPresentationManager.Instance?.EndChainSelectionPresentation();
        }

        ResetChainSession();

        if (wasSelectionOpen)
        {
            // ChainQTEController 이벤트 송신
            OnSelectionClosed?.Invoke();
        }
    }

    /// <summary>
    /// Chain 대상의 그로기 종료 처리
    /// </summary>
    private void HandleChainTargetGroggyEnd()
    {
        /// Executing 중 그로기 종료
        /// → 현재 ChainAttack 모션은 끝까지 실행
        /// → 추가 QTE만 금지
        /// → 애니메이션 종료 시 전체 Chain 종료
        /// 
        // Executing 일때 예외처리
        if (CurrentPhase == ChainAttackPhase.Executing)
        {
            return;
        }

        CloseSelectionPhase();
    }

    /// <summary>
    /// 현재 Chain 대상의 그로기 종료 이벤트 구독
    /// </summary>
    private void BindChainTarget(Enemy target)
    {
        UnbindChainTarget();

        ChainTarget = target;

        if (ChainTarget?.Groggy != null)
        {
            ChainTarget.Groggy.OnGroggyEnd += HandleChainTargetGroggyEnd;
        }
    }

    /// <summary>
    /// 현재 Chain 대상의 그로기 종료 이벤트 해제
    /// </summary>
    private void UnbindChainTarget()
    {
        if (ChainTarget?.Groggy != null)
        {
            ChainTarget.Groggy.OnGroggyEnd -= HandleChainTargetGroggyEnd;
        }
    }

    /// <summary>
    /// 현재 Chain 세션 전체 장부 초기화 및 Inactive 전환
    /// </summary>
    private void ResetChainSession()
    {
        UnbindChainTarget();

        _usedHeroes.Clear();

        ChainTarget = null;
        TriggerHero = null;
        NextHeroCandidate = null;
        PrevHeroCandidate = null;

        CurrentChainHero = null;

        _currentSelectionTime = 0f;
        CurrentPhase = ChainAttackPhase.Inactive;
    }

    /// <summary>
    /// Hero 오브젝트 이름 반환
    /// 로그 디버깅용 / 추후 삭제
    /// </summary>
    private string GetHeroName(Hero hero)
    {
        return hero != null ? hero.name : "없음";
    }

    /// <summary>
    /// Update - Selecting Phase 에서 실행
    /// _selectionDuration 시간 초과 시 CancelSelectionPhase
    /// </summary>
    private void UpdateSelectionTimer()
    {
        if (!IsSelectionOpen) return;

        _currentSelectionTime -= Time.unscaledDeltaTime;

        if (_currentSelectionTime <= 0f)
        {
            _currentSelectionTime = 0f;
            CancelSelectionPhase();
        }
    }

    /// <summary>
    /// Selecting Phase 진입 및 타이머·연출 시작
    /// </summary>
    private void EnterSelectionPhase()
    {
        // QTE Timer On
        _currentSelectionTime = _selectionDuration;
        
        CurrentPhase = ChainAttackPhase.Selecting;

        // 일반 전투 입력 차단 후 QTE 전용 입력 활성화
        InputManager.Instance?.EnterChainQTEInputMode();

        // Seleting 연출 시작
        CombatPresentationManager.Instance?.StartChainSelectionPresentation(ChainTarget, TriggerHero);

        // UI Controller 들에게 신호 송신
        OnSelectionOpened?.Invoke();
    }

    #endregion


    #region [Executing Phase 메서드 영역]

    /// <summary>
    /// 선택된 Hero를 Chain 실행 Hero로 확정 및
    /// PartySwapManager 에게 ChainSwap 요청
    /// </summary>
    private void SelectHeroCandidate(Hero selectedHero)
    {
        if (!IsSelectionOpen) return;
        if (!CanSelectHero(selectedHero)) return;

        PartySwapManager partySwapManager = PartySwapManager.Instance;

        if (partySwapManager == null)
        {
            Debug.LogError("PartySwapManager Instance Error");
            return;
        }

        // PartySwapManager 에게 ChainSwap 요청하되,
        // 실패시 QTE Selection Phase 닫고 InActive로 전환
        if (!partySwapManager.TryExecuteChainSwap(selectedHero, ChainTarget))
        {
            Debug.LogError($"[Chain 선택 실패] {selectedHero.name}");

            CloseSelectionPhase();
            return;
        }

        // Chain 교대 성공 후 실행 Hero 장부 등록
        CurrentChainHero = selectedHero;
        _usedHeroes.Add(selectedHero);

        EnterExecutionPhase();

        Debug.Log($"[Chain Hero 선택] {selectedHero.name}");
    }

    /// <summary>
    /// Selecting 이후 Executing Phase로 전환
    /// </summary>
    private void EnterExecutionPhase()
    {
        _currentSelectionTime = 0f;
        CurrentPhase = ChainAttackPhase.Executing;

        // QTE 전용 입력 종료 및 일반 입력 복구
        InputManager.Instance?.ExitChainQTEInputMode();
        // QTE Selecting 연출 종료
        CombatPresentationManager.Instance?.EndChainSelectionPresentation();

        NextHeroCandidate = null;
        PrevHeroCandidate = null;

        OnSelectionClosed?.Invoke();
    }

    /// <summary>
    /// 현재 ChainAttack Executing 종료 처리
    /// </summary>
    public void CompleteChainExecution(Hero completedHero)
    {
        if (CurrentPhase != ChainAttackPhase.Executing) return;
        if (CurrentChainHero != completedHero) return;

        ResetChainSession();
    }

    #endregion


    #region [Input Handler 이벤트 구독 영역]

    private void SubscribeInputEvents()
    {
        InputManager input = InputManager.Instance;

        input.OnChainPrevPerformed += HandleChainPrevPerformedInput;
        input.OnChainNextPerformed += HandleChainNextPerformedInput;
        input.OnChainCancelPerformed += HandleChainCancelPerformedInput;
    }

    private void UnsubscribeInputEvents()
    {
        if (InputManager.Instance == null) return;

        InputManager input = InputManager.Instance;

        input.OnChainPrevPerformed -= HandleChainPrevPerformedInput;
        input.OnChainNextPerformed -= HandleChainNextPerformedInput;
        input.OnChainCancelPerformed -= HandleChainCancelPerformedInput;
    }

    #endregion


    #region [QTE 전용 Input Handler 영역]

    /// <summary>
    /// 좌클릭 - 역방향 Chain 후보 선택
    /// </summary>
    private void HandleChainPrevPerformedInput()
    {
        if (!IsSelectionOpen) return;
        if (PrevHeroCandidate == null) return;

        Debug.Log("ChainAttack : 역방향 입력 수신");

        SelectHeroCandidate(PrevHeroCandidate);
    }

    /// <summary>
    /// 우클릭 - 정방향 Chain 후보 선택
    /// </summary>
    private void HandleChainNextPerformedInput()
    {
        if (!IsSelectionOpen) return;
        if (NextHeroCandidate == null) return;

        Debug.Log("ChainAttack : 정방향 입력 수신");

        SelectHeroCandidate(NextHeroCandidate);
    }

    /// <summary>
    /// 휠 클릭 - Chain QTE 취소
    /// </summary>
    private void HandleChainCancelPerformedInput()
    {
        if (!IsSelectionOpen) return;

        Debug.Log("ChainAttack : QTE 취소 입력 수신");

        CancelSelectionPhase();
    }

    #endregion
}