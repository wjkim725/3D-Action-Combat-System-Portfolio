using UnityEngine;


public class EnemyAI : MonoBehaviour
{
    private Enemy _enemy;
    private BTNode _btRoot; // 비헤이비어 트리의 최상위 노드
    private EnemyBlackboard _blackboard;
    private EnemySensor _sensor;
    private EnemyDecision _decision;

    private void Awake()
    {
        _enemy = GetComponent<Enemy>();
        _blackboard = new EnemyBlackboard();
        _blackboard.Owner = _enemy;
        Debug.Assert(_blackboard.Owner != null);

        _sensor = new EnemySensor(_blackboard);
        _decision = new EnemyDecision(_blackboard);
    }

    private void Start()
    {
        _btRoot = SetupBehaviorTree();
    }

    private void Update()
    {
        _sensor.Tick();
        _decision.Tick();

        if (_btRoot != null && !_enemy.IsAIControlSuspended)
        {
            // 매 프레임 최상위 노드부터 아래로 조건을 훑으며 내려갑니다.
            _btRoot.Evaluate();
        }
    }

    // 적의 인공지능 트리를 조립하는 메서드
    private BTNode SetupBehaviorTree()
    {
        return new BTSelector(_blackboard, new System.Collections.Generic.List<BTNode>
        {
            // --------------------------------------------------------------------------------
            // 1순위 행동: 공격 시퀀스
            // --------------------------------------------------------------------------------
            new BTSequence(_blackboard, new System.Collections.Generic.List<BTNode>
            {
                new BTCondition_CanAttack(_blackboard), // 쿨타임이 끝났고 실제 개별 공격 사거리 안인지 검사
                new BTAction_Attack(_blackboard)
            }),

            // --------------------------------------------------------------------------------
            // 2순위 행동: Orbit 시퀀스
            // --------------------------------------------------------------------------------
            new BTSequence(_blackboard, new System.Collections.Generic.List<BTNode>
            {
                new BTCondition_IsInOrbitDistance(_blackboard),
                new BTCondition_CheckGlobalCooldown(_blackboard), // 글로벌 쿨타임 중일 때
                new BTAction_Orbit(_blackboard)                   // 설정 시간 동안 주변을 돎 (Running 반환)
            }),

            // --------------------------------------------------------------------------------
            // 3순위 행동: 추격 시퀀스 
            // --------------------------------------------------------------------------------
            new BTSequence(_blackboard, new System.Collections.Generic.List<BTNode>
            {
                new BTCondition_HasTarget(_blackboard),
                new BTCondition_IsInChaseRange(_blackboard),
                new BTAction_Chase(_blackboard)
            }),

            // --------------------------------------------------------------------------------
            // 4순위 행동: 기본 대기
            // --------------------------------------------------------------------------------
            new BTAction_Idle(_blackboard)
        });
    }

    
}
