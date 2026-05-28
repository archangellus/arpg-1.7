using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PLAYERTWO.ARPGProject
{
    [AddComponentMenu("PLAYER TWO/ARPG Project/Misc/Global Health Bar")]
    public class GlobalHealthBar : Singleton<GlobalHealthBar>
    {
        [Header("Components")]
        [Tooltip("The text component used to display the entity's name.")]
        public TMP_Text nameText;

        [Tooltip("The image component used to display the health bar fill.")]
        public Image fillBar;

        [Header("Settings")]
        [Tooltip("Whether the health bar is active and can be shown.")]
        public bool active = true;

        [Tooltip("The duration (in seconds) the health bar remains visible after taking damage.")]
        public float visibleDuration = 1f;

        protected Entity m_entity;
        protected Destructible m_destructible;
        protected float m_lastUpdateTime;
        protected bool m_persistent;

        protected virtual void Start()
        {
            active = enabled;
            gameObject.SetActive(false);
        }

        protected virtual void Update()
        {
            if (!active || (!m_persistent && Time.time > m_lastUpdateTime + visibleDuration))
                Disable();
        }

        public virtual void Assign(Entity entity)
        {
            Assign(entity, false);
        }

        public virtual void Assign(Entity entity, bool persistent)
        {
            if (!active || m_entity == entity)
            {
                m_persistent = persistent;
                UpdateHealthBar();
                return;
            }

            if (m_entity)
                m_entity.stats.onHealthChanged.RemoveListener(UpdateHealthBar);

            if (m_destructible)
            {
                m_destructible.onHitPointsChanged.RemoveListener(UpdateHealthBar);
                m_destructible = null;
            }

            m_entity = entity;
            m_persistent = persistent;
            nameText.text = entity.entityName;
            m_entity.stats.onHealthChanged.AddListener(UpdateHealthBar);
            UpdateHealthBar();
        }

        public static void AssignToInstance(Entity entity)
        {
            if (instance)
                instance.Assign(entity);
        }

        public static void AssignToInstance(Entity entity, bool persistent)
        {
            if (instance)
                instance.Assign(entity, persistent);
        }

        public virtual void Assign(Destructible destructible, string displayName = null)
        {
            Assign(destructible, displayName, false);
        }

        public virtual void Assign(
            Destructible destructible,
            string displayName = null,
            bool persistent = false
        )
        {
            if (!active || m_destructible == destructible)
            {
                m_persistent = persistent;
                UpdateHealthBar();
                return;
            }

            if (m_entity)
            {
                m_entity.stats.onHealthChanged.RemoveListener(UpdateHealthBar);
                m_entity = null;
            }

            if (m_destructible)
                m_destructible.onHitPointsChanged.RemoveListener(UpdateHealthBar);

            m_destructible = destructible;
            m_persistent = persistent;
            nameText.text = string.IsNullOrEmpty(displayName)
                ? destructible.gameObject.name
                : displayName;
            m_destructible.onHitPointsChanged.AddListener(UpdateHealthBar);
            UpdateHealthBar();
        }

        public static void AssignToInstance(Destructible destructible, string displayName = null)
        {
            if (instance)
                instance.Assign(destructible, displayName);
        }

        public static void AssignToInstance(
            Destructible destructible,
            string displayName,
            bool persistent
        )
        {
            if (instance)
                instance.Assign(destructible, displayName, persistent);
        }

        public virtual void Clear(Entity entity)
        {
            if (m_entity == entity)
                Disable();
        }

        public static void ClearInstance(Entity entity)
        {
            if (instance)
                instance.Clear(entity);
        }

        public virtual void Clear(Destructible destructible)
        {
            if (m_destructible == destructible)
                Disable();
        }

        public static void ClearInstance(Destructible destructible)
        {
            if (instance)
                instance.Clear(destructible);
        }

        protected virtual void UpdateHealthBar()
        {
            if (!active)
                return;

            if (m_destructible)
            {
                gameObject.SetActive(true);
                fillBar.fillAmount = m_destructible.GetHitPointsPercent();
                m_lastUpdateTime = Time.time;
            }
            else if (m_entity)
            {
                gameObject.SetActive(true);
                fillBar.fillAmount = m_entity.stats.GetHealthPercent();
                m_lastUpdateTime = Time.time;
            }
        }

        protected virtual void Disable()
        {
            if (m_entity)
            {
                m_entity.stats.onHealthChanged.RemoveListener(UpdateHealthBar);
                m_entity = null;
            }

            if (m_destructible)
            {
                m_destructible.onHitPointsChanged.RemoveListener(UpdateHealthBar);
                m_destructible = null;
            }

            gameObject.SetActive(false);
            m_persistent = false;
        }
    }
}
