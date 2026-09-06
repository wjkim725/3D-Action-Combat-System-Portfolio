using UnityEngine;

// 데미지를 입을수 있으면 상속되는 인터페이스
public interface IDamageable
{
    IStatus Status { get; }

    /// <summary>
    /// return
    /// true - 실제 피해 적용 성공
    /// false - 사망-무적 등으로 피해 미적용
    /// </summary>
    bool TakeDamage(HitInfo hitInfo);
}
// 데미지를 줄수 있으면 상속되는 인터페이스
public interface IAttacker
{   // 공격자라면 자신의 스탯정보를 제공해야 한다는 규칙 선언
    IStatus Status { get; }
    // 이펙트 생성 위치, 피격 방향 연산 등을 위해 Transform 제공
    Transform AttackerTransform { get; }
    // 타격 성공시 규칙
    void OnHitSuccess(float payBackEnegy, float payBackDecibel);
}
// Status 역할을 하는 컴포넌트에 상속될 인터페이스
public interface IStatus
{   // 다양한 Status 인터페이스들의 조상이 될 기초 인터페이스
    public float TotalAttack { get; }
    public float TotalMaxHP { get; }
    public float CurrentHP { get; }
    public bool IsDead => false;
}
public interface IHeroStatus : IStatus
{
    public float TotalImpact => 100f;
    public float TotalCritical => 10f;
    public float TotalCriticalHitDamage => 100f;
}
public interface IEnemyStatus : IStatus
{
    public float MaxGroggyGauge => 100f;
    public float CurrentGroggyGauge => 0f;
    public bool IsGroggy => false;
    public float GroggyDamageMultiplier => 120f;
    public float BaseGroggyDuration => 5f;
}
