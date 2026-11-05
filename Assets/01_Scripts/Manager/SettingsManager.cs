using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;

[DefaultExecutionOrder(-100)]
public class SettingsManager : MonoBehaviour
{
    [Serializable]
    public struct SettingsData
    {
        public float MasterVolume;
        public float SFXVolume;
        public float BGMVolume;
        public float MouseSensitivity;
        public int Width;
        public int Height;
        public bool FullScreen;
    }

    public static SettingsManager Instance { get; private set; }

    [Header("Audio Mixer")]
    [SerializeField] private AudioMixer _audioMixer;
    // 믹서의 각 Volume을 아래 이름으로 Expose한 뒤 연결해야 함.
    [SerializeField] private string _masterParameter = "MasterVolume";
    [SerializeField] private string _sfxParameter = "SFXVolume";
    [SerializeField] private string _bgmParameter = "BGMVolume";

    [Header("Settings Window")]
    [SerializeField] private SettingsUI _settingsUI;
    [SerializeField] private bool _useGameplayInput = true;     // 메인메뉴 -> false
    private InputManager _inputManager;
    private CombatPresentationManager _combatPresentationManager;
    private bool _isSettingsOpen;
    private CursorLockMode _previousCursorLock;
    private bool _previousCursorVisible;
    public bool IsSettingsOpen => _isSettingsOpen;

    private const string SaveKey = "Settings.V1";
    private readonly List<Vector2Int> _resolutions = new();
    private readonly HashSet<string> _warnedParameters = new();
    private SettingsData _savedSettings;        // 확정된 설정 데이터
    private SettingsData _previewSettings;      // 미리보기 설정 데이터
    private bool _hasStarted;

    public SettingsData SavedSettings => _savedSettings;
    public IReadOnlyList<Vector2Int> Resolutions => _resolutions;
    // CameraController가 기존 회전 감도에 곱하는 배율
    public float MouseSensitivity => _previewSettings.MouseSensitivity;

    #region [초기화]

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        foreach (Resolution resolution in Screen.resolutions)
            AddResolution(resolution.width, resolution.height);
        AddResolution(Screen.width, Screen.height);

