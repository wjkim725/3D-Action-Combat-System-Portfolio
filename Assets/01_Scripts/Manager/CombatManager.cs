using UnityEngine;

public class CombatManager : MonoBehaviour
{
    public static CombatManager Instance { get; private set; }

    // 현재 플레이어를 공격 중이거나, 패링 타이밍을 제공하고 있는 적의 참조
    // 이녀석이 패링 전의 ParryTarget의 역할을 해주고
    // 이걸 참조해서 Hero-LastAttacker에 담아 사용
    public IAttacker CurrentDefenseWindowAttacker { get; private set; }   
    public bool IsParryAssistWindowOpen { get; private set; }
    public bool IsPerfectDodgeWindowOpen { get; private set; }

    // 극한 회피와 패링 지원이 한 모션에 여러번 재생되지 않기위한 플래그
    private bool _isPerfectDodgeConsumed = false;
    private bool _isParryAssistConsumed = false;

    [SerializeField]
    private float _heroHitStopDuration = 0.06f;     // Hero 타격 역경직 시간
    [SerializeField]
    private float _enemyHitStopDuration = 0.1f;     // Enemy 타격 역경직 시간

    private void Awake()
    {
        // 싱글톤 예외 처리 및 인스턴스 지정
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
            return;
        }
    }

    #region [전투 자원 계산 및 적용 영역]

    /// <summary>
    /// 실제 타격 로직
    /// </summary>
    /// <returns>실제 피해 적용 성공 여부</returns>
    public bool ProcessHit(CombatEventContext context)
    {
        // 극한회피 / 패링지원 체크
        if (TryResolveDefense(context))
        {
            return false;
        }

        // 공격자의 인스턴스에서 Status 가져옴
        IStatus attackerStatus = context.Attacker.Status;
        if (attackerStatus == null) return false;

        // 어떤 공격자든 공통인 기본 스탯 추출
        float attack = attackerStatus.TotalAttack;

        // 특정한 오브젝트들만 사용되는 스탯들은 기본값으로 초기화
        float impact = 0f;
        float critChance = 0f;
        float critDamage = 100f;

        // 패턴 매칭 (IHeroStatus) 인지 검사
        if (attackerStatus is IHeroStatus heroStatus)
        {   // 유형에 맞는 타격 데이터 초기화
            impact = heroStatus.TotalImpact;
            critChance = heroStatus.TotalCritical;
            critDamage = heroStatus.TotalCriticalHitDamage;
        }
         
        // 기본 데미지 공식 적용 (tuple 반환) - (finalDamage, isCrit)
        var damageResult = CalculateFinalDamage(attack, context.DamageMultiplier, critChance, critDamage);
        // 기본 무력화 공식 적용
        float finalDaze = CalculateFinalDaze(impact, context.DazeMultiplier);

        // 피격자의 인스턴스에서 Status가져옴 (일단 그로기 약체화 배율. 방어력도 추가된다면 여기서 처리될듯?)
        IStatus targetStatus = context.Target.Status;

        // 피격자가 몬스터 규칙(IEnemyStatus)을 따르는지 체크
        if (targetStatus is IEnemyStatus enemyStatus)
        {
            // 몬스터의 Groggy 컴포넌트를 확인해서 현재 실제 '그로기 상태'인지 검사
            // (context.target을 Enemy 클래스로 캐스팅하거나 IDamageable에 IsGroggy를 열어두어 체크)
            if (context.Target is Enemy groggyEnemy && groggyEnemy.Status.IsGroggy)
            {
                damageResult.finalDamage *= enemyStatus.GroggyDamageMultiplier / 100;
                Debug.Log($"[그로기 약체화 배율] {enemyStatus.GroggyDamageMultiplier}% 적용중");
            }
        }

        // 최종 HitInfo 패키지 조립
        HitInfo finalHitInfo = new HitInfo
        {
            damage = damageResult.finalDamage,
            isCrit = damageResult.isCrit,
            dazeAmount = finalDaze,
            hitType = context.HitType,
            attacker = context.Attacker,
            canExtendGroggy = context.CanExtendGroggy,
            groggyExtendTime = context.GroggyExtendTime,
            payBackEnergy = context.PayBackEnergy,
            payBackDecibel = context.PayBackDecibel
        };

        // 타격 대상이 Destroy 될수도 있으니, 그전에 데미지 텍스트 위치 캐싱
        Vector3 damageTextPosition = GetDamageTextSpawnPosition(context.Target);

        // 피격자에게 TakeDamage 호출하도록 명령
        bool wasDamageApplied = context.Target.TakeDamage(finalHitInfo);

        // -------------------- 타격 성공 여부 ---------------------------- 

        // 사망·무적 등으로 피해가 적용되지 않았다면
        // 텍스트·역경직·전투 자원·ChainAttack 모두 발생시키지 않음
        if (!wasDamageApplied) return false;

        // 타격 성공 VFX 처리
        if (context.HasImpactData)
        {
            VFXManager.Instance?.PlayHitImpact(
               context.Attacker,
               context.ImpactPoint,
               context.ImpactDirection
            );

            if (context.Attacker is Hero)
            {
                ParticleVFXManager.Instance?.PlayHitSpark(
                    context.Attacker,
                    context.ImpactPoint,
                    context.ImpactDirection
                );
            }
        }

        // Hero -> Enemy 에게 주는 HitMark(흔적) 데이터가 있을때 
        // VFX Manager 연출 실행
        if (context.Attacker is Hero &&
            context.Target is Enemy hitMarkEnemy &&
            context.HasImpactData &&
            context.HitMarkData != null)
        {
            VFXManager.Instance?.PlayHitMark(
                context.HitMarkData,
                hitMarkEnemy.transform,
                context.ImpactPoint,
                context.SurfaceNormal,
                context.SlashDirection
            );
        }

        // Chain Attack 발동 검사
        TryOpenChainAttack(context);

        // 데미지 텍스트 출력
        DamageTextManager.Instance?.PopDamageText(
            damageTextPosition,
            Mathf.RoundToInt(finalHitInfo.damage),
            finalHitInfo.isCrit
        );

        // Hero 타격 역경직 처리
        if (context.Attacker is Hero hero)
        {
            hero.Animation.TriggerHitStop(_heroHitStopDuration);
        }
        // Enemy 타격 역경직 처리
        else if (context.Attacker is Enemy enemy)
        {
            enemy.Animation.TriggerHitStop(_enemyHitStopDuration);
        }

        // 피격자 경직은 Target.TakeDamage 내부에서 처리
        // -> Hero / Enemy 각자 책임지게 변경

        return true;
    }

    /// <summary>
    /// 데미지 계산 공식 - 현재 디자인은 ( (TotalAttack / 100) * 계수 * (치명타 시 치명타 피해 / 100) )
    /// </summary>
    /// <param name="totalAttack">HeroStatus에서 가져온 최종 공격력 (마을 스탯 + 버프)</param>
    /// <param name="attackMultiplier">현재 공격(평타 콤보, 스킬 등)의 고유 계수</param>
    /// <param name="criticalChance">치명타 확률 (예: 53.5% 이면 53.5f 전달)</param>
    /// <param name="criticalHitDamage">치명타 피해량 (예: 150% 이면 150f 전달)</param>
    /// <returns>최종 계산된 데미지 수치와 치명타 여부</returns>
    private (float finalDamage, bool isCrit) CalculateFinalDamage(float totalAttack, float attackMultiplier, float critcal, float criticalHitDamage)
    {   // 튜플 반환형
        // 1. 치명타가 배제된 기본 베이스 데미지 계산 ((마을 스탯 / 100) * 계수)
        // 기획 의도에 맞춰 공식 그대로 구현 (공격력이 100일 때 계수가 1.5(150%)면 baseDamage는 1.5)
        float baseDamage = (totalAttack / 100f) * attackMultiplier;

        // 2. 치명타 확률 주사위 굴리기 (0.000...f ~ 100.0f 사이의 랜덤 실수)
        float randomRoll = Random.Range(0f, 100f);

        // 내 치명타 확률보다 낮은 숫자가 나왔다면 치명타 성공 (예: 확률이 60%일 때, 0~60 사이가 나오면 성공)
        bool isCrit = randomRoll <= critcal;

        // 3. 치명타 여부에 따른 최종 데미지 연산
        float finalDamage = baseDamage;

        if (isCrit)
        {
            // 치명타 성공 시: 베이스 데미지에 (치명타 피해 / 100)을 곱셈
            finalDamage = baseDamage * (criticalHitDamage / 100f);
        }

        // 4. 데미지 텍스트 팝업을 위해 '최종 데미지'와 '치명타 여부'를 동시에 반환 (튜플 활용)
        return (finalDamage, isCrit);
    }

    /// <summary>
    /// 그로기 계산 공식 - 현재 디자인은 ( (TotalImpact / 100) * 그로기 계수 )
    /// </summary>
    /// <param name="totalImpact">최종 충격력(캐릭터 그로기 스탯)</param>
    /// <param name="dazeMultiplier">현재 공격 그로기 계수</param>
    private float CalculateFinalDaze(float totalImpact, float dazeMultiplier)
    {
        return ((totalImpact / 100) * dazeMultiplier);
    }

    /// <summary>
    /// 공격자의 공격이 성공할 시, 공격자의 전투 자원이 차오르게 명령하는 메서드
    /// </summary>
    public void PayBackCombatResource(CombatEventContext context)
    {
        context.Attacker.OnHitSuccess(
            context.PayBackEnergy,
            context.PayBackDecibel
        );
    }

    #endregion


    #region [극한회피 / 패링지원 관리 영역]

    // TODO : 추후 Dictionary 활용하는 식으로 변경 예정
    // CombatManagerExam 스크립트 참고

    /// <summary>
    /// 공격에 대응했는지 반환 (극한회피/패링지원)
    /// </summary>
    private bool TryResolveDefense(CombatEventContext context)
    {
        // 타격대상이 Hero인지 체크
        if (context.Target is not Hero targetHero) return false;
        // Hero 무적여부 체크
        if (!targetHero.ActionRegistry.IsInvincible) return false;

        // HeroState에 따른 Swtich-Case
        switch (targetHero.CurrentState)
        {
            case HeroState.ParryIn:
                TryResolveParryAssist(targetHero, context);
                return true;

            case HeroState.Dodge:
                ResolvePerfectDodge(targetHero, context.Attacker);
                return true;

            default:
                return true;
        }
    }

    /// <summary>
    /// 패링지원 성공 여부를 반환
    /// </summary>
    private bool TryResolveParryAssist(
    Hero hero,
    CombatEventContext context)
    {
        if (hero.CurrentState != HeroState.ParryIn) return false;
        if (!IsParryAssistWindowOpen) return false;
        if (CurrentDefenseWindowAttacker != context.Attacker) return false;
        if (!_isParryAssistConsumed) return false;

        ResolveParryAssistSuccess(hero, context);
        return true;
    }

    public bool TryConsumeParryAssistRequest(out Enemy parryTarget)
    {
        parryTarget = null;

        if (!IsParryAssistWindowOpen) return false;
        if (_isParryAssistConsumed) return false;

        if (CurrentDefenseWindowAttacker is not Enemy activeEnemy) return false;
        if (!IsValidEnemyTarget(activeEnemy)) return false;

        _isParryAssistConsumed = true;
        parryTarget = activeEnemy;

        return true;
    }

    /// <summary>
    /// 패링지원 성공 시 장부 초기화 및 추가 연출 실행
    /// </summary>
    private void ResolveParryAssistSuccess(
    Hero hero,
    CombatEventContext context)
    {
        IAttacker attacker = context.Attacker;

        Debug.Log($"[패링지원 성공] {hero.name}");

        IsParryAssistWindowOpen = false;

        // ParryImpactSpark 발동지점
        if (context.HasImpactData)
        {
            Vector3 parryPoint = context.ImpactPoint;

            if (hero.VFX != null &&
                hero.VFX.TryGetParryImpactPoint(
                    out Vector3 anchorPoint))
            {
                parryPoint = anchorPoint;
            }

            ParticleVFXManager.Instance?.PlayParryParticle(
                context.Attacker,
                parryPoint,
                context.ImpactDirection
            );
        }

        if (attacker is Enemy enemy)
        {
            hero.SetCounterTarget(enemy);
            hero.ResolveParryImpact(enemy);
            ApplyParryAssistResult(hero, enemy);
            CombatPresentationManager.Instance?.PlayParryImpact();
        }
    }

    /// <summary>
    /// 극한회피 성공 시 추가 연출 실행
    /// </summary>
    private void ResolvePerfectDodge(Hero hero, IAttacker attacker)
    {
        if (!IsPerfectDodgeWindowOpen) return;
        if (CurrentDefenseWindowAttacker != attacker) return;
        if (_isPerfectDodgeConsumed) return;

        if (attacker is Enemy enemy)
        {
            hero.SetCounterTarget(enemy);
        }

        _isPerfectDodgeConsumed = true;

        if (CombatPresentationManager.Instance != null)
        {
            CombatPresentationManager.Instance.PlayPerfectDodgePresentation();
        }

        // Hero.ActionRegistry 회피 반격 가능 처리
        hero.ActionRegistry.SetCanPerfectDodgeCounter(true);
    }

    /// <summary>
    /// 적이 패링 가능한 공격을 시작할 때 호출하여 공격자 등록
    /// EnemyAnimationEventHandler - OnDefenseWindowOpen 에서 호출
    /// </summary>
    public void OpenDefenseWindow(Enemy attacker, bool canParry, bool canDodge)
    {
        if (attacker == null)
        {
            return;
        }

        CurrentDefenseWindowAttacker = attacker;

        IsParryAssistWindowOpen = canParry;
        IsPerfectDodgeWindowOpen = canDodge;

        _isPerfectDodgeConsumed = false;
        _isParryAssistConsumed = false;
    }

    /// <summary>
    /// 패링 타이밍이 끝났거나, 공격이 종료되었을 때 호출하여 비우기
    /// EnemyAnimationEventHandler - OnDefenseWindowClose 에서 호출
    /// Hero - ResetParryTarget 에서 호출
    /// </summary>
    public void CloseDefenseWindow(IAttacker attacker)
    {
        if (CurrentDefenseWindowAttacker != attacker)
        {
            return;
        }

        CurrentDefenseWindowAttacker = null;

        IsParryAssistWindowOpen = false;
        IsPerfectDodgeWindowOpen = false;

        _isPerfectDodgeConsumed = false;
        _isParryAssistConsumed = false;
    }

    /// <summary>
    /// 패링지원 대상 Enemy를 반환합니다.
    /// 현재 공격자를 우선하며, 없으면 록온 타겟을 대체 대상으로 사용
    /// </summary>
    //public bool TryGetParryAssistTarget(out Enemy parryTarget)
    //{
    //    if (CurrentActiveAttacker is Enemy activeEnemy && IsValidEnemyTarget(activeEnemy))
    //    {
    //        parryTarget = activeEnemy;
    //        return true;
    //    }

    //    Transform lockOnTarget = LockOnManager.Instance?.MagneticTarget;
    //    parryTarget = lockOnTarget != null
    //        ? lockOnTarget.GetComponentInParent<Enemy>() : null;

    //    return IsValidEnemyTarget(parryTarget);
    //}

    /// <summary>
    /// 사망하지 않은 Enemy인지 검사합니다.
    /// </summary>
    private bool IsValidEnemyTarget(Enemy enemy)
    {
        return enemy != null && enemy.Status != null && !enemy.Status.IsDead;
    }

    /// <summary>
    /// 패링지원 성공 시 확정된 공격자 한 명에게만
    /// 그로기 수치를 적용하고 Hero에게 전투 자원을 지급
    /// </summary>
    private void ApplyParryAssistResult(Hero hero, Enemy target)
    {
        if (hero == null || !IsValidEnemyTarget(target)) return;

        var parrySequences = hero.CombatData.parrySequences;

        // 현재 기획단계에서는 Parry 데이터는 motion[0] hit[0] 기준에 맞춰 적용
        if (parrySequences == null || parrySequences.Count == 0 ||
            parrySequences[0].hits == null || parrySequences[0].hits.Count == 0)
        {
            Debug.LogWarning($"[{hero.name}] 패링지원 데이터가 없습니다.");
            return;
        }

        HeroCombatDataSO.HitData hitData = parrySequences[0].hits[0];

        float finalDaze = CalculateFinalDaze(
            hero.Status.TotalImpact,
            hitData.dazeMultiplier
        );

        HitInfo parryHitInfo = new HitInfo
        {
            attacker = hero,
            damage = 0f,
            isCrit = false,
            dazeAmount = finalDaze,
            hitType = HitType.Parry,
            canExtendGroggy = hitData.canExtendGroggy,
            groggyExtendTime = hitData.groggyExtendTime
        };

        target.Groggy?.GroggyLogic(parryHitInfo);
        hero.OnHitSuccess(hitData.payBackEnergy, hitData.payBackDecibel);
    }

    #endregion


    #region [콤보스킬 (Chain Attack) 영역]

    /// <summary>
    /// 현재 타격이 Chain Attack 요청 자격이 있는지 검사
    /// -> ChainAttackManager.TryOpen 호출단계로 이동
    /// </summary>
    private void TryOpenChainAttack(CombatEventContext context)
    {
        if (!context.CanTriggerChainAttack) return;     // 해당 타격이 콤스 열수있는 강공격인지 체크
        if (context.Target is not Enemy enemy) return; 
        if (context.Attacker is not Hero hero) return;
        if (enemy.Status.IsDead) return;
        if (!enemy.Status.IsGroggy) return;

        ChainAttackManager chainAttackManager = ChainAttackManager.Instance;

        if (chainAttackManager == null)
        {
            Debug.LogWarning("ChainAttackManager Instance 없음");
            return;
        }

        PartyStateManager partyStateManager = PartyStateManager.Instance;

        if (partyStateManager == null)
        {
            Debug.LogWarning("PartyStateManager Instance 없음");
            return;
        }

        // 온필드 Hero 의 공격으로만 ChainAttack 발동가능
        if (hero != partyStateManager.CurrentHero)
        {
            return;
        }

        bool opened = chainAttackManager.TryOpenChainSelection(enemy, hero);

        if (opened)
        {
            Debug.Log("Chain QTE 개방 성공");
        }
    }

    #endregion


    #region [데미지 텍스트 position 캐싱 메서드]
    private Vector3 GetDamageTextSpawnPosition(IDamageable target)
    {
        if (target is not Component targetComponent)
            return Vector3.zero;

        Transform targetTransform = targetComponent.transform;
        Vector3 spawnPosition = targetTransform.position + Vector3.up * 1.5f;

        Camera mainCamera = Camera.main;
        if (mainCamera == null) return spawnPosition;

        Vector3 directionToCamera = (mainCamera.transform.position - targetTransform.position).normalized;

        spawnPosition += directionToCamera * 0.7f;
        spawnPosition +=
            mainCamera.transform.right * Random.Range(-0.2f, 0.2f) +
            Vector3.up * Random.Range(-0.1f, 0.1f);

        return spawnPosition;
    }
    #endregion
}
