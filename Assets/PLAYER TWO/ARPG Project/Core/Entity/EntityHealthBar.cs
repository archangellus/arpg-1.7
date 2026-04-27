using UnityEngine;

namespace PLAYERTWO.ARPGProject
{
    [RequireComponent(typeof(Entity))]
    [AddComponentMenu("PLAYER TWO/ARPG Project/Entity/Entity Health Bar")]
    public class EntityHealthBar : MonoBehaviour
    {
        public enum BarType
        {
            Global,
            Individual,
        }

        [Header("Settings")]
        [Tooltip("The type of health bar to use for this entity.")]
        public BarType barType = BarType.Global;

        [Tooltip("Offset of the health bar from the entity's position in screen space.")]
        public Vector2 offset = new(0, 50f);

        protected Entity m_entity;
        protected GUIHealthBar m_individualBar;

        protected virtual void Start()
        {
            InitializeEntity();
            InitializeHealthBar();
            InitializeCallbacks();
        }

        protected virtual void InitializeEntity()
        {
            m_entity = GetComponent<Entity>();
        }

        protected virtual void InitializeHealthBar()
        {
            if (barType != BarType.Individual)
                return;

            m_individualBar = GUIHealthBarManager.instance.InstantiateHealthBar(m_entity);

            if (m_individualBar)
            {
                m_individualBar.target = transform;
                m_individualBar.offset = offset;
                m_individualBar.UpdatePosition();
                m_individualBar.SetHealth(m_entity.stats.GetHealthPercent());
                GUIHealthBarManager.instance.AddHealthBar(m_individualBar);
            }
        }

        protected virtual void InitializeCallbacks()
        {
            m_entity.onDie.AddListener(OnDie);
            m_entity.onRevive.AddListener(OnRevive);
            m_entity.stats.onHealthChanged.AddListener(OnHealthChanged);
        }

        protected virtual void OnDie()
        {
            if (m_individualBar)
                m_individualBar.gameObject.SetActive(false);
        }

        protected virtual void OnRevive()
        {
            if (m_individualBar)
                m_individualBar.gameObject.SetActive(true);
        }

        protected virtual void OnHealthChanged()
        {
            if (m_individualBar)
                m_individualBar.SetHealth(m_entity.stats.GetHealthPercent());
            else if (barType == BarType.Global)
                GlobalHealthBar.AssignToInstance(m_entity);
        }

        protected virtual void OnDisable()
        {
            if (m_individualBar)
                m_individualBar.gameObject.SetActive(false);
        }

        protected virtual void OnEnable()
        {
            if (m_individualBar)
                m_individualBar.gameObject.SetActive(true);
        }
    }
}
