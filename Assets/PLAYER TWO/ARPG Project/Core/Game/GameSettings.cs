using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace PLAYERTWO.ARPGProject
{
    [AddComponentMenu("PLAYER TWO/ARPG Project/Game/Game Settings")]
    public class GameSettings : Singleton<GameSettings>
    {
        public enum ResolutionOption
        {
            Lowest,
            Low,
            Medium,
            High,
            Maximum,
        }

        protected int m_currentResolution;
        protected bool m_currentFullScreen;
        protected bool m_currentPostProcessing;
        protected float m_currentMusicVolume;
        protected float m_currentEffectsVolume;
        protected float m_currentUiEffectsVolume;

        protected const string k_resolutionKey = "settings/resolution";
        protected const string k_fullScreenKey = "settings/fullScreen";
        protected const string k_postProcessingKey = "settings/postProcessing";
        protected const string k_musicVolumeKey = "settings/musicVolume";
        protected const string k_effectsVolumeKey = "settings/effectsVolume";
        protected const string k_uiEffectsVolumeKey = "settings/uiEffectsVolume";

        protected GameAudio m_audio => GameAudio.instance;
        protected PostProcessToggler m_postProcess => PostProcessToggler.instance;

        protected override void Awake()
        {
            base.Awake();
            Load();
        }

        protected virtual void OnDisable() => Save();

        /// <summary>
        /// Returns the current screen resolution option.
        /// </summary>
        public virtual int GetResolution() => m_currentResolution;

        /// <summary>
        /// Returns a list of available screen resolution options.
        /// </summary>
        public virtual List<string> GetResolutions()
        {
#if UNITY_STANDALONE
            return GetScreenResolutions();
#else
            return GetRenderingResolutions();
#endif
        }

        protected virtual List<string> GetScreenResolutions() =>
            Screen.resolutions.Select(resolution => resolution.ToString()).ToList();

        protected virtual List<string> GetRenderingResolutions() =>
            System.Enum.GetNames(typeof(ResolutionOption)).ToList();

        /// <summary>
        /// Returns true if the game is in full screen.
        /// </summary>
        public virtual bool GetFullScreen() => Screen.fullScreen;

        /// <summary>
        /// Returns true if the post-processing is enabled.
        /// </summary>
        public virtual bool GetPostProcessing() => m_postProcess.value;

        /// <summary>
        /// Returns the current music volume.
        /// </summary>
        public virtual float GetMusicVolume() => m_audio.GetMusicVolume();

        /// <summary>
        /// Returns the current effects volume.
        /// </summary>
        public virtual float GetEffectsVolume() => m_audio.GetEffectsVolume();

        /// <summary>
        /// Returns the current UI Effects volume.
        /// </summary>
        public virtual float GetUIEffectsVolume() => m_audio.GetUIEffectsVolume();

        /// <summary>
        /// Sets the game resolution based on a given option.
        /// </summary>
        /// <param name="option">The option you want to set.</param>
        public void SetResolution(int option)
        {
#if UNITY_STANDALONE
            SetScreenResolution(option);
#else
            SetRenderingResolution(option);
#endif
        }

        protected virtual void SetScreenResolution(int option)
        {
            var resolution = Screen.resolutions[option];
            Screen.SetResolution(
                resolution.width,
                resolution.height,
                Screen.fullScreenMode,
                resolution.refreshRateRatio
            );
            m_currentResolution = option;
        }

        protected virtual void SetRenderingResolution(int option)
        {
            var resolution = GetResolutionScale((ResolutionOption)option);
            Screen.SetResolution(resolution.x, resolution.y, Screen.fullScreen);
            m_currentResolution = option;
        }

        /// <summary>
        /// Enables or disables the full screen mode.
        /// </summary>
        public virtual void SetFullScreen(bool value)
        {
            Screen.fullScreen = value;
            m_currentFullScreen = value;
        }

        /// <summary>
        /// Enables or disables the post processing effects.
        /// </summary>
        public virtual void SetPostProcessing(bool value)
        {
            m_postProcess.SafeCall(p => p.SetValue(value));
            m_currentPostProcessing = value;
        }

        /// <summary>
        /// Sets the music volume to a given value.
        /// </summary>
        public virtual void SetMusicVolume(float value)
        {
            m_audio.SetMusicVolume(value);
            m_currentMusicVolume = value;
        }

        /// <summary>
        /// Sets the effects volume to a given value.
        /// </summary>
        public virtual void SetEffectsVolume(float value)
        {
            m_audio.SetEffectsVolume(value);
            m_currentEffectsVolume = value;
        }

        /// <summary>
        /// Sets the UI Effects volume to a given value.
        /// </summary>
        public virtual void SetUIEffectsVolume(float value)
        {
            m_audio.SetUIEffectsVolume(value);
            m_currentUiEffectsVolume = value;
        }

        protected virtual Vector2Int GetResolutionScale(ResolutionOption option)
        {
            var nativeWidth = Display.main.systemWidth;
            var nativeHeight = Display.main.systemHeight;

            switch (option)
            {
                default:
                case ResolutionOption.Maximum:
                    return new Vector2Int(nativeWidth, nativeHeight);
                case ResolutionOption.High:
                    return new Vector2Int((int)(nativeWidth * 0.8f), (int)(nativeHeight * 0.8f));
                case ResolutionOption.Medium:
                    return new Vector2Int((int)(nativeWidth * 0.6f), (int)(nativeHeight * 0.6f));
                case ResolutionOption.Low:
                    return new Vector2Int((int)(nativeWidth * 0.4f), (int)(nativeHeight * 0.4f));
                case ResolutionOption.Lowest:
                    return new Vector2Int((int)(nativeWidth * 0.3f), (int)(nativeHeight * 0.3f));
            }
        }

        /// <summary>
        /// Saves all the settings.
        /// </summary>
        public virtual void Save()
        {
            PlayerPrefs.SetInt(k_resolutionKey, m_currentResolution);
            PlayerPrefs.SetInt(k_fullScreenKey, m_currentFullScreen ? 1 : 0);
            PlayerPrefs.SetInt(k_postProcessingKey, m_currentPostProcessing ? 1 : 0);
            PlayerPrefs.SetFloat(k_musicVolumeKey, m_currentMusicVolume);
            PlayerPrefs.SetFloat(k_effectsVolumeKey, m_currentEffectsVolume);
            PlayerPrefs.SetFloat(k_uiEffectsVolumeKey, m_currentUiEffectsVolume);
            PlayerPrefs.Save();
        }

        /// <summary>
        /// Loads and applies all the settings.
        /// </summary>
        public virtual void Load()
        {
#if UNITY_STANDALONE
            var initialResolution = Screen.resolutions.Length - 1;
#else
            var initialResolution = (int)ResolutionOption.Maximum;
#endif
            var resolution = PlayerPrefs.GetInt(k_resolutionKey, initialResolution);
            var fullScreen = PlayerPrefs.GetInt(k_fullScreenKey, Screen.fullScreen ? 1 : 0);
            var postProcessing = PlayerPrefs.GetInt(k_postProcessingKey, 1);
            var musicVolume = PlayerPrefs.GetFloat(k_musicVolumeKey, m_audio.initialMusicVolume);
            var effectsVolume = PlayerPrefs.GetFloat(
                k_effectsVolumeKey,
                m_audio.initialEffectsVolume
            );
            var uiEffectsVolume = PlayerPrefs.GetFloat(
                k_uiEffectsVolumeKey,
                m_audio.initialUiEffectsVolume
            );

            SetResolution(resolution);
            SetFullScreen(fullScreen == 1);
            SetPostProcessing(postProcessing == 1);
            SetMusicVolume(musicVolume);
            SetEffectsVolume(effectsVolume);
            SetUIEffectsVolume(uiEffectsVolume);
        }
    }
}
