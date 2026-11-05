using UnityEngine;

/// <summary>
/// 
/// </summary>
public class ErikaMechanic : MonoBehaviour, IHeroMechanic, IAfterShockTargetProvider
{
    public Hero Hero => _hero;
    public HeroState CurrentState => _hero.CurrentState;
    public Animator Animator => _hero.Animation.Animator;
    public HeroMechanicResourceType ResourceType => _resourceType;
    public float CurrentResource => _currentResource;
    public float MaxResource => _maxResource;
    public float RequiredResource => _requiredResource;
    public Transform AfterShockTarget => _afterShockTarget;
    public bool IsChargeAttackFinisherActive =>
        CurrentState == HeroState.ChargeAttack && _chargeAttackPhase == ChargeAttackPhase.Finisher;
    public bool IsCombatRotationLocked =>
        CurrentState == HeroState.ChargeAttack && _chargeAttackPhase == ChargeAttackPhase.AutoDodge;

    private Hero _hero;

    [Header("Quick Assist Settings")]
    [SerializeField] private SwapDirection _quickAssistDirection = SwapDirection.Next;
    private static readonly int HashExSkill = Animator.StringToHash("Base Layer.EX_Skill.EX_Skill");

    private enum ChargeAttackPhase { None, Loop, AutoDodge, Finisher }

    [Header("Charge Attack Auto Dodge Settings")]
    [SerializeField, Min(0f)] private float _autoDodgeMoveMultiplier = 1f;
    private ChargeAttackPhase _chargeAttackPhase;
    private Vector3 _attackDirectionBeforeAutoDodge;
    private static readonly int HashChargeAttackLoop =
        Animator.StringToHash("Base Layer.ChargeAttack.Attack_Charge");
    private static readonly int HashChargeAttackAutoDodge =
        Animator.StringToHash("Base Layer.ChargeAttack.Attack_Charge_AutoDodge");

    [Header("Mechanic Resource Settings")]
    [SerializeField] private HeroMechanicResourceType _resourceType;
    [SerializeField] private float _currentResource;
    [SerializeField] private float _maxResource;
    [SerializeField] private float _requiredResource;


    [Header("Aftershock Settings")]
    [SerializeField, Min(0f)] private float _aftershockCooldown = 10f;
    private float _remainingAftershockCooldown;
    // 여진 시작 시 확보하며, 실행 도중 록온 대상이 바뀌어도 유지한다.
    private Transform _afterShockTarget;

    [Header("AfterShock Movement & Rotation Settings")]
    [SerializeField] private float _afterShockRotationSpeed = 480f;

    [Header("AfterShock Start Position Settings")]
    [SerializeField, Min(1f)] private float _afterShockStartDistance = 4f;
    [SerializeField] private float _afterShockStartSideOffset = -0.65f;
    [SerializeField] private LayerMask _afterShockGroundMask = 1 << 9; // Bottom
    [SerializeField] private LayerMask _afterShockObstacleMask = (1 << 0) | (1 << 8) | (1 << 10);
    private Vector3 _afterShockStartPosition;
    private static readonly float[] AfterShockSearchAngles = { 0f, 45f, -45f, 90f, -90f, 180f };

    // 강공격 막타 전이 Hash 캐싱 필드
    private static readonly int HashChargeAttackFinisher =
        Animator.StringToHash("Base Layer.ChargeAttack.Attack_Charge_Finisher");


    private void Awake()
    {
        _hero = GetComponent<Hero>();
        _hero.OnAttackHitConfirmed += HandleAttackHitConfirmed;
    }

    private void OnDestroy()
    {
        if (_hero == null) return;

        _hero.OnAttackHitConfirmed -= HandleAttackHitConfirmed;
    }

    #region [FSM 제어 영역]

