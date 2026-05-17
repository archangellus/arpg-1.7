using UnityEngine;

public abstract class AIProfile : ScriptableObject
{
    public float activationRange = 10f;
    public float stoppingDistance = 0.1f;

    public abstract Vector3 GetTargetPosition(Vector3 currentTargetPosition, Vector3 startPoint, AIController controller);
    public abstract void DrawGizmos(Vector3 startPoint, AIController controller);
    public abstract bool InRange(Vector3 currentPos, Vector3 playerPos, AIController controller);

    public virtual float GetWaitTime()
    {
        return 0f;
    }
    public virtual int GetNextWaypointIndex(int currentWaypointIndex)
    {
        return currentWaypointIndex;
    }
    public virtual bool IsPlayerInActivationRange(Vector3 currentPos, Vector3 playerPos)
    {
        float distanceToPlayer = Vector3.Distance(currentPos, playerPos);
        return distanceToPlayer <= activationRange;
    }
}
