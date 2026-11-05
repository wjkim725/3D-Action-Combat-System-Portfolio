using TMPro;
using UnityEngine;

public class TutorialUI : MonoBehaviour
{
    [Header("Message Fields")]
    [SerializeField] private TMP_Text _headerText;
    [SerializeField] private TMP_Text _titleText;
    [SerializeField] private TMP_Text _bodyText;
    [SerializeField] private TMP_Text _progressText;
    [SerializeField] private TMP_Text _stepText;

    #region [안내 표시]

    /// <summary>
    /// 데이터 저장 형식과 무관하게 전달받은 단계 안내 문구를 표시함.
    /// </summary>
    public void ShowMessage(string header, string title, string body, string step)
    {
        _headerText.text = header;
        _titleText.text = title;
        _bodyText.text = body;
        _stepText.text = step;
        gameObject.SetActive(true);
    }

    /// <summary>
    /// 목적지 중심까지의 남은 거리를 미터 단위로 표시함.
    /// </summary>
    public void SetRemainingDistance(float distance)
    {
        _progressText.SetText("목표까지 {0:1} m", Mathf.Max(0f, distance));
    }

    /// <summary>
    /// 현재 단계의 목표나 진행 상태를 표시함.
    /// </summary>
    public void SetProgress(string message)
    {
        _progressText.text = message;
    }

    /// <summary>
    /// 안내 패널을 숨김.
    /// </summary>
    public void Hide()
    {
        gameObject.SetActive(false);
    }

    #endregion
}
