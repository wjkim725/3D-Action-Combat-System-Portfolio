using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Rendering;
using static HeroSwapSystem;

/// <summary>
/// Hero에서 사용되는 이벤트 애니메이션 이벤트 핸들러 전담 컴포넌트
/// </summary>
public class HeroAnimationEventHandler : MonoBehaviour
{
    private Hero _hero;

    /// <summary>
    /// DashAttack 이후 이어질 AttackCombo 연계 진입시점 퍼센트
    /// </summary>
    [SerializeField]
    private float _dashAttackToComboTiming = 0.35f;


    private readonly List<AnimatorClipInfo> _footstepClipInfos = new();

    // 동일 클립이 여러 Blend Tree 슬롯에서 발생시킨 중복 이벤트를 차단함.
    private int _lastFootstepFrame = -1;

    private void Awake()
    {
        _hero = GetComponent<Hero>();
    }

    #region [애니메이션 이벤트 수신 영역]

    /// <summary>
    /// 무적 플래그 On 이벤트
    /// Dodge/Parry/Ult 등 FSM 기반의 무적처리가 우선되고,
    /// 예외적인 공격 애니메이션 클립에 배치하는 방식으로 처리. 남발 ㄴㄴ
    /// </summary>
    public void OnInvincibleStateStart()
    {
        _hero.ActionRegistry.SetIsInvincible(true);
    }
    /// <summary>
    /// 무적 플래그 Off 이벤트
    /// 기본적으로 OnAttackEnd에서 ActionRegistry.SetIsInvincible(false);
    /// 가 호출 되지만, 예외적인 공격 모션이 있다면 추가로 이벤트 배치
    /// </summary>
    public void OnInvincibleStateEnd()
    {
        _hero.ActionRegistry.SetIsInvincible(false);
    }

    /// <summary>
    /// 연계 공격 기믹이 있는 모션의 시작지점에 배치
    /// </summary>
    public void OnComboInputStart()
    {
        if (_hero.ActionRegistry.IsComboLinkable) return;

        _hero.ActionRegistry.SetIsComboLinkable(true);
        Debug.Log("[Combo Index] : " + _hero.ActionRegistry.CurrentComboIndex);
    }

    #region [OnAdvanceComboIndex 래퍼 함수 영역 ]

    /// <summary>
    /// 추가 콤보 입력 없이 다음 모션으로 이어지는 공격에서 사용.
    /// 현재 모션의 마지막 HitFrame 이후,
    /// 다음 모션의 첫 HitFrame 이전에 한 번 배치.
    /// ComboIndex를 증가시키고 HitIndex를 0으로 초기화.
    /// </summary>
    private void AdvanceComboIndex(HeroState expectedState)
    {
        if (_hero.CurrentState != expectedState)
            return;

        _hero.ActionRegistry.IncrementComboIndex();
    }

    /// <summary>
    /// 현재 평꾹 구간에서 발생한 이벤트에 한해 다음 모션의 타격 인덱스로 진행함.
    /// </summary>
    public void OnChargeAttackAdvanceComboIndex(AnimationEvent animationEvent)
    {
        if (_hero.Mechanic?.CanProcessAnimationEvent(animationEvent) == false) return;
        AdvanceComboIndex(HeroState.ChargeAttack);
    }
    public void OnSkillAdvanceComboIndex()
    {
        AdvanceComboIndex(HeroState.Skill);
    }
    public void OnUltAdvanceComboIndex()
    {
        AdvanceComboIndex(HeroState.Ult);
    }
    public void OnChainAdvanceComboIndex()
    {
        AdvanceComboIndex(HeroState.ChainAttack);
    }
    public void OnDodgeCounterAdvanceComboIndex()
    {
        AdvanceComboIndex(HeroState.DodgeCounter);
    }
    public void OnQuickAssistAdvanceComboIndex()
    {
        AdvanceComboIndex(HeroState.QuickAssist);
    }
    public void OnAfterShockAdvanceComboIndex()
    {
        AdvanceComboIndex(HeroState.AfterShock);
    }

