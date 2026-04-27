using UnityEngine;

namespace PLAYERTWO.ARPGProject
{
    [System.Serializable]
    public class QuestItemReward
    {
        [Tooltip("The scriptable object of the Item.")]
        public Item data;

        [Tooltip("The amount of additional attributes the Item will have.")]
        public int attributes;

        /// <summary>
        /// Returns a new Item Instance based on this object's attributes.
        /// </summary>
        public ItemInstance CreateItemInstance()
        {
            if (attributes > 0)
                return new ItemInstance(data, true, attributes, attributes);

            return new ItemInstance(data, false);
        }
    }
}
