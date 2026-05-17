using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

[CreateAssetMenu(menuName = "AIProfile/FollowPlayerProfile")]
public class AIFollowPlayerProfile : AIProfile
{
    [Header("Distance Settings")]
    public float followDistance = 5f;
    public float speed = 1f;

    [Tooltip("The pet will stop when it is within this buffer around Follow Distance. This prevents jitter at the boundary.")]
    public float followDistanceDeadZone = 0.25f;

    [Header("Pet Spawn / Summon")]
    [Tooltip("Optional input action used to summon/desummon the pet (new input system).")]
    public InputActionReference summonAction;
    public bool startSummoned = true;
    public bool teleportToPlayerOnSummon = true;
    public float summonOffsetDistance = 1.75f;

    [Header("Gather Settings")]
    public bool enableGathering = true;
    [Tooltip("Tags the pet can gather.")]
    public List<string> gatherableTags = new List<string> { "Gold", "Collectible" };
    public float gatherSearchRange = 8f;
    public float gatherInteractDistance = 1.25f;
    public float gatherScanInterval = 0.2f;

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

    private float idleTime = 0f;
    private int animationIndex = 0;
    private bool specialIdleAnimationIsPlaying = false;
    private Transform playerTransform = null;
    private bool isSummoned;
    private Transform currentGatherTarget;
    private float nextGatherScanTime;

