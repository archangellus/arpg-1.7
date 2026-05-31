using System.Collections.Generic;
using UnityEngine;

namespace PLAYERTWO.ARPGProject
{
    [AddComponentMenu("PLAYER TWO/ARPG Project/Entity/Entity Auto Gathering")]
    public class EntityAutoGathering : MonoBehaviour
    {
        [System.Flags]
        public enum GatherMode
        {
            Items = 1,
            SpecialItems = 2,
            Money = 4,
        }

        [Header("Gathering Settings")]
        [Tooltip("Which types of collectibles this Entity will automatically gather.")]
        public GatherMode gatherMode = GatherMode.Items | GatherMode.Money;

        [Tooltip("The radius in world units to scan for collectibles.")]
        public float gatherRadius = 5f;

        [Tooltip(
            "Maximum Y-axis difference between the Entity and a collectible for it to be considered in range."
        )]
        public float maxYDifference = 2f;

        [Tooltip(
            "Seconds a collectible stays on the ground before it starts moving toward the Entity."
        )]
        public float gatherDelay = 1f;

        [Tooltip(
            "Time in seconds for a collectible to travel to the Entity once it starts moving."
        )]
        public float moveDuration = 0.2f;

        /// <summary>
        /// The Entity this component gathers collectibles for. Auto-assigned on Start if present on the same GameObject.
        /// </summary>
        public Entity entity { get; set; }

        protected Dictionary<Collectible, float> m_pendingCollectibles = new();
        protected Dictionary<
            Collectible,
            (float startTime, Vector3 startPosition)
        > m_movingCollectibles = new();

        protected List<Collectible> m_visibleCollectibles = new();
        protected List<Collectible> m_toRemove = new();
        protected List<Collectible> m_toPromote = new();
        protected List<Collectible> m_toCollect = new();

        protected virtual void InitializeEntity()
        {
            TryGetComponent(out Entity found);
            entity = found;
        }

        protected virtual void Start()
        {
            InitializeEntity();
        }

        protected virtual void FixedUpdate()
        {
            if (!entity)
                return;

            ScanForCollectibles();
            ProcessPendingCollectibles();
            ProcessMovingCollectibles();
        }

        protected virtual void ScanForCollectibles()
        {
            m_visibleCollectibles.Clear();

            var sqrRadius = gatherRadius * gatherRadius;
            var origin = transform.position;

            foreach (var collectible in Collectible.all)
            {
                var diff = collectible.transform.position - origin;

                if (Mathf.Abs(diff.y) > maxYDifference)
                    continue;

                if (diff.x * diff.x + diff.z * diff.z > sqrRadius)
                    continue;

                if (!ShouldGather(collectible))
                    continue;

                m_visibleCollectibles.Add(collectible);

                if (
                    !m_pendingCollectibles.ContainsKey(collectible)
                    && !m_movingCollectibles.ContainsKey(collectible)
                )
                    m_pendingCollectibles[collectible] = Time.time;
            }

            m_toRemove.Clear();

            foreach (var collectible in m_pendingCollectibles.Keys)
            {
                if (!m_visibleCollectibles.Contains(collectible))
                    m_toRemove.Add(collectible);
            }

            foreach (var collectible in m_toRemove)
                m_pendingCollectibles.Remove(collectible);
        }

        protected virtual void ProcessPendingCollectibles()
        {
            m_toPromote.Clear();

            foreach (var (collectible, detectedTime) in m_pendingCollectibles)
            {
                if (Time.time - detectedTime < gatherDelay)
                    continue;

                if (!CanCollect(collectible))
                    continue;

                m_toPromote.Add(collectible);
            }

            foreach (var collectible in m_toPromote)
            {
                collectible.StartGathering();
                m_movingCollectibles[collectible] = (Time.time, collectible.transform.position);
                m_pendingCollectibles.Remove(collectible);
            }
        }

