using UnityEngine;
using UnityEngine.SceneManagement;

public class GameSessionController : MonoBehaviour
{
    [Header("Settings")]
    [SerializeField] private SettingsManager _settingsManager;

    [Header("Party Formation")]
    [SerializeField] private PartyFormationController _partyFormationController;

    [Header("Scene Transition")]
    [SerializeField] private string _mainMenuSceneName = "MainMenuScene";
    [SerializeField] private string _combatSceneName = "GamePlayScene";
    [SerializeField] private string _partyFormationSceneName = "PartyFormationScene";
    public bool IsTransitioning { get; private set; }

    #region [세션 종료]

    /// <summary>
    /// 설정창의 일시정지와 미리보기를 정리한 뒤 메인메뉴로 이동함.
    /// </summary>
    public void ReturnToMainMenu()
    {
        if (BeginSceneTransition(_mainMenuSceneName))
            PartyFormationController.ClearConfirmedParty();
    }

    /// <summary>
    /// 튜토리얼 완료 후 파티편성 씬으로 이동함.
    /// </summary>
    public void EnterPartyFormation()
    {
        if (BeginSceneTransition(_partyFormationSceneName))
            PartyFormationController.ClearConfirmedParty();
    }

    /// <summary>
    /// 확정 편성이 있으면 전투 씬으로 이동하고 실패하면 편성 입력을 복원함.
    /// </summary>
    public void EnterCombat()
    {
        if (IsTransitioning) return;
        if (!PartyFormationController.TryGetConfirmedParty(out Hero[] party) ||
            !BeginSceneTransition(_combatSceneName))
        {
            _partyFormationController?.ResetConfirmation();
        }
    }

    /// <summary>
    /// 등록된 씬으로 이동하며 로드 요청 실패 시 기존 설정창 상태를 복원함.
    /// </summary>
    private bool BeginSceneTransition(string sceneName)
    {
        if (!isActiveAndEnabled || IsTransitioning) return false;
        if (string.IsNullOrWhiteSpace(sceneName) ||
            !Application.CanStreamedLevelBeLoaded(sceneName))
        {
            Debug.LogError($"GameSessionController: '{sceneName}' 씬 등록을 확인해야 함.", this);
            return false;
        }

        IsTransitioning = true;
        // 설정창이 없는 파티편성 씬에서도 같은 전환 경로를 사용함.
        bool wasSettingsOpen = _settingsManager != null && _settingsManager.IsSettingsOpen;
        _settingsManager?.CloseSettings();
        try
        {
            if (SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single) != null)
                return true;
        }
        catch (System.Exception exception)
        {
            Debug.LogError($"GameSessionController: '{sceneName}' 로드 요청 실패. {exception.Message}", this);
        }
        IsTransitioning = false;
        if (wasSettingsOpen) _settingsManager.OpenSettings();
        return false;
    }

    /// <summary>
    /// 설정 상태를 정리하고 게임을 종료함. Editor에서는 Play Mode를 종료함.
    /// </summary>
    public void QuitGame()
    {
        if (!isActiveAndEnabled || IsTransitioning) return;
        IsTransitioning = true;
        if (_settingsManager != null) _settingsManager.CloseSettings();
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    #endregion
}
