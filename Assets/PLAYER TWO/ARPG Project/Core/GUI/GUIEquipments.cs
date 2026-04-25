using System.Collections.Generic;
using UnityEngine;

namespace PLAYERTWO.ARPGProject
{
    [AddComponentMenu("PLAYER TWO/ARPG Project/GUI/GUI Equipments")]
    public class GUIEquipments : MonoBehaviour
    {
        protected Dictionary<ItemSlots, GUIEquipmentSlot> m_slots;
        protected EntityItemManager m_equipments;

        /// <summary>
        /// Returns the reference to the Entity's Item Manager.
        /// </summary>
        public EntityItemManager equipments
        {
            get
            {
                if (!m_equipments)
                    m_equipments = Level.instance.player.items;

                return m_equipments;
            }
        }

        /// <summary>
        /// Returns the GUI Equipment Slot for the given item slot, or null if not found.
        /// </summary>
        protected virtual GUIEquipmentSlot GetSlot(ItemSlots slot) =>
            m_slots.TryGetValue(slot, out var result) ? result : null;

        protected virtual void InitializeSlots()
        {
            var found = GetComponentsInChildren<GUIEquipmentSlot>();
            m_slots = new Dictionary<ItemSlots, GUIEquipmentSlot>(found.Length);

            foreach (var slot in found)
                m_slots[slot.slot] = slot;
        }

        protected virtual void InitializeEquipments()
        {
            foreach (var kvp in m_slots)
                Equip(equipments.GetOrInitializeItem(kvp.Key), kvp.Value);
        }

        /// <summary>
        /// Tries to auto equip a given GUI Item on the first free slot
        /// that corresponds to the item slot.
        /// </summary>
        /// <param name="item">The GUI Item you want to equip.</param>
        /// <returns>Returns true if the item was equipped.</returns>
        public virtual bool TryAutoEquip(GUIItem item)
        {
            foreach (var kvp in m_slots)
            {
                if (kvp.Key == ItemSlots.RingA || kvp.Key == ItemSlots.RingB)
                    continue;

                if (TryEquip(item, kvp.Value))
                    return true;
            }

            return TryAutoEquipRing(item);
        }

        /// <summary>
        /// Tries to auto equip a ring by preferring an empty slot.
        /// Tries Ring A first if empty, then Ring B if empty.
        /// If both slots are occupied, falls back to Ring A.
        /// </summary>
        /// <param name="item">The GUI Item you want to equip.</param>
        /// <returns>Returns true if the item was equipped.</returns>
        protected virtual bool TryAutoEquipRing(GUIItem item)
        {
            var ringA = GetSlot(ItemSlots.RingA);
            var ringB = GetSlot(ItemSlots.RingB);

            var ringAIsEmpty = ringA && !ringA.item;
            var ringBIsEmpty = ringB && !ringB.item;

            if (ringAIsEmpty && TryEquip(item, ringA))
                return true;
            if (ringBIsEmpty && TryEquip(item, ringB))
                return true;
            if (TryEquip(item, ringA))
                return true;

            return false;
        }

        /// <summary>
        /// Tries to equip a given GUI Item on a given GUI Equipment Slot.
        /// </summary>
        /// <param name="item">The GUI Item you want to equip.</param>
        /// <param name="slot">The GUI Equipment Slot you want to equip the item on.</param>
        /// <returns>Returns true if the item was equipped.</returns>
        public virtual bool TryEquip(GUIItem item, GUIEquipmentSlot slot)
        {
            if (!slot || !slot.CanEquip(item))
                return false;

            slot.Equip(item);
            return true;
        }

        /// <summary>
        /// Equips an GUI Item on a given GUI Equipment Slot.
        /// </summary>
        /// <param name="item">The GUI Item you want to equip.</param>
        /// <param name="equipment">The GUI Equipment Slot you want to equip the item on.</param>
        public virtual void Equip(ItemInstance item, GUIEquipmentSlot equipment)
        {
            if (item != null && equipment)
            {
                equipment.Equip(GUI.instance.CreateGUIItem(item));
            }
        }

        protected virtual void Awake()
        {
            InitializeSlots();
        }

        protected virtual void Start()
        {
            InitializeEquipments();
        }
    }
}
