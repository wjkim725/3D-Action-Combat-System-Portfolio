using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

/// <summary>
/// 첫 Hero를 기준으로 입장 카메라 Timeline을 재생하고 종료 시 전투 조작을 복원함.
/// </summary>
public class CombatEntryController : MonoBehaviour
{
    [Header("Entry Timeline")]
    [SerializeField] private PlayableDirector _director;
    [SerializeField] private EntryCameraHandoff _cameraHandoff;
    [SerializeField] private Animator _cameraAnimator;
    [SerializeField] private CinemachineCamera _entryCamera;
    [SerializeField] private CinemachineCamera _gameplayCamera;
    [SerializeField] private CinemachineBrain _brain;

    [Header("Presentation")]
    [SerializeField] private Canvas[] _hideCanvases;
    private Light _entryLight;
    private bool[] _canvasStates;

    private Hero _hero;
    private bool _isPlaying;

    public bool IsPlaying => _isPlaying;

    #region [입장 연출]

    /// <summary>
    /// 씬 시작 담당자의 요청으로 Hero의 입장 상태와 Timeline 재생을 시작함.
    /// </summary>
    public bool TryBeginEntry(Hero hero)
    {
        if (!isActiveAndEnabled || _isPlaying || hero == null) return false;
        Transform cameraAnchor = hero.EntryCameraAnchor;
        if (_director == null || !(_director.playableAsset is TimelineAsset timeline) ||
            cameraAnchor == null || _cameraHandoff == null || _cameraAnimator == null || _entryCamera == null ||
            _gameplayCamera == null || _brain == null)
        {
            Debug.LogError("CombatEntryController: 입장 Timeline과 카메라 참조를 확인해야 함.", this);
            return false;
        }

        _hero = hero;
        _entryLight = hero.EntryKeyLight;
        _hero.SetEntryTimelineControlled(true);
        _hero.ChangeState(HeroState.Entry);
        if (CombatDetector.Instance != null) CombatDetector.Instance.HoldCombatState(10f);
        CameraController gameplayController = _gameplayCamera.GetComponentInParent<CameraController>();
        if (gameplayController != null) gameplayController.PrepareEntryView(_hero);
        _canvasStates = new bool[_hideCanvases != null ? _hideCanvases.Length : 0];
        for (int i = 0; i < _canvasStates.Length; i++)
        {
            if (_hideCanvases[i] == null) continue;
            _canvasStates[i] = _hideCanvases[i].enabled;
            _hideCanvases[i].enabled = false;
        }
        if (_entryLight != null) _entryLight.enabled = true;
        // 얼굴 높이 차이를 보정하되 위치는 흔들리는 머리 본 대신 Hero 원점을 기준으로 유지함.
        Animator animator = _hero.Animation.Animator;
        Transform head = animator.isHuman ? animator.GetBoneTransform(HumanBodyBones.Head) : null;
        float height = head != null ? _hero.transform.InverseTransformPoint(head.position).y : 1.55f;
        float heightScale = Mathf.Clamp(height / 1.55f, 0.75f, 1.25f);
        _cameraHandoff.SetEntryTarget(cameraAnchor, _entryLight, heightScale);

        foreach (TrackAsset track in timeline.GetOutputTracks())
        {
            if (track is AnimationTrack) _director.SetGenericBinding(track, _cameraAnimator);
            else if (track is CinemachineTrack) _director.SetGenericBinding(track, _brain);
        }
        _director.SetReferenceValue("EntryCamera", _entryCamera);
        _director.SetReferenceValue("GameplayCamera", _gameplayCamera);
        _director.extrapolationMode = DirectorWrapMode.None;
        _director.timeUpdateMode = DirectorUpdateMode.GameTime;
        _director.stopped += HandleTimelineStopped;
        _isPlaying = true;
        // Entry 상태 진입에서 시작한 모션과 같은 프레임에 Timeline을 재생함.
        _director.time = 0;
        _director.Play();
        _director.Evaluate();
        return true;
    }

    private void Update()
    {
        if (_isPlaying && (_hero == null || _hero.CurrentState != HeroState.Entry)) EndEntry();
    }

    private void OnDisable()
    {
        EndEntry();
    }

    /// <summary>
    /// Timeline 정상 종료와 외부 정지에 공통 입장 종료 처리를 적용함.
    /// </summary>
    private void HandleTimelineStopped(PlayableDirector director)
    {
        EndEntry();
    }

    /// <summary>
    /// Timeline 카메라 제어를 해제하고 입장 상태와 입력을 한 번만 복원함.
    /// </summary>
    public void EndEntry()
    {
        if (!_isPlaying) return;
        _isPlaying = false;
        _director.stopped -= HandleTimelineStopped;
        _director.Stop();
        _cameraHandoff.ClearEntryTarget();
        if (_entryLight != null) _entryLight.enabled = false;
        for (int i = 0; i < _canvasStates.Length; i++)
            if (_hideCanvases[i] != null) _hideCanvases[i].enabled = _canvasStates[i];
        if (_hero != null) _hero.CompleteEntry();
    }

    #endregion
}
