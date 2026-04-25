using UnityEngine;

namespace PLAYERTWO.ARPGProject
{
    public abstract class ItemEquippable : Item
    {
        [Header("Equipment Settings")]
        [Tooltip("The maximum durability points of this Item.")]
        public int maxDurability;

        [Tooltip("The minimum required level to equip this Item.")]
        public int requiredLevel;

        [Tooltip("The minimum required strength to equip this Item.")]
        public int requiredStrength;

        [Tooltip("The minimum required dexterity to equip this Item.")]
        public int requiredDexterity;

        /// <summary>
        /// Returns true if the Entity meets the required attributes to equip this Item.
        /// </summary>
        /// <param name="entity">The Entity whose stats are checked against the requirements.</param>
        public virtual bool MeetsRequirements(Entity entity)
        {
            return entity.stats.level >= requiredLevel
                && entity.stats.strength >= requiredStrength
                && entity.stats.dexterity >= requiredDexterity;
        }

        /// <summary>
        /// Returns true if this Item can be equipped in the given slot,
        /// considering special conditions such as dual wield and shields.
        /// </summary>
        /// <param name="slot">The slot in which you want to equip this Item.</param>
        /// <param name="items">The Entity Item Manager used to check current equipment state.</param>
        public abstract bool CanEquipInSlot(ItemSlots slot, EntityItemManager items);

        /// <summary>
        /// Instantiates this Item's prefab using the appropriate slot transforms from the given
        /// Entity Item Manager. Returns null by default; override in weapon and shield subclasses.
        /// </summary>
        /// <param name="slot">The slot in which this Item is being equipped.</param>
        /// <param name="items">The Entity Item Manager that provides the slot transforms.</param>
        public virtual GameObject InstantiateOn(ItemSlots slot, EntityItemManager items) => null;
    }
}
