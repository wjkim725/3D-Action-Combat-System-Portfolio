using UnityEngine;

[DisallowMultipleComponent]
public class HeroAudio : MonoBehaviour
{
    #region [Cue 데이터 영역]

    [SerializeField]
    private AudioCueSO _footstepCue;

    [SerializeField]
    private AudioCueSO _dodgeCue;

    [SerializeField]
    private AudioCueSO _perfectDodgeCue;

    #endregion


    #region [SFX 재생 영역]

    public void PlayFootStep()
    {
        if (!isActiveAndEnabled || _footstepCue == null)
            return;

        AudioManager.Instance?.PlayOneShot(_footstepCue, transform.position);
    }

    /// <summary>
    /// HeroFSM EnterState - Dodge 호출
    /// </summary>
    public void PlayDodge()
    {
        if (!isActiveAndEnabled || _dodgeCue == null)
            return;

        AudioManager.Instance?.PlayOneShot(_dodgeCue, transform.position);
    }

    #endregion

}
