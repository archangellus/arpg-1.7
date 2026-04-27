using System.Linq;
using UnityEngine;
using UnityEngine.Events;

namespace PLAYERTWO.ARPGProject
{
    [AddComponentMenu("PLAYER TWO/ARPG Project/Quest/Quest Giver")]
    public class QuestGiver : Interactive
    {
        public enum State
        {
            None,
            QuestAvailable,
            QuestInProgress,
        }

        [Header("Quest Giver Settings")]
        [Tooltip("The name of the Quest Giver.")]
        public string questGiverName = "Quest Giver";

        [Tooltip("The list of Quests this Quest Giver offers to the Player.")]
        public Quest[] quests;

        [Space(10)]
        public UnityEvent<State> onStateChange;

        /// <summary>
        /// The current state of the Quest Giver.
        /// </summary>
        public State state { get; protected set; } = State.None;

        protected QuestsManager m_manager => Game.instance.quests;

        protected override void Start()
        {
            base.Start();
            InitializeCallbacks();
            InitializeStates();
        }

        protected virtual void InitializeCallbacks()
        {
            m_manager.onQuestAdded += OnQuestAdded;
            m_manager.onQuestCompleted += OnQuestCompleted;
            m_manager.onQuestRemoved += OnQuestRemoved;
        }

        protected virtual void InitializeStates()
        {
            QuestInstance instance;
            var completedQuests = quests.Count(quest =>
                m_manager.TryGetQuest(quest, out instance) && instance.completed
            );

            if (completedQuests == quests.Length)
            {
                ChangeStateTo(State.None);
                return;
            }

            var state = quests.Any(quest =>
                m_manager.TryGetQuest(quest, out instance) && !instance.completed
            )
                ? State.QuestInProgress
                : State.QuestAvailable;

            ChangeStateTo(state);
        }

        /// <summary>
        /// Returns the first non-completed Quest from the quests array.
        /// </summary>
        public virtual Quest CurrentQuest(out QuestInstance instance)
        {
            foreach (var quest in quests)
            {
                if (!m_manager.TryGetQuest(quest, out instance) || !instance.completed)
                    return quest;
            }

            instance = null;
            return null;
        }

        protected override void OnInteract(object _)
        {
            CompleteReturningQuests();
            ShowQuestWindow();
        }

        protected virtual void CompleteReturningQuests()
        {
            foreach (var quest in quests)
            {
                if (
                    m_manager.TryGetQuest(quest, out var instance)
                    && !instance.completed
                    && instance.returningToGiver
                )
                {
                    GUIWindowsManager
                        .instance.GetDialogue()
                        .ShowAndFill(
                            questGiverName,
                            quest.completeDialogue,
                            () => instance.NextState()
                        );
                }
            }
        }

        protected virtual void ShowQuestWindow()
        {
            var current = CurrentQuest(out var instance);
            if (current && (instance == null || !instance.returningToGiver))
            {
                GUIWindowsManager
                    .instance.GetDialogue()
                    .ShowAndFill(
                        questGiverName,
                        current.startDialogue,
                        () => GUIWindowsManager.instance.quest.SetQuest(current)
                    );
            }
        }

        /// <summary>
        /// Returns true if the QuestInstance matches any quest in the quests array.
        /// </summary>
        /// <param name="instance">The QuestInstance to check.</param>
        protected virtual bool MatchesQuest(QuestInstance instance)
        {
            if (quests.Length == 0 || !quests.Contains(instance.data))
                return false;

            return true;
        }

        protected virtual void ChangeStateTo(State state)
        {
            this.state = state;
            onStateChange.Invoke(state);
        }

        protected virtual void OnQuestAdded(QuestInstance instance)
        {
            if (!MatchesQuest(instance))
                return;

            ChangeStateTo(State.QuestInProgress);
        }

        protected virtual void OnQuestCompleted(QuestInstance instance)
        {
            if (!MatchesQuest(instance))
                return;

            ChangeStateTo(CurrentQuest(out _) ? State.QuestAvailable : State.None);
        }

        protected virtual void OnQuestRemoved(QuestInstance instance)
        {
            if (!MatchesQuest(instance))
                return;

            ChangeStateTo(State.QuestAvailable);
        }
    }
}
