using UnityEngine;

namespace PLAYERTWO.ARPGProject
{
    [AddComponentMenu("PLAYER TWO/ARPG Project/GUI/GUI Collectibles")]
    public class GUICollectibles : MonoBehaviour
    {
        [Tooltip("If true, this container's visibility can be changed.")]
        public bool canToggleVisibility = true;

        protected bool m_visible = true;
        protected CanvasGroup m_canvasGroup;

        /// <summary>
        /// Toggles the visibility of the collectibles.
        /// </summary>
        public virtual void Toggle()
        {
            if (!canToggleVisibility)
                return;

            m_visible = !m_visible;
            m_canvasGroup.alpha = m_visible ? 1 : 0;
        }

        protected virtual void InitializeCanvasGroup()
        {
            if (!TryGetComponent(out m_canvasGroup))
                m_canvasGroup = gameObject.AddComponent<CanvasGroup>();
        }

        protected virtual void Start()
        {
            InitializeCanvasGroup();
        }
    }
}
