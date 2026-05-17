using System.Collections.Generic;
using PLAYERTWO.ARPGProject;
using UnityEngine;

[CreateAssetMenu(menuName = "AIProfile/FollowPlayerProfile")]
public class AIFollowPlayerProfile : AIProfile
{
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

    [Header("Player Assist Gathering")]
    public bool enableGathering = true;
    public bool gatherMoney = true;
    public bool gatherItems = true;
    public List<ItemTypeFilter> gatherableItemTypes = new() { ItemTypeFilter.Any };
    public float gatherScanRange = 8f;
    public float gatherMoveToCollectibleRange = 1.25f;
    public float gatherDelay = 0.15f;
    public float gatherMoveDuration = 0.2f;

    public enum ItemTypeFilter
    {
        Any,
        Weapon,
        Armor,
        Consumable,
        Skill,
        Equippable,
        Shield,
        Bow,
        Blade,
        Ring,
        Amulet,
        Potion,
    }

    private float idleTime = 0f;
    private int animationIndex = 0;
    private bool specialIdleAnimationIsPlaying = false;
    private Transform playerTransform = null;
    private Transform petTransform = null;
    private Entity playerEntity;
    private Entity petEntity;
    private Collectible currentGatherTarget;
    private readonly Dictionary<Collectible, float> pendingCollectibles = new();
    private readonly Dictionary<Collectible, (float startTime, Vector3 startPosition)> movingCollectibles = new();

