using System.Collections.Generic;
using UnityEngine;

public class EnemyAnimationEventHandler : MonoBehaviour
{
    private Enemy _enemy;
    private int _lastFootstepFrame = -1;
    private readonly List<AnimatorClipInfo> _footstepClipInfos = new();

    private void Awake()
    {
        _enemy = GetComponent<Enemy>();
    }


    #region [공격 이벤트]

    /// <summary>
    /// 각 연계 모션 애니메이션 시작점에 배치
    /// Animation Event의 Int 매개변수로 모션 인덱스 전달
    /// </summary>
    public void OnAttackMotionStart(int motionIndex)
    {
        if (_enemy.CurrentState != EnemyState.Attack) return;

        _enemy.ActionRegistry.SetMotionIndex(motionIndex);
        _enemy.Movement.BeginAttackRotationTracking();
    }

    /// <summary>
    /// 실제 타격이 발생하는 프레임마다 배치
    /// </summary>
    public void OnAttackExecuteFrame()
    {
        if (_enemy.CurrentState != EnemyState.Attack) return;

        int motion = _enemy.ActionRegistry.CurrentMotionIndex;
        int hit = _enemy.ActionRegistry.GetHitIndexAndAdvance();

        _enemy.HitChecker.ExecuteAttackHitCheck(motion, hit);

        // 타격 처리 직후 DefenseWindowClose 
        CombatManager.Instance?.CloseDefenseWindow(_enemy);
    }

    public void OnAttackEnd()
    {
        // 공격 상태가 이미 다른 상태에 의해 인터럽트됐다면 무시
        // EX : 공격중 그로기 발생한 경우
        if (_enemy.CurrentState != EnemyState.Attack) return;

        _enemy.ActionRegistry.ResetPatternSequence();
        _enemy.ChangeState(EnemyState.Idle);
    }

    /// <summary>
    /// 극한회피 / 패링지원을 위한 DefenseWindow Open 메서드
    /// </summary>
    /// <param name="animationEvent">
    /// Int : 1 - DodgeOnly / 2 - Dodge + Parry
    /// Float : 공격 모션 정지 시간
    /// </param>
    public void OnDefenseWindowOpen(AnimationEvent animationEvent)
    {
        if (_enemy.CurrentState != EnemyState.Attack) return;
        if (CombatManager.Instance == null) return;

        int defenseType = animationEvent.intParameter;
        float pauseDuration = animationEvent.floatParameter;

        // 이 부분 0 받으면 워닝사인 없는 공격으로 활용가능할지도.
        if (defenseType is not 1 and not 2)
        {
            Debug.LogWarning($"잘못된 DefenseType: {defenseType}");
            return;
        }

        // Hero 추적 회전 중지 명령
        _enemy.Movement.StopAttackRotationTracking();

        DefenseWindowType defenseWindowType =
            (DefenseWindowType)defenseType;

        bool canDodge = true;
        bool canParry =
            defenseWindowType == DefenseWindowType.DodgeAndParry;

        CombatManager.Instance.OpenDefenseWindow(
            _enemy,
            canParry,
            canDodge
        );

        VFXManager.Instance?.PlayWarningSign(
            _enemy,
            defenseWindowType
        );

        // 일단 기존 역경직 Animator Speed 조절 메서드 활용
        _enemy.Animation.TriggerHitStop(pauseDuration);
    }

    /// <summary>
    /// 극한회피 / 패링지원을 위한 DefenseWindow Close 메서드
    /// -> 해당 이벤트 핸들러를 HitFrame 1프레임 이후에 배치하니
    /// 패링교대로 등장하고 나왔는데 DefenseWindow가 닫혀서 패링이 안되는 경우가 생김.
    /// HitFrame 하단에 같은 호출시키기로 결정
    /// </summary>
    //public void OnDefenseWindowClose()
    //{
    //    CombatManager.Instance?.CloseDefenseWindow(_enemy);
    //}

    #endregion

    /// <summary>
    /// CombatIdle 애니메이션 종료 후 Idle로 복귀
    /// </summary>
    public void OnCombatIdleEnd()
    {
        if (_enemy.CurrentState != EnemyState.CombatIdle) return;

        Debug.Log("OnCombatIdleEnd");


        _enemy.CompleteCombatIdle();
    }

