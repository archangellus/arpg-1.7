using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace PLAYERTWO.ARPGProject
{
    [AddComponentMenu("PLAYER TWO/ARPG Project/GUI/Entity Effect Icon")]
    public class EntityEffectIcon : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        [Header("Icon Settings")]
        [Tooltip("The Image used to display the effect's icon sprite.")]
        public Image iconImage;

        [Tooltip("The Image used to display the effect's remaining duration via fill amount.")]
        public Image durationImage;

        protected EntityEffectInstance m_instance;
        protected bool m_inspecting;

        /// <summary>
        /// Assigns an <see cref="EntityEffectInstance"/> to this icon and updates the icon sprite.
        /// </summary>
        public virtual void SetEffect(EntityEffectInstance instance)
        {
            m_instance = instance;

            if (iconImage != null)
                iconImage.sprite = instance.data.icon;

            UpdateFill();
        }

        public void OnPointerEnter(PointerEventData _)
        {
#if UNITY_STANDALONE || UNITY_WEBGL
            m_inspecting = true;
            GUIEffectInspector.instance.SafeCall(i => i.Show(m_instance));
#endif
        }

        public void OnPointerExit(PointerEventData _)
        {
#if UNITY_STANDALONE || UNITY_WEBGL
            m_inspecting = false;
            GUIEffectInspector.instance.SafeCall(i => i.Hide());
#endif
        }

        protected virtual void OnDisable()
        {
            if (!m_inspecting)
                return;

            m_inspecting = false;
            GUIEffectInspector.instance.SafeCall(i => i.Hide());
        }

        protected virtual void Update()
        {
            if (m_instance != null)
                UpdateFill();
        }

        protected virtual void UpdateFill()
        {
            if (durationImage == null)
                return;

            durationImage.fillAmount =
                m_instance.data.duration <= 0
                    ? 0f
                    : 1f - Mathf.Clamp01(m_instance.remainingDuration / m_instance.data.duration);
        }
    }
}
