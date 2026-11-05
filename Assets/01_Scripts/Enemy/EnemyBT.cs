using System.Collections.Generic;
using UnityEngine;


public class EnemyBlackboard
{
    // Enemy 본체 참조
    public Enemy Owner;

    // Sensor
    public Transform Target;
    public bool HasTarget;
    public float DistanceToTarget;

    // Decision
    public bool IsInChaseRange;
    public bool IsInOrbitDistance;

    public EnemyAttackPattern SelectedAttackPattern;
    public bool CanAttack;

    // Combat Wait
    public EnemyCombatWaitType NextCombatWaitType = EnemyCombatWaitType.Orbit;
}

public enum NodeState { Running, Success, Failure }


public abstract class BTNode
{
    protected EnemyBlackboard blackboard;

    protected BTNode(EnemyBlackboard blackboard) { this.blackboard = blackboard; }
    public abstract NodeState Evaluate(); // 매 프레임 실행될 로직
}


#region [복합 노드]

public class BTSelector : BTNode
{
    protected List<BTNode> children = new List<BTNode>();

    public BTSelector(
        EnemyBlackboard blackboard,
        List<BTNode> children) 
        : base(blackboard) 
    {
        this.children = children;
    }

    public override NodeState Evaluate()
    {
        foreach (var node in children)
        {
            switch (node.Evaluate())
            {
                case NodeState.Failure:
                    continue; // 다음 자식 노드를 검사합니다.
                case NodeState.Success:
                    return NodeState.Success; // 하나라도 성공하면 즉시 종료!
                case NodeState.Running:
                    return NodeState.Running; // 하나라도 실행 중이면 즉시 종료!
            }
        }
        return NodeState.Failure; // 모든 자식이 실패했을 때만 Failure
    }
}

public class BTSequence : BTNode
{
    protected List<BTNode> children = new List<BTNode>();

    public BTSequence(
        EnemyBlackboard blackboard,
        List<BTNode> children) 
        : base(blackboard)
    {
        this.children = children;
    }

    public override NodeState Evaluate()
    {
        foreach (var node in children)
        {
            switch (node.Evaluate())
            {
                case NodeState.Failure:
                    return NodeState.Failure; // 하나라도 실패하면 시퀀스 전체 즉시 실패!
                case NodeState.Success:
                    continue; // 성공했으니 다음 자식 단계로 이동합니다.
                case NodeState.Running:
                    return NodeState.Running; // 실행 중이면 다음 프레임에 이어서 검사
            }
        }
        return NodeState.Success;
    }
}

#endregion


#region [조건 노드]

public class BTCondition_HasTarget : BTNode
{
    public BTCondition_HasTarget(EnemyBlackboard blackboard) : base(blackboard) { }


    public override NodeState Evaluate()
    {
        return blackboard.HasTarget
            ? NodeState.Success : NodeState.Failure;
    }
}

public class BTCondition_CanAttack : BTNode
{
    public BTCondition_CanAttack(EnemyBlackboard blackboard) : base(blackboard) { }

    public override NodeState Evaluate()
    {
        return blackboard.CanAttack
            ? NodeState.Success : NodeState.Failure;
    }
}

public class BTCondition_CheckGlobalCooldown : BTNode
{
    public BTCondition_CheckGlobalCooldown(EnemyBlackboard blackboard) : base(blackboard) { }

    public override NodeState Evaluate()
    {
        // 전역 쿨타임(패턴 휴식기) 체크
        return blackboard.Owner.Pattern.IsGlobalCooldownActive 
            ? NodeState.Success : NodeState.Failure;
    }
}

public class BTCondition_ShouldOrbit : BTNode
{
    public BTCondition_ShouldOrbit(
        EnemyBlackboard blackboard)
        : base(blackboard)
    {
    }

    public override NodeState Evaluate()
    {
        return blackboard.NextCombatWaitType ==
               EnemyCombatWaitType.Orbit
            ? NodeState.Success
            : NodeState.Failure;
    }
}

public class BTCondition_ShouldCombatIdle : BTNode
{
    public BTCondition_ShouldCombatIdle(
        EnemyBlackboard blackboard)
        : base(blackboard)
    {
    }

    public override NodeState Evaluate()
    {
        return blackboard.NextCombatWaitType ==
               EnemyCombatWaitType.CombatIdle
            ? NodeState.Success
            : NodeState.Failure;
    }
}

public class BTCondition_IsInChaseRange : BTNode
{
    public BTCondition_IsInChaseRange(EnemyBlackboard blackboard) : base(blackboard) { }

    public override NodeState Evaluate()
    {
        return blackboard.IsInChaseRange
            ? NodeState.Success
            : NodeState.Failure;
    }
}

public class BTCondition_IsInOrbitDistance : BTNode
{
    public BTCondition_IsInOrbitDistance(EnemyBlackboard blackboard) : base(blackboard) { }

    public override NodeState Evaluate()
    {
        return blackboard.IsInOrbitDistance
            ? NodeState.Success
            : NodeState.Failure;
    }
}

