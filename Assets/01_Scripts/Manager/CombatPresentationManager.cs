using System.Collections;
using System.Collections.Generic;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Rendering;
using UnityEngine.Timeline;

public class CombatPresentationManager : MonoBehaviour
{
    public static CombatPresentationManager Instance { get; private set; }

    [Header("Cinemachine Impulse Source")]
    [SerializeField] private CinemachineImpulseSource _parryImpulseSource;
    [SerializeField] private CinemachineImpulseSource _hitImpulseSource;

    [Header("Gameplay Camera")]
    [SerializeField] private CameraController _gameplayVCam;

    [Header("Combat HUD")]
    [SerializeField] private Canvas _combatHUDCanvas;
    private bool _wasCombatHUDEnabled; // 궁극기 시작 전 표시 상태를 종료·중단 시 그대로 복구함.

    [Header("Global Presentation Settings")]
    [SerializeField] private bool _isPresenting = false;
    public bool IsPresenting => _isPresenting;


    [Header("VCam Priority")]
    // 메인 GamePlayVCam : 10
    private const int UltVCamPriority = 30;
    private const int ChainQTEVCamPriority = 25;
    private const int ParryVCamPriority = 20;


    [Header("PlayableDirector")]
    [SerializeField] private PlayableDirector _playableDirector;
    private bool _isUltPresentationActive;
    public bool IsUltPresentationActive => _isUltPresentationActive;
    private Hero _ultCaster;
    private Animator _ultAnimator;
    private CinemachineCamera _activeUltCamera;
    private GameObject _ultLighting;
    private bool _ultLayersHidden;
    private CinemachineBrain _ultBrain;
    private CinemachineBlendDefinition _previousUltBlend;
    private bool _ultBlendOverridden;


    [Header("Ult Rendering")]
    [SerializeField] private string _ultRenderLayerName = "UltCharacter";
    [SerializeField] private Color _ultBackgroundColor = new Color(0.015f, 0.035f, 0.09f, 1f);
    private readonly Dictionary<GameObject, int> _ultOriginalLayers = new Dictionary<GameObject, int>();
    private Camera _ultRenderCamera;
    private CameraClearFlags _ultOriginalClearFlags;
    private Color _ultOriginalBackgroundColor;

    [Header("Main Camera & Layer Mask")]
    private Camera _mainCamera;
    private int _originalMask;


    [Header("Perfect Dodge Slow Motion Settings")]
    [SerializeField] private float _perfectDodgeTimeScale = 0.15f;
    [SerializeField] private float _perfectDodgeDuration = 0.33f;
    private float _previousDodgeTimeScale;
    private float _previousDodgeFixedDeltaTime;
    private bool _isPerfectDodgeSlowMotionActive;   // 중복 호출 시 TimeScale과 DeltaTime이 오염되는것 방지
    private Coroutine _perfectDodgeSlowMotionCoroutine;
    private Coroutine _delayedPerfectDodgePresentationCoroutine;


    [Header("Perfect Dodge Vignette Settings")]
    [SerializeField] private Volume _perfectDodgeVolume;
    [SerializeField] private float _perfectDodgeVignetteFadeInDuration = 0.05f;
    [SerializeField] private float _perfectDodgeVignetteFadeOutDuration = 0.05f;
    private Coroutine _perfectDodgeVignetteCoroutine;



    [Header("Parry Impact Settings")]
    [SerializeField] private float _parryImpactFreezeDuration = 0.2f;
    [SerializeField] private float _parryImpactRecoveryTimeScale = 0.15f;
    [SerializeField] private float _parryImpactRecoveryDuration = 0.15f;
    private float _previousParryImpactTimeScale;
    private float _previousParryImpactFixedDeltaTime;
    private bool _isParryImpactActive;      // 중복 호출 시 TimeScale과 DeltaTime이 오염되는것 방지

    [Header("Parry Camera Settings")]
    [SerializeField] private CinemachineCamera _parryVCam;
    // ParryVCam 의 CM 컴포넌트가 참조할 LookAt 선언
    [SerializeField] private Transform _parryVCamLookAt;
    [SerializeField, Range(0f, 1f)] private float _parryLookTargetRatio = 0.25f; // Hero와 Enemy 사이에서 카메라가 바라볼 지점
    [SerializeField] private float _parryCameraFollowSpeed = 7f;
    [SerializeField] private float _parryCameraRotationSpeed = 10f;
    private bool _isParryCameraActive;      // 패링 연출 카메라 활성화 여부
    private Coroutine _parryCameraCoroutine;
    private Hero _parryCameraOwner;        // 현재 패링 카메라를 사용하는 Hero


    [Header("Chain Selection Settings")]
    [SerializeField] private float _chainSelectionTimeScale = 0.2f;
    // 지속시간 필드는 ChainAttackManager에서 관리
    private float _previousChainTimeScale;
    private float _previousChainFixedDeltaTime;
    private bool _isChainSelectionSlowMotionActive;
    private Coroutine _parryTimeCoroutine;


    [Header("Chain QTE Camera Settings")]
    [SerializeField] private CinemachineCamera _chainQTEVCam;
    private bool _isChainCameraActive;


