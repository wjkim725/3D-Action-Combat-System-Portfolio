using System.Collections;
using UnityEngine;
using static HeroSwapSystem;

public class PartySwapManager : MonoBehaviour
{
    /// <summary>
    /// 일반 교대 실행에 필요한 값을 검증 완료 후 묶어서 전달
    /// </summary>
    private readonly struct SwapContext
    {
        public Hero OutHero { get; }
        public Hero InHero { get; }
        public SwapDirection Direction { get; }
        public HeroSwapSystem.SwapType SwapType { get; }
        public Enemy ActionTarget { get; }
        public Vector3 OutHeroPosition { get; }
        public Quaternion OutHeroRotation { get; }

        public SwapContext(
            Hero outHero,
            Hero inHero,
            SwapDirection direction,
            HeroSwapSystem.SwapType swapType,
            Enemy actionTarget)
        {
            OutHero = outHero;
            InHero = inHero;
            Direction = direction;
            SwapType = swapType;
            ActionTarget = actionTarget;
            OutHeroPosition = outHero.transform.position;
            OutHeroRotation = outHero.transform.rotation;
        }
    }

    public static PartySwapManager Instance { get; private set; }

    [SerializeField] private CameraController _cameraController;
    [SerializeField] private PartyStateManager _partyStateManager;


    [Header("Parry Assist Entrance Position Setting")]
    [SerializeField] private float _parrySpawnDistance = 1.5f;

    [Header("Chain Attack Entrance Position Setting")]
    [SerializeField] private float _chainSpawnDistance = 1.8f;

    [SerializeField] private float _quickAssistSpawnDistance = 7.0f;

    private bool _isInputEventSubscribed;


    #region [프로퍼티 영역]

    // 기존 외부 참조 코드 호환용 프로퍼티
    // 파티 상태의 실제 소유자는 PartyStateManager
    // PartyStateManager의 속성에 직접 접근해서 사용해도 되나,
    // 기존 코드와의 호환성을 위해 유지하는 대신 접근제어자 private
    private Hero[] PartyMembers =>
        _partyStateManager != null ? _partyStateManager.PartyMembers : null;

    private Hero CurrentHero =>
        _partyStateManager != null ? _partyStateManager.CurrentHero : null;