    /// <summary>
    /// Hero가 새 상태에 진입할 때 Erika 전용 처리를 실행한다.
    /// </summary>
    public void EnterState(HeroState newState, bool isComboConnection)
    {
        switch (newState)
        {
            case HeroState.Idle:
                break;
            case HeroState.ConvertCombat:
                break;
            case HeroState.Walk:
                break;
            case HeroState.Run:
                break;
            case HeroState.Sprint:
                break;
            case HeroState.Dodge:
                break;
            case HeroState.DodgeCounter:
                break;
            case HeroState.Attack:
                break;
            case HeroState.ChargeAttack:
                _chargeAttackPhase = ChargeAttackPhase.Loop;
                
                // 평꾹 전체 무적 유지
                _hero.ActionRegistry.SetIsInvincible(true);
                
                /// <summary>
                /// 일반 회피 차단
                /// -> 전용 자동회피 메커니즘 존재
                /// </summary>
                _hero.ActionRegistry.SetIsManualDodgeBlocked(true);

                /// <summary>
                /// 평꾹 중 퀵스왑 차단
                /// 대신 이벤트 핸들러 기반 전용 빠른지원 윈도우 열어줌
                /// </summary>
                _hero.SwapSystem.SetIsQuickSwapWindowOpen(false);
                break;
            case HeroState.Skill:
                /// <summary>
                /// 강특 중 퀵스왑 차단
                /// 대신 이벤트 핸들러 기반 전용 빠른지원 윈도우 열어줌
                /// </summary>
                if (_hero.SkillSystem.IsExSkillMode)
                    _hero.SwapSystem.SetIsQuickSwapWindowOpen(false);
                break;
            case HeroState.Ult:
                break;
            case HeroState.SwapIn:
                break;
            case HeroState.SwapOut:
                break;
            case HeroState.ParryIn:
                break;
            case HeroState.ParryOut:
                break;
            case HeroState.ParryCounter:
                break;
            case HeroState.QuickAssist:
                break;
            case HeroState.ChainAttack:
                break;
            case HeroState.Hit:
                break;
            case HeroState.Knockback:
                break;
            case HeroState.InActive:
                // InActive 전환 후 최소 3초 후에 여진공격 발동
                _remainingAftershockCooldown = Mathf.Max(
                    _remainingAftershockCooldown,
                    3f
                );
                break;
            case HeroState.AfterShock:
                _hero.ActionRegistry.ResetComboIndex();
                _remainingAftershockCooldown = _aftershockCooldown;
                _hero.ActionRegistry.SetOffFieldActionActive(true);
                SetAfterShockStartPosition();
                _hero.SwapSystem.PrepareSwapInEntrance();
                TriggerAfterShock();
                break;
            case HeroState.Dead:
                break;
        }
    }

    /// <summary>
    /// 매프레임 Erika 전용 처리를 실행한다.
    /// </summary>
    public void UpdateState()
    {
        _remainingAftershockCooldown = Mathf.Max(
            0f,
            _remainingAftershockCooldown - Time.deltaTime
        );

        switch (CurrentState)
        {
            case HeroState.Idle:
                break;
            case HeroState.ConvertCombat:
                break;
            case HeroState.Walk:
                break;
            case HeroState.Run:
                break;
            case HeroState.Sprint:
                break;
            case HeroState.Dodge:
                break;
            case HeroState.DodgeCounter:
                break;
            case HeroState.Attack:
                break;
            case HeroState.ChargeAttack:
                break;
            case HeroState.Skill:
                break;
            case HeroState.Ult:
                break;
            case HeroState.SwapIn:
                break;
            case HeroState.SwapOut:
                break;
            case HeroState.ParryIn:
                break;
            case HeroState.ParryOut:
                break;
            case HeroState.ParryCounter:
                break;
            case HeroState.QuickAssist:
                break;
            case HeroState.ChainAttack:
                break;
            case HeroState.Hit:
                break;
            case HeroState.Knockback:
                break;
            case HeroState.InActive:
                TryBeginAftershock();
                break;
            case HeroState.AfterShock:
                UpdateAfterShockRotation();
                UpdateAfterShockMovement();
                break;
            case HeroState.Dead:
                break;
        }
    }

