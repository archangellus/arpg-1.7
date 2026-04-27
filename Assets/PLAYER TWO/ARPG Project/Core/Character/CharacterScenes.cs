using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace PLAYERTWO.ARPGProject
{
    public class CharacterScenes
    {
        public class Scene
        {
            public string name;
            public Entity[] entities;
            public Waypoint[] waypoints;
            public Interactive[] interactives;
            public GameObject[] gameObjects;
            public DroppedItem[] droppedItems;
            public Dictionary<string, Discoverable> discoverables;
            public byte[] fogOfWarCells;
        }

        public class Entity
        {
            public Vector3 position;
            public Vector3 rotation;
            public int health;
        }

        public class Waypoint
        {
            public string title;
            public bool active;
        }

        public class Interactive
        {
            public bool interactive;
        }

        public class GameObject
        {
            public bool active;
            public Vector3 position;
            public Vector3 rotation;
        }

        public class DroppedItem
        {
            public Vector3 position;
            public Vector3 rotation;
            public ItemInstance itemInstance;
        }

        public class Discoverable
        {
            public string id;
            public int state;
        }

        public List<Scene> scenes;

        public virtual void InitializeScenes()
        {
            if (scenes != null)
                return;

            scenes = new List<Scene>();
        }

        /// <summary>
        /// Updates all scene data from a given Level.
        /// </summary>
        /// <param name="level">The Level you want to update the scene data from.</param>
        public virtual void UpdateScene(Level level)
        {
            if (!level)
                return;

            var name = level.currentScene.name;

            var entities = level
                .entities.Select(e => new Entity()
                {
                    position = e.position,
                    rotation = e.transform.rotation.eulerAngles,
                    health = e.stats.health,
                })
                .ToArray();

            var waypoints = level
                .waypoints.waypoints.Select(w => new Waypoint()
                {
                    title = w.title,
                    active = w.active,
                })
                .ToArray();

            var interactives = GetInteractives(level);
            var gameObjects = GetGameObjects(level);
            var droppedItems = GetDroppedItems(level);
            var discoverables = GetDiscoverables(level);
            var fogOfWarCells = GetFogOfWarCells();

            if (!scenes.Exists(scene => scene.name == name))
                scenes.Add(new Scene() { name = name });

            var scene = scenes.Find(scene => scene.name == name);

            scene.entities = entities;
            scene.waypoints = waypoints;
            scene.interactives = interactives;
            scene.gameObjects = gameObjects;
            scene.droppedItems = droppedItems;
            scene.discoverables = discoverables;
            scene.fogOfWarCells = fogOfWarCells;
        }

        /// <summary>
        /// Tries to get a Scene bu its name.
        /// </summary>
        /// <param name="name">The name of the scene you want to get.</param>
        /// <param name="scene">The instance of the Scene you want to find.</param>
        /// <returns>Returns true if it was able to get the scene.</returns>
        public virtual bool TryGetScene(string name, out Scene scene)
        {
            scene = scenes.Find(scene => scene.name == name);
            return scene != null;
        }

        protected virtual Interactive[] GetInteractives(Level level)
        {
            return level
                .interactives?.Select(i => new Interactive() { interactive = i.interactive })
                .ToArray();
        }

        protected virtual GameObject[] GetGameObjects(Level level)
        {
            return level
                .gameObjects?.Select(g => new GameObject()
                {
                    position = g.transform.position,
                    rotation = g.transform.rotation.eulerAngles,
                    active = g.activeSelf,
                })
                .ToArray();
        }

        protected virtual DroppedItem[] GetDroppedItems(Level level)
        {
            return level
                .droppedItems.Select(item => new DroppedItem()
                {
                    position = item.transform.position,
                    rotation = item.transform.rotation.eulerAngles,
                    itemInstance = item.item,
                })
                .ToArray();
        }

        protected virtual byte[] GetFogOfWarCells()
        {
            if (!MapFogOfWar.instance)
                return null;

            return System.Array.ConvertAll(MapFogOfWar.instance.cells, c => (byte)c);
        }

        protected virtual Dictionary<string, Discoverable> GetDiscoverables(Level level)
        {
            return level.discoverables.ToDictionary(
                d => d.Key,
                d => new Discoverable() { id = d.Value.id, state = (int)d.Value.state }
            );
        }

        public static CharacterScenes CreateFromSerializer(ScenesSerializer serializer)
        {
            if (serializer == null)
                return new CharacterScenes();

            return new CharacterScenes()
            {
                scenes = serializer
                    .scenes.Select(scene =>
                    {
                        if (!GameScenes.IsSceneValid(scene.name))
                        {
                            Debug.LogWarning(
                                $"Scene '{scene.name}' won't be loaded from the "
                                    + "save file because it was not found in build settings."
                            );
                            return null;
                        }

                        return new Scene()
                        {
                            name = scene.name,
                            entities = scene
                                .entities.Select(e => new Entity()
                                {
                                    position = e.position.ToUnity(),
                                    rotation = e.rotation.ToUnity(),
                                    health = e.health,
                                })
                                .ToArray(),
                            waypoints = scene
                                .waypoints.Select(w => new Waypoint()
                                {
                                    title = w.title,
                                    active = w.active,
                                })
                                .ToArray(),
                            interactives = scene
                                .interactives?.Select(i => new Interactive()
                                {
                                    interactive = i.interactive,
                                })
                                .ToArray(),
                            gameObjects = scene
                                .gameObjects?.Select(g => new GameObject()
                                {
                                    position = g.position.ToUnity(),
                                    rotation = g.rotation.ToUnity(),
                                    active = g.active,
                                })
                                .ToArray(),
                            droppedItems = scene
                                .droppedItems?.Select(i => new DroppedItem()
                                {
                                    position = i.position.ToUnity(),
                                    rotation = i.rotation.ToUnity(),
                                    itemInstance = ItemInstance.CreateFromSerializer(
                                        i.itemInstance
                                    ),
                                })
                                .Where(item => item != null)
                                .ToArray(),
                            discoverables = scene.discoverables?.ToDictionary(
                                d => d.id,
                                d => new Discoverable() { id = d.id, state = d.state }
                            ),
                            fogOfWarCells = scene.fogOfWarCells,
                        };
                    })
                    .Where(scene => scene != null)
                    .ToList(),
            };
        }
    }
}
