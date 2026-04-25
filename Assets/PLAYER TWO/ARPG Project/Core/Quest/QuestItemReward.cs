using UnityEngine;

namespace PLAYERTWO.ARPGProject
{
    [System.Serializable]
    public class QuestItemReward
    {
        [Tooltip("The scriptable object of the Item.")]
        public Item data;

        [Tooltip(
            "The rarity to apply to the reward item. Leave empty for a plain item with no affixes."
        )]
        public ItemRarity rarity;

        /// <summary>
        /// Returns a new Item Instance based on this object's rarity.
        /// </summary>
        public ItemInstance CreateItemInstance()
        {
            if (rarity != null)
                return new ItemInstance(data, rarity);

            return new ItemInstance(data);
        }
    }
}
