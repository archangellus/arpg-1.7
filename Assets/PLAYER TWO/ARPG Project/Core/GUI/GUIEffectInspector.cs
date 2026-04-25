using UnityEngine;
using UnityEngine.UI;

namespace PLAYERTWO.ARPGProject
{
    [AddComponentMenu("PLAYER TWO/ARPG Project/GUI/GUI Effect Inspector")]
    public class GUIEffectInspector : GUIInspector<GUIEffectInspector>
    {
        [Tooltip("A reference to the Text component used as the effect name.")]
        public Text effectName;

        [Tooltip("A reference to the Text component used to list stat modifiers.")]
        public Text modifiers;

        [Tooltip("A reference to the Text component used for the damage over time description.")]
        public Text dot;

        protected CanvasGroup m_group;
        protected EntityEffectInstance m_effectInstance;

        protected virtual void InitializeCanvasGroup()
        {
            if (!TryGetComponent(out m_group))
                m_group = gameObject.AddComponent<CanvasGroup>();

            m_group.blocksRaycasts = false;
        }

        /// <summary>
        /// Shows the inspector with information from a given <see cref="EntityEffectInstance"/>.
        /// </summary>
        /// <param name="instance">The effect instance to inspect.</param>
        public virtual void Show(EntityEffectInstance instance)
        {
            if (instance == null || gameObject.activeSelf)
                return;

            m_effectInstance = instance;
            gameObject.SetActive(true);
            UpdateAll();
            FadIn();
        }

        /// <summary>
        /// Hides the inspector.
        /// </summary>
        public virtual void Hide()
        {
            if (!gameObject.activeSelf)
                return;

            gameObject.SetActive(false);
        }

        protected virtual void UpdateAll()
        {
            UpdateEffectName();
            UpdateModifiers();
            UpdateDot();
        }

        protected virtual void UpdateEffectName() =>
            effectName.text = m_effectInstance.data.effectName;

        protected virtual void UpdateModifiers()
        {
            var text = m_effectInstance.data.GetModifiersText();
            modifiers.gameObject.SetActive(!string.IsNullOrEmpty(text));

            if (modifiers.gameObject.activeSelf)
                modifiers.text = text;
        }

        protected virtual void UpdateDot()
        {
            var hasDot = m_effectInstance.data is EntityDebuff debuff && debuff.HasDot();
            dot.gameObject.SetActive(hasDot);

            if (!hasDot)
                return;

            var debuffData = (EntityDebuff)m_effectInstance.data;
            var interval = debuffData.dotInterval > 0f ? debuffData.dotInterval : 1f;
            dot.text = $"Applies {debuffData.dotDamagePercent}% of damage every {interval} seconds";
        }

        protected virtual void Start()
        {
            InitializeCanvasGroup();
            Hide();
        }
    }
}