        _savedSettings = GetDefaultSettings();
        if (PlayerPrefs.HasKey(SaveKey))
        {
            try
            {
                _savedSettings = Normalize(JsonUtility.FromJson<SettingsData>(PlayerPrefs.GetString(SaveKey)));
            }
            catch (ArgumentException)
            {
                Debug.LogWarning("저장된 설정을 읽지 못해 기본값을 사용함.", this);
            }
        }
        _previewSettings = _savedSettings;
        if (_settingsUI != null) _settingsUI.gameObject.SetActive(false);
    }

    private void Start()
    {
        if (Instance != this) return;
        _hasStarted = true;
        if (_settingsUI == null)
            Debug.LogError("SettingsManager: SettingsUI 참조가 누락됨.", this);
        SubscribeInput();
        // AudioMixer.SetFloat는 Awake/OnEnable 대신 Start 이후에 호출함.
        ApplyPreview(_previewSettings);
        ApplyDisplay(_savedSettings);
    }

    private void OnEnable()
    {
        if (_hasStarted && Instance == this) SubscribeInput();
    }

    private void OnDisable()
    {
        CloseSettings();
        if (_inputManager != null)
            _inputManager.OnToggleSettingsPerformed -= HandleToggleSettingsInput;
        _inputManager = null;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    #endregion


    #region [설정창 제어]

    /// <summary>
    /// 전투 입력 연동을 사용하는 경우 입력 매니저에 설정창 이벤트를 구독함.
    /// </summary>
    private void SubscribeInput()
    {
        if (!_useGameplayInput || _inputManager != null || _settingsUI == null) return;
        _inputManager = InputManager.Instance;
        if (_inputManager == null)
        {
            Debug.LogError("SettingsManager: InputManager 참조가 누락됨.", this);
            return;
        }
        _inputManager.OnToggleSettingsPerformed += HandleToggleSettingsInput;
    }

    /// <summary>
    /// ESC 입력으로 설정창의 열림 상태를 전환함.
    /// </summary>
    private void HandleToggleSettingsInput()
    {
        if (_isSettingsOpen && _settingsUI != null && _settingsUI.TryCancelSessionConfirmation()) return;
        if (_isSettingsOpen) CloseSettings();
        else OpenSettings();
    }

    /// <summary>
    /// 설정창 상태와 전투 입력 연동 여부에 따라 열기 가능 여부를 반환함.
    /// </summary>
    private bool CanOpenSettings()
    {
        if (_isSettingsOpen || !isActiveAndEnabled) return false;
        if (_settingsUI == null) return false;
        if (!_useGameplayInput) return true;
        if (_inputManager == null) return false;
        // ChainQTE 열렸을때 설정창 진입 차단
        if (_inputManager.IsChainQTEInputActive) return false;
        // UltCutscene 연출중일때 설정창 진입 차단
        CombatPresentationManager presentation = CombatPresentationManager.Instance;
        if (presentation != null && presentation.IsUltPresentationActive) return false;

        return true;
    }

    /// <summary>
    /// 설정창을 표시하고 전투 입력 연동 시 전투 진행과 입력을 일시정지함.
    /// </summary>
    public void OpenSettings()
    {
        if (!CanOpenSettings()) return;
        _previousCursorLock = Cursor.lockState;
        _previousCursorVisible = Cursor.visible;
        _isSettingsOpen = true;
        if (_useGameplayInput)
        {
            _combatPresentationManager = CombatPresentationManager.Instance;
            if (_combatPresentationManager != null) _combatPresentationManager.SetGamePaused(true);
            _inputManager.SetSettingsInputActive(true);
        }
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        _settingsUI.gameObject.SetActive(true);
    }

    /// <summary>
    /// 설정창을 숨기고, OnDisable이 호출되지 않는 경우에도 입력과 커서 상태를 복원함.
    /// </summary>
    public void CloseSettings()
    {
        if (!_isSettingsOpen) return;
        if (_settingsUI != null) _settingsUI.gameObject.SetActive(false);
        CompleteSettingsClose();
    }

    /// <summary>
    /// 외부 비활성화를 포함한 종료 경로에서 게임 시간과 미리보기, 입력 상태를 한 번만 복원함.
    /// </summary>
    public void CompleteSettingsClose()
    {
        if (!_isSettingsOpen) return;
        _isSettingsOpen = false;
        if (_combatPresentationManager != null) _combatPresentationManager.SetGamePaused(false);
        _combatPresentationManager = null;
        CancelPreview();
        Cursor.lockState = _previousCursorLock;
        Cursor.visible = _previousCursorVisible;
        if (_inputManager != null) _inputManager.SetSettingsInputActive(false);
    }

    #endregion

    #region [설정 편집과 저장]

    /// <summary>
    /// 볼륨과 감도 배율의 기본값 및 현재 화면 크기를 반환함.
    /// </summary>
    public SettingsData GetDefaultSettings()
    {
        return new SettingsData
        {
            MasterVolume = 0.75f, SFXVolume = 1f, BGMVolume = 1f,
            MouseSensitivity = 1f, Width = Screen.width, Height = Screen.height,
            FullScreen = true
        };
    }

    /// <summary>
    /// 편집 중인 볼륨과 감도 배율만 미리 적용하며 화면 설정과 저장값은 유지함.
    /// </summary>
    public void ApplyPreview(SettingsData settings)
    {
        _previewSettings = Normalize(settings);
        if (!_hasStarted) return;
        SetVolume(_masterParameter, _previewSettings.MasterVolume);
        SetVolume(_sfxParameter, _previewSettings.SFXVolume);
        SetVolume(_bgmParameter, _previewSettings.BGMVolume);
    }

    /// <summary>
    /// 설정을 확정하여 화면에 적용하고 로컬 저장소에 기록함.
    /// </summary>
    public void ApplySettings(SettingsData settings)
    {
        _savedSettings = Normalize(settings);
        ApplyPreview(_savedSettings);
        ApplyDisplay(_savedSettings);
        PlayerPrefs.SetString(SaveKey, JsonUtility.ToJson(_savedSettings));
        PlayerPrefs.Save();
    }

    /// <summary>
    /// 미확정 미리보기를 마지막 확정값으로 복원함.
    /// </summary>
    public void CancelPreview()
    {
        ApplyPreview(_savedSettings);
    }

    /// <summary>
    /// 입력값을 지원 범위로 보정하고 사용할 수 없는 해상도를 현재 화면으로 대체함.
    /// </summary>
    private SettingsData Normalize(SettingsData settings)
    {
        settings.MasterVolume = ClampValue(settings.MasterVolume, 0f, 1f);
        settings.SFXVolume = ClampValue(settings.SFXVolume, 0f, 1f);
        settings.BGMVolume = ClampValue(settings.BGMVolume, 0f, 1f);
        settings.MouseSensitivity = ClampValue(settings.MouseSensitivity, 0.1f, 3f);
        if (!_resolutions.Contains(new Vector2Int(settings.Width, settings.Height)))
        {
            settings.Width = Screen.width;
            settings.Height = Screen.height;
        }
        return settings;
    }

    /// <summary>
    /// 유한하지 않은 입력은 기본 배율로 대체하고 지정 범위로 제한함.
    /// </summary>
    private static float ClampValue(float value, float min, float max)
    {
        if (float.IsNaN(value) || float.IsInfinity(value)) return 1f;
        return Mathf.Clamp(value, min, max);
    }

    #endregion

    #region [시스템 적용]

    /// <summary>
    /// 선형 볼륨을 데시벨로 변환하며 0은 믹서 최저 음량으로 처리함.
    /// </summary>
    private void SetVolume(string parameter, float volume)
    {
        bool success = _audioMixer != null &&
            _audioMixer.SetFloat(parameter, volume <= 0f ? -80f : Mathf.Log10(volume) * 20f);
        if (!success && _warnedParameters.Add(parameter))
            Debug.LogWarning($"SettingsManager: 믹서 연결과 노출 파라미터 '{parameter}'를 확인해야 함.", this);
    }

    /// <summary>
    /// 선택한 해상도와 창 모드를 적용함. 전체화면은 테두리 없는 모드로 사용함.
    /// </summary>
    private void ApplyDisplay(SettingsData settings)
    {
        Screen.SetResolution(settings.Width, settings.Height,
            settings.FullScreen ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed);
    }

    /// <summary>
    /// 주사율 차이로 중복되는 화면 크기를 제외하고 해상도 목록을 구성함.
    /// </summary>
    private void AddResolution(int width, int height)
    {
        Vector2Int size = new(width, height);
        if (width > 0 && height > 0 && !_resolutions.Contains(size)) _resolutions.Add(size);
    }

    #endregion
}
