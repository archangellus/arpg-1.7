using System.Collections.Generic;
using UnityEngine;

namespace PLAYERTWO.ARPGProject
{
    [CreateAssetMenu(
        fileName = "New Game Data",
        menuName = "PLAYER TWO/ARPG Project/Game/Game Data"
    )]
    public class GameData : ScriptableObject
    {
        [Tooltip("The list of all available Characters.")]
        public List<Character> characters;

        [Tooltip("The list of all available Items.")]
        public List<Item> items;

        [Tooltip("The list of all available Skills.")]
        public List<Skill> skills;

        [Tooltip("The list of all available Quests.")]
        public List<Quest> quests;

        [Tooltip("The list of item rarity tiers, ordered from lowest to highest rarity.")]
        public List<ItemRarity> itemRarities;

        [Header("Status Effects")]
        [Tooltip("The entity effect applied to a target when a Bleed chance triggers on hit.")]
        public EntityEffect bleedEffect;

        [Tooltip("The entity effect applied to a target when a Burn chance triggers on hit.")]
        public EntityEffect burnEffect;

        [Tooltip("The entity effect applied to a target when a Freeze chance triggers on hit.")]
        public EntityEffect freezeEffect;

        [Tooltip("The entity effect applied to a target when a Poison chance triggers on hit.")]
        public EntityEffect poisonEffect;
    }
}
