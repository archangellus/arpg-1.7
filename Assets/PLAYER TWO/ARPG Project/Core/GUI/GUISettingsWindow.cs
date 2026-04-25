using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PLAYERTWO.ARPGProject
{
    [AddComponentMenu("PLAYER TWO/ARPG Project/GUI/GUI Settings Window")]
    public class GUISettingsWindow : GUIWindow
    {
        [Header("Game Settings")]
        [Tooltip("References the dropdown used to display the game's resolutions.")]
        public TMP_Dropdown resolution;

        [Tooltip("References the toggle that controls the full screen state.")]
        public Toggle fullScreen;

        [Tooltip("References the toggle that controls the post-processing state.")]
        public Toggle postProcessing;

        [Tooltip("References the music volume slider.")]
        public Slider musicVolume;

        [Tooltip("References the effects volume slider.")]
        public Slider effectsVolume;

        [Tooltip("References the ui effects volume slider.")]
        public Slider uiVolume;

        protected GameSettings m_settings => GameSettings.instance;

        protected override void Start()
        {
            base.Start();

            InitializeResolution();
            InitializeFullScreen();
            InitializePostProcessing();
            InitializeMusicVolume();
            InitializeEffectsVolume();
            InitializeUIEffectsVolume();
        }

        protected virtual void OnDisable() => m_settings.SafeCall(s => s.Save());

        protected virtual void OnApplicationFocus(bool hasFocus)
        {
            if (!hasFocus)
                m_settings.SafeCall(s => s.Save());
        }

        protected virtual void InitializeResolution()
        {
            resolution.ClearOptions();
            resolution.AddOptions(m_settings.GetResolutions());
            resolution.value = m_settings.GetResolution();
            resolution.onValueChanged.AddListener(m_settings.SetResolution);
#if UNITY_WEBGL
            resolution.interactable = false;
#endif
        }

        protected virtual void InitializeFullScreen()
        {
            fullScreen.isOn = m_settings.GetFullScreen();
            fullScreen.onValueChanged.AddListener(m_settings.SetFullScreen);
        }

        protected virtual void InitializePostProcessing()
        {
            postProcessing.isOn = m_settings.GetPostProcessing();
            postProcessing.onValueChanged.AddListener(m_settings.SetPostProcessing);
        }

        protected virtual void InitializeMusicVolume()
        {
            musicVolume.value = m_settings.GetMusicVolume();
            musicVolume.onValueChanged.AddListener(m_settings.SetMusicVolume);
        }

        protected virtual void InitializeEffectsVolume()
        {
            effectsVolume.value = m_settings.GetEffectsVolume();
            effectsVolume.onValueChanged.AddListener(m_settings.SetEffectsVolume);
        }

        protected virtual void InitializeUIEffectsVolume()
        {
            uiVolume.value = m_settings.GetUIEffectsVolume();
            uiVolume.onValueChanged.AddListener(m_settings.SetUIEffectsVolume);
        }
    }
}
