using System;
using UnityEngine;

/// <summary>
/// Maria의 강특 에너지 소비를 전용 게이지로 전환하고 Blazing 강화를 유지함.
/// </summary>
[DisallowMultipleComponent]
public class MariaMechanic : MonoBehaviour, IHeroMechanic
{
    [Header("Mechanic Resource")]
    [SerializeField, Min(0.01f)] private float _maxResource = 100f;
    [SerializeField, Min(0f)] private float _resourcePerEnergy = 1f;
    [SerializeField, Min(0f)] private float _resourceDrainPerSecond = 5f;

    [Header("Blazing Buff")]
    [SerializeField, Min(0f)] private float _attackBoostPercent = 30f;
    // 치확과 치피는 퍼센트 포인트 단위로 더함.
    [SerializeField, Min(0f)] private float _criticalBoostFlat = 15f;
    [SerializeField, Min(0f)] private float _criticalHitDamageBoostFlat = 50f;

    [Header("Blazing VFX")]
    [SerializeField] private HeroWeapon _weapon;
    [SerializeField] private ParticleSystem _idleFlame;
    [SerializeField] private ParticleSystem _combatFlame;

    private HeroStatus _status;
    private bool _lastNotifiedBlazingActive;
    private Buff _blazingBuff;
    private float _currentResource;

    #region [공통 계약 영역]

    public Hero Hero { get; private set; }
    public HeroState CurrentState => Hero.CurrentState;
    public Animator Animator { get; private set; }
    public HeroMechanicResourceType ResourceType => HeroMechanicResourceType.Gauge;
    public float CurrentResource => _currentResource;
    public float MaxResource => _maxResource;
    public bool IsBlazingActive => _status != null && _status.HasBuff(_blazingBuff);
    public bool IsCombatRotationLocked => false;
    public bool IsChargeAttackFinisherActive => false;

    #endregion

    #region [이벤트 영역]

    public event Action<bool> OnBlazingActiveChanged;

    #endregion

    #region [초기화 영역]

    private void Awake()
    {
        Hero = GetComponent<Hero>();
        _status = GetComponent<HeroStatus>();
        Animator = GetComponent<Animator>();
        // 버프 조립
        _blazingBuff = new Buff(new[]
        {
            new BuffEffect(BuffType.AttackBoost, _attackBoostPercent, BuffValueMode.Percent),
            new BuffEffect(BuffType.CriticalBoost, _criticalBoostFlat),
            new BuffEffect(BuffType.CirticalHitDamageBoost, _criticalHitDamageBoostFlat)
        }, 0f, BuffDurationMode.External);
    }

    private void OnEnable()
    {
        _status.OnEnergyConsumed += HandleEnergyConsumed;
        if (_weapon != null) _weapon.OnWeaponDisplayStateChanged += HandleWeaponDisplayStateChanged;
        SynchronizeBlazingBuff();
        SynchronizeBlazingVFX();
    }

    private void OnDisable()
    {
        _status.OnEnergyConsumed -= HandleEnergyConsumed;
        if (_weapon != null) _weapon.OnWeaponDisplayStateChanged -= HandleWeaponDisplayStateChanged;
        SetFlameActive(_idleFlame, false);
        SetFlameActive(_combatFlame, false);
    }

    private void OnDestroy()
    {
        if (_status != null) _status.RemoveBuff(_blazingBuff);
    }

    #endregion

    #region [FSM 제어 영역]