#endregion


#region [액션 노드]

public class BTAction_Attack : BTNode
{
    public BTAction_Attack(EnemyBlackboard blackboard) : base(blackboard) { }

    public override NodeState Evaluate()
    {
        if (blackboard.SelectedAttackPattern == null)
            return NodeState.Failure;

        blackboard.Owner.ExecuteAttack(blackboard.SelectedAttackPattern);

        return NodeState.Success;
    }
}

public class BTAction_Chase : BTNode
{
    public BTAction_Chase(EnemyBlackboard blackboard) : base(blackboard) { }

    public override NodeState Evaluate()
    {
        // FSM 상태를 Chase로 바꿈
        blackboard.Owner.ChangeState(EnemyState.Chase);

        // 이렇게 해야 다음 프레임에 BT가 1순위(공격 가능 여부)를 다시 검사할 수 있어서,
        // 사거리 내에 도달하는 순간 즉시 추격을 멈추고 공격으로 매끄럽게 전환됩니다.
        return NodeState.Running;
    }
}

public class BTAction_Idle : BTNode
{
    public BTAction_Idle(EnemyBlackboard blackboard) : base(blackboard) { }


    public override NodeState Evaluate()
    {
        if (blackboard.Owner.CurrentState != EnemyState.Idle)
        {
            blackboard.Owner.ChangeState(EnemyState.Idle);
        }
        return NodeState.Success;
    }
}

public class BTAction_CombatIdle : BTNode
{
    public BTAction_CombatIdle(
        EnemyBlackboard blackboard) : base(blackboard)
    {
    }

    public override NodeState Evaluate()
    {
        blackboard.NextCombatWaitType = EnemyCombatWaitType.Orbit;

        blackboard.Owner.ChangeState(EnemyState.CombatIdle);

        return NodeState.Success;
    }
}

public class BTAction_Orbit : BTNode
{
    
    // 목적지에 도달하지 못했을 때를 대비한 안전 제한시간
    private const float MaxRepositionDuration = 3f;

    private float _elapsedRepositionTime;
    private bool _isOrbiting;

    public BTAction_Orbit(
        EnemyBlackboard blackboard)
        : base(blackboard)
    {
    }

    public override NodeState Evaluate()
    {
        Enemy owner =
            blackboard.Owner;

        if (owner == null ||
            owner.Movement == null)
        {
            ResetOrbitAction();
            return NodeState.Failure;
        }

        /*
         * 새로운 Reposition 시작
         *
         * EnemyState.Orbit 진입 시
         * EnemyMovement.StartOrbiting()이 호출되고,
         * 아직 목적지가 없으므로 BeginOrbiting()이 실행.
         */
        if (!_isOrbiting)
        {
            _isOrbiting = true;
            _elapsedRepositionTime = 0f;

            owner.ChangeState(
                EnemyState.Orbit
            );

            return NodeState.Running;
        }

        /*
         * Hit 등으로 Orbit이 중단됐다가
         * Idle 상태로 복귀한 경우.
         *
         * EnemyMovement에 기존 목적지가 남아 있으므로
         * StartOrbiting() 내부에서 ResumeOrbiting()이 실행된다.
         */
        if (owner.CurrentState !=
            EnemyState.Orbit)
        {
            owner.ChangeState(
                EnemyState.Orbit
            );

            return NodeState.Running;
        }

        /*
         * 실제 Orbit 상태로 움직이는 시간만 누적한다.
         * AI가 Hit 상태로 정지한 시간은 포함되지 않는다.
         */
        _elapsedRepositionTime +=
            Time.deltaTime;

        bool hasReachedDestination =
            owner.Movement
                .HasReachedRepositionDestination;

        bool hasTimedOut =
            _elapsedRepositionTime >=
            MaxRepositionDuration;

        if (!hasReachedDestination &&
            !hasTimedOut)
        {
            return NodeState.Running;
        }

        /*
         * 제한시간 초과로 종료됐다면
         * 아직 이동 속도가 남아 있을 수 있으므로 즉시 정지한다.
         *
         * 목적지 도착 시에는 OrbitAndWalk()에서 이미
         * CurrentMoveSpeed와 Animator MoveSpeed가 0으로 정리된다.
         */
        if (hasTimedOut &&
            !hasReachedDestination)
        {
            owner.Movement.StopMovingImmediate();
        }

        owner.Movement.CompleteOrbiting();
        owner.AI.MarkOrbitEnded();

        blackboard.NextCombatWaitType =
            EnemyCombatWaitType.CombatIdle;

        ResetOrbitAction();

        return NodeState.Success;
    }

    /// <summary>
    /// BTAction 내부의 Reposition 진행 정보를 초기화한다.
    /// EnemyMovement의 목적지 정보는 CompleteOrbiting()에서 정리한다.
    /// </summary>
    private void ResetOrbitAction()
    {
        _isOrbiting = false;
        _elapsedRepositionTime = 0f;
    }
}

#endregion
