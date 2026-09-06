using System.Collections;
using UnityEngine;

public class EnemyAnimation : MonoBehaviour
{
    public Animator Animator { get; private set; }

    // 역경직 코루틴용 필드
    private Coroutine _hitStopCoroutine;

    private static readonly int HashIsIdle = Animator.StringToHash("IsIdle");
    private static readonly int HashMoveSpeed = Animator.StringToHash("MoveSpeed");
    private static readonly int HashIsGroggy = Animator.StringToHash("IsGroggy");
    private static readonly int HashDeathTrigger = Animator.StringToHash("DeathTrigger");

    private void Awake()
    {
        Animator = GetComponent<Animator>();
    }

    /// <summary>
    /// 외부(CombatManager)에서 역경직을 명령할 때 호출하는 단 하나의 안전한 창구
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

    public void SetMoveSpeed(float speed)
    {
        Animator.SetFloat(HashMoveSpeed, speed);
    }

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
}
