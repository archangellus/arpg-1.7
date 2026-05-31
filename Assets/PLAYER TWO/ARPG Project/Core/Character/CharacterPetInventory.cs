using System.Collections.Generic;

namespace PLAYERTWO.ARPGProject
{
    public class CharacterPetInventory
    {
        public Dictionary<ItemInstance, InventoryCell> initialItems = new();

        public Dictionary<ItemInstance, InventoryCell> currentItems =>
            m_inventory != null ? m_inventory.items : initialItems;

        protected Inventory m_inventory;

        public CharacterPetInventory() { }

        public virtual void InitializeInventory(Inventory inventory)
        {
            m_inventory = inventory;
            m_inventory.money = 0;

            foreach (var item in initialItems)
            {
                var row = item.Value.row;
                var column = item.Value.column;
                m_inventory.TryInsertItem(item.Key, row, column);
            }
        }

        public static CharacterPetInventory CreateFromSerializer(InventorySerializer serializer)
        {
            var inventory = new CharacterPetInventory();

            if (serializer == null)
                return inventory;

            foreach (var entry in serializer.items)
            {
                var instance = ItemInstance.CreateFromSerializer(entry.item);

                if (instance == null)
                    continue;

                inventory.initialItems.Add(instance, new(entry.row, entry.column));
            }

            return inventory;
        }
    }
}