        protected virtual void ProcessMovingCollectibles()
        {
            m_toCollect.Clear();

            foreach (var (collectible, data) in m_movingCollectibles)
            {
                if (collectible == null)
                {
                    m_toCollect.Add(collectible);
                    continue;
                }

                var t = (Time.time - data.startTime) / moveDuration;
                collectible.transform.position = Vector3.Lerp(
                    data.startPosition,
                    entity.position,
                    t
                );

                if (t >= 1f)
                    m_toCollect.Add(collectible);
            }

            foreach (var collectible in m_toCollect)
            {
                if (collectible != null)
                {
                    collectible.interactive = true;

                    if (ShouldUsePetInventory(collectible))
                    {
                        if (!TryCollectIntoPetInventory(collectible))
                        {
                            collectible.StartGathering();
                            collectible.interactive = true;
                        }
                    }
                    else
                    {
                        collectible.Interact(GetCollectorEntity(collectible));
                    }
                }

                m_movingCollectibles.Remove(collectible);
            }
        }

        /// <summary>
        /// Returns true if the given Collectible matches the current gather mode flags.
        /// </summary>
        protected virtual bool ShouldGather(Collectible collectible)
        {
            if (collectible is CollectibleMoney)
                return (gatherMode & GatherMode.Money) != 0;

            if (collectible is CollectibleItem collectibleItem)
            {
                var isSpecial =
                    collectibleItem.item != null
                    && collectibleItem.item.attributes != null
                    && collectibleItem.item.attributes.GetAttributesCount() > 0;

                return isSpecial
                    ? (gatherMode & GatherMode.SpecialItems) != 0
                    : (gatherMode & GatherMode.Items) != 0;
            }

            return false;
        }

        /// <summary>
        /// Returns true if the Entity's inventory has space to receive the given Collectible.
        /// </summary>
        protected virtual bool CanCollect(Collectible collectible)
        {
            if (collectible is CollectibleMoney)
                return true;

            if (collectible is CollectibleItem collectibleItem && collectibleItem.item != null)
            {
                var inventory = GetTargetInventory(collectible);
                if (inventory == null)
                    return false;

                var item = collectibleItem.item;

                if (item.IsStackable())
                {
                    foreach (var existingItem in inventory.items.Keys)
                    {
                        if (
                            existingItem.data == item.data
                            && existingItem.stack < existingItem.data.stackCapacity
                        )
                            return true;
                    }
                }

                return inventory.CanInsertItem(item);
            }

            return false;
        }

        protected virtual bool TryCollectIntoPetInventory(Collectible collectible)
        {
            if (!ShouldUsePetInventory(collectible))
                return false;

            return collectible.TryCollectInto(PetInventorySettings.instance.inventory, entity);
        }

        protected virtual bool ShouldUsePetInventory(Collectible collectible)
        {
            return collectible is CollectibleItem
                && entity
                && PetInventorySettings.instance
                && PetInventorySettings.isPetActive
                && IsActivePetGatherer();
        }

        protected virtual bool IsActivePetGatherer()
        {
            if (entity.GetComponentInParent<PetSummonOwnership>())
                return true;

            var activePet = PetInventorySettings.activePetTransform;
            if (!activePet)
                return false;

            return entity.transform == activePet
                || entity.transform.IsChildOf(activePet)
                || activePet.IsChildOf(entity.transform);
        }

        protected virtual Inventory GetTargetInventory(Collectible collectible)
        {
            if (ShouldUsePetInventory(collectible))
                return PetInventorySettings.instance.inventory;

            var collector = GetCollectorEntity(collectible);
            return collector ? collector.inventory.instance : null;
        }

        protected virtual Entity GetCollectorEntity(Collectible collectible)
        {
            if (collectible is CollectibleMoney && entity.GetComponentInParent<PetSummonOwnership>())
                return Level.instance ? Level.instance.player : entity;

            return entity;
        }

#if UNITY_EDITOR
        protected virtual void OnDrawGizmosSelected()
        {
            UnityEditor.Handles.color = Color.cyan;
            UnityEditor.Handles.DrawWireDisc(transform.position, Vector3.up, gatherRadius);
        }
#endif
    }
}
