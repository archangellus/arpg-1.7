using UnityEngine;

namespace PLAYERTWO.ARPGProject
{
    [CreateAssetMenu(fileName = "New Amulet", menuName = "PLAYER TWO/ARPG Project/Item/Amulet")]
    public class ItemAmulet : ItemEquippable
    {
        /// <inheritdoc/>
        public override bool CanEquipInSlot(ItemSlots slot, EntityItemManager items) =>
            slot == ItemSlots.Amulet;
    }
}
