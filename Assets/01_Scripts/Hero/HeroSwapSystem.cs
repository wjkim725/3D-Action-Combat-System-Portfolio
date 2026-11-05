using UnityEngine;

public class HeroSwapSystem : MonoBehaviour
{
    public enum SwapType { Normal, QuickAssist, Parry, Chain }
    
    private Hero _hero;
    private HeroDitherController _ditherController;

    #region [프로퍼티 영역]

    public SwapType CurrentSwapType { get; set; } = SwapType.Normal;

    /// <summary>
    /// 연속 패링 교대의 상태 전환 중 카메라 종료를 생략할지 반환함.
    /// </summary>
    public bool IsParryCameraHandoffActive { get; private set; }

    /// <summary>
    /// 애니메이션 이벤트 같이 명시적으로 교대 가능 여부를 관리하는 플래그
    /// 휴먼 에러 방지를 위해서 Hero-ExitState에서 범용적으로 Open
    /// </summary>
    public bool IsQuickSwapWindowOpen { get; private set; } = true;

    /// <summary>
    /// 일반 교대 / 빠른 지원 요청 가능 여부 <- 빠지는 추후 분리?
    /// PartySwapManager의 Input Handler 에서 사용됨.
    /// </summary>
    public bool CanRequestSwap
    {
        get
        {
            if (_hero.Status.IsDead || _hero.CurrentState == HeroState.Entry) return false;
            if (!IsQuickSwapWindowOpen) return false;

            if (_hero.CurrentState == HeroState.Ult) return false;
            if (_hero.CurrentState == HeroState.ChainAttack) return false;
            if (_hero.CurrentState == HeroState.ParryIn) return false;
            if (_hero.CurrentState == HeroState.ParryCounter) return false;
            if (_hero.CurrentState == HeroState.QuickAssist) return false;
            if (_hero.CurrentState == HeroState.Hit) return false;
            // Knockback 피격시에는 빠른지원 교대 요청만 가능
            if (_hero.CurrentState == HeroState.Knockback) return false;    

            return true;
        }
    }

    /// <summary>
    /// 패링지원 교대 요청 가능 여부
    /// 연속 패링을 위해 ParryIn·ParryCounter 상태에서도 허용
    /// </summary>
    public bool CanRequestParry
    {
        get
        {
            if (_hero.Status.IsDead || _hero.CurrentState == HeroState.Entry) return false;

            if (_hero.CurrentState == HeroState.Ult) return false;
            if (_hero.CurrentState == HeroState.ChainAttack) return false;

            return true;
        }
    }

    /// <summary>
    /// 다음 Hero의 교대 액션 가능 여부 플래그
    /// GetValidNextHero / GetValidPrevHero 에서 사용됨.
    /// </summary>
    public bool CanBeSwappedIn
    {
        get
        {
            // 사망 시에는 교대 불가
            if(_hero.Status.IsDead)
            {
                return false;
            }

            // 오프필드 액션 중에는 교대 불가
            if (_hero.ActionRegistry.IsOffFieldActionActive)
            {
                return false;
            }

            if(_hero.CurrentState != HeroState.InActive) return false;

            // 위의 억제 기믹들에 걸리지 않았다면 언제든 교대 가능(true)
            return true;
        }
    }

    #endregion


    private void Awake()
    {
        _hero = GetComponent<Hero>();
        _ditherController = GetComponent<HeroDitherController>();
    }

    public void SetIsQuickSwapWindowOpen(bool isOpen)
    {
        IsQuickSwapWindowOpen = isOpen;
    }
    public void SwitchHeroRenderer(bool isPresent)
    {
        // 1. 본체 렌더러 On/Off
        if (_hero.BodyRenderers != null)
        {
            foreach (var r in _hero.BodyRenderers) if (r != null) r.enabled = isPresent;
        }

        // 2. 무기 렌더러 On/Off
        if (_hero.Weapon != null)
        {
            if (isPresent)
            {
                // 온필드로 나올 때는 Hero의 IsCombat 플래그에 따라 위치 제어
                if (_hero != null)
                {
                    if (_hero.CurrentState == HeroState.Entry || _hero.IsCombat)
                    {
                        _hero.Weapon.ChangeWeaponDisplayState(HeroWeapon.WeaponDisplayState.InCombat);
                    }
                    else
                    {
                        _hero.Weapon.ChangeWeaponDisplayState(HeroWeapon.WeaponDisplayState.OutCombat);
                    }
                }
            }
            else
            {
                // 오프필드로 숨겨질 때는 무기 렌더러 그룹 전체를 완전 Hide
                _hero.Weapon.ChangeWeaponDisplayState(HeroWeapon.WeaponDisplayState.Hide);
            }
        }
    }
    /// <summary>
    /// 다른 Collider가 Hero에 의해 차단되는지 설정함.
    /// 퇴장 이동을 유지하기 위해 CharacterController 자체는 끄지 않음.
    /// -> Collider 컴포넌트를 꺼버리면 CharacterController도 같이 꺼져버려서 해당 방식으로 구현
    /// </summary>
    public void SwitchHeroCollider(bool isPresent)
    {
        CharacterController controller = _hero.Movement.CharacterController;
        if (controller == null) return;

        controller.detectCollisions = isPresent;
    }