    #endregion

    /// <summary>
    /// 연계 공격 기믹이 있는 모션의 다음 공격시점 직전에 배치
    /// </summary>
    public void OnCheckCombo()
    {
        if (_hero.CurrentState != HeroState.Attack && _hero.CurrentState != HeroState.Skill) return;

        // 콤보 윈도우 예약 성공 시
        if (_hero.ActionRegistry.IsComboReserved)
        {
            _hero.ActionRegistry.IncrementComboIndex();
            _hero.ActionRegistry.SetIsComboReserved(false);
            _hero.ActionRegistry.CloseCancelWindow();

            _hero.ChangeState(_hero.CurrentState, isComboConnection: true);
        }
        else
        {
            // 오프필드 상황일 때
            if (_hero.ActionRegistry.IsOffFieldActionActive)
            {
                _hero.ActionRegistry.ResetCombo();
                _hero.ActionRegistry.SetOffFieldActionActive(false);
                _hero.ChangeState(HeroState.InActive);

                return;
            }
            // 일반적인 온필드 상황에서 콤보 윈도우 예약 X
            else
            {
                _hero.ActionRegistry.ResetCombo();

                // 하드코딩 파트 : Attack은 애니메이션 클립 하나를 분할해서 사용하기 때문에
                // 강제로 Idle로 전환시켜야됨.
                if (_hero.CurrentState == HeroState.Attack) _hero.BackToIdle();
            }

        }
    }
    /// <summary>
    /// 대쉬 공격 후반부에 배치 - 대쉬 공격 이후에 기본 공격 2단부터 이어서 발동되게 설계
    /// -> 현재는 Attack 애니메이션이 하나의 클립을 분할해서 사용하는 식이라 
    /// AttackCombo의 중간부터 실행하는 구조
    /// </summary>
    public void OnCheckDashAttackCombo()
    {
        if (_hero.CurrentState != HeroState.Attack) return;

        // 대쉬 공격 중 다음 공격 예약이 들어왔다면
        if (_hero.ActionRegistry.IsComboReserved)
        {
            // Attack 1타를 스킵하고 바로 2타로 가야 하므로 콤보 인덱스를 1로 초기화
            _hero.ActionRegistry.SetComboIndex(1);
            _hero.ActionRegistry.SetIsComboReserved(false);
            _hero.ActionRegistry.CloseCancelWindow();

            Debug.Log("[Attack Combo Index] : " + _hero.ActionRegistry.CurrentComboIndex);

            // 대쉬 공격 플래그를 꺼서 애니메이터가 일반 평타 트리거를 받도록 함
            _hero.Animation.SetIsDashAttack(false);

            // 이제 ActionRegistry 안에서 CurrentComboIndex 변경될때마다 자동적으로 처리
            //_hero.Animation.SetComboIndex(_hero.ActionRegistry.CurrentComboIndex);

            // Attack 2타 강제 실행
            _hero.Animation.DashAttackToComboAttack(_dashAttackToComboTiming);

            // 회전 및 록온 타겟 갱신 공통화 호출
            _hero.Movement.RefreshCombatDirection(_hero.HeroAxis);
        }
        else
        {
            // 연타가 없었다면 대쉬공격 후 Idle로 리셋
            _hero.ActionRegistry.ResetCombo();
            Debug.Log("대쉬공격 이후 예약 없음 - 종료");
        }
    }
    /// <summary>
    /// 이벤트의 기대 상태와 재생 출처를 검증한 뒤 공격과 체인 실행을 종료함.
    /// </summary>
    public void OnAttackEnd(AnimationEvent animationEvent)
    {
        if (animationEvent == null || !animationEvent.isFiredByAnimator) return;
        if (_hero.CurrentState != (HeroState)animationEvent.intParameter) return;

        // 공격 상태 여부 체크
        bool isValidEndState =
            _hero.CurrentState == HeroState.Attack ||
            _hero.CurrentState == HeroState.ChargeAttack ||
            _hero.CurrentState == HeroState.Skill ||
            _hero.CurrentState == HeroState.Ult ||
            _hero.CurrentState == HeroState.ParryCounter ||
            _hero.CurrentState == HeroState.DodgeCounter ||
            _hero.CurrentState == HeroState.QuickAssist ||
            _hero.CurrentState == HeroState.ChainAttack;

        if (!isValidEndState) return;
        if (!CanAcceptAttackEnd(animationEvent)) return;

        CompleteAttack();
    }

