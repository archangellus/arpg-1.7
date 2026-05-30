using UnityEngine;

namespace PLAYERTWO.ARPGProject
{
    [AddComponentMenu("PLAYER TWO/ARPG Project/Inventory/Pet Inventory Settings")]
    public class PetInventorySettings : Singleton<PetInventorySettings>
    {
        [Header("Inventory Settings")]
        [Min(1)]
        [Tooltip("The amount of rows available on the Pet Inventory.")]
        public int rows = 4;

        [Min(1)]
        [Tooltip("The amount of columns available on the Pet Inventory.")]
        public int columns = 6;

        protected Inventory m_inventory;
        protected bool m_loadedCharacterItems;

        public Inventory instance
        {
            get
            {
                if (m_inventory == null)
                    CreateInventory();

                LoadCharacterItems();
                return m_inventory;
            }
        }

        protected virtual void CreateInventory()
        {
            m_inventory = new Inventory(rows, columns);
            m_inventory.money = 0;
        }

        protected virtual void LoadCharacterItems()
        {
            if (m_loadedCharacterItems || !Game.instance)
                return;

            m_loadedCharacterItems = true;
            Game.instance.currentCharacter.petInventory.InitializeInventory(m_inventory);
        }
    }
}
