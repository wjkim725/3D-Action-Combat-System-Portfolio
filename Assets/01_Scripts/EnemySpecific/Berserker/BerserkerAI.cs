using System.Collections.Generic;

public class BerserkerAI : EnemyAI
{
    #region [행동 트리 구성]

    /// <summary>
    /// Mutant와 동일한 초기 우선순위로 Berserker의 행동 트리를 구성함.
    /// </summary>
    protected override BTNode SetupBehaviorTree()
    {
        return new BTSelector(
            Blackboard,
            new List<BTNode>
            {
                // --------------------------------------------------------------------------------
                // 1순위 행동: 공격 시퀀스
                //--------------------------------------------------------------------------------
                new BTSequence(
                    Blackboard,
                    new List<BTNode>
                    {
                        new BTCondition_CanAttack(Blackboard),
                        new BTAction_Attack(Blackboard)
                    }),

                // --------------------------------------------------------------------------------
                // 2순위 행동: 전투 대기 시퀀스
                // --------------------------------------------------------------------------------
                new BTSequence(
                    Blackboard,
                    new List<BTNode>
                    {
                        new BTCondition_IsInOrbitDistance(Blackboard),
                        new BTCondition_CheckGlobalCooldown(Blackboard),
                        new BTCondition_ShouldCombatIdle(Blackboard),
                        new BTAction_CombatIdle(Blackboard)
                    }),

                // --------------------------------------------------------------------------------
                // 3순위 행동: Orbit 시퀀스
                // --------------------------------------------------------------------------------
                new BTSequence(
                    Blackboard,
                    new List<BTNode>
                    {
                        new BTCondition_IsInOrbitDistance(Blackboard),
                        new BTCondition_CheckGlobalCooldown(Blackboard),
                        new BTCondition_ShouldOrbit(Blackboard),
                        new BTAction_Orbit(Blackboard)
                    }),

                // --------------------------------------------------------------------------------
                // 4순위 행동: 추격 시퀀스
                // --------------------------------------------------------------------------------
                new BTSequence(
                    Blackboard,
                    new List<BTNode>
                    {
                        new BTCondition_HasTarget(Blackboard),
                        new BTCondition_IsInChaseRange(Blackboard),
                        new BTAction_Chase(Blackboard)
                    }),

                // --------------------------------------------------------------------------------
                // 5순위 행동: 기본 대기
                // --------------------------------------------------------------------------------
                new BTAction_Idle(Blackboard)
            });
    }

    #endregion
}
