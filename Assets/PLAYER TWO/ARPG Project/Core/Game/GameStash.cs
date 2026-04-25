using UnityEngine;

namespace PLAYERTWO.ARPGProject
{
    [AddComponentMenu("PLAYER TWO/ARPG Project/Game/Game Stash")]
    public class GameStash : Singleton<GameStash>
    {
        [Header("Settings")]
        [Tooltip("The amount of stashes available to store items.")]
        public int amount = 5;

        [Tooltip("The amount of rows available on each stash.")]
        public int rows = 10;

        [Tooltip("The amount of columns available on each stash.")]
        public int columns = 7;

        /// <summary>
        /// Returns all the inventories that represents each stash.
        /// </summary>
        public Inventory[] inventories { get; protected set; }

        protected virtual void InitializeInventories()
        {
            if (inventories != null)
                return;

            inventories = new Inventory[amount];

            for (int i = 0; i < amount; i++)
            {
                inventories[i] = new Inventory(rows, columns);
            }
        }

        /// <summary>
        /// Gets a stash Inventory by its index.
        /// </summary>
        /// <param name="index">The index of the stash you want to get.</param>
        public Inventory GetInventory(int index)
        {
            if (!inventories.IsIndexValid(index))
                return null;

            return inventories[index];
        }

        /// <summary>
        /// Loads the data of the stash from a given Inventory Serializer.
        /// </summary>
        /// <param name="inventories">The Inventory Serializer you want to read the data from.</param>
        public virtual void LoadData(InventorySerializer[] inventories)
        {
            if (inventories == null)
                return;

            this.inventories = new Inventory[amount];

            for (int i = 0; i < amount; i++)
            {
                this.inventories[i] = new Inventory(rows, columns);

                foreach (var item in inventories[i].items)
                {
                    var instance = ItemInstance.CreateFromSerializer(item.item);
                    this.inventories[i].TryInsertItem(instance, item.row, item.column);
                }

                this.inventories[i].money = inventories[i].money;
            }
        }

        protected override void Initialize()
        {
            InitializeInventories();
        }
    }
}
