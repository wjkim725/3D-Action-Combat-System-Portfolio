using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class SettingsUI : MonoBehaviour
{
    [Header("Volume Sliders")]
    [SerializeField] private Slider _masterVolumeSlider;
    [SerializeField] private Slider _sfxVolumeSlider;
    [SerializeField] private Slider _bgmVolumeSlider;

    [Header("Sensitivity")]
    [SerializeField] private Slider _mouseSensitivitySlider;

    [Header("Value Labels")]
    [SerializeField] private Text _masterVolumeText;
    [SerializeField] private Text _sfxVolumeText;
    [SerializeField] private Text _bgmVolumeText;
    [SerializeField] private Text _mouseSensitivityText;

    [Header("Display")]
    [SerializeField] private Dropdown _resolutionDropdown;
    [SerializeField] private Dropdown _windowModeDropdown;

    [Header("Buttons")]
    [SerializeField] private Button _applyButton;
    [SerializeField] private Button _resetButton;
    [SerializeField] private Button _closeButton;

    [Header("Session Buttons")]
    [SerializeField] private bool _showSessionButtons;
    [SerializeField] private GameObject _sessionButtons;
    [SerializeField] private Button _returnToMainMenuButton;
    [SerializeField] private Button _quitGameButton;
    [SerializeField] private GameSessionController _gameSessionController;

    [Header("Session Confirmation")]
    [SerializeField] private GameObject _sessionConfirmationPopup;
    [SerializeField] private Text _sessionConfirmationText;
    [SerializeField] private Button _sessionConfirmButton;
    [SerializeField] private Button _sessionCancelButton;
    [SerializeField] private CanvasGroup _settingsContentGroup;

    private enum SessionAction { None, ReturnToMainMenu, QuitGame }
    private SessionAction _pendingSessionAction;
    private GameObject _sessionReturnSelection;

    private SettingsManager _manager;
    private SettingsManager.SettingsData _editingSettings;
    private bool _initialized;

    #region [초기화와 이벤트 연결]

    private void Start()
    {
        _manager = SettingsManager.Instance;
        if (_manager == null || _masterVolumeSlider == null || _sfxVolumeSlider == null ||
            _bgmVolumeSlider == null || _mouseSensitivitySlider == null ||
            _masterVolumeText == null || _sfxVolumeText == null || _bgmVolumeText == null ||
            _mouseSensitivityText == null || _resolutionDropdown == null ||
            _windowModeDropdown == null || _applyButton == null || _resetButton == null || _closeButton == null)
        {
            Debug.LogError("SettingsUI: SettingsManager 또는 Inspector UI 참조가 누락됨.", this);
            enabled = false;
            return;
        }

        ConfigureSlider(_masterVolumeSlider, 0f, 1f);
        ConfigureSlider(_sfxVolumeSlider, 0f, 1f);
        ConfigureSlider(_bgmVolumeSlider, 0f, 1f);
        ConfigureSlider(_mouseSensitivitySlider, 0.1f, 3f);

        List<string> options = new();
        foreach (Vector2Int size in _manager.Resolutions) options.Add($"{size.x} × {size.y}");
        _resolutionDropdown.ClearOptions();
        _resolutionDropdown.AddOptions(options);
        _windowModeDropdown.ClearOptions();
        _windowModeDropdown.AddOptions(new List<string> { "창 모드", "전체화면" });

        _masterVolumeSlider.onValueChanged.AddListener(HandleSliderChanged);
        _sfxVolumeSlider.onValueChanged.AddListener(HandleSliderChanged);
        _bgmVolumeSlider.onValueChanged.AddListener(HandleSliderChanged);
        _mouseSensitivitySlider.onValueChanged.AddListener(HandleSliderChanged);
        _resolutionDropdown.onValueChanged.AddListener(HandleDisplayChanged);
        _windowModeDropdown.onValueChanged.AddListener(HandleDisplayChanged);
        _applyButton.onClick.AddListener(Apply);
        _resetButton.onClick.AddListener(ResetDefaults);
        _closeButton.onClick.AddListener(Close);
        InitializeSessionButtons();
        _initialized = true;
        BeginEditing();
    }

    private void OnEnable()
    {
        if (_initialized && _manager != null) BeginEditing();
    }

    private void OnDisable()
    {
        ResetSessionConfirmation(false);
        if (SettingsManager.Instance != null) SettingsManager.Instance.CompleteSettingsClose();
    }

    private void OnDestroy()
    {
        if (!_initialized) return;
        if (_masterVolumeSlider != null) _masterVolumeSlider.onValueChanged.RemoveListener(HandleSliderChanged);
        if (_sfxVolumeSlider != null) _sfxVolumeSlider.onValueChanged.RemoveListener(HandleSliderChanged);
        if (_bgmVolumeSlider != null) _bgmVolumeSlider.onValueChanged.RemoveListener(HandleSliderChanged);
        if (_mouseSensitivitySlider != null) _mouseSensitivitySlider.onValueChanged.RemoveListener(HandleSliderChanged);
        if (_resolutionDropdown != null) _resolutionDropdown.onValueChanged.RemoveListener(HandleDisplayChanged);
        if (_windowModeDropdown != null) _windowModeDropdown.onValueChanged.RemoveListener(HandleDisplayChanged);
        if (_applyButton != null) _applyButton.onClick.RemoveListener(Apply);
        if (_resetButton != null) _resetButton.onClick.RemoveListener(ResetDefaults);
        if (_closeButton != null) _closeButton.onClick.RemoveListener(Close);
        if (_returnToMainMenuButton != null) _returnToMainMenuButton.onClick.RemoveListener(HandleReturnToMainMenu);
        if (_quitGameButton != null) _quitGameButton.onClick.RemoveListener(HandleQuitGame);
        if (_sessionConfirmButton != null) _sessionConfirmButton.onClick.RemoveListener(ConfirmSessionAction);
        if (_sessionCancelButton != null) _sessionCancelButton.onClick.RemoveListener(CancelSessionConfirmation);
    }

    /// <summary>
    /// 슬라이더의 입력 단위와 소수점 사용 범위를 통일함.
    /// </summary>
    private void ConfigureSlider(Slider slider, float min, float max)
    {
        slider.wholeNumbers = false;
        slider.minValue = min;
        slider.maxValue = max;
    }

    #endregion

    #region [설정 편집]

    /// <summary>
    /// 마지막 확정값을 편집용으로 복사하고 UI를 동기화함.
    /// </summary>
    private void BeginEditing()
    {
        _editingSettings = _manager.SavedSettings;
        RefreshUI();
    }

    /// <summary>
    /// 모든 슬라이더 입력을 편집값에 반영하고 미리보기를 요청함.
    /// </summary>
    private void HandleSliderChanged(float value)
    {
        _editingSettings.MasterVolume = _masterVolumeSlider.value;
        _editingSettings.SFXVolume = _sfxVolumeSlider.value;
        _editingSettings.BGMVolume = _bgmVolumeSlider.value;
        _editingSettings.MouseSensitivity = _mouseSensitivitySlider.value;
        _manager.ApplyPreview(_editingSettings);
        RefreshValueLabels();
    }

    /// <summary>
    /// 화면 선택값만 보관하며 실제 화면 변경은 적용 버튼까지 보류함.
    /// </summary>
    private void HandleDisplayChanged(int value)
    {
        Vector2Int size = _manager.Resolutions[_resolutionDropdown.value];
        _editingSettings.Width = size.x;
        _editingSettings.Height = size.y;
        _editingSettings.FullScreen = _windowModeDropdown.value == 1;
    }

    /// <summary>
    /// 편집값을 확정하고 저장한 뒤 보정된 설정을 UI에 반영함.
    /// </summary>
    public void Apply()
    {
        if (!_initialized || _manager == null) return;
        _manager.ApplySettings(_editingSettings);
        BeginEditing();
    }

    /// <summary>
    /// 기본값을 편집 UI와 미리보기에 반영하며 저장은 적용 버튼까지 보류함.
    /// </summary>
    public void ResetDefaults()
    {
        if (!_initialized || _manager == null) return;
        _editingSettings = _manager.GetDefaultSettings();
        _manager.ApplyPreview(_editingSettings);
        RefreshUI();
    }

    /// <summary>
    /// 미확정 설정을 취소하고 이 컴포넌트가 부착된 SettingsCanvas를 닫음.
    /// </summary>
    public void Close()
    {
        if (SettingsManager.Instance != null) SettingsManager.Instance.CloseSettings();
    }

    #endregion

    #region [세션 버튼과 확인창]

    /// <summary>
    /// 표시 옵션과 필수 참조를 확인하고 세션 버튼 이벤트를 연결함.
    /// </summary>
    private void InitializeSessionButtons()
    {
        if (_sessionButtons != null) _sessionButtons.SetActive(_showSessionButtons);
        ResetSessionConfirmation(false);
        if (!_showSessionButtons) return;
        if (_sessionButtons == null || _returnToMainMenuButton == null || _quitGameButton == null ||
            _gameSessionController == null || _sessionConfirmationPopup == null ||
            _sessionConfirmationText == null || _sessionConfirmButton == null ||
            _sessionCancelButton == null || _settingsContentGroup == null)
        {
            Debug.LogError("SettingsUI: 세션 버튼 또는 GameSessionController 참조가 누락됨.", this);
            if (_sessionButtons != null) _sessionButtons.SetActive(false);
            return;
        }
        _returnToMainMenuButton.onClick.AddListener(HandleReturnToMainMenu);
        _quitGameButton.onClick.AddListener(HandleQuitGame);
        _sessionConfirmButton.onClick.AddListener(ConfirmSessionAction);
        _sessionCancelButton.onClick.AddListener(CancelSessionConfirmation);
    }

    /// <summary>
    /// 메인메뉴 복귀 확인을 요청함.
    /// </summary>
    private void HandleReturnToMainMenu()
    {
        OpenSessionConfirmation(SessionAction.ReturnToMainMenu, _returnToMainMenuButton.gameObject);
    }

    /// <summary>
    /// 게임 종료 확인을 요청함.
    /// </summary>
    private void HandleQuitGame()
    {
        OpenSessionConfirmation(SessionAction.QuitGame, _quitGameButton.gameObject);
    }

    /// <summary>
    /// 요청한 세션 동작을 보관하고 취소 버튼을 기본 선택함.
    /// </summary>
    private void OpenSessionConfirmation(SessionAction action, GameObject returnSelection)
    {
        if (!_showSessionButtons || _gameSessionController == null ||
            _gameSessionController.IsTransitioning || _pendingSessionAction != SessionAction.None) return;
        _pendingSessionAction = action;
        _sessionReturnSelection = returnSelection;
        _sessionConfirmationText.text = action == SessionAction.ReturnToMainMenu
            ? "현재 전투를 종료하고 메인메뉴로 돌아가시겠습니까?"
            : "게임을 종료하시겠습니까?";
        _settingsContentGroup.interactable = false;
        _settingsContentGroup.blocksRaycasts = false;
        _sessionConfirmationPopup.SetActive(true);
        if (EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(_sessionCancelButton.gameObject);
    }

    /// <summary>
    /// 확인창이 열린 경우에만 취소하고 처리 여부를 반환함.
    /// </summary>
    public bool TryCancelSessionConfirmation()
    {
        if (_pendingSessionAction == SessionAction.None) return false;
        CancelSessionConfirmation();
        return true;
    }

    /// <summary>
    /// 설정창을 유지하면서 세션 확인만 취소함.
    /// </summary>
    private void CancelSessionConfirmation()
    {
        ResetSessionConfirmation(true);
    }

    /// <summary>
    /// 확인창과 대기 동작을 초기화하고 요청 시 이전 버튼 선택을 복원함.
    /// </summary>
    private void ResetSessionConfirmation(bool restoreSelection)
    {
        _pendingSessionAction = SessionAction.None;
        if (_sessionConfirmationPopup != null) _sessionConfirmationPopup.SetActive(false);
        if (_settingsContentGroup != null)
        {
            _settingsContentGroup.interactable = true;
            _settingsContentGroup.blocksRaycasts = true;
        }
        if (restoreSelection && EventSystem.current != null && _sessionReturnSelection != null)
            EventSystem.current.SetSelectedGameObject(_sessionReturnSelection);
        _sessionReturnSelection = null;
    }

    /// <summary>
    /// 대기 중인 요청을 한 번 소비하고 세션 컨트롤러에 실행을 요청함.
    /// </summary>
    private void ConfirmSessionAction()
    {
        if (_gameSessionController == null || _gameSessionController.IsTransitioning) return;
        SessionAction action = _pendingSessionAction;
        ResetSessionConfirmation(true);
        if (action == SessionAction.ReturnToMainMenu) _gameSessionController.ReturnToMainMenu();
        else if (action == SessionAction.QuitGame) _gameSessionController.QuitGame();
    }

    #endregion

    #region [화면 갱신]

    /// <summary>
    /// 변경 이벤트를 재발생시키지 않고 편집값으로 UI를 갱신함.
    /// </summary>
    private void RefreshUI()
    {
        _masterVolumeSlider.SetValueWithoutNotify(_editingSettings.MasterVolume);
        _sfxVolumeSlider.SetValueWithoutNotify(_editingSettings.SFXVolume);
        _bgmVolumeSlider.SetValueWithoutNotify(_editingSettings.BGMVolume);
        _mouseSensitivitySlider.SetValueWithoutNotify(_editingSettings.MouseSensitivity);
        for (int i = 0; i < _manager.Resolutions.Count; i++)
        {
            Vector2Int size = _manager.Resolutions[i];
            if (size.x == _editingSettings.Width && size.y == _editingSettings.Height)
            {
                _resolutionDropdown.SetValueWithoutNotify(i);
                break;
            }
        }
        _windowModeDropdown.SetValueWithoutNotify(_editingSettings.FullScreen ? 1 : 0);
        RefreshValueLabels();
    }

    /// <summary>
    /// 볼륨은 백분율로, 감도는 기본 감도 대비 배율로 표시함.
    /// </summary>
    private void RefreshValueLabels()
    {
        _masterVolumeText.text = $"{Mathf.RoundToInt(_editingSettings.MasterVolume * 100f)}%";
        _sfxVolumeText.text = $"{Mathf.RoundToInt(_editingSettings.SFXVolume * 100f)}%";
        _bgmVolumeText.text = $"{Mathf.RoundToInt(_editingSettings.BGMVolume * 100f)}%";
        _mouseSensitivityText.text = _editingSettings.MouseSensitivity.ToString("0.0");
    }

    #endregion
}
