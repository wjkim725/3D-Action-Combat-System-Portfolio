using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Hero의 Status / Buff 및 Status 관리 모듈 - Buff는 분리해야될지도
/// </summary>
public class HeroStatus : MonoBehaviour, IHeroStatus
{
    public Hero _hero { get; private set; }

    [Header("Base Stats")]
    [SerializeField, Min(0f)]
    private float _baseAttack = 100.0f;         // 공격력
    [SerializeField, Min(0f)]
    private float _baseImpact = 100.0f;         // 충격력
    [SerializeField, Min(0f)]
    private float _baseCritical = 10.0f;            // 치명타 확률
    [SerializeField, Min(0f)]
    private float _baseCriticalHitDamage = 200f;    // 치명타 피해량
    [SerializeField, Min(0f)]
    private float _baseMaxHP = 10000f;              // 최대 체력도 버프에 영향

    [Header("Combat Resources")]
    [SerializeField, Min(0f)]
    private float _currentHP;
    [SerializeField, Min(0f)]
    private float _currentEnergy = 40f;
    private float _maxEnergy = 120f; 
    private bool _isEnergyLockedAtZero; // 튜토리얼 등의 일반스킬 연습 중 에너지를 0으로 유지함.
    private bool _isSandboxInfiniteEnergy; // CombatSandBox 기능: 에너지 무한 유지 플래그.
    private bool _isSandboxInfiniteDecibel; // CombatSandBox 기능: 데시벨 무한 유지 플래그.

    public float CurrentHP => _currentHP;
    public float MaxHP => TotalMaxHP; // UI 슬라이더는 최종 최대 체력을 기준으로 비율을 계산
    public event Action<float> OnEnergyConsumed;

    public float CurrentEnergy => _currentEnergy;
    public float MaxEnergy => _maxEnergy;
    public bool IsDead => (_currentHP <= 0f);   // 사망 처리 프로퍼티

    [Header("Energy Recovery")]
    [SerializeField, Min(0f)]
    private float _energyRecoveryPerSecond = 1f; // 게임 시간 1초당 자동회복량.

    [Header("Decibel")]
    [SerializeField, Min(0f)]
    private float _maxDecibel = 3000f;
    [SerializeField, Min(0f)]
    private float _requiredDecibel = 3000f;
    [SerializeField, Min(0f)]
    private float _currentDecibel = 1000f;

    public float MaxDecibel => _maxDecibel;
    public float RequiredDecibel => _requiredDecibel;
    public float CurrentDecibel => _currentDecibel;
    public bool HasEnoughDecibelForUlt =>
        _currentDecibel >= _requiredDecibel;


    // [최종 스탯 능력치 프로퍼티]
    public float TotalAttack { get; private set; }
    public float TotalImpact { get; private set; }
    public float TotalCritical { get; private set; }
    public float TotalCriticalHitDamage { get; private set; }
    public float TotalMaxHP { get; private set; } 

    // 버프들을 담아둘 리스트 (예: 공격력 증가, 치명타 증가 등)
    private readonly List<Buff> _activeBuffs = new List<Buff>();
    private readonly List<Buff> _expiredBuffs = new List<Buff>();
    private bool _isClearingBuffs;

    private void Awake()
    {
        _hero = GetComponent<Hero>();
        RecalculateStats();

        _currentHP = MaxHP;
        _currentDecibel = Mathf.Clamp(_currentDecibel, 0f, _maxDecibel);
    }

    private void Update()
    {
        UpdateBuffTimers();
        UpdateEnergyRecovery();
    }

    #region [버프 및 능력치 관리]

    /// <summary>
    /// 기본 능력치와 활성 버프로 최종 수치를 다시 계산함.
    /// </summary>
    public void RecalculateStats()
    {
        TotalAttack = CalculateBuffedStat(_baseAttack, BuffType.AttackBoost);
        TotalImpact = CalculateBuffedStat(_baseImpact, BuffType.ImpactBoost);
        TotalCritical = Mathf.Clamp(CalculateBuffedStat(_baseCritical, BuffType.CriticalBoost), 0f, 100f);
        TotalCriticalHitDamage = CalculateBuffedStat(_baseCriticalHitDamage, BuffType.CirticalHitDamageBoost);
        TotalMaxHP = _baseMaxHP;
        if (_currentHP > TotalMaxHP) _currentHP = TotalMaxHP;
    }

