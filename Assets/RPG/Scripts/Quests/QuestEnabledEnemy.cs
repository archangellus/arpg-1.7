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

        [Tooltip("Delay in seconds before disabling this enemy when no matching active quest is found.")]
        [Min(0f)]
        public float disableDelay;

        protected Entity m_entity;
        protected QuestsManager m_quests;
        protected Coroutine m_initializeRoutine;
        protected Coroutine m_disableRoutine;
        protected Coroutine m_startupRefreshRoutine;
        protected Collider[] m_colliders;
        protected Renderer[] m_renderers;
        protected bool m_waitingForQuestState;

        protected virtual void InitializeEntity()
        {
            m_entity = GetComponent<Entity>();
            m_entity.onDie.AddListener(AddQuestProgression);
            m_colliders = GetComponentsInChildren<Collider>(true);
            m_renderers = GetComponentsInChildren<Renderer>(true);
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

            if (m_quests == null)
                m_quests = Game.instance ? Game.instance.quests : null;

            if (m_quests == null)
                return;

            var shouldBeActive = false;

            foreach (var quest in m_quests.list)
            {
                if (quest.IsProgressKey(enemyKey) && !quest.completed)
                {
                    shouldBeActive = true;
                    break;
                }
            }

            if (shouldBeActive)
            {
                if (m_disableRoutine != null)
                {
                    StopCoroutine(m_disableRoutine);
                    m_disableRoutine = null;
                }

                SetEnemyEnabled(true);
            }
            else
            {
                TryDisableEnemy();
            }
        }

        protected virtual void TryDisableEnemy()
        {
           if (disableDelay <= 0f || !gameObject.activeInHierarchy)
            {
                SetEnemyEnabled(false);
                m_disableRoutine = null;
                return;
            }

            if (m_disableRoutine != null)
                StopCoroutine(m_disableRoutine);

            if (!isActiveAndEnabled)
            {
                m_disableRoutine = null;
                SetEnemyEnabled(false);
                return;
            }


            m_disableRoutine = StartCoroutine(DisableEnemyAfterDelay());
        }

        protected virtual IEnumerator DisableEnemyAfterDelay()
        {
            yield return new WaitForSeconds(disableDelay);
            SetEnemyEnabled(false);
            m_disableRoutine = null;
        }


        protected virtual void SetEnemyEnabled(bool value)
        {
            if (m_entity != null)
                m_entity.enabled = value;

            if (m_colliders != null)
                foreach (var current in m_colliders)
                    if (current)
                        current.enabled = value;

            if (m_renderers != null)
                foreach (var current in m_renderers)
                    if (current)
                        current.enabled = value;
        }

        protected virtual void ApplyPreInitializationLock(bool value)
        {
            if (!showOnlyWhenQuestIsActive || m_waitingForQuestState == value)
                return;

            m_waitingForQuestState = value;

            if (m_entity != null)
                m_entity.enabled = !value;

            if (m_colliders != null)
                foreach (var current in m_colliders)
                    if (current)
                        current.enabled = !value;

            if (m_renderers != null)
                foreach (var current in m_renderers)
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

            if (m_startupRefreshRoutine != null)
                StopCoroutine(m_startupRefreshRoutine);

            m_startupRefreshRoutine = StartCoroutine(RefreshAfterQuestLoad());
            m_initializeRoutine = null;
        }

        protected virtual IEnumerator RefreshAfterQuestLoad()
        {
            yield return null;
            HandleActive();
        }

        protected virtual void OnDestroy()
        {
            if (m_initializeRoutine != null)
                StopCoroutine(m_initializeRoutine);

            if (m_disableRoutine != null)
                StopCoroutine(m_disableRoutine);

            if (m_startupRefreshRoutine != null)
                StopCoroutine(m_startupRefreshRoutine);

            RemoveCallbacks();
        }
    }
}
