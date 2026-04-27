using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace PLAYERTWO.ARPGProject
{
    [AddComponentMenu("PLAYER TWO/ARPG Project/GUI/GUI Health Bar Manager")]
    public class GUIHealthBarManager : Singleton<GUIHealthBarManager>
    {
        [Header("Container Settings")]
        [Tooltip("If true, the container is visible on start.")]
        public bool visibleOnStart = true;

        [Tooltip("Duration of the alpha transition when showing or hiding the container.")]
        public float alphaTransitionDuration = 0.5f;

        [Header("Health Bar Prefabs")]
        [Tooltip("Prefab for the enemy health bar.")]
        public GUIHealthBar enemyHealthBarPrefab;

        [Tooltip("Prefab for the summoned entity health bar.")]
        public GUIHealthBar summonHealthBarPrefab;

        protected CanvasGroup m_canvasGroup;
        protected List<GUIHealthBar> m_healthBars = new();

        protected WaitForSeconds m_updateWait = new(1 / 60f);

        protected virtual void Start()
        {
            InitializeCanvasGroup();
            InitializeVisibility();
            StartCoroutine(UpdateRoutine());
        }

        protected virtual void InitializeCanvasGroup()
        {
            if (!TryGetComponent(out m_canvasGroup))
                m_canvasGroup = gameObject.AddComponent<CanvasGroup>();

            m_canvasGroup.blocksRaycasts = false;
            m_canvasGroup.alpha = 0;
        }

        protected virtual void InitializeVisibility()
        {
            if (visibleOnStart)
                StartCoroutine(AlphaRoutine(0, 1));
        }

        /// <summary>
        /// Shows the health bars.
        /// </summary>
        public virtual void Show()
        {
            StopAllCoroutines();
            StartCoroutine(AlphaRoutine(m_canvasGroup.alpha, 1));
        }

        /// <summary>
        /// Hides the health bars.
        /// </summary>
        public virtual void Hide()
        {
            StopAllCoroutines();
            StartCoroutine(AlphaRoutine(m_canvasGroup.alpha, 0));
        }

        /// <summary>
        /// Instantiates a health bar based on the entity's tag.
        /// </summary>
        /// <param name="entity">The entity to create a health bar for.</param>
        /// <returns>The instantiated health bar.</returns>
        public GUIHealthBar InstantiateHealthBar(Entity entity)
        {
            if (entity.IsEnemy() && enemyHealthBarPrefab != null)
                return Instantiate(enemyHealthBarPrefab, transform);
            else if (entity.IsSummoned() && summonHealthBarPrefab != null)
                return Instantiate(summonHealthBarPrefab, transform);

            return null;
        }

        /// <summary>
        /// Adds a health bar to the list.
        /// </summary>
        /// <param name="healthBar">The health bar to add.</param>
        public void AddHealthBar(GUIHealthBar healthBar)
        {
            if (!m_healthBars.Contains(healthBar) && healthBar != null)
                m_healthBars.Add(healthBar);
        }

        /// <summary>
        /// Removes a health bar from the list.
        /// </summary>
        /// <param name="healthBar">The health bar to remove.</param>
        public void RemoveHealthBar(GUIHealthBar healthBar)
        {
            if (m_healthBars.Contains(healthBar))
            {
                m_healthBars.Remove(healthBar);
                Destroy(healthBar.gameObject);
            }
        }

        protected virtual IEnumerator AlphaRoutine(float from, float to)
        {
            float elapsedTime = 0;
            float duration = alphaTransitionDuration;

            while (elapsedTime < duration)
            {
                elapsedTime += Time.deltaTime;
                m_canvasGroup.alpha = Mathf.Lerp(from, to, elapsedTime / duration);
                yield return null;
            }

            m_canvasGroup.alpha = to;
        }

        protected virtual IEnumerator UpdateRoutine()
        {
            while (true)
            {
                foreach (var healthBar in m_healthBars)
                {
                    if (healthBar.gameObject.activeInHierarchy)
                        healthBar.UpdatePosition();
                }

                yield return m_updateWait;
            }
        }
    }
}