    /// <summary>
    /// 검증된 공격의 장부를 정리하고 온필드 또는 오프필드 종료를 처리함.
    /// </summary>
    private void CompleteAttack()
    {

        if (_hero.CurrentState == HeroState.ChainAttack)
        {
            ChainAttackManager.Instance?.CompleteChainExecution(_hero);
        }

        // 애니메이션 이벤트 무적은 일반 공격, 평꾹, 스킬에서 관리
        if (_hero.CurrentState == HeroState.Attack ||
            _hero.CurrentState == HeroState.ChargeAttack ||
            _hero.CurrentState == HeroState.Skill)
        {
            _hero.ActionRegistry.SetIsInvincible(false);
        }

        _hero.ActionRegistry.ResetCombo();

        // 오프필드 공격 여부 체크
        if (_hero.ActionRegistry.IsOffFieldActionActive)
        {
            _hero.ActionRegistry.SetOffFieldActionActive(false);
            _hero.ChangeState(HeroState.InActive);
        }
        else
        {
            // 온필드 공격일때는 Idle
            _hero.BackToIdle();
        }
    }

    /// <summary>
    /// 현재 행동의 클립과 정상 Idle 복귀에서 발생한 종료만 허용함.
    /// </summary>
    private bool CanAcceptAttackEnd(AnimationEvent animationEvent)
    {
        Animator animator = _hero.Animation.Animator;
        AnimatorStateInfo activeState = animator.GetCurrentAnimatorStateInfo(0);
        if (animator.IsInTransition(0))
        {
            AnimatorStateInfo nextState = animator.GetNextAnimatorStateInfo(0);
            if (!nextState.IsName("Base Layer.Idle_blend")) activeState = nextState;
        }

        return activeState.IsName("Base Layer.Idle_blend") ||
            animationEvent.animatorStateInfo.fullPathHash == activeState.fullPathHash;
    }

    /// <summary>
    /// 종료 클립 없이 Idle로 복귀하는 평꾹의 상태 이탈을 검증하여 종료함.
    /// </summary>
    public void OnChargeAttackStateExit(int layerIndex)
    {
        if (layerIndex != 0 || _hero.CurrentState != HeroState.ChargeAttack) return;
        Animator animator = _hero.Animation.Animator;
        AnimatorStateInfo destination = animator.IsInTransition(0)
            ? animator.GetNextAnimatorStateInfo(0)
            : animator.GetCurrentAnimatorStateInfo(0);
        if (!destination.IsName("Base Layer.Idle_blend")) return;
        CompleteAttack();
    }

    public void OnConvertEnd()
    {
        if (_hero.CurrentState != HeroState.ConvertCombat) return;

        if (_hero.HeroAxis.magnitude > 0.1f)
        {
            if (_hero.Movement.IsDashActive)
            {
                _hero.ChangeState(HeroState.Sprint);
            }
            else
            {
                _hero.ChangeState(_hero.Movement.IsWalkMode ? HeroState.Walk : HeroState.Run);
            }
        }
        else
        {
            _hero.ChangeState(HeroState.Idle);
        }
    }
    public void AttachToHand()
    {
        if (_hero.Weapon != null)
        {
            _hero.Weapon.ChangeWeaponDisplayState(HeroWeapon.WeaponDisplayState.InCombat);
        }
    }
    /// <summary>
    /// 입장 연출 중에는 무장을 유지하고 그 외 상태에서는 등 무기로 전환함.
    /// </summary>
    public void AttachToBack()
    {
        if (_hero.CurrentState == HeroState.Entry) return;
        if (_hero.Weapon != null)
        {
            _hero.Weapon.ChangeWeaponDisplayState(HeroWeapon.WeaponDisplayState.OutCombat);
        }
    }


