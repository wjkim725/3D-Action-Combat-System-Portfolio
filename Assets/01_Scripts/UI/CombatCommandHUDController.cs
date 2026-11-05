using UnityEngine;

/// <summary>
/// 조작 Hero의 전투 자원과 지원 상태를 커맨드 HUD에 연결함.
/// </summary>
[DisallowMultipleComponent]
public class CombatCommandHUDController : MonoBehaviour
{
    [Header("HUD Root")]
    [SerializeField] private GameObject _commandHUDRoot;

    [Header("Command Slots")]
    [SerializeField] private CombatCommandSlot _attackSlot;
    [SerializeField] private CombatCommandSlot _dodgeSlot;
    [SerializeField] private CombatCommandSlot _skillSlot;
    [SerializeField] private CombatCommandSlot _ultimateSlot;
    [SerializeField] private CombatCommandSlot _swapNextSlot;

    [Header("Quick Assist Presentation")]
    [SerializeField] private QuickAssistHUDView _quickAssistView;

    private PartyStateManager _partyStateManager;
    private ChainAttackManager _chainAttackManager;
    private QuickAssistManager _quickAssistManager;
    private Hero _currentHero;
    private bool _hasStarted;

    private void Start()
    {
        _hasStarted = true;
        ConnectManagers();
    }

    private void OnEnable()
    {
        if (_hasStarted) ConnectManagers();
    }

    private void OnDisable()
    {
        DisconnectManagers();
        BindHero(null);
    }

    private void LateUpdate()
    {
        RefreshCommands();
    }

    #region [데이터 연결]
    /// <summary>
    /// 초기화가 끝난 Manager에 이벤트를 연결하고 현재 Hero를 표시함.
    /// </summary>
    private void ConnectManagers()
    {
        DisconnectManagers();
        _partyStateManager = PartyStateManager.Instance;
        _chainAttackManager = ChainAttackManager.Instance;
        _quickAssistManager = QuickAssistManager.Instance;
        if (_partyStateManager == null)
        {
            Debug.LogError("[CombatCommandHUD] PartyStateManager를 찾을 수 없습니다.", this);
            enabled = false;
            return;
        }

        _partyStateManager.OnCurrentHeroChanged += HandleCurrentHeroChanged;
        if (_quickAssistManager != null)
            _quickAssistManager.OnWindowClosed += HandleAssistWindowClosed;
        if (_chainAttackManager != null)
        {
            _chainAttackManager.OnSelectionOpened += HandleChainSelectionChanged;
            _chainAttackManager.OnSelectionClosed += HandleChainSelectionChanged;
        }
        BindHero(_partyStateManager.CurrentHero);
    }

    /// <summary>
    /// HUD 비활성화 시 구독을 해제하여 중복 갱신을 방지함.
    /// </summary>
    private void DisconnectManagers()
    {
        if (_partyStateManager != null)
            _partyStateManager.OnCurrentHeroChanged -= HandleCurrentHeroChanged;
        if (_chainAttackManager != null)
        {
            _chainAttackManager.OnSelectionOpened -= HandleChainSelectionChanged;
            _chainAttackManager.OnSelectionClosed -= HandleChainSelectionChanged;
        }
        _partyStateManager = null;
        _chainAttackManager = null;
        if (_quickAssistManager != null)
            _quickAssistManager.OnWindowClosed -= HandleAssistWindowClosed;
        _quickAssistManager = null;
    }

    /// <summary>
    /// 확정된 조작 Hero 변경을 수신함.
    /// </summary>
    private void HandleCurrentHeroChanged(Hero hero)
    {
        BindHero(hero);
    }

    /// <summary>
    /// 이전 표시를 초기화한 뒤 새 Hero의 상태를 즉시 표시함.
    /// </summary>
    private void BindHero(Hero hero)
    {
        _currentHero = hero;
        _attackSlot?.ResetPresentation();
        _dodgeSlot?.ResetPresentation();
        _skillSlot?.ResetPresentation();
        _ultimateSlot?.ResetPresentation();
        _swapNextSlot?.ResetPresentation();
        RefreshCommands();
    }
    #endregion

    #region [상태 표시]
    /// <summary>
    /// 연계 선택의 개방·종료 시 커맨드 표시를 동기화함.
    /// </summary>
    private void HandleChainSelectionChanged()
    {
        RefreshCommands();
    }

    /// <summary>
    /// 지원 창의 종료 원인을 전용 연출에 전달함.
    /// </summary>
    private void HandleAssistWindowClosed(QuickAssistCloseReason reason)
    {
        _quickAssistView?.ClosePresentation(reason);
    }

    /// <summary>
    /// 현재 Hero와 기존 전투 시스템의 값을 읽어 HUD만 갱신함.
    /// </summary>
    private void RefreshCommands()
    {
        bool hasHero = _currentHero != null && _currentHero.Status != null && !_currentHero.Status.IsDead;
        bool isSelecting = _chainAttackManager != null && _chainAttackManager.IsSelectionOpen;
        bool visible = hasHero && !isSelecting;
        if (_commandHUDRoot != null && _commandHUDRoot.activeSelf != visible)
            _commandHUDRoot.SetActive(visible);
        if (!visible) return;

        HeroMovement movement = _currentHero.Movement;
        _dodgeSlot?.SetCooldown(movement != null ? movement.DodgeCooldownRemaining : 0f,
            movement != null ? movement.DodgeCooldownDuration : 0f);

        // PartyHUD와 동일하게 다음 스킬 행동의 강특 가능 여부를 표시함.
        float energyRequirement;
        bool isSkillReady = false;
        if (_currentHero.SkillSystem != null)
            _currentHero.SkillSystem.TryGetNextExSkillPreview(out energyRequirement, out isSkillReady);
        _skillSlot?.SetReady(isSkillReady);
        _ultimateSlot?.SetReady(_currentHero.Status.HasEnoughDecibelForUlt);
        RefreshAssist();
    }

    /// <summary>
    /// 지원 방향과 관계없이 현재 빠른 지원 대상을 Space 슬롯에 표시함.
    /// </summary>
    private void RefreshAssist()
    {
        if (_swapNextSlot == null || _partyStateManager == null) return;

        Hero nextHero = _partyStateManager.GetNextAliveHero();
        QuickAssistManager manager = QuickAssistManager.Instance;
        Hero assistHero = manager != null ? manager.AssistHero : null;
        // Space 입력은 열린 지원 창의 방향을 따르므로 해당 방향의 실제 후보를 확인함.
        Hero expectedAssistHero = manager != null && manager.CurrentSwapDirection == SwapDirection.Prev
            ? _partyStateManager.GetPrevAliveHero() : nextHero;
        bool isAssistActive = manager != null && manager.IsWindowOpen &&
            manager.TriggerHero != null &&
            manager.TriggerHero.Status != null && !manager.TriggerHero.Status.IsDead &&
            assistHero != null && assistHero == expectedAssistHero && assistHero.Status != null &&
            !assistHero.Status.IsDead && assistHero.SwapSystem != null;

        _swapNextSlot.SetAvailable(isAssistActive || nextHero != null);
        if (_quickAssistView != null)
        {
            _swapNextSlot.SetReady(false);
            _quickAssistView.SetAssist(isAssistActive ? assistHero : null);
        }
        else
        {
            _swapNextSlot.SetAssistPortrait(isAssistActive ? assistHero.Portrait : null, isAssistActive);
            _swapNextSlot.SetReady(isAssistActive);
        }
    }
    #endregion
}
