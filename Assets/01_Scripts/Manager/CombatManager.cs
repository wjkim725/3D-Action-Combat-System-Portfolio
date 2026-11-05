using UnityEngine;

public class CombatManager : MonoBehaviour
{
    public static CombatManager Instance { get; private set; }

    // 실제 피해 적용에 성공한 대상별 알림. Context는 재사용되므로 구독자는 저장·변경하지 않음.
    public event System.Action<CombatEventContext> OnHitApplied;
    // 극한회피 판정과 중복 소비 처리가 완료된 Hero 및 공격자를 전달함.
    public event System.Action<Hero, IAttacker> OnPerfectDodgeSucceeded;
    // 교대 진입이 아닌 실제 Parry Impact 성공 처리가 끝난 뒤 전달함.
    public event System.Action<Hero, IAttacker> OnParryAssistSucceeded;

    // 현재 플레이어를 공격 중이거나, 패링 타이밍을 제공하고 있는 적의 참조
    // 이녀석이 패링 전의 ParryTarget의 역할을 해주고
    // 이걸 참조해서 Hero-LastAttacker에 담아 사용
    public IAttacker CurrentDefenseWindowAttacker { get; private set; }   
    public bool IsParryAssistWindowOpen { get; private set; }
    public bool IsPerfectDodgeWindowOpen { get; private set; }

    // 극한 회피와 패링 지원이 한 모션에 여러번 재생되지 않기위한 플래그
    private bool _isPerfectDodgeConsumed = false;
    private bool _isParryAssistConsumed = false;
    // 수락된 패링은 다른 Enemy의 방어 윈도우 갱신과 독립적으로 유지함.
    private Hero _pendingParryHero;
    private Enemy _pendingParryAttacker;

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
            attacker = context.Attacker,

            damage = damageResult.finalDamage,
            isCrit = damageResult.isCrit,
            dazeAmount = finalDaze,

            hitType = context.HitType,
            reactionDirection = context.ReactionDirection,
            hasReactionDirection = context.HasReactionDirection,

            canExtendGroggy = context.CanExtendGroggy,
            groggyExtendTime = context.GroggyExtendTime,