    /// <summary>
    /// Hero가 기존 상태를 벗어날 때 Erika 전용 정보를 정리함.
    /// </summary>
    public void ExitState(HeroState currentState, bool preserveAnimMove = false)
    {
        switch (currentState)
        {
            case HeroState.Idle:
                break;
            case HeroState.ConvertCombat:
                break;
            case HeroState.Walk:
                break;
            case HeroState.Run:
                break;
            case HeroState.Sprint:
                break;
            case HeroState.Dodge:
                break;
            case HeroState.DodgeCounter:
                break;
            case HeroState.Attack:
                break;
            case HeroState.ChargeAttack:
                _hero.ActionRegistry.SetIsManualDodgeBlocked(false);
                _hero.ActionRegistry.SetIsInvincible(false);
                _chargeAttackPhase = ChargeAttackPhase.None;
                _attackDirectionBeforeAutoDodge = Vector3.zero;
                break;
            case HeroState.Skill:
                break;
            case HeroState.Ult:
                break;
            case HeroState.SwapIn:
                break;
            case HeroState.SwapOut:
                break;
            case HeroState.ParryIn:
                break;
            case HeroState.ParryOut:
                break;
            case HeroState.ParryCounter:
                break;
            case HeroState.QuickAssist:
                break;
            case HeroState.ChainAttack:
                break;
            case HeroState.Hit:
                break;
            case HeroState.Knockback:
                break;
            case HeroState.InActive:
                break;
            case HeroState.AfterShock:
                ResetTriggerAfterShock();
                _afterShockTarget = null;
                // 교대 진입 중인 캐릭터를 숨기지 않도록 퇴장은 InActive 진입에서 처리함.
                _hero.ActionRegistry.SetOffFieldActionActive(false);
                break;
            case HeroState.Dead:
                break;
        }
    }

    #endregion


    #region [애니메이션 제어 영역]

    private static readonly int HashAfterShockTrigger = Animator.StringToHash("AfterShockTrigger");
    private void TriggerAfterShock()
    {
        Animator.SetTrigger(HashAfterShockTrigger);
    }
    private void ResetTriggerAfterShock()
    {
        Animator.ResetTrigger(HashAfterShockTrigger);
    }

    /// <summary>
    /// 타격 인덱스를 초기화하고 평꾹 마무리 상태를 처음부터 재생함.
    /// </summary>
    public void PlayChargeAttackFinisher()
    {
        if (CurrentState != HeroState.ChargeAttack) return;

        _chargeAttackPhase = ChargeAttackPhase.Finisher;
        _hero.ActionRegistry.ResetComboIndex();
        _hero.ActionRegistry.DeactivateAnimMove();
        _hero.Movement.ClearFixedAnimMovement();
        Animator.CrossFadeInFixedTime(HashChargeAttackFinisher, 0.05f, 0, 0f);
    }

    #endregion


    #region [애니메이션 이벤트 핸들러 영역]

    /// <summary>
    /// 현재 강특 클립의 이벤트에서 지정한 방향의 빠른지원 창을 요청함.
    /// </summary>
    public void OnExSkillQuickAssistWindowOpen(AnimationEvent animationEvent)
    {
        if (CurrentState != HeroState.Skill) return;
        if (animationEvent == null || !animationEvent.isFiredByAnimator) return;
        if (animationEvent.animatorStateInfo.fullPathHash != HashExSkill) return;

        // 후딜 캔슬 이벤트가 EX 모드를 해제한 뒤에도 클립 출처로 발동을 허용함.
        TryOpenQuickAssistWindow();
    }

    /// <summary>
    /// 현재 평꾹 막타 클립의 이벤트에서 지정한 방향의 빠른지원 창을 요청함.
    /// </summary>
    public void OnChargeAttackFinisherQuickAssistWindowOpen(AnimationEvent animationEvent)
    {
        if (!IsChargeAttackFinisherActive) return;
        if (!CanProcessAnimationEvent(animationEvent)) return;

        TryOpenQuickAssistWindow();
    }

    /// <summary>
    /// 온필드 Erika의 유효한 전투 타겟을 대상으로 빠른지원 창을 요청함.
    /// </summary>
    private void TryOpenQuickAssistWindow()
    {
        if (PartyStateManager.Instance == null || PartyStateManager.Instance.CurrentHero != _hero) return;
        if (QuickAssistManager.Instance == null || LockOnManager.Instance == null) return;

        Transform target = LockOnManager.Instance.MagneticTarget;
        if (target == null || !target.gameObject.activeInHierarchy) return;

        Enemy enemy = target.GetComponentInParent<Enemy>();
        if (enemy == null || enemy.Status == null || enemy.Status.IsDead) return;

        QuickAssistManager.Instance.TryOpen(_hero, enemy, _quickAssistDirection);
    }