    /// <summary>
    /// 현재 동작의 이벤트에 한해 지정한 방향으로 애니메이션 이동을 시작함.
    /// </summary>
    public void OnAnimMoveStart(AnimationEvent animationEvent)
    {
        if (_hero.Mechanic?.CanProcessAnimationEvent(animationEvent) == false) return;
        _hero.Movement.BeginAnimMovement(
            (AnimMoveDirectionType)animationEvent.intParameter
        );

        _hero.ActionRegistry.SetIsTargetTrackingActive(false);
        _hero.ActionRegistry.ActivateAnimMove();
    }

    /// <summary>
    /// 현재 동작의 이벤트에 한해 타겟 방향을 추적하는 애니메이션 이동을 시작함.
    /// </summary>
    public void OnTrackingAnimMoveStart(AnimationEvent animationEvent)
    {
        if (_hero.Mechanic?.CanProcessAnimationEvent(animationEvent) == false) return;
        _hero.Movement.BeginAnimMovement(
            (AnimMoveDirectionType)animationEvent.intParameter
        );

        _hero.ActionRegistry.SetIsTargetTrackingActive(true);
        _hero.ActionRegistry.ActivateAnimMove();
    }

    /// <summary>
    /// 애니메이션 이동을 종료하고 고정 이동 설정을 해제함.
    /// </summary>
    public void OnAnimMoveEnd(AnimationEvent animationEvent)
    {
        if (_hero.Mechanic?.CanProcessAnimationEvent(animationEvent) == false) return;
        _hero.ActionRegistry.DeactivateAnimMove();
        _hero.Movement.ClearFixedAnimMovement();
    }

    /// <summary>
    /// 기존에는 패링 애니메이션에서 호출하는 방식이었으나,
    /// CombatManager-ProcessHit 에서 패링처리 될때 호출되는 방식
    /// ..이었으나, 해당 파이프라인에서
    /// Hero - ResolveParryImpact 메서드 호출
    /// 직접 호출
    /// </summary>
    //public void OnParryMoveStart()
    //{
    //    if (_hero.CurrentState != HeroState.ParryIn) return;

    //    _hero.ActionRegistry.ActivateAnimMove();
    //    _hero.Movement.SetCurrentParryAssistSpeed(_hero.Movement.MaxParryAssistSpeed);
    //}

    public void OnSwapInEnd()
    {
        if (_hero.CurrentState != HeroState.SwapIn) return;

        // 잔상으로 등장 연출을 하던 중이었다면, 연출이 끝났으니 명시적으로 SwapOut 전환
        if (_hero.ActionRegistry.IsOffFieldActionActive)
        {
            _hero.ChangeState(HeroState.SwapOut);
        }
        // 일반적인 온필드 메인 등장 완료인 경우
        else
        {
            _hero.ChangeState(HeroState.Idle);
        }
    }
    /// <summary>
    /// 퇴장 이동과 애니메이션을 유지한 채 디더 퇴장을 시작함.
    /// </summary>
    public void OnSwapOutFadeStart()
    {
        if (_hero.CurrentState != HeroState.SwapOut) return;

        _hero.SwapSystem.BeginSwapOutFade(CompleteSwapOut);
    }

    /// <summary>
    /// 디더 퇴장 완료 후 잔상 상태를 해제하고 비활성 상태로 전환함.
    /// </summary>
    private void CompleteSwapOut()
    {
        if (_hero.CurrentState != HeroState.SwapOut) return;

        _hero.ActionRegistry.SetOffFieldActionActive(false);
        _hero.ChangeState(HeroState.InActive);
    }

