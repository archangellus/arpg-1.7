using UnityEngine;

namespace PLAYERTWO.ARPGProject
{
    [RequireComponent(typeof(Collider))]
    [AddComponentMenu("PLAYER TWO/ARPG Project/Quest/Multiple Quest Trigger")]
    public class MultipleQuestTrigger : MonoBehaviour
    {
        [Tooltip("The Quest you want to trigger completion.")]
        public Quest quest;

        [Tooltip("Optional Quest to automatically start when the trigger Quest is completed while the Player is inside this trigger.")]
        public Quest nextQuest;

        protected Collider m_collider;
        protected bool m_playerInside;

        protected QuestsManager m_manager => Game.instance.quests;

        protected virtual void InitializeCollider()
        {
            m_collider = GetComponent<Collider>();
            m_collider.isTrigger = true;
        }

        protected virtual void Start()
        {
            InitializeCollider();
            m_manager.onQuestCompleted += OnQuestCompleted;
        }

        protected virtual void OnDestroy()
        {
            if (Game.instance)
                m_manager.onQuestCompleted -= OnQuestCompleted;
        }

        protected virtual void OnTriggerEnter(Collider other)
        {
            if (!other.IsPlayer())
                return;

            m_playerInside = true;

            if (m_manager.TryGetQuest(quest, out var instance) && instance.completed)
            {
                TryStartNextQuest();
                return;
            }

            m_manager.Trigger(quest);
        }

        protected virtual void OnTriggerExit(Collider other)
        {
            if (!other.IsPlayer())
                return;

            m_playerInside = false;
        }

        protected virtual void OnQuestCompleted(QuestInstance instance)
        {
            if (!m_playerInside || instance.data != quest)
                return;

            TryStartNextQuest();
        }

        protected virtual void TryStartNextQuest()
        {
            if (!nextQuest || m_manager.ContainsQuest(nextQuest))
                return;

            m_manager.AddQuest(nextQuest);
        }
    }
}