    /// <summary>
    /// 첫 여진 타격 시 대상과 자원을 확인하고, 자원을 소비한 뒤 공격을 요청한다.
    /// 대상이 무효하거나 자원이 부족하면 여진을 종료한다.
    /// </summary>
    public void OnAfterShockFirstExecuteFrame()
    {
        if (CurrentState != HeroState.AfterShock) return;

        // 타겟 유효성 검사 후 유효하지 않으면 자원 소비없이 return
        if (!IsValidAfterShockTarget(_afterShockTarget))
        {
            OnAfterShockEnd();
            return;
        }

        // 자원 소모 시도
        if (!TryConsumeResource())
        {
            // 2차 안전장치 -> 필요한가?
            OnAfterShockEnd();
            return;
        }

        OnAfterShockExecuteFrame();
    }

    public void OnAfterShockExecuteFrame()
    {
        if (CurrentState != HeroState.AfterShock) return;

        int combo = _hero.ActionRegistry.CurrentComboIndex;
        int hit = _hero.ActionRegistry.GetHitIndexAndAdvance();

        _hero.HitChecker.ExecuteAfterShockHitCheck(combo, hit);
    }

    /// <summary>
    /// 여진 상태에서만 잔상 시퀀스를 종료하고 비활성 상태로 전환함.
    /// </summary>
    public void OnAfterShockEnd()
    {
        if (CurrentState != HeroState.AfterShock) return;

        _hero.ChangeState(HeroState.InActive);
    }

    /// <summary>
    /// 평꾹 반복 시작 시 콤보와 히트 인덱스를 초기화함.
    /// </summary>
    public void OnChargeAttackLoopStart(AnimationEvent animationEvent)
    {
        if (_hero.CurrentState != HeroState.ChargeAttack) return;
        if (!CanProcessAnimationEvent(animationEvent)) return;

        _hero.ActionRegistry.ResetComboIndex();
    }

    /// <summary>
    /// 평꾹 입력이 해제됐으면 지정한 마무리 상태로 전환함.
    /// </summary>
    public void OnChargeAttackReleaseWindow(AnimationEvent animationEvent)
    {
        if (_hero.CurrentState != HeroState.ChargeAttack) return;
        if (!CanProcessAnimationEvent(animationEvent)) return;
        if (_hero.ActionRegistry.IsAttackButtonHolding) return;
        if (_hero.Animation.Animator.IsInTransition(0)) return;

        PlayChargeAttackFinisher();
    }

    /// <summary>
    /// 평꾹의 현재 구간과 이벤트를 발생시킨 Animator 상태가 일치하는지 확인함.
    /// </summary>
    public bool CanProcessAnimationEvent(AnimationEvent animationEvent)
    {
        if (animationEvent == null || !animationEvent.isFiredByAnimator)
            return CurrentState != HeroState.ChargeAttack;

        int sourceHash = animationEvent.animatorStateInfo.fullPathHash;
        if (CurrentState != HeroState.ChargeAttack)
            return sourceHash != HashChargeAttackLoop && sourceHash != HashChargeAttackAutoDodge &&
                sourceHash != HashChargeAttackFinisher;

        switch (_chargeAttackPhase)
        {
            case ChargeAttackPhase.Loop: return sourceHash == HashChargeAttackLoop;
            case ChargeAttackPhase.AutoDodge: return sourceHash == HashChargeAttackAutoDodge;
            case ChargeAttackPhase.Finisher: return sourceHash == HashChargeAttackFinisher;
            default: return false;
        }
    }

