using System.Collections.Generic;
using PLAYERTWO.ARPGProject;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(AIController))]
public class AIFollowPetGatherer : MonoBehaviour
{
    public enum GatherItemType
    {
        Any,
        Armor,
        Weapon,
        Skill,
        Consumable,
        Misc
    }

    [Header("Detection")]
    [Min(0.1f)] public float gatherRange = 6f;
    public bool gatherMoney = true;
    public bool gatherItems = true;
    public List<GatherItemType> allowedItemTypes = new() { GatherItemType.Any };

    [Header("Collection")]
    [Min(0.05f)] public float collectDistance = 1.2f;

    private AIController controller;
    private Transform player;
    private Entity playerEntity;
    private Collectible currentTarget;
    private bool returningToPlayer;

    private void Awake() => controller = GetComponent<AIController>();

    private void Update()
    {
        if (!(controller.currentProfile is AIFollowPlayerProfile))
            return;

        if (player == null)
        {
            player = controller.GetPlayerTransform();
            if (player != null) player.TryGetComponent(out playerEntity);
            return;
        }

        if (currentTarget == null)
            currentTarget = FindBestCollectible();

        if (currentTarget == null)
        {
            returningToPlayer = false;
            return;
        }

        if (!IsValidTarget(currentTarget))
        {
            currentTarget = null;
            returningToPlayer = false;
            return;
        }

        controller.SetMoveSpeed(Mathf.Max(controller.GetMoveSpeed(), 2f));
        controller.SetDestination(currentTarget.transform.position);

        float distanceToCollectible = Vector3.Distance(transform.position, currentTarget.transform.position);
        if (distanceToCollectible <= collectDistance)
        {
            currentTarget.StartGathering();
            object collector = (object)playerEntity ?? gameObject;
            currentTarget.Collect(collector);
            currentTarget = null;
            returningToPlayer = true;
        }

        if (returningToPlayer && player != null)
        {
            float distanceToPlayer = Vector3.Distance(transform.position, player.position);
            if (distanceToPlayer <= Mathf.Max(1.5f, controller.GetStoppingDistance() + 0.5f))
                returningToPlayer = false;
        }
    }

    private Collectible FindBestCollectible()
    {
        Collectible best = null;
        float bestDistance = float.MaxValue;

        for (int i = 0; i < Collectible.all.Count; i++)
        {
            Collectible candidate = Collectible.all[i];
            if (!IsValidTarget(candidate))
                continue;

            float distance = Vector3.Distance(transform.position, candidate.transform.position);
            if (distance > gatherRange || distance >= bestDistance)
                continue;

            best = candidate;
            bestDistance = distance;
        }

        return best;
    }

    private bool IsValidTarget(Collectible collectible)
    {
        if (collectible == null || !collectible.gameObject.activeInHierarchy)
            return false;

        if (collectible is CollectibleMoney)
            return gatherMoney;

        if (collectible is CollectibleItem collectibleItem)
        {
            if (!gatherItems || collectibleItem.item == null || collectibleItem.item.data == null)
                return false;

            return IsAllowedItemType(collectibleItem.item);
        }

        return false;
    }

    private bool IsAllowedItemType(ItemInstance item)
    {
        if (allowedItemTypes == null || allowedItemTypes.Count == 0 || allowedItemTypes.Contains(GatherItemType.Any))
            return true;

        foreach (var type in allowedItemTypes)
        {
            switch (type)
            {
                case GatherItemType.Armor when item.data is ItemArmor:
                case GatherItemType.Weapon when item.data is ItemWeapon:
                case GatherItemType.Skill when item.IsSkill():
                case GatherItemType.Consumable when item.IsConsumable():
                    return true;
                case GatherItemType.Misc when !(item.data is ItemArmor) && !(item.data is ItemWeapon) && !item.IsSkill() && !item.IsConsumable():
                    return true;
            }
        }

        return false;
    }
}