    public void OnParryPrepareEnd()
    {
        _hero.HandleParryPrepareEnd();
    }
    /// <summary>
    /// 패링 성공 모션의 반격 시점에서 예약을 실행하거나 입력 창을 엶.
    /// </summary>
    public void OnParryCounterCheck()
    {
        if (!_hero.IsParryImpactActive) return;

        if (_hero.ActionRegistry.IsParryCounterReserved)
        {
            _hero.ChangeState(HeroState.ParryCounter);
        }
        else
        {
            _hero.ActionRegistry.SetIsParryCounterWindowOpen(true);
        }
    }
    public void OnParryEnd()
    {
        if (_hero.CurrentState != HeroState.ParryIn) return;

        _hero.ActionRegistry.SetIsParryCounterWindowOpen(false);

        // 패링 반격 없이 패링 모션이 종료되었으므로 대상 정리
        _hero.ClearCounterTarget();

        // 패링후 교대가 이루어져서 오프필드 상태였다면 InActive 전환
        if (_hero.ActionRegistry.IsOffFieldActionActive)
        {
            _hero.ActionRegistry.SetOffFieldActionActive(false);
            _hero.ChangeState(HeroState.InActive);
        }
        else
        {
            // 일반적인 온필드 패링 마무리 후에는 원래 싸우던 중이므로 Idle 전환
            _hero.BackToIdle();
        }
    }
    public void OnParryCameraEnd()
    {
        if (_hero.CurrentState != HeroState.ParryIn) return;
        if(CombatPresentationManager.Instance != null)
        {
            CombatPresentationManager.Instance.TryStopParryCamera(_hero);
        }

    }

    /// <summary>
    /// 일반 공격 및 대시 공격 상태에서만 캔슬 윈도우를 엶.
    /// </summary>
    public void OnAttackCancelWindowOpen()
    {
        if (_hero.CurrentState != HeroState.Attack) return;

        _hero.ActionRegistry.OpenCancelWindow();
    }

    /// <summary>
    /// 평꾹 상태에서만 캔슬 윈도우를 엶.
    /// </summary>
    public void OnChargeAttackCancelWindowOpen(AnimationEvent animationEvent)
    {
        if (_hero.Mechanic?.CanProcessAnimationEvent(animationEvent) == false) return;
        if (_hero.CurrentState != HeroState.ChargeAttack) return;

        _hero.ActionRegistry.OpenCancelWindow();
    }

    /// <summary>
    /// 스킬 상태에서만 캔슬 윈도우를 엶.
    /// </summary>
    public void OnSkillCancelWindowOpen()
    {
        if (_hero.CurrentState != HeroState.Skill) return;

        _hero.ActionRegistry.OpenCancelWindow();
    }

    /// <summary>
    /// 스킬 상태에서만 캔슬 윈도우를 열고 EX 모드를 해제하여 후딜 회피를 허용함.
    /// </summary>
    public void OnExSkillCancelWindowOpen()
    {
        if (_hero.CurrentState != HeroState.Skill) return;

        _hero.ActionRegistry.OpenCancelWindow();
        _hero.SkillSystem.SetIsExSkillMode(false);
    }

    /// <summary>
    /// 회피 상태에서만 캔슬 윈도우를 엶.
    /// </summary>
    public void OnDodgeCancelWindowOpen()
    {
        if (_hero.CurrentState != HeroState.Dodge) return;

        _hero.ActionRegistry.OpenCancelWindow();
    }

    /// <summary>
    /// 회피 반격 상태에서만 캔슬 윈도우를 엶.
    /// </summary>
    public void OnDodgeCounterCancelWindowOpen()
    {
        if (_hero.CurrentState != HeroState.DodgeCounter) return;

        _hero.ActionRegistry.OpenCancelWindow();
    }

    /// <summary>
    /// 교대 진입 상태에서만 캔슬 윈도우를 엶.
    /// </summary>
    public void OnSwapInCancelWindowOpen()
    {
        if (_hero.CurrentState != HeroState.SwapIn) return;

        _hero.ActionRegistry.OpenCancelWindow();
    }