    [Header("Hit Impulse Settings")]
    [SerializeField] private float _lightHitImpulseForce = 0.25f;
    [SerializeField] private float _heavyHitImpulseForce = 0.5f;
    [SerializeField] private float _knockbackHitImpulseForce = 0.8f;


    [Header("SFX Assets")]
    [SerializeField] private AudioCueSO _parryImpactCue;
    [SerializeField] private AudioCueSO _perfectDodgeCue;
    [SerializeField] private AudioCueSO _chainEntryCue;





    private void Awake()
    {

        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    private void Start()
    {
        _mainCamera = Camera.main;

        // 게임 시작 시 연출용 카메라가 선택되지 않도록 대기 상태로 초기화
        if (_parryVCam != null)
        {
            _parryVCam.Priority = 0;

            if (_parryVCamLookAt != null)
            {
                _parryVCam.LookAt = _parryVCamLookAt;
            }
            else
            {
                Debug.LogError(
                    "[CombatPresentationManager] ParryVCamLookAt 참조가 누락되었습니다.",
                    this
                );
            }
        }
        if (_chainQTEVCam != null) _chainQTEVCam.Priority = 0;
    }

    private void OnDisable()
    {
        if (Instance != this) return;

        // 종료 시에는 남은 연출을 재개하지 않고 시간과 카메라를 정리함.
        SettingsManager.Instance?.CloseSettings();
        StopAllCoroutines();
        ResetUltPresentation();
        StopParryCamera();
        if (_isChainSelectionSlowMotionActive || _isChainCameraActive)
            EndChainSelectionPresentation();

        _isParryImpactActive = false;
        _isPerfectDodgeSlowMotionActive = false;
        _parryTimeCoroutine = null;
        _parryCameraCoroutine = null;
        _perfectDodgeSlowMotionCoroutine = null;
        _perfectDodgeVignetteCoroutine = null;
        _delayedPerfectDodgePresentationCoroutine = null;
        if (_perfectDodgeVolume != null) _perfectDodgeVolume.weight = 0f;
        _isPresenting = false;

        SetGamePaused(false);
        if (_hasPresentationTimeOverride)
        {
            Time.timeScale = _initialTimeScale;
            Time.fixedDeltaTime = _initialFixedDeltaTime;
            _hasPresentationTimeOverride = false;
        }
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }



    #region [게임 일시정지와 연출 시간]

    private bool _isGamePaused;
    private float _resumeTimeScale;
    private float _resumeFixedDeltaTime;
    // 비활성화 시 중단된 슬로우 모션이 남지 않도록 첫 시간 변경 전 값을 보존함.
    private bool _hasPresentationTimeOverride;
    private float _initialTimeScale;
    private float _initialFixedDeltaTime;
    // 일시정지 중 카메라도 게임 시간을 따르도록 설정하고, 재개 시 복원할 Brain 참조
    private CinemachineBrain _pausedBrain;      
    private bool _previousIgnoreTimeScale;

    public bool IsGamePaused => _isGamePaused;
    private float PresentationTimeScale => _isGamePaused ? _resumeTimeScale : Time.timeScale;
    private float PresentationFixedDeltaTime => _isGamePaused ? _resumeFixedDeltaTime : Time.fixedDeltaTime;

    /// <summary>
    /// 연출의 시간 상태를 보존하여 게임 일시정지를 적용하거나 해제함.
    /// </summary>
    public void SetGamePaused(bool paused)
    {
        if (_isGamePaused == paused) return;
        if (paused && !isActiveAndEnabled) return;

        if (paused)
        {
            _resumeTimeScale = Time.timeScale;
            _resumeFixedDeltaTime = Time.fixedDeltaTime;
            _isGamePaused = true;
            Time.timeScale = 0f;

            if (_mainCamera == null) _mainCamera = Camera.main;
            _pausedBrain = _mainCamera != null ? _mainCamera.GetComponent<CinemachineBrain>() : null;
            if (_pausedBrain != null)
            {
                _previousIgnoreTimeScale = _pausedBrain.IgnoreTimeScale;
                _pausedBrain.IgnoreTimeScale = false;
            }
            return;
        }

        _isGamePaused = false;
        Time.timeScale = _resumeTimeScale;
        Time.fixedDeltaTime = _resumeFixedDeltaTime;
        if (_pausedBrain != null) _pausedBrain.IgnoreTimeScale = _previousIgnoreTimeScale;
        _pausedBrain = null;
    }

    /// <summary>
    /// 연출 시간을 변경하되 일시정지 중에는 복귀할 값만 갱신함.
    /// </summary>
    private void SetPresentationTime(float timeScale, float fixedDeltaTime)
    {
        if (!_hasPresentationTimeOverride)
        {
            _initialTimeScale = PresentationTimeScale;
            _initialFixedDeltaTime = PresentationFixedDeltaTime;
            _hasPresentationTimeOverride = true;
        }
        if (_isGamePaused)
        {
            _resumeTimeScale = timeScale;
            _resumeFixedDeltaTime = fixedDeltaTime;
            return;
        }
        Time.timeScale = timeScale;
        Time.fixedDeltaTime = fixedDeltaTime;
    }

    /// <summary>
    /// 게임 일시정지 시간을 제외하고 실시간 기준으로 대기함.
    /// </summary>
    private IEnumerator WaitForPresentationSeconds(float duration)
    {
        float elapsedTime = 0f;
        while (_isGamePaused || elapsedTime < duration)
        {
            yield return null;
            if (_isGamePaused) continue;
            elapsedTime += Time.unscaledDeltaTime;
        }
    }

    #endregion

    #region [1. 궁극기 연출 제어 영역]

    /// <summary>
    /// 애니메이션 이벤트 요청으로 시전자와 카메라를 등록하고 궁극기 연출을 시작함.
    /// </summary>
    /// <param name="caster">궁극기를 시전하는 Hero</param>
    /// <param name="ultTimelineAsset">궁극기 타임라인 에셋</param>
    /// <param name="ultVcam">캐릭터가 가진 궁극기 전용 가상 카메라</param>
    /// <param name="duration">실시간 기준 연출 유지 상한(초). 상태 이탈 시 조기 종료함.</param>
    public void PlayUltCutscene(Hero caster, TimelineAsset ultTimelineAsset, CinemachineCamera ultVcam, float duration)
    {
        if (_isPresenting || caster == null || ultTimelineAsset == null || _playableDirector == null) return;

        _isPresenting = true;
        _isUltPresentationActive = true;
        _ultCaster = caster;
        _activeUltCamera = ultVcam;
        _playableDirector.playableAsset = ultTimelineAsset;

        // Canvas만 숨겨 HUD 스크립트와 이벤트 구독은 유지함.
        if (_combatHUDCanvas != null)
        {
            _wasCombatHUDEnabled = _combatHUDCanvas.enabled;
            _combatHUDCanvas.enabled = false;
        }

        StartCoroutine(ExecuteUltCutsceneRoutine(ultTimelineAsset, ultVcam, duration));
    }

    /// <summary>
    /// 궁극기 연출을 유지하고 시간 만료, 시전자 소멸·비활성화 또는 Ult 상태 이탈 시 정리함.
    /// </summary>
    private IEnumerator ExecuteUltCutsceneRoutine(TimelineAsset ultTimelineAsset, CinemachineCamera ultVcam, float duration)
    {
        const float slowTimeScale = 0.05f;

        try
        {
            // 일시정지 직후 도착한 애니메이션 이벤트는 재개 이후에 처리함.
            while (_isGamePaused) yield return null;
            if (_ultCaster == null || !_ultCaster.isActiveAndEnabled ||
                _ultCaster.CurrentState != HeroState.Ult) yield break;

            SetupUltPresentation(ultTimelineAsset, ultVcam, slowTimeScale, out _ultAnimator);

            float endTime = Time.realtimeSinceStartup + duration;
            while (Time.realtimeSinceStartup < endTime && _ultCaster != null &&
                   _ultCaster.isActiveAndEnabled && _ultCaster.CurrentState == HeroState.Ult)
            {
                yield return null;
            }
        }
        finally
        {
            ResetUltPresentation();
        }
    }

    /// <summary>
    /// 궁극기 카메라를 활성화하고 월드 감속, 시전자 배속 보정과 Timeline 바인딩·재생을 설정함.
    /// </summary>
    private void SetupUltPresentation(TimelineAsset ultTimelineAsset, CinemachineCamera ultVcam, float slowTimeScale, out Animator heroAnimator)
    {
        heroAnimator = null;

        // 조명은 Timeline에서 활성화하며 종료·중단 시 Manager가 최종 해제함.
        Transform lightingTransform = _ultCaster.transform.Find("Ult_Lighting");
        _ultLighting = lightingTransform != null ? lightingTransform.gameObject : null;

        if (ultVcam != null) ultVcam.Priority = UltVCamPriority;

        // 월드 시간과 물리 업데이트 간격을 함께 감속함.
        SetPresentationTime(slowTimeScale, 0.02f * slowTimeScale);

        // 카메라 Follow 대상의 Animator를 역배속 보정하여 실시간 기준 정상 속도를 유지함.
        if (ultVcam != null && ultVcam.Follow != null)
        {
            heroAnimator = ultVcam.Follow.GetComponent<Animator>();
            if (heroAnimator != null)
            {
                heroAnimator.speed = 1f / slowTimeScale;
            }
            else
            {
                Debug.Log("HeroAnimator 접근 실패");
            }

            if (_playableDirector != null)
            {
                if (ultTimelineAsset != null)
                {
                    _playableDirector.playableAsset = ultTimelineAsset;

                    // Signal, 시전자 카메라와 전용 조명을 각 Timeline 트랙에 연결함.
                    foreach (var track in ultTimelineAsset.GetOutputTracks())
                    {
                        if (track is ActivationTrack && track.name == "Ult_Lighting")
                        {
                            // null도 바인딩하여 이전 시전자의 조명 참조가 남지 않게 함.
                            _playableDirector.SetGenericBinding(track, _ultLighting);
                            if (_ultLighting == null)
                                Debug.LogWarning("[Presentation] 시전자의 Ult_Lighting을 찾을 수 없습니다.", _ultCaster);
                        }
                        if (track is SignalTrack)
                            _playableDirector.SetGenericBinding(track, GetComponent<SignalReceiver>());
                        if (track.name == "Animation Track" && ultVcam != null)
                        {
                            var vcamAnimator = ultVcam.GetComponent<Animator>();
                            _playableDirector.SetGenericBinding(track, vcamAnimator);
                        }
                    }
                }

                _playableDirector.Play();

                if (_playableDirector.playableGraph.IsValid())
                {
                    // GameTime으로 재생되는 Timeline도 역배속 보정하여 월드 감속을 상쇄함.
                    int rootPlayableCount = _playableDirector.playableGraph.GetRootPlayableCount();
                    for (int i = 0; i < rootPlayableCount; i++)
                    {
                        _playableDirector.playableGraph.GetRootPlayable(i).SetSpeed(1f / slowTimeScale);
                    }
                }
            }
            else
            {
                Debug.Log("PlayableDirector 접근 실패");
            }
        }
    }

    /// <summary>
    /// 정상 종료와 중단 경로에서 궁극기의 시간, 카메라, 렌더링, 조명 및 실행 상태를 일괄 정리함.
    /// </summary>
    private void ResetUltPresentation()
    {
        if (!_isUltPresentationActive) return;

        RestoreUltTime();

        // 중단 경로에서는 새 블렌드를 시작하지 않고 카메라를 반환함.
        if (_activeUltCamera != null) _activeUltCamera.Priority = 0;
        // Timeline 정지 후 렌더링 설정을 복구해 종료 평가의 영향을 제거함.
        if (_playableDirector != null) _playableDirector.Stop();
        ResetLayers();
        if (_ultLighting != null) _ultLighting.SetActive(false);
        if (_ultBlendOverridden && _ultBrain != null) _ultBrain.DefaultBlend = _previousUltBlend;

        if (_combatHUDCanvas != null) _combatHUDCanvas.enabled = _wasCombatHUDEnabled;

        _ultBlendOverridden = false;
        _ultBrain = null;
        _ultCaster = null;
        _ultAnimator = null;
        _activeUltCamera = null;
        _ultLighting = null;
        _isUltPresentationActive = false;
        _isPresenting = false;
    }

    #endregion


    #region [타임라인용 시그널 메서드 영역]

    /// <summary>
    /// Timeline Signal로 시전자의 몸체·무기만 전용 레이어에 표시하고 단색 배경을 적용함.
    /// </summary>
    public void TurnOffLayersForUlt()
    {
        if (_ultLayersHidden || !_isUltPresentationActive || _ultCaster == null) return;

        Camera renderCamera = Camera.main;
        int renderLayer = LayerMask.NameToLayer(_ultRenderLayerName);
        if (renderCamera == null || renderLayer < 0)
        {
            Debug.LogError("[Presentation] 궁극기 렌더링 카메라 또는 전용 레이어가 없습니다.", this);
            return;
        }

        HashSet<GameObject> renderObjects = new HashSet<GameObject>();
        CollectUltRenderObjects(_ultCaster.BodyRenderers, renderObjects);
        if (_ultCaster.Weapon != null)
            CollectUltRenderObjects(_ultCaster.Weapon.AllWeaponRenderers, renderObjects);

        if (renderObjects.Count == 0)
        {
            Debug.LogError("[Presentation] 궁극기 시전자의 Renderer 참조가 없습니다.", _ultCaster);
            return;
        }

        // 물리 판정 레이어가 함께 바뀌는 잘못된 참조는 적용 전에 거부함.
        foreach (GameObject renderObject in renderObjects)
        {
            if (renderObject == _ultCaster.gameObject || renderObject.GetComponent<Collider>() != null)
            {
                Debug.LogError("[Presentation] 궁극기 Renderer는 Hero 루트·Collider와 분리해야 합니다.", renderObject);
                return;
            }
        }

        _ultRenderCamera = renderCamera;
        _originalMask = renderCamera.cullingMask;
        _ultOriginalClearFlags = renderCamera.clearFlags;
        _ultOriginalBackgroundColor = renderCamera.backgroundColor;
        _ultLayersHidden = true;

        // Renderer의 표시 상태와 ParticleSystem 재생 상태는 변경하지 않음.
        foreach (GameObject renderObject in renderObjects)
        {
            _ultOriginalLayers.Add(renderObject, renderObject.layer);
            renderObject.layer = renderLayer;
        }

        renderCamera.cullingMask = 1 << renderLayer;
        renderCamera.clearFlags = CameraClearFlags.SolidColor;
        renderCamera.backgroundColor = _ultBackgroundColor;
    }

    /// <summary>
    /// 명시된 Renderer의 오브젝트를 중복 없이 수집하며 자식의 레이어는 변경하지 않음.
    /// </summary>
    private void CollectUltRenderObjects(IEnumerable<Renderer> renderers, HashSet<GameObject> renderObjects)
    {
        if (renderers == null) return;
        foreach (Renderer renderer in renderers)
        {
            if (renderer != null) renderObjects.Add(renderer.gameObject);
        }
    }

    /// <summary>
    /// Timeline Signal 또는 최종 정리에서 월드·Animator·Timeline 배속을 정상화하고 카메라는 유지함.
    /// </summary>
    public void RestoreUltTime()
    {
        if (!_isUltPresentationActive) return;

        SetPresentationTime(1f, 0.02f);
        if (_ultAnimator != null) _ultAnimator.speed = 1f;

        if (_playableDirector == null || !_playableDirector.playableGraph.IsValid()) return;

        var graph = _playableDirector.playableGraph;
        for (int i = 0; i < graph.GetRootPlayableCount(); i++)
            graph.GetRootPlayable(i).SetSpeed(1f);
    }

    /// <summary>
    /// Timeline Signal로 궁극기 카메라 우선순위를 내려 전투 카메라로 복귀함.
    /// </summary>
    public void ReleaseUltCamera()
    {
        if (_activeUltCamera == null) return;
        _ultBrain = Camera.main != null ? Camera.main.GetComponent<CinemachineBrain>() : null;
        if (_ultBrain != null && !_ultBlendOverridden)
        {
            // 기본 블렌드를 임시 변경하며 최종 정리에서 저장값으로 복구함.
            _previousUltBlend = _ultBrain.DefaultBlend;
            _ultBrain.DefaultBlend = new CinemachineBlendDefinition(CinemachineBlendDefinition.Styles.EaseInOut, 0.2f);
            _ultBlendOverridden = true;
        }
        _activeUltCamera.Priority = 0;
    }

    /// <summary>
    /// Timeline Signal 또는 최종 정리에서 원래 오브젝트 레이어와 카메라 배경·컬링 마스크를 복구함.
    /// </summary>
    public void ResetLayers()
    {
        if (!_ultLayersHidden) return;

        foreach (KeyValuePair<GameObject, int> entry in _ultOriginalLayers)
        {
            if (entry.Key != null) entry.Key.layer = entry.Value;
        }
        _ultOriginalLayers.Clear();

        // Camera.main이 변경되거나 시전자가 소멸해도 저장한 대상 기준으로 복구함.
        if (_ultRenderCamera != null)
        {
            _ultRenderCamera.cullingMask = _originalMask;
            _ultRenderCamera.clearFlags = _ultOriginalClearFlags;
            _ultRenderCamera.backgroundColor = _ultOriginalBackgroundColor;
        }
        _ultRenderCamera = null;
        _ultLayersHidden = false;
    }

    #endregion


    #region [2. 패링 / 히트스톱 연출 제어 영역]

    /// <summary>
    /// ParryIn 진입 시 패링 카메라를 시작하거나 새 Hero로 추적 대상을 갱신함.
    /// </summary>
    public void PlayParryCamera(Hero parryHero, Enemy attacker)
    {
        // 다른 연출은 보호하고, 기존 패링 연출의 대상 갱신은 허용함.
        if (_isPresenting && !_isParryCameraActive) return;
        if (_parryVCam == null || parryHero == null || attacker == null) return;

        Transform cameraAnchor = SelectParryCameraAnchor(parryHero, attacker);
        if (cameraAnchor == null) return;

        bool shouldSnap = !_isParryCameraActive;

        if (_parryCameraCoroutine != null)
        {
            StopCoroutine(_parryCameraCoroutine);
            _parryCameraCoroutine = null;
        }

        _parryCameraOwner = parryHero;
        _isPresenting = true;
        _isParryCameraActive = true;

        // 첫 진입만 즉시 배치하고, 연속 패링은 코루틴의 기존 보간으로 연결함.
        if (shouldSnap)
        {
            UpdateParryCameraPose(parryHero, attacker, cameraAnchor, true);
        }
        _parryVCam.Priority = ParryVCamPriority;

        _parryCameraCoroutine = StartCoroutine(
            ExecuteParryCameraRoutine(parryHero, attacker, cameraAnchor)
        );
    }

    /// <summary>
    /// 패링 직전 Enemy의 Viewport 에 따른 카메라 앵커 선택
    /// </summary>
    private Transform SelectParryCameraAnchor(Hero parryHero, Enemy attacker)
    {
        if (_mainCamera == null)
        {
            _mainCamera = Camera.main;
        }

        if (_mainCamera == null) return null;

        Vector3 enemyLookPoint =
            attacker.CamPivot.position;

        Vector3 attackerViewportPosition =
            _mainCamera.WorldToViewportPoint(enemyLookPoint);

        return attackerViewportPosition.x > 0.5f
            ? parryHero.ParryCameraAnchorRight
            : parryHero.ParryCameraAnchorLeft;

    }

    /// <summary>
    /// Hero의 카메라 앵커 위치로 패링 카메라를 이동시키고,
    /// Hero와 Enemy 사이를 바라보도록 회전
    /// </summary>
    private void UpdateParryCameraPose(
        Hero parryHero,
        Enemy attacker,
        Transform cameraAnchor,
        bool snap
    )
    {
        Vector3 heroLookPoint = parryHero.CamPivot.position;
        Vector3 enemyLookPoint = attacker.CamPivot.position;
        Vector3 finalLookPoint = Vector3.Lerp(heroLookPoint, enemyLookPoint, _parryLookTargetRatio);

        // 카메라가 바라보는 지점을 장애물 가림 판정에도 사용함.
        if (_parryVCamLookAt != null)
        {
            _parryVCamLookAt.position = finalLookPoint;
        }

        Vector3 targetPosition = cameraAnchor.position;
        Vector3 lookDirection = finalLookPoint - targetPosition;

        Quaternion targetRotation = lookDirection.sqrMagnitude > 0.01f
            ? Quaternion.LookRotation(lookDirection.normalized) : cameraAnchor.rotation;

        if (snap)
        {
            _parryVCam.transform.SetPositionAndRotation(targetPosition, targetRotation);
            _parryVCam.PreviousStateIsValid = false;
            return;
        }

        float positionLerp = 1f - Mathf.Exp(-_parryCameraFollowSpeed * Time.unscaledDeltaTime);
        float rotationLerp = 1f - Mathf.Exp(-_parryCameraRotationSpeed * Time.unscaledDeltaTime);

        Vector3 smoothedPosition = Vector3.Lerp(
            _parryVCam.transform.position,
            targetPosition,
            positionLerp
        );

        Quaternion smoothedRotation = Quaternion.Slerp(
            _parryVCam.transform.rotation,
            targetRotation,
            rotationLerp
        );

        _parryVCam.transform.SetPositionAndRotation(smoothedPosition, smoothedRotation);
    }

    /// <summary>
    /// 패링 카메라가 활성화된 동안 지정한 Hero와 공격자를 추적함.
    /// </summary>
    private IEnumerator ExecuteParryCameraRoutine(
        Hero parryHero,
        Enemy attacker,
        Transform cameraAnchor
    )
    {
        while (_isParryCameraActive)
        {
            if (parryHero == null || attacker == null || cameraAnchor == null)
            {
                StopParryCamera();
                yield break;
            }

            if (_isGamePaused)
            {
                yield return null;
                continue;
            }
            UpdateParryCameraPose(parryHero, attacker, cameraAnchor, false);
            yield return null;
        }
    }

    /// <summary>
    /// 현재 소유자의 패링 카메라 종료 요청을 처리하고 성공 여부를 반환함.
    /// </summary>
    public bool TryStopParryCamera(Hero requestingHero)
    {
        if (!_isParryCameraActive) return false;
        if (requestingHero == null) return false;
        if (requestingHero != _parryCameraOwner) return false;

        StopParryCamera();
        return true;
    }

    /// <summary>
    /// 패링 카메라와 소유자 정보를 정리하고 추적 중인 Gameplay 카메라로 블렌딩함.
    /// </summary>
    private void StopParryCamera()
    {
        if (!_isParryCameraActive) return;

        _isParryCameraActive = false;
        _parryCameraOwner = null;

        if (_parryCameraCoroutine != null)
        {
            StopCoroutine(_parryCameraCoroutine);
            _parryCameraCoroutine = null;
        }

        // Gameplay 카메라는 계속 추적하던 구도를 유지하며 Custom Blend로 복귀함.
        if (_parryVCam != null)
        {
            _parryVCam.Priority = 0;
        }

        _isPresenting = false;
    }

    /// <summary>
    /// 패링 성공 시 전용 타격 효과를 시작
    /// HitFrame - CombatManager - ResolveParryAssistSuccess 호출
    /// </summary>
    public void PlayParryImpact()
    {
        // CM Impulse 신호 생성
        _parryImpulseSource?.GenerateImpulse();
        PlayParryImpactSFX();

        if (_parryTimeCoroutine != null)
        {
            StopCoroutine(_parryTimeCoroutine);
        }

        if (!_isParryImpactActive)
        {
            _previousParryImpactTimeScale = PresentationTimeScale;
            _previousParryImpactFixedDeltaTime = PresentationFixedDeltaTime;
            _isParryImpactActive = true;
        }

        _parryTimeCoroutine = StartCoroutine(ExecuteParryImpactRoutine());
    }

    /// <summary>
    /// 게임 일시정지를 제외하고 패링 정지와 회복 슬로우를 진행함.
    /// </summary>
    private IEnumerator ExecuteParryImpactRoutine()
    {
        SetPresentationTime(0f, 0f);

        yield return WaitForPresentationSeconds(_parryImpactFreezeDuration);

        SetPresentationTime(_parryImpactRecoveryTimeScale, 0.02f * _parryImpactRecoveryTimeScale);

        yield return WaitForPresentationSeconds(_parryImpactRecoveryDuration);

        SetPresentationTime(_previousParryImpactTimeScale, _previousParryImpactFixedDeltaTime);

        _isParryImpactActive = false;
        _parryTimeCoroutine = null;
    }

    /// <summary>
    /// AudioManager 인스턴스를 거쳐서 지정된 AudioCue 재생
    /// </summary>
    private void PlayParryImpactSFX()
    {
        if (!isActiveAndEnabled || _parryImpactCue == null)
            return;

        AudioManager.Instance?.PlayOneShot(_parryImpactCue, transform.position);
    }

    #endregion


    #region [3. 극한 회피 연출 제어 영역]

    /// <summary>
    /// 극한 회피 발동시 슬로우 모션 + 비네트 후처리 연출 제어 메서드
    /// </summary>
    public void PlayPerfectDodgePresentation()
    {
        if (_isPresenting) return;

        if (_perfectDodgeSlowMotionCoroutine != null)
        {
            StopCoroutine(_perfectDodgeSlowMotionCoroutine);
        }

        if (_perfectDodgeVignetteCoroutine != null)
        {
            StopCoroutine(_perfectDodgeVignetteCoroutine);
        }

        if (!_isPerfectDodgeSlowMotionActive)
        {
            _previousDodgeTimeScale = PresentationTimeScale;
            _previousDodgeFixedDeltaTime = PresentationFixedDeltaTime;
            _isPerfectDodgeSlowMotionActive = true;
        }

        PlayPerfectDodgeSFX();
        _perfectDodgeSlowMotionCoroutine = StartCoroutine(ExecutePerfectDodgeSlowMotion());
        _perfectDodgeVignetteCoroutine = StartCoroutine(ExecutePerfectDodgeVignette());
    }

    /// <summary>
    /// 게임 일시정지를 제외하고 극한회피 슬로우 모션을 진행함.
    /// </summary>
    private IEnumerator ExecutePerfectDodgeSlowMotion()
    {
        SetPresentationTime(_perfectDodgeTimeScale, 0.02f * _perfectDodgeTimeScale);

        yield return WaitForPresentationSeconds(_perfectDodgeDuration);

        SetPresentationTime(_previousDodgeTimeScale, _previousDodgeFixedDeltaTime);

        _isPerfectDodgeSlowMotionActive = false;
        _perfectDodgeSlowMotionCoroutine = null;
    }

    /// <summary>
    /// 게임 일시정지 중에는 현재 농도를 유지하고 비네트 진입과 해제를 진행함.
    /// </summary>
    private IEnumerator ExecutePerfectDodgeVignette()
    {
        if (_perfectDodgeVolume == null) yield break;

        _perfectDodgeVolume.weight = 0f;

        float elapsedTime = 0f;

        // 비네트 빠른 진입
        while (elapsedTime < _perfectDodgeVignetteFadeInDuration)
        {
            if (_isGamePaused)
            {
                yield return null;
                continue;
            }
            elapsedTime += Time.unscaledDeltaTime;
            _perfectDodgeVolume.weight = Mathf.Clamp01(
                elapsedTime / _perfectDodgeVignetteFadeInDuration
            );
            yield return null;
        }

        _perfectDodgeVolume.weight = 1f;

        float holdDuration = Mathf.Max(
            0f,
            _perfectDodgeDuration -
            _perfectDodgeVignetteFadeInDuration -
            _perfectDodgeVignetteFadeOutDuration
        );

        yield return WaitForPresentationSeconds(holdDuration);

        elapsedTime = 0f;

        // 비네트 부드러운 해제
        while (elapsedTime < _perfectDodgeVignetteFadeOutDuration)
        {
            if (_isGamePaused)
            {
                yield return null;
                continue;
            }
            elapsedTime += Time.unscaledDeltaTime;
            _perfectDodgeVolume.weight = 1f - Mathf.Clamp01(
                elapsedTime / _perfectDodgeVignetteFadeOutDuration
            );
            yield return null;
        }

        _perfectDodgeVolume.weight = 0f;
        _perfectDodgeVignetteCoroutine = null;
    }

    /// <summary>
    /// HeroMechanic의 TryEvadeHit에서 요청한 극한회피 연출을 게임 시간 기준으로 지연 실행함.
    /// </summary>
    /// <param name="delay">연출 시작까지 기다릴 게임 시간(초). 0 이하는 즉시 실행함.</param>
    public void PlayDelayedPerfectDodgePresentation(float delay)
    {
        if (_delayedPerfectDodgePresentationCoroutine != null)
        {
            StopCoroutine(_delayedPerfectDodgePresentationCoroutine);
            _delayedPerfectDodgePresentationCoroutine = null;
        }

        if (delay <= 0f)
        {
            PlayPerfectDodgePresentation();
            return;
        }

        _delayedPerfectDodgePresentationCoroutine =
            StartCoroutine(ExecuteDelayedPerfectDodgePresentation(delay));
    }

    /// <summary>
    /// 지정한 게임 시간만큼 대기한 뒤 기존 극한회피 연출을 실행함.
    /// </summary>
    private IEnumerator ExecuteDelayedPerfectDodgePresentation(float delay)
    {
        yield return new WaitForSeconds(delay);

        _delayedPerfectDodgePresentationCoroutine = null;
        PlayPerfectDodgePresentation();
    }

    /// <summary>
    /// AudioManager 인스턴스를 거쳐서 지정된 AudioCue 재생
    /// </summary>
    private void PlayPerfectDodgeSFX()
    {
        if (!isActiveAndEnabled || _perfectDodgeCue == null)
            return;

        AudioManager.Instance?.PlayOneShot(_perfectDodgeCue, transform.position);
    }

    #endregion


    #region [4. Chain Attack 연출 제어 영역]

    /// <summary>
    /// Chain QTE Selecting 연출 시작
    /// </summary>
    public void StartChainSelectionPresentation(Enemy target, Hero triggerHero)
    {
        if (target == null || triggerHero == null) return;

        // Chain QTE Entry SFX 재생
        AudioManager.Instance?.PlayOneShot(_chainEntryCue, triggerHero.transform.position);

        // Chain 카메라 활성화 및 구도 설정
        StartChainSelectionCamera(target, triggerHero);

        // 슬로우 모션 연출 시작
        StartChainSelectionSlowMotion();

        // 데미지 폰트 출력 중단 및 활성화된 오브젝트 비활성화
        DamageTextManager.Instance?.SetOutputSuppressed(true);

        // 추후 비네트 시작 추가
        // -> CM 컴포넌트로 작동
    }

    /// <summary>
    /// Chain QTE Selecting 연출 종료
    /// 슬로우모션·DamageText·전용 카메라 종료
    /// </summary>
    public void EndChainSelectionPresentation()
    {
        // 슬로우 모션 연출 종료
        StopChainSelectionSlowMotion();

        // 데미지 폰트 출력 시작
        DamageTextManager.Instance?.SetOutputSuppressed(false);

        // Selecting 전용 카메라 종료
        if (_chainQTEVCam != null)
        {
            _chainQTEVCam.Priority = 0;
        }

        _isChainCameraActive = false;

        if (_gameplayVCam != null)
        {
            _gameplayVCam.EndPresentationFocus();
        }

        // 추후 비네트 종료 추가
    }

    /// <summary>
    /// Chain QTE Selecting 슬로우 모션 시작
    /// </summary>
    public void StartChainSelectionSlowMotion()
    {
        if (_isChainSelectionSlowMotionActive) return;

        _previousChainTimeScale = PresentationTimeScale;
        _previousChainFixedDeltaTime = PresentationFixedDeltaTime;
        _isChainSelectionSlowMotionActive = true;

        SetPresentationTime(_chainSelectionTimeScale, 0.02f * _chainSelectionTimeScale);
    }

    /// <summary>
    /// Chain QTE Selecting 슬로우모션 종료 및 기존 시간 복구
    /// </summary>
    public void StopChainSelectionSlowMotion()
    {
        if (!_isChainSelectionSlowMotionActive) return;

        SetPresentationTime(_previousChainTimeScale, _previousChainFixedDeltaTime);

        _isChainSelectionSlowMotionActive = false;
    }

    /// <summary>
    /// ChainAttack_VCam 활성화 및 Selecting 구도 적용
    /// </summary>
    private void StartChainSelectionCamera(Enemy target, Hero triggerHero)
    {
        if (_chainQTEVCam == null || triggerHero == null) return;

        // ChainAttack 카메라 추적 대상을 현재 TriggerHero로 동기화
        SetChainCameraTarget(triggerHero);

        // 현재 실제 Gameplay 카메라 시점을 Chain 카메라 시작 시점으로 동기화


        if (!_isChainCameraActive)
        {
            // Chain 종료 후 Gameplay Camera 복귀 방향 동기화
            _gameplayVCam?.BeginPresentationFocus(target.CamPivot);
            _isChainCameraActive = true;
        }

        _chainQTEVCam.Priority = ChainQTEVCamPriority;
    }

    /// <summary>
    /// ChainAttack 카메라의 추적 대상을 TriggerHero로 동기화
    /// </summary>
    private void SetChainCameraTarget(Hero triggerHero)
    {
        if (_chainQTEVCam == null || triggerHero == null) return;

        Transform camPivot = triggerHero.transform.Find("CamPivot");

        if (camPivot == null)
        {
            camPivot = triggerHero.transform;
        }

        _chainQTEVCam.Follow = camPivot;
        _chainQTEVCam.LookAt = camPivot;
    }

    #endregion


    #region [5. Camera Hit Impulse 연출 제어 영역]

    /// <summary>
    /// 타격 성공 시 카메라 흔들림 연출을 
    /// 발생시킬 외부 공개용 메서드
    /// </summary>
    public void PlayHitImpulse(HitType hitType)
    {
        if (_hitImpulseSource == null) return;

        float impulseForce = GetHitImpulseForce(hitType);

        if (impulseForce <= 0f) return;

        _hitImpulseSource.GenerateImpulseWithForce(impulseForce);
    }

    /// <summary>
    /// HitType 에 따라 Impulse Force 가공 메서드
    /// </summary>
    /// <param name="hitType"></param>
    /// <returns></returns>
    private float GetHitImpulseForce(HitType hitType)
    {
        switch (hitType)
        {
            case HitType.None:
                return 0f;

            case HitType.Light:
                return _lightHitImpulseForce;

            case HitType.Heavy:
                return _heavyHitImpulseForce;

            case HitType.Knockback:
                return _knockbackHitImpulseForce;

            default:
                return 0f;
        }
    }

    #endregion


    
}
