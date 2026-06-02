using System;
using System.Collections;
using System.Collections.Generic;
using PLAYERTWO.ARPGProject;
using UnityEngine;
using Random = UnityEngine.Random;

namespace ShatterStone
{
    /// <summary>
    /// Ore-node style mining component that drops PLAYER TWO ItemMaterial items through ItemLootStats
    /// instead of spawning a manually assigned refined pickup prefab.
    /// </summary>
    public class MaterialOreNode : MonoBehaviour, IMineableNode
    {
        protected struct NodeDropBounds
        {
            public float minX, maxX, minZ, maxZ, centerY;
            public bool valid;

            public NodeDropBounds(float minX, float maxX, float minZ, float maxZ, float centerY, bool valid)
            {
                this.minX = minX;
                this.maxX = maxX;
                this.minZ = minZ;
                this.maxZ = maxZ;
                this.centerY = centerY;
                this.valid = valid;
            }
        }

        #region Serialized Fields

        [Header("Drop Settings")]
        [Tooltip("Broken/shattered visual prefab spawned when this node is depleted.")]
        [SerializeField] private GameObject pieces;

        [Tooltip("The Item Loot Stats asset used for material drops. Its Items list should contain ItemMaterial assets.")]
        [SerializeField] private ItemLootStats stats;

        [Tooltip("How many loot batches are rolled per mining hit. Each batch uses stats.lootChance and stats.loopCount.")]
        [SerializeField, Min(0)] private int dropOnHit = 1;

        [Tooltip("How many hits are required to deplete this node.")]
        [SerializeField, Min(1)] private int hitsToDestroy = 3;

        [Tooltip("If true, this node only drops ItemMaterial entries from stats.items.")]
        [SerializeField] private bool materialsOnly = true;

        [Tooltip("If false, this node ignores stats.moneyChance and always tries to drop material items.")]
        [SerializeField] private bool allowMoneyDrops = false;

        [Header("Ground Settings")]
        [Tooltip("The ground layer mask. The loot will not spawn if the ground below the loot point is not in this layer.")]
        [SerializeField] private LayerMask groundMask = -5;

        [Tooltip("The maximum distance to the ground. If the ground is further than this from the loot point, nothing spawns.")]
        [SerializeField] private float maxGroundDistance = 2f;

        [Header("Knockback Settings")]
        [SerializeField] private Vector3 knockAngle;
        [SerializeField] private AnimationCurve knockCurve;
        [SerializeField] private float knockDuration = 1f;

        [Header("Respawn Settings")]
        [SerializeField] private bool enableRespawn = true;
        [SerializeField] private float respawnDelay = 30f;

        [Header("Configuration")]
        [SerializeField] private bool cacheVisualBoundaries = true;
        [SerializeField] private MiningNodeAudio nodeAudio;
        [SerializeField] private Collider nodeCollider;
        [SerializeField] private Renderer[] childRenderers;

        [Header("Debug")]
        [SerializeField] private bool enableDebugLogs = false;

        #endregion

        private const float GroundOffset = 0.1f;
        private const float LootRayOffset = 0.5f;
        private const float LootLoopDelay = 0.1f;
        private const float DelayDestroySeconds = 5f;

        private readonly List<Item> lootItems = new List<Item>();
        private WaitForSeconds waitForLootLoopDelay;
        private NodeDropBounds nodeBounds;
        private ItemLootStats cachedStats;
        private int hitIndex;
        private bool depleted;


        protected virtual void Start()
        {
            waitForLootLoopDelay = new WaitForSeconds(LootLoopDelay);

            if (nodeAudio == null)
                nodeAudio = GetComponent<MiningNodeAudio>();

            if (nodeCollider == null)
                nodeCollider = GetComponent<Collider>();

            if (childRenderers == null || childRenderers.Length == 0)
                childRenderers = GetComponentsInChildren<Renderer>();

            RebuildLootItemsCache();
        }

        public virtual void Interact() => Interact(1);