    /// <summary>
    /// 빠른 지원 상태에서만 캔슬 윈도우를 엶.
    /// </summary>
    public void OnQuickAssistCancelWindowOpen()
    {
        if (_hero.CurrentState != HeroState.QuickAssist) return;

        _hero.ActionRegistry.OpenCancelWindow();
    }

    /// <summary>
    /// 궁극기 상태에서만 캔슬 윈도우를 엶.
    /// </summary>
    public void OnUltCancelWindowOpen()
    {
        if (_hero.CurrentState != HeroState.Ult) return;

        _hero.ActionRegistry.OpenCancelWindow();
    }

    public void OnQuickSwapWindowOpen()
    {
        _hero.SwapSystem.SetIsQuickSwapWindowOpen(true);
    }
    public void OnQuickSwapWindowClose()
    {
        _hero.SwapSystem.SetIsQuickSwapWindowOpen(false);
    }
    public void OnDeathAnimationEnd()
    {
        if (_hero.CurrentState != HeroState.Dead) return;
        if (PartySwapManager.Instance == null) return;

        // 다음 영웅으로 교대 및 Renderer,Collider,CharCon Off
        PartySwapManager.Instance.HandleDeathSwap(_hero);
    }

    public void OnAttackExecuteFrame()
    {
        if (_hero.CurrentState != HeroState.Attack) return;

        // 현재 평타가 몇 타째인지 Registry에서 확인 (ex: 1타 모션이면 0)
        int combo = _hero.ActionRegistry.CurrentComboIndex;

        // 히트 수 관리 메서드 호출
        // HitFrame 이벤트 첫 호출이면 0 반환하고 내부 카운트는 1로 증가,
        // 두 번째 호출이면 1 반환하고 2로 증가.
        int hit = _hero.ActionRegistry.GetHitIndexAndAdvance();

        // 우선적으로 hit는 배제, 추후 HeroHitChecker까지 수정되면 hit 까지 전달
        _hero.HitChecker.ExecuteAttackHitCheck(combo, hit);
    }


    /// <summary>
    /// 평꾹 상태의 차지 공격 데이터로 타격을 실행함.
    /// </summary>
    public void OnChargeAttackExecuteFrame(AnimationEvent animationEvent)
    {
        if (_hero.Mechanic?.CanProcessAnimationEvent(animationEvent) == false) return;
        if (_hero.CurrentState != HeroState.ChargeAttack) return;

        int combo = _hero.ActionRegistry.CurrentComboIndex;
        int hit = _hero.ActionRegistry.GetHitIndexAndAdvance();

        _hero.HitChecker.ExecuteChargeAttackHitCheck(combo, hit);
    }
    /// <summary>
    /// 평꾹 막타의 타격 인덱스를 진행하고 전용 데이터로 판정을 실행함.
    /// </summary>
    public void OnChargeAttackFinisherExecuteFrame(AnimationEvent animationEvent)
    {
        if (_hero.Mechanic?.CanProcessAnimationEvent(animationEvent) == false) return;
        if (_hero.CurrentState != HeroState.ChargeAttack) return;

        int hit = _hero.ActionRegistry.GetHitIndexAndAdvance();
        _hero.HitChecker.ExecuteChargeAttackFinisherHitCheck(hit);
    }

