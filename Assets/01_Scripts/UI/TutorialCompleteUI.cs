using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class TutorialCompleteUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private GameSessionController _gameSessionController;
    [SerializeField] private Button _startBattleButton;
    [SerializeField] private Button _mainMenuButton;
    private InputManager _inputManager;
    private CombatPresentationManager _presentation;
    private CursorLockMode _previousCursorLock;
    private bool _previousCursorVisible;
    private bool _wasGamePaused;
    private bool _wasModalInputActive;
    private bool _isOpen;

    #region [완료 팝업 표시]

    /// <summary>
    /// 전투 입력과 시간을 잠그고 완료 팝업의 첫 버튼을 선택함.
    /// </summary>
    public void Show()
    {
        if (_isOpen) return;
        _inputManager = InputManager.Instance;
        _presentation = CombatPresentationManager.Instance;
        if (_inputManager == null || _presentation == null || _gameSessionController == null)
        {
            Debug.LogError("TutorialCompleteUI: 입력, 연출 및 세션 참조를 확인해야 함.", this);
            return;
        }
        _previousCursorLock = Cursor.lockState;
        _previousCursorVisible = Cursor.visible;
        _wasGamePaused = _presentation.IsGamePaused;
        _wasModalInputActive = _inputManager.IsModalInputActive;
        _isOpen = true;
        _inputManager.SetModalInputActive(true);
        _presentation.SetGamePaused(true);
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        gameObject.SetActive(true);
        _startBattleButton.interactable = true;
        _mainMenuButton.interactable = true;
        _startBattleButton.Select();
    }

    private void OnDisable()
    {
        if (!_isOpen) return;
        _isOpen = false;
        if (_presentation != null) _presentation.SetGamePaused(_wasGamePaused);
        if (_inputManager != null) _inputManager.SetModalInputActive(_wasModalInputActive);
        Cursor.lockState = _previousCursorLock;
        Cursor.visible = _previousCursorVisible;
        if (EventSystem.current != null && EventSystem.current.currentSelectedGameObject != null &&
            EventSystem.current.currentSelectedGameObject.transform.IsChildOf(transform))
            EventSystem.current.SetSelectedGameObject(null);
    }

    #endregion

    #region [버튼 UnityEvent]

    /// <summary>
    /// 완료 팝업 버튼에서 세션의 파티편성 씬 이동을 요청함.
    /// </summary>
    public void EnterPartyFormation()
    {
        if (!_isOpen || _gameSessionController.IsTransitioning) return;
        _gameSessionController.EnterPartyFormation();
        RefreshButtons();
    }

    /// <summary>
    /// 메인메뉴 버튼에서 세션의 메인메뉴 이동을 요청함.
    /// </summary>
    public void ReturnToMainMenu()
    {
        if (!_isOpen || _gameSessionController.IsTransitioning) return;
        _gameSessionController.ReturnToMainMenu();
        RefreshButtons();
    }

    /// <summary>
    /// 로드 요청이 수락된 경우 두 버튼의 중복 실행을 막음.
    /// </summary>
    private void RefreshButtons()
    {
        _startBattleButton.interactable = !_gameSessionController.IsTransitioning;
        _mainMenuButton.interactable = !_gameSessionController.IsTransitioning;
    }

    #endregion
}
