using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace PLAYERTWO.ARPGProject
{
    [AddComponentMenu("PLAYER TWO/ARPG Project/Level/Level")]
    public class Level : Singleton<Level>
    {
        /// <summary>Flags that control which loot types the Level actively tracks and limits.</summary>
        [System.Flags]
        public enum LootTracking
        {
            None = 0,
            Items = 1 << 0,
            Money = 1 << 1,
        }

        [Tooltip(
            "The transform that represents the initial position and rotation of the Character."
        )]
        public Transform playerOrigin;

        [Header("Tracking Lists")]
        [Tooltip("The list of all entities in which the Level tracks.")]
        public Entity[] entities;

        [Tooltip("The list of all interactives objects in which the Level tracks.")]
        public Interactive[] interactives;

        [Tooltip("The list of all Game Objects in which the Level tracks.")]
        public GameObject[] gameObjects;

        [HideInInspector]
        [Tooltip("The list of all items dropped by entities in which the Level tracks.")]
        public List<CollectibleItem> droppedItems;

        [HideInInspector]
        [Tooltip("The list of all money dropped by entities in which the Level tracks.")]
        public List<CollectibleMoney> droppedMoney;

        [Header("Item Drop Settings")]
        [Tooltip("The Layer Mask of the ground to drop items.")]
        public LayerMask dropGroundLayer;

        [Tooltip(
            "Which loot types the Level tracks and counts toward the limit. Untrack a type to let it spawn without limit or FIFO eviction."
        )]
        public LootTracking trackLoot = LootTracking.Items | LootTracking.Money;

        [Tooltip(
            "Maximum number of tracked loot objects (items + money combined). When a new one is added beyond this limit, the oldest is destroyed."
        )]
        [Min(1)]
        public int lootLimit = 50;

        protected float? m_levelStartTime;
        protected List<Collectible> m_droppedLootOrder = new();

        /// <summary>
        /// Returns the Entity that represents the current player.
        /// </summary>
        public Entity player { get; protected set; }

        /// <summary>
        /// Returns the time since the level started, in seconds.
        /// </summary>
        public static float TimeSinceLevelStart
        {
            get
            {
                if (instance == null || instance.m_levelStartTime == null)
                    return 0;

                return Time.time - (float)instance.m_levelStartTime;
            }
        }

        public LevelQuests quests => LevelQuests.instance;
        public LevelWaypoints waypoints => LevelWaypoints.instance;
        public CharacterInstance currentCharacter => Game.instance.currentCharacter;
        public Scene currentScene => SceneManager.GetActiveScene();

        public Dictionary<string, Discoverable> discoverables { get; protected set; } = new();

        protected virtual void InitializePlayer()
        {
            if (Physics.Raycast(playerOrigin.position, Vector3.down, out var hit))
            {
                player = currentCharacter.Instantiate();

                if (
                    currentCharacter.currentScene.CompareTo(currentScene.name) == 0
                    && (
                        currentCharacter.initialPosition != Vector3.zero
                        || currentCharacter.initialRotation.eulerAngles != Vector3.zero
                    )
                )
                    player.Teleport(
                        currentCharacter.initialPosition,
                        currentCharacter.initialRotation
                    );
                else
                {
                    Game.instance.currentCharacter.currentScene = currentScene.name;

                    if (waypoints.IsValidWaypoint(currentCharacter.currentWaypoint))
                    {
                        waypoints.TeleportTo(currentCharacter.currentWaypoint);
                        currentCharacter.currentWaypoint = null;
                        return;
                    }

                    var position = hit.point + Vector3.up;
                    var rotation = playerOrigin.rotation;

                    player.Teleport(position, rotation);
                }
            }
        }

        protected virtual void RestoreState()
        {
            if (!currentCharacter.scenes.TryGetScene(currentScene.name, out var scene))
                return;

            for (int i = 0; i < scene.entities.Length; i++)
            {
                if (i >= entities.Length)
                    break;

                var position = scene.entities[i].position;
                var rotation = Quaternion.Euler(scene.entities[i].rotation);

                if (scene.entities[i].health == 0)
                    entities[i].gameObject.SetActive(false);
                else
                {
                    entities[i].stats.Initialize();
                    entities[i].stats.health = scene.entities[i].health;
                    entities[i].Teleport(position, rotation);
                }
            }

            for (int i = 0; i < scene.waypoints.Length; i++)
            {
                if (i >= waypoints.waypoints.Count)
                    break;

                if (waypoints.waypoints[i].title.CompareTo(scene.waypoints[i].title) != 0)
                    continue;

                waypoints.waypoints[i].active = scene.waypoints[i].active;
            }

            RestoreQuestItems(scene);
            RestoreGameObjects(scene);
            RestoreDroppedItems(scene);
            RestoreDiscoverables(scene);
            RestoreFogOfWar(scene);
        }

        protected virtual void RestoreQuestItems(CharacterScenes.Scene scene)
        {
            if (scene.interactives == null)
                return;

            for (int i = 0; i < scene.interactives.Length; i++)
            {
                if (interactives == null || i >= interactives.Length)
                    break;

                interactives[i].interactive = scene.interactives[i].interactive;
            }
        }

        protected virtual void RestoreGameObjects(CharacterScenes.Scene scene)
        {
            if (scene.gameObjects == null)
                return;

            for (int i = 0; i < scene.gameObjects.Length; i++)
            {
                if (gameObject == null || i >= gameObjects.Length)
                    break;

                gameObjects[i].transform.position = scene.gameObjects[i].position;
                gameObjects[i].transform.rotation = Quaternion.Euler(scene.gameObjects[i].rotation);
                gameObjects[i].SetActive(scene.gameObjects[i].active);
            }
        }

        protected virtual void RestoreDroppedItems(CharacterScenes.Scene scene)
        {
            droppedItems = new();
            droppedMoney = new();
            m_droppedLootOrder = new();

            if (scene.droppedItems != null)
            {
                foreach (var drop in scene.droppedItems)
                    InstantiateItemDrop(
                        drop.itemInstance,
                        drop.position,
                        Quaternion.Euler(drop.rotation)
                    );
            }

            if (scene.droppedMoney != null)
            {
                foreach (var drop in scene.droppedMoney)
                    InstantiateMoneyDrop(
                        drop.amount,
                        drop.position,
                        Quaternion.Euler(drop.rotation)
                    );
            }
        }

        protected virtual void RestoreFogOfWar(CharacterScenes.Scene scene)
        {
            if (scene.fogOfWarCells == null || !MapFogOfWar.instance)
                return;

            if (scene.fogOfWarCells.Length != MapFogOfWar.instance.cells.Length)
                return;

            for (int i = 0; i < scene.fogOfWarCells.Length; i++)
                MapFogOfWar.instance.cells[i] = (MapFogOfWar.FogState)scene.fogOfWarCells[i];
        }

        protected virtual void RestoreDiscoverables(CharacterScenes.Scene scene)
        {
            var instances = FindObjectsByType<Discoverable>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None
            );

            foreach (var instance in instances)
            {
                if (string.IsNullOrEmpty(instance.id))
                    continue;

                if (scene.discoverables?.ContainsKey(instance.id) == true)
                {
                    var state = scene.discoverables[instance.id].state;
                    instance.SetState((Discoverable.State)state);
                }

                discoverables.Add(instance.id, instance);
            }
        }

        public virtual bool TryInstantiateItemDropAtMousePosition(ItemInstance item)
        {
            if (player.inputs.MouseRaycast(out var hit, dropGroundLayer))
            {
                InstantiateItemDrop(item, hit.point);
                return true;
            }

            return false;
        }

        public virtual CollectibleItem InstantiateItemDrop(
            ItemInstance item,
            Vector3 position,
            Quaternion rotation = default
        )
        {
            var collectible = Instantiate(Game.instance.collectibleItemPrefab, position, rotation);
            collectible.SetItem(item);

            if ((trackLoot & LootTracking.Items) != 0)
            {
                EnforceLootLimit();
                collectible.onCollect.AddListener(() => RemoveFromDroppedItems(collectible));
                droppedItems.Add(collectible);
                m_droppedLootOrder.Add(collectible);
            }

            return collectible;
        }

        /// <summary>
        /// Instantiates a money drop at the given position and registers it with the Level's
        /// loot tracking if the <see cref="LootTracking.Money"/> flag is enabled.
        /// </summary>
        public virtual CollectibleMoney InstantiateMoneyDrop(
            int amount,
            Vector3 position,
            Quaternion rotation = default
        )
        {
            var money = Instantiate(Game.instance.collectibleMoneyPrefab, position, rotation);
            money.amount = amount;

            if ((trackLoot & LootTracking.Money) != 0)
            {
                EnforceLootLimit();
                money.onCollect.AddListener(() => RemoveFromDroppedMoney(money));
                droppedMoney.Add(money);
                m_droppedLootOrder.Add(money);
            }

            return money;
        }

        protected virtual void EnforceLootLimit()
        {
            while (m_droppedLootOrder.Count >= lootLimit)
            {
                var oldest = m_droppedLootOrder[0];
                m_droppedLootOrder.RemoveAt(0);

                if (oldest is CollectibleItem item)
                    droppedItems.Remove(item);
                else if (oldest is CollectibleMoney money)
                    droppedMoney.Remove(money);

                Destroy(oldest.gameObject);
            }
        }

        protected virtual void RemoveFromDroppedItems(CollectibleItem collectible)
        {
            droppedItems.Remove(collectible);
            m_droppedLootOrder.Remove(collectible);
        }

        protected virtual void RemoveFromDroppedMoney(CollectibleMoney money)
        {
            droppedMoney.Remove(money);
            m_droppedLootOrder.Remove(money);
        }

        /// <summary>
        /// Updates the scene data for the current Character.
        /// </summary>
        public virtual void UpdateSceneData() => currentCharacter.scenes.UpdateScene(instance);

        protected virtual void EvaluateQuestScene() =>
            Game.instance.quests.ReachedScene(currentScene.name);

        protected override void Initialize()
        {
            InitializePlayer();
            RestoreState();
        }

        protected virtual void Start()
        {
            EvaluateQuestScene();
            m_levelStartTime = Time.time;
        }
    }
}
