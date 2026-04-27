using UnityEngine;

namespace PLAYERTWO.ARPGProject
{
    [RequireComponent(typeof(Entity))]
    [AddComponentMenu("PLAYER TWO/ARPG Project/Quest/Quest Enemy")]
    public class QuestEnemy : MonoBehaviour
    {
        [Header("Quest Enemy Settings")]
        [Tooltip(
            "If true, this Game Object will stay disabled until the Player accepts the Quest with the progress key of this enemy."
        )]
        public bool showOnlyWhenQuestIsActive = true;

        [Tooltip("The enemy key that matches the progress key of the Quest.")]
        public string enemyKey;

        protected Entity m_entity;

        protected virtual void InitializeEntity()
        {
            m_entity = GetComponent<Entity>();
            m_entity.onDie.AddListener(AddQuestProgression);
        }

        protected virtual void InitializeCallbacks()
        {
            Game.instance.quests.onQuestAdded += _ => HandleActive();
            Game.instance.quests.onQuestRemoved += _ => HandleActive();
        }

        protected virtual void HandleActive()
        {
            if (!showOnlyWhenQuestIsActive)
                return;

            gameObject.SetActive(false);

            foreach (var quest in Game.instance.quests.list)
            {
                if (quest.IsProgressKey(enemyKey) && !quest.completed)
                    gameObject.SetActive(true);
            }
        }

        public virtual void AddQuestProgression() =>
            Game.instance.quests.AddProgress(enemyKey);

        protected virtual void Start()
        {
            InitializeEntity();
            InitializeCallbacks();
            HandleActive();
        }
    }
}