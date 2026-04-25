using UnityEngine;

namespace PLAYERTWO.ARPGProject
{
    [RequireComponent(typeof(Entity))]
    [AddComponentMenu("PLAYER TWO/ARPG Project/Quest/Quest Enemy")]
    public class QuestEnemy : MonoBehaviour
    {
        [Tooltip("The enemy key that matches the progress key of the Quest.")]
        public string enemyKey;

        protected Entity m_entity;

        protected virtual void InitializeEntity()
        {
            m_entity = GetComponent<Entity>();
            m_entity.onDie.AddListener(AddQuestProgression);
        }

        public virtual void AddQuestProgression() =>
            Game.instance.quests.AddProgress(enemyKey);

        protected virtual void Start() => InitializeEntity();
    }
}
