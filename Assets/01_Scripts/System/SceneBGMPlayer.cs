using UnityEngine;

/// <summary>
/// 씬 초기화가 끝난 뒤 유지 중인 BGM 매니저에 해당 씬의 곡을 요청함.
/// </summary>
public class SceneBGMPlayer : MonoBehaviour
{
    public enum BGMType { MainMenu, Combat }

    [Header("Scene BGM")]
    [SerializeField] private BGMType _bgm;

    #region [씬 BGM 요청]

    private void Start()
    {
        if (BGMManager.Instance == null)
        {
            Debug.LogError("SceneBGMPlayer: 씬에 BGMManager 프리팹이 필요함.", this);
            return;
        }

        if (_bgm == BGMType.MainMenu) BGMManager.Instance.PlayMainMenuBGM();
        else BGMManager.Instance.PlayMainCombatBGM();
    }

    #endregion
}
