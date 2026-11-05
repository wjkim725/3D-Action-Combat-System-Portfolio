using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.Playables;

/// <summary>
/// Hero 기준의 입장 경로를 월드 구도로 변환하고 Gameplay 카메라로 연결함.
/// </summary>
public class EntryCameraHandoff : CinemachineExtension
{
    [Header("References")]
    [SerializeField] private PlayableDirector _director;
    [SerializeField] private CinemachineCamera _gameplayCamera;

    [Header("Timeline Timing")]
    [SerializeField] private float _matchStartTime = 3.2f;
    [SerializeField] private float _matchEndTime = 4.05f;

    [Header("Timeline Local Pose")]
    [SerializeField] private Vector3 _localPosition;
    [SerializeField] private Quaternion _localRotation = Quaternion.identity;

    [Header("Entry Lighting")]
    [SerializeField] private float _lightIntensity = 0.8f;

    private Transform _entryAnchor;
    private Light _entryLight;
    private float _heightScale = 1f;
    private Vector3 _lightOffset;
    private Quaternion _lightRotation;

    #region [전환 구도 보정]

    /// <summary>
    /// 첫 Hero의 연출 기준점과 조명을 연결하고 체형 보정값을 설정함.
    /// </summary>
    public void SetEntryTarget(Transform anchor, Light entryLight, float heightScale)
    {
        _entryAnchor = anchor;
        _entryLight = entryLight;
        _heightScale = heightScale;
        if (_entryLight == null) return;
        _lightOffset = _entryLight.transform.localPosition;
        _lightRotation = _entryLight.transform.localRotation;
    }

    /// <summary>
    /// 연출 연결을 해제하고 Hero 조명의 로컬 배치를 복원함.
    /// </summary>
    public void ClearEntryTarget()
    {
        if (_entryLight != null)
        {
            _entryLight.transform.localPosition = _lightOffset;
            _entryLight.transform.localRotation = _lightRotation;
        }
        _entryAnchor = null;
        _entryLight = null;
    }

    /// <summary>
    /// Timeline에서 계산한 카메라 출력에 위치·회전·렌즈 보정을 적용함.
    /// </summary>
    protected override void PostPipelineStageCallback(
        CinemachineVirtualCameraBase vcam,
        CinemachineCore.Stage stage,
        ref CameraState state,
        float deltaTime)
    {
        if (stage != CinemachineCore.Stage.Finalize) return;
        if (_director == null || _gameplayCamera == null || _entryAnchor == null) return;

        // Timeline 로컬 경로를 Hero 기준 월드 구도로 변환함. 카메라 부모 좌표는 사용하지 않음.
        Vector3 localPosition = _localPosition;
        Quaternion localRotation = _localRotation.normalized;
        state.RawPosition = _entryAnchor.TransformPoint(localPosition * _heightScale);
        state.RawOrientation = _entryAnchor.rotation * localRotation;

        // 조명은 전투 카메라 보정 전의 입장 경로를 따라가며 기존 조명 구도를 유지함.
        if (_entryLight != null)
        {
            Vector3 lightPosition = localPosition + localRotation * _lightOffset;
            _entryLight.transform.SetPositionAndRotation(
                _entryAnchor.TransformPoint(lightPosition * _heightScale),
                state.RawOrientation * _lightRotation);
            _entryLight.intensity = _lightIntensity;
        }

        float time = (float)_director.time;
        if (time <= _matchStartTime) return;

        float progress = Mathf.InverseLerp(_matchStartTime, _matchEndTime, time);
        float weight = Mathf.SmoothStep(0f, 1f, progress);
        Transform target = _gameplayCamera.transform;

        state.RawPosition = Vector3.Lerp(state.RawPosition, target.position, weight);
        state.RawOrientation = Quaternion.Slerp(state.RawOrientation, target.rotation, weight);
        state.Lens = LensSettings.Lerp(state.Lens, _gameplayCamera.Lens, weight);
    }

    #endregion
}
