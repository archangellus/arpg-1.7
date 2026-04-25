using UnityEngine;

namespace PLAYERTWO.ARPGProject
{
    [AddComponentMenu("PLAYER TWO/ARPG Project/Game/Level Audio")]
    public class LevelAudio : MonoBehaviour
    {
        [Tooltip("The Audio Clip that represents the theme sound of this Level.")]
        public AudioClip theme;

        protected GameAudio m_audio => GameAudio.instance;

        protected virtual void Start() => m_audio.PlayMusic(theme);

        protected virtual void OnDisable() => GameAudio.instance.SafeCall(a => a.StopMusic());
    }
}
