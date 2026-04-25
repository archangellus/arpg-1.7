using UnityEngine;

namespace PLAYERTWO.ARPGProject
{
    [AddComponentMenu("PLAYER TWO/ARPG Project/GUI/GUI Consumable Slot")]
    public class GUIConsumableSlot : GUIItemSlot
    {
        protected Entity m_entity => Level.instance.player;

        protected override void HandleRightClick()
        {
            if (!item)
                return;

#if UNITY_STANDALONE || UNITY_WEBGL
            m_entity.inventory.instance.TryAddOrStack(item.item);
            Unequip();
            Destroy(m_tempItem.gameObject);
#else
            m_entity.items.ConsumeItem(item.item);
#endif
        }

        public override bool CanEquip(GUIItem item) => !base.item && item.item.IsConsumable();

        public override bool CanUnequip() => true;

        public virtual void Clear()
        {
            Destroy(item.gameObject);
            item = null;
        }
    }
}