    /// <summary>
    /// 사망 진입 시 게이지를 초기화하며 일반 교대에서는 유지함.
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
                break;
            case HeroState.Skill:
                break;
            case HeroState.Ult:
                FillResource();
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
                break;
            case HeroState.Dead:
                ResetResource();
                break;
        }
    }

    /// <summary>
    /// 생존한 온필드 Maria의 게이지만 게임 시간에 따라 소모하고 강화 상태를 동기화함.
    /// </summary>
    public void UpdateState()
    {
        if (!isActiveAndEnabled) return;
        if (_status.IsDead)
        {
            ResetResource();
            return;
        }

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
                break;
            case HeroState.AfterShock:
                break;
            case HeroState.Dead:
                ResetResource();
                return;
        }

        if (PartyStateManager.Instance != null && PartyStateManager.Instance.CurrentHero == Hero)
        {
            _currentResource = Mathf.Max(0f, _currentResource - _resourceDrainPerSecond * Time.deltaTime);
        }
        SynchronizeBlazingBuff();
    }

    /// <summary>
    /// 상태 종료 시 게이지를 보존함.
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
                break;
            case HeroState.Dead:
                break;
        }
    }

    #endregion

    #region [공통 판정 연결 영역]

    /// <summary>
    /// Maria는 전용 회피 판정을 사용하지 않으므로 공통 피격 처리를 유지함.
    /// </summary>
    public bool TryEvadeHit(CombatEventContext context)
    {
        return false;
    }

    /// <summary>
    /// 직접 호출 또는 현재 기본 레이어의 재생 대상으로 유지되는 이벤트만 허용함.
    /// </summary>
    public bool CanProcessAnimationEvent(AnimationEvent animationEvent)
    {
        if (animationEvent == null || !animationEvent.isFiredByAnimator) return true;
        int sourceHash = animationEvent.animatorStateInfo.fullPathHash;
        if (Animator.IsInTransition(0))
        {
            return sourceHash == Animator.GetNextAnimatorStateInfo(0).fullPathHash;
        }
        return sourceHash == Animator.GetCurrentAnimatorStateInfo(0).fullPathHash;
    }

    #endregion

    #region [게이지 및 강화 관리]

    /// <summary>
    /// 생존한 Maria의 Skill 상태에서 실제 소비한 에너지에 비례해 게이지를 충전함.
    /// </summary>
    private void HandleEnergyConsumed(float consumedEnergy)
    {
        if (_status.IsDead || CurrentState != HeroState.Skill) return;
        _currentResource = Mathf.Clamp(_currentResource + consumedEnergy * _resourcePerEnergy, 0f, _maxResource);
        SynchronizeBlazingBuff();
    }

    /// <summary>
    /// 게이지와 생존 상태에 맞춰 자신이 소유한 Blazing 버프만 등록하거나 해제함.
    /// </summary>
    private void SynchronizeBlazingBuff()
    {
        bool shouldApply = _currentResource > 0f && !_status.IsDead && CurrentState != HeroState.Dead;
        if (shouldApply)
        {
            if (!IsBlazingActive)
            {
                _status.AddBuff(_blazingBuff);
                SynchronizeBlazingVFX();
            }
        }
        else if (IsBlazingActive)
        {
            _status.RemoveBuff(_blazingBuff);
            SynchronizeBlazingVFX();
        }

        NotifyBlazingActiveChanged();
    }

    /// <summary>
    /// 실제 Blazing 상태가 마지막 알림과 달라졌을 때 변경을 알림.
    /// </summary>
    private void NotifyBlazingActiveChanged()
    {
        bool isActive = IsBlazingActive;
        if (_lastNotifiedBlazingActive == isActive)
            return;

        _lastNotifiedBlazingActive = isActive;
        OnBlazingActiveChanged?.Invoke(isActive);
    }

    /// <summary>
    /// 전투 초기화 또는 사망 시 게이지와 자신이 등록한 버프를 함께 제거함.
    /// </summary>
    public void ResetResource()
    {
        _currentResource = 0f;
        _status.RemoveBuff(_blazingBuff);
        SynchronizeBlazingVFX();
        NotifyBlazingActiveChanged();
    }

    /// <summary>
    /// 전용 게이지를 최대치로 충전하고 Blazing 상태를 동기화함.
    /// </summary>
    private void FillResource()
    {
        _currentResource = _maxResource;
        SynchronizeBlazingBuff();
    }

    #endregion

    #region [불꽃 표시]

    /// <summary>
    /// 무기 표시 변경 후 Blazing 불꽃을 동기화함.
    /// </summary>
    private void HandleWeaponDisplayStateChanged(HeroWeapon.WeaponDisplayState state)
    {
        SynchronizeBlazingVFX();
    }

    /// <summary>
    /// Blazing이 활성화된 동안 현재 표시 중인 검의 불꽃만 재생함.
    /// </summary>
    private void SynchronizeBlazingVFX()
    {
        bool canShow = isActiveAndEnabled && IsBlazingActive && _weapon != null;
        SetFlameActive(_idleFlame, canShow && _weapon.CurrentState == HeroWeapon.WeaponDisplayState.OutCombat);
        SetFlameActive(_combatFlame, canShow && _weapon.CurrentState == HeroWeapon.WeaponDisplayState.InCombat);
    }

    /// <summary>
    /// 불꽃을 재생하거나 자식 입자까지 제거하고 비활성화함.
    /// </summary>
    private void SetFlameActive(ParticleSystem flame, bool active)
    {
        if (flame == null) return;
        if (active)
        {
            if (!flame.gameObject.activeSelf) flame.gameObject.SetActive(true);
            if (!flame.isPlaying) flame.Play(true);
        }
        else
        {
            flame.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            if (flame.gameObject.activeSelf) flame.gameObject.SetActive(false);
        }
    }

    #endregion

}
