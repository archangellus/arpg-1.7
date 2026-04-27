using System.Collections.Generic;
using UnityEngine;

namespace PLAYERTWO.ARPGProject
{
    [AddComponentMenu("PLAYER TWO/ARPG Project/Misc/Merchant")]
    public class Merchant : Interactive
    {
        [System.Serializable]
        public class MerchantItem
        {
            [Tooltip("The scriptable object representing the Item.")]
            public Item data;

            [Tooltip("The amount of additional attributes on this Item.")]
            public int attributes;
        }

        [System.Serializable]
        public class Section
        {
            [Tooltip("The title of the section.")]
            public string title;

            [Tooltip("The items available to purchase from this section.")]
            public MerchantItem[] items;
        }

        [Header("Merchant Settings")]
        [Tooltip("The amount of rows in the Merchant's Inventory.")]
        public int rows;

        [Tooltip("The amount of columns in the Merchant's Inventory.")]
        public int columns;

        [Tooltip("The title of the section to buy back items.")]
        public string buyBackTitle = "BUY BACK";

        [Tooltip("The shopping sections/categories available on the Merchant.")]
        public Section[] sections;

        /// <summary>
        /// Returns a dictionary of inventories using their section title as key.
        /// </summary>
        public Dictionary<string, Inventory> inventories { get; protected set; }

        protected GUIMerchant m_guiMerchant => GUIWindowsManager.instance.GetMerchant();

        protected virtual void InitializeInventories()
        {
            inventories = new Dictionary<string, Inventory>();

            foreach (var section in sections)
            {
                if (section.items == null)
                    continue;

                var inventory = new Inventory(rows, columns);
                inventories.Add(section.title, inventory);

                foreach (var item in section.items)
                {
                    if (item.data == null)
                        continue;

                    if (item.attributes > 0)
                        inventory.TryAddItem(
                            new ItemInstance(item.data, true, item.attributes, item.attributes)
                        );
                    else
                        inventory.TryAddItem(new ItemInstance(item.data, false));
                }
            }

            inventories.Add(buyBackTitle, new Inventory(rows, columns));
        }

        protected override void OnInteract(object other)
        {
            if (other is not Entity)
                return;

            GUIWindowsManager.instance.merchantWindow.Show();
            GUIWindowsManager.instance.inventoryWindow.Show();

            m_guiMerchant.SetMerchant(this);
        }

        protected override void Start()
        {
            base.Start();
            InitializeInventories();
        }
    }
}
