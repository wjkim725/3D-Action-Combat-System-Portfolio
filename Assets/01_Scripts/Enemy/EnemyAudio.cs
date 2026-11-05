using UnityEngine;

[DisallowMultipleComponent]
public class EnemyAudio : MonoBehaviour
{
    #region [Cue 데이터 영역]

    [Header("Common SFX")]
    [SerializeField]
    private AudioCueSO _footstepCue;

    [SerializeField]
    private AudioCueSO _hitCue;

    [SerializeField]
    private AudioCueSO _swingCue;

    #endregion


    #region [SFX 재생 영역]

    /// <summary>
    /// 현재 위치에서 발소리를 재생함.
    /// </summary>
    public void PlayFootStep()
    {
        if (!isActiveAndEnabled || _footstepCue == null)
            return;

        AudioManager.Instance?.PlayOneShot(
            _footstepCue,
            transform.position);
    }

    /// <summary>
    /// 피격 반응 또는 사망 진입 요청에 따라 현재 위치에서 반응음을 재생함.
    /// </summary>
    public void PlayHit()
    {
        if (!isActiveAndEnabled || _hitCue == null)
            return;

        AudioManager.Instance?.PlayOneShot(
            _hitCue,
            transform.position);
    }

    /// <summary>
    /// 현재 위치에서 공격 휘두르기 효과음을 재생함.
    /// </summary>
    public void PlaySwing()
    {
        if (!isActiveAndEnabled || _swingCue == null)
            return;

        AudioManager.Instance?.PlayOneShot(
            _swingCue,
            transform.position);
    }

    #endregion
}