    public void OnDashAttackExecuteFrame()
    {
        if (_hero.CurrentState != HeroState.Attack) return;

        int combo = _hero.ActionRegistry.CurrentComboIndex;
        int hit = _hero.ActionRegistry.GetHitIndexAndAdvance();

        _hero.HitChecker.ExecuteDashAttackHitCheck(combo, hit);
    }
    public void OnSkillExecuteFrame()
    {
        if (_hero.CurrentState != HeroState.Skill) return;

        int combo = _hero.ActionRegistry.CurrentComboIndex;
        int hit = _hero.ActionRegistry.GetHitIndexAndAdvance();

        _hero.HitChecker.ExecuteSkillHitCheck(combo, hit);

    }
    public void OnExSkillExecuteFrame()
    {
        if (_hero.CurrentState != HeroState.Skill) return;

        int combo = _hero.ActionRegistry.CurrentComboIndex;
        int hit = _hero.ActionRegistry.GetHitIndexAndAdvance();

        _hero.HitChecker.ExecuteExSkillHitCheck(combo, hit);
    }
    /// <summary>
    /// 에너지 소모없는 강특 막타 모션이 있는 Maria같은 Hero를 위한 이벤트 핸들러
    /// </summary>
    public void OnExSkillFinaleExecuteFrame()
    {
        if (_hero.CurrentState != HeroState.Skill) return;

        int hit = _hero.ActionRegistry.GetHitIndexAndAdvance();

        // 피니시 전용 판정 호출 (막타는 단발성이므로 combo 인덱스가 필요 없음)
        _hero.HitChecker.ExecuteExSkillFinaleHitCheck(hit);
    }
    public void OnUltExecuteFrame()
    {
        if (_hero.CurrentState != HeroState.Ult) return;

        int combo = _hero.ActionRegistry.CurrentComboIndex;
        int hit = _hero.ActionRegistry.GetHitIndexAndAdvance();

        _hero.HitChecker.ExecuteUltHitCheck(combo, hit);
    }
    public void OnUltCutSceneTrigger()
    {
        if (CombatPresentationManager.Instance.IsPresenting) return;

        if (_hero.UltTimelineAsset != null && _hero.UltVcam != null)
        {
            CombatPresentationManager.Instance.PlayUltCutscene(
                _hero,
                _hero.UltTimelineAsset,
                _hero.UltVcam,
                _hero.UltCutsceneDuration
            );
            // 정상적으로 연출 작동 후, 연출용 전용 위치로 이동
            _hero.Movement.ExecuteUltMovement(LockOnManager.Instance.MagneticTarget);
        }
        else
        {
            Debug.Log($"{gameObject.name}에 ultVcam 또는 TimelineAsset 할당 X");
        }
    }

    public void OnParryExecuteFrame()
    {
        /// <summary>
        /// 기능상 HeroAnimationEventHandler에 선언돼 있지만,
        /// 실제로는 패링 파이프라인 구조상 애니메이션 이벤트로 호출되는 메서드 X
        /// -> 해당 과정은 CombatManager가 책임을 가지도록 바뀜
        /// </summary>
    }
    public void OnParryCounterExecuteFrame()
    {
        if (_hero.CurrentState != HeroState.ParryCounter) return;

        int combo = _hero.ActionRegistry.CurrentComboIndex;
        int hit = _hero.ActionRegistry.GetHitIndexAndAdvance();

        _hero.HitChecker.ExecuteParryCounterHitCheck(combo, hit);
    }
    public void OnDodgeCounterExecuteFrame()
    {
        if (_hero.CurrentState != HeroState.DodgeCounter) return;

        int combo = _hero.ActionRegistry.CurrentComboIndex;
        int hit = _hero.ActionRegistry.GetHitIndexAndAdvance();

        _hero.HitChecker.ExecuteDodgeCounterHitCheck(combo, hit);
    }
    public void OnChainAttackExecuteFrame()
    {
        if (_hero.CurrentState != HeroState.ChainAttack) return;

        int combo = _hero.ActionRegistry.CurrentComboIndex;
        int hit = _hero.ActionRegistry.GetHitIndexAndAdvance();

        _hero.HitChecker.ExecuteChainAttackHitCheck(combo, hit);
    }
    public void OnQuickAssistExecuteFrame()
    {
        if (_hero.CurrentState != HeroState.QuickAssist) return;

        int combo = _hero.ActionRegistry.CurrentComboIndex;
        int hit = _hero.ActionRegistry.GetHitIndexAndAdvance();

        _hero.HitChecker.ExecuteQuickAssistHitCheck(combo, hit);
    }


