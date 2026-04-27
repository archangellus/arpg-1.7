namespace PLAYERTWO.ARPGProject
{
    public class CharacterEquipments
    {
        public ItemInstance initialRightHand;
        public ItemInstance initialLeftHand;
        public ItemInstance initialHelm;
        public ItemInstance initialChest;
        public ItemInstance initialPants;
        public ItemInstance initialGloves;
        public ItemInstance initialBoots;
        public ItemInstance[] initialConsumables;

        public ItemInstance currentRightHand => m_items ? m_items.GetRightHand() : initialRightHand;
        public ItemInstance currentLeftHand => m_items ? m_items.GetLeftHand() : initialLeftHand;
        public ItemInstance currentHelm => m_items ? m_items.GetHelm() : initialHelm;
        public ItemInstance currentChest => m_items ? m_items.GetChest() : initialChest;
        public ItemInstance currentPants => m_items ? m_items.GetPants() : initialPants;
        public ItemInstance currentGloves => m_items ? m_items.GetGloves() : initialGloves;
        public ItemInstance currentBoots => m_items ? m_items.GetBoots() : initialBoots;
        public ItemInstance[] currentConsumables => m_items ? m_items.GetConsumables() : initialConsumables;

        protected EntityItemManager m_items;

        public CharacterEquipments(Character data)
        {
            InstantiateItem(data.rightHand, ref initialRightHand);
            InstantiateItem(data.leftHand, ref initialLeftHand);
            InstantiateItem(data.helm, ref initialHelm);
            InstantiateItem(data.chest, ref initialChest);
            InstantiateItem(data.pants, ref initialPants);
            InstantiateItem(data.gloves, ref initialGloves);
            InstantiateItem(data.boots, ref initialBoots);
            InstantiateConsumables(data.initialConsumables, data.maxConsumableSlots);
        }

        public CharacterEquipments(ItemInstance initialRightHand,
            ItemInstance initialLeftHand, ItemInstance initialHelm,
            ItemInstance initialChest, ItemInstance initialPants,
            ItemInstance initialGloves, ItemInstance initialBoots,
            ItemInstance[] initialConsumables)
        {
            this.initialRightHand = initialRightHand;
            this.initialLeftHand = initialLeftHand;
            this.initialHelm = initialHelm;
            this.initialChest = initialChest;
            this.initialPants = initialPants;
            this.initialGloves = initialGloves;
            this.initialBoots = initialBoots;
            this.initialConsumables = initialConsumables;
        }

        /// <summary>
        /// Initializes a given Entity Item Manager.
        /// </summary>
        /// <param name="items">The Entity Item Manager you want to initialize.</param>
        public virtual void InitializeEquipments(EntityItemManager items)
        {
            m_items = items;
            m_items.TryEquip(initialRightHand, ItemSlots.RightHand);
            m_items.TryEquip(initialLeftHand, ItemSlots.LeftHand);
            m_items.TryEquip(initialHelm, ItemSlots.Helm);
            m_items.TryEquip(initialChest, ItemSlots.Chest);
            m_items.TryEquip(initialPants, ItemSlots.Pants);
            m_items.TryEquip(initialGloves, ItemSlots.Gloves);
            m_items.TryEquip(initialBoots, ItemSlots.Boots);
            m_items.SetConsumables(initialConsumables);
        }

        protected virtual void InstantiateItem(CharacterItem item, ref ItemInstance reference)
        {
            if (item.data != null) reference = item.ToItemInstance(true);
        }

        protected virtual void InstantiateConsumables(Item[] consumables, int maxCapacity)
        {
            initialConsumables = new ItemInstance[maxCapacity];

            for (int i = 0; i < maxCapacity; i++)
            {
                if (consumables.Length <= i || !consumables[i]) continue;

                initialConsumables[i] = new ItemInstance(consumables[i]);
            }
        }

        public static CharacterEquipments CreateFromSerializer(EquipmentsSerializer equipments)
        {
            var consumables = new ItemInstance[equipments.consumables.Length];

            for (int i = 0; i < consumables.Length; i++)
            {
                consumables[i] = ItemInstance.CreateFromSerializer(equipments.consumables[i]);
            }

            return new CharacterEquipments(
                ItemInstance.CreateFromSerializer(equipments.rightHand),
                ItemInstance.CreateFromSerializer(equipments.leftHand),
                ItemInstance.CreateFromSerializer(equipments.helm),
                ItemInstance.CreateFromSerializer(equipments.chest),
                ItemInstance.CreateFromSerializer(equipments.pants),
                ItemInstance.CreateFromSerializer(equipments.gloves),
                ItemInstance.CreateFromSerializer(equipments.boots),
                consumables
            );
        }
    }
}
