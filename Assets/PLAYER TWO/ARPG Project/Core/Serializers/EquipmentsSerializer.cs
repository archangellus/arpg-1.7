using System.Collections.Generic;
using UnityEngine;

namespace PLAYERTWO.ARPGProject
{
    [System.Serializable]
    public class EquipmentsSerializer
    {
        [System.Serializable]
        public class EquipmentEntry
        {
            public ItemSlots slot;
            public ItemSerializer item;

            public EquipmentEntry(ItemSlots slot, ItemSerializer item)
            {
                this.slot = slot;
                this.item = item;
            }
        }

        public List<EquipmentEntry> items = new();
        public ItemSerializer[] consumables;

        public EquipmentsSerializer(CharacterEquipments equipments)
        {
            foreach (ItemSlots slot in System.Enum.GetValues(typeof(ItemSlots)))
            {
                var instance = equipments.GetCurrent(slot);

                if (instance != null)
                    items.Add(new EquipmentEntry(slot, new ItemSerializer(instance)));
            }

            SerializeConsumables(equipments.currentConsumables);
        }

        /// <summary>
        /// Returns the serialized item for the given slot, or null if not found.
        /// </summary>
        public ItemSerializer GetItem(ItemSlots slot)
        {
            foreach (var entry in items)
                if (entry.slot == slot)
                    return entry.item;

            return null;
        }

        protected virtual void SerializeConsumables(ItemInstance[] consumables)
        {
            this.consumables = new ItemSerializer[consumables.Length];

            for (int i = 0; i < this.consumables.Length; i++)
            {
                if (consumables[i] == null)
                    continue;

                this.consumables[i] = new ItemSerializer(consumables[i]);
            }
        }

        public virtual string ToJson() => JsonUtility.ToJson(this);

        public static EquipmentsSerializer FromJson(string json) =>
            JsonUtility.FromJson<EquipmentsSerializer>(json);
    }
}