    /// <summary>
    /// 자동회피 종료 시 입력 유지 여부에 따라 반복 공격 또는 막타로 복귀함.
    /// </summary>
    public void OnChargeAttackAutoDodgeEnd(AnimationEvent animationEvent)
    {
        if (CurrentState != HeroState.ChargeAttack || _chargeAttackPhase != ChargeAttackPhase.AutoDodge) return;
        if (!CanProcessAnimationEvent(animationEvent)) return;

        _hero.ActionRegistry.DeactivateAnimMove();
        _hero.Movement.ClearFixedAnimMovement();
        RestoreAttackDirectionAfterAutoDodge();

        // 평꾹에서 손을 뗀 경우 평꾹막타로 마무리.
        if (!_hero.ActionRegistry.IsAttackButtonHolding)
        {
            PlayChargeAttackFinisher();
            return;
        }

        _chargeAttackPhase = ChargeAttackPhase.Loop;
        _hero.ActionRegistry.ResetComboIndex();
        _hero.Animation.SetIsAttackCharging(true);
        Animator.CrossFadeInFixedTime(HashChargeAttackLoop, 0.05f, 0, 0f);
    }

    public void OnExSkillAddResourceFrame()
    {
        if (_hero.CurrentState != HeroState.Skill && !_hero.SkillSystem.IsExSkillMode) return;

        AddResource(2f);
    }

    #endregion


    #region [고유 Resource 관리 영역]

    /// <summary>
    /// Hero의 평꾹 타격 판정 성공 알림마다 전용 자원을 1스택 획득함.
    /// 동일 판정으로 맞힌 대상 수에 따라 획득량이 증가하지 않으며,
    /// 다단히트는 각 판정이 성공할 때마다 획득함.
    /// </summary>
    private void HandleAttackHitConfirmed(HeroCombatAttackType attackType)
    {
        if (attackType != HeroCombatAttackType.ChargeAttack) return;

        AddResource(1f);
    }

    /// <summary>
    /// 고유 Resource 를 획득하며 최대 보유량을 넘지 않도록 제한한다.
    /// </summary>
    private void AddResource(float amount)
    {
        if (amount <= 0f) return;

        _currentResource = Mathf.Clamp(
            _currentResource + amount,
            0f,
            _maxResource
        );
    }

    /// <summary>
    /// Resource 가 충분하면 1회 요구량을 소비한다.
    /// 부족하거나 요구량이 유효하지 않으면 소비하지 않고 false를 반환한다.
    /// </summary>
    private bool TryConsumeResource()
    {
        if (_requiredResource <= 0f) return false;
        if (_currentResource < _requiredResource) return false;

        _currentResource -= _requiredResource;
        return true;
    }

    /// <summary>
    /// 파라미터로 받은 목표값으로 현재 Resource 초기화한다.
    /// </summary>
    private void InitializeResource(float targetResource)
    {
        if (targetResource > _maxResource)
        {
            targetResource = _maxResource;
        }

        _currentResource = targetResource;
    }

    /// <summary>
    /// 고유 Resource 를 0으로 초기화한다.
    /// </summary>
    private void ResetResource()
    {
        _currentResource = 0f;
    }

    #endregion


    #region [Movement & Rotation 영역]

    /// <summary>
    /// 여진공격의 고정 대상을 기준으로 애니메이션 이동을 요청한다.
    /// 대상이 무효하면 대상 없는 이동 규칙을 사용한다.
    /// </summary>
    private void UpdateAfterShockMovement()
    {
        Transform target = IsValidAfterShockTarget(_afterShockTarget)
            ? _afterShockTarget : null;

        _hero.Movement.ExecuteCombatAnimMovement(target);
    }

    /// <summary>
    /// 유효한 여진공격 대상을 향해 부드럽게 회전
    /// </summary>
    private void UpdateAfterShockRotation()
    {
        if (!IsValidAfterShockTarget(_afterShockTarget)) return;

        Vector3 targetDirection = _afterShockTarget.position - transform.position;
        targetDirection.y = 0f;

        if (targetDirection.sqrMagnitude < 0.01f) return;

        Quaternion targetRotation = Quaternion.LookRotation(targetDirection);

        transform.rotation = Quaternion.RotateTowards(
            transform.rotation,
            targetRotation,
            _afterShockRotationSpeed * Time.deltaTime
        );
    }

    #endregion


    #region [AfterShock 영역]

