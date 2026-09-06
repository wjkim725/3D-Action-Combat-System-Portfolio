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

public class BTAction_Orbit : BTNode
{
    private float _orbitDuration = 0.5f; // Orbit을 유지할 시간 (예: 2초)
    private float _startTime;
    private bool _isOrbiting = false;   // 현재 Orbit 타이머가 도는 중인지 체크할 플래그

    public BTAction_Orbit(EnemyBlackboard blackboard) : base(blackboard) { }


    public override NodeState Evaluate()
    {
        // 1. 처음 진입할 때 타이머 시작
        if (blackboard.Owner.CurrentState != EnemyState.Orbit || !_isOrbiting)
        {
            _startTime = Time.time;
            _isOrbiting = true;
            blackboard.Owner.ChangeState(EnemyState.Orbit);
            return NodeState.Running;
        }

        // 2. 설정한 시간이 아직 안 지났다면 계속 돌기 유지
        if (Time.time < _startTime + _orbitDuration)
        {
            return NodeState.Running; // 확실하게 계속 Running을 리턴
        }

        // 3. 시간 다 채웠으면 플래그 끄고 Success 리턴해서 트리 리셋
        _isOrbiting = false;
        return NodeState.Success;
    }
}

#endregion