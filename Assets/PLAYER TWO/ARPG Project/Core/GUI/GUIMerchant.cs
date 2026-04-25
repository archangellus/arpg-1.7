using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace PLAYERTWO.ARPGProject
{
    [AddComponentMenu("PLAYER TWO/ARPG Project/GUI/GUI Merchant")]
    public class GUIMerchant : MonoBehaviour
    {
        [Header("General Settings")]
        [Tooltip("The prefab to use as tabs.")]
        public UITab tabPrefab;

        [Tooltip("The prefab to use as the sections, which corresponds to different inventories.")]
        public GUIInventory sectionPrefab;

        [Tooltip("A reference to the toggle group component.")]
        public ToggleGroup toggleGroup;

        [Header("Containers References")]
        [Tooltip("A reference to the container for sections.")]
        public RectTransform sectionsContainer;

        [Tooltip("A reference to the container for the tabs.")]
        public RectTransform tabsContainer;

        [Header("Audio Settings")]
        [Tooltip("The Audio Clip that plays when buying an item.")]
        public AudioClip buyClip;

        [Tooltip("The Audio Clip that plays when selling an item.")]
        public AudioClip sellClip;

        [Tooltip("The Audio Clip that plays when switching between tabs.")]
        public AudioClip switchTabClip;

        protected Merchant m_merchant;
        protected Dictionary<string, GUIInventory> m_sections;

        protected Inventory m_playerInventory => Level.instance.player.inventory.instance;

        protected GUIInventory m_playerGUIInventory => GUIWindowsManager.instance.GetInventory();

        protected GameAudio m_audio => GameAudio.instance;

        protected virtual void InitializeSections()
        {
            m_sections = new Dictionary<string, GUIInventory>();

            var isFirstSection = true;

            foreach (var section in m_merchant.inventories)
            {
                var tab = Instantiate(tabPrefab, tabsContainer);
                var shopSection = Instantiate(sectionPrefab, sectionsContainer);

                shopSection.SetInventory(section.Value);
                shopSection.InitializeInventory();
                shopSection.gameObject.SetActive(isFirstSection);

                tab.text.text = section.Key;
                tab.toggle.isOn = isFirstSection;
                tab.toggle.group = toggleGroup;

                tab.toggle.onValueChanged.AddListener(
                    (value) =>
                    {
                        shopSection.gameObject.SetActive(value);
                        if (m_audio)
                            m_audio.PlayUiEffect(switchTabClip);
                    }
                );

                if (isFirstSection)
                    isFirstSection = false;

                m_sections.Add(section.Key, shopSection);
            }
        }

        protected virtual void DestroySections()
        {
            foreach (Transform child in sectionsContainer)
                Destroy(child.gameObject);

            foreach (Transform child in tabsContainer)
                Destroy(child.gameObject);
        }

        /// <summary>
        /// Sets the Merchant instance this GUI represents, resetting all tabs and sections.
        /// </summary>
        /// <param name="merchant">The Merchant instance you want to set.</param>
        public virtual void SetMerchant(Merchant merchant)
        {
            if (m_merchant == merchant)
                return;

            m_merchant = merchant;

            DestroySections();
            InitializeSections();
        }

        /// <summary>
        /// Returns the GUI Inventory that corresponds to the current active section.
        /// </summary>
        public virtual GUIInventory GetActiveSection()
        {
            foreach (var section in m_sections)
            {
                if (section.Value.gameObject.activeSelf)
                    return section.Value;
            }

            return null;
        }

        /// <summary>
        /// Returns the GUI Inventory that corresponds to the "Buy Back" section.
        /// </summary>
        public virtual GUIInventory GetBuyBackSection()
        {
            if (!m_sections.ContainsKey(m_merchant.buyBackTitle))
                return null;

            return m_sections[m_merchant.buyBackTitle];
        }

        /// <summary>
        /// Tries to buy an given GUI Item from the Player.
        /// </summary>
        /// <param name="item">The GUI Item you want this merchant to buy.</param>
        /// <returns>Returns true if the Merchant was able to buy the item.</returns>
        public virtual bool TryBuy(GUIItem item)
        {
            var price = item.item.GetSellPrice();
            var buyBackSection = GetBuyBackSection();

            if (m_playerGUIInventory.Contains(item) && !m_playerGUIInventory.TryRemove(item))
            {
                m_audio.PlayDeniedSound();
                return false;
            }

            if (!buyBackSection || !buyBackSection.TryAutoInsert(item))
                Destroy(item.gameObject);

            m_playerInventory.money += price;
            m_audio.PlayUiEffect(buyClip);
            return true;
        }

        /// <summary>
        /// Tries to sell a given GUI Item to the Player.
        /// </summary>
        /// <param name="item">The GUI Item you want to buy.</param>
        /// <returns>Returns true if the item was sold to the Player.</returns>
        public virtual bool TrySell(GUIItem item)
        {
            var section = GetActiveSection();
            var price = item.item.GetPrice();

            if (m_playerInventory.money < price || !m_playerGUIInventory.TryAutoInsert(item))
            {
                m_audio.PlayDeniedSound();
                return false;
            }

            section.TryRemove(item);
            m_playerInventory.money -= price;
            m_audio.PlayUiEffect(sellClip);
            return true;
        }

        protected virtual void OnDisable() => GUIWindowsManager.instance.inventoryWindow.Hide();
    }
}
