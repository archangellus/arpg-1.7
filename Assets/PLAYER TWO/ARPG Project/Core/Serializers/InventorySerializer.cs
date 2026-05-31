using UnityEngine;
using System.Collections.Generic;

namespace PLAYERTWO.ARPGProject
{
    [System.Serializable]
    public class InventorySerializer
    {
        [System.Serializable]
        public class InventoryItem
        {
            public ItemSerializer item;
            public int row;
            public int column;
        }

        public int money;
        public List<InventoryItem> items = new();

        public InventorySerializer(Inventory inventory)
        {
            Initialize(inventory.items, inventory.money);
        }

        public InventorySerializer(CharacterInventory inventory)
        {
            Initialize(inventory.currentItems, inventory.currentMoney);
        }

        protected virtual void Initialize(Dictionary<ItemInstance, InventoryCell> items, int coins)
        {
            foreach (var item in items)
            {
                var id = GameDatabase.instance
                    .GetElementId<ARPGProject.Item>(item.Key.data);

                var serializedItem = new ItemSerializer(item.Key);

                var itemData = new InventoryItem()
                {
                    item = serializedItem,
                    row = item.Value.row,
                    column = item.Value.column
                };

                this.items.Add(itemData);
            }

            money = coins;
        }

        public virtual string ToJson() => JsonUtility.ToJson(this);

        public static InventorySerializer FromJson(string json) =>
            JsonUtility.FromJson<InventorySerializer>(json);
    }
}
