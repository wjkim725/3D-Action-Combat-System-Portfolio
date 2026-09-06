using UnityEngine;
using UnityEngine.Rendering;
using static HeroSwapSystem;

/// <summary>
/// Hero에서 사용되는 이벤트 애니메이션 이벤트 핸들러 전담 컴포넌트
/// </summary>
public class HeroAnimationEventHandler : MonoBehaviour
{
    private Hero _hero;

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
            // 퀵스왑 상황일때
            if (_hero.ActionRegistry.IsOffFieldActionActive)
            {
                // 장부 비워주고 return
                // -> 자연스럽게 뒤에 나올 OnAttackEnd 이벤트에서 SwapOut 상태로 전환
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
            _hero.Animation.DashAttackToComboAttack(0.35f);

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
    public void OnAttackEnd()
    {
        // 공격 상태 여부 체크
        bool isValidEndState =
            _hero.CurrentState == HeroState.Attack ||
            _hero.CurrentState == HeroState.Skill ||
            _hero.CurrentState == HeroState.Ult ||
            _hero.CurrentState == HeroState.ParryCounter ||
            _hero.CurrentState == HeroState.DodgeCounter ||
            _hero.CurrentState == HeroState.QuickAssist;

        if (!isValidEndState) return;

        // 애니메이션 이벤트 무적은 Attack / Skill만 관리
        if (_hero.CurrentState == HeroState.Attack ||
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

    public void OnChainAttackEnd()
    {
        if (_hero.CurrentState != HeroState.ChainAttack) return;

        _hero.ActionRegistry.ResetCombo();

        if (ChainAttackManager.Instance != null)
        {
            ChainAttackManager.Instance.CompleteChainExecution(_hero);
        }

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

    // TODO: 직접 ChangeState 하지않고 Hero 본체로 신호 쏴주는 식으로 변경
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
    public void AttachToBack()
    {
        if (_hero.Weapon != null)
        {
            _hero.Weapon.ChangeWeaponDisplayState(HeroWeapon.WeaponDisplayState.OutCombat);
        }
    }
    public void OnDeathAnimationEnd()
    {
        if (_hero.CurrentState != HeroState.Dead) return;
        if (PartySwapManager.Instance == null) return;

        // 다음 영웅으로 교대 및 Renderer,Collider,CharCon Off
        PartySwapManager.Instance.HandleDeathSwap(_hero);
    }


    public void OnAnimMoveStart()
    {
        _hero.ActionRegistry.ActivateAnimMove();
    }
    public void OnAnimMoveEnd()
    {
        _hero.ActionRegistry.DeactivateAnimMove();
    }

    /// <summary>
    /// 진행 방향 지정하고 IsAnimMoveActive 플래그 켜줌
    /// ChainAttack 등에서 사용
    /// </summary>
    public void OnDirectionalAnimMoveStart(int direction)
    {
        _hero.Movement.SetAnimMoveDirection((AnimMoveDirectionType)direction);

        _hero.ActionRegistry.ActivateAnimMove();
    }

    /// <summary>
    /// 교대 애니메이션 이동 구간 시작
    /// 이동 종료는 OnAnimMoveEnd 사용
    /// </summary>
    /// <param name="direction">1 - Forward / 2 - Backward</param>
    public void OnSwapAnimMoveStart(int direction)
    {
        if (_hero.CurrentState != HeroState.SwapIn && _hero.CurrentState != HeroState.SwapOut) return;

        AnimMoveDirectionType directionType = (AnimMoveDirectionType)direction;

        if (directionType != AnimMoveDirectionType.Forward &&
            directionType != AnimMoveDirectionType.Backward) return;

        _hero.Movement.BeginSwapAnimMovement(directionType);
        _hero.ActionRegistry.ActivateAnimMove();
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
    public void OnSwapOutEnd()
    {
        if (_hero.CurrentState != HeroState.SwapOut) return;

        _hero.ActionRegistry.SetOffFieldActionActive(false);
        _hero.ChangeState(HeroState.InActive);
    }

    public void OnParryCounterCheck()
    {
        if (_hero.CurrentState != HeroState.ParryIn) return;

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

    public void OnParryCounterMoveStart()
    {
        if (_hero.CurrentState != HeroState.ParryCounter) return;

        _hero.ActionRegistry.ActivateAnimMove();
    }
    public void OnParryCounterMoveEnd()
    {
        if (_hero.CurrentState != HeroState.ParryCounter) return;

        _hero.ActionRegistry.DeactivateAnimMove();
    }

    public void OnCancelWindowOpen()
    {
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
    /// <summary>
    /// Quick Assist Window를 여는 커맨드의 HitFrame에 배치
    /// </summary>
    /// <param name="direction">0 - Next / 1 - Prev</param>
    public void OnQuickAssistWindowOpen(int direction)
    {
        if (QuickAssistManager.Instance == null ||
            LockOnManager.Instance == null)
        {
            return;
        }

        SwapDirection swapDirection = (SwapDirection)direction;

        if (swapDirection != SwapDirection.Next &&
            swapDirection != SwapDirection.Prev)
        {
            return;
        }

        Transform magneticTarget =
            LockOnManager.Instance.MagneticTarget;

        if (magneticTarget == null)
        {
            return;
        }

        Enemy assistTarget =
            magneticTarget.GetComponentInParent<Enemy>();

        if (assistTarget == null)
        {
            return;
        }

        QuickAssistManager.Instance.TryOpen(
            _hero,
            assistTarget,
            swapDirection
        );
    }

    public void OnAttackHitFrame()
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
    public void OnChargeAttackHitFrame()
    {
        if (_hero.CurrentState != HeroState.Attack) return;

        int combo = _hero.ActionRegistry.CurrentComboIndex;
        int hit = _hero.ActionRegistry.GetHitIndexAndAdvance();

        _hero.HitChecker.ExecuteChargeAttackHitCheck(combo, hit);
    }
    public void OnDashAttackHitFrame()
    {
        if (_hero.CurrentState != HeroState.Attack) return;

        int combo = _hero.ActionRegistry.CurrentComboIndex;
        int hit = _hero.ActionRegistry.GetHitIndexAndAdvance();

        _hero.HitChecker.ExecuteDashAttackHitCheck(combo, hit);
    }
    public void OnSkillHitFrame()
    {
        if (_hero.CurrentState != HeroState.Skill) return;

        int combo = _hero.ActionRegistry.CurrentComboIndex;
        int hit = _hero.ActionRegistry.GetHitIndexAndAdvance();

        _hero.HitChecker.ExecuteSkillHitCheck(combo, hit);

    }
    public void OnExSkillHitFrame()
    {
        if (_hero.CurrentState != HeroState.Skill) return;

        int combo = _hero.ActionRegistry.CurrentComboIndex;
        int hit = _hero.ActionRegistry.GetHitIndexAndAdvance();

        _hero.HitChecker.ExecuteExSkillHitCheck(combo, hit);
    }
    /// <summary>
    /// 에너지 소모없는 강특 막타 모션이 있는 Maria같은 Hero를 위한 이벤트 핸들러
    /// </summary>
    public void OnExSkillFinaleHitFrame()
    {
        if (_hero.CurrentState != HeroState.Skill) return;

        int hit = _hero.ActionRegistry.GetHitIndexAndAdvance();

        // 피니시 전용 판정 호출 (막타는 단발성이므로 combo 인덱스가 필요 없음)
        _hero.HitChecker.ExecuteExSkillFinaleHitCheck(hit);
    }
    public void OnUltHitFrame()
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

    public void OnParryHitFrame()
    {
        /// <summary>
        /// 기능상 HeroAnimationEventHandler에 선언돼 있지만,
        /// 실제로는 패링 파이프라인 구조상 애니메이션 이벤트로 호출되는 메서드 X
        /// -> 해당 과정은 CombatManager가 책임을 가지도록 바뀜
        /// </summary>
    }
    public void OnParryCounterHitFrame()
    {
        if (_hero.CurrentState != HeroState.ParryCounter) return;

        int combo = _hero.ActionRegistry.CurrentComboIndex;
        int hit = _hero.ActionRegistry.GetHitIndexAndAdvance();

        _hero.HitChecker.ExecuteParryCounterHitCheck(combo, hit);
    }
    public void OnDodgeCounterHitFrame()
    {
        if (_hero.CurrentState != HeroState.DodgeCounter) return;

        int combo = _hero.ActionRegistry.CurrentComboIndex;
        int hit = _hero.ActionRegistry.GetHitIndexAndAdvance();

        _hero.HitChecker.ExecuteDodgeCounterHitCheck(combo, hit);
    }
    public void OnChainAttackHitFrame()
    {
        if (_hero.CurrentState != HeroState.ChainAttack) return;

        int combo = _hero.ActionRegistry.CurrentComboIndex;
        int hit = _hero.ActionRegistry.GetHitIndexAndAdvance();

        _hero.HitChecker.ExecuteChainAttackHitCheck(combo, hit);
    }
    public void OnQuickAssistHitFrame()
    {
        if (_hero.CurrentState != HeroState.QuickAssist) return;

        int combo = _hero.ActionRegistry.CurrentComboIndex;
        int hit = _hero.ActionRegistry.GetHitIndexAndAdvance();

        _hero.HitChecker.ExecuteQuickAssistHitCheck(combo, hit);
    }


    public void OnSwordTrailStart()
    {
        bool canPlayTrail =
            _hero.CurrentState == HeroState.Attack ||
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
}