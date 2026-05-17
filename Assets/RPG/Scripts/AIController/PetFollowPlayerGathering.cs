using UnityEngine;
using PLAYERTWO.ARPGProject;

[DisallowMultipleComponent]
public class PetFollowPlayerGathering : MonoBehaviour
{
    [System.Flags]
    public enum GatherMode
    {
        Items = 1,
        SpecialItems = 2,
        Money = 4,
    }

    [Header("Gathering Settings")]
    public GatherMode gatherMode = GatherMode.Items | GatherMode.Money;
    public float detectRangeFromPlayer = 8f;
    public float maxYDifference = 2f;
    public float gatherDelay = 0.8f;
    public float collectDistance = 0.8f;
    public float moveDuration = 0.2f;

    private AIController controller;
    private Transform playerTransform;
    private Entity playerEntity;

    private Collectible currentTarget;
    private float detectedTime;

    private Collectible movingCollectible;
    private float movingCollectibleStartTime;
    private Vector3 movingCollectibleStartPosition;

    public bool IsGathering => currentTarget != null || movingCollectible != null;

    private void Awake()
    {
        controller = GetComponent<AIController>();
    }

    public void SetPlayer(Transform player)
    {
        playerTransform = player;
        playerEntity = player != null ? player.GetComponent<Entity>() : null;
    }

    public bool TryGetGatherTargetPosition(out Vector3 gatherTargetPosition)
    {
        gatherTargetPosition = transform.position;
        ProcessMovingCollectible();

        if (playerTransform == null || playerEntity == null)
            return false;

        if (movingCollectible != null)
            return true;

        if (currentTarget == null || !IsCollectibleValid(currentTarget))
        {
            currentTarget = FindClosestCollectible();
            detectedTime = Time.time;
        }

        if (currentTarget == null)
            return false;

        gatherTargetPosition = currentTarget.transform.position;
        gatherTargetPosition.y = transform.position.y;

        float distanceToCollectible = Vector3.Distance(transform.position, gatherTargetPosition);
        if (distanceToCollectible > collectDistance)
            return true;

        if (Time.time - detectedTime < gatherDelay)
            return true;

        if (!CanCollect(currentTarget))
        {
            currentTarget = null;
            return false;
        }

        currentTarget.StartGathering();
        movingCollectible = currentTarget;
        movingCollectibleStartTime = Time.time;
        movingCollectibleStartPosition = currentTarget.transform.position;
        currentTarget = null;

        return false;
    }

    private void ProcessMovingCollectible()
    {
        if (movingCollectible == null || playerEntity == null)
        {
            movingCollectible = null;
            return;
        }

        float t = (Time.time - movingCollectibleStartTime) / Mathf.Max(0.0001f, moveDuration);
        movingCollectible.transform.position = Vector3.Lerp(movingCollectibleStartPosition, playerEntity.position, t);

        if (t < 1f)
            return;

        movingCollectible.interactive = true;
        movingCollectible.Interact(playerEntity);
        movingCollectible = null;
    }

    private Collectible FindClosestCollectible()
    {
        Collectible closest = null;
        float closestSqrDistance = float.MaxValue;
        Vector3 playerPosition = playerTransform.position;
        float sqrRange = detectRangeFromPlayer * detectRangeFromPlayer;

        foreach (var collectible in Collectible.all)
        {
            if (!IsCollectibleValid(collectible) || !ShouldGather(collectible))
                continue;

            Vector3 diff = collectible.transform.position - playerPosition;
            if (Mathf.Abs(diff.y) > maxYDifference)
                continue;

            float sqrDistance = diff.x * diff.x + diff.z * diff.z;
            if (sqrDistance > sqrRange || sqrDistance >= closestSqrDistance)
                continue;

            closestSqrDistance = sqrDistance;
            closest = collectible;
        }

        return closest;
    }

    private bool IsCollectibleValid(Collectible collectible) => collectible != null && collectible.interactive;

    private bool ShouldGather(Collectible collectible)
    {
        if (collectible is CollectibleMoney)
            return (gatherMode & GatherMode.Money) != 0;

        if (collectible is CollectibleItem collectibleItem)
        {
            bool isSpecial = collectibleItem.item != null
                && collectibleItem.item.attributes != null
                && collectibleItem.item.attributes.GetAttributesCount() > 0;

            return isSpecial
                ? (gatherMode & GatherMode.SpecialItems) != 0
                : (gatherMode & GatherMode.Items) != 0;
        }

        return false;
    }

    private bool CanCollect(Collectible collectible)
    {
        if (playerEntity == null)
            return false;

        if (collectible is CollectibleMoney)
            return true;

        if (collectible is CollectibleItem collectibleItem && collectibleItem.item != null)
        {
            var inventory = playerEntity.inventory.instance;
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
}
