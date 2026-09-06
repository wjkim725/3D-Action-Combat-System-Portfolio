using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ChainQTECandidateSlot : MonoBehaviour
{
    [Header("Candidate UI")]
    [SerializeField] private RawImage _portrait;
    [SerializeField] private TMP_Text _inputGuideText;

    public Hero CandidateHero { get; private set; }

    /// <summary>
    /// Chain Attack 후보 정보 연결
    /// </summary>
    public void BindHero(Hero hero, string inputGuide)
    {
        if (hero == null)
        {
            Clear();
            return;
        }

        CandidateHero = hero;
        gameObject.SetActive(true);

        if (_inputGuideText != null)
        {
            _inputGuideText.text = inputGuide;
        }

        if (_portrait != null)
        {
            _portrait.texture = hero.Portrait;
        }
    }

    /// <summary>
    /// 후보 정보와 슬롯 초기화
    /// </summary>
    public void Clear()
    {
        CandidateHero = null;

        if (_inputGuideText != null)
        {
            _inputGuideText.text = string.Empty;
        }

        if (_portrait != null)
        {
            _portrait.texture = null;
        }

        gameObject.SetActive(false);
    }
}