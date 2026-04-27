using System.Collections.Generic;

namespace PLAYERTWO.ARPGProject
{
    public class CharacterInventory
    {
        public int initialMoney;
        public Dictionary<ItemInstance, InventoryCell> initialItems;

        public int currentMoney => m_inventory ? m_inventory.instance.money : initialMoney;
        public Dictionary<ItemInstance, InventoryCell> currentItems =>
            m_inventory != null ? m_inventory.instance.items : initialItems;

        protected EntityInventory m_inventory;

        public CharacterInventory(Character data) => Initialize(data.inventory, data.initialMoney);

        public CharacterInventory(CharacterInventoryItem[] items, int money) => Initialize(items, money);

        public virtual void InitializeInventory(EntityInventory inventory)
        {
            m_inventory = inventory;
            m_inventory.instance.money = initialMoney;

            foreach (var item in initialItems)
            {
                var row = item.Value.row;
                var column = item.Value.column;
                m_inventory.instance.TryInsertItem(item.Key, row, column);
            }
        }

        protected virtual void Initialize(CharacterInventoryItem[] items, int money)
        {
            initialMoney = money;
            initialItems = new();

            foreach (var inventoryItem in items)
            {
                if (inventoryItem.item.data == null) continue;

                var instance = inventoryItem.item.ToItemInstance();
                initialItems.Add(instance, new(inventoryItem.row, inventoryItem.column));
            }
        }

        public static CharacterInventory CreateFromSerializer(InventorySerializer serializer)
        {
            var items = new List<CharacterInventoryItem>();

            foreach (var item in serializer.items)
            {
                var data = GameDatabase.instance.FindElementById<Item>(item.item.itemId);
                var attributes = CharacterItemAttributes.CreateFromSerializer(item.item.attributes);
                var cItem = new CharacterItem(data, attributes, item.item.durability, item.item.stack);
                var inventoryItem = new CharacterInventoryItem(cItem, item.row, item.column);

                items.Add(inventoryItem);
            }

            return new CharacterInventory(items.ToArray(), serializer.money);
        }
    }
}
