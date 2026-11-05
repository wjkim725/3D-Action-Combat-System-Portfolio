using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class MainMenuController : MonoBehaviour
{
    [Header("Managers")]
    [SerializeField] private SettingsManager _settingsManager;

    [Header("Menu")]
    [SerializeField] private CanvasGroup _menuGroup;
    [SerializeField] private Button _battleButton;
    [SerializeField] private Button _tutorialButton;
    [SerializeField] private Button _settingsButton;
    [SerializeField] private Button _quitButton;

    [Header("Background Cameras")]
    [SerializeField] private Camera[] _backgroundCameras;
    [SerializeField, Min(1f)] private float _cameraHoldDuration = 12f;
    [SerializeField, Min(0.01f)] private float _cameraFadeDuration = 0.6f;
    [SerializeField] private CanvasGroup _backgroundFade;

    [Header("Popups")]
    [SerializeField] private GameObject _quitPopup;
    [SerializeField] private Button _quitCancelButton;
    [SerializeField] private Button _quitConfirmButton;
    [SerializeField] private Selectable _settingsFirstSelection;

    [Header("Scene Transition")]
    [UnityEngine.Serialization.FormerlySerializedAs("_combatSceneName")]
    [SerializeField] private string _partyFormationSceneName = "PartyFormationScene";
    [SerializeField] private string _tutorialSceneName = "TutorialScene";
    [SerializeField, Min(0.01f)] private float _sceneFadeDuration = 0.5f;
    [SerializeField] private CanvasGroup _screenFade;

    private int _cameraIndex;
    private bool _initialized;
    private bool _isLeavingMenu;
    private bool _wasPopupOpen;
    private GameObject _returnSelection;
    private Coroutine _cameraCycleCoroutine;

    private bool IsPopupOpen => _settingsManager.IsSettingsOpen ||
        _quitPopup.activeSelf;
    private bool CanExecuteMenuAction => _initialized && isActiveAndEnabled &&
        !_isLeavingMenu && !IsPopupOpen;

    #region [초기화]

    private void Start()
    {
        if (!HasRequiredReferences())
        {
            Debug.LogError("MainMenuController: 매니저, 카메라, 메뉴 및 팝업 UI 참조를 확인해야 함.", this);
            enabled = false;
            return;
        }

        _battleButton.onClick.AddListener(EnterCombat);
        _tutorialButton.onClick.AddListener(EnterTutorial);
        _settingsButton.onClick.AddListener(OpenSettings);
        _quitButton.onClick.AddListener(OpenQuitConfirmation);
        _quitCancelButton.onClick.AddListener(CloseCurrentPopup);
        _quitConfirmButton.onClick.AddListener(ConfirmQuit);
        _initialized = true;
        _quitPopup.SetActive(false);
        SetBackgroundCamera(0);
        ResumeMenu();
    }

    private void OnEnable()
    {
        if (_initialized) ResumeMenu();
    }

    private void Update()
    {
        if (!_initialized || _isLeavingMenu) return;
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            CloseCurrentPopup();
        RefreshMenuInteraction();
    }

    private void OnDisable()
    {
        StopAllCoroutines();
        _cameraCycleCoroutine = null;
        if (!_initialized) return;
        CloseCurrentPopup();
        if (_menuGroup != null)
        {
            _menuGroup.interactable = false;
            _menuGroup.blocksRaycasts = false;
        }
        if (_screenFade != null)
        {
            _screenFade.alpha = 0f;
            _screenFade.blocksRaycasts = false;
        }
    }

    private void OnDestroy()
    {
        if (!_initialized) return;
        if (_battleButton != null) _battleButton.onClick.RemoveListener(EnterCombat);
        if (_tutorialButton != null) _tutorialButton.onClick.RemoveListener(EnterTutorial);
        if (_settingsButton != null) _settingsButton.onClick.RemoveListener(OpenSettings);
        if (_quitButton != null) _quitButton.onClick.RemoveListener(OpenQuitConfirmation);
        if (_quitCancelButton != null) _quitCancelButton.onClick.RemoveListener(CloseCurrentPopup);
        if (_quitConfirmButton != null) _quitConfirmButton.onClick.RemoveListener(ConfirmQuit);
    }

    /// <summary>
    /// 메뉴 동작에 필요한 참조와 카메라 목록의 유효성을 확인함.
    /// </summary>
    private bool HasRequiredReferences()
    {
        if (_settingsManager == null || EventSystem.current == null ||
            _menuGroup == null || _battleButton == null || _tutorialButton == null ||
            _settingsButton == null || _quitButton == null || _backgroundFade == null ||
            _screenFade == null || _quitPopup == null ||
            _quitCancelButton == null ||
            _quitConfirmButton == null || _settingsFirstSelection == null ||
            _backgroundCameras == null || _backgroundCameras.Length == 0)
            return false;

        foreach (Camera camera in _backgroundCameras)
            if (camera == null) return false;
        return true;
    }

    /// <summary>
    /// 커서와 메뉴 입력을 복원하고 배경 순환을 시작함.
    /// </summary>
    private void ResumeMenu()
    {
        _isLeavingMenu = false;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        _backgroundFade.alpha = 0f;
        _backgroundFade.blocksRaycasts = false;
        _screenFade.alpha = 0f;
        _screenFade.blocksRaycasts = false;
        RefreshMenuInteraction();
        if (!IsPopupOpen) SelectObject(_battleButton.gameObject);
        _cameraCycleCoroutine = StartCoroutine(CycleBackgroundCameras());
    }

    #endregion

    #region [팝업과 메뉴 입력]

    /// <summary>
    /// 기존 설정 매니저를 통해 설정창을 표시함.
    /// </summary>
    public void OpenSettings()
    {
        if (!CanExecuteMenuAction) return;
        _returnSelection = _settingsButton.gameObject;
        _settingsManager.OpenSettings();
        RefreshMenuInteraction();
        if (_settingsManager.IsSettingsOpen) SelectObject(_settingsFirstSelection.gameObject);
    }

    /// <summary>
    /// 종료 확인 팝업을 표시하고 취소 버튼을 기본 선택함.
    /// </summary>
    public void OpenQuitConfirmation()
    {
        if (!CanExecuteMenuAction) return;
        _returnSelection = _quitButton.gameObject;
        _quitPopup.SetActive(true);
        RefreshMenuInteraction();
        SelectObject(_quitCancelButton.gameObject);
    }

    /// <summary>
    /// 열린 팝업을 닫고 메뉴 입력 및 선택을 복원함.
    /// </summary>
    public void CloseCurrentPopup()
    {
        if (!_initialized) return;
        if (_settingsManager != null && _settingsManager.IsSettingsOpen)
            _settingsManager.CloseSettings();
        if (_quitPopup != null) _quitPopup.SetActive(false);
        if (_settingsManager != null && _quitPopup != null &&
            _menuGroup != null && _battleButton != null)
            RefreshMenuInteraction();
    }

    /// <summary>
    /// 팝업 상태에 따라 메뉴 입력을 차단하고 닫힌 순간 선택을 복원함.
    /// </summary>
    private void RefreshMenuInteraction()
    {
        bool popupOpen = IsPopupOpen;
        bool interactable = !popupOpen && !_isLeavingMenu;
        _menuGroup.interactable = interactable;
        _menuGroup.blocksRaycasts = interactable;
        if (_wasPopupOpen && !popupOpen && !_isLeavingMenu)
            SelectObject(_returnSelection != null ? _returnSelection : _battleButton.gameObject);
        _wasPopupOpen = popupOpen;
    }

    /// <summary>
    /// EventSystem의 선택 대상을 변경함.
    /// </summary>
    private void SelectObject(GameObject target)
    {
        if (EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(target);
    }

    #endregion

    #region [배경 전환]

    /// <summary>
    /// 팝업이 닫힌 동안 유지 시간을 누적하고 고정 카메라를 순환함.
    /// </summary>
    private IEnumerator CycleBackgroundCameras()
    {
        while (!_isLeavingMenu)
        {
            float elapsed = 0f;
            while (elapsed < _cameraHoldDuration)
            {
                if (!IsPopupOpen) elapsed += Time.unscaledDeltaTime;
                yield return null;
            }
            yield return Fade(_backgroundFade, 1f, _cameraFadeDuration, true);
            while (IsPopupOpen) yield return null;
            SetBackgroundCamera((_cameraIndex + 1) % _backgroundCameras.Length);
            yield return Fade(_backgroundFade, 0f, _cameraFadeDuration, true);
        }
    }

    /// <summary>
    /// AudioListener가 있는 오브젝트를 유지하고 Camera 컴포넌트만 전환함.
    /// </summary>
    private void SetBackgroundCamera(int index)
    {
        _cameraIndex = index;
        for (int i = 0; i < _backgroundCameras.Length; i++)
            _backgroundCameras[i].enabled = i == index;
    }

    /// <summary>
    /// 비스케일 시간으로 투명도를 변경하고 배경 전환은 팝업 중 정지함.
    /// </summary>
    private IEnumerator Fade(CanvasGroup group, float target, float duration, bool pauseForPopup)
    {
        float start = group.alpha;
        float elapsed = 0f;
        duration = Mathf.Max(0.01f, duration);
        while (elapsed < duration)
        {
            if (!pauseForPopup || !IsPopupOpen)
            {
                elapsed += Time.unscaledDeltaTime;
                group.alpha = Mathf.Lerp(start, target, elapsed / duration);
            }
            yield return null;
        }
        group.alpha = target;
    }

    #endregion

    #region [씬 이동과 종료]

    /// <summary>
    /// 전투 전 파티편성 씬으로의 이동을 요청함.
    /// </summary>
    public void EnterCombat()
    {
        BeginSceneTransition(_partyFormationSceneName);
    }

    /// <summary>
    /// 별도 튜토리얼 씬으로의 이동을 요청함.
    /// </summary>
    public void EnterTutorial()
    {
        BeginSceneTransition(_tutorialSceneName);
    }

    /// <summary>
    /// 대상 씬을 확인하고 중복 입력을 차단한 뒤 공통 화면 전환을 시작함.
    /// </summary>
    private void BeginSceneTransition(string sceneName)
    {
        if (!CanExecuteMenuAction) return;
        if (string.IsNullOrWhiteSpace(sceneName) ||
            !Application.CanStreamedLevelBeLoaded(sceneName))
        {
            Debug.LogWarning($"MainMenuController: 대상 씬 '{sceneName}'의 빌드 등록을 확인해야 함.", this);
            return;
        }

        _isLeavingMenu = true;
        RefreshMenuInteraction();
        SelectObject(null);
        if (_cameraCycleCoroutine != null) StopCoroutine(_cameraCycleCoroutine);
        _cameraCycleCoroutine = null;
        StartCoroutine(LoadScene(sceneName));
    }

    /// <summary>
    /// 전체 화면을 가린 뒤 대상 씬을 로드하고 요청 실패 시 메뉴를 복원함.
    /// </summary>
    private IEnumerator LoadScene(string sceneName)
    {
        _screenFade.blocksRaycasts = true;
        yield return Fade(_screenFade, 1f, _sceneFadeDuration, false);
        AsyncOperation operation = null;
        try
        {
            operation = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single);
        }
        catch (System.Exception exception)
        {
            Debug.LogError($"MainMenuController: 씬 로드 요청 실패. {exception.Message}", this);
        }
        if (operation == null)
        {
            ResumeMenu();
            yield break;
        }
        yield return operation;
    }

    /// <summary>
    /// 종료 확인 상태에서 게임을 종료하고 Editor에서는 Play Mode를 종료함.
    /// </summary>
    public void ConfirmQuit()
    {
        if (!_initialized || !isActiveAndEnabled || _isLeavingMenu || !_quitPopup.activeSelf) return;
        _isLeavingMenu = true;
        RefreshMenuInteraction();
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    #endregion
}
