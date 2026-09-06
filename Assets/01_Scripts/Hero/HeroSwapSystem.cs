using UnityEngine;

public class HeroSwapSystem : MonoBehaviour
{
    public enum SwapType { Normal, QuickAssist, Parry, Chain }
    
    private Hero _hero;
    private HeroDitherController _ditherController;

    #region [프로퍼티 영역]

    public SwapType CurrentSwapType { get; set; } = SwapType.Normal;

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
            if (_hero.Status.IsDead) return false;
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
            if (_hero.Status.IsDead) return false;

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
                    if (_hero.IsCombat)
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
    public void SwitchHeroCollider(bool isPresent)
    {
        // Collider On/Off
        if (TryGetComponent<Collider>(out var col)) col.enabled = isPresent;
    }
    public void SwitchHeroCharacterController(bool isPresent)
    {
        // CharacterController On/Off
        if (_hero.Movement.CharacterController != null)
            _hero.Movement.CharacterController.enabled = isPresent;
    }
    public void PrepareSwapInEntrance()
    {
        _ditherController?.SetFadeImmediate(0f);

        SwitchHeroRenderer(true);
        SwitchHeroCharacterController(true);
        SwitchHeroCollider(true);

        _ditherController?.FadeIn();
    }
    public void SetInActiveEntrance()
    {
        SwitchHeroCharacterController(false);
        SwitchHeroCollider(false);

        if (_ditherController != null)
        {
            _ditherController.FadeOut(() => SwitchHeroRenderer(false));
            return;
        }

        SwitchHeroRenderer(false);
        //_hero.VFX.StopSwordTrail();
    }


    #region [퇴장 시퀀스 영역]
    
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
                BeginQuickSwapOut();
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

    private void BeginQuickSwapOut()
    {
        _hero.ActionRegistry.SetOffFieldActionActive(false);
        SetInActiveEntrance();
        _hero.ChangeState(HeroState.InActive);
    }

    private void BeginParrySwapOut()
    {
        _hero.ActionRegistry.SetOffFieldActionActive(false);
        SetInActiveEntrance();
        _hero.ChangeState(HeroState.InActive);
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


        // 진행 중인 일반 전투 장부 정리
        //_hero.ActionRegistry.SetOffFieldActionActive(false);
        //_hero.ActionRegistry.ResetActionRegistry();

        // Chain 교대는 잔상 없이 즉시 퇴장
        // TODO : OffFieldActionActive 켜주고 넘어가는 방식 채용?
        //_hero.ChangeState(HeroState.InActive);
        //_hero.ActionRegistry.SetOffFieldActionActive(true);
    }

    #endregion

}
