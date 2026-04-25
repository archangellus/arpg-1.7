using System.Collections;
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

        public enum ShowMode
        {
            OnDamage,
            OnHighlight,
        }

        [Header("Settings")]
        [Tooltip("The type of health bar to use for this entity.")]
        public BarType barType = BarType.Global;

        [Tooltip(
            "Controls when the health bar is shown. Falls back to OnDamage on mobile platforms."
        )]
        public ShowMode showMode = ShowMode.OnHighlight;

        [Tooltip(
            "Duration in seconds the individual bar stays visible after taking damage (OnDamage mode only)."
        )]
        public float visibleDuration = 3f;

        [Tooltip("Offset of the health bar from the entity's position in screen space.")]
        public Vector2 offset = new(0, 50f);

        protected Entity m_entity;
        protected GUIHealthBar m_individualBar;

        protected virtual void Start()
        {
            InitializeEntity();
            InitializeShowMode();
            InitializeHealthBar();
            InitializeCallbacks();
        }

        protected virtual void InitializeEntity()
        {
            m_entity = GetComponent<Entity>();
        }

        protected virtual void InitializeShowMode()
        {
#if UNITY_ANDROID || UNITY_IOS
            showMode = ShowMode.OnDamage;
#endif
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
                m_individualBar.gameObject.SetActive(false);
                GUIHealthBarManager.instance.AddHealthBar(m_individualBar);
            }
        }

        protected virtual void InitializeCallbacks()
        {
            m_entity.onDie.AddListener(OnDie);
            m_entity.onRevive.AddListener(OnRevive);
            m_entity.stats.onHealthChanged.AddListener(OnHealthChanged);

            if (showMode == ShowMode.OnHighlight)
                m_entity.onHighlightChanged.AddListener(OnHighlightChanged);
        }

        protected virtual void OnDie()
        {
            StopCoroutine(nameof(HideIndividualBarDelayed));

            if (m_individualBar)
                m_individualBar.gameObject.SetActive(false);
        }

        protected virtual void OnRevive()
        {
            if (m_individualBar && showMode == ShowMode.OnHighlight && m_entity.isHighlighted)
                m_individualBar.gameObject.SetActive(true);
        }

        protected virtual void OnHealthChanged()
        {
            if (m_individualBar)
            {
                m_individualBar.SetHealth(m_entity.stats.GetHealthPercent());

                if (showMode == ShowMode.OnDamage)
                {
                    m_individualBar.gameObject.SetActive(!m_entity.isDead);
                    StopCoroutine(nameof(HideIndividualBarDelayed));
                    StartCoroutine(nameof(HideIndividualBarDelayed));
                }
            }
            else if (barType == BarType.Global && m_entity.stats.initialized)
            {
                if (showMode == ShowMode.OnDamage || m_entity.isHighlighted)
                    GlobalHealthBar.AssignToInstance(m_entity);
            }
        }

        protected virtual void OnHighlightChanged(bool highlighted)
        {
            if (barType == BarType.Individual && m_individualBar)
                m_individualBar.gameObject.SetActive(highlighted && !m_entity.isDead);
            else if (barType == BarType.Global && highlighted)
                GlobalHealthBar.AssignToInstance(m_entity);
        }

        protected virtual IEnumerator HideIndividualBarDelayed()
        {
            yield return new WaitForSeconds(visibleDuration);

            if (m_individualBar)
                m_individualBar.gameObject.SetActive(false);
        }

        protected virtual void OnDisable()
        {
            if (m_individualBar)
                m_individualBar.gameObject.SetActive(false);
        }

        protected virtual void OnEnable()
        {
            if (m_individualBar && showMode == ShowMode.OnHighlight && m_entity.isHighlighted)
                m_individualBar.gameObject.SetActive(true);
        }
    }
}
