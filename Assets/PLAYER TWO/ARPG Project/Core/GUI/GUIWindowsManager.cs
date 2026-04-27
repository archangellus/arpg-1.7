using UnityEngine;

namespace PLAYERTWO.ARPGProject
{
    [AddComponentMenu("PLAYER TWO/ARPG Project/GUI/GUI Windows Manager")]
    public class GUIWindowsManager : Singleton<GUIWindowsManager>
    {
        [Tooltip("A reference to the GUI Skills Manager.")]
        public GUISkillsManager skills;

        [Tooltip("A reference to the GUI Stats Manager.")]
        public GUIStatsManager stats;

        [Tooltip("A reference to the GUI Player Inventory.")]
        public GUIWindow inventoryWindow;

        [Tooltip("A reference to the GUI Quest Window.")]
        public GUIQuestWindow quest;

        [Tooltip("A reference to the GUI Quest Log.")]
        public GUIQuestLog questLog;

        [Tooltip("A reference to the GUI Blacksmith.")]
        public GUIBlacksmith blacksmith;

        [Tooltip("A reference to the Stash Window.")]
        public GUIWindow stashWindow;

        [Tooltip("A reference to the GUI Merchant.")]
        public GUIWindow merchantWindow;

        [Tooltip("A reference to the GUI Waypoints Window.")]
        public GUIWindow waypointsWindow;

        [Tooltip("A reference to the GUI Information.")]
        public GUIWindow informationWindow;

        [Tooltip("A reference to the GUI Dialogue Window.")]
        public GUIWindow dialogueWindow;

        [Tooltip("References the Map Window.")]
        public GUIWindow mapWindow;

        [Header("Audio Settings")]
        [Tooltip("The Audio Clip that plays when opening windows.")]
        public AudioClip openClip;

        [Tooltip("The Audio Clip that plays when closing windows.")]
        public AudioClip closeClip;

        protected GameAudio m_audio => GameAudio.instance;

        /// <summary>
        /// Returns the reference to the GUI Player Inventory.
        /// </summary>
        public GUIPlayerInventory GetInventory()
        {
            if (!inventoryWindow)
                return null;

            return inventoryWindow.GetComponent<GUIPlayerInventory>();
        }

        /// <summary>
        /// Returns the reference to the GUI Merchant.
        /// </summary>
        public GUIMerchant GetMerchant()
        {
            if (!merchantWindow)
                return null;

            return merchantWindow.GetComponent<GUIMerchant>();
        }

        /// <summary>
        /// Returns the reference to the GUI Information.
        /// </summary>
        public GUIInformation GetInformation()
        {
            if (!informationWindow)
                return null;

            return informationWindow.GetComponent<GUIInformation>();
        }

        /// <summary>
        /// Returns the reference to the GUI Dialogue.
        /// </summary>
        public GUIDialogue GetDialogue()
        {
            if (!dialogueWindow)
                return null;

            return dialogueWindow.GetComponent<GUIDialogue>();
        }

        protected virtual void Start()
        {
            var windows = GetComponentsInChildren<GUIWindow>(true);

            if (!m_audio)
                return;

            foreach (var window in windows)
            {
                window.onOpen.AddListener(() => m_audio.PlayUiEffect(openClip));
                window.onClose.AddListener(() => m_audio.PlayUiEffect(closeClip));
            }
        }
    }
}