        public virtual void Interact(int hits)
        {
            if (depleted)
                return;

            hits = Mathf.Max(1, hits);

            if (ShouldCalculateNodeBounds())
                nodeBounds = CalculateNodeBounds();

            int appliedHits = 0;

            for (int i = 0; i < hits && hitIndex < hitsToDestroy; i++)
            {
                hitIndex++;
                appliedHits++;

                if (dropOnHit > 0)
                    StartCoroutine(LootRoutine(dropOnHit));
            }

            if (appliedHits <= 0)
                return;

            if (hitIndex < hitsToDestroy)
            {
                StartCoroutine(Animate());
                nodeAudio?.PlayImpactSound();
            }
            else
            {
                depleted = true;
                ReplaceNodeVisualsWithBrokenOne();
            }
        }

        [Obsolete("Use Interact(hits) instead")]
        public void oreHit() => Interact(1);

        protected virtual bool ShouldCalculateNodeBounds()
        {
            return !cacheVisualBoundaries || hitIndex == 0 || !nodeBounds.valid;
        }

        protected virtual NodeDropBounds CalculateNodeBounds()
        {
            Renderer renderer = TryGetComponent(out MeshRenderer meshRenderer)
                ? meshRenderer
                : GetComponentInChildren<Renderer>();

            if (renderer == null)
            {
                Vector3 position = transform.position;
                return new NodeDropBounds(position.x, position.x, position.z, position.z, position.y, true);
            }

            Bounds bounds = renderer.bounds;
            return new NodeDropBounds(bounds.min.x, bounds.max.x, bounds.min.z, bounds.max.z, bounds.center.y, true);
        }

        protected virtual IEnumerator LootRoutine(int lootBatches)
        {
            if (stats == null)
            {
                LogWarning("No ItemLootStats assigned, so no material drops can be spawned.");
                yield break;
            }

            if (cachedStats != stats)
                RebuildLootItemsCache();

            for (int batch = 0; batch < lootBatches; batch++)
            {
                if (Random.value > stats.lootChance)
                    continue;

                int loops = Mathf.Max(0, stats.loopCount);

                for (int i = 0; i <= loops; i++)
                {
                    yield return waitForLootLoopDelay;

                    if (!TryGetLootPosition(out Vector3 position))
                        continue;

                    if (allowMoneyDrops && Random.value <= stats.moneyChance)
                        InstantiateMoney(position);
                    else
                        InstantiateItem(position);
                }
            }
        }

        protected virtual bool TryGetLootPosition(out Vector3 position)
        {
            Vector3 origin = GetLootOrigin();

            if (Physics.Raycast(
                    origin,
                    Vector3.down,
                    out RaycastHit hit,
                    maxGroundDistance,
                    groundMask,
                    QueryTriggerInteraction.Ignore))
            {
                position = hit.point + Vector3.up * GroundOffset;
                return true;
            }

            position = Vector3.zero;
            return false;
        }

        protected virtual Vector3 GetLootOrigin()
        {
            if (!nodeBounds.valid)
                nodeBounds = CalculateNodeBounds();

            Vector3 origin = new Vector3(
                Random.Range(nodeBounds.minX, nodeBounds.maxX),
                nodeBounds.centerY + LootRayOffset,
                Random.Range(nodeBounds.minZ, nodeBounds.maxZ)
            );

            if (stats != null && stats.randomPosition)
            {
                Vector2 random = Random.insideUnitCircle.normalized;
                float minRadius = Mathf.Max(0f, stats.randomPositionMinRadius);
                float maxRadius = Mathf.Max(minRadius, stats.randomPositionMaxRadius);
                float radius = Random.Range(minRadius, maxRadius);
                origin += new Vector3(random.x, 0f, random.y) * radius;
            }

            return origin;
        }

        protected virtual void InstantiateItem(Vector3 position)
        {
            if (lootItems.Count == 0)
            {
                LogWarning(materialsOnly
                    ? "ItemLootStats has no ItemMaterial entries in its Items list."
                    : "ItemLootStats has no valid item entries in its Items list.");
                return;
            }

            Item itemData = lootItems[Random.Range(0, lootItems.Count)];
            int rolledRarity = RollRarity();

            ItemInstance item = rolledRarity >= 0
                ? new ItemInstance(itemData, rolledRarity)
                : new ItemInstance(itemData);

            if (Level.instance == null)
            {
                LogWarning("Level.instance is null, so ItemDrop cannot be spawned.");
                return;
            }

            Level.instance.InstantiateItemDrop(item, position);
            Log($"Spawned material item '{itemData.name}' at {position}.");
        }