    public void OnDeathEnd()
    {
        if (_enemy.CurrentState != EnemyState.Dead) return;

        _enemy.CompleteDeath();
    }

    /// <summary>
    /// 범용 피격 애니메이션 이벤트 핸들러
    /// </summary>
    public void OnHitReactionEnd(
        int completedHitType)
    {
        if (_enemy == null ||
            _enemy.HitReaction == null)
        {
            return;
        }

        _enemy.HitReaction.CompleteReaction(
            (HitType)completedHitType);
    }
    /// <summary>
    /// Light 피격 애니메이션 종료 Event 핸들러
    /// </summary>
    public void OnLightHitReactionEnd()
    {
        _enemy.HitReaction.CompleteReaction(
            HitType.Light);
    }
    /// <summary>
    /// Heavy 피격 애니메이션 종료 Event 핸들러
    /// </summary>
    public void OnHeavyHitReactionEnd()
    {
        _enemy.HitReaction.CompleteReaction(
            HitType.Heavy);
    }

    #region [SFX 재생 요청 영역]

    /// <summary>
    /// 현재 동작의 대표 클립에서 발생한 발소리를 프레임당 한 번 재생함.
    /// 이동 BlendTree의 대표 클립만 허용하여 중복 재생을 방지함.
    /// </summary>
    public void OnFootstepSFX(AnimationEvent animationEvent)
    {
        if (_enemy == null || _enemy.Audio == null)
            return;

        // 사망한 Enemy의 발소리 이벤트를 차단함.
        if (_enemy.CurrentState == EnemyState.Dead)
            return;

        if (animationEvent == null || !animationEvent.isFiredByAnimator)
            return;

        if (_lastFootstepFrame == Time.frameCount)
            return;

        if (!CanPlayFootstep(animationEvent))
            return;

        _lastFootstepFrame = Time.frameCount;
        _enemy.Audio.PlayFootStep();
    }

    /// <summary>
    /// 기본 레이어의 재생 대상과 대표 클립에 해당하는 이벤트인지 반환함.
    /// </summary>
    private bool CanPlayFootstep(AnimationEvent animationEvent)
    {
        Animator animator = _enemy.Animation.Animator;

        _footstepClipInfos.Clear();

        AnimatorStateInfo targetState;

        if (animator.IsInTransition(0))
        {
            // 전환 중에는 진입할 상태를 기준으로 이전 상태의 이벤트를 차단함.
            targetState = animator.GetNextAnimatorStateInfo(0);
            animator.GetNextAnimatorClipInfo(0, _footstepClipInfos);
        }
        else
        {
            targetState = animator.GetCurrentAnimatorStateInfo(0);
            animator.GetCurrentAnimatorClipInfo(0, _footstepClipInfos);
        }

        if (animationEvent.animatorStateInfo.fullPathHash !=
            targetState.fullPathHash)
        {
            return false;
        }

        AnimationClip dominantClip = GetDominantFootstepClip();

        return dominantClip != null &&
            animationEvent.animatorClipInfo.clip == dominantClip;
    }

    /// <summary>
    /// 가중치가 가장 큰 클립을 선택하며 동률이면 먼저 조회된 클립을 유지함.
    /// </summary>
    private AnimationClip GetDominantFootstepClip()
    {
        AnimationClip dominantClip = null;
        float highestWeight = 0f;

        foreach (AnimatorClipInfo clipInfo in _footstepClipInfos)
        {
            if (clipInfo.clip == null || clipInfo.weight <= highestWeight)
                continue;

            highestWeight = clipInfo.weight;
            dominantClip = clipInfo.clip;
        }

        return dominantClip;
    }

    /// <summary>
    /// 공격 중인 Enemy의 휘두르기 효과음을 재생함.
    /// </summary>
    public void OnSwingSFX()
    {
        if (_enemy == null || _enemy.Audio == null)
            return;

        // 피격·그로기·사망으로 중단된 공격의 잔여 이벤트를 차단함.
        if (_enemy.CurrentState != EnemyState.Attack)
            return;

        _enemy.Audio.PlaySwing();
    }

    #endregion
}
