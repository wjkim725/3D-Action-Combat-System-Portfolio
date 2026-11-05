using System.Collections;
using UnityEngine;

public class EnemyAnimation : MonoBehaviour
{
    public Animator Animator { get; private set; }

    [Header("Movement Animation")]
    [SerializeField]
    private bool _useDirectionalMovement;

    public bool UsesDirectionalMovement => _useDirectionalMovement;

    [Header("AI Mode Transition")]
    private float _idleReturnBlendDuration = 0.15f;
    public bool IsBlendingToIdle { get; private set; }
    private int _idleBlendStartFrame;
    // 해당 필드에 들어갈 값 주의 -> Animator의 기본 Idle State 이름은 Idle_Base 로 통일.
    private static readonly int HashIdleBaseState = Animator.StringToHash("Base Layer.Idle.Idle_Base");

    // 역경직 코루틴용 필드
    private Coroutine _hitStopCoroutine;

    private static readonly int HashIsIdle = Animator.StringToHash("IsIdle");
    private static readonly int HashMoveSpeed = Animator.StringToHash("MoveSpeed");
    private static readonly int HashMoveX = Animator.StringToHash("MoveX");
    private static readonly int HashMoveZ = Animator.StringToHash("MoveZ");
    private static readonly int HashIsGroggy = Animator.StringToHash("IsGroggy");
    private static readonly int HashHitType = Animator.StringToHash("HitType");
    private static readonly int HashHitDirection = Animator.StringToHash("HitDirection");
    
    private static readonly int HashDeathTrigger = Animator.StringToHash("DeathTrigger");
    private static readonly int HashCombatIdleTrigger = Animator.StringToHash("CombatIdleTrigger");
    private static readonly int HashHitTrigger = Animator.StringToHash("HitTrigger");

    private void Awake()
    {
        Animator = GetComponent<Animator>();
    }

    private void Update()
    {
        CheckIdleBlendCompletion();
    }

    private void OnDisable()
    {
        ClearIdleBlendWait();
    }

    #region [AI 모드 전환]

    /// <summary>
    /// 실제 Animator의 Idle 블렌딩 완료를 확인하고 AI 재개 대기를 해제함.
    /// </summary>
    private void CheckIdleBlendCompletion()
    {
        if (!IsBlendingToIdle || Time.frameCount <= _idleBlendStartFrame) return;

        // 일시정지 중 시간을 세지 않고 실제 Animator의 Idle 도착을 확인함.
        if (!Animator.IsInTransition(0) &&
            Animator.GetCurrentAnimatorStateInfo(0).fullPathHash == HashIdleBaseState)
        {
            IsBlendingToIdle = false;
        }
    }

    /// <summary>
    /// 중단된 공격의 역경직을 해제하고 Idle로 블렌딩하며 완료까지 AI 재개를 보류함.
    /// </summary>
    public void BlendToIdle()
    {
        if (!Animator.HasState(0, HashIdleBaseState))
        {
            Debug.LogError("EnemyAnimation: AI 모드 전환에 필요한 Idle 상태가 없습니다.", this);
            return;
        }

        if (_hitStopCoroutine != null)
        {
            StopCoroutine(_hitStopCoroutine);
            _hitStopCoroutine = null;
            SetAnimatorSpeed(1f);
        }

        SetIsIdle(true);
        _idleBlendStartFrame = Time.frameCount;
        IsBlendingToIdle = true;
        Animator.CrossFadeInFixedTime(HashIdleBaseState, _idleReturnBlendDuration, 0, 0f);
    }

    /// <summary>
    /// 피격, 그로기, 사망 또는 비활성화 시 모드 전환 대기만 해제함.
    /// 새 상태의 애니메이션과 AI 정지 여부는 해당 상태의 기존 처리를 따름.
    /// </summary>
    public void ClearIdleBlendWait()
    {
        IsBlendingToIdle = false;
    }

    #endregion

    /// <summary>
    /// 외부(CombatManager)에서 역경직을 명령할 때 호출
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

    // 내부에서만 안전하게 속도를 조절하도록 제어 구조 캡슐화
    private void SetAnimatorSpeed(float speed)
    {
        if (Animator != null)
        {
            Animator.speed = speed;
        }
    }


    public void SetIsIdle(bool isIdle)
    {
        Animator.SetBool(HashIsIdle, isIdle);
    }
    public void SetIsGroggy(bool isGroggy)
    {
        Animator.SetBool(HashIsGroggy, isGroggy);
    }

    #region [이동 애니메이션 영역]

    /// <summary>
    /// 로컬 수평 이동 속도와 선택적으로 좌우·전후 파라미터를 Animator에 전달함.
    /// </summary>
    public void SetMovement(Vector3 localMoveVelocity)
    {
        localMoveVelocity.y = 0f;
        Animator.SetFloat(HashMoveSpeed, localMoveVelocity.magnitude);

        if (!_useDirectionalMovement)
        {
            return;
        }

        Animator.SetFloat(HashMoveX, localMoveVelocity.x);
        Animator.SetFloat(HashMoveZ, localMoveVelocity.z);
    }

    #endregion

    public void TriggerAttack(string triggerName)
    {
        Animator.SetTrigger(triggerName);
    }
    public void ResetTriggerAttack(string triggerName)
    {
        Animator.ResetTrigger(triggerName);
    }

    public void TriggerDeath()
    {
        Animator.SetTrigger(HashDeathTrigger);
    }
    public void ResetTriggerDeath()
    {
        Animator.ResetTrigger(HashDeathTrigger);
    }

    public void TriggerCombatIdle()
    {
        Animator.SetTrigger(HashCombatIdleTrigger);
    }
    public void ResetTriggerCombatIdle()
    {
        Animator.ResetTrigger(HashCombatIdleTrigger);
    }

    /// <summary>
    /// 피격 강도와 Enemy 로컬 기준 피격 방향을 Animator에 전달하고
    /// 피격 애니메이션 전환 Trigger를 실행한다.
    /// </summary>
    public void PlayHitReaction(
        HitType hitType,
        HitReactionDirection direction)
    {
        if (Animator == null ||
            hitType == HitType.None)
        {
            return;
        }

        Animator.SetInteger(
            HashHitType,
            (int)hitType);

        Animator.SetInteger(
            HashHitDirection,
            (int)direction);

        // 연속 피격에서도 Trigger가 확실하게 다시 실행되도록 초기화한다.
        Animator.ResetTrigger(HashHitTrigger);
        Animator.SetTrigger(HashHitTrigger);
    }
    public void ResetTriggerHit()
    {
        Animator.ResetTrigger(HashHitTrigger);
    }

}
