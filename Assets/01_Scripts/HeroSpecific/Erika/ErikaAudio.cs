using UnityEngine;

[DisallowMultipleComponent]
public class ErikaAudio : MonoBehaviour
{
    private Hero _hero;

    private void Awake()
    {
        _hero = GetComponent<Hero>();
    }

    [Header("Sliding")]
    [SerializeField] private AudioCueSO _slidingCue;

    [Header("Bow String")]
    [SerializeField] private AudioCueSO _bowStringLightCue;
    [SerializeField] private AudioCueSO _bowStringNormalCue;
    [SerializeField] private AudioCueSO _bowStringHeavyCue;
    [SerializeField] private AudioCueSO _bowStringUltCue;

    [Header("Arrow")]
    [SerializeField] private AudioCueSO _arrowLightCue;
    [SerializeField] private AudioCueSO _arrowHeavyCue;

    [Header("Melee")]
    [SerializeField] private AudioCueSO _meleeCue;

    #region [SFX 이벤트 영역]

    /// <summary>
    /// 애니메이션 이벤트 시점에 슬라이딩 효과음을 재생함.
    /// </summary>
    public void OnSlidingSFX()
    {
        // 여진공격에서는 오디오 차단 -> 재생되면 난잡함
        if (_hero.CurrentState == HeroState.AfterShock) return;

        if (!isActiveAndEnabled || _slidingCue == null)
            return;

        AudioManager.Instance?.PlayOneShot(
            _slidingCue,
            transform.position);
    }

    /// <summary>
    /// 애니메이션 이벤트 시점에 가벼운 시위 당김 효과음을 재생함.
    /// </summary>
    public void OnBowStringLightSFX()
    {
        if (!isActiveAndEnabled || _bowStringLightCue == null)
            return;

        AudioManager.Instance?.PlayOneShot(
            _bowStringLightCue,
            transform.position);
    }

    /// <summary>
    /// 애니메이션 이벤트 시점에 일반 시위 당김 효과음을 재생함.
    /// </summary>
    public void OnBowStringNormalSFX()
    {
        if (!isActiveAndEnabled || _bowStringNormalCue == null)
            return;

        AudioManager.Instance?.PlayOneShot(
            _bowStringNormalCue,
            transform.position);
    }

    /// <summary>
    /// 애니메이션 이벤트 시점에 강한 시위 당김 효과음을 재생함.
    /// </summary>
    public void OnBowStringHeavySFX()
    {
        if (!isActiveAndEnabled || _bowStringHeavyCue == null)
            return;

        AudioManager.Instance?.PlayOneShot(
            _bowStringHeavyCue,
            transform.position);
    }

    /// <summary>
    /// 애니메이션 이벤트 시점에 궁극기 시위 당김 효과음을 재생함.
    /// </summary>
    public void OnBowStringUltSFX()
    {
        if (!isActiveAndEnabled || _bowStringUltCue == null)
            return;

        AudioManager.Instance?.PlayOneShot(
            _bowStringUltCue,
            transform.position);
    }

    /// <summary>
    /// 애니메이션 이벤트 시점에 가벼운 화살 발사 효과음을 재생함.
    /// </summary>
    public void OnArrowLightSFX()
    {
        if (!isActiveAndEnabled || _arrowLightCue == null)
            return;

        AudioManager.Instance?.PlayOneShot(
            _arrowLightCue,
            transform.position);
    }

    /// <summary>
    /// 애니메이션 이벤트 시점에 강한 화살 발사 효과음을 재생함.
    /// </summary>
    public void OnArrowHeavySFX()
    {
        if (!isActiveAndEnabled || _arrowHeavyCue == null)
            return;

        AudioManager.Instance?.PlayOneShot(
            _arrowHeavyCue,
            transform.position);
    }

    /// <summary>
    /// 애니메이션 이벤트 시점에 근접 공격 모션 효과음을 재생함.
    /// </summary>
    public void OnMeleeSFX()
    {
        if (!isActiveAndEnabled || _meleeCue == null)
            return;

        AudioManager.Instance?.PlayOneShot(
            _meleeCue,
            transform.position);
    }

    #endregion
}
