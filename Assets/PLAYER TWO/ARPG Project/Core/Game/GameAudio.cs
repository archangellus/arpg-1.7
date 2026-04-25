using UnityEngine;

namespace PLAYERTWO.ARPGProject
{
    [AddComponentMenu("PLAYER TWO/ARPG Project/Game/Game Audio")]
    public class GameAudio : Singleton<GameAudio>
    {
        [Header("Audio Settings")]
        [Range(0, 1f)]
        [Tooltip("The initial volume of the music Audio Source.")]
        public float initialMusicVolume = 0.5f;

        [Range(0, 1f)]
        [Tooltip("The initial volume of the effects Audio Source.")]
        public float initialEffectsVolume = 0.5f;

        [Range(0, 1f)]
        [Tooltip("The initial volume of the ui effects Audio Source.")]
        public float initialUiEffectsVolume = 0.5f;

        [Header("General Audios")]
        [Tooltip("An Audio Clip to be used as a 'denied' effect.")]
        public AudioClip deniedClip;

        protected AudioSource m_musicSource;
        protected AudioSource m_effectsSource;
        protected AudioSource m_uiEffectsSource;

        /// <summary>
        /// Returns the Audio Source that plays music.
        /// </summary>
        public AudioSource musicSource
        {
            get
            {
                if (!m_musicSource)
                    InitializeMusicSource();

                return m_musicSource;
            }
        }

        /// <summary>
        /// Returns the Audio Source that plays sound effects.
        /// </summary>
        public AudioSource effectsSource
        {
            get
            {
                if (!m_effectsSource)
                    InitializeEffectsSource();

                return m_effectsSource;
            }
        }

        /// <summary>
        /// Returns the Audio Source that plays ui sound effects.
        /// </summary>
        public AudioSource uiEffectsSource
        {
            get
            {
                if (!m_uiEffectsSource)
                    InitializeUIEffectsSource();

                return m_uiEffectsSource;
            }
        }

        protected virtual void InitializeMusicSource()
        {
            if (m_musicSource)
                return;

            m_musicSource = gameObject.AddComponent<AudioSource>();
            m_musicSource.loop = true;
            m_musicSource.volume = initialMusicVolume;
        }

        protected virtual void InitializeEffectsSource()
        {
            if (m_effectsSource)
                return;

            m_effectsSource = gameObject.AddComponent<AudioSource>();
            m_effectsSource.volume = initialEffectsVolume;
        }

        protected virtual void InitializeUIEffectsSource()
        {
            if (m_uiEffectsSource)
                return;

            m_uiEffectsSource = gameObject.AddComponent<AudioSource>();
            m_uiEffectsSource.volume = initialUiEffectsVolume;
        }

        /// <summary>
        /// Returns the current music volume.
        /// </summary>
        public virtual float GetMusicVolume() => musicSource.volume;

        /// <summary>
        /// Returns the current effects volume.
        /// </summary>
        public virtual float GetEffectsVolume() => effectsSource.volume;

        /// <summary>
        /// Returns the current ui effects volume.
        /// </summary>
        public virtual float GetUIEffectsVolume() => uiEffectsSource.volume;

        /// <summary>
        /// Sets the music volume to a given value.
        /// </summary>
        public virtual void SetMusicVolume(float value) => musicSource.volume = value;

        /// <summary>
        /// Sets the effects volume to a given value.
        /// </summary>
        public virtual void SetEffectsVolume(float value) => effectsSource.volume = value;

        /// <summary>
        /// Sets the ui effects volume to a given value.
        /// </summary>
        public virtual void SetUIEffectsVolume(float value) => uiEffectsSource.volume = value;

        /// <summary>
        /// Plays an Audio Clip with the music Audio Source in loop.
        /// </summary>
        /// <param name="clip">The Audio Clip you want to play.</param>
        public virtual void PlayMusic(AudioClip clip)
        {
            if (clip == null) return;

            musicSource.clip = clip;
            musicSource.Play();
        }

        /// <summary>
        /// Stops playing the current music.
        /// </summary>
        public virtual void StopMusic() => musicSource.Stop();

        /// <summary>
        /// Plays an Audio Clip with the effects Audio Source for one time.
        /// </summary>
        /// <param name="clip">The Audio Clip you want to play.</param>
        public virtual void PlayEffect(AudioClip clip)
        {
            if (clip == null) return;

            effectsSource.PlayOneShot(clip);
        }

        /// <summary>
        /// Plays an Audio Clip with the ui effects Audio Source for one time.
        /// </summary>
        /// <param name="clip">The Audio Clip you want to play.</param>
        public virtual void PlayUiEffect(AudioClip clip)
        {
            if (clip == null) return;

            uiEffectsSource.Stop();
            uiEffectsSource.PlayOneShot(clip);
        }

        /// <summary>
        /// Plays te denied sound using the effects Audio Source.
        /// </summary>
        public virtual void PlayDeniedSound() => PlayEffect(deniedClip);

        protected override void Initialize()
        {
            InitializeMusicSource();
            InitializeEffectsSource();
            InitializeUIEffectsSource();
        }
    }
}
