using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 편성 순서를 소유하고 파티 UI와 표시용 Hero 배치를 함께 갱신함.
/// 확정 결과에는 씬 모델 대신 Hero 프리팹을 보관하며 전투 초기화는 외부에서 담당함.
/// </summary>
public class PartyFormationController : MonoBehaviour
{
    [Serializable]
    private class HeroEntry
    {
        public string displayName;
        public Hero prefab;
        public Transform preview;
    }

    [Serializable]
    private class RosterSlot
    {
        public RawImage portrait;
        public Button button;
        public GameObject frame;
        public GameObject selection;
    }

    [Serializable]
    private class PartySlot
    {
        public Button button;
        public RawImage portrait;
        public Text heroName;
        public GameObject frame;
        public GameObject selection;
        public Transform previewAnchor;
    }

    [Header("Formation")]
    [SerializeField] private HeroEntry[] _heroes;
    [SerializeField] private RosterSlot[] _rosterSlots;
    [SerializeField] private PartySlot[] _slots;
    [SerializeField] private int[] _initialParty = { -1, -1, -1 }; // -1은 빈 슬롯임.

    [Header("Navigation")]
    [SerializeField] private Button _backButton;
    [SerializeField] private Button _deployButton;
    [SerializeField] private string _mainMenuSceneName = "MainMenuScene";

    [Header("Deployment")]
    [Tooltip("편성 저장 후 호출함. 전투씬 이동은 이후 세션 전환 담당자와 연결함.")]
    [SerializeField] private UnityEvent _onDeployRequested = new UnityEvent();

    // 씬 전환 동안 프리팹 참조만 유지함. 새 Play 세션과 메뉴 복귀 시 정리함.
    private static Hero[] _confirmedParty;
    private int[] _party;
    private UnityAction[] _heroActions;
    private bool _initialized;
    private bool _isReturningToMenu;

    public int NextEmptySlotIndex => _party != null ? Array.IndexOf(_party, -1) : -1;
    public bool IsPartyFull => _party != null && _party.Length > 0 && NextEmptySlotIndex < 0;
    public bool IsConfirmed { get; private set; }
    public int PartyCount
    {
        get
        {
            if (_party == null) return 0;
            int count = 0;
            foreach (int heroIndex in _party)
                if (heroIndex >= 0) count++;
            return count;
        }
    }
    private bool CanEdit => _initialized && isActiveAndEnabled && !_isReturningToMenu &&
        (!IsConfirmed || _onDeployRequested.GetPersistentEventCount() == 0);

    #region [초기화와 이벤트 연결]

    private void Start()
    {
        if (!HasRequiredReferences())
        {
            Debug.LogError("PartyFormationController: Hero, 슬롯, 미리보기 Anchor와 버튼 참조를 확인해야 함.", this);
            enabled = false;
            return;
        }

        _party = (int[])_initialParty.Clone();
        _heroActions = new UnityAction[_heroes.Length];
        for (int i = 0; i < _heroes.Length; i++)
        {
            int index = i;
            _heroActions[i] = () => SelectHero(index);
            _rosterSlots[i].button.onClick.AddListener(_heroActions[i]);
        }
        _backButton.onClick.AddListener(ReturnToMainMenu);
        _deployButton.onClick.AddListener(ConfirmParty);
        ClearConfirmedParty();
        _initialized = true;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        RefreshFormation();
    }

    private void Update()
    {
        if (CanEdit && Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            ReturnToMainMenu();
    }

    private void OnEnable()
    {
        if (_initialized) RefreshFormation();
    }

    private void OnDisable()
    {
        if (_initialized) SetButtonsInteractable(false);
    }

    private void OnDestroy()
    {
        if (!_initialized) return;
        for (int i = 0; i < _heroes.Length; i++)
            if (_rosterSlots[i].button != null) _rosterSlots[i].button.onClick.RemoveListener(_heroActions[i]);
        if (_backButton != null) _backButton.onClick.RemoveListener(ReturnToMainMenu);
        if (_deployButton != null) _deployButton.onClick.RemoveListener(ConfirmParty);
    }

    /// <summary>
    /// 편성 가능한 프리팹과 중복 없는 기본 순서 및 화면 참조를 검사함.
    /// </summary>
    private bool HasRequiredReferences()
    {
        if (_heroes == null || _rosterSlots == null || _slots == null || _initialParty == null ||
            _rosterSlots.Length != _heroes.Length ||
            _slots.Length == 0 || _heroes.Length < _slots.Length ||
            _initialParty.Length != _slots.Length || _backButton == null || _deployButton == null)
            return false;

        for (int i = 0; i < _heroes.Length; i++)
        {
            RosterSlot roster = _rosterSlots[i];
            if (roster == null || roster.button == null || roster.portrait == null ||
                roster.frame == null || roster.selection == null) return false;
            HeroEntry entry = _heroes[i];
            if (entry == null || entry.prefab == null || entry.prefab.gameObject.scene.IsValid() ||
                entry.prefab.Portrait == null || entry.preview == null || string.IsNullOrWhiteSpace(entry.displayName))
                return false;
            for (int j = 0; j < i; j++)
                if (_heroes[j].prefab == entry.prefab || _heroes[j].preview == entry.preview) return false;
        }

        for (int i = 0; i < _slots.Length; i++)
        {
            PartySlot slot = _slots[i];
            if (slot == null || slot.button == null || slot.portrait == null || slot.heroName == null ||
                slot.frame == null || slot.selection == null || slot.previewAnchor == null ||
                _initialParty[i] < -1 || _initialParty[i] >= _heroes.Length ||
                (_initialParty[i] >= 0 && Array.IndexOf(_initialParty, _initialParty[i]) != i))
                return false;
        }
        return true;
    }

    #endregion

    #region [파티 편성과 화면 갱신]

    /// <summary>
    /// 편성된 Hero는 해당 자리에서 제외하고 미편성 Hero는 가장 왼쪽 빈자리에 추가함.
    /// </summary>
    public void SelectHero(int heroIndex)
    {
        if (!CanEdit || heroIndex < 0 || heroIndex >= _heroes.Length) return;
        int occupiedSlot = Array.IndexOf(_party, heroIndex);
        if (occupiedSlot >= 0)
        {
            _party[occupiedSlot] = -1;
        }
        else
        {
            int emptySlot = NextEmptySlotIndex;
            if (emptySlot < 0) return;
            _party[emptySlot] = heroIndex;
        }
        ResetConfirmation();
        RefreshFormation();
    }

    /// <summary>
    /// 현재 편성 순서의 Hero 프리팹을 반환하며 잘못된 슬롯에는 null을 반환함.
    /// </summary>
    public Hero GetPartyHero(int slotIndex)
    {
        if (!_initialized || slotIndex < 0 || slotIndex >= _party.Length || _party[slotIndex] < 0) return null;
        return _heroes[_party[slotIndex]].prefab;
    }

    /// <summary>
    /// 같은 편성 순서로 UI와 모델 위치를 갱신하며 Animator의 재생 상태를 유지함.
    /// </summary>
    private void RefreshFormation()
    {
        for (int i = 0; i < _heroes.Length; i++)
        {
            bool isSelected = Array.IndexOf(_party, i) >= 0;
            _rosterSlots[i].portrait.texture = _heroes[i].prefab.Portrait;
            _rosterSlots[i].frame.SetActive(!isSelected);
            _rosterSlots[i].selection.SetActive(isSelected);
            _heroes[i].preview.gameObject.SetActive(Array.IndexOf(_party, i) >= 0);
        }

        for (int i = 0; i < _slots.Length; i++)
        {
            PartySlot slot = _slots[i];
            bool isEmpty = _party[i] < 0;
            slot.frame.SetActive(i != NextEmptySlotIndex);
            slot.selection.SetActive(i == NextEmptySlotIndex);
            slot.portrait.enabled = !isEmpty;
            if (isEmpty)
            {
                slot.portrait.texture = null;
                slot.heroName.text = string.Empty;
                continue;
            }
            HeroEntry entry = _heroes[_party[i]];
            slot.portrait.texture = entry.prefab.Portrait;
            slot.heroName.text = entry.displayName;
            entry.preview.SetPositionAndRotation(slot.previewAnchor.position, slot.previewAnchor.rotation);
        }
        SetButtonsInteractable(CanEdit);
    }

    /// <summary>
    /// 컴포넌트 활성 상태와 씬 전환 여부에 맞춰 버튼 입력을 제어함.
    /// </summary>
    private void SetButtonsInteractable(bool interactable)
    {
        foreach (PartySlot slot in _slots)
        {
            // 슬롯은 표시 전용이며 편성 입력은 하단 초상화에서만 받음.
            slot.button.interactable = false;
            slot.button.transition = Selectable.Transition.None;
        }
        foreach (RosterSlot slot in _rosterSlots) slot.button.interactable = interactable;
        _backButton.interactable = interactable;
        _deployButton.interactable = interactable && !IsConfirmed && PartyCount > 0;
    }

    #endregion

    #region [편성 확정과 씬 이동]

    /// <summary>
    /// 빈 슬롯을 제외한 왼쪽 순서로 편성을 확정하고 출전 이벤트를 호출함. 씬 로드와 Hero 생성은 수행하지 않음.
    /// </summary>
    public void ConfirmParty()
    {
        if (!CanEdit || IsConfirmed || PartyCount == 0) return;
        _confirmedParty = new Hero[PartyCount];
        int memberIndex = 0;
        for (int i = 0; i < _party.Length; i++)
        {
            if (_party[i] < 0) continue;
            _confirmedParty[memberIndex++] = GetPartyHero(i);
        }
        IsConfirmed = true;
        SetButtonsInteractable(false);
        _onDeployRequested.Invoke();
        // 전환 담당자를 연결하기 전에는 확정 결과를 보관하되 편집과 메뉴 복귀를 허용함.
        if (_onDeployRequested.GetPersistentEventCount() == 0 && this != null && isActiveAndEnabled)
            SetButtonsInteractable(CanEdit);
    }

    /// <summary>
    /// 씬 전환 실패 또는 편성 변경 시 확정을 취소하고 입력을 복원함.
    /// </summary>
    public void ResetConfirmation()
    {
        IsConfirmed = false;
        ClearConfirmedParty();
        if (_initialized) SetButtonsInteractable(CanEdit);
    }

    /// <summary>
    /// 확정된 프리팹 순서의 복사본을 전달함. 전투씬의 실제 Hero 배열로 직접 사용하지 않음.
    /// </summary>
    public static bool TryGetConfirmedParty(out Hero[] party)
    {
        party = null;
        if (_confirmedParty == null || _confirmedParty.Length == 0) return false;
        foreach (Hero hero in _confirmedParty)
            if (hero == null) return false;
        party = (Hero[])_confirmedParty.Clone();
        return true;
    }

    /// <summary>
    /// Domain Reload 설정과 무관하게 새 실행 세션의 이전 편성을 제거함.
    /// </summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    public static void ClearConfirmedParty()
    {
        _confirmedParty = null;
    }

    /// <summary>
    /// 메인메뉴로 이동하고 로드 요청 실패 시 편성 화면 입력을 복원함.
    /// </summary>
    public void ReturnToMainMenu()
    {
        if (!CanEdit) return;
        if (string.IsNullOrWhiteSpace(_mainMenuSceneName) ||
            !Application.CanStreamedLevelBeLoaded(_mainMenuSceneName))
        {
            Debug.LogError("PartyFormationController: 메인메뉴 씬 등록을 확인해야 함.", this);
            return;
        }

        _isReturningToMenu = true;
        SetButtonsInteractable(false);
        try
        {
            if (SceneManager.LoadSceneAsync(_mainMenuSceneName, LoadSceneMode.Single) != null)
            {
                ClearConfirmedParty();
                return;
            }
        }
        catch (Exception exception)
        {
            Debug.LogError($"PartyFormationController: 메뉴 이동 실패. {exception.Message}", this);
        }
        _isReturningToMenu = false;
        RefreshFormation();
    }

    #endregion
}