    /// <summary>검 궤적을 사용하는 공격 상태에서 연출을 시작한다.</summary>
    public void OnSwordTrailStart()
    {
        bool canPlayTrail =
            _hero.CurrentState == HeroState.Attack ||
            _hero.CurrentState == HeroState.ChargeAttack ||
            _hero.CurrentState == HeroState.Skill ||
            _hero.CurrentState == HeroState.Ult ||
            _hero.CurrentState == HeroState.ParryIn ||
            _hero.CurrentState == HeroState.ParryCounter ||
            _hero.CurrentState == HeroState.DodgeCounter ||
            _hero.CurrentState == HeroState.ChainAttack;

        if (!canPlayTrail) return;

        _hero.VFX?.PlaySwordTrail();
    }
    public void OnSwordTrailEnd()
    {
        _hero.VFX?.StopSwordTrail();
    }

    public void OnHitReactionEnd(int hitType)
    {
        _hero.HitReaction.CompleteReaction((HitType)hitType);
    }

    public void OnSuperArmorStart()
    {
        _hero.ActionRegistry.SetIsSuperArmorActive(true);
    }
    public void OnSuperArmorEnd()
    {
        _hero.ActionRegistry.SetIsSuperArmorActive(false);
    }

    #endregion


    #region [SFX 재생 요청 영역]

    /// <summary>
    /// 현재 동작의 대표 클립에서 발생한 발소리를 프레임당 한 번 재생함.
    /// Hero Movement 애니메이션 State가 블렌드 트리로 구성돼있다 보니,
    /// 블렌딩 과정에서 두 클립 모두 이벤트 핸들러가 호출되는 문제 해결을 위한 로직.
    /// </summary>
    public void OnFootstepSFX(AnimationEvent animationEvent)
    {
        if (_hero == null || _hero.Audio == null)
            return;

        // 완전히 퇴장한 Hero의 발소리 이벤트를 차단함.
        if (_hero.CurrentState == HeroState.InActive)
            return;

        if (animationEvent == null || !animationEvent.isFiredByAnimator)
            return;

        if (_lastFootstepFrame == Time.frameCount)
            return;

        if (!CanPlayFootstep(animationEvent))
            return;

        _lastFootstepFrame = Time.frameCount;
        _hero.Audio.PlayFootStep();
    }

    /// <summary>
    /// 기본 레이어의 재생 대상과 대표 클립에 해당하는 이벤트인지 반환함.
    /// </summary>
    private bool CanPlayFootstep(AnimationEvent animationEvent)
    {
        Animator animator = _hero.Animation.Animator;

        _footstepClipInfos.Clear();

        AnimatorStateInfo targetState;

        if (animator.IsInTransition(0))
        {
            // 전환 중에는 진입할 상태를 기준으로 이전 상태의 이벤트를 차단함.
            targetState = animator.GetNextAnimatorStateInfo(0);
            animator.GetNextAnimatorClipInfo(0, _footstepClipInfos);
        }
        else
        {
            targetState = animator.GetCurrentAnimatorStateInfo(0);
            animator.GetCurrentAnimatorClipInfo(0, _footstepClipInfos);
        }

        if (animationEvent.animatorStateInfo.fullPathHash !=
            targetState.fullPathHash)
        {
            return false;
        }

        AnimationClip dominantClip = GetDominantFootstepClip();

        return dominantClip != null &&
            animationEvent.animatorClipInfo.clip == dominantClip;
    }

    /// <summary>
    /// 가중치가 가장 큰 클립을 선택하며 동률이면 먼저 조회된 클립을 유지함.
    /// </summary>
    private AnimationClip GetDominantFootstepClip()
    {
        AnimationClip dominantClip = null;
        float highestWeight = 0f;

        foreach (AnimatorClipInfo clipInfo in _footstepClipInfos)
        {
            if (clipInfo.clip == null || clipInfo.weight <= highestWeight)
                continue;

            highestWeight = clipInfo.weight;
            dominantClip = clipInfo.clip;
        }

        return dominantClip;
    }

    #endregion
}
