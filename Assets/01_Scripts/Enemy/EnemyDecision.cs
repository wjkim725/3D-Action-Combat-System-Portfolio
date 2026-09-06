using UnityEngine;

public class EnemyDecision
{
    private EnemyBlackboard blackboard;

    private bool isChasing;
    private bool isOrbiting;

    public EnemyDecision(EnemyBlackboard blackboard)
    {
        this.blackboard = blackboard;
    }

    public void Tick()
    {
        UpdateChaseState();
        UpdateOrbitState();
        UpdateAttackState();
    }


    private void UpdateChaseState()
    {
        float distance = blackboard.DistanceToTarget;

        if (!isChasing)
        {
            if (distance <= 15f)
                isChasing = true;
        }
        else
        {
            if (distance >= 17f)
                isChasing = false;
        }

        blackboard.IsInChaseRange = isChasing;
    }

    private void UpdateOrbitState()
    {
        float distance = blackboard.DistanceToTarget;

        if (!isOrbiting)
        {
            if (distance <= 4f)
                isOrbiting = true;
        }
        else
        {
            if (distance >= 6f)
                isOrbiting = false;
        }

        blackboard.IsInOrbitDistance = isOrbiting;
    }

    private void UpdateAttackState()
    {
        blackboard.SelectedAttackPattern =
            blackboard.Owner.Pattern.GetAttackPattern(
                blackboard.DistanceToTarget);

        blackboard.CanAttack =
            blackboard.SelectedAttackPattern != null;
    }
}
