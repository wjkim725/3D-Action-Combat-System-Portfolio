using Unity.VisualScripting;
using UnityEngine;

/// <summary>
/// EnemyAI에서 사용될 유틸리티 클래스
/// Blackboard의 필드들을 갱신 시켜주는 역할
/// </summary>
public class EnemySensor
{
    private EnemyBlackboard blackboard;

    public EnemySensor(EnemyBlackboard blackboard)
    {
        this.blackboard = blackboard;
    }

    public void Tick()
    {
        if (blackboard.Owner == null) return;

        blackboard.Target = blackboard.Owner.HeroTransform;

        blackboard.HasTarget = blackboard.Target != null;

        if (!blackboard.HasTarget)
        {
            blackboard.DistanceToTarget = Mathf.Infinity;
            return;
        }

        blackboard.DistanceToTarget = Vector3.Distance
            (blackboard.Owner.transform.position, blackboard.Target.position);

    }

}
