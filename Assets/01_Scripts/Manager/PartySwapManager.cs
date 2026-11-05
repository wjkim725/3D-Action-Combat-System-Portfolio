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
        public Vector3 TargetPosition { get; }
        public Vector3 TargetForward { get; }
        public Vector3 OutHeroPosition { get; }
        public Quaternion OutHeroRotation { get; }

        public SwapContext(
            Hero outHero,
            Hero inHero,
            SwapDirection direction,
            HeroSwapSystem.SwapType swapType,
            Enemy actionTarget,
            Vector3 targetPosition = default,
            Vector3 targetForward = default)
        {
            OutHero = outHero;
            InHero = inHero;
            Direction = direction;
            SwapType = swapType;
            ActionTarget = actionTarget;
            TargetPosition = targetPosition;
            TargetForward = targetForward;
            OutHeroPosition = outHero.transform.position;
            OutHeroRotation = outHero.transform.rotation;
        }
    }

    public static PartySwapManager Instance { get; private set; }

    // 일반·패링·빠른지원 교대의 실행 성공과 방향을 전달함. 체인·사망 교대는 별도 경로임.
    public event System.Action<Hero, Hero, SwapDirection, HeroSwapSystem.SwapType> OnSwapExecuted;

    [SerializeField] private CameraController _cameraController;
    [SerializeField] private PartyStateManager _partyStateManager;


    [Header("Swap Position Correction")]
    [Tooltip("교대 위치 검사에서 장애물로 취급할 레이어. Default의 투명벽을 포함함.")]
    [SerializeField] private LayerMask _swapObstacleMask = (1 << 0) | (1 << 8);

    [Tooltip("교대 위치를 보정할 때 충돌 지점에서 확보할 여유 거리(m).")]
    [SerializeField, Min(0.001f)] private float _swapCollisionClearance = 0.03f;

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
        StartCoroutine(InitializePartyFieldState());
    }

    private void OnDestroy()
    {
        UnsubscribeInputEvents();

        if (Instance == this)
        {
            Instance = null;
        }
    }

    /// <summary>
    /// 비활성 Hero의 초기화를 기다린 뒤 대기 상태로 준비하여 파티에 합류시킴.
    /// 현재 조작 Hero의 위치, 상태와 카메라는 변경하지 않음.
    /// </summary>
    public IEnumerator AddPartyMember(Hero hero)
    {
        if (_partyStateManager == null || hero == null) yield break;
        if (PartyMembers != null && System.Array.IndexOf(PartyMembers, hero) >= 0) yield break;

        hero.gameObject.SetActive(true);
        if (!hero.gameObject.activeInHierarchy || hero.Status == null || hero.SwapSystem == null) yield break;
        hero.DisableInput();
        // 최초 합류는 퇴장 연출 없이 숨겨 초기화 대기 프레임에 모델이 노출되지 않게 함.
        hero.SwapSystem.SwitchHeroRenderer(false);
        hero.SwapSystem.SwitchHeroCollider(false);
        hero.SwapSystem.SwitchHeroCharacterController(false);
        hero.Status.SetEnergyLockedAtZero(_partyStateManager.IsPartyEnergyLockedAtZero);

        // 처음 활성화된 Hero의 Start가 Idle을 설정한 뒤 최종 대기 상태를 확정함.
        yield return null;
        if (hero == null) yield break;
        hero.DisableInput();
        hero.ChangeState(HeroState.InActive);
        _partyStateManager.TryAddPartyMember(hero);
    }

    /// <summary>
    /// 첫 렌더링 전에 현재 Hero만 즉시 표시하고, 초기화 이후 HUD를 갱신함.
    /// </summary>
    public IEnumerator InitializePartyFieldState()
    {
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

                // 하단 코드는 이제 PartyStateManager - OnCurrnetHeroChanged 이벤트 기점으로 호출
                // _cameraController?.SetFollowTarget(hero);

                hero.SwapSystem.SetInitialFieldPresence(true);
                if (hero.CurrentState != HeroState.Entry) hero.EnableInput();
            }
            else
            {
                hero.ChangeState(HeroState.InActive);
                hero.DisableInput();
                // InActive 진입의 퇴장 Fade를 취소하고 최초 대기 멤버를 즉시 숨김.
                hero.SwapSystem.SetInitialFieldPresence(false);
            }
        }

        // 다른 컴포넌트의 Start가 완료된 뒤 HUD 구독자에게 알림.
        yield return null;
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

        if (!ExecuteSwap(context) && swapType == HeroSwapSystem.SwapType.Parry)
            CombatManager.Instance?.CancelParryAssistRequest(inHero);
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
            !combatManager.TryConsumeParryAssistRequest(inHero, out parryTarget))
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
            quickContext.AssistTarget,
            quickContext.TargetPosition,
            quickContext.TargetForward
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
    /// 일반 교대의 안전한 위치를 먼저 확인하고 퇴장·등장·상태 전환을 처리함.
    /// 현재 Hero 인덱스와 이벤트는 모든 실행이 끝난 뒤 확정함.
    /// </summary>
    private bool ExecuteSwap(SwapContext context)
    {
        Hero outHero = context.OutHero;
        Hero inHero = context.InHero;
        HeroSwapSystem.SwapType swapType = context.SwapType;

        // 퇴장과 입력 차단 전에 일반 교대의 안전한 등장 위치를 확정함.
        Vector3 normalSwapPosition = context.OutHeroPosition;
        if (swapType == HeroSwapSystem.SwapType.Normal &&
            !TryResolveNormalSwapPosition(context, out normalSwapPosition))
        {
            return false;
        }

        // 패링지원 대상은 검증 완료 후 등장 Hero에게 주입
        if (swapType == HeroSwapSystem.SwapType.Parry ||
            swapType == HeroSwapSystem.SwapType.QuickAssist)
        {
            Enemy target = context.ActionTarget;
            if (swapType == HeroSwapSystem.SwapType.QuickAssist &&
                (target == null || !target.isActiveAndEnabled || target.Status == null || target.Status.IsDead))
                inHero.ClearCounterTarget();
            else
                inHero.SetCounterTarget(target);
        }

        // 결정된 타입을 등장/퇴장 영웅 장부에 명시적 주입
        inHero.SwapSystem.CurrentSwapType = swapType;
        outHero.SwapSystem.CurrentSwapType = swapType;

        // 퇴장할 영웅(outHero) 제어 처리
        outHero.DisableInput();
        outHero.SwapSystem.BeginSwapOutProcess(swapType);

        // 일반 교대는 검사한 위치를 적용하고 다른 교대는 기존 배치 경로를 유지함.
        if (swapType == HeroSwapSystem.SwapType.Normal)
        {
            inHero.transform.SetPositionAndRotation(normalSwapPosition, context.OutHeroRotation);
        }
        else if (swapType == HeroSwapSystem.SwapType.QuickAssist)
        {
            // 대상이 사망·제거되어도 실행 정보에 확정된 좌표로 등장함.
            PrepareIncomingHeroNearPosition(inHero, context.TargetPosition, context.TargetForward,
                context.OutHeroPosition, context.OutHeroRotation, _quickAssistSpawnDistance);
        }
        else
        {
            PrepareIncomingHeroForSwap(
                inHero,
                context.OutHeroPosition,
                context.OutHeroRotation,
                swapType,
                context.Direction,
                context.ActionTarget
            );
        }

        // inHero 물리적 등장
        inHero.SwapSystem.PrepareSwapInEntrance();
        inHero.EnableInput();

        // LockOnManager ActiveHero 등록
        LockOnManager.Instance?.RegisterActiveHero(inHero);

        // CameraController 에게 교대 카메라 연출 요청
        _cameraController?.TriggerSwapSmoothing();

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

        OnSwapExecuted?.Invoke(outHero, inHero, context.Direction, swapType);
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
    public bool TryExecuteChainSwap(Hero inHero, Vector3 targetPosition, Vector3 targetForward)
    {
        Hero outHero = CurrentHero;

        if (_partyStateManager == null) return false;
        if (outHero == null || inHero == null) return false;
        if (outHero == inHero) return false;
        if (outHero.SwapSystem == null || inHero.SwapSystem == null) return false;

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

        // 잔상 공격 중 재등장한 Hero가 Chain 종료 시 퇴장하지 않도록 정리함.
        inHero.ActionRegistry.SetOffFieldActionActive(false);

        // ChainTarget 근처 등장 위치 설정
        PrepareIncomingHeroNearPosition(
            inHero,
            targetPosition,
            targetForward,
            outHeroPosition,
            outHeroRotation,
            _chainSpawnDistance
        );

        // 렌더러·콜라이더·CharacterController 활성화
        inHero.SwapSystem.PrepareSwapInEntrance();
        inHero.EnableInput();

        // LockOnManager ActiveHero 등록
        LockOnManager.Instance?.RegisterActiveHero(inHero);
        // CameraController 에게 교대 카메라 연출 요청
        _cameraController?.TriggerSwapSmoothing();

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
            $"대상 위치: {targetPosition}"
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

        // 5. LockOnManager ActiveHero 등록 및 카메라 SwapSmoothing
        LockOnManager.Instance?.RegisterActiveHero(inHero);
        _cameraController?.TriggerSwapSmoothing();

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
    /// 기존 좌우 뒤쪽 등장 방향을 유지하며 일반 교대의 안전한 위치를 결정함.
    /// 출발 공간도 확보할 수 없으면 상태를 변경하지 않고 실패를 반환함.
    /// </summary>
    private bool TryResolveNormalSwapPosition(SwapContext context, out Vector3 resolvedPosition)
    {
        resolvedPosition = context.OutHeroPosition;
        CharacterController controller = context.InHero.Movement.CharacterController;
        if (controller == null)
        {
            Debug.LogError($"[일반 교대 오류] {context.InHero.name}의 CharacterController가 없습니다.");
            return false;
        }

        float sideSign = context.Direction == SwapDirection.Next ? 1f : -1f;
        Vector3 desiredPosition = context.OutHeroPosition +
            context.OutHeroRotation * Vector3.right * sideSign -
            context.OutHeroRotation * Vector3.forward;

        // 비활성 Controller의 bounds 대신 등장 시점의 회전과 배율로 캡슐을 구성함.
        Vector3 scale = controller.transform.lossyScale;
        scale = new Vector3(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z));
        float horizontalScale = Mathf.Max(scale.x, scale.z);
        float worldRadius = controller.radius * horizontalScale;
        float worldHeight = Mathf.Max(controller.height * scale.y, worldRadius * 2f);
        Vector3 center = context.OutHeroPosition +
            context.OutHeroRotation * Vector3.Scale(controller.center, scale);
        Vector3 up = context.OutHeroRotation * Vector3.up;
        float halfSegment = worldHeight * 0.5f - worldRadius;
        Vector3 capsuleTop = center + up * halfSegment;
        Vector3 capsuleBottom = center - up * halfSegment;

        // 정상적인 바닥 접촉과 skinWidth 범위의 미세 겹침을 허용함.
        float skinTolerance = Mathf.Min(
            controller.skinWidth * Mathf.Max(horizontalScale, scale.y), worldRadius * 0.1f);
        float queryRadius = Mathf.Max(worldRadius - skinTolerance, 0.001f);
        if (Physics.CheckCapsule(capsuleBottom, capsuleTop, queryRadius,
            _swapObstacleMask, QueryTriggerInteraction.Ignore))
        {
            return false;
        }

        Vector3 displacement = desiredPosition - context.OutHeroPosition;
        float distance = displacement.magnitude;
        if (distance <= 0.001f) return true;

        Vector3 direction = displacement / distance;
        float allowedDistance = distance;
        if (Physics.CapsuleCast(capsuleBottom, capsuleTop, queryRadius, direction,
            out RaycastHit hit, distance, _swapObstacleMask, QueryTriggerInteraction.Ignore))
        {
            allowedDistance = Mathf.Max(0f, hit.distance - _swapCollisionClearance);
        }

        // 첫 충돌 지점 이전에서만 재검사하여 벽 반대편의 빈 공간을 선택하지 않음.
        const int maxPositionChecks = 5;
        for (int i = 0; i < maxPositionChecks; i++)
        {
            Vector3 offset = direction * allowedDistance;
            if (!Physics.CheckCapsule(capsuleBottom + offset, capsuleTop + offset,
                queryRadius, _swapObstacleMask, QueryTriggerInteraction.Ignore))
            {
                resolvedPosition = context.OutHeroPosition + offset;
                return true;
            }

            allowedDistance *= 0.5f;
        }

        // 출발 공간은 이미 확인했으므로 제자리 교대를 허용함.
        return true;
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

        PrepareIncomingHeroNearPosition(inHero, targetTransform.position, targetTransform.forward,
            referencePosition, fallbackRotation, spawnDistance);
    }

    /// <summary>
    /// 대상 위치와 방향을 기준으로 등장 위치 및 바라볼 방향을 설정함.
    /// </summary>
    private void PrepareIncomingHeroNearPosition(
        Hero inHero,
        Vector3 targetPosition,
        Vector3 targetForward,
        Vector3 referencePosition,
        Quaternion fallbackRotation,
        float spawnDistance)
    {

        Vector3 directionFromTarget =
            referencePosition - targetPosition;

        directionFromTarget.y = 0f;

        if (directionFromTarget.sqrMagnitude < 0.01f)
        {
            directionFromTarget = -targetForward;
        }

        directionFromTarget.Normalize();

        Vector3 spawnPosition =
            targetPosition +
            directionFromTarget * spawnDistance;

        spawnPosition.y = referencePosition.y;

        Vector3 lookDirection =
            targetPosition - spawnPosition;

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
