using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ChainQTEHUDController : MonoBehaviour
{
    [Header("QTE Root")]
    [SerializeField] private GameObject _chainQTERoot;

    [Header("Timer UI")]
    [SerializeField] private TMP_Text _timerText;
    [SerializeField] private Slider _timerSlider;

    [Header("Candidate Slots")]
    [SerializeField] private ChainQTECandidateSlot _prevCandidateSlot;
    [SerializeField] private ChainQTECandidateSlot _nextCandidateSlot;

    [Header("Cancel Guide Text")]
    [SerializeField] private TMP_Text _cancelGuideText;

    private ChainAttackManager _chainAttackManager;

    private void Start()
    {
        _chainAttackManager = ChainAttackManager.Instance;

        if (_chainAttackManager == null)
        {
            Debug.LogError("[ChainQTEHUDController] ChainAttackManager 없음");
            enabled = false;
            return;
        }

        SubscribeEvents();
        SyncInitialState();
    }

    private void OnDestroy()
    {
        UnsubscribeEvents();
    }

    private void Update()
    {
        if (_chainAttackManager == null) return;
        if (!_chainAttackManager.IsSelectionOpen) return;

        UpdateTimerUI();
    }

    /// <summary>
    /// ChainAttackManager 이벤트 구독
    /// </summary>
    private void SubscribeEvents()
    {
        _chainAttackManager.OnSelectionOpened += HandleSelectionOpened;
        _chainAttackManager.OnSelectionClosed += HandleSelectionClosed;
    }

    /// <summary>
    /// ChainAttackManager 이벤트 구독 해제
    /// </summary>
    private void UnsubscribeEvents()
    {
        if (_chainAttackManager == null) return;

        _chainAttackManager.OnSelectionOpened -= HandleSelectionOpened;
        _chainAttackManager.OnSelectionClosed -= HandleSelectionClosed;
    }

    /// <summary>
    /// HUD 초기 상태와 ChainAttackManager 상태 동기화
    /// </summary>
    private void SyncInitialState()
    {
        if (_chainAttackManager.IsSelectionOpen)
        {
            HandleSelectionOpened();
            return;
        }

        HideQTE();
    }

    /// <summary>
    /// Selecting Phase 진입 시 QTE UI 개방
    /// </summary>
    private void HandleSelectionOpened()
    {
        if (_chainQTERoot == null) return;

        _chainQTERoot.SetActive(true);

        _prevCandidateSlot?.BindHero(
            _chainAttackManager.PrevHeroCandidate,
            "Mouse 1"
        );

        _nextCandidateSlot?.BindHero(
            _chainAttackManager.NextHeroCandidate,
            "Mouse 2"
        );

        if (_cancelGuideText != null)
        {
            _cancelGuideText.text = "Cancel : Mouse 3";
        }

        InitializeTimerUI();
    }

    /// <summary>
    /// Selecting Phase 종료 시 QTE UI 종료
    /// </summary>
    private void HandleSelectionClosed()
    {
        HideQTE();
    }

    /// <summary>
    /// QTE 타이머 최소값/최대값 세팅 및 초기화
    /// </summary>
    private void InitializeTimerUI()
    {
        if (_timerSlider != null)
        {
            _timerSlider.minValue = 0f;
            _timerSlider.maxValue = 1f;
        }

        UpdateTimerUI();
    }

    /// <summary>
    /// QTE UI 전체 초기화 및 비활성화
    /// </summary>
    private void HideQTE()
    {
        _prevCandidateSlot?.Clear();
        _nextCandidateSlot?.Clear();

        if (_timerSlider != null)
        {
            _timerSlider.value = 0f;
        }

        if (_timerText != null)
        {
            _timerText.text = string.Empty;
        }

        if (_chainQTERoot != null)
        {
            _chainQTERoot.SetActive(false);
        }
    }

    /// <summary>
    /// ChainAttackManager 타이머 값 UI 반영
    /// </summary>
    private void UpdateTimerUI()
    {
        if (_timerSlider != null)
        {
            _timerSlider.value = _chainAttackManager.SelectionTimeRatio;
        }

        if (_timerText != null)
        {
            float remainingTime = Mathf.Max(0f, _chainAttackManager.CurrentSelectionTime);

            int totalHundredths = Mathf.CeilToInt(remainingTime * 100f);

            int seconds = totalHundredths / 100;
            int hundredths = totalHundredths % 100;

            _timerText.text = $"00:{seconds:00}:{hundredths:00}";
        }
    }
}