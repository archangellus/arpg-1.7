using UnityEngine;
using UnityEngine.Events;

namespace PLAYERTWO.ARPGProject
{
    [AddComponentMenu("PLAYER TWO/ARPG Project/Misc/Highlighter")]
    public class Highlighter : MonoBehaviour
    {
        public UnityEvent<bool> onSetHighlight;

        [Header("Highlight Settings")]
        [Range(0f, 1f)]
        [Tooltip("The maximum intensity of the highlighting color.")]
        public float maxIntensity = 1f;

        [Tooltip("The property of the material to change when highlighting.")]
        public string propertyName = "_Emission";

        [Tooltip(
            "Renderers to apply the highlighting effect on. "
                + "If empty, the Highlighter will try to find renderers in its children."
        )]
        public Renderer[] renderers;

        /// <summary>
        /// Returns the current highlighting state of this object.
        /// </summary>
        public bool highlighted { get; protected set; }

        protected MaterialPropertyBlock m_properties;

        protected virtual void InitializeRenderers()
        {
            if (renderers == null || renderers.Length == 0)
                renderers = GetComponentsInChildren<Renderer>();

            m_properties = new MaterialPropertyBlock();
        }

        protected virtual void SetMaterialsEmission(float value)
        {
            foreach (var renderer in renderers)
            {
                renderer.GetPropertyBlock(m_properties);

                if (m_properties != null)
                {
                    m_properties.SetFloat(propertyName, value);
                    renderer.SetPropertyBlock(m_properties);
                }
            }
        }

        /// <summary>
        /// Sets if the object is highlighted.
        /// </summary>
        /// <param name="value">The highlighting state of this object.</param>
        public virtual void SetHighlight(bool value)
        {
            if (highlighted == value || renderers == null)
                return;

            SetMaterialsEmission(value ? maxIntensity : 0);
            highlighted = value;
            onSetHighlight.Invoke(value);
        }

        protected virtual void Start() => InitializeRenderers();
    }
}
