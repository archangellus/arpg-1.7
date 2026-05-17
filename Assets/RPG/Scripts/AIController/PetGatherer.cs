using System.Collections.Generic;
using PLAYERTWO.ARPGProject;
using UnityEngine;
using UnityEngine.AI;

[DisallowMultipleComponent]
public class PetGatherer : MonoBehaviour
{
    public enum GatherableType
    {
        Money,
        RegularItems,
        SpecialItems,
    }

    [Header("Gather Settings")]
    public float gatherRange = 8f;
    public float maxYDifference = 2f;
    public float collectDistance = 1.35f;
    public float returnDistanceFromPlayer = 3f;
    public List<GatherableType> gatherableTypes = new() { GatherableType.Money, GatherableType.RegularItems };

    private AIController aiController;
    private NavMeshAgent navMeshAgent;
    private Entity petEntity;
    private Transform player;
    private Collectible activeTarget;
    private bool isGathering;

    private void Awake()
    {
        TryGetComponent(out aiController);
        TryGetComponent(out navMeshAgent);
        TryGetComponent(out petEntity);
    }

    public void SetPlayer(Transform playerTransform)
    {
        player = playerTransform;
    }

    public bool TryStartGather()
    {
        if (isGathering || player == null)
            return false;

        if (!TryFindNearestCollectible(out var collectible))
            return false;

        activeTarget = collectible;
        isGathering = true;

        if (aiController != null)
            aiController.enabled = false;

        if (navMeshAgent != null)
            navMeshAgent.isStopped = false;

        return true;
    }

    private void Update()
    {
        if (!isGathering)
            return;

        if (activeTarget == null)
        {
            ReturnToPlayer();
            return;
        }

        if (navMeshAgent != null)
        {
            navMeshAgent.SetDestination(activeTarget.transform.position);
            var distance = Vector3.Distance(transform.position, activeTarget.transform.position);

            if (distance <= collectDistance)
            {
                activeTarget.StartGathering();
                activeTarget.interactive = true;
                activeTarget.Interact(petEntity);
                activeTarget = null;
                ReturnToPlayer();
            }
        }
        else
        {
            Vector3 next = Vector3.MoveTowards(transform.position, activeTarget.transform.position, Time.deltaTime * 4f);
            transform.position = next;
        }
    }

    private void ReturnToPlayer()
    {
        if (player == null)
        {
            StopGathering();
            return;
        }

        if (navMeshAgent != null)
        {
            var targetPos = player.position - player.forward * returnDistanceFromPlayer;
            navMeshAgent.SetDestination(targetPos);

            if (Vector3.Distance(transform.position, targetPos) <= collectDistance)
                StopGathering();
        }
        else
        {
            StopGathering();
        }
    }

    private void StopGathering()
    {
        isGathering = false;
        activeTarget = null;

        if (aiController != null)
            aiController.enabled = true;
    }

    private bool TryFindNearestCollectible(out Collectible collectible)
    {
        collectible = null;

        float closestSqr = float.MaxValue;
        Vector3 origin = transform.position;
        float sqrRange = gatherRange * gatherRange;

        foreach (var candidate in Collectible.all)
        {
            if (candidate == null || !candidate.interactive)
                continue;

            if (!MatchesFilters(candidate))
                continue;

            Vector3 diff = candidate.transform.position - origin;

            if (Mathf.Abs(diff.y) > maxYDifference)
                continue;

            float sqr = diff.x * diff.x + diff.z * diff.z;
            if (sqr > sqrRange || sqr >= closestSqr)
                continue;

            closestSqr = sqr;
            collectible = candidate;
        }

        return collectible != null;
    }

    private bool MatchesFilters(Collectible collectible)
    {
        if (collectible is CollectibleMoney)
            return gatherableTypes.Contains(GatherableType.Money);

        if (collectible is CollectibleItem collectibleItem)
        {
            bool special = collectibleItem.item != null && collectibleItem.item.attributes != null && collectibleItem.item.attributes.GetAttributesCount() > 0;
            return special ? gatherableTypes.Contains(GatherableType.SpecialItems) : gatherableTypes.Contains(GatherableType.RegularItems);
        }

        return false;
    }
}
