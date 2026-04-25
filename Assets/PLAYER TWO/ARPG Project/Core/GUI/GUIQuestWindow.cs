using UnityEngine;
using UnityEngine.UI;

namespace PLAYERTWO.ARPGProject
{
    [RequireComponent(typeof(GUIWindow))]
    [AddComponentMenu("PLAYER TWO/ARPG Project/GUI/GUI Quest Window")]
    public class GUIQuestWindow : MonoBehaviour
    {
        [Header("Text References")]
        [Tooltip("A reference to the Text component that represents the Quest title.")]
        public Text title;

        [Tooltip("A reference to the Text component that represents the Quest description.")]
        public Text description;

        [Tooltip("A reference to the Text component that represents the Quest objective.")]
        public Text objective;

        [Tooltip("A reference to the Text component that represents the Quest rewards.")]
        public Text rewards;

        [Header("Containers Reference")]
        [Tooltip(
            "A reference to the GameObject that contains the actions from Quest when the window from a giver."
        )]
        public GameObject giverActions;

        [Tooltip(
            "A reference to the GameObject that contains the actions from Quest when the window is from a Log."
        )]
        public GameObject logActions;

        [Header("Actions Reference")]
        [Tooltip("A reference to the Button used to accept the Quest.")]
        public Button accept;

        [Tooltip("A reference to the Button used to decline the Quest.")]
        public Button decline;

        [Tooltip("A reference to the Button used to cancel the Quest.")]
        public Button cancel;

        protected GUIWindow m_window;

        /// <summary>
        /// Returns the GUI Window component associated to this GUI Quest Window.
        /// </summary>
        public GUIWindow window
        {
            get
            {
                if (!m_window)
                    m_window = GetComponent<GUIWindow>();

                return m_window;
            }
        }

        /// <summary>
        /// Returns the Quest this GUI Quest Window represents.
        /// </summary>
        public Quest quest { get; protected set; }

        protected virtual void InitializeCallbacks()
        {
            accept.onClick.AddListener(Accept);
            decline.onClick.AddListener(Decline);
            cancel.onClick.AddListener(Cancel);
        }

        /// <summary>
        /// Accepts the current Quest.
        /// </summary>
        public virtual void Accept()
        {
            if (!quest)
                return;

            window.Toggle();
            Game.instance.quests.AddQuest(quest);
        }

        /// <summary>
        /// Declines the current Quest.
        /// </summary>
        public virtual void Decline() => window.Toggle();

        /// <summary>
        /// Cancels the current Quest.
        /// </summary>
        public virtual void Cancel()
        {
            if (!quest)
                return;

            window.Toggle();
            Game.instance.quests.RemoveQuest(quest);
        }

        /// <summary>
        /// Sets the current Quest.
        /// </summary>
        /// <param name="quest">The Quest you want to set.</param>
        public virtual void SetQuest(Quest quest)
        {
            if (!quest)
                return;

            this.quest = quest;
            window.Show();
            UpdateTexts();
            UpdateButtons();
        }

        protected virtual void UpdateTexts()
        {
            title.text = quest.title;
            description.text = quest.description;
            objective.text = quest.objective;
            rewards.text = quest.GetRewardText();
        }

        protected virtual void UpdateButtons()
        {
            if (!quest)
                return;

            var isActive = Game.instance.quests.IsQuestActive(quest);
            var isLog = Game.instance.quests.ContainsQuest(quest);
            logActions.SetActive(isLog && isActive);
            giverActions.SetActive(!isLog);
        }

        protected virtual void Start() => InitializeCallbacks();

        protected virtual void OnEnable() => UpdateButtons();
    }
}
