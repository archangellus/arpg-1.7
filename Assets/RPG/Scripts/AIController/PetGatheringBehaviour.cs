using System.Collections.Generic;
using UnityEngine;
using PLAYERTWO.ARPGProject;

[DisallowMultipleComponent]
public class PetGatheringBehaviour : MonoBehaviour
{
    [Header("Gather Settings")]
    public bool enableGathering = true;
    public float gatherRangeFromPlayer = 10f;
    public float pickupDistance = 1f;
    public float returnDistanceToPlayer = 5f;
    public float moveDuration = 0.2f;
    public float maxYDifference = 2f;
    public bool gatherMoney = true;
    public bool gatherItems = true;
    public List<Item> allowedItemTypes = new();

    private AIController controller;
    private Transform player;
    private Entity playerEntity;
    private Collectible currentTarget;
    private readonly Dictionary<Collectible, (float startTime, Vector3 startPosition)> movingCollectibles = new();
    private readonly List<Collectible> toCollect = new();

    public bool HasActiveTask => currentTarget != null || movingCollectibles.Count > 0;

    private void Awake()
    {
        controller = GetComponent<AIController>();
    }

    private void Update()
    {
        ProcessMovingCollectibles();
    }

    public bool TryGetGatherDestination(out Vector3 destination)
    {
        destination = transform.position;

        if (!enableGathering || controller == null)
            return false;

        player = controller.GetPlayerTransform();
        if (player == null)
            return false;

        if (playerEntity == null)
            playerEntity = player.GetComponent<Entity>();

        if (currentTarget == null)
            currentTarget = FindCollectibleTarget();

        if (currentTarget == null)
            return false;

        if (!CanStillGather(currentTarget))
        {
            currentTarget = null;
            return false;
        }

        destination = currentTarget.transform.position;
        destination.y = transform.position.y;

        float distance = Vector3.Distance(transform.position, currentTarget.transform.position);
        if (distance <= pickupDistance)
        {
            StartCollectibleMovement(currentTarget);
            currentTarget = null;
            destination = player.position;
            destination.y = transform.position.y;
        }

        return true;
    }

    private Collectible FindCollectibleTarget()
    {
        if (player == null)
            return null;

        Collectible best = null;
        float bestDistance = float.MaxValue;
        Vector3 playerPos = player.position;

        foreach (var collectible in Collectible.all)
        {
            if (!CanStillGather(collectible))
                continue;

            var diffPlayer = collectible.transform.position - playerPos;
            if (Mathf.Abs(diffPlayer.y) > maxYDifference)
                continue;

            float horizontalFromPlayer = new Vector2(diffPlayer.x, diffPlayer.z).magnitude;
            if (horizontalFromPlayer > gatherRangeFromPlayer)
                continue;

            float petDistance = Vector3.Distance(transform.position, collectible.transform.position);
            if (petDistance < bestDistance)
            {
                bestDistance = petDistance;
                best = collectible;
            }
        }

        return best;
    }

    private bool CanStillGather(Collectible collectible)
    {
        if (collectible == null || !collectible.interactive)
            return false;

        if (collectible is CollectibleMoney)
            return gatherMoney;

        if (collectible is CollectibleItem collectibleItem)
        {
            if (!gatherItems || collectibleItem.item == null || collectibleItem.item.data == null)
                return false;

            if (allowedItemTypes == null || allowedItemTypes.Count == 0)
                return true;

            return allowedItemTypes.Contains(collectibleItem.item.data);
        }

        return false;
    }

    private void StartCollectibleMovement(Collectible collectible)
    {
        collectible.StartGathering();
        movingCollectibles[collectible] = (Time.time, collectible.transform.position);
    }

    private void ProcessMovingCollectibles()
    {
        if (playerEntity == null || movingCollectibles.Count == 0)
            return;

        toCollect.Clear();

        foreach (var (collectible, data) in movingCollectibles)
        {
            if (collectible == null)
            {
                toCollect.Add(collectible);
                continue;
            }

            float t = (Time.time - data.startTime) / Mathf.Max(0.01f, moveDuration);
            collectible.transform.position = Vector3.Lerp(data.startPosition, playerEntity.position, t);

            if (t >= 1f)
                toCollect.Add(collectible);
        }

        foreach (var collectible in toCollect)
        {
            if (collectible != null)
            {
                collectible.interactive = true;
                collectible.Interact(playerEntity);
            }

            movingCollectibles.Remove(collectible);
        }
    }
}
