using UnityEngine;
using UnityEngine.UI;

namespace PLAYERTWO.ARPGProject
{
    [AddComponentMenu("PLAYER TWO/ARPG Project/GUI/GUI Blacksmith")]
    public class GUIBlacksmith : GUIWindow
    {
        [Header("Blacksmith Settings")]
        [Tooltip("The slot to place the items to repair.")]
        public GUIBlacksmithSlot slot;

        [Tooltip("The reference to the 'repair' Button.")]
        public Button repairButton;

        [Tooltip("The reference to the 'repair all' Button.")]
        public Button repairAllButton;

        [Tooltip("The reference to the 'repair cost' Text.")]
        public Text repairCostText;

        [Tooltip("The reference to the 'repair all cost' Text.")]
        public Text repairAllCostText;

        [Header("Audio Settings")]
        [Tooltip("The Audio Clip that plays when repairing an Item.")]
        public AudioClip repairAudio;

        protected Blacksmith m_blacksmith;
        protected GUIInventory m_inventory;

        protected virtual void UpdateButtons()
        {
            repairButton.interactable =
                m_blacksmith.GetPriceToRepair(slot.item.SafeGet(i => i.item)) > 0;
            repairAllButton.interactable = m_blacksmith.GetPriceToRepairAll() > 0;
        }

        protected virtual void InitializeCallbacks()
        {
            repairButton.onClick.AddListener(OnRepairClicked);
            repairAllButton.onClick.AddListener(OnRepairAllClicked);
            slot.onEquip.AddListener(OnEquip);
            slot.onUnequip.AddListener(OnUnequip);
        }

        protected virtual void OnRepairClicked()
        {
            if (!m_blacksmith || !slot.item)
                return;

            if (m_blacksmith.TryRepair(slot.item.item))
            {
                ClearRepairCost();
                UpdateButtons();
                m_audio.PlayUiEffect(repairAudio);
            }
        }

        protected virtual void OnRepairAllClicked()
        {
            if (!m_blacksmith)
                return;

            if (m_blacksmith.TryRepairAll())
            {
                UpdateRepairAllCost();
                UpdateButtons();
                m_audio.PlayUiEffect(repairAudio);
            }
        }

        public virtual void OnEquip(GUIItem item)
        {
            if (item.item.GetDurabilityRate() == 1)
            {
                ClearRepairCost();
                return;
            }

            UpdateRepairCost();
            UpdateRepairAllCost();
            UpdateButtons();
        }

        public virtual void OnUnequip(GUIItem _)
        {
            ClearRepairCost();
            UpdateRepairAllCost();
            UpdateButtons();
        }

        public virtual void Show(Blacksmith blacksmith)
        {
            base.Show();
            m_blacksmith = blacksmith;
            m_inventory = GUIWindowsManager.instance.GetInventory();
            m_inventory.GetComponent<GUIWindow>().SafeCall(w => w.Show());
            UpdateRepairAllCost();
            UpdateButtons();
        }

        public virtual void Refresh()
        {
            if (!isOpen)
                return;

            UpdateRepairCost();
            UpdateRepairAllCost();
            UpdateButtons();
        }

        protected virtual void UpdateRepairCost() =>
            repairCostText.text = m_blacksmith
                .GetPriceToRepair(slot.item.SafeGet(i => i.item))
                .ToString();

        protected virtual void ClearRepairCost() => repairCostText.text = "0";

        protected virtual void UpdateRepairAllCost() =>
            repairAllCostText.text = m_blacksmith.GetPriceToRepairAll().ToString();

        protected override void OnClose()
        {
            if (!m_inventory)
                return;

            m_inventory.GetComponent<GUIWindow>().SafeCall(w => w.Hide());
        }

        protected override void Start()
        {
            base.Start();
            InitializeCallbacks();
            UpdateButtons();
        }

        protected virtual void OnDisable()
        {
            if (!slot || !slot.item)
                return;

            if (slot.item.TryMoveToLastPosition())
                slot.Unequip();
        }
    }
}
