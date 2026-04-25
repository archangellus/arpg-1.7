using UnityEngine;

namespace PLAYERTWO.ARPGProject
{
    [AddComponentMenu("PLAYER TWO/ARPG Project/GUI/GUI Blacksmith Slot")]
    public class GUIBlacksmithSlot : GUIItemSlot
    {
        public override bool CanEquip(GUIItem item) =>
            !this.item && item && item.item.IsEquippable();

        public override bool CanUnequip() => true;

        protected override void HandleRightClick()
        {
            if (item && item.TryMoveToLastPosition())
                Unequip();
        }
    }
}