    public override Vector3 GetTargetPosition(Vector3 currentTargetPosition, Vector3 startPoint, AIController controller)
    {
        HandleSummonInput(controller);

        if (!isSummoned)
        {
            controller.SetMoveSpeed(0f);
            SetAnimatorSpeed(controller, 0f);
            return controller.StopAtCurrentPosition();
        }

        playerTransform = controller.GetPlayerTransform();
        if (playerTransform == null)
        {
            return currentTargetPosition;
        }

        if (TryHandleGathering(controller, out Vector3 gatherTargetPosition))
        {
            return gatherTargetPosition;
        }

        float playerSpeed = controller.GetPlayerSpeed();

        Vector3 toPlayer = playerTransform.position - controller.transform.position;
        toPlayer.y = 0f;

        float distanceToPlayer = toPlayer.magnitude;
        Vector3 directionToPlayer = distanceToPlayer > 0.0001f ? toPlayer / distanceToPlayer : controller.transform.forward;

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

    private void HandleSummonInput(AIController controller)
    {
        if (!Application.isPlaying)
        {
            isSummoned = startSummoned;
            return;
        }

        if (summonAction == null || summonAction.action == null)
        {
            if (!isSummoned && startSummoned)
            {
                isSummoned = true;
            }
            return;
        }

        if (!summonAction.action.enabled)
        {
            summonAction.action.Enable();
        }

        if (summonAction.action.WasPressedThisFrame())
        {
            isSummoned = !isSummoned;
            currentGatherTarget = null;

            if (isSummoned && teleportToPlayerOnSummon)
            {
                Transform player = controller.GetPlayerTransform();
                if (player != null)
                {
                    Vector3 spawnDirection = player.forward;
                    spawnDirection.y = 0f;
                    if (spawnDirection.sqrMagnitude <= 0.0001f)
                    {
                        spawnDirection = Vector3.forward;
                    }

                    Vector3 summonPosition = player.position - spawnDirection.normalized * summonOffsetDistance;
                    summonPosition.y = controller.transform.position.y;
                    controller.transform.position = summonPosition;
                }
            }
        }
    }

    private bool TryHandleGathering(AIController controller, out Vector3 targetPosition)
    {
        targetPosition = Vector3.zero;

        if (!enableGathering || gatherableTags == null || gatherableTags.Count == 0)
        {
            return false;
        }

        if (currentGatherTarget == null && Time.time >= nextGatherScanTime)
        {
            nextGatherScanTime = Time.time + Mathf.Max(0.05f, gatherScanInterval);
            currentGatherTarget = FindClosestGatherable(controller.transform.position);
        }

        if (currentGatherTarget == null)
        {
            return false;
        }

        targetPosition = currentGatherTarget.position;
        targetPosition.y = controller.transform.position.y;

        float distance = Vector3.Distance(controller.transform.position, currentGatherTarget.position);
        if (distance <= Mathf.Max(0.1f, gatherInteractDistance))
        {
            GameObject gatheredObject = currentGatherTarget.gameObject;
            currentGatherTarget = null;
            Object.Destroy(gatheredObject);
            return false;
        }

        controller.SetMoveSpeed(speed);
        SetAnimatorSpeed(controller, speed);
        ExitSpecialIdleAnimationIfMoving(controller, speed);
        return true;
    }

    private Transform FindClosestGatherable(Vector3 center)
    {
        Collider[] nearby = Physics.OverlapSphere(center, Mathf.Max(0.1f, gatherSearchRange));
        Transform best = null;
        float bestDistance = float.MaxValue;

        for (int i = 0; i < nearby.Length; i++)
        {
            Collider candidate = nearby[i];
            if (candidate == null || candidate.transform == null)
            {
                continue;
            }

            if (!IsAllowedGatherTag(candidate.gameObject.tag))
            {
                continue;
            }

            float distance = Vector3.SqrMagnitude(candidate.transform.position - center);
            if (distance < bestDistance)
            {
                bestDistance = distance;
                best = candidate.transform;
            }
        }

        return best;
    }

    private bool IsAllowedGatherTag(string tag)
    {
        if (string.IsNullOrWhiteSpace(tag) || gatherableTags == null)
        {
            return false;
        }

        for (int i = 0; i < gatherableTags.Count; i++)
        {
            string allowedTag = gatherableTags[i];
            if (!string.IsNullOrWhiteSpace(allowedTag) && allowedTag == tag)
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
        // unchanged below
        if (controller.GetMovementSpeedMagnitude() <= 0.01f)
        {
            idleTime += Time.deltaTime;
            if (idleAnimations == null || idleAnimations.Count == 0) return;
            if (playAnimationsInOrder)
            {
                if (animationIndex >= idleAnimations.Count) animationIndex = 0;
                float extraDelay = 0f;
                if (animationDelays != null && animationIndex < animationDelays.Count) extraDelay = animationDelays[animationIndex];
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
        if (string.IsNullOrEmpty(animationName)) return;
        animator.Play(animationName, 0, 0f);
        specialIdleAnimationIsPlaying = true;
    }

    private void ExitSpecialIdleAnimationIfMoving(AIController controller, float movementSpeed)
    {
        idleTime = 0f;
        animationIndex = 0;
        Animator animator = controller.GetAnimator();
        if (animator == null) { specialIdleAnimationIsPlaying = false; return; }
        SetAnimatorSpeed(controller, movementSpeed);
        if (!returnToDefaultIdleWhenMoving || !specialIdleAnimationIsPlaying || string.IsNullOrEmpty(DefaultIdleAnimationName))
        {
            if (movementSpeed > 0.01f) specialIdleAnimationIsPlaying = false;
            return;
        }
        if (idleExitFadeDuration > 0f) animator.CrossFade(DefaultIdleAnimationName, idleExitFadeDuration, 0);
        else animator.Play(DefaultIdleAnimationName, 0, 0f);
        specialIdleAnimationIsPlaying = false;
    }

    private void SetAnimatorSpeed(AIController controller, float value)
    {
        Animator animator = controller.GetAnimator();
        if (animator != null) animator.SetFloat(controller.animatorParameter, value);
    }

    public override bool InRange(Vector3 position1, Vector3 position2, AIController controller)
    {
        return Vector3.Distance(position1, position2) <= activationRange;
    }

    public override void DrawGizmos(Vector3 startPoint, AIController controller)
    {
        if (controller == null || !controller.ShouldDrawGizmos()) return;
        Vector3 gizmoCenter = controller.transform.position;
        int segments = Mathf.Max(8, numSegments);
        if (showActivationRangeGizmo) controller.DrawGroundAlignedFilledCircle(gizmoCenter, activationRange, activationRangeColor, segments, gizmoYOffset);
        if (showDeadZoneGizmo)
        {
            float deadZone = Mathf.Max(controller.GetStoppingDistance(), followDistanceDeadZone);
            float innerDistance = Mathf.Max(0f, followDistance - deadZone);
            float outerDistance = followDistance + deadZone;
            controller.DrawGroundAlignedFilledRing(gizmoCenter, innerDistance, outerDistance, followDistanceDeadZoneColor, segments, gizmoYOffset + 0.01f);
            controller.DrawGroundAlignedWireCircle(gizmoCenter, innerDistance, followDistanceDeadZoneColor, segments, gizmoYOffset + 0.015f);
            controller.DrawGroundAlignedWireCircle(gizmoCenter, outerDistance, followDistanceDeadZoneColor, segments, gizmoYOffset + 0.015f);
        }
        if (showFollowDistanceGizmo) controller.DrawGroundAlignedWireCircle(gizmoCenter, followDistance, followDistanceColor, segments, gizmoYOffset + 0.02f);
        if (showStoppingDistanceGizmo) controller.DrawGroundAlignedFilledCircle(gizmoCenter, stoppingDistance, stoppingDistanceColor, segments, gizmoYOffset + 0.03f);
    }
}
