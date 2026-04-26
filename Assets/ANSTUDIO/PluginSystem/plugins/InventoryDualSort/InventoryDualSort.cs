using System.Collections.Generic;
using UnityEngine;

namespace PLAYERTWO.ARPGProject.InventoryDualSort
{
    /// <summary>
    /// Plugin that alternates inventory sorting between horizontal and vertical scans.
    /// </summary>
    [Plugin("inventory-dual-sort", DisplayName = "Inventory Dual Sort", Version = "1.1.0", LoadOrder = 100)]
    public class InventoryDualSort : IPlugin
    {
        private readonly Dictionary<Inventory, bool> m_sortHorizontal = new();

        public void Initialize()
        {
            EventBus.InventoryAutoSortRequestedEvent += OnInventoryAutoSortRequested;
            Debug.Log("[InventoryDualSort] Initialized");
        }

        public void Shutdown()
        {
            EventBus.InventoryAutoSortRequestedEvent -= OnInventoryAutoSortRequested;
            m_sortHorizontal.Clear();
            Debug.Log("[InventoryDualSort] Shutdown");
        }

        private void OnInventoryAutoSortRequested(Inventory inventory)
        {
            if (inventory == null)
                return;

            bool horizontal = true;
            if (m_sortHorizontal.TryGetValue(inventory, out var current))
                horizontal = current;

            SortInventory(inventory, horizontal);
            m_sortHorizontal[inventory] = !horizontal;
        }

        private static void SortInventory(Inventory inventory, bool horizontal)
        {
            var sortedItems = new List<ItemInstance>(inventory.items.Keys);
            sortedItems.Sort((a, b) =>
            {
                int areaComparison = (b.rows * b.columns).CompareTo(a.rows * a.columns);
                return areaComparison != 0 ? areaComparison : b.rows.CompareTo(a.rows);
            });

            inventory.Clear();

            foreach (var item in sortedItems)
                TryAddOrStack(inventory, item, horizontal);
        }

        private static bool TryAddOrStack(Inventory inventory, ItemInstance item, bool horizontal)
        {
            if (inventory.TryStackItem(item))
                return true;

            return TryAddItem(inventory, item, horizontal);
        }

        private static bool TryAddItem(Inventory inventory, ItemInstance item, bool horizontal)
        {
            if (horizontal)
            {
                for (int i = 0; i < inventory.rows; i++)
                {
                    for (int j = 0; j < inventory.columns; j++)
                    {
                        if (inventory.TryInsertItem(item, i, j))
                        {
                            inventory.onItemAdded?.Invoke(item, new InventoryCell(i, j));
                            return true;
                        }
                    }
                }
            }
            else
            {
                for (int j = 0; j < inventory.columns; j++)
                {
                    for (int i = 0; i < inventory.rows; i++)
                    {
                        if (inventory.TryInsertItem(item, i, j))
                        {
                            inventory.onItemAdded?.Invoke(item, new InventoryCell(i, j));
                            return true;
                        }
                    }
                }
            }

            return false;
        }
    }
}