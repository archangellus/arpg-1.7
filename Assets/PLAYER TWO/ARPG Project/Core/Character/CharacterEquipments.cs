using System.Collections.Generic;

namespace PLAYERTWO.ARPGProject
{
    public class CharacterEquipments
    {
        public Dictionary<ItemSlots, ItemInstance> initialItems = new();
        public ItemInstance[] initialConsumables;

        /// <summary>
        /// Returns the initial item for the given slot, or null if none was set.
        /// </summary>
        public ItemInstance GetInitial(ItemSlots slot) =>
            initialItems.TryGetValue(slot, out var item) ? item : null;

        /// <summary>
        /// Returns the current item for the given slot from the Entity Item Manager,
        /// or the initial item if no manager is assigned.
        /// </summary>
        public ItemInstance GetCurrent(ItemSlots slot) =>
            m_items ? m_items.GetOrInitializeItem(slot) : GetInitial(slot);

        public ItemInstance[] currentConsumables =>
            m_items ? m_items.GetConsumables() : initialConsumables;

        protected EntityItemManager m_items;

        public CharacterEquipments(Character data)
        {
            foreach (var entry in data.initialEquipments.entries)
                SetInitialItem(entry.slot, entry.item);

            InstantiateConsumables(data.initialConsumables, data.maxConsumableSlots);
        }

        public CharacterEquipments(
            Dictionary<ItemSlots, ItemInstance> initialItems,
            ItemInstance[] initialConsumables
        )
        {
            this.initialItems = initialItems;
            this.initialConsumables = initialConsumables;
        }

        /// <summary>
        /// Initializes a given Entity Item Manager.
        /// </summary>
        /// <param name="items">The Entity Item Manager you want to initialize.</param>
        public virtual void InitializeEquipments(EntityItemManager items)
        {
            m_items = items;

            foreach (ItemSlots slot in System.Enum.GetValues(typeof(ItemSlots)))
                m_items.TryEquip(GetInitial(slot), slot);

            m_items.SetConsumables(initialConsumables);
        }

        protected virtual void SetInitialItem(ItemSlots slot, CharacterItem item)
        {
            if (item.data != null)
                initialItems[slot] = item.ToItemInstance(true);
        }

        protected virtual void InstantiateConsumables(ItemConsumable[] consumables, int maxCapacity)
        {
            initialConsumables = new ItemInstance[maxCapacity];

            for (int i = 0; i < maxCapacity; i++)
            {
                if (consumables.Length <= i || !consumables[i])
                    continue;

                initialConsumables[i] = new ItemInstance(consumables[i]);
            }
        }

        public static CharacterEquipments CreateFromSerializer(EquipmentsSerializer serializer)
        {
            var initialItems = new Dictionary<ItemSlots, ItemInstance>();

            foreach (ItemSlots slot in System.Enum.GetValues(typeof(ItemSlots)))
            {
                var instance = ItemInstance.CreateFromSerializer(serializer.GetItem(slot));

                if (instance != null)
                    initialItems[slot] = instance;
            }

            var consumables = new ItemInstance[serializer.consumables.Length];

            for (int i = 0; i < consumables.Length; i++)
                consumables[i] = ItemInstance.CreateFromSerializer(serializer.consumables[i]);

            return new CharacterEquipments(initialItems, consumables);
        }
    }
}