            payBackEnergy = context.PayBackEnergy,
            payBackDecibel = context.PayBackDecibel
        };

        // 타격 대상이 Destroy 될수도 있으니, 그전에 데미지 텍스트 위치 캐싱
        Vector3 damageTextAnchorPosition = context.Target.DamageTextAnchor.position;
        // 피해 적용 과정에서 대상이 제거되어도 재생 위치를 유지함.
        Vector3 hitImpactSFXPosition = context.HasImpactData
            ? context.ImpactPoint
            : damageTextAnchorPosition;

        // 피격자에게 TakeDamage 호출하도록 명령
        bool wasDamageApplied = context.Target.TakeDamage(finalHitInfo);

        // -------------------- 타격 성공 여부 ---------------------------- 

        // 사망·무적 등으로 피해가 적용되지 않았다면
        // 텍스트·역경직·전투 자원·ChainAttack 모두 발생시키지 않음
        if (!wasDamageApplied) return false;

        PlayHitImpactSFX(context, hitImpactSFXPosition);

        // 타격 성공 VFX 처리
        if (context.HasImpactData)
        {
            // 공격자 HitImpact
            VFXManager.Instance?.PlayHitImpact(
                context.Attacker,
                context.ImpactPoint,
                context.ImpactDirection
            );

            // 공격자 Hero - HitSpark
            if (context.Attacker is Hero)
            {
                ParticleVFXManager.Instance?.PlayHitSpark(
                    context.Attacker,
                    context.ImpactPoint,
                    context.ImpactDirection
                );
            }

            // 피격자 Enemy - HitReactionVFX
            if (context.Target is Enemy hitReactionEnemy)
            {
                ParticleVFXManager.Instance?.PlayHitReactionVFX(
                    hitReactionEnemy,
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
                context.ImpactDirection,
                context.SurfaceNormal,
                context.SlashDirection
            );
        }

        // Camera Shake 연출 실행
        // -> HeroHitChecker 로 호출 위치 변경
        // Motion & Hit 로 구성되있는 데이터 구조상
        // 모션당 한번의 CameraShake 발동을 위해서는 이전


        // Chain Attack 발동 검사
        TryOpenChainAttack(context);

        // 데미지 텍스트 출력
        DamageTextManager.Instance?.PopDamageText(
            damageTextAnchorPosition,
            Mathf.RoundToInt(finalHitInfo.damage),
            finalHitInfo.isCrit
        );

        // Hero 근접 공격에 역경직 발생
        // 투사체 공격에는 발사 시점에 역경직
        // 설치형 공격에는 역경직 발생 X
        if (context.Attacker is Hero hero)
        {
            if (context.HitDetectionType == HitDetectionType.MeleeOverlap)
            {
                TriggerHeroHitStop(hero);
            }
        }
        // Enemy 타격 역경직 처리
        else if (context.Attacker is Enemy enemy)
        {
            enemy.Animation.TriggerHitStop(_enemyHitStopDuration);
        }

        // 피격자 경직은 Target.TakeDamage 내부에서 처리
        // -> Hero / Enemy 각자 책임지게 변경

        OnHitApplied?.Invoke(context);
        return true;
    }

    /// <summary>
    /// 근접·영역 적중 또는 투사체 발사 시 공통 설정 시간으로 Hero 역경직을 요청한다.
    /// </summary>
    public void TriggerHeroHitStop(Hero hero)
    {
        if (hero == null) return;

        hero.Animation.TriggerHitStop(_heroHitStopDuration);
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
    /// 한 번의 타격 판정에서 한 대상 이상에게 피해 적용이 성공하면 자원을 반환함.
    /// 대상별 ProcessHit 처리가 끝난 뒤 판정 실행자가 한 번 호출하며,
    /// Hero의 OnAttackHitConfirmed를 통해 캐릭터 전용 기믹에 성공을 알림.
    /// </summary>
    public void PayBackCombatResource(CombatEventContext context)
    {
        if (context.Attacker is Hero hero)
        {
            hero.OnHitSuccess(context.PayBackEnergy, context.PayBackDecibel);
            hero.NotifyAttackHitConfirmed(context.AttackType);
        }
        else if (context.Attacker is Enemy enemy)
        {
            enemy.OnHitSuccess(context.PayBackEnergy, context.PayBackDecibel);
        }
    }

    /// <summary>
    /// 동일 타격에서 최초로 수락되는 적중음 요청 한 번만 재생함.
    /// </summary>
    private void PlayHitImpactSFX(CombatEventContext context, Vector3 position)
    {
        if (context.HitImpactSFX == null || context.HasPlayedHitImpactSFX)
            return;

        AudioManager manager = AudioManager.Instance;
        if (manager == null) return;

        bool wasPlaybackRequested = manager.PlayOneShot(context.HitImpactSFX, position);
        if (wasPlaybackRequested)
            context.MarkHitImpactSFXPlayed();
    }

    #endregion


    #region [극한회피 / 패링지원 관리 영역]

    /// <summary>
    /// 공격에 대응했는지 반환함 (자동회피/극한회피/패링지원).
    /// </summary>
    private bool TryResolveDefense(CombatEventContext context)
    {
        // 타격대상이 Hero인지 체크
        if (context.Target is not Hero targetHero) return false;

        // 사망 및 잔상 상태에서는 방어 기믹을 실행하지 않고 피격을 차단함.
        if (targetHero.Status.IsDead || targetHero.ActionRegistry.IsOffFieldActionActive) return true;

        // 무적 상태체크 이전에 자동회피 기믹 우선 작동
        if (targetHero.Mechanic?.TryEvadeHit(context) == true) return true;

        // 회피·패링 성공은 액션 무적 구간에서만 처리함.
        // CombatSandBox 무적만 활성화된 경우 피해만 차단함.
        if (!targetHero.ActionRegistry.IsActionInvincible)
            return targetHero.ActionRegistry.IsInvincibilityActive;

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
        if (_pendingParryHero != hero || _pendingParryAttacker == null) return false;
        if (_pendingParryAttacker != (Enemy)context.Attacker) return false;
        if (!IsValidEnemyTarget(_pendingParryAttacker)) return false;
        if (!hero.TryResolveParryImpact(_pendingParryAttacker)) return false;

        CancelParryAssistRequest(hero);
        ResolveParryAssistSuccess(hero, context);
        return true;
    }

    /// <summary>
    /// 교대할 Hero와 해당 공격자를 묶어 패링 요청을 한 번 수락함.
    /// </summary>
    public bool TryConsumeParryAssistRequest(Hero incomingHero, out Enemy parryTarget)
    {
        parryTarget = null;
        if (incomingHero == null || incomingHero.Status.IsDead) return false;

        if (!IsParryAssistWindowOpen) return false;
        if (_isParryAssistConsumed) return false;

        if (CurrentDefenseWindowAttacker is not Enemy activeEnemy) return false;
        if (!IsValidEnemyTarget(activeEnemy)) return false;

        _isParryAssistConsumed = true;
        // 연속 패링 교대가 실제 수락된 경우에만 이전 요청을 교체함.
        _pendingParryHero = incomingHero;
        _pendingParryAttacker = activeEnemy;
        parryTarget = activeEnemy;

        return true;
    }

    /// <summary>
    /// 지정 Hero가 소유한 미완료 패링 요청만 해제함.
    /// </summary>
    public void CancelParryAssistRequest(Hero hero)
    {
        if (_pendingParryHero != hero) return;
        _pendingParryHero = null;
        _pendingParryAttacker = null;
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

        if (CurrentDefenseWindowAttacker == attacker)
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
            ApplyParryAssistResult(hero, enemy);
            CombatPresentationManager.Instance?.PlayParryImpact();
            //CombatPresentationManager.Instance?.TryStopParryCamera(hero);
        }
        OnParryAssistSucceeded?.Invoke(hero, attacker);
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
        OnPerfectDodgeSucceeded?.Invoke(hero, attacker);
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

        // 같은 적의 다음 공격 기회에 이전 요청을 재사용하지 않음.
        if (_pendingParryAttacker == attacker) CancelParryAssistRequest(_pendingParryHero);
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
        // 최신 윈도우와 무관하게 해당 적의 미완료 요청을 종료함.
        if (_pendingParryAttacker != null && _pendingParryAttacker == (Enemy)attacker)
            CancelParryAssistRequest(_pendingParryHero);
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

        // 궁극기 연출 중 지연 타격으로 Chain QTE가 개방되는 것을 차단함.
        if (CombatPresentationManager.Instance != null &&
            CombatPresentationManager.Instance.IsUltPresentationActive)
        {
            return;
        }

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

}
