using UnityEngine;
using UnityEngine.Events;

namespace PLAYERTWO.ARPGProject
{
    [AddComponentMenu("PLAYER TWO/ARPG Project/Misc/Post Process Toggler")]
    public class PostProcessToggler : Singleton<PostProcessToggler>
    {
        public UnityEvent<bool> onValueChanged;

        /// <summary>
        /// Returns the current state of the post-processing.
        /// </summary>
        public bool value { get; protected set; }

        protected GameSettings m_settings => GameSettings.instance;

        protected virtual void OnEnable()
        {
            if (!m_settings)
                return;

            SetValue(m_settings.GetPostProcessing());
        }

        /// <summary>
        /// Enables or disables the post-processing effects.
        /// </summary>
        public virtual void SetValue(bool value)
        {
            this.value = value;
            onValueChanged.Invoke(value);
        }
    }
}
