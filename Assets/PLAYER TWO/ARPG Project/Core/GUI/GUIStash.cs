using UnityEngine;
using UnityEngine.UI;

namespace PLAYERTWO.ARPGProject
{
    [AddComponentMenu("PLAYER TWO/ARPG Project/GUI/GUI Stash")]
    public class GUIStash : GUIInventory
    {
        [Header("Stash Settings")]
        [Tooltip("The index of the stash from the Game Stash this GUI Stash represents.")]
        public int stashIndex;

        [Header("Deposit Settings")]
        [Tooltip("A reference to the Button that activates the deposit Window.")]
        public Button depositButton;

        [Tooltip("A reference to the Input Field to input the amount of depositing coins.")]
        public InputField depositField;

        [Tooltip("A reference to the Window to deposit coins.")]
        public GUIWindow depositWindow;

        [Header("Withdraw Settings")]
        [Tooltip("A reference to the Button that activates the withdraw Window.")]
        public Button withdrawButton;

        [Tooltip("A reference to the Input Field to input the amount of withdraw coins.")]
        public InputField withdrawField;

        [Tooltip("A reference to the Window to withdraw coins.")]
        public GUIWindow withdrawWindow;

        [Header("Audio Settings")]
        [Tooltip("The Audio Clip that plays when the Stash shows up.")]
        public AudioClip showClip;

        [Tooltip("The Audio Clip that plays when depositing coins.")]
        public AudioClip depositClip;

        [Tooltip("The Audio Clip that plays when withdraw coins.")]
        public AudioClip withdrawClip;

        protected GUIWindow m_window;

        protected EntityInventory m_playerInventory => Level.instance.player.inventory;
        protected GUIInventory m_playerInventoryGUI => GUIWindowsManager.instance.GetInventory();

        protected virtual void InitializeWindow()
        {
            m_window = GetComponentInParent<GUIWindow>();
        }

        protected virtual void InitializeActions()
        {
            depositButton.onClick.AddListener(OnDeposit);
            withdrawButton.onClick.AddListener(OnWithdraw);
        }

        protected virtual void OnDeposit()
        {
            var amount = int.Parse(depositField.text);

            if (m_playerInventory.instance.money < amount)
                return;

            m_playerInventory.instance.money -= amount;
            m_inventory.money += amount;
            depositWindow.Hide();
            PlayAudio(depositClip);
        }

        protected virtual void OnWithdraw()
        {
            var amount = int.Parse(withdrawField.text);

            if (m_inventory.money < amount)
                return;

            m_inventory.money -= amount;
            m_playerInventory.instance.money += amount;
            withdrawWindow.Hide();
            PlayAudio(withdrawClip);
        }

        protected virtual void Start()
        {
            SetInventory(GameStash.instance.GetInventory(stashIndex));
            InitializeWindow();
            InitializeInventory();
            InitializeActions();
        }

        protected virtual void OnEnable()
        {
            m_audio.PlayUiEffect(showClip);
            m_playerInventoryGUI.GetComponent<GUIWindow>().SafeCall(w => w.Show());
        }

        protected virtual void OnDisable()
        {
            depositWindow.Hide();
            withdrawWindow.Hide();

            if (!m_window.isOpen)
                m_playerInventoryGUI.GetComponent<GUIWindow>().SafeCall(w => w.Hide());
        }
    }
}
