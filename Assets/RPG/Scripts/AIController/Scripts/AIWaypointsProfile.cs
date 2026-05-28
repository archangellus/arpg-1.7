using System.Collections;
using UnityEngine;

[System.Serializable]
public struct WaypointData
{
    public string waypointName;
    public float waitTime;
    public string animationName;

    public WaypointData(string name, float time, string animation)
    {
        waypointName = name;
        waitTime = time;
        animationName = animation;
    }
}

[CreateAssetMenu(menuName = "AIProfile/WaypointsProfile")]
public class AIWaypointsProfile : AIProfile
{
    public float gizmoSize = 1f;
    public string defaultAnimation = "Idle";
    public WaypointData[] waypoints = new WaypointData[0];
    public float speed = 3f;

    [Header("Gizmo Settings")]
    public bool showWaypointGizmos = true;
    public bool showStoppingDistanceGizmo = true;
    public bool showActivationRangeGizmo = true;
    public Color waypointColor = Color.blue;
    public Color stoppingDistanceColor = Color.red;
    public Color activationRangeColor = Color.yellow;
    public float gizmoYOffset = 0.03f;
    public int rangeGizmoSegments = 48;

    private int currentWaypointIndex = -1;

    public override Vector3 GetTargetPosition(Vector3 currentTargetPosition, Vector3 startPoint, AIController controller)
    {
        if (waypoints == null || waypoints.Length == 0)
        {
            return currentTargetPosition;
        }

        controller.SetMoveSpeed(speed);

        if (controller.GetRemainingDistance() <= controller.GetStoppingDistance() && !controller.IsWaiting())
        {
            currentWaypointIndex = GetNextWaypointIndex(currentWaypointIndex);

            GameObject waypoint = GameObject.Find(waypoints[currentWaypointIndex].waypointName);
            if (waypoint == null)
            {
                Debug.LogError("Waypoint " + waypoints[currentWaypointIndex].waypointName + " not found");
                return currentTargetPosition;
            }

            controller.StartCoroutine(controller.WaitAndAnimate(waypoints[currentWaypointIndex].waitTime, waypoints[currentWaypointIndex].animationName, speed, controller));
            return waypoint.transform.position;
        }

        return currentTargetPosition;
    }

    public string GetCurrentAnimation()
    {
        if (currentWaypointIndex >= 0 && currentWaypointIndex < waypoints.Length)
        {
            return waypoints[currentWaypointIndex].animationName;
        }

        return null;
    }

    public override bool InRange(Vector3 currentPos, Vector3 playerPos, AIController controller)
    {
        return Vector3.Distance(currentPos, playerPos) <= activationRange;
    }

    public override void DrawGizmos(Vector3 startPoint, AIController controller)
    {
        if (controller == null || !controller.ShouldDrawGizmos())
        {
            return;
        }

        if (showWaypointGizmos && waypoints != null)
        {
            foreach (WaypointData waypointData in waypoints)
            {
                GameObject waypoint = GameObject.Find(waypointData.waypointName);
                if (waypoint != null)
                {
                    Gizmos.color = waypointColor;
                    Vector3 waypointGizmoPosition = controller.GetGroundAlignedGizmoPoint(waypoint.transform.position, gizmoYOffset);
                    Gizmos.DrawSphere(waypointGizmoPosition, gizmoSize);
                }
            }
        }

        Vector3 center = controller.transform.position;

        if (showStoppingDistanceGizmo)
        {
            controller.DrawGroundAlignedWireCircle(center, stoppingDistance, stoppingDistanceColor, rangeGizmoSegments, gizmoYOffset + 0.01f);
        }

        if (showActivationRangeGizmo)
        {
            controller.DrawGroundAlignedWireCircle(center, activationRange, activationRangeColor, rangeGizmoSegments, gizmoYOffset + 0.02f);
        }
    }

    public override int GetNextWaypointIndex(int currentWaypointIndex)
    {
        if (waypoints == null || waypoints.Length == 0)
        {
            return -1;
        }

        return (currentWaypointIndex + 1) % waypoints.Length;
    }

    private IEnumerator WaitAndAnimate(float waitTime, string animationName, AIController controller)
    {
        yield return new WaitForSeconds(waitTime);

        Animator animator = controller.GetAnimator();
        if (animator != null && !string.IsNullOrEmpty(animationName))
        {
            animator.Play(animationName);
        }
    }
}
