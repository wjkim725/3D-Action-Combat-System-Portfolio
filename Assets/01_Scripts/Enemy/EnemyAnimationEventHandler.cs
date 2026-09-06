using UnityEngine;

public class EnemyAnimationEventHandler : MonoBehaviour
{
    private Enemy _enemy;

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
    }

    /// <summary>
    /// 실제 타격이 발생하는 프레임마다 배치
    /// </summary>
    public void OnAttackHitFrame()
    {
        if (_enemy.CurrentState != EnemyState.Attack) return;

        int motion = _enemy.ActionRegistry.CurrentMotionIndex;
        int hit = _enemy.ActionRegistry.GetHitIndexAndAdvance();

        _enemy.HitChecker.ExecuteAttackHitCheck(motion, hit);
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
    /// <param name="defenseType">
    /// 1 - DodgeOnly : 빨간색 워닝 사인
    /// 2 - Dodge + Parry : 노란색 워닝 사인
    /// </param>
    public void OnDefenseWindowOpen(int defenseType)
    {
        if (_enemy.CurrentState != EnemyState.Attack) return;
        if (CombatManager.Instance == null) return;

        if (defenseType is not 1 and not 2)
        {
            Debug.LogWarning($"잘못된 DefenseType: {defenseType}");
            return;
        }

        DefenseWindowType defenseWindowType = (DefenseWindowType)defenseType;

        bool canDodge = true;
        bool canParry = defenseWindowType == DefenseWindowType.DodgeAndParry;

        CombatManager.Instance.OpenDefenseWindow(
            _enemy,
            canParry,
            canDodge
        );

        VFXManager.Instance?.PlayWarningSign(_enemy, defenseWindowType);
    }

    /// <summary>
    /// 극한회피 / 패링지원을 위한 DefenseWindow Close 메서드
    /// </summary>
    public void OnDefenseWindowClose()
    {
        CombatManager.Instance?.CloseDefenseWindow(_enemy);
    }

    #endregion


    public void OnDeathEnd()
    {
        if (_enemy.CurrentState != EnemyState.Dead) return;

        _enemy.CompleteDeath();
    }

}
