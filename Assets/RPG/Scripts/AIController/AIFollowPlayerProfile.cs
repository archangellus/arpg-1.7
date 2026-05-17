using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "AIProfile/FollowPlayerProfile")]
public class AIFollowPlayerProfile : AIProfile
{
    public enum GatherMode
    {
        Disabled,
        GatherGoldOnly,
        GatherItemsOnly,
        GatherGoldAndItems
    }

    [Header("Distance Settings")]
    public float followDistance = 5f;
    public float speed = 1f;

    [Tooltip("The pet will stop when it is within this buffer around Follow Distance. This prevents jitter at the boundary.")]
    public float followDistanceDeadZone = 0.25f;

    [Header("Gizmo Settings")]
    public int numSegments = 50;
    public bool showFollowDistanceGizmo = true;
    public bool showDeadZoneGizmo = true;
    public bool showActivationRangeGizmo = true;
    public bool showStoppingDistanceGizmo = true;
    public Color followDistanceColor = Color.cyan;
    public Color followDistanceDeadZoneColor = new Color(0f, 1f, 1f, 0.22f);
    public Color activationRangeColor = Color.yellow;
    public Color stoppingDistanceColor = Color.red;
    public float gizmoYOffset = 0.03f;

    [Header("Animation Settings")]
    public List<string> idleAnimations;
    public float idleTimeThreshold = 5f;
    public List<float> animationDelays;
    public string DefaultIdleAnimationName = "Idle";
    public bool playAnimationsInOrder = false;

    [Tooltip("When a special idle animation was started with Animator.Play, this returns the Animator to your normal idle/locomotion state as soon as movement starts.")]
    public bool returnToDefaultIdleWhenMoving = true;

    [Tooltip("Small blend time used when leaving a special idle animation and returning to the normal idle/locomotion state.")]
    public float idleExitFadeDuration = 0.1f;

    [Header("Pet Gather Settings")]
    public GatherMode gatherMode = GatherMode.GatherGoldAndItems;
    [Tooltip("How far from the pet to scan for gatherable targets.")]
    public float gatherScanRange = 7f;
    [Tooltip("How close the pet must get before a gather target is considered collected.")]
    public float gatherCollectDistance = 1f;
    [Tooltip("Tags that should be treated as gatherable items.")]
    public List<string> gatherableItemTags = new List<string> { "Item", "Gold" };
    [Tooltip("How often to search for nearby gather targets.")]
    public float gatherScanInterval = 0.25f;

    private float idleTime = 0f;
    private int animationIndex = 0;
    private bool specialIdleAnimationIsPlaying = false;
    private Transform playerTransform = null;
    private float gatherScanTimer = 0f;
    private Transform activeGatherTarget = null;

    public override Vector3 GetTargetPosition(Vector3 currentTargetPosition, Vector3 startPoint, AIController controller)
    {
        playerTransform = controller.GetPlayerTransform();
        if (playerTransform == null)
        {
            return currentTargetPosition;
        }

        float playerSpeed = controller.GetPlayerSpeed();

        if (TryHandleGatherAction(controller))
        {
            return controller.GetCurrentTargetPosition();
        }

        Vector3 toPlayer = playerTransform.position - controller.transform.position;
        toPlayer.y = 0f;

        float distanceToPlayer = toPlayer.magnitude;
        Vector3 directionToPlayer = distanceToPlayer > 0.0001f ? toPlayer / distanceToPlayer : controller.transform.forward;

        // Always use the desired point on the follow ring, not the player's center.
        // Returning the player's center makes the pet cross the boundary, then correct back, causing jitter.
        Vector3 desiredFollowPosition = playerTransform.position - directionToPlayer * followDistance;
        desiredFollowPosition.y = controller.transform.position.y;

        float deadZone = Mathf.Max(controller.GetStoppingDistance(), followDistanceDeadZone);
        float innerDistance = Mathf.Max(0f, followDistance - deadZone);
        float outerDistance = followDistance + deadZone;
        bool isInsideComfortBand = distanceToPlayer >= innerDistance && distanceToPlayer <= outerDistance;

        if (isInsideComfortBand)
        {
            controller.SetMoveSpeed(0f);
            SetAnimatorSpeed(controller, 0f);
            HandleIdleAnimations(controller);
            return controller.StopAtCurrentPosition();
        }

        float followSpeed = distanceToPlayer < innerDistance ? speed : Mathf.Max(speed, playerSpeed);
        controller.SetMoveSpeed(followSpeed);
        SetAnimatorSpeed(controller, followSpeed);
        ExitSpecialIdleAnimationIfMoving(controller, followSpeed);

        return desiredFollowPosition;
    }

