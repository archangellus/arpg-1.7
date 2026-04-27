using UnityEngine;
using System.Collections;

namespace PLAYERTWO.ARPGProject
{
    [RequireComponent(typeof(Entity))]
    [AddComponentMenu("PLAYER TWO/ARPG Project/Quest/Quest Enabled Enemy")]
    public class QuestEnabledEnemy : MonoBehaviour
    {
        [Header("Quest Enemy Settings")]
        [Tooltip(
            "If true, this Game Object will stay disabled until the Player accepts the Quest with the progress key of this enemy."
        )]
        public bool showOnlyWhenQuestIsActive = true;

        [Tooltip("The enemy key that matches the progress key of the Quest.")]
        public string enemyKey;

        protected Entity m_entity;
        protected QuestsManager m_quests;
        protected Coroutine m_initializeRoutine;
        protected Collider[] m_colliders;
        protected bool m_waitingForQuestState;

        protected virtual void InitializeEntity()
        {
            m_entity = GetComponent<Entity>();
            m_entity.onDie.AddListener(AddQuestProgression);
            m_colliders = GetComponentsInChildren<Collider>(true);
        }

        protected virtual void InitializeCallbacks()
        {
            if (m_quests == null)
                return;

            m_quests.onQuestAdded += OnQuestChanged;
            m_quests.onQuestRemoved += OnQuestChanged;
            m_quests.onProgressChanged += OnQuestChanged;
            m_quests.onQuestCompleted += OnQuestChanged;
        }

        protected virtual void RemoveCallbacks()
        {
            if (m_quests == null)
                return;

            m_quests.onQuestAdded -= OnQuestChanged;
            m_quests.onQuestRemoved -= OnQuestChanged;
            m_quests.onProgressChanged -= OnQuestChanged;
            m_quests.onQuestCompleted -= OnQuestChanged;
        }

        protected virtual void OnQuestChanged(QuestInstance _)
        {
            HandleActive();
        }

        protected virtual void HandleActive()
        {
            if (!showOnlyWhenQuestIsActive)
                return;

            ApplyPreInitializationLock(false);
            gameObject.SetActive(false);

            foreach (var quest in Game.instance.quests.list)
            {
                if (quest.IsProgressKey(enemyKey) && !quest.completed)
                    gameObject.SetActive(true);
            }
        }

        protected virtual void ApplyPreInitializationLock(bool value)
        {
            if (!showOnlyWhenQuestIsActive || m_waitingForQuestState == value)
                return;

            m_waitingForQuestState = value;

            if (m_entity != null)
                m_entity.enabled = !value;

            if (m_colliders == null)
                return;

            foreach (var current in m_colliders)
                if (current)
                    current.enabled = !value;
        }

        public virtual void AddQuestProgression() =>
            Game.instance.quests.AddProgress(enemyKey);

        protected virtual void Start()
        {
            InitializeEntity();
            ApplyPreInitializationLock(true);
            m_initializeRoutine = StartCoroutine(InitializeQuestTracking());
        }

        protected virtual IEnumerator InitializeQuestTracking()
        {
            while (Game.instance == null || Game.instance.quests == null)
                yield return null;

            m_quests = Game.instance.quests;
            InitializeCallbacks();
            HandleActive();
            m_initializeRoutine = null;
        }

        protected virtual void OnDestroy()
        {
            if (m_initializeRoutine != null)
                StopCoroutine(m_initializeRoutine);

            RemoveCallbacks();
        }
    }
}