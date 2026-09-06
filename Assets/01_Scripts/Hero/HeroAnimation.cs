using System.Collections;
using UnityEngine;

public class HeroAnimation : MonoBehaviour
{
    public Animator Animator { get; private set; }

    //private Animator _animator;

    [Header("Layer Settings")]
    [SerializeField] private int _upperBodyLayerIndex = 1;

    [Header("Idle Settings")]
    [SerializeField] private int _idleAniCount = 3; // 기본(0), 랜덤1(1), 랜덤2(2)
    [SerializeField] private float _waitTimeForRandomIdle = 10f;

    // 각 랜덤 Idle 애니메이션의 실제 플레이 시간 배열화 (0번 인덱스는 기본이므로 비워둠)
    [SerializeField] private float[] _randomIdlePlayTimes = { 0f, 5f, 9f };

    private bool _isIdle = true;
    private float _idleTimer = 0f;
    private float _randomIdleTimer = 0f;
    private bool _isPlayingRandomIdle = false;
    private int _randomIdleIndex;

    // --- 애니메이션 파라미터 해시 캐싱 (성능 최적화) ---
    private static readonly int HashIdleIndex = Animator.StringToHash("IdleIndex");
    private static readonly int HashIsIdle = Animator.StringToHash("IsIdle");
    private static readonly int HashIsCombat = Animator.StringToHash("IsCombat");
    private static readonly int HashMoveSpeed = Animator.StringToHash("MoveSpeed");
    private static readonly int HashHorizontal = Animator.StringToHash("Horizontal");
    private static readonly int HashVertical = Animator.StringToHash("Vertical");
    private static readonly int HashComboIndex = Animator.StringToHash("ComboIndex");
    private static readonly int HashIsCharging = Animator.StringToHash("IsCharging");
    private static readonly int HashIsDashAttack = Animator.StringToHash("IsDashAttack");
    private static readonly int HashIsComboReserved = Animator.StringToHash("IsComboReserved");
    private static readonly int HashIsParryCounterReserved = Animator.StringToHash("IsParryCounterReserved");
    private static readonly int HashDeathType = Animator.StringToHash("DeathType");
    private static readonly int HashHitType = Animator.StringToHash("HitType");
    private static readonly int HashHitVariant = Animator.StringToHash("HitVariant");

    private static readonly int HashAttackTrigger = Animator.StringToHash("AttackTrigger");
    private static readonly int HashSkillTrigger = Animator.StringToHash("SkillTrigger");
    private static readonly int HashUltTrigger = Animator.StringToHash("UltTrigger");
    private static readonly int HashExSkillTrigger = Animator.StringToHash("ExSkillTrigger");
    private static readonly int HashSwapInTrigger = Animator.StringToHash("SwapInTrigger");
    private static readonly int HashSwapOutTrigger = Animator.StringToHash("SwapOutTrigger");
    private static readonly int HashParryTrigger = Animator.StringToHash("ParryTrigger");
    private static readonly int HashParryImpactTrigger = Animator.StringToHash("ParryImpactTrigger");
    private static readonly int HashParryCounterTrigger = Animator.StringToHash("ParryCounterTrigger");
    private static readonly int HashDodgeCounterTrigger = Animator.StringToHash("DodgeCounterTrigger");
    private static readonly int HashDeathTrigger = Animator.StringToHash("DeathTrigger");
    private static readonly int HashChainTrigger = Animator.StringToHash("ChainTrigger");
    private static readonly int HashHitTrigger = Animator.StringToHash("HitTrigger");
    private static readonly int HashQuickAssistTrigger = Animator.StringToHash("QuickAssistTrigger");

    // 역경직 코루틴용 필드
    private Coroutine _hitStopCoroutine;

    // 외부 공개용 프로퍼티
    public bool IsCharging => Animator.GetBool("IsCharging");
    public bool IsDashAttack => Animator.GetBool("IsDashAttack");