    /// <summary>
    /// 같은 종류의 비율 증가를 합산한 뒤 고정 증가를 더함.
    /// </summary>
    private float CalculateBuffedStat(float baseValue, BuffType type)
    {
        float flat = 0f;
        float percent = 0f;
        foreach (Buff buff in _activeBuffs)
        {
            foreach (BuffEffect effect in buff.Effects)
            {
                if (effect.Type != type) continue;
                if (effect.ValueMode == BuffValueMode.Percent) percent += effect.Value;
                else flat += effect.Value;
            }
        }
        return Mathf.Max(0f, baseValue * (1f + percent / 100f) + flat);
    }

    /// <summary>
    /// 생존한 Hero에 미등록 버프를 한 번 적용하고 능력치를 즉시 갱신함.
    /// </summary>
    public bool AddBuff(Buff buff)
    {
        if (buff == null || IsDead || _isClearingBuffs || buff.Owner != null || HasBuff(buff)) return false;
        buff.Attach(this);
        _activeBuffs.Add(buff);
        RecalculateStats();
        try { buff.OnApplyBuff(this); }
        catch (Exception exception) { Debug.LogException(exception, this); }
        return true;
    }

    /// <summary>
    /// 지정한 버프 인스턴스가 등록되어 있는지 반환함.
    /// </summary>
    public bool HasBuff(Buff buff)
    {
        return buff != null && _activeBuffs.Contains(buff);
    }

    /// <summary>
    /// 지정한 버프만 제거하고 능력치 복원 후 해제 콜백을 실행함.
    /// </summary>
    public bool RemoveBuff(Buff buff)
    {
        if (buff == null || !_activeBuffs.Remove(buff)) return false;
        buff.Detach();
        RecalculateStats();
        try { buff.OnRemoveBuff(this); }
        catch (Exception exception) { Debug.LogException(exception, this); }
        return true;
    }

    /// <summary>
    /// 전체 해제 중 재등록을 차단하고 모든 버프를 정리함.
    /// </summary>
    public void ClearBuffs()
    {
        if (_isClearingBuffs) return;
        _isClearingBuffs = true;
        try
        {
            // 콜백에서 다른 버프를 해제하더라도 각 해제는 한 번만 실행함.
            Buff[] buffs = _activeBuffs.ToArray();
            foreach (Buff buff in buffs) RemoveBuff(buff);
        }
        finally
        {
            _isClearingBuffs = false;
        }
    }

    /// <summary>
    /// 온필드 여부와 무관하게 시간제 버프를 갱신하고 만료 항목을 해제함.
    /// </summary>
    private void UpdateBuffTimers()
    {
        _expiredBuffs.Clear();
        foreach (Buff buff in _activeBuffs)
        {
            buff.Tick(Time.deltaTime);
            if (buff.IsFinished) _expiredBuffs.Add(buff);
        }
        foreach (Buff buff in _expiredBuffs)
        {
            // 앞선 해제 콜백이 재등록하여 시간을 초기화한 버프는 유지함.
            if (buff.IsFinished) RemoveBuff(buff);
        }
    }

    #endregion

    #region [HP 관리 영역]

    /// <summary>
    /// 부활같은 특수한 상황에서 사용될 CurrentHP Setter 메서드
    /// 일반적인 CurrentHP 값 변경에는 ModifyHp 메서드 사용
    /// </summary>
    public void SetCurrentHP(float amount)
    {
        _currentHP = amount;
        _currentHP = Mathf.Clamp(_currentHP, 0f, TotalMaxHP);
    }

