using UnityEngine;

// 데미지를 입을수 있으면 상속되는 인터페이스
public interface IDamageable
{
    IStatus Status { get; }
    Transform DamageTextAnchor { get; }

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

/// <summary>
/// Hero 본체가 호출하는 캐릭터 전용 메카닉 계약.
/// 상태 저장과 ChangeState는 Hero 본체가 담당한다.
///
/// 상태 전환 순서:
/// 1. Mechanic.ExitState(기존 상태)
/// 2. Hero.ExitState(기존 상태)
/// 3. Hero.CurrentState를 새 상태로 변경
/// 4. Hero.EnterState(새 상태)
/// 5. Mechanic.EnterState(새 상태)
///
/// 프레임 갱신 순서:
/// Hero.Update에서 Hero.UpdateState 실행 후 Mechanic.UpdateState 호출.
/// 전용 메카닉은 별도 Update에서 UpdateState를 중복 호출하지 않는다.
///
/// 메카닉에서 상태 전환이 필요하면 Hero.ChangeState로 요청한다.
/// 메카닉 갱신 중 전환하면 Enter/Exit는 즉시 실행되며,
/// 새 상태의 Hero.UpdateState는 다음 프레임에 실행된다.
public interface IHeroMechanic
{
    public Hero Hero { get; }
    public HeroState CurrentState { get; }
    public Animator Animator { get; }

    public HeroMechanicResourceType ResourceType { get; }
    bool IsCombatRotationLocked { get; }
    bool IsChargeAttackFinisherActive { get; }

    /// <summary>
    /// 전용 회피로 피격을 처리했으면 true를 반환함.
    /// </summary>
    bool TryEvadeHit(CombatEventContext context);

    /// <summary>
    /// 현재 전용 동작에서 해당 애니메이션 이벤트를 처리할 수 있는지 반환함.
    /// </summary>
    bool CanProcessAnimationEvent(AnimationEvent animationEvent);
    float CurrentResource { get; }
    float MaxResource { get; }

    #region [FSM 제어 영역]
    void EnterState(HeroState newState, bool isComboConnection);
    void UpdateState();
    void ExitState(HeroState currentState, bool preserveAnimMove = false);
    // ChageState는 Hero 본체의 메서드 사용
    #endregion
}

/// <summary>
/// 여진 공격을 지원하는 메카닉이 고정 공격 대상을 제공하는 규약.
/// 대상은 여진 시작 시 확보하고 종료 시 해제한다.
/// 발사기는 구체적인 캐릭터 클래스 대신 이 인터페이스로 대상을 조회한다.
/// 대상이 없거나 무효하면 공격자 전방으로 발사한다.
/// </summary>
public interface IAfterShockTargetProvider
{
    Transform AfterShockTarget { get; }
}