    /// <summary>
    /// 발동 조건을 확인하고 대상을 확보한 뒤 여진 상태 전환을 요청한다.
    /// </summary>
    private void TryBeginAftershock()
    {
        if (!CanBeginAftershock()) return;
        if (!TrySetAfterShockTarget()) return;
        if (!TryFindAfterShockStartPosition(out _afterShockStartPosition))
        {
            _afterShockTarget = null;
            return;
        }

        _hero.ChangeState(HeroState.AfterShock);
    }

    /// <summary>
    /// 오프필드 대기 상태와 생존 여부, 보유 자원을 검사한다.
    /// </summary>
    private bool CanBeginAftershock()
    {
        if (CurrentState != HeroState.InActive) return false;
        if (_hero.Status.IsDead) return false;
        if (_remainingAftershockCooldown > 0f) return false;

        if (_requiredResource <= 0f) return false;
        if (_currentResource < _requiredResource) return false;

        return true;
    }

    /// <summary>
    /// 대상의 플레이어 쪽을 우선하여 바닥과 공간이 확보된 등장 위치를 찾는다.
    /// 모든 후보가 막히면 상태·쿨타임·자원을 변경하지 않는다.
    /// </summary>
    private bool TryFindAfterShockStartPosition(out Vector3 position)
    {
        position = default;
        CharacterController controller = _hero.Movement.CharacterController;
        if (controller == null || !IsValidAfterShockTarget(_afterShockTarget)) return false;

        Hero currentHero = PartyStateManager.Instance != null
            ? PartyStateManager.Instance.CurrentHero : null;
        Vector3 outward = (currentHero != null ? currentHero.transform.position : transform.position)
            - _afterShockTarget.position;
        outward.y = 0f;
        if (outward.sqrMagnitude < 0.01f) outward = -_afterShockTarget.forward;
        outward.y = 0f;
        if (outward.sqrMagnitude < 0.01f) outward = Vector3.back;
        outward.Normalize();

        Vector3 scale = transform.lossyScale;
        float radius = controller.radius * Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.z));
        float height = Mathf.Max(controller.height * Mathf.Abs(scale.y), radius * 2f);
        Vector3 centerOffset = Vector3.Scale(controller.center, scale);

        foreach (float angle in AfterShockSearchAngles)
        {
            Vector3 candidateOutward = Quaternion.Euler(0f, angle, 0f) * outward;
            Vector3 right = Vector3.Cross(Vector3.up, -candidateOutward);
            Vector3 candidate = _afterShockTarget.position + candidateOutward * _afterShockStartDistance
                + right * _afterShockStartSideOffset;

            if (!Physics.Raycast(candidate + Vector3.up * 2f, Vector3.down, out RaycastHit ground,
                5f, _afterShockGroundMask, QueryTriggerInteraction.Ignore)) continue;
            if (Vector3.Angle(ground.normal, Vector3.up) > controller.slopeLimit) continue;

            candidate.y = ground.point.y + 0.02f - centerOffset.y + height * 0.5f;
            Vector3 lookDirection = _afterShockTarget.position - candidate;
            lookDirection.y = 0f;
            Quaternion rotation = Quaternion.LookRotation(lookDirection);
            Vector3 center = candidate + rotation * centerOffset;
            Vector3 halfSegment = Vector3.up * (height * 0.5f - radius);
            int occupiedMask = _afterShockObstacleMask | (1 << 3) | (1 << 6);
            bool occupied = false;
            foreach (Collider other in Physics.OverlapCapsule(center - halfSegment, center + halfSegment,
                radius, occupiedMask, QueryTriggerInteraction.Ignore))
            {
                if (other.transform.IsChildOf(transform)) continue;
                occupied = true;
                break;
            }
            if (occupied) continue;

            // 벽 반대편이나 대상이 보이지 않는 위치에는 등장하지 않는다.
            if (Physics.Linecast(_afterShockTarget.position + Vector3.up, center,
                _afterShockObstacleMask, QueryTriggerInteraction.Ignore)) continue;

            position = candidate;
            return true;
        }
        return false;
    }

    /// <summary>
    /// 진입 직전에 확보한 위치와 대상 방향을 등장 처리 전에 적용한다.
    /// </summary>
    private void SetAfterShockStartPosition()
    {
        Vector3 direction = _afterShockTarget.position - _afterShockStartPosition;
        direction.y = 0f;
        _hero.Movement.SetWorldPose(_afterShockStartPosition, Quaternion.LookRotation(direction));
    }

    /// <summary>
    /// 여진 대상이 활성 상태이며 살아 있는 Enemy인지 검사한다.
    /// </summary>
    private bool IsValidAfterShockTarget(Transform target)
    {
        if (target == null || !target.gameObject.activeInHierarchy) return false;

        Enemy enemy = target.GetComponentInParent<Enemy>();
        if (enemy == null || enemy.Status == null) return false;

        return enemy.gameObject.activeInHierarchy && !enemy.Status.IsDead;
    }

    /// <summary>
    /// 현재 MagneticTarget이 유효하면 이번 여진의 고정 대상으로 설정한다.
    /// 유효하지 않으면 기존 참조를 비우고 false를 반환한다.
    /// </summary>
    private bool TrySetAfterShockTarget()
    {
        Transform target = LockOnManager.Instance != null
            ? LockOnManager.Instance.MagneticTarget : null;

        if (!IsValidAfterShockTarget(target))
        {
            _afterShockTarget = null;
            return false;
        }

        _afterShockTarget = target;
        return true;
    }

    #endregion


    #region [전용 ChargeAttack 로직 영역]

    /// <summary>
    /// 평꾹 반복 중 피격을 회피하고 입력 방향 또는 캐릭터 우측으로 이동을 준비함.
    /// </summary>
    public bool TryEvadeHit(CombatEventContext context)
    {
        if (CurrentState != HeroState.ChargeAttack) return false;
        if (_chargeAttackPhase == ChargeAttackPhase.AutoDodge) return true;
        if (_chargeAttackPhase != ChargeAttackPhase.Loop) return false;

        bool isLoopPlaying = Animator.GetCurrentAnimatorStateInfo(0).fullPathHash == HashChargeAttackLoop ||
            (Animator.IsInTransition(0) && Animator.GetNextAnimatorStateInfo(0).fullPathHash == HashChargeAttackLoop);
        if (!isLoopPlaying) return false;

        // 방향입력이 없다면 기본적으로 후방으로 회피
        Vector3 direction = _hero.HeroAxis.sqrMagnitude > 0.01f ? _hero.HeroAxis : -transform.forward;
        direction.y = 0f;
        direction.Normalize();

        _attackDirectionBeforeAutoDodge = transform.forward;
        _chargeAttackPhase = ChargeAttackPhase.AutoDodge;
        _hero.Animation.ResetTriggerChargeAttack();
        _hero.ActionRegistry.ResetCombo();
        _hero.ActionRegistry.CloseCancelWindow();
        _hero.ActionRegistry.SetIsInvincible(true);
        _hero.ActionRegistry.SetIsTargetTrackingActive(false);
        _hero.Movement.ConfigureFixedAnimMovement(direction, _autoDodgeMoveMultiplier);
        _hero.Movement.SetDodgeRotationDirect(direction);
        Animator.CrossFadeInFixedTime(HashChargeAttackAutoDodge, 0.05f, 0, 0f);

        // 극한회피 연출
        if (CombatPresentationManager.Instance != null)
        {
            CombatPresentationManager.Instance.PlayDelayedPerfectDodgePresentation(0.2f);
        }
        return true;
    }

    /// <summary>
    /// 회피 종료 위치에서 타겟을 향하거나 회피 전 공격 방향을 복원함.
    /// </summary>
    private void RestoreAttackDirectionAfterAutoDodge()
    {
        Vector3 direction = _attackDirectionBeforeAutoDodge;
        Transform target = LockOnManager.Instance != null ? LockOnManager.Instance.MagneticTarget : null;
        if (target != null && target.gameObject.activeInHierarchy)
        {
            Vector3 targetDirection = target.position - transform.position;
            targetDirection.y = 0f;
            if (targetDirection.sqrMagnitude > 0.0001f)
                direction = targetDirection;
        }


        // 회피이후 회전은 HeroMovement - UpdateCombatRotation 적용되게 방치
        //direction.y = 0f;
        //if (direction.sqrMagnitude < 0.0001f) return;

        //_hero.Movement.SetDodgeRotationDirect(direction.normalized);
    }

    #endregion

}
