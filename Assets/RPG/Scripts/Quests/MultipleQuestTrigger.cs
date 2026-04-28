using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

namespace PLAYERTWO.ARPGProject
{
    [RequireComponent(typeof(Collider))]
    [AddComponentMenu("PLAYER TWO/ARPG Project/Quest/Multiple Quest Trigger")]
    public class MultipleQuestTrigger : MonoBehaviour
    {
        [Tooltip("Quests handled by this trigger (one quest per element).")]
        public List<Quest> quests = new();

        [FormerlySerializedAs("quest")]
        [SerializeField, HideInInspector]
        protected Quest m_legacyQuest;

        [FormerlySerializedAs("nextQuest")]
        [SerializeField, HideInInspector]
        protected Quest m_legacyNextQuest;

        protected Collider m_collider;
        protected bool m_playerInside;

        protected QuestsManager m_manager => Game.instance ? Game.instance.quests : null;

        protected virtual void UpgradeLegacyData()
        {
            if (quests.Count > 0 || !m_legacyQuest)
                return;

            quests.Add(m_legacyQuest);

            if (m_legacyNextQuest)
                quests.Add(m_legacyNextQuest);

            m_legacyQuest = null;
            m_legacyNextQuest = null;
        }

        protected virtual void InitializeCollider()
        {
            m_collider = GetComponent<Collider>();
            m_collider.isTrigger = true;
        }

        protected virtual void Start()
        {
            UpgradeLegacyData();
            InitializeCollider();

            if (m_manager != null)
                m_manager.onQuestCompleted += OnQuestCompleted;
        }

        protected virtual void OnValidate()
        {
            UpgradeLegacyData();
        }

        protected virtual void OnDestroy()
        {
            if (m_manager != null)
                m_manager.onQuestCompleted -= OnQuestCompleted;
        }

        protected virtual void OnTriggerEnter(Collider other)
        {
            if (!other.IsPlayer() || m_manager == null)
                return;

            m_playerInside = true;

            for (var i = 0; i < quests.Count; i++)
            {
                var quest = quests[i];

                if (!quest)
                    continue;

                if (m_manager.TryGetQuest(quest, out var instance) && instance.completed)
                {
                    TryStartNextQuest(i);
                    continue;
                }

                m_manager.Trigger(quest);
                return;
            }
        }

        protected virtual void OnTriggerExit(Collider other)
        {
            if (!other.IsPlayer())
                return;

            m_playerInside = false;
        }

        protected virtual void OnQuestCompleted(QuestInstance instance)
        {
            if (!m_playerInside || m_manager == null)
                return;

            for (var i = 0; i < quests.Count; i++)
            {
                if (quests[i] != instance.data)
                    continue;

                TryStartNextQuest(i);
                return;
            }
        }

        protected virtual void TryStartNextQuest(int questIndex)
        {
            var nextQuest = GetNextQuest(questIndex);

            if (!nextQuest || m_manager.ContainsQuest(nextQuest))
                return;

            m_manager.AddQuest(nextQuest);
        }

        protected virtual Quest GetNextQuest(int questIndex)
        {
            for (var i = questIndex + 1; i < quests.Count; i++)
            {
                if (quests[i])
                    return quests[i];
            }

            return null;
        }
    }
}