        protected virtual void InstantiateMoney(Vector3 position)
        {
            int finalAmount = Random.Range(stats.minMoneyAmount, stats.maxMoneyAmount);

            if (Level.instance == null)
            {
                LogWarning("Level.instance is null, so MoneyDrop cannot be spawned.");
                return;
            }

            Level.instance.InstantiateMoneyDrop(finalAmount, position);
        }

        protected virtual int RollRarity()
        {
            if (stats == null || stats.rarityLevels == null || stats.rarityLevels.Count == 0)
                return -1;

            GameDatabase db = GameDatabase.instance;

            if (db == null)
                return -1;

            foreach (ItemLootStats.RarityChance entry in stats.rarityLevels)
            {
                if (Random.value > entry.chance)
                    continue;

                if (entry.rarity == null)
                {
                    LogWarning("A rarity entry has no ItemRarity assigned. The item will drop without rarity attributes.");
                    return -1;
                }

                int rarityId = db.itemRarities.IndexOf(entry.rarity);

                if (rarityId < 0)
                {
                    LogWarning($"ItemRarity '{entry.rarity.name}' was not found in the Game Database. The item will drop without rarity attributes.");
                    return -1;
                }

                return rarityId;
            }

            return -1;
        }

        protected virtual void RebuildLootItemsCache()
        {
            cachedStats = stats;
            lootItems.Clear();

            if (stats == null || stats.items == null)
                return;

            foreach (Item item in stats.items)
            {
                if (item == null)
                    continue;

                if (materialsOnly && !(item is ItemMaterial))
                    continue;

                lootItems.Add(item);
            }
        }

        protected virtual void ReplaceNodeVisualsWithBrokenOne()
        {
            if (pieces != null)
            {
                GameObject brokenPieces = Instantiate(pieces, transform.position, transform.rotation);
                brokenPieces.transform.localScale = transform.localScale;
            }

            if (nodeCollider)
                nodeCollider.enabled = false;

            foreach (Renderer renderer in childRenderers)
            {
                if (renderer)
                    renderer.enabled = false;
            }

            nodeAudio?.PlayShatterSound();

            if (enableRespawn)
                ResetNode(respawnDelay);
            else
                StartCoroutine(DelayDestroy());
        }

        protected virtual IEnumerator Animate()
        {
            if (nodeCollider)
                nodeCollider.enabled = false;

            Quaternion originalRotation = transform.localRotation;
            Quaternion knockRotation = Quaternion.Euler(knockAngle);

            float t = 0f;

            while (t < knockDuration)
            {
                float v = knockCurve != null ? knockCurve.Evaluate(t / knockDuration) : t / knockDuration;
                transform.localRotation = originalRotation * Quaternion.Slerp(Quaternion.identity, knockRotation, v);
                t += Time.deltaTime;
                yield return null;
            }

            transform.localRotation = originalRotation;

            if (!depleted && nodeCollider)
                nodeCollider.enabled = true;
        }

        public virtual void ResetNode(float delay) => StartCoroutine(ResetAsync(delay));

        public virtual IEnumerator ResetAsync(float delay)
        {
            yield return new WaitForSeconds(delay);
            RevertToInitialState();
        }

        protected virtual void RevertToInitialState()
        {
            hitIndex = 0;
            depleted = false;
            nodeBounds = CalculateNodeBounds();

            if (nodeCollider)
                nodeCollider.enabled = true;

            foreach (Renderer renderer in childRenderers)
            {
                if (renderer)
                    renderer.enabled = true;
            }
        }

        protected virtual IEnumerator DelayDestroy()
        {
            yield return new WaitForSeconds(DelayDestroySeconds);
            Destroy(gameObject);
        }

        private void Log(string message)
        {
            if (enableDebugLogs)
                Debug.Log("[MaterialOreNode] " + message, this);
        }

        private void LogWarning(string message)
        {
            if (enableDebugLogs)
                Debug.LogWarning("[MaterialOreNode] " + message, this);
        }
    }
}
