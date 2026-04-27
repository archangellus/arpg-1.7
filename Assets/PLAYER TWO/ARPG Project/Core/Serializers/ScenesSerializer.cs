using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace PLAYERTWO.ARPGProject
{
    [System.Serializable]
    public class ScenesSerializer
    {
        [System.Serializable]
        public class Scene
        {
            public string name;
            public Entity[] entities;
            public Waypoint[] waypoints;
            public Interactive[] interactives;
            public GameObject[] gameObjects;
            public DroppedItem[] droppedItems;
            public DroppedMoney[] droppedMoney;
            public Discoverable[] discoverables;
            public byte[] fogOfWarCells;
        }

        [System.Serializable]
        public class Entity
        {
            public UnitySerializer.Vector3 position;
            public UnitySerializer.Vector3 rotation;
            public int health;
        }

        [System.Serializable]
        public class Waypoint
        {
            public string title;
            public bool active;
        }

        [System.Serializable]
        public class Interactive
        {
            public bool interactive;
        }

        [System.Serializable]
        public class GameObject
        {
            public bool active;
            public UnitySerializer.Vector3 position;
            public UnitySerializer.Vector3 rotation;
        }

        [System.Serializable]
        public class DroppedItem
        {
            public UnitySerializer.Vector3 position;
            public UnitySerializer.Vector3 rotation;
            public ItemSerializer itemInstance;
        }

        [System.Serializable]
        public class DroppedMoney
        {
            public UnitySerializer.Vector3 position;
            public UnitySerializer.Vector3 rotation;
            public int amount;
        }

        [System.Serializable]
        public class Discoverable
        {
            public string id;
            public int state;
        }

        public List<Scene> scenes;

        public ScenesSerializer(CharacterScenes scenes)
        {
            this.scenes = new List<Scene>();

            if (scenes.scenes == null)
                return;

            foreach (var scene in scenes.scenes)
            {
                this.scenes.Add(
                    new Scene()
                    {
                        name = scene.name,
                        entities = scene
                            .entities.Select(e => new Entity()
                            {
                                position = new UnitySerializer.Vector3(e.position),
                                rotation = new UnitySerializer.Vector3(e.rotation),
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
                                position = new UnitySerializer.Vector3(g.position),
                                rotation = new UnitySerializer.Vector3(g.rotation),
                                active = g.active,
                            })
                            .ToArray(),
                        droppedItems = scene
                            .droppedItems?.Select(i => new DroppedItem()
                            {
                                position = new UnitySerializer.Vector3(i.position),
                                rotation = new UnitySerializer.Vector3(i.rotation),
                                itemInstance = new ItemSerializer(i.itemInstance),
                            })
                            .ToArray(),
                        droppedMoney = scene
                            .droppedMoney?.Select(m => new DroppedMoney()
                            {
                                position = new UnitySerializer.Vector3(m.position),
                                rotation = new UnitySerializer.Vector3(m.rotation),
                                amount = m.amount,
                            })
                            .ToArray(),
                        discoverables = scene
                            .discoverables?.Values.Select(d => new Discoverable()
                            {
                                id = d.id,
                                state = d.state,
                            })
                            .ToArray(),
                        fogOfWarCells = scene.fogOfWarCells,
                    }
                );
            }
        }

        public virtual string ToJson() => JsonUtility.ToJson(this);

        public static ScenesSerializer FromJson(string json) =>
            JsonUtility.FromJson<ScenesSerializer>(json);
    }
}