    private void Awake()
    {
        Animator = GetComponent<Animator>();
    }

    /// <summary>
    /// 기본 Idle 상태가 지속될 때 주기적으로 랜덤 Idle 모션을 재생하는 로직
    /// 기존에는 HeroAnimation에서 Update로 무조건 호출됬지만,
    /// 현재는 Hero에서 UpdateState - Idle에서만 호출되게 최적화
    /// </summary>
    public void UpdateIdleLogic()
    {
        if (!_isPlayingRandomIdle)
        {
            _idleTimer += Time.deltaTime;
        }

        // 1. 지정된 대기 시간을 넘기면 랜덤 애니메이션 선정
        if (_idleTimer >= _waitTimeForRandomIdle && !_isPlayingRandomIdle)
        {
            _randomIdleIndex = Random.Range(1, _idleAniCount);
            _isPlayingRandomIdle = true;
            _randomIdleTimer = 0f;

            Debug.Log($"{_randomIdleIndex}번 Idle 애니메이션 재생");
            Animator.SetFloat(HashIdleIndex, _randomIdleIndex);
        }

        // 2. 랜덤 애니메이션이 재생 중일 때 시간 체크 및 탈출 판정
        if (_isPlayingRandomIdle)
        {
            _randomIdleTimer += Time.deltaTime;

            float currentPlayTime = _randomIdleIndex < _randomIdlePlayTimes.Length
                ? _randomIdlePlayTimes[_randomIdleIndex] : 5f;

            if (_randomIdleTimer >= currentPlayTime)
            {
                Animator.SetFloat(HashIdleIndex, 0);
                _randomIdleTimer = 0f;
                _idleTimer = 0f;
                _isPlayingRandomIdle = false;
            }
        }
    }

    /// <summary>
    /// Idle 상태 탈출 시 호출됨 -> UpdateIdleLogic에서 쓰이는 필드들 값 초기화
    /// </summary>
    public void ClearIdleLogicTimers()
    {
        _idleTimer = 0f;
        _randomIdleTimer = 0f;
        _isPlayingRandomIdle = false;
        Animator.SetFloat(HashIdleIndex, 0);
    }

    /// <summary>
    /// 외부(CombatManager)에서 역경직을 명령할 때 호출되는 메서드
    /// </summary>
    /// <param name="duration">역경직 지속 시간</param>
    public void TriggerHitStop(float duration)
    {
        if (duration <= 0f) return;

        // 이미 역경직이 돌고 있는 와중에 또 맞췄다면, 
        // 이전 타이머를 깔끔하게 취소하고 새 타이머로 갱신
        if (_hitStopCoroutine != null)
        {
            StopCoroutine(_hitStopCoroutine);
        }

        // 새로운 역경직 타이머 스타트
        _hitStopCoroutine = StartCoroutine(Co_HitStop(duration));
    }

    private IEnumerator Co_HitStop(float duration)
    {
        SetAnimatorSpeed(0f);

        // 현실 시간 기준으로 프레임 대기 (Time.timeScale 영향 없음)
        yield return new WaitForSecondsRealtime(duration);

        // 애니메이션 원상 복구
        SetAnimatorSpeed(1f);

        // 코루틴 인스턴스 비워주기
        _hitStopCoroutine = null;
    }

    private void SetAnimatorSpeed(float speed)
    {
        if (Animator != null)
        {
            Animator.speed = speed;
        }
    }

    // --- 외부 제어 Setter 영역 ---
    public void SetIsIdle(bool isIdle)
    {
        _isIdle = isIdle;
        Animator.SetBool(HashIsIdle, isIdle);
    }

    public void SetIsCombat(bool isCombat)
    {
        // Float 파라미터이지만 가독성을 위해 bool을 인자로 받아 처리
        Animator.SetFloat(HashIsCombat, isCombat ? 1f : 0f);
    }