    private bool TryHandleGatherAction(AIController controller)
    {
        if (gatherMode == GatherMode.Disabled)
        {
            activeGatherTarget = null;
            return false;
        }

        gatherScanTimer -= Time.deltaTime;
        if (activeGatherTarget == null && gatherScanTimer <= 0f)
        {
            gatherScanTimer = Mathf.Max(0.05f, gatherScanInterval);
            activeGatherTarget = FindClosestGatherTarget(controller.transform.position);
        }

        if (activeGatherTarget == null)
        {
            return false;
        }

        Vector3 gatherPos = activeGatherTarget.position;
        gatherPos.y = controller.transform.position.y;
        float distanceToTarget = Vector3.Distance(controller.transform.position, gatherPos);

        if (distanceToTarget <= Mathf.Max(0.05f, gatherCollectDistance))
        {
            Object.Destroy(activeGatherTarget.gameObject);
            activeGatherTarget = null;
            return false;
        }

        controller.SetMoveSpeed(speed);
        SetAnimatorSpeed(controller, speed);
        ExitSpecialIdleAnimationIfMoving(controller, speed);
        controller.SetDestination(gatherPos);
        return true;
    }

    private Transform FindClosestGatherTarget(Vector3 fromPosition)
    {
        Collider[] nearby = Physics.OverlapSphere(fromPosition, Mathf.Max(0.1f, gatherScanRange));
        Transform closest = null;
        float closestSqr = float.MaxValue;

        foreach (Collider hit in nearby)
        {
            if (hit == null || hit.transform == null)
            {
                continue;
            }

            if (!IsValidGatherTarget(hit.gameObject))
            {
                continue;
            }

            float sqr = (hit.transform.position - fromPosition).sqrMagnitude;
            if (sqr < closestSqr)
            {
                closestSqr = sqr;
                closest = hit.transform;
            }
        }

        return closest;
    }

    private bool IsValidGatherTarget(GameObject target)
    {
        if (target == null)
        {
            return false;
        }

        bool allowGold = gatherMode == GatherMode.GatherGoldOnly || gatherMode == GatherMode.GatherGoldAndItems;
        bool allowItems = gatherMode == GatherMode.GatherItemsOnly || gatherMode == GatherMode.GatherGoldAndItems;

        if (allowGold && target.CompareTag("Gold"))
        {
            return true;
        }

        if (!allowItems || gatherableItemTags == null)
        {
            return false;
        }

        for (int i = 0; i < gatherableItemTags.Count; i++)
        {
            string tag = gatherableItemTags[i];
            if (!string.IsNullOrWhiteSpace(tag) && target.CompareTag(tag))
            {
                return true;
            }
        }

        return false;
    }

    private void HandleIdleAnimations(AIController controller)
    {
        Animator animator = controller.GetAnimator();
        if (animator == null)
        {
            return;
        }

        if (controller.GetMovementSpeedMagnitude() <= 0.01f)
        {
            idleTime += Time.deltaTime;

            if (idleAnimations == null || idleAnimations.Count == 0)
            {
                return;
            }

            if (playAnimationsInOrder)
            {
                if (animationIndex >= idleAnimations.Count)
                {
                    animationIndex = 0;
                }

                float extraDelay = 0f;
                if (animationDelays != null && animationIndex < animationDelays.Count)
                {
                    extraDelay = animationDelays[animationIndex];
                }

                if (idleTime >= idleTimeThreshold + extraDelay)
                {
                    PlaySpecialIdleAnimation(animator, idleAnimations[animationIndex]);

                    idleTime = 0f;
                    animationIndex = (animationIndex + 1) % idleAnimations.Count;
                }
            }
            else if (idleTime >= idleTimeThreshold)
            {
                PlaySpecialIdleAnimation(animator, idleAnimations[Random.Range(0, idleAnimations.Count)]);
                idleTime = 0f;
            }
        }
        else
        {
            ExitSpecialIdleAnimationIfMoving(controller, controller.GetMoveSpeed());
        }
    }

