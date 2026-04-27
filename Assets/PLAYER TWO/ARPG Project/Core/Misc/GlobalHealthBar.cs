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
        protected float m_lastUpdateTime;

        protected virtual void Start()
        {
            active = enabled;
            gameObject.SetActive(false);
        }

        protected virtual void Update()
        {
            if (!active || (m_entity && Time.time > m_lastUpdateTime + visibleDuration))
                Disable();
        }

        public virtual void Assign(Entity entity)
        {
            if (!active || m_entity == entity)
                return;

            if (m_entity)
                m_entity.stats.onHealthChanged.RemoveListener(UpdateHealthBar);

            m_entity = entity;
            nameText.text = entity.entityName;
            m_entity.stats.onHealthChanged.AddListener(UpdateHealthBar);
            UpdateHealthBar();
        }

        public static void AssignToInstance(Entity entity)
        {
            if (instance)
                instance.Assign(entity);
        }

        protected virtual void UpdateHealthBar()
        {
            if (!active || !m_entity)
                return;

            gameObject.SetActive(true);
            fillBar.fillAmount = m_entity.stats.GetHealthPercent();
            m_lastUpdateTime = Time.time;
        }

        protected virtual void Disable()
        {
            if (m_entity)
                m_entity.stats.onHealthChanged.RemoveListener(UpdateHealthBar);

            m_entity = null;
            gameObject.SetActive(false);
        }
    }
}
