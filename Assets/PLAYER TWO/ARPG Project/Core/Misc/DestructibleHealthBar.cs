using System.Collections;
using UnityEngine;

namespace PLAYERTWO.ARPGProject
{
    [RequireComponent(typeof(Destructible))]
    [AddComponentMenu("PLAYER TWO/ARPG Project/Misc/Destructible Health Bar")]
    public class DestructibleHealthBar : MonoBehaviour
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
        [Tooltip("The type of health bar to use for this destructible.")]
        public BarType barType = BarType.Global;

        [Tooltip(
            "Controls when the health bar is shown. Falls back to OnDamage on mobile platforms."
        )]
        public ShowMode showMode = ShowMode.OnHighlight;

        [Tooltip(
            "Duration in seconds the individual bar stays visible after taking damage (OnDamage mode only)."
        )]
        public float visibleDuration = 3f;

        [Tooltip(
            "The name displayed on the health bar. Defaults to the GameObject name if left empty."
        )]
        public string displayName;

        [Tooltip("Offset of the health bar from the destructible's position in screen space.")]
        public Vector2 offset = new(0, 50f);

        protected Destructible m_destructible;
        protected GUIHealthBar m_individualBar;
        protected Highlighter m_highlighter;
        protected bool m_isHighlighted;

        protected virtual void Start()
        {
            InitializeDestructible();
            InitializeShowMode();
            InitializeHighlighter();
            InitializeHealthBar();
            InitializeCallbacks();
        }

        protected virtual void InitializeDestructible()
        {
            m_destructible = GetComponent<Destructible>();
        }

        protected virtual void InitializeShowMode()
        {
#if UNITY_ANDROID || UNITY_IOS
            showMode = ShowMode.OnDamage;
#endif
        }

        protected virtual void InitializeHighlighter()
        {
            if (showMode != ShowMode.OnHighlight)
                return;

            if (TryGetComponent(out m_highlighter))
                m_highlighter.onSetHighlight.AddListener(OnHighlightChanged);
        }

        protected virtual void InitializeHealthBar()
        {
            if (barType != BarType.Individual)
                return;

            m_individualBar = GUIHealthBarManager.instance.InstantiateHealthBar(m_destructible);

            if (m_individualBar)
            {
                m_individualBar.target = transform;
                m_individualBar.offset = offset;
                m_individualBar.UpdatePosition();
                m_individualBar.SetHealth(m_destructible.GetHitPointsPercent());
                m_individualBar.gameObject.SetActive(false);
                GUIHealthBarManager.instance.AddHealthBar(m_individualBar);
            }
        }

        protected virtual void InitializeCallbacks()
        {
            m_destructible.onHitPointsChanged.AddListener(OnHitPointsChanged);
            m_destructible.OnDestruct.AddListener(OnDestruct);
        }

        protected virtual void OnDestruct()
        {
            StopCoroutine(nameof(HideIndividualBarDelayed));

            if (m_individualBar)
                m_individualBar.gameObject.SetActive(false);
        }

        protected virtual void OnHitPointsChanged()
        {
            if (m_individualBar)
            {
                m_individualBar.SetHealth(m_destructible.GetHitPointsPercent());

                if (showMode == ShowMode.OnDamage)
                {
                    m_individualBar.gameObject.SetActive(true);
                    StopCoroutine(nameof(HideIndividualBarDelayed));
                    StartCoroutine(nameof(HideIndividualBarDelayed));
                }
            }
            else if (barType == BarType.Global)
            {
                if (showMode == ShowMode.OnDamage || m_isHighlighted)
                    GlobalHealthBar.AssignToInstance(m_destructible, displayName);
            }
        }

        protected virtual void OnHighlightChanged(bool highlighted)
        {
            m_isHighlighted = highlighted;

            if (barType == BarType.Individual && m_individualBar)
                m_individualBar.gameObject.SetActive(highlighted);
            else if (barType == BarType.Global && highlighted)
                GlobalHealthBar.AssignToInstance(m_destructible, displayName);
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
            if (m_individualBar && showMode == ShowMode.OnHighlight && m_isHighlighted)
                m_individualBar.gameObject.SetActive(true);
        }
    }
}
