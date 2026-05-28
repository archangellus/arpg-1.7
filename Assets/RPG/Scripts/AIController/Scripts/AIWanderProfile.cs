using System.Collections.Generic;
using UnityEngine;

public enum AreaShape
{
    Circle,
    Square
}

[CreateAssetMenu(menuName = "AIProfile/WanderProfile")]
public class AIWanderProfile : AIProfile
{
    [Header("Radius Settings")]
    public float circleRadius = 10f;
    public float squareSideLength = 10f;
    public AreaShape areaShape = AreaShape.Circle;

    [Header("Gizmo Settings")]
    public bool showWanderAreaGizmo = true;
    public bool showStoppingDistanceGizmo = true;
    public Color wanderColor = Color.blue;
    public Color stoppingDistanceColor = Color.red;
    public float gizmoYOffset = 0.03f;
    public int circleGizmoSegments = 48;
    public int squareGroundSubdivisions = 8;

    [Header("Timings")]
    public float minWaitTime = 0f;
    public float maxWaitTime = 1f;
    public float speed = 3.5f;

    [Header("Waiting Animations")]
    public List<string> waitingAnimationNames;

    [Header("Fleeing Settings")]
    public bool enableFleeing = false;
    public float fleeSpeedFactor = 1.5f;
    public float fleeMinDistance = 10f;
    public float fleeMaxDistance = 0f;

    public override Vector3 GetTargetPosition(Vector3 currentTargetPosition, Vector3 startPoint, AIController controller)
    {
        Vector3 targetPosition = currentTargetPosition;
        Transform playerTransform = controller.GetPlayerTransform();

        if (enableFleeing && playerTransform != null && Vector3.Distance(playerTransform.position, controller.transform.position) <= fleeMinDistance)
        {
            targetPosition = GetFleeingPosition(controller.transform.position, playerTransform.position, fleeMaxDistance, fleeSpeedFactor);

            float baseSpeed = Mathf.Max(speed, controller.GetPlayerSpeed());
            controller.SetMoveSpeed(baseSpeed * fleeSpeedFactor);

            return targetPosition;
        }

        controller.SetMoveSpeed(speed);

        if (controller.GetRemainingDistance() <= controller.GetStoppingDistance() && !controller.IsWaiting())
        {
            Vector3 randomDirection;

            switch (areaShape)
            {
                case AreaShape.Circle:
                    randomDirection = Random.insideUnitSphere * circleRadius;
                    randomDirection.y = 0f;
                    break;
                case AreaShape.Square:
                    randomDirection = new Vector3(Random.Range(-squareSideLength / 2f, squareSideLength / 2f), 0f, Random.Range(-squareSideLength / 2f, squareSideLength / 2f));
                    break;
                default:
                    randomDirection = Vector3.zero;
                    break;
            }

            targetPosition = controller.initialPosition + randomDirection;

            float waitTime = Random.Range(minWaitTime, maxWaitTime);
            if (waitTime > 0f)
            {
                controller.StartCoroutine(controller.WaitAndResume(waitTime, speed, waitingAnimationNames));
            }
        }

        return targetPosition;
    }

    public Vector3 GetFleeingPosition(Vector3 currentPosition, Vector3 playerPosition, float maxDistance, float speedFactor)
    {
        Vector3 fleeDirection = currentPosition - playerPosition;
        fleeDirection.y = 0f;

        if (fleeDirection.sqrMagnitude <= 0.0001f)
        {
            fleeDirection = Vector3.back;
        }

        fleeDirection.Normalize();
        float distanceToFlee = maxDistance > 0f ? maxDistance : Mathf.Max(1f, speedFactor);
        return currentPosition + fleeDirection * distanceToFlee;
    }

    public override void DrawGizmos(Vector3 startPoint, AIController controller)
    {
        if (controller == null || !controller.ShouldDrawGizmos())
        {
            return;
        }

        Vector3 gizmoCenter = startPoint;

        if (showWanderAreaGizmo)
        {
            switch (areaShape)
            {
                case AreaShape.Circle:
                    controller.DrawGroundAlignedFilledCircle(gizmoCenter, circleRadius, wanderColor, circleGizmoSegments, gizmoYOffset);
                    controller.DrawGroundAlignedWireCircle(gizmoCenter, circleRadius, wanderColor, circleGizmoSegments, gizmoYOffset + 0.01f);
                    break;
                case AreaShape.Square:
                    controller.DrawGroundAlignedFilledSquare(gizmoCenter, squareSideLength, wanderColor, squareGroundSubdivisions, gizmoYOffset);
                    controller.DrawGroundAlignedWireSquare(gizmoCenter, squareSideLength, wanderColor, squareGroundSubdivisions, gizmoYOffset + 0.01f);
                    break;
            }
        }

        if (showStoppingDistanceGizmo)
        {
            controller.DrawGroundAlignedFilledCircle(gizmoCenter, stoppingDistance, stoppingDistanceColor, circleGizmoSegments, gizmoYOffset + 0.02f);
        }
    }

    public override bool InRange(Vector3 currentPos, Vector3 startPoint, AIController controller)
    {
        switch (areaShape)
        {
            case AreaShape.Circle:
                return Vector3.Distance(currentPos, startPoint) <= circleRadius;
            case AreaShape.Square:
                Vector3 localPosition = currentPos - startPoint;
                return Mathf.Abs(localPosition.x) <= squareSideLength / 2f && Mathf.Abs(localPosition.z) <= squareSideLength / 2f;
            default:
                return false;
        }
    }
}
