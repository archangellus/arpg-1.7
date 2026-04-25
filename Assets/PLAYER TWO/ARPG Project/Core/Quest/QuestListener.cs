using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace PLAYERTWO.ARPGProject
{
    [AddComponentMenu("PLAYER TWO/ARPG Project/Quest/Quest Listener")]
    public class QuestListener : MonoBehaviour
    {
        [Tooltip(
            "If true, the listener will trigger all the 'On Start' and 'On Complete' events when the Player's quests are loaded."
        )]
        public bool triggerOnStart = true;

        [Tooltip("The list of Quests this Quest Listener must be keeping track of.")]
        public List<Quest> quests;

        public UnityEvent onStart;
        public UnityEvent onComplete;
        public UnityEvent onProgressChanged;
        public UnityEvent onQuestRemoved;

        protected QuestsManager m_manager => Game.instance.currentCharacter.quests.manager;

        protected virtual void InitializeCallbacks()
        {
            m_manager.onQuestAdded += OnQuestAdded;
            m_manager.onQuestCompleted += OnQuestCompleted;
            m_manager.onProgressChanged += OnProgressChanged;
            m_manager.onQuestRemoved += OnQuestRemoved;
        }

        protected virtual void HandleInitialQuests()
        {
            if (!triggerOnStart)
                return;

            foreach (var instance in m_manager.list)
            {
                OnQuestAdded(instance);

                if (instance.completed)
                    OnQuestCompleted(instance);
            }
        }

        protected virtual void OnQuestAdded(QuestInstance instance)
        {
            if (!quests.Contains(instance.data))
                return;

            onStart.Invoke();
        }

        protected virtual void OnQuestCompleted(QuestInstance instance)
        {
            if (!quests.Contains(instance.data))
                return;

            onComplete.Invoke();
        }

        protected virtual void OnProgressChanged(QuestInstance instance)
        {
            if (!quests.Contains(instance.data))
                return;

            onProgressChanged.Invoke();
        }

        protected virtual void OnQuestRemoved(QuestInstance instance)
        {
            if (!quests.Contains(instance.data))
                return;

            onQuestRemoved.Invoke();
        }

        protected virtual void Start()
        {
            InitializeCallbacks();
            HandleInitialQuests();
        }
    }
}
