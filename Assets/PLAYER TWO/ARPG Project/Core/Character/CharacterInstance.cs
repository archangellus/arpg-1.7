using UnityEngine;

namespace PLAYERTWO.ARPGProject
{
    public class CharacterInstance
    {
        public Character data;

        public string name;
        public string currentScene;
        public string currentWaypoint;

        public Vector3 initialPosition;
        public Quaternion initialRotation;

        public CharacterStats stats;
        public CharacterEquipments equipments;
        public CharacterInventory inventory;
        public CharacterSkills skills;
        public CharacterQuests quests;
        public CharacterScenes scenes;

        public Entity entity { get; protected set; }

        public Vector3 currentPosition =>
            entity && entity.enabled ? entity.position : initialPosition;

        public Quaternion currentRotation =>
            entity && entity.enabled ? entity.transform.rotation : initialRotation;

        public CharacterInstance() { }

        public CharacterInstance(Character data, string name)
        {
            this.data = data;
            this.name = name;
            currentScene = data.initialScene;
            stats = new CharacterStats(data);
            equipments = new CharacterEquipments(data);
            inventory = new CharacterInventory(data);
            skills = new CharacterSkills(data);
            quests = new CharacterQuests();
            scenes = new CharacterScenes();
        }

        /// <summary>
        /// Instantiates a new Entity from this Character Instance data.
        /// </summary>
        public virtual Entity Instantiate()
        {
            if (entity == null)
            {
                entity = Object.Instantiate(data.entity);
                stats.InitializeStats(entity.stats);
                equipments.InitializeEquipments(entity.items);
                inventory.InitializeInventory(entity.inventory);
                skills.InitializeSkills(entity.skills);
                quests.InitializeQuests();
                scenes.InitializeScenes();
            }

            return entity;
        }

        public static CharacterInstance CreateFromSerializer(CharacterSerializer serializer)
        {
            var data = GameDatabase.instance.FindElementById<Character>(serializer.characterId);

            return new CharacterInstance()
            {
                data = data,
                name = serializer.name,
                currentScene = serializer.scene,
                currentWaypoint = serializer.waypoint,
                initialPosition = serializer.position.ToUnity(),
                initialRotation = Quaternion.Euler(serializer.rotation.ToUnity()),
                stats = CharacterStats.CreateFromSerializer(serializer.stats),
                equipments = CharacterEquipments.CreateFromSerializer(serializer.equipments),
                inventory = CharacterInventory.CreateFromSerializer(serializer.inventory),
                skills = CharacterSkills.CreateFromSerializer(serializer.skills),
                quests = CharacterQuests.CreateFromSerializer(serializer.quests),
                scenes = CharacterScenes.CreateFromSerializer(serializer.scenes),
            };
        }
    }
}
