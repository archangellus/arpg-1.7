using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace PLAYERTWO.ARPGProject
{
    [AddComponentMenu("PLAYER TWO/ARPG Project/Level/Level")]
    public class Level : Singleton<Level>
    {
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

        [Header("Item Drop Settings")]
        [Tooltip("The Layer Mask of the ground to drop items.")]
        public LayerMask dropGroundLayer;

        protected float? m_levelStartTime;

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
            if (scene.droppedItems == null)
                return;

            droppedItems = new();

            foreach (var drop in scene.droppedItems)
            {
                InstantiateItemDrop(
                    drop.itemInstance,
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
            collectible.onCollect.AddListener(() => droppedItems.Remove(collectible));
            collectible.SetItem(item);
            droppedItems.Add(collectible);
            return collectible;
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
