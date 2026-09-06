using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Hero의 Status / Buff 및 Status 관리 모듈 - Buff는 분리해야될지도
/// </summary>
public class HeroStatus : MonoBehaviour, IHeroStatus
{
    public Hero _hero { get; private set; }

    // 기본 스탯 (마을스탯)
    private float _baseAttack = 100.0f;         // 공격력
    private float _baseImpact = 100.0f;         // 충격력
    private float _baseCritical = 10.0f;            // 치명타 확률
    private float _baseCriticalHitDamage = 200f;    // 치명타 피해량
    private float _baseMaxHP = 10000f;              // 최대 체력도 버프에 영향

    // [실시간 변동형 전투 자원]
    [SerializeField]
    private float _currentHP;
    [SerializeField]
    private float _currentEnergy = 40f;
    [SerializeField]
    private float _currentDecibel = 0f;

    // [최종 스탯 능력치 프로퍼티]
    public float TotalAttack { get; private set; }
    public float TotalImpact { get; private set; }
    public float TotalCritical { get; private set; }
    public float TotalCriticalHitDamage { get; private set; }
    public float TotalMaxHP { get; private set; } 

    // [UI 및 외부 연동용 전투 자원 프로퍼티]
    public float CurrentHP => _currentHP;
    public float MaxHP => TotalMaxHP; // UI 슬라이더는 최종 최대 체력을 기준으로 비율을 계산

    public float CurrentEnergy => _currentEnergy;
    private float _maxEnergy = 120f; 
    public float MaxEnergy => _maxEnergy;

    public float CurrentDecibel => _currentDecibel;
    private float _maxDecibel = 3000f; // 최대 데시벨 게이지 고정
    public float MaxDecibel => _maxDecibel;

    // 사망 처리 프로퍼티
    public bool IsDead => (_currentHP <= 0f);

    // 버프들을 담아둘 리스트 (예: 공격력 증가, 치명타 증가 등)
    private List<Buff> _activeBuffs = new List<Buff>();

    private void Awake()
    {
        _hero = GetComponent<Hero>();
        RecalculateStats();

        _currentHP = MaxHP;
        _currentDecibel = 1000f;
    }

    private void Update()
    {
        // 매 프레임 버프 업타임 관리 및 스탯 역산
        //UpdateBuffTimers();
        RecalculateStats();
    }

    public void RecalculateStats()
    {
        float attackSum = _baseAttack;
        float dazeSum = _baseImpact;
        float criticalSum = _baseCritical;
        float criticalHitDamageSum = _baseCriticalHitDamage;
        float maxHPSum = _baseMaxHP;

        // 활성화된 버프들을 돌면서 최종 수치 합산 (버프 꼬임 방지!)
        //foreach (var buff in _activeBuffs)
        //{
        //    if (buff.Type == BuffType.AttackBoost) attackSum += buff.Value;
        //    if (buff.Type == BuffType.ImpactBoost) dazeSum += buff.Value;
        //}

        TotalAttack = attackSum;
        TotalImpact = dazeSum;
        TotalCritical = criticalSum;
        TotalCriticalHitDamage = criticalHitDamageSum;
        TotalMaxHP = maxHPSum;

        // 안전장치: 아이템 장착 해제 등으로 최대 체력(TotalMaxHP)이 줄어들었을 때, 
        // 현재 체력이 최대 체력을 초과하고 있다면 최대 체력에 맞춰 조정
        if (_currentHP > TotalMaxHP)
        {
            _currentHP = TotalMaxHP;
        }
    }

    public void AddBuff(Buff newBuff) { _activeBuffs.Add(newBuff); RecalculateStats(); }

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

    /// <summary>
    /// 에너지 가감 메서드 (타격 시 충전, 강화스킬 사용 시 소모)
    /// </summary>
    public void ModifyEnergy(float amount)
    {
        _currentEnergy += amount;
        _currentEnergy = Mathf.Clamp(_currentEnergy, 0f, _maxEnergy);
    }

    /// <summary>
    /// 데시벨(궁극기) 게이지 가감 메서드
    /// </summary>
    public void ModifyDecibel(float amount)
    {
        _currentDecibel += amount;
        _currentDecibel = Mathf.Clamp(_currentDecibel, 0f, _maxDecibel);
    }
}
