using UnityEngine;
using UnityEngine.Audio;

[CreateAssetMenu(fileName = "AudioCueSO", menuName = "Scriptable Objects/AudioCueSO")]
public class AudioCueSO : ScriptableObject
{
    public enum ClipSelectionMode
    {
        Random,
        Sequential
    }

    [Header("Audio Clips")]
    [Tooltip("같은 용도로 교체 재생할 수 있는 클립 후보")]
    [SerializeField] private AudioClip[] _clips;

    [SerializeField]
    private ClipSelectionMode _selectionMode = ClipSelectionMode.Random;

    [Tooltip("랜덤 선택 시 직전에 선택한 후보의 연속 재생 방지")]
    [SerializeField] private bool _avoidImmediateRepeat = true;

    [Header("Playback")]
    [SerializeField, Range(0f, 1f)]
    private float _volume = 1f;

    [Tooltip("재생 시작 시 선택할 피치 범위. 원본 피치는 (1, 1)")]
    [SerializeField] private Vector2 _pitchRange = Vector2.one;

    [SerializeField] private AudioMixerGroup _outputMixerGroup;

    [Header("Spatial Audio")]
    [Tooltip("0은 2D, 1은 3D 재생")]
    [SerializeField, Range(0f, 1f)]
    private float _spatialBlend = 0f;


    #region [설정 조회]

    public int ClipCount => _clips != null ? _clips.Length : 0;
    public ClipSelectionMode SelectionMode => _selectionMode;
    public bool AvoidImmediateRepeat => _avoidImmediateRepeat;

    public float Volume => _volume;
    public Vector2 PitchRange => _pitchRange;
    public AudioMixerGroup OutputMixerGroup => _outputMixerGroup;

    public float SpatialBlend => _spatialBlend;


    /// <summary>
    /// 지정한 후보 인덱스의 클립을 반환함.
    /// </summary>
    public AudioClip GetClip(int index)
    {
        if (index < 0 || index >= ClipCount)
            return null;

        return _clips[index];
    }

    #endregion

    #region [설정 검증]

    /// <summary>
    /// 클립 후보가 존재하며 모든 항목에 클립이 할당되어 있는지 반환함.
    /// </summary>
    public bool HasValidClips()
    {
        if (ClipCount == 0)
            return false;

        for (int i = 0; i < _clips.Length; i++)
        {
            if (_clips[i] == null)
                return false;
        }

        return true;
    }

    private void OnValidate()
    {
        _volume = Mathf.Clamp01(_volume);

        // 정방향 재생을 사용하며 0 피치로 인한 재생 정지를 방지함.
        float minimumPitch = Mathf.Min(_pitchRange.x, _pitchRange.y);
        float maximumPitch = Mathf.Max(_pitchRange.x, _pitchRange.y);

        _pitchRange = new Vector2(
            Mathf.Clamp(minimumPitch, 0.01f, 3f),
            Mathf.Clamp(maximumPitch, 0.01f, 3f));

        _spatialBlend = Mathf.Clamp01(_spatialBlend);
    }

    #endregion
}
