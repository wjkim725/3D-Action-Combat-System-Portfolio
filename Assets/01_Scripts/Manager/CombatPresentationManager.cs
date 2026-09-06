using System.Collections;
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

    [Header("Gameplay Camera")]
    [SerializeField] private CameraController _gameplayVCam;

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


    [Header("Perfect Dodge Vignette Settings")]
    [SerializeField] private Volume _perfectDodgeVolume;
    [SerializeField] private float _perfectDodgeVignetteFadeInDuration = 0.05f;
    [SerializeField] private float _perfectDodgeVignetteFadeOutDuration = 0.1f;
    private Coroutine _perfectDodgeVignetteCoroutine;



    [Header("Parry Impact Settings")]
    [SerializeField] private float _parryImpactFreezeDuration = 0.1f;
    [SerializeField] private float _parryImpactRecoveryTimeScale = 0.2f;
    [SerializeField] private float _parryImpactRecoveryDuration = 0.15f;
    private float _previousParryImpactTimeScale;
    private float _previousParryImpactFixedDeltaTime;
    private bool _isParryImpactActive;      // 중복 호출 시 TimeScale과 DeltaTime이 오염되는것 방지

    [Header("Parry Camera Settings")]
    [SerializeField] private CinemachineCamera _parryVCam;
    [SerializeField] private float _parryLookHeight = 1.2f;      // Hero와 Enemy의 발이 아닌 상체를 바라보도록 높이 보정
    [SerializeField, Range(0f, 1f)] private float _parryLookTargetRatio = 0.16f; // Hero와 Enemy 사이에서 카메라가 바라볼 지점
    [SerializeField] private float _parryCameraFollowSpeed = 15f;
    [SerializeField] private float _parryCameraRotationSpeed = 18f;
    private bool _isParryCameraActive;      // 패링 연출 카메라 활성화 여부
    private Coroutine _parryCameraCoroutine;


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





    private void Awake()
    {

        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    private void Start()
    {
        _mainCamera = Camera.main;

        // 게임 시작 시 연출용 카메라가 선택되지 않도록 대기 상태로 초기화
        if (_parryVCam != null) _parryVCam.Priority = 0;
        if (_chainQTEVCam != null) _chainQTEVCam.Priority = 0;
    }

    

    #region [1. 궁극기 연출 제어 영역]

    /// <summary>
    /// 플레이어 캐릭터의 궁극기 숏컷신 연출을 시작합니다.
    /// </summary>
    /// <param name="caster">궁극기를 시전하는 영웅 본체 (추가)</param>
    /// <param name="ultTimelineAsset">궁극기 타임라인 에셋</param>
    /// <param name="ultVcam">캐릭터가 가진 궁극기 전용 가상 카메라</param>
    /// <param name="duration">컷신 연출 시간</param>
    public void PlayUltCutscene(Hero caster, TimelineAsset ultTimelineAsset, CinemachineCamera ultVcam, float duration)
    {
        if (_isPresenting) return;

        _isPresenting = true;
        _playableDirector.playableAsset = ultTimelineAsset;

        // [동적 바인딩 보강 로직]
        // 시전자(caster)의 자식 중에서 "Ult_Background"라는 이름을 가진 오브젝트 찾기
        Transform ultBgTransform = caster.transform.Find("Ult_Background");

        if (ultBgTransform != null)
        {
            GameObject ultBgObject = ultBgTransform.gameObject;

            // 타임라인 에셋 내부의 모든 출력 트랙(Output Track)들을 순회
            foreach (var output in ultTimelineAsset.outputs)
            {
                // 타임라인 에디터 창에 적힌 트랙 이름이 "Ult_Background" 혹은 "Activation Track" 인지 확인
                if (output.streamName == "Ult_Background" || output.streamName == "Activation Track")
                {
                    // 현재 시전자의 Ult_Background 오브젝트를 해당 트랙에 바인딩
                    _playableDirector.SetGenericBinding(output.sourceObject, ultBgObject);
                    //Debug.Log($"[Presentation] {caster.gameObject.name}의 Ult_Background를 타임라인에 동적 바인딩 성공!");
                    break;
                }
            }
        }
        else
        {
            Debug.LogWarning($"[Presentation] {caster.gameObject.name} 자식에서 Ult_Background를 찾을 수 없습니다.");
        }

        StartCoroutine(ExecuteUltCutsceneRoutine(ultTimelineAsset, ultVcam, duration));
    }

    private IEnumerator ExecuteUltCutsceneRoutine(TimelineAsset ultTimelineAsset, CinemachineCamera ultVcam, float duration)
    {
        _isPresenting = true;

        float slowTimeScale = 0.05f;
        Animator heroAnimator = null;

        // 연출 시작 전반부 셋업을 수행
        SetupUltPresentation(ultTimelineAsset, ultVcam, slowTimeScale, out heroAnimator);

        yield return new WaitForSecondsRealtime(duration);

        // 연출 종료 후반부 원상 복구를 수행
        ResetUltPresentation(ultVcam, heroAnimator);

        _isPresenting = false;
    }
        
    /// <summary>
    /// 궁극기 연출 시작에 필요한 시간 감속, 애니메이터 보정, 타임라인 조립을 총괄합니다.
    /// </summary>
    private void SetupUltPresentation(TimelineAsset ultTimelineAsset, CinemachineCamera ultVcam, float slowTimeScale, out Animator heroAnimator)
    {
        heroAnimator = null;

        if (ultVcam != null) ultVcam.Priority = UltVCamPriority;

        // 게임 전체 슬로우 모션
        Time.timeScale = slowTimeScale;
        Time.fixedDeltaTime = 0.02f * Time.timeScale;

        // 플레이어의 애니메이터를 찾아서 정상 속도(1배속)로 돌도록 펌핑
        // 0.05(timeScale) * 20.0f(Animator.speed) = 1.0f (실제 체감 정상 속도)
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
                // 캐릭터가 건네준 새로운 타임라인 에셋을 플레이어 기기에 갈아끼웁니다.
                if (ultTimelineAsset != null)
                {
                    _playableDirector.playableAsset = ultTimelineAsset;

                    // 타임라인 에셋 내부의 트랙들을 순회하며 카메라 트랙을 찾아 자동 조립합니다.
                    foreach (var track in ultTimelineAsset.GetOutputTracks())
                    {
                        // 타임라인 에디터에서 만든 트랙 이름이 Animation Track일 때 바인딩을 수행합니다.
                        if (track.name == "Animation Track" && ultVcam != null)
                        {
                            var vcamAnimator = ultVcam.GetComponent<Animator>();
                            _playableDirector.SetGenericBinding(track, vcamAnimator);
                            break;
                        }
                    }
                }

                _playableDirector.Play();

                if (_playableDirector.playableGraph.IsValid())
                {
                    // 하드코딩 방지 : 그래프 내의 모든 최상위 루트 노드 개수를 파악하여 루프로 속도를 조절합니다.
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
    /// 궁극기 연출이 끝난 후 모든 게임 오브젝트와 시간, 타임라인 배속을 원래대로 되돌립니다.
    /// </summary>
    private void ResetUltPresentation(CinemachineCamera ultVcam, Animator heroAnimator)
    {
        // 모든 연출 원상 복구
        if (ultVcam != null) ultVcam.Priority = 0;
        Time.timeScale = 1f;
        Time.fixedDeltaTime = 0.02f;

        // HeroAnimator & PlayableDirector 재생 속도 원상복구
        if (heroAnimator != null)
        {
            heroAnimator.speed = 1f;
        }
        if (_playableDirector != null && _playableDirector.playableGraph.IsValid())
        {
            // 복구할 때도 루프를 돌며 모든 루트 노드의 속도를 안전하게 1배속으로 되돌립니다.
            int rootPlayableCount = _playableDirector.playableGraph.GetRootPlayableCount();
            for (int i = 0; i < rootPlayableCount; i++)
            {
                _playableDirector.playableGraph.GetRootPlayable(i).SetSpeed(1f);
            }
        }
    }

    #endregion


    #region [2. 패링 / 히트스톱 연출 제어 영역 (추후 확장)]

    /// <summary>
    /// 패링 성공 시 전용 카메라 연출을 시작
    /// Hero-EnterState-ParryIn 호출
    /// </summary>
    public void PlayParryCamera(Hero parryHero, Enemy attacker)
    {
        if (_isPresenting) return;
        if (_parryVCam == null || parryHero == null || attacker == null) return;
        if (parryHero.ParryCameraAnchor == null) return;

        if (_parryCameraCoroutine != null)
        {
            StopCoroutine(_parryCameraCoroutine);
        }

        // GamePlayVCam - _presentationLookTarget 초기화
        _gameplayVCam?.BeginPresentationFocus(attacker.transform);

        _isPresenting = true;
        _isParryCameraActive = true;

        UpdateParryCameraPose(parryHero, attacker, true);
        _parryVCam.Priority = ParryVCamPriority;

        _parryCameraCoroutine = StartCoroutine(ExecuteParryCameraRoutine(parryHero, attacker));
    }

    /// <summary>
    /// Hero의 카메라 앵커 위치로 패링 카메라를 이동시키고,
    /// Hero와 Enemy 사이를 바라보도록 회전
    /// </summary>
    private void UpdateParryCameraPose(Hero parryHero, Enemy attacker, bool snap)
    {
        Transform cameraAnchor = parryHero.ParryCameraAnchor;

        Vector3 heroLookPoint = parryHero.transform.position + Vector3.up * _parryLookHeight;
        Vector3 enemyLookPoint = attacker.transform.position + Vector3.up * _parryLookHeight;
        Vector3 finalLookPoint = Vector3.Lerp(heroLookPoint, enemyLookPoint, _parryLookTargetRatio);

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
    /// 패링 카메라 Priority를 일정 시간 동안 높인 뒤 일반 게임 카메라로 복귀
    /// </summary>
    private IEnumerator ExecuteParryCameraRoutine(Hero parryHero, Enemy attacker)
    {
        while (_isParryCameraActive)
        {
            if (parryHero == null || attacker == null)
            {
                StopParryCamera();
                yield break;
            }

            UpdateParryCameraPose(parryHero, attacker, false);
            yield return null;
        }
    }

    /// <summary>
    /// 패링 연출 카메라 종료 메서드
    /// </summary>
    public void StopParryCamera()
    {
        _isParryCameraActive = false;

        if (_parryCameraCoroutine != null)
        {
            StopCoroutine(_parryCameraCoroutine);
            _parryCameraCoroutine = null;
        }

        if (_gameplayVCam != null)
        {
            _gameplayVCam.EndPresentationFocus();
        }

        // 실제 화면 복귀는 CM Brain의 Custom Blend가 처리
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

        if (_parryTimeCoroutine != null)
        {
            StopCoroutine(_parryTimeCoroutine);
        }

        if (!_isParryImpactActive)
        {
            _previousParryImpactTimeScale = Time.timeScale;
            _previousParryImpactFixedDeltaTime = Time.fixedDeltaTime;
            _isParryImpactActive = true;
        }

        _parryTimeCoroutine = StartCoroutine(ExecuteParryImpactRoutine());
    }

    private IEnumerator ExecuteParryImpactRoutine()
    {
        Time.timeScale = 0f;
        Time.fixedDeltaTime = 0f;

        yield return new WaitForSecondsRealtime(_parryImpactFreezeDuration);

        Time.timeScale = _parryImpactRecoveryTimeScale;
        Time.fixedDeltaTime = 0.02f * Time.timeScale;

        yield return new WaitForSecondsRealtime(_parryImpactRecoveryDuration);

        Time.timeScale = _previousParryImpactTimeScale;
        Time.fixedDeltaTime = _previousParryImpactFixedDeltaTime;

        _isParryImpactActive = false;
        _parryTimeCoroutine = null;
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
            _previousDodgeTimeScale = Time.timeScale;
            _previousDodgeFixedDeltaTime = Time.fixedDeltaTime;
            _isPerfectDodgeSlowMotionActive = true;
        }

        _perfectDodgeSlowMotionCoroutine = StartCoroutine(ExecutePerfectDodgeSlowMotion());
        _perfectDodgeVignetteCoroutine = StartCoroutine(ExecutePerfectDodgeVignette());
    }

    /// <summary>
    /// 슬로우 모션 코루틴
    /// </summary>
    private IEnumerator ExecutePerfectDodgeSlowMotion()
    {
        Time.timeScale = _perfectDodgeTimeScale;
        Time.fixedDeltaTime = 0.02f * Time.timeScale;

        yield return new WaitForSecondsRealtime(_perfectDodgeDuration);

        Time.timeScale = _previousDodgeTimeScale;
        Time.fixedDeltaTime = _previousDodgeFixedDeltaTime;

        _isPerfectDodgeSlowMotionActive = false;
        _perfectDodgeSlowMotionCoroutine = null;
    }

    /// <summary>
    /// 비네트 포스트 프로세싱 적용 코루틴
    /// Volume 컴포넌트의 Weight를 0 - 1 전환하는 과정으로 On/Off 처리
    /// </summary>
    private IEnumerator ExecutePerfectDodgeVignette()
    {
        if (_perfectDodgeVolume == null) yield break;

        _perfectDodgeVolume.weight = 0f;

        float elapsedTime = 0f;

        // 비네트 빠른 진입
        while (elapsedTime < _perfectDodgeVignetteFadeInDuration)
        {
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

        yield return new WaitForSecondsRealtime(holdDuration);

        elapsedTime = 0f;

        // 비네트 부드러운 해제
        while (elapsedTime < _perfectDodgeVignetteFadeOutDuration)
        {
            elapsedTime += Time.unscaledDeltaTime;
            _perfectDodgeVolume.weight = 1f - Mathf.Clamp01(
                elapsedTime / _perfectDodgeVignetteFadeOutDuration
            );
            yield return null;
        }

        _perfectDodgeVolume.weight = 0f;
        _perfectDodgeVignetteCoroutine = null;
    }

    #endregion


    #region [4. Chain Attack 연출 제어 영역]

    /// <summary>
    /// Chain QTE Selecting 연출 시작
    /// </summary>
    public void StartChainSelectionPresentation(Enemy target, Hero triggerHero)
    {
        if (target == null || triggerHero == null) return;

        // Chain 카메라 활성화 및 구도 설정
        StartChainSelectionCamera(target, triggerHero);

        // 슬로우 모션 연출 시작
        StartChainSelectionSlowMotion();

        // 데미지 폰트 출력 중단 및 활성화된 오브젝트 비활성화
        DamageTextManager.Instance?.SetOutputSuppressed(true);

        // 추후 비네트 시작 추가
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

        _previousChainTimeScale = Time.timeScale;
        _previousChainFixedDeltaTime = Time.fixedDeltaTime;
        _isChainSelectionSlowMotionActive = true;

        Time.timeScale = _chainSelectionTimeScale;
        Time.fixedDeltaTime = 0.02f * Time.timeScale;
    }

    /// <summary>
    /// Chain QTE Selecting 슬로우모션 종료 및 기존 시간 복구
    /// </summary>
    public void StopChainSelectionSlowMotion()
    {
        if (!_isChainSelectionSlowMotionActive) return;

        Time.timeScale = _previousChainTimeScale;
        Time.fixedDeltaTime = _previousChainFixedDeltaTime;

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
            _gameplayVCam?.BeginPresentationFocus(target.transform);
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


    #region [타임라인용 시그널 메서드 영역]

    // 궁극기 연출 시작 시 호출
    // 컬링 마스크로 레이어 필터링 후 렌더링
    public void TurnOffLayersForUlt()
    {
        _mainCamera = Camera.main;

        if (_mainCamera == null) return;

        // 기존 컬링 마스크 상태 저장
        _originalMask = _mainCamera.cullingMask;

        // 빼고 싶은 레이어 이름들을 지정 (예: "Enemy", "Environment")
        int enemyLayer = LayerMask.NameToLayer("Enemy");
        int envLayer = LayerMask.NameToLayer("Environment");

        // 비트 연산으로 특정 레이어만 꺼버리기 (~ 연산자 사용)
        _mainCamera.cullingMask &= ~(1 << enemyLayer);
        _mainCamera.cullingMask &= ~(1 << envLayer);
    }
    // 궁극기 연출 종료 시 호출
    public void ResetLayers()
    {
        if (_mainCamera == null) return;

        // 원래대로 복구
        _mainCamera.cullingMask = _originalMask;
    }

    #endregion
}