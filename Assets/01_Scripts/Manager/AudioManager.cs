using System.Collections.Generic;
using UnityEngine;

public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    [Header("Audio Source Pool")]
    [SerializeField, Min(1)]
    private int _sourceCount = 16;

    private readonly List<AudioSource> _sources = new();

    // 0은 미대여이며 반환한 핸들은 재사용하지 않음.
    private long _nextLoopHandle = 1;
    private readonly Dictionary<long, LoopPlayback> _loopPlaybacks = new();
    private readonly HashSet<AudioSource> _reservedLoopSources = new();
    private readonly List<long> _finishedLoopHandles = new();

    /// <summary>
    /// 대여한 반복음 Source와 위치 추적 대상 참조
    /// -> Transform 을 참조해서 SFX 재생 위치 따라가기 위해 선언.
    /// </summary>
    private sealed class LoopPlayback
    {
        public AudioSource Source;
        public Transform FollowTarget;
    }

    // 같은 Cue를 사용하는 요청들이 후보 선택 기록을 공유함.
    private readonly Dictionary<AudioCueSO, int> _lastClipIndices = new();
    private readonly HashSet<AudioCueSO> _warnedCues = new();

    #region [초기화]

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        InitializeSources();
    }

    private void OnDisable()
    {
        _finishedLoopHandles.Clear();
        _finishedLoopHandles.AddRange(_loopPlaybacks.Keys);
        foreach (long handle in _finishedLoopHandles)
            StopLoop(handle);
        _finishedLoopHandles.Clear();

        foreach (AudioSource source in _sources)
        {
            if (source == null)
                continue;

            source.Stop();
            source.clip = null;
            source.loop = false;
        }

        _lastClipIndices.Clear();
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    private void LateUpdate()
    {
        _finishedLoopHandles.Clear();
        foreach (KeyValuePair<long, LoopPlayback> pair in _loopPlaybacks)
        {
            LoopPlayback playback = pair.Value;
            if (playback.Source == null ||
                playback.FollowTarget == null ||
                !playback.FollowTarget.gameObject.activeInHierarchy)
            {
                _finishedLoopHandles.Add(pair.Key);
                continue;
            }

            playback.Source.transform.position = playback.FollowTarget.position;
        }

        // Dictionary 순회를 끝낸 뒤 유효하지 않은 대여를 반환함.
        foreach (long handle in _finishedLoopHandles)
            StopLoop(handle);
    }

    /// <summary>
    /// 일회성 소리와 반복음에 사용할 AudioSource 풀을 자식으로 생성함.
    /// </summary>
    private void InitializeSources()
    {
        int count = Mathf.Max(1, _sourceCount);

        for (int i = 0; i < count; i++)
        {
            GameObject sourceObject = new GameObject($"AudioSource_{i + 1}");
            sourceObject.transform.SetParent(transform, false);

            AudioSource source = sourceObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = false;
            source.dopplerLevel = 0f;
            source.ignoreListenerPause = false;

            _sources.Add(source);
        }
    }

    #endregion

    #region [일회성 사운드 재생]

    /// <summary>
    /// Cue에서 클립 하나를 선택하여 지정 위치에서 재생함.
    /// </summary>
    /// <returns>Source에 재생을 요청하면 true를 반환함.</returns>
    public bool PlayOneShot(AudioCueSO cue, Vector3 position)
    {
        if (!isActiveAndEnabled || cue == null || AudioListener.pause)
            return false;

        if (!cue.HasValidClips())
        {
            WarnInvalidCueOnce(cue);
            return false;
        }

        AudioSource source = FindAvailableSource();
        if (source == null)
            return false;

        int clipIndex = SelectClipIndex(cue);
        AudioClip clip = cue.GetClip(clipIndex);

        source.transform.position = position;
        source.clip = clip;
        source.volume = cue.Volume;
        source.pitch = Random.Range(
            cue.PitchRange.x,
            cue.PitchRange.y);

        source.outputAudioMixerGroup = cue.OutputMixerGroup;
        source.spatialBlend = cue.SpatialBlend;
        source.loop = false;

        source.Play();

        _lastClipIndices[cue] = clipIndex;
        return true;
    }

    /// <summary>
    /// 반복음으로 예약되지 않은 유휴 AudioSource를 반환함.
    /// </summary>
    private AudioSource FindAvailableSource()
    {
        foreach (AudioSource source in _sources)
        {
            if (source != null &&
                !_reservedLoopSources.Contains(source) &&
                !source.isPlaying)
                return source;
        }

        return null;
    }

    #endregion

    #region [반복 사운드 재생]

    /// <summary>
    /// 풀에서 Source를 대여하여 대상 위치에서 반복 재생함.
    /// </summary>
    /// <returns>대여 성공 시 핸들을, 실패 시 0을 반환함.</returns>
    public long StartLoop(AudioCueSO cue, Transform followTarget)
    {
        if (!isActiveAndEnabled || cue == null ||
            followTarget == null || !followTarget.gameObject.activeInHierarchy)
        {
            return 0;
        }

        if (!cue.HasValidClips())
        {
            WarnInvalidCueOnce(cue);
            return 0;
        }

        AudioSource source = FindAvailableSource();
        if (source == null)
            return 0;

        int clipIndex = SelectClipIndex(cue);
        long handle = _nextLoopHandle++;

        // 일시정지로 isPlaying이 false가 되어도 다른 SFX에 재사용하지 않음.
        _reservedLoopSources.Add(source);
        _loopPlaybacks.Add(handle, new LoopPlayback
        {
            Source = source,
            FollowTarget = followTarget
        });

        source.transform.position = followTarget.position;
        source.loop = true;
        source.clip = cue.GetClip(clipIndex);
        source.volume = cue.Volume;
        source.pitch = Random.Range(
            cue.PitchRange.x,
            cue.PitchRange.y);
        source.outputAudioMixerGroup = cue.OutputMixerGroup;
        source.spatialBlend = cue.SpatialBlend;
        source.dopplerLevel = 0f;
        source.ignoreListenerPause = false;

        source.Play();

        _lastClipIndices[cue] = clipIndex;
        return handle;
    }

    /// <summary>
    /// 해당 핸들의 반복음을 종료하고 Source를 풀에 반환함.
    /// </summary>
    public void StopLoop(long handle)
    {
        if (!_loopPlaybacks.TryGetValue(handle, out LoopPlayback playback))
            return;

        _loopPlaybacks.Remove(handle);
        _reservedLoopSources.Remove(playback.Source);

        AudioSource source = playback.Source;
        if (source == null)
            return;

        source.Stop();
        source.clip = null;
        source.loop = false;
        source.volume = 1f;
        source.pitch = 1f;
        source.outputAudioMixerGroup = null;
        source.spatialBlend = 0f;
        source.transform.localPosition = Vector3.zero;
    }

    /// <summary>
    /// 핸들이 유효한 반복음 대여를 가리키는지 확인함.
    /// </summary>
    public bool IsLoopActive(long handle)
    {
        return _loopPlaybacks.TryGetValue(handle, out LoopPlayback playback)
            && playback.Source != null
            && playback.FollowTarget != null
            && playback.FollowTarget.gameObject.activeInHierarchy;
    }

    #endregion

    #region [클립 선택]

    /// <summary>
    /// Cue의 선택 방식과 직전 기록에 따라 후보 인덱스를 반환함.
    /// </summary>
    private int SelectClipIndex(AudioCueSO cue)
    {
        int count = cue.ClipCount;
        int previousIndex = -1;

        if (_lastClipIndices.TryGetValue(cue, out int storedIndex))
            previousIndex = storedIndex;

        if (cue.SelectionMode == AudioCueSO.ClipSelectionMode.Sequential)
            return (previousIndex + 1) % count;

        bool excludePrevious =
            cue.AvoidImmediateRepeat &&
            count > 1 &&
            previousIndex >= 0 &&
            previousIndex < count;

        if (!excludePrevious)
            return Random.Range(0, count);

        // 직전 후보를 제외한 나머지 인덱스에서 동일 확률로 선택함.
        int selectedIndex = Random.Range(0, count - 1);

        if (selectedIndex >= previousIndex)
            selectedIndex++;

        return selectedIndex;
    }

    /// <summary>
    /// 클립 후보가 없거나 미할당된 Cue에 대해 한 번만 경고함.
    /// </summary>
    private void WarnInvalidCueOnce(AudioCueSO cue)
    {
        if (!_warnedCues.Add(cue))
            return;

        Debug.LogWarning(
            $"[AudioManager] {cue.name}: 클립 목록이 비어 있거나 " +
            "할당되지 않은 항목이 있습니다.",
            cue);
    }

    #endregion
}
