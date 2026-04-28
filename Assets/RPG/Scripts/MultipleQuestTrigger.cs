using System.Collections.Generic;
using UnityEngine;

namespace PLAYERTWO.ARPGProject
{
    [RequireComponent(typeof(Collider))]
    [AddComponentMenu("PLAYER TWO/ARPG Project/Quest/Multiple Quest Trigger")]
    public class MultipleQuestTrigger : MonoBehaviour
    {
        [Tooltip("The Quest sequence to start. Each quest starts after the previous one is completed.")]
        public List<Quest> quests = new();

        protected Collider m_collider;
        protected int m_currentQuestIndex = -1;
        protected bool m_sequenceStarted;

        protected QuestsManager m_manager => Game.instance.quests;

        protected virtual void OnEnable()
        {
            if (Game.instance)
                m_manager.onQuestCompleted += OnQuestCompleted;
        }

        protected virtual void OnDisable()
        {
            if (Game.instance)
                m_manager.onQuestCompleted -= OnQuestCompleted;
        }

        protected virtual void InitializeCollider()
        {
            m_collider = GetComponent<Collider>();
            m_collider.isTrigger = true;
        }

        protected virtual void Start() => InitializeCollider();

        protected virtual void OnTriggerEnter(Collider other)
        {
            if (!other.IsPlayer() || m_sequenceStarted)
                return;

            m_sequenceStarted = true;
            StartNextQuest();
        }

        protected virtual void OnQuestCompleted(QuestInstance instance)
        {
            if (!m_sequenceStarted || m_currentQuestIndex < 0 || m_currentQuestIndex >= quests.Count)
                return;

            if (instance.data != quests[m_currentQuestIndex])
                return;

            StartNextQuest();
        }

        protected virtual void StartNextQuest()
        {
            var nextQuest = FindNextQuest();

            if (!nextQuest)
                return;

            m_manager.AddQuest(nextQuest);

            if (m_manager.TryGetQuest(nextQuest, out var instance) && instance.completed)
                StartNextQuest();
        }

        protected virtual Quest FindNextQuest()
        {
            for (var i = m_currentQuestIndex + 1; i < quests.Count; i++)
            {
                var quest = quests[i];

                if (!quest)
                    continue;

                m_currentQuestIndex = i;
                return quest;
            }

            return null;
        }
    }
}