    /// <summary>
    /// 중력 속도를 초기화하고 CharacterController의 활성 상태를 설정함.
    /// 완전 퇴장 시 비활성화하고 등장 전에 다시 활성화함.
    /// </summary>
    public void SwitchHeroCharacterController(bool isPresent)
    {
        _hero.Movement.ResetGravity();

        CharacterController controller = _hero.Movement.CharacterController;
        if (controller == null) return;

        controller.enabled = isPresent;
    }
    /// <summary>
    /// 최초 파티 배치에서 교대 Fade 없이 표시와 충돌 상태를 즉시 적용함.
    /// </summary>
    public void SetInitialFieldPresence(bool isPresent)
    {
        _ditherController?.SetFadeImmediate(isPresent ? 1f : 0f);
        SwitchHeroRenderer(isPresent);
        SwitchHeroCharacterController(isPresent);
        SwitchHeroCollider(isPresent);
    }

    public void PrepareSwapInEntrance()
    {
        _ditherController?.SetFadeImmediate(0f);

        SwitchHeroRenderer(true);
        SwitchHeroCharacterController(true);
        SwitchHeroCollider(true);

        _ditherController?.FadeIn();
    }
    /// <summary>
    /// 이동과 충돌을 비활성화하고 남아 있는 표시를 정리함.
    /// 이미 숨겨진 경우 디더를 반복하지 않음.
    /// </summary>
    public void SetInActiveEntrance()
    {
        SwitchHeroCharacterController(false);
        SwitchHeroCollider(false);

        if (_ditherController == null || _ditherController.IsFullyHidden)
        {
            SwitchHeroRenderer(false);
            return;
        }

        _ditherController.FadeOut(() => SwitchHeroRenderer(false));
    }


    #region [퇴장 시퀀스 영역]

    /// <summary>
    /// 퇴장 동작을 유지하며 디더를 진행하고 완전히 숨긴 뒤 완료를 통보함.
    /// </summary>
    public void BeginSwapOutFade(System.Action onComplete)
    {
        if (_hero.CurrentState != HeroState.SwapOut) return;

        if (_ditherController == null || _ditherController.IsFullyHidden)
        {
            CompleteSwapOutFade(onComplete);
            return;
        }

        _ditherController.FadeOut(() => CompleteSwapOutFade(onComplete));
    }

    /// <summary>
    /// 퇴장 상태가 유지된 경우 렌더러를 숨기고 완료 콜백을 실행함.
    /// </summary>
    private void CompleteSwapOutFade(System.Action onComplete)
    {
        if (_hero.CurrentState != HeroState.SwapOut) return;

        SwitchHeroRenderer(false);
        onComplete?.Invoke();
    }
    
    /// <summary>
    /// outHero 를 교대타입에 맞게 분기처리
    /// </summary>
    public void BeginSwapOutProcess(SwapType swapType)
    {
        CurrentSwapType = swapType;

        switch (swapType)
        {
            case SwapType.Normal:
                BeginNormalSwapOut();
                break;
            case SwapType.QuickAssist:
                BeginQuickAssistSwapOut();
                break;
            case SwapType.Parry:
                BeginParrySwapOut();
                break;
            case SwapType.Chain:
                BeginChainSwapOut();
                break;
        }
    }

