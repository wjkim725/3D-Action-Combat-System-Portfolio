using UnityEngine;

public class HeroHitReaction : MonoBehaviour
{
    private Hero _hero;

    public Enemy CurrentReactionAttacker => _currentReactionAttacker;
    private Enemy _currentReactionAttacker;

    private HitType _currentHitType = HitType.None;

    private void Awake()
    {
        _hero = GetComponent<Hero>();    
    }


    public void OnEnterReactionState()
    {
        _hero.ActionRegistry.ResetCombo();
        _hero.ActionRegistry.SetIsButtonHolding(false);

        _hero.Animation.SetMoveSpeed(0f);
        _hero.Animation.SetIsCharging(false);
        _hero.Animation.SetIsDashAttack(false);
    }

    public void OnExitReactionState()
    {
        _currentHitType = HitType.None;
        _currentReactionAttacker = null;
    }

    /// <summary>
    /// 타격당할 때 경직도 처리 메서드
    /// </summary>
    public void TryStartReaction(HitInfo hitInfo)
    {
        HitType incomingHitType = hitInfo.hitType;

        if (!CanStartHitReaction(incomingHitType))
        {
            return;
        }

        HeroState nextState =
            incomingHitType == HitType.Knockback
                ? HeroState.Knockback : HeroState.Hit;

        _currentHitType = incomingHitType;
        _currentReactionAttacker = hitInfo.attacker as Enemy;

        /*
         * Light → Heavy는 둘 다 HeroState.Hit이므로
         * FSM 상태를 재진입하지 않고 애니메이션만 교체
         *
         * Hit → Knockback은 서로 다른 상태이므로
         * 정상적인 상태 전이를 실행
         */
        if (_hero.CurrentState != nextState)
        {
            _hero.ChangeState(nextState);
        }

        if (incomingHitType == HitType.Knockback)
        {
            // 넉백은 슈퍼아머 Break
            _hero.ActionRegistry.SetIsSuperArmorActive(false);
        }

        _hero.Animation.PlayHitReaction(incomingHitType);
    }

    private bool CanStartHitReaction(HitType incomingHitType)
    {
        if (_hero.Status.IsDead) return false;

        if (incomingHitType == HitType.None ||
            incomingHitType == HitType.Parry)
        {
            return false;
        }

        if (_hero.CurrentState == HeroState.InActive ||
            _hero.CurrentState == HeroState.Dead)
        {
            return false;
        }

        // 슈퍼아머는 Light/Heavy만 방어
        if (_hero.ActionRegistry.IsSuperArmorActive &&
            incomingHitType != HitType.Knockback)
        {
            return false;
        }

        // 현재 피격 상태가 아니라면 정상 진입
        if (!_hero.IsInHitReaction)
        {
            return true;
        }

        // 피격 중에는 더 높은 등급만 허용
        return (int)incomingHitType > (int)_currentHitType;
    }

    public void CompleteReaction(HitType completedHitType)
    {
        if (!_hero.IsInHitReaction) return;

        // 이전 피격 애니메이션의 늦은 이벤트 방어
        if (_currentHitType != completedHitType) return;

        _hero.BackToIdle();
    }

}
