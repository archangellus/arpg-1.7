using System.Collections.Generic;
using UnityEngine;

namespace PLAYERTWO.ARPGProject
{
    [CreateAssetMenu(
        fileName = "New Item Loot Stats",
        menuName = "PLAYER TWO/ARPG Project/Item/Item Loot Stats"
    )]
    public class ItemLootStats : ScriptableObject
    {
        [System.Serializable]
        public class RarityChance
        {
            [Tooltip("The rarity to apply to this loot drop.")]
            public ItemRarity rarity;

            [Range(0, 1)]
            [Tooltip(
                "The chance for this rarity level to be selected. "
                    + "Entries are evaluated in order; the first roll that passes wins."
            )]
            public float chance;
        }

        [Header("Loot Settings")]
        [Range(0, 1)]
        [Tooltip("The chance of looting anything.")]
        public float lootChance = 0.5f;

        [Tooltip("The amount of times the loot will repeat.")]
        public int loopCount;

        [Header("Position Settings")]
        [Tooltip("If true, the loot will be instantiated in a random position.")]
        public bool randomPosition;

        [Tooltip("The maximum distance from the loot center to instantiate the loot.")]
        public float randomPositionMaxRadius = 3f;

        [Tooltip("The minimum distance from the loot center to instantiate the loot.")]
        public float randomPositionMinRadius = 1.5f;

        [Header("Rarity Settings")]
        [Tooltip(
            "List of possible rarity levels and their drop chances. "
                + "Evaluated in order; the first roll that passes determines the item's rarity. "
                + "If no entry passes, the item drops with no affixes."
        )]
        public List<RarityChance> rarityLevels;

        [Space(10)]
        [Tooltip("A list of items that can be looted.")]
        public Item[] items;

        [Header("Money Settings")]
        [Range(0, 1)]
        [Tooltip("The chance of looting money instead of items.")]
        public float moneyChance = 0.5f;

        [Tooltip("The minimum amount of money that can be looted.")]
        public int minMoneyAmount = 500;

        [Tooltip("The maximum amount of money that can be looted.")]
        public int maxMoneyAmount = 2500;
    }
}