    private void PlaySpecialIdleAnimation(Animator animator, string animationName)
    {
        if (string.IsNullOrEmpty(animationName))
        {
            return;
        }

        animator.Play(animationName, 0, 0f);
        specialIdleAnimationIsPlaying = true;
    }

    private void ExitSpecialIdleAnimationIfMoving(AIController controller, float movementSpeed)
    {
        idleTime = 0f;
        animationIndex = 0;

        Animator animator = controller.GetAnimator();
        if (animator == null)
        {
            specialIdleAnimationIsPlaying = false;
            return;
        }

        SetAnimatorSpeed(controller, movementSpeed);

        if (!returnToDefaultIdleWhenMoving || !specialIdleAnimationIsPlaying || string.IsNullOrEmpty(DefaultIdleAnimationName))
        {
            if (movementSpeed > 0.01f)
            {
                specialIdleAnimationIsPlaying = false;
            }

            return;
        }

        // Important: Animator.Play can put the Animator into an idle-only state that has no direct
        // Speed > 0 transition. Return to the normal idle/locomotion state first, then the Speed
        // parameter can drive walk/run transitions again.
        if (idleExitFadeDuration > 0f)
        {
            animator.CrossFade(DefaultIdleAnimationName, idleExitFadeDuration, 0);
        }
        else
        {
            animator.Play(DefaultIdleAnimationName, 0, 0f);
        }

        specialIdleAnimationIsPlaying = false;
    }

    private void SetAnimatorSpeed(AIController controller, float value)
    {
        Animator animator = controller.GetAnimator();
        if (animator != null)
        {
            animator.SetFloat(controller.animatorParameter, value);
        }
    }

    public override bool InRange(Vector3 position1, Vector3 position2, AIController controller)
    {
        return Vector3.Distance(position1, position2) <= activationRange;
    }

    public override void DrawGizmos(Vector3 startPoint, AIController controller)
    {
        if (controller == null || !controller.ShouldDrawGizmos())
        {
            return;
        }

        Vector3 gizmoCenter = controller.transform.position;
        int segments = Mathf.Max(8, numSegments);

        if (showActivationRangeGizmo)
        {
            controller.DrawGroundAlignedFilledCircle(gizmoCenter, activationRange, activationRangeColor, segments, gizmoYOffset);
        }

        if (showDeadZoneGizmo)
        {
            float deadZone = Mathf.Max(controller.GetStoppingDistance(), followDistanceDeadZone);
            float innerDistance = Mathf.Max(0f, followDistance - deadZone);
            float outerDistance = followDistance + deadZone;
            controller.DrawGroundAlignedFilledRing(gizmoCenter, innerDistance, outerDistance, followDistanceDeadZoneColor, segments, gizmoYOffset + 0.01f);
            controller.DrawGroundAlignedWireCircle(gizmoCenter, innerDistance, followDistanceDeadZoneColor, segments, gizmoYOffset + 0.015f);
            controller.DrawGroundAlignedWireCircle(gizmoCenter, outerDistance, followDistanceDeadZoneColor, segments, gizmoYOffset + 0.015f);
        }

        if (showFollowDistanceGizmo)
        {
            controller.DrawGroundAlignedWireCircle(gizmoCenter, followDistance, followDistanceColor, segments, gizmoYOffset + 0.02f);
        }

        if (showStoppingDistanceGizmo)
        {
            controller.DrawGroundAlignedFilledCircle(gizmoCenter, stoppingDistance, stoppingDistanceColor, segments, gizmoYOffset + 0.03f);
        }
    }
}
