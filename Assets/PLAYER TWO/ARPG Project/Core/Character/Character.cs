using UnityEngine;

namespace PLAYERTWO.ARPGProject
{
    [CreateAssetMenu(fileName = "New Character", menuName = "PLAYER TWO/ARPG Project/Character/Character")]
    public class Character : ScriptableObject
    {
        [Header("General Settings")]
        [Tooltip("The prefab of the Entity that represents this Character in hte game world.")]
        public Entity entity;

        [Tooltip("The initial scene where this Character spawns.")]
        public string initialScene;

        [Header("Initial Stats")]
        [Tooltip("The initial level of the Character.")]
        public int level = 1;

        [Tooltip("The initial strength points of the Character.")]
        public int strength = 25;

        [Tooltip("The initial dexterity points of the Character.")]
        public int dexterity = 15;

        [Tooltip("The initial vitality points of the Character.")]
        public int vitality = 15;

        [Tooltip("The initial energy points of the Character.")]
        public int energy = 10;

        [Header("Initial Equipments")]
        [Tooltip("The initial Item equipped on the right hand slot.")]
        public CharacterItem rightHand;

        [Tooltip("The initial Item equipped on the left hand slot.")]
        public CharacterItem leftHand;

        [Tooltip("The initial Item equipped on the helm slot.")]
        public CharacterItem helm;

        [Tooltip("The initial Item equipped on the chest slot.")]
        public CharacterItem chest;

        [Tooltip("The initial Item equipped on the pants slot.")]
        public CharacterItem pants;

        [Tooltip("The initial Item equipped on the gloves slot.")]
        public CharacterItem gloves;

        [Tooltip("The initial Item equipped on the boots slot.")]
        public CharacterItem boots;

        [Header("Initial Consumables")]
        [Tooltip("The maximum amount of available consumable slots.")]
        public int maxConsumableSlots = 4;

        [Tooltip("A list of the initial consumables items equipped.")]
        public ItemConsumable[] initialConsumables;

        [Header("Initial Inventory")]
        [Tooltip("The initial amount of money available on the Inventory when the Character is created.")]
        public int initialMoney;

        [Tooltip("The initial items on the Inventory when the Character is created.")]
        public CharacterInventoryItem[] inventory;

        [Header("Initial Skills")]
        [Tooltip("The initial list of available Skills.")]
        public Skill[] availableSkills;

        [Tooltip("The initial list of equipped Skills.")]
        public Skill[] equippedSkills;
    }
}