    /// <summary>
    /// 체력 가감 메서드 (피격 시 damage는 양수, 힐 보급 시 damage는 음수 전달)
    /// </summary>
    public void ModifyHP(float amount)
    {
        _currentHP += amount;
        // 0 ~ 최종 최대 체력 사이로 값 제한 처리
        _currentHP = Mathf.Clamp(_currentHP, 0f, TotalMaxHP);
    }

    #endregion

    #region [Energy 관리 영역]

    /// <summary>
    /// 적 감지 기반의 회복 허용 상태에서 생존한 비조작 캐릭터의 에너지를 자동회복함.
    /// </summary>
    private void UpdateEnergyRecovery()
    {
        if (_isEnergyLockedAtZero) return;
        if (_hero.IsInputEnabled || IsDead) return;
        if (_currentEnergy >= _maxEnergy || _energyRecoveryPerSecond <= 0f) return;

        CombatDetector combatDetector = CombatDetector.Instance;
        if (combatDetector == null || !combatDetector.CanRecoverEnergy) return;

        ModifyEnergy(_energyRecoveryPerSecond * Time.deltaTime);
    }

    /// <summary>
    /// 에너지를 증감하고 최대 에너지 범위로 제한함.
    /// 0 유지 설정이 활성화된 동안에는 샌드박스 무한 에너지보다 우선하여 변경을 차단함.
    /// </summary>
    public void ModifyEnergy(float amount)
    {
        if (_isEnergyLockedAtZero)
        {
            _currentEnergy = 0f;
            return;
        }

        float previousEnergy = _currentEnergy;
        _currentEnergy = Mathf.Clamp(_currentEnergy + amount, 0f, _maxEnergy);
        float consumedEnergy = previousEnergy - _currentEnergy;
        // CombatSandBox 기능: 소비량 이벤트는 보존하고 실제 보유량을 즉시 복원함.
        if (_isSandboxInfiniteEnergy) _currentEnergy = _maxEnergy;
        if (consumedEnergy > 0f) OnEnergyConsumed?.Invoke(consumedEnergy);
    }

    /// <summary>
    /// 에너지 0 유지 여부를 설정하며 활성화 즉시 보유 에너지를 비움.
    /// 강제 초기화는 스킬 소비가 아니므로 소비 이벤트를 발생시키지 않음.
    /// 해제 시 이전 에너지를 복원하지 않음.
    /// </summary>
    public void SetEnergyLockedAtZero(bool isLocked)
    {
        _isEnergyLockedAtZero = isLocked;
        if (isLocked) _currentEnergy = 0f;
    }

    #endregion

    #region [Decibel 관리 영역]

    /// <summary>
    /// 데시벨(궁극기) 게이지 가감 메서드
    /// </summary>
    public void ModifyDecibel(float amount)
    {
        _currentDecibel += amount;
        _currentDecibel = Mathf.Clamp(_currentDecibel, 0f, _maxDecibel);
        if (_isSandboxInfiniteDecibel) _currentDecibel = _maxDecibel;
    }

    /// <summary>
    /// CombatSandBox 기능의 에너지 무한 여부를 설정하고 활성화 시 최대 충전함.
    /// </summary>
    public void SetSandboxInfiniteEnergy(bool isEnabled)
    {
        _isSandboxInfiniteEnergy = isEnabled;
        if (isEnabled) ModifyEnergy(_maxEnergy - _currentEnergy);
    }

    /// <summary>
    /// CombatSandBox 기능의 데시벨 무한 여부를 설정하고 활성화 시 최대 충전함.
    /// </summary>
    public void SetSandboxInfiniteDecibel(bool isEnabled)
    {
        _isSandboxInfiniteDecibel = isEnabled;
        if (isEnabled) ModifyDecibel(_maxDecibel - _currentDecibel);
    }

    /// <summary>
    /// Hero - HandleUltStartedInput 호출
    /// Ult 발동 가능 여부 체크 및
    /// 발동 시 데시벨 소모 후 true 반환
    /// </summary>
    public bool TryConsumeUltDecibel()
    {
        if (!HasEnoughDecibelForUlt)
            return false;

        ModifyDecibel(-_requiredDecibel);
        return true;
    }

    #endregion
}