    #endregion


    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        ResolvePartyStateManager();
    }

    private void Start()
    {
        if (_partyStateManager == null)
        {
            Debug.LogError("PartyStateManager Instance Error");
            return;
        }

        if (InputManager.Instance == null)
        {
            Debug.LogError("InputManager Instance Error");
            return;
        }

        SubscribeInputEvents();

        StopAllCoroutines();
        StartCoroutine(SetPartyState());
    }

    private void OnDestroy()
    {
        UnsubscribeInputEvents();

        if (Instance == this)
        {
            Instance = null;
        }
    }

    public IEnumerator SetPartyState()
    {
        yield return new WaitForSeconds(0.01f);

        Hero[] partyMembers = PartyMembers;
        if (partyMembers == null || partyMembers.Length == 0) yield break;

        for (int i = 0; i < partyMembers.Length; i++)
        {
            Hero hero = partyMembers[i];
            if (hero == null) continue;

            if (i == _partyStateManager.CurrentHeroIndex)
            {
                // 현재 조작 Hero 등록 후 초기 소프트 록온 타겟 탐색
                if (LockOnManager.Instance != null)
                {
                    LockOnManager.Instance.RegisterActiveHero(hero);
                    LockOnManager.Instance.RefreshSoftLockTarget();
                }

                hero.SwapSystem.PrepareSwapInEntrance();
                hero.EnableInput();
            }
            else
            {
                hero.ChangeState(HeroState.InActive);
                hero.DisableInput();
            }
        }

        // 모든 파티원 초기화 완료 후 HUD 갱신
        _partyStateManager.NotifyCurrentHeroChanged();
    }


    #region [Input Event Handler 영역]

    /// <summary>
    /// 정방향 교대 입력 Input Event Handler
    /// </summary>
    private void HandleSwapNextPerformedInput()
    {
        TryRequestSwap(SwapDirection.Next);
    }

    /// <summary>
    /// 역방향 교대 입력 Input Event Handler
    /// </summary>
    private void HandleSwapPrevPerformedInput()
    {
        TryRequestSwap(SwapDirection.Prev);
    }

    /// <summary>
    /// 교대 방향에 따라 교대 대상 탐색 + 어떤 교대 타입으로 보낼지 판단
    /// </summary>
    private void TryRequestSwap(SwapDirection direction)
    {
        // QuickAssist 교대 우선 판정
        QuickAssistManager quickAssistManager = QuickAssistManager.Instance;

        if (quickAssistManager != null && quickAssistManager.IsWindowOpen)
        {
            TryRequestQuickAssist();
            return;
        }

        // QuickAssist 아닐 때 패링·일반 교대 판정
        HeroSwapSystem.SwapType swapType = DetermineStandardSwapType();

        if (!CurrentHeroCanRequestSwap(swapType)) return;

        Hero outHero = CurrentHero;
        Hero inHero = _partyStateManager.GetValidSwapHero(direction);

        if (inHero == null)
        {
            Debug.Log("교대 불가 : 교대 가능 Hero 없음");
            return;
        }

        if (!TryResolveParryTarget(
            swapType,
            inHero,
            out Enemy parryTarget))
        {
            return;
        }

        // 모든 교대 조건 검증이 끝난 후 실행용 데이터 구성
        SwapContext context = new SwapContext(
            outHero,
            inHero,
            direction,
            swapType,
            parryTarget
        );

        ExecuteSwap(context);
    }

    /// <summary>
    /// 현재 파티와 조작 Hero가 교대 요청 가능한 상태인지 검사
    /// </summary>
    private bool CurrentHeroCanRequestSwap(HeroSwapSystem.SwapType swapType)
    {
        // ChainAttack Executing Phase에서도 교대입력 차단
        // SwapSystem에서도 차단하고 있긴 함.
        if (ChainAttackManager.Instance != null && ChainAttackManager.Instance.CurrentPhase != ChainAttackPhase.Inactive)
            return false;

        if (_partyStateManager == null) return false;
        if (PartyMembers == null || PartyMembers.Length <= 1) return false;
        if (CurrentHero == null || CurrentHero.SwapSystem == null) return false;

        if (swapType == HeroSwapSystem.SwapType.Parry)
        {
            return CurrentHero.SwapSystem.CanRequestParry;
        }

        return CurrentHero.SwapSystem.CanRequestSwap;
    }

    /// <summary>
    /// 패링 교대라면 확정된 패링 대상을 검증하고 반환
    /// 일반·빠른지원 교대에서는 별도 대상 없이 통과
    /// </summary>
    private bool TryResolveParryTarget(HeroSwapSystem.SwapType swapType, Hero inHero, out Enemy parryTarget)
    {
        parryTarget = null;

        if (swapType != HeroSwapSystem.SwapType.Parry)
            return true;

        CombatManager combatManager = CombatManager.Instance;

        if (combatManager == null ||
            !combatManager.TryConsumeParryAssistRequest(out parryTarget))
        {
            Debug.LogWarning(
                $"[Parry Assist] 패링 기회가 이미 소비됐거나 " +
                $"{inHero.name}의 ParryTarget을 찾지 못했습니다."
            );

            return false;
        }

        return true;
    }

    private bool TryRequestQuickAssist()
    {
        QuickAssistManager manager = QuickAssistManager.Instance;

        if (manager == null || _partyStateManager == null)
        {
            return false;
        }

        if (!manager.TryGetExecutionData(
            out QuickAssistContext quickContext))
        {
            return false;
        }

        Hero outHero = CurrentHero;
        Hero inHero = quickContext.AssistHero;

        if (outHero == null ||
            outHero.Status == null ||
            outHero.Status.IsDead ||
            outHero.SwapSystem == null)
        {
            return false;
        }

        if (inHero == null ||
            inHero == outHero ||
            inHero.SwapSystem == null)
        {
            return false;
        }

        // 기존 OffFieldAction 강제 종료
        ForceEndOffFieldAction(inHero);

        SwapContext swapContext = new SwapContext(
            outHero,
            inHero,
            quickContext.Direction,
            HeroSwapSystem.SwapType.QuickAssist,
            quickContext.AssistTarget
        );

        if (!ExecuteSwap(swapContext))
        {
            return false;
        }

        manager.Complete(quickContext);
        return true;
    }

    #endregion


    #region [교대 실행 영역]

    /// <summary>
    /// 검증이 끝난 교대 정보를 기준으로 퇴장·등장·상태 전환을 순서대로 처리
    /// 현재 Hero 인덱스와 이벤트는 모든 실행이 끝난 뒤 확정
    /// </summary>
    private bool ExecuteSwap(SwapContext context)
    {
        Hero outHero = context.OutHero;
        Hero inHero = context.InHero;
        HeroSwapSystem.SwapType swapType = context.SwapType;

        // 패링지원 대상은 검증 완료 후 등장 Hero에게 주입
        if (swapType == HeroSwapSystem.SwapType.Parry ||
            swapType == HeroSwapSystem.SwapType.QuickAssist)
        {
            inHero.SetCounterTarget(context.ActionTarget);
        }

        // 결정된 타입을 등장/퇴장 영웅 장부에 명시적 주입
        inHero.SwapSystem.CurrentSwapType = swapType;
        outHero.SwapSystem.CurrentSwapType = swapType;

        // 퇴장할 영웅(outHero) 제어 처리
        outHero.DisableInput();
        outHero.SwapSystem.BeginSwapOutProcess(swapType);

        // inHero 등장 위치 처리
        PrepareIncomingHeroForSwap(
            inHero,
            context.OutHeroPosition,
            context.OutHeroRotation,
            swapType,
            context.Direction,
            context.ActionTarget
        );

        // inHero 물리적 등장
        inHero.SwapSystem.PrepareSwapInEntrance();
        inHero.EnableInput();

        // LockOnManager ActiveHero 및 카메라 대상 갱신
        LockOnManager.Instance?.RegisterActiveHero(inHero);
        UpdateCameraTarget(inHero);

        // inHero 최종 상태전환
        EnterIncomingHeroState(inHero, swapType);

        // 모든 교대 처리가 성공한 뒤 현재 Hero와 HUD 이벤트 확정
        if (!_partyStateManager.TrySetCurrentHero(inHero))
        {
            Debug.LogError(
                $"[교대 오류] {inHero.name}을 현재 Hero로 확정하지 못했습니다."
            );

            return false;
        }

        return true;
    }

    /// <summary>
    /// 교대 타입에 따라 등장 Hero의 최종 상태를 결정
    /// </summary>
    private void EnterIncomingHeroState(Hero inHero, HeroSwapSystem.SwapType swapType)
    {
        switch (swapType)
        {
            case HeroSwapSystem.SwapType.Parry:
                inHero.ChangeState(HeroState.ParryIn);
                break;
            case HeroSwapSystem.SwapType.QuickAssist:       
                inHero.ChangeState(HeroState.QuickAssist);
                break;
            case HeroSwapSystem.SwapType.Chain:
                inHero.ChangeState(HeroState.ChainAttack);
                break;
            default:
                // 완전히 퇴장 중이거나 일반 대기 상태라면 SwapIn으로 전환
                if (inHero.CurrentState != HeroState.SwapIn)
                {
                    inHero.ChangeState(HeroState.SwapIn);
                }
                else
                {
                    // 이미 SwapIn 상태라면 트리거만 갱신
                    inHero.Animation.TriggerSwapIn();
                }
                break;
        }
    }

    /// <summary>
    /// 선택된 Hero의 Chain Attack 교대 실행
    /// ChainAttackManager 에서 호출
    /// </summary>
    public bool TryExecuteChainSwap(Hero inHero, Enemy chainTarget)
    {
        Hero outHero = CurrentHero;

        if (_partyStateManager == null) return false;
        if (outHero == null || inHero == null || chainTarget == null) return false;
        if (outHero == inHero) return false;
        if (outHero.SwapSystem == null || inHero.SwapSystem == null) return false;
        if (chainTarget.Status == null || chainTarget.Status.IsDead) return false;

        if (inHero.Status == null || inHero.Status.IsDead) return false;

        Vector3 outHeroPosition = outHero.transform.position;
        Quaternion outHeroRotation = outHero.transform.rotation;

        // Chain 교대 타입 등록
        outHero.SwapSystem.CurrentSwapType = HeroSwapSystem.SwapType.Chain;
        inHero.SwapSystem.CurrentSwapType = HeroSwapSystem.SwapType.Chain;

        // 기존 Hero 즉시 퇴장
        outHero.DisableInput();
        outHero.SwapSystem.BeginSwapOutProcess(HeroSwapSystem.SwapType.Chain);

        // 등장 Hero의 기존 장부 초기화
        inHero.ActionRegistry.ResetActionRegistry();

        // ChainTarget 근처 등장 위치 설정
        PrepareIncomingHeroNearTarget(
            inHero,
            chainTarget,
            outHeroPosition,
            outHeroRotation,
            _chainSpawnDistance
        );

        // 렌더러·콜라이더·CharacterController 활성화
        inHero.SwapSystem.PrepareSwapInEntrance();
        inHero.EnableInput();

        // 록온 및 Gameplay 카메라의 복귀 대상 갱신
        LockOnManager.Instance?.RegisterActiveHero(inHero);
        UpdateCameraTarget(inHero);

        // 현재 Hero 확정
        if (!_partyStateManager.TrySetCurrentHero(inHero))
        {
            Debug.LogError(
                $"[Chain 교대 오류] {inHero.name}을 현재 Hero로 확정하지 못했습니다."
            );
            return false;
        }

        // Chain Attack 상태 진입
        EnterIncomingHeroState(inHero, HeroSwapSystem.SwapType.Chain);

        Debug.Log(
            $"[Chain 교대] {outHero.name} → {inHero.name} | " +
            $"대상: {chainTarget.name}"
        );

        return true;
    }

    #endregion

    #region [유틸리티 메서드]

    private HeroSwapSystem.SwapType DetermineStandardSwapType()
    {
        // 패링 교대 우선순위 부여
        if (CombatManager.Instance != null &&
            CombatManager.Instance.IsParryAssistWindowOpen)
        {
            return HeroSwapSystem.SwapType.Parry;
        }

        return HeroSwapSystem.SwapType.Normal;
    }

    /// <summary>
    /// 조작중인 Hero가 사망처리 됐을 때 (애니메이션 이벤트에서 호출) 교대 시퀀스
    /// </summary>
    public void HandleDeathSwap(Hero deadHero)
    {
        if (deadHero == null || deadHero != CurrentHero) return;

        // 1차: 정상적으로 즉시 등장 가능한 Hero 탐색
        Hero inHero = _partyStateManager.GetValidNextHero();

        // 2차: 생존자는 있지만 일시적으로 교대 불가한 상황인지 검사
        if (inHero == null)
        {
            inHero = _partyStateManager.GetNextAliveHero();

            // 진짜 생존자가 없는 경우에만 전멸 처리
            if (inHero == null)
            {
                deadHero.DisableInput();
                deadHero.SwapSystem.SetInActiveEntrance();

                HandlePartyDefeat();
                return;
            }

            // 생존 Hero의 OffFieldAction을 강제로 종료하고 등장 준비
            ForceEndOffFieldAction(inHero);
        }

        ExecuteDeathSwap(deadHero, inHero);
    }

    /// <summary>
    /// 사망한 현재 Hero를 퇴장시키고 탐색된 생존 Hero를 필드에 등장
    /// </summary>
    private void ExecuteDeathSwap(Hero deadHero, Hero inHero)
    {
        if (deadHero == null || inHero == null) return;
        if (deadHero != CurrentHero) return;
        if (inHero.Status == null || inHero.Status.IsDead) return;

        Vector3 swapPosition = deadHero.transform.position;
        Quaternion swapRotation = deadHero.transform.rotation;

        // 1. 사망 Hero 비활성화
        deadHero.DisableInput();
        deadHero.ActionRegistry.ResetActionRegistry();
        deadHero.SwapSystem.SetInActiveEntrance();

        // Dead 상태는 부활 전까지 유지
        // deadHero.ChangeState(HeroState.InActive)는 호출하지 않음

        // 2. 등장 Hero의 기존 행동 상태 초기화
        inHero.ActionRegistry.ResetActionRegistry();
        inHero.SwapSystem.CurrentSwapType = HeroSwapSystem.SwapType.Normal;

        // 3. 사망 Hero 위치로 이동
        inHero.transform.SetPositionAndRotation(swapPosition, swapRotation);

        // 4. 렌더러·물리 컴포넌트 활성화
        inHero.SwapSystem.PrepareSwapInEntrance();
        inHero.EnableInput();

        // 5. 록온과 카메라 대상 갱신
        LockOnManager.Instance?.RegisterActiveHero(inHero);
        UpdateCameraTarget(inHero);

        // 6. 등장 애니메이션 실행
        inHero.ChangeState(HeroState.SwapIn);

        // 7. 모든 사망 교대 처리 후 현재 Hero와 HUD 이벤트 확정
        if (!_partyStateManager.TrySetCurrentHero(inHero))
        {
            Debug.LogError(
                $"[사망 교대 오류] {inHero.name}을 현재 Hero로 확정하지 못했습니다."
            );
            return;
        }

        Debug.Log(
            $"[사망 교대] {deadHero.gameObject.name} → {inHero.gameObject.name}"
        );
    }

    /// <summary>
    /// IsOffFieldActionActive를 강제로 종료시키고 HeroState.InActive 상태로 만든 후
    /// 사망 교대를 진행할 수 있도록 준비
    /// </summary>
    private void ForceEndOffFieldAction(Hero hero)
    {
        if (!hero.ActionRegistry.IsOffFieldActionActive) return;

        hero.ActionRegistry.SetOffFieldActionActive(false);
        hero.ActionRegistry.ResetCombo();
        hero.Animation.ClearEveryTrigger();

        if (hero.CurrentState != HeroState.InActive)
        {
            hero.ChangeState(HeroState.InActive);
        }
    }

    /// <summary>
    /// 파티 전멸 상태를 PartyStateManager에 전달
    /// 실제 전투 실패 UI와 재시작 처리는 이후 별도 시스템에서 처리
    /// </summary>
    private void HandlePartyDefeat()
    {
        _partyStateManager?.NotifyPartyDefeated();
    }

    /// <summary>
    /// InHero의 등장 위치를 조정해주는 헬퍼 메서드
    /// </summary>
    private void PrepareIncomingHeroForSwap(
    Hero inHero,
    Vector3 outHeroPosition,
    Quaternion outHeroRotation,
    HeroSwapSystem.SwapType swapType,
    SwapDirection direction,
    Enemy actionTarget)
    {
        bool usesTargetPosition =
            swapType == HeroSwapSystem.SwapType.Parry ||
            swapType == HeroSwapSystem.SwapType.QuickAssist;

        bool hasValidTarget =
            actionTarget != null &&
            actionTarget.Status != null &&
            !actionTarget.Status.IsDead;

        if (usesTargetPosition && hasValidTarget)
        {
            float spawnDistance =
                swapType == HeroSwapSystem.SwapType.Parry
                    ? _parrySpawnDistance
                    : _quickAssistSpawnDistance;

            PrepareIncomingHeroNearTarget(
                inHero,
                actionTarget,
                outHeroPosition,
                outHeroRotation,
                spawnDistance
            );

            return;
        }

        float sideSign =
            direction == SwapDirection.Next ? 1f : -1f;

        Vector3 normalSpawnPosition =
            outHeroPosition +
            outHeroRotation * Vector3.right * sideSign -
            outHeroRotation * Vector3.forward;

        inHero.transform.SetPositionAndRotation(
            normalSpawnPosition,
            outHeroRotation
        );
    }

    /// <summary>
    /// 교대 시 카메라 연출 준비
    /// GamePlay_VCam에만 적용
    /// </summary>
    /// <param name="hero"></param>
    private void UpdateCameraTarget(Hero hero)
    {
        if (hero == null || _cameraController == null) return;

        Transform pivot = hero.transform.Find("CamPivot");

        if (pivot == null)
        {
            pivot = hero.transform;
        }

        _cameraController.SetFollowTarget(pivot);
        _cameraController.TriggerSwapSmoothing();
    }

    /// <summary>
    /// 유니티 인스펙터 창에서 PartyStateManager가
    /// 참조되지 않았을 때 직접 참조
    /// </summary>
    private void ResolvePartyStateManager()
    {
        if (_partyStateManager != null) return;

        _partyStateManager = PartyStateManager.Instance;

        if (_partyStateManager == null)
        {
            _partyStateManager =
                FindFirstObjectByType<PartyStateManager>();
        }
    }

    /// <summary>
    /// 지정된 Target 주변에 등장 Hero 배치 및 대상 방향 회전
    /// </summary>
    private void PrepareIncomingHeroNearTarget(
        Hero inHero,
        Enemy actionTarget,
        Vector3 referencePosition,
        Quaternion fallbackRotation,
        float spawnDistance)
    {
        Transform targetTransform = actionTarget.transform;

        Vector3 directionFromTarget =
            referencePosition - targetTransform.position;

        directionFromTarget.y = 0f;

        if (directionFromTarget.sqrMagnitude < 0.01f)
        {
            directionFromTarget = -targetTransform.forward;
        }

        directionFromTarget.Normalize();

        Vector3 spawnPosition =
            targetTransform.position +
            directionFromTarget * spawnDistance;

        spawnPosition.y = referencePosition.y;

        Vector3 lookDirection =
            targetTransform.position - spawnPosition;

        lookDirection.y = 0f;

        Quaternion spawnRotation =
            lookDirection.sqrMagnitude > 0.01f
                ? Quaternion.LookRotation(lookDirection.normalized)
                : fallbackRotation;

        inHero.transform.SetPositionAndRotation(
            spawnPosition,
            spawnRotation
        );
    }

    #endregion

    #region [이벤트 구독 영역]

    private void SubscribeInputEvents()
    {
        if (_isInputEventSubscribed) return;

        InputManager input = InputManager.Instance;
        if (input == null) return;

        input.OnSwapNextPerformed += HandleSwapNextPerformedInput;
        input.OnSwapPrevPerformed += HandleSwapPrevPerformedInput;

        _isInputEventSubscribed = true;
    }

    private void UnsubscribeInputEvents()
    {
        if (!_isInputEventSubscribed ||
            InputManager.Instance == null)
        {
            return;
        }

        InputManager input = InputManager.Instance;

        input.OnSwapNextPerformed -= HandleSwapNextPerformedInput;
        input.OnSwapPrevPerformed -= HandleSwapPrevPerformedInput;

        _isInputEventSubscribed = false;
    }

    #endregion
}