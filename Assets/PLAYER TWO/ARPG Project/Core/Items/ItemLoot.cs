using System.Collections;
using UnityEngine;

namespace PLAYERTWO.ARPGProject
{
    [AddComponentMenu("PLAYER TWO/ARPG Project/Item/Item Loot")]
    public class ItemLoot : MonoBehaviour
    {
        [Tooltip("The Item Loot Stats settings for the loots.")]
        public ItemLootStats stats;

        [Header("Ground Settings")]
        [Tooltip(
            "The ground layer mask. The loot won't be "
                + "spawned if the ground below the loot point is not in this layer."
        )]
        public LayerMask groundMask = -5;

        [Tooltip(
            "The maximum distance to the ground. If the ground is "
                + "further than this distance from the loot point, the loot won't be spawned."
        )]
        public float maxGroundDistance = 2f;

        protected Entity m_entity;

        protected WaitForSeconds m_waitForLootLoopDelay;

        protected Item[] m_lootItems;

        protected const float k_groundOffset = 0.1f;
        protected const float k_lootRayOffset = 0.5f;
        protected const float k_lootLoopDelay = 0.1f;

        protected Game m_game => Game.instance;

        /// <summary>
        /// Gets the loot items from the stats.
        /// This property returns an array of items that can be looted.
        /// </summary>
        public Item[] lootItems
        {
            get
            {
                if (m_lootItems == null && stats.items.HasValues())
                    m_lootItems = stats.items.RemoveEmptyEntries<Item>();

                return m_lootItems;
            }
        }

        protected virtual void InitializeWaits()
        {
            m_waitForLootLoopDelay = new WaitForSeconds(k_lootLoopDelay);
        }

        protected virtual void InitializeEntity()
        {
            if (TryGetComponent(out m_entity))
                m_entity.onDie.AddListener(Loot);
        }

        /// <summary>
        /// Starts the looting routine.
        /// </summary>
        public virtual void Loot()
        {
            if (Random.Range(0, 1f) > stats.lootChance)
                return;

            StopAllCoroutines();
            StartCoroutine(LootRoutine());
        }

        protected virtual void InstantiateItem(Vector3 position)
        {
            if (!lootItems.HasValues())
                return;

            var index = Random.Range(0, lootItems.Length);
            var rolledRarity = RollRarity();
            var item =
                rolledRarity >= 0
                    ? new ItemInstance(lootItems[index], rolledRarity)
                    : new ItemInstance(lootItems[index]);

            Level.instance.InstantiateItemDrop(item, position);
        }

        /// <summary>
        /// Rolls a rarity level from the stats rarity list.
        /// Entries are evaluated in order; the first roll that passes wins.
        /// Returns -1 if no entry passes or if the list is empty.
        /// </summary>
        protected virtual int RollRarity()
        {
            if (stats.rarityLevels == null || stats.rarityLevels.Count == 0)
                return -1;

            var db = GameDatabase.instance;

            foreach (var entry in stats.rarityLevels)
            {
                if (Random.value <= entry.chance)
                {
                    if (entry.rarity == null)
                    {
                        Debug.LogWarning(
                            $"ItemLoot on '{name}': a rarity entry has no ItemRarity assigned. "
                                + "No attributes will be generated."
                        );
                        return -1;
                    }

                    var rarityId = db.itemRarities.IndexOf(entry.rarity);

                    if (rarityId < 0)
                    {
                        Debug.LogWarning(
                            $"ItemLoot on '{name}': ItemRarity '{entry.rarity.name}' was not found "
                                + "in the Game Database. No attributes will be generated."
                        );
                        return -1;
                    }

                    return rarityId;
                }
            }

            return -1;
        }

        protected virtual void InstantiateMoney(Vector3 position)
        {
            var level = m_entity ? m_entity.stats.level : 1;
            var baseAmount = Random.Range(stats.minMoneyAmount, stats.maxMoneyAmount);
            var multiplier = 1 + (level - 1) * m_game.enemyLootMoneyIncreaseRate;
            var finalAmount = Mathf.RoundToInt(baseAmount * multiplier);
            Level.instance.InstantiateMoneyDrop(finalAmount, position);
        }

        protected virtual Vector3 GetLootOrigin()
        {
            var random = Random.insideUnitCircle;
            var radius = Random.Range(stats.randomPositionMinRadius, stats.randomPositionMaxRadius);
            var randomOffset = new Vector3(random.x, 0, random.y) * radius;
            var position = transform.position + Vector3.up * k_lootRayOffset;

            if (stats.randomPosition)
                position += randomOffset;

            return position;
        }

        protected IEnumerator LootRoutine()
        {
            for (int i = 0; i <= stats.loopCount; i++)
            {
                yield return m_waitForLootLoopDelay;

                var origin = GetLootOrigin();

                if (
                    Physics.Raycast(
                        origin,
                        Vector3.down,
                        out var hit,
                        maxGroundDistance,
                        groundMask,
                        QueryTriggerInteraction.Ignore
                    )
                )
                {
                    var position = hit.point + Vector3.up * k_groundOffset;

                    if (Random.Range(0, 1f) > stats.moneyChance)
                    {
                        InstantiateMoney(position);
                        continue;
                    }

                    InstantiateItem(position);
                }
            }
        }

        protected virtual void Start()
        {
            InitializeWaits();
            InitializeEntity();
        }
    }
}
