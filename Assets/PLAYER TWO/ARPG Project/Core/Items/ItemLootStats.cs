using UnityEngine;

namespace PLAYERTWO.ARPGProject
{
    [CreateAssetMenu(
        fileName = "New Item Loot Stats",
        menuName = "PLAYER TWO/ARPG Project/Item/Item Loot Stats"
    )]
    public class ItemLootStats : ScriptableObject
    {
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

        [Header("Attribute Settings")]
        [Tooltip(
            "If true, the Loot System will generate additional attributes for the items looted."
        )]
        public bool generateAttributes = true;

        [Tooltip("The minimum amount of attributes.")]
        public int minAttributes;

        [Tooltip("The maximum amount of attributes.")]
        public int maxAttributes;

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
