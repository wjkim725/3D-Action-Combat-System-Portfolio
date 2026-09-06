using UnityEngine;

public class EnemyStatus : MonoBehaviour, IEnemyStatus
{
    [Header("Health Status")]
    [SerializeField] private float _maxHP = 10000;
    [SerializeField] private float _currentHP;

    [Header("Groggy Status")]
    [SerializeField] private float _maxGroggyGauge = 50f;
    [SerializeField] private float _currentGroggyGauge = 0f;
    private bool _isGroggy = false;
    // 그로기 약체화 배율 -> CombatManager에서 GroggyDamageMultiplier/100 해서 최종 데미지 처리
    [SerializeField] private float _groggyDamageMultiplier = 130f;
    // 몬스터 기본 그로기 지속 시간
    [SerializeField] private float _baseGroggyDuration = 12f;

    public float TotalAttack { get; private set; } = 100f;      // 추후 몬스터 데미지 계산 방식 추가
    public float TotalMaxHP => _maxHP;
    public float CurrentHP => _currentHP;
    public bool IsDead => (_currentHP <= 0f);    
    public float MaxGroggyGauge => _maxGroggyGauge;
    public float CurrentGroggyGauge => _currentGroggyGauge;
    public bool IsGroggy => _isGroggy;
    public float GroggyDamageMultiplier => _groggyDamageMultiplier;
    public float BaseGroggyDuration => _baseGroggyDuration;


    private void Awake()
    {
        _currentHP = _maxHP;
        _currentGroggyGauge = 0f;
    }

    public void ModifyHP(float amount)
    {
        _currentHP += amount;
        _currentHP = Mathf.Clamp(_currentHP, 0f, _maxHP);
    }

    public void SetGroggyState(bool isGroggy)
    {
        _isGroggy = isGroggy;
    }

    public void ModifyGroggyGauge(float amount)
    {
        _currentGroggyGauge += amount;
        _currentGroggyGauge = Mathf.Clamp(_currentGroggyGauge, 0f, _maxGroggyGauge);
    }

    public void ResetGroggyGauge()
    {
        _currentGroggyGauge = 0f;
    }
}