    public override Vector3 GetTargetPosition(Vector3 currentTargetPosition, Vector3 startPoint, AIController controller)
    {
        playerTransform = controller.GetPlayerTransform();
        petTransform = controller.transform;

        if (playerTransform == null)
        {
            return currentTargetPosition;
        }
        if (playerEntity == null && playerTransform != null)
            playerEntity = playerTransform.GetComponent<Entity>();

        if (petEntity == null)
            controller.TryGetComponent(out petEntity);

        float playerSpeed = controller.GetPlayerSpeed();

        if (enableGathering)
        {
            ScanForCollectiblesInRange();

            if (TryGetGatherTarget(out var gatherTarget))
            {
                currentGatherTarget = gatherTarget;
                ProcessPendingCollectibles(controller, currentGatherTarget);
                ProcessMovingCollectibles();

                if (currentGatherTarget != null)
                    return MoveToGatherTarget(controller, currentGatherTarget, playerSpeed);
            }
            else
            {
                currentGatherTarget = null;
            }

            ProcessPendingCollectibles(controller, null);
            ProcessMovingCollectibles();
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

    private Vector3 MoveToGatherTarget(AIController controller, Collectible gatherTarget, float playerSpeed)
    {
        var gatherTargetPosition = gatherTarget.transform.position;
        gatherTargetPosition.y = controller.transform.position.y;

        float speedToCollect = Mathf.Max(speed, playerSpeed);
        controller.SetMoveSpeed(speedToCollect);
        SetAnimatorSpeed(controller, speedToCollect);
        ExitSpecialIdleAnimationIfMoving(controller, speedToCollect);

        return gatherTargetPosition;
    }

    private bool TryGetGatherTarget(out Collectible gatherTarget)
    {
        gatherTarget = null;

        if (petTransform == null)
        {
            return false;
        }

        float gatherRangeSqr = gatherScanRange * gatherScanRange;
        float closestDistance = float.MaxValue;

        foreach (var collectible in Collectible.all)
        {
            if (collectible == null || !ShouldGather(collectible))
            {
                continue;
            }

            float distToPetSqr = (collectible.transform.position - petTransform.position).sqrMagnitude;
            if (distToPetSqr > gatherRangeSqr)
            {
                continue;
            }

            if (distToPetSqr < closestDistance)
            {
                closestDistance = distToPetSqr;
                gatherTarget = collectible;
            }
        }

        return gatherTarget != null;
    }

    private void ScanForCollectiblesInRange()
    {
        if (petTransform == null)
            return;

        float gatherRangeSqr = gatherScanRange * gatherScanRange;

        foreach (var collectible in Collectible.all)
        {
            if (collectible == null || !ShouldGather(collectible))
                continue;

            float sqrDistance = (collectible.transform.position - petTransform.position).sqrMagnitude;
            if (sqrDistance > gatherRangeSqr)
                continue;

            if (!pendingCollectibles.ContainsKey(collectible) && !movingCollectibles.ContainsKey(collectible))
                pendingCollectibles[collectible] = Time.time;
        }

        var removeList = new List<Collectible>();

        foreach (var collectible in pendingCollectibles.Keys)
        {
            if (collectible == null)
            {
                removeList.Add(collectible);
                continue;
            }

            float sqrDistance = (collectible.transform.position - petTransform.position).sqrMagnitude;
            if (sqrDistance > gatherRangeSqr || !ShouldGather(collectible))
                removeList.Add(collectible);
        }

        foreach (var collectible in removeList)
            pendingCollectibles.Remove(collectible);
    }

    private void ProcessPendingCollectibles(AIController controller, Collectible activeTarget)
    {
        var promoteList = new List<Collectible>();

        foreach (var pair in pendingCollectibles)
        {
            var collectible = pair.Key;
            if (collectible == null)
            {
                promoteList.Add(collectible);
                continue;
            }

            if (Time.time - pair.Value < gatherDelay)
                continue;

            if (!CanCollect(collectible))
                continue;

            if (activeTarget == collectible)
            {
                float distToPet = Vector3.Distance(collectible.transform.position, petTransform.position);
                float effectiveGatherRange = Mathf.Max(gatherMoveToCollectibleRange, controller.GetStoppingDistance() + controller.arrivalTolerance + 0.05f);

                if (distToPet <= effectiveGatherRange)
                {
                    collectible.StartGathering();
                    movingCollectibles[collectible] = (Time.time, collectible.transform.position);
                    promoteList.Add(collectible);
                    currentGatherTarget = null;
                }
            }
        }

        foreach (var collectible in promoteList)
            pendingCollectibles.Remove(collectible);
    }

    private void ProcessMovingCollectibles()
    {
        var toCollect = new List<Collectible>();

        foreach (var pair in movingCollectibles)
        {
            var collectible = pair.Key;
            if (collectible == null || playerTransform == null)
            {
                toCollect.Add(collectible);
                continue;
            }

            float t = (Time.time - pair.Value.startTime) / Mathf.Max(0.01f, gatherMoveDuration);
            collectible.transform.position = Vector3.Lerp(pair.Value.startPosition, petTransform.position, t);

            if (t >= 1f)
                toCollect.Add(collectible);
        }

        foreach (var collectible in toCollect)
        {
            if (collectible != null)
            {
                collectible.interactive = true;
                collectible.Interact(playerEntity != null ? playerEntity : petEntity);
            }

            movingCollectibles.Remove(collectible);
        }
    }


    private bool CanCollect(Collectible collectible)
    {
        if (collectible is CollectibleMoney)
            return true;

        var collector = playerEntity != null ? playerEntity : petEntity;
        if (collector == null)
            return false;

        if (collectible is CollectibleItem collectibleItem && collectibleItem.item != null)
        {
            var inventory = collector.inventory.instance;
            var item = collectibleItem.item;

            if (item.IsStackable())
            {
                foreach (var existingItem in inventory.items.Keys)
                {
                    if (existingItem.data == item.data && existingItem.stack < existingItem.data.stackCapacity)
                        return true;
                }
            }

            return inventory.CanInsertItem(item);
        }

        return false;
    }

    private bool ShouldGather(Collectible collectible)
    {
        if (collectible is CollectibleMoney)
            return gatherMoney;

        if (!gatherItems || collectible is not CollectibleItem collectibleItem || collectibleItem.item?.data == null)
            return false;

        if (gatherableItemTypes == null || gatherableItemTypes.Count == 0 || gatherableItemTypes.Contains(ItemTypeFilter.Any))
            return true;

        var item = collectibleItem.item.data;

        foreach (var filter in gatherableItemTypes)
        {
            if ((filter == ItemTypeFilter.Weapon && item is ItemWeapon)
                || (filter == ItemTypeFilter.Armor && item is ItemArmor)
                || (filter == ItemTypeFilter.Consumable && item is ItemConsumable)
                || (filter == ItemTypeFilter.Skill && item is ItemSkill)
                || (filter == ItemTypeFilter.Equippable && item is ItemEquippable)
                || (filter == ItemTypeFilter.Shield && item is ItemShield)
                || (filter == ItemTypeFilter.Bow && item is ItemBow)
                || (filter == ItemTypeFilter.Blade && item is ItemBlade)
                || (filter == ItemTypeFilter.Ring && item is ItemRing)
                || (filter == ItemTypeFilter.Amulet && item is ItemAmulet)
                || (filter == ItemTypeFilter.Potion && item is ItemPotion))
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
