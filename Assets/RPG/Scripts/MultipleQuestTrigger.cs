using UnityEngine;

namespace PLAYERTWO.ARPGProject
{
    [RequireComponent(typeof(Collider))]
    [AddComponentMenu("PLAYER TWO/ARPG Project/Quest/Multiple Quest Trigger")]
    public class MultipleQuestTrigger : MonoBehaviour
    {
        [Tooltip("The Quest you want to trigger completion.")]
        public Quest quest;

        protected Collider m_collider;

        protected QuestsManager m_manager => Game.instance.quests;

        protected virtual void InitializeCollider()
        {
            m_collider = GetComponent<Collider>();
            m_collider.isTrigger = true;
        }

        protected virtual void Start() => InitializeCollider();

        protected virtual void OnTriggerEnter(Collider other)
        {
            if (!other.IsPlayer())
                return;

            m_manager.Trigger(quest);
        }
    }
}