    public void SetMoveSpeed(float moveSpeed)
    {
        Animator.SetFloat(HashMoveSpeed, moveSpeed);
    }

    public void SetComboIndex(int comboIndex)
    {
        Animator.SetInteger(HashComboIndex, comboIndex);
    }

    public void SetIsCharging(bool isCharging)
    {
        Animator.SetBool(HashIsCharging, isCharging);
    }

    public void SetIsDashAttack(bool isDashAttack)
    {
        Animator.SetBool(HashIsDashAttack, isDashAttack);
    }

    public void SetIsComboReserved(bool isComboReserved) => Animator.SetBool(HashIsComboReserved, isComboReserved);

    public void SetIsParryCounterReserved(bool isReserved) => Animator.SetBool(HashIsParryCounterReserved, isReserved);

    public void SetIsDeathType(int deathType)
    {
        Animator.SetInteger(HashDeathType, deathType);
    }
    // --- 외부 제어 Getter 영역 ---
    public bool GetIsDashAttack()
    {
        return Animator.GetBool(HashIsDashAttack);
    }

    // --- 상체 영역 독립 재생 (발도/납도) ---

    public void DrawSword()
    {
        Animator.CrossFadeInFixedTime("Draw_Start", 0.1f, _upperBodyLayerIndex);
    }

    public void SheathSword()
    {
        Animator.CrossFadeInFixedTime("Sheath_Start", 0.1f, _upperBodyLayerIndex);
    }

    // --- Trigger 제어 영역 ---

    /// <summary>
    /// Trigger 파라미터 큐 비워주는 통합 메서드
    /// </summary>
    public void ClearEveryTrigger()
    {
        ResetTriggerAttack();
        ResetTriggerSkill();
        ResetTriggerExSkill();
        ResetTriggerUlt();
        ResetTriggerSwapIn();
        ResetTriggerSwapOut();
        ResetTriggerParry();
        ResetTriggerParryImpact();
        ResetTriggerParryCounter();
        ResetTriggerDodgeCounter();
        ResetTriggerDeath();
        ResetTriggerChain();
        ResetTriggerHit();
        ResetTriggerQuickAssist();
    }

    public void TriggerAttack()
    {
        Animator.SetTrigger(HashAttackTrigger);
    }
    public void ResetTriggerAttack() => Animator.ResetTrigger(HashAttackTrigger);

    public void TriggerSkill()
    {
        Animator.SetTrigger(HashSkillTrigger);
    }
    public void ResetTriggerSkill() => Animator.ResetTrigger(HashSkillTrigger);

    public void TriggerUlt() => Animator.SetTrigger(HashUltTrigger);
    public void ResetTriggerUlt() => Animator.ResetTrigger(HashUltTrigger);

    public void TriggerExSkill() => Animator.SetTrigger(HashExSkillTrigger);
    public void ResetTriggerExSkill() => Animator.ResetTrigger(HashExSkillTrigger);

    public void TriggerSwapIn() => Animator.SetTrigger(HashSwapInTrigger);
    public void ResetTriggerSwapIn() => Animator.ResetTrigger(HashSwapInTrigger);

    public void TriggerSwapOut() => Animator.SetTrigger(HashSwapOutTrigger);
    public void ResetTriggerSwapOut() => Animator.ResetTrigger(HashSwapOutTrigger);

    public void TriggerParry()
    {
        Animator.SetTrigger(HashParryTrigger);
    }
    public void ResetTriggerParry()
    {
        Animator.ResetTrigger(HashParryTrigger);
    }

    public void TriggerImpactParry()
    {
        Animator.SetTrigger(HashParryImpactTrigger);
    }
    public void ResetTriggerParryImpact()
    {
        Animator.ResetTrigger(HashParryImpactTrigger);
    }

    public void TriggerParryCounter()
    {
        Animator.SetTrigger(HashParryCounterTrigger);
    }
    public void ResetTriggerParryCounter()
    {
        Animator.ResetTrigger(HashParryCounterTrigger);
    }

    public void TriggerDodgeCounter()
    {
        Animator.SetTrigger(HashDodgeCounterTrigger);
    }
    public void ResetTriggerDodgeCounter()
    {
        Animator.ResetTrigger(HashDodgeCounterTrigger);
    }

    public void TriggerDeath()
    {
        Animator.SetTrigger(HashDeathTrigger);
    }
    public void ResetTriggerDeath()
    {
        Animator.ResetTrigger(HashDeathTrigger);
    }

    public void TriggerChain()
    {
        Animator.SetTrigger(HashChainTrigger);
    }
    public void ResetTriggerChain()
    {
        Animator.ResetTrigger(HashChainTrigger);
    }

    public void PlayHitReaction(HitType hitType)
    {
        if (hitType == HitType.None || hitType == HitType.Parry)
        {
            return;
        }

        int hitVariant = 0;

        if (hitType == HitType.Light || hitType == HitType.Heavy)
        {
            hitVariant = Random.Range(0, 2);
        }

        // 전이 조건값을 먼저 설정
        Animator.SetInteger(HashHitType, (int)hitType);
        Animator.SetInteger(HashHitVariant, hitVariant);

        /*
         * Light → Heavy처럼 HeroState.Hit 내부에서
         * 다시 피격 애니메이션을 요청할 수 있으므로
         * 이전 Trigger를 명시적으로 초기화
         */
        Animator.ResetTrigger(HashHitTrigger);
        Animator.SetTrigger(HashHitTrigger);
    }
    public void ResetTriggerHit()
    {
        Animator.ResetTrigger(HashHitTrigger);
    }

    public void TriggerQuickAssist()
    {
        Animator.SetTrigger(HashQuickAssistTrigger);
    }
    public void ResetTriggerQuickAssist()
    {
        Animator.ResetTrigger(HashQuickAssistTrigger);
    }

    // --- CrossFade 제어 영역  ---

    public void CrossFadeToIdle(float fixedTime)
    {
        Animator.CrossFadeInFixedTime("Idle_blend", fixedTime);
    }

    public void CrossFadeToDodge(float fixedTime, Vector3 dashDir)
    {
        Animator.CrossFadeInFixedTime("Dodge_blend", fixedTime);
        Animator.SetFloat(HashHorizontal, dashDir.x);
        Animator.SetFloat(HashVertical, dashDir.z);
    }

    public void CrossFadeToSkill(float fixedTime)
    {
        Animator.CrossFadeInFixedTime("Skill", fixedTime);
    }

    public void CrossFadeToExSkill(float fixedTime)
    {
        Animator.CrossFadeInFixedTime("EX_Skill", fixedTime);
    }

    public void CrossFadeToSwapIn(float fixedTime)
    {
        Animator.CrossFadeInFixedTime("Swap_In.Entry", fixedTime);
    }

    public void CrossFadeToSwapOut(float fixedTime)
    {
        Animator.CrossFadeInFixedTime("Swap_Out.Turn", fixedTime);
    }

    public void CrossFadeToParryReady(float fixedTime)
    {
        Animator.CrossFadeInFixedTime("Base Layer.Parry.Parry_Ready", fixedTime);
    }

    public void CrossFadeToParryAfterNoAttack(float fixedTime)
    {
        Animator.CrossFadeInFixedTime("Base Layer.Parry.Parry_After_NoAttack", fixedTime);
    }

    // --- Play 제어 영역 ---

    /// <summary>
    /// 대쉬공격에서 일반 공격 2타로 이어질수 있게 강제로 Play 시켜줄 메서드
    /// </summary>
    /// <param name="fixedTime">애니메이션 시작 지점</param>
    public void DashAttackToComboAttack(float fixedTime)
    {
        Animator.Play("Attack_System.Attack", 0, fixedTime);
    }
}