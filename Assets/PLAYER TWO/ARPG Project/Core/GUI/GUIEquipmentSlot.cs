using UnityEngine;

namespace PLAYERTWO.ARPGProject
{
    [AddComponentMenu("PLAYER TWO/ARPG Project/GUI/GUI Equipment Slot")]
    public class GUIEquipmentSlot : GUIItemSlot
    {
        [Header("Equipment Settings")]
        public ItemSlots slot;

        protected Entity m_entity => Level.instance.player;
        protected GUIWindowsManager m_windowsManager => GUIWindowsManager.instance;
        protected GUIInventory m_inventory => m_windowsManager.GetInventory();
        protected GUIBlacksmith m_blacksmith => m_windowsManager.blacksmith;

        protected override void HandleRightClick()
        {
            if (!item || !CanUnequip())
                return;

            if (m_blacksmith.isOpen && m_blacksmith.slot.CanEquip(item))
            {
                var guiItem = item;
                Unequip();
                m_blacksmith.slot.Equip(guiItem);
            }
            else if (m_inventory.TryAutoInsert(item))
            {
                Unequip();
            }
        }

        public override bool CanEquip(GUIItem item)
        {
            if (!item || (base.item && (!CanUnequip() || !m_inventory.CanAutoInsert(base.item))))
                return false;

            return m_entity.items.CanEquip(item.item, slot);
        }

        public override bool CanUnequip()
        {
            if (slot != ItemSlots.RightHand)
                return true;

            return !m_entity.items.IsUsingWeaponLeft();
        }

        public override void Equip(GUIItem item)
        {
            if (item && (!base.item || m_inventory.TryAutoInsert(base.item)))
            {
                Unequip();
                m_entity.items.TryEquip(item.item, slot);
                base.Equip(item);
            }
        }

        public override void Unequip()
        {
            if (!item)
                return;

            m_entity.items.RemoveItem(slot);
            base.Unequip();
        }
    }
}
