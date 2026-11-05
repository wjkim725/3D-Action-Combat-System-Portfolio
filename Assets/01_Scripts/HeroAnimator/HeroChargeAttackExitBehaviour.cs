using UnityEngine;

/// <summary>
/// 해제 후 실행 모션 없이 끝나는 평꾹의 Animator 이탈을 Hero 종료 처리와 연결한다.
/// </summary>
public class HeroChargeAttackExitBehaviour : StateMachineBehaviour
{
    /// <summary>다른 Hero 상태로 중단된 경우는 제외하고 평꾹 종료를 전달한다.</summary>
    public override void OnStateExit(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        Hero hero = animator.GetComponent<Hero>();
        if (hero == null || hero.CurrentState != HeroState.ChargeAttack) return;

        hero.AnimationEventHandler.OnChargeAttackStateExit(layerIndex);
    }
}