    private void BeginNormalSwapOut()
    {
        // 순수 이동 상태였다면 공격 중이 아니므로 즉시 퇴장 (일반적인 퇴장)
        if (_hero.CurrentState == HeroState.Walk || _hero.CurrentState == HeroState.Run ||
            _hero.CurrentState == HeroState.Sprint || _hero.CurrentState == HeroState.Dodge ||
            _hero.CurrentState == HeroState.Idle || _hero.CurrentState == HeroState.ConvertCombat)
        {
            _hero.ChangeState(HeroState.SwapOut);
        }
        else
        {
            // 잔상 시퀀스 진행시, OffFieldActive 플래그 켜주고 넘어감
            _hero.ActionRegistry.SetOffFieldActionActive(true);

            //_hero.ActionRegistry.SetIsInvincible(true);
            // 여기서 무적 플래그 관리하면 애니메이션 이벤트에서
            // 플래그 오염가능성 생김
            // -> TakeDamage에서 IsOffFieldActionActive 켜져있을때 데미지 차단하는 방식으로 변경
        }
    }

    /// <summary>
    /// 공격 중에는 잔상으로 마무리하고, 넉백 및 비공격 상태에서는 즉시 퇴장함.
    /// </summary>
    private void BeginQuickAssistSwapOut()
    {
        switch (_hero.CurrentState)
        {
            case HeroState.Attack:
            case HeroState.ChargeAttack:
            case HeroState.Skill:
            case HeroState.Ult:
            case HeroState.DodgeCounter:
            case HeroState.ParryCounter:
            case HeroState.QuickAssist:
            case HeroState.ChainAttack:
                // 현재 타격 인덱스와 이동을 유지하고 종료 이벤트에서 퇴장함.
                _hero.ActionRegistry.SetOffFieldActionActive(true);
                return;

            default:
                // 넉백을 포함한 비공격 상태는 즉시 퇴장함.
                _hero.ActionRegistry.SetOffFieldActionActive(false);
                _hero.ChangeState(HeroState.InActive);
                return;
        }
    }

    /// <summary>
    /// 패링 교대 시 이전 Hero의 상태에 따라 즉시 퇴장하거나 잔여 동작을 유지함.
    /// </summary>
    private void BeginParrySwapOut()
    {
        if (_hero.CurrentState == HeroState.ParryIn)
        {
            // 정리 클립의 OnParryEnd를 생략하므로 패링 정보를 직접 정리함.
            _hero.ActionRegistry.SetIsParryCounterResereved(false);
            _hero.ActionRegistry.SetIsParryCounterWindowOpen(false);
            _hero.ActionRegistry.SetOffFieldActionActive(false);
            _hero.ClearCounterTarget();

            // 기존 카메라를 유지하고 뒤이어 등장하는 Hero에게 인계함.
            IsParryCameraHandoffActive = true;
            try
            {
                _hero.ChangeState(HeroState.InActive);
            }
            finally
            {
                IsParryCameraHandoffActive = false;
            }

            return;
        }

        // 비공격 상태에서는 바로 InActive
        if (_hero.CurrentState == HeroState.Walk || _hero.CurrentState == HeroState.Run ||
            _hero.CurrentState == HeroState.Sprint || _hero.CurrentState == HeroState.Dodge ||
            _hero.CurrentState == HeroState.Idle || _hero.CurrentState == HeroState.ConvertCombat)
        {
            _hero.ActionRegistry.SetOffFieldActionActive(false);
            SetInActiveEntrance();
            _hero.ChangeState(HeroState.InActive);
        }
        // 공격 상태에서는 잔상 시퀀스 진행
        else
        {
            // 잔상 시퀀스 진행시, OffFieldActive 플래그 켜주고 넘어감
            _hero.ActionRegistry.SetOffFieldActionActive(true);
        }


    }

    private void BeginChainSwapOut()
    {
        // 순수 이동 상태였다면 공격 중이 아니므로 즉시 InActive
        if (_hero.CurrentState == HeroState.Walk || _hero.CurrentState == HeroState.Run ||
            _hero.CurrentState == HeroState.Sprint || _hero.CurrentState == HeroState.Dodge ||
            _hero.CurrentState == HeroState.Idle || _hero.CurrentState == HeroState.ConvertCombat)
        {
            _hero.ActionRegistry.SetOffFieldActionActive(false);
            SetInActiveEntrance();
            _hero.ChangeState(HeroState.InActive);
        }
        else
        {
            // 잔상 시퀀스 진행시, OffFieldActive 플래그 켜주고 넘어감
            _hero.ActionRegistry.SetOffFieldActionActive(true);
        }
    }

    #endregion

}
