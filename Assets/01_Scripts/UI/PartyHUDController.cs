using UnityEngine;

public class PartyHUDController : MonoBehaviour
{
    [Header("Party HUD Root")]
    [SerializeField] private GameObject _partyHUDRoot;

    [Header("Hero HUD Slots")]
    [SerializeField] private HeroHUDSlot _currentHeroSlot;
    [SerializeField] private HeroHUDSlot _nextHeroSlot;
    [SerializeField] private HeroHUDSlot _prevHeroSlot;

    private PartyStateManager _partyStateManager;
    private ChainAttackManager _chainAttackManager;

    private void Start()
    {
        _partyStateManager = PartyStateManager.Instance;

        if (_partyStateManager == null)
        {
            Debug.LogError("[PartyHUDController] PartyStateManager를 찾을 수 없습니다.");
            enabled = false;
            return;
        }

        _partyStateManager.OnCurrentHeroChanged += HandleCurrentHeroChanged;
        RefreshPartySlots();


        _chainAttackManager = ChainAttackManager.Instance;

        if (_chainAttackManager != null)
        {
            _chainAttackManager.OnSelectionOpened += HandleChainSelectionOpened;
            _chainAttackManager.OnSelectionClosed += HandleChainSelectionClosed;

            SetPartyHUDActive(!_chainAttackManager.IsSelectionOpen);
        }
    }

    private void OnDestroy()
    {
        if (_partyStateManager != null)
        {
            _partyStateManager.OnCurrentHeroChanged -= HandleCurrentHeroChanged;
        }

        if (_chainAttackManager != null)
        {
            _chainAttackManager.OnSelectionOpened -= HandleChainSelectionOpened;
            _chainAttackManager.OnSelectionClosed -= HandleChainSelectionClosed;
        }
    }

    /// <summary>
    /// 현재 Hero 변경 신호를 받으면 파티 슬롯 전체를 다시 연결
    /// </summary>
    private void HandleCurrentHeroChanged(Hero currentHero)
    {
        RefreshPartySlots();
    }

    private void RefreshPartySlots()
    {
        if (_partyStateManager == null) return;

        _currentHeroSlot?.BindHero(_partyStateManager.CurrentHero);
        _nextHeroSlot?.BindHero(_partyStateManager.NextHero);
        _prevHeroSlot?.BindHero(_partyStateManager.PrevHero);
    }

    private void HandleChainSelectionOpened()
    {
        SetPartyHUDActive(false);
    }

    private void HandleChainSelectionClosed()
    {
        SetPartyHUDActive(true);
    }

    private void SetPartyHUDActive(bool isActive)
    {
        if (_partyHUDRoot != null)
        {
            _partyHUDRoot.SetActive(isActive);
        }
    }
}