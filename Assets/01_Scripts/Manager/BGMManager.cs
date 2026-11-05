using UnityEngine;

/// <summary>
/// 씬 사이에 BGM 재생을 유지하고 새 곡의 시작 음량을 서서히 높임.
/// </summary>
public class BGMManager : MonoBehaviour
{
    public static BGMManager Instance { get; private set; }

    [Header("BGM List")]
    [SerializeField] private AudioCueSO _mainMenuBGM;
    [SerializeField] private AudioCueSO _mainCombatBGM;

    [Header("Fade In")]
    [SerializeField, Min(0f)] private float _fadeInDuration = 0.6f;

    private AudioSource _source;
    private float _targetVolume;
    private float _fadeProgress;
    private double _fadeStartedAt;

    #region [초기화]

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        // Managers 등의 자식으로 배치해도 BGM 오브젝트만 씬 밖에 유지함.
        transform.SetParent(null, true);
        DontDestroyOnLoad(gameObject);
        _source = gameObject.AddComponent<AudioSource>();
        _source.playOnAwake = false;
        _source.loop = true;
        _source.spatialBlend = 0f;
        _source.dopplerLevel = 0f;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void Update()
    {
        if (_source == null || _fadeProgress >= 1f) return;
        // 설정창의 일시정지와 무관하게 진행하며 믹서의 사용자 볼륨은 변경하지 않음.
        _fadeProgress = _fadeInDuration <= 0f ? 1f :
            Mathf.Clamp01((float)(AudioSettings.dspTime - _fadeStartedAt) / _fadeInDuration);
        _source.volume = _targetVolume * _fadeProgress;
    }

    #endregion

    #region [BGM 재생]

    /// <summary>
    /// 메인메뉴와 파티편성에서 사용하는 BGM 재생을 요청함.
    /// </summary>
    public void PlayMainMenuBGM()
    {
        PlayBGM(_mainMenuBGM);
    }

    /// <summary>
    /// 전투와 튜토리얼에서 사용하는 BGM 재생을 요청함.
    /// </summary>
    public void PlayMainCombatBGM()
    {
        PlayBGM(_mainCombatBGM);
    }

    /// <summary>
    /// 같은 곡은 재생 위치와 페이드를 유지하고 새 곡은 무음부터 페이드인함.
    /// </summary>
    private void PlayBGM(AudioCueSO cue)
    {
        if (cue == null || !cue.HasValidClips())
        {
            Debug.LogError("BGMManager: 재생 가능한 BGM Cue가 필요함.", this);
            return;
        }

        AudioClip clip = cue.GetClip(0);
        // 재생 상태가 일시적으로 false여도 같은 곡의 재생 위치와 페이드는 초기화하지 않음.
        if (_source.clip == clip) return;

        _targetVolume = cue.Volume;
        _source.outputAudioMixerGroup = cue.OutputMixerGroup;

        _source.Stop();
        _source.clip = clip;
        _fadeProgress = _fadeInDuration <= 0f ? 1f : 0f;
        _source.volume = _targetVolume * _fadeProgress;
        _source.Play();
        _fadeStartedAt = AudioSettings.dspTime;
    }

    #endregion
}
