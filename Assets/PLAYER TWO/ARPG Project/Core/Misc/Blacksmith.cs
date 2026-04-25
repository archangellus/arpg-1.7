using UnityEngine;

namespace PLAYERTWO.ARPGProject
{
    [AddComponentMenu("PLAYER TWO/ARPG Project/NPC/Blacksmith")]
    public class Blacksmith : Interactive
    {
        [Header("Item Repair Settings")]
        [Tooltip("Minimum cost of repairing an Item.")]
        public int minPrice;

        [Tooltip("Maximum cost of repairing an Item.")]
        public int maxPrice;

        protected Entity m_entity;

        protected GUIBlacksmith m_blacksmithWindow => GUIWindowsManager.instance.blacksmith;

        /// <summary>
        /// Tries to repair a given Item Instance.
        /// </summary>
        /// <param name="item">The Item Instance you're trying to repair.</param>
        /// <returns>Returns true if the Item Instance was successfully repaired.</returns>
        public virtual bool TryRepair(ItemInstance item)
        {
            var price = GetPriceToRepair(item);

            if (m_entity.inventory.instance.money < price)
                return false;

            item.Repair();
            m_entity.inventory.instance.money -= price;
            return true;
        }

        /// <summary>
        /// Tries to repair all the items from the Entity inventory.
        /// </summary>
        /// <returns>Returns true if the items were repaired.</returns>
        public virtual bool TryRepairAll()
        {
            var price = GetPriceToRepairAll();

            if (m_entity.inventory.instance.money < price)
                return false;

            foreach (var item in m_entity.inventory.instance.items)
                item.Key.Repair();

            foreach (var item in m_entity.items.GetEquippedItems())
                item.Repair();

            m_entity.inventory.instance.money -= price;
            return true;
        }

        /// <summary>
        /// Returns the total cost to repair a given Item Instance.
        /// </summary>
        /// <param name="item">The Item Instance you want to get the cost from.</param>
        public virtual int GetPriceToRepair(ItemInstance item)
        {
            if (item == null)
                return 0;

            var durability = item.GetDurabilityRate();

            if (durability == 1)
                return 0;

            return (int)Mathf.Lerp(maxPrice, minPrice, durability);
        }

        /// <summary>
        /// Returns the total cost to repair all items from the Entity's inventory.
        /// </summary>
        public virtual int GetPriceToRepairAll()
        {
            if (!m_entity)
                return 0;

            var total = 0;

            foreach (var item in m_entity.inventory.instance.items)
                total += GetPriceToRepair(item.Key);

            foreach (var item in m_entity.items.GetEquippedItems())
                total += GetPriceToRepair(item);

            return total;
        }

        protected override void OnInteract(object other)
        {
            if (other is not Entity)
                return;

            if ((other as Entity) != m_entity)
            {
                m_entity = other as Entity;
                m_entity.inventory.onItemAdded.AddListener((_) => m_blacksmithWindow.Refresh());
                m_entity.inventory.onItemInserted.AddListener((_) => m_blacksmithWindow.Refresh());
                m_entity.inventory.onItemRemoved.AddListener(m_blacksmithWindow.Refresh);
                m_entity.items.onChanged.AddListener(m_blacksmithWindow.Refresh);
            }

            m_blacksmithWindow.Show(this);
        }
    }
}
