using UnityEngine;

namespace PLAYERTWO.ARPGProject
{
    [CreateAssetMenu(fileName = "New Ring", menuName = "PLAYER TWO/ARPG Project/Item/Ring")]
    public class ItemRing : ItemEquippable
    {
        /// <inheritdoc/>
        public override bool CanEquipInSlot(ItemSlots slot, EntityItemManager items) =>
            slot == ItemSlots.RingA || slot == ItemSlots.RingB;
    }
}
