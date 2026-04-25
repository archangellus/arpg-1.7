using UnityEngine;
using System.Collections;

namespace PLAYERTWO.ARPGProject
{
    [RequireComponent(typeof(CanvasGroup))]
    [AddComponentMenu("PLAYER TWO/ARPG Project/Misc/Fader")]
    public class Fader : Singleton<Fader>
    {
        [Tooltip("The duration in seconds of the fading.")]
        public float duration = 0.5f;

        protected CanvasGroup m_group;

        /// <summary>
        /// Fades out with no callback.
        /// </summary>
        public virtual void FadeOut() => FadeOut(() => { });

        /// <summary>
        /// Fades in with no callback.
        /// </summary>
        public virtual void FadeIn() => FadeIn(() => { });

        /// <summary>
        /// Fades out with callback.
        /// </summary>
        /// <param name="onFinished">The action that will be invoked in the end of the routine.</param>
        public virtual void FadeOut(System.Action onFinished)
        {
            m_group.blocksRaycasts = true;
            StopAllCoroutines();
            StartCoroutine(AlphaRoutine(0, 1, onFinished));
        }

        /// <summary>
        /// Fades in with callback.
        /// </summary>
        /// <param name="onFinished">The action that will be invoked in the end of the routine.</param>
        public virtual void FadeIn(System.Action onFinished)
        {
            StopAllCoroutines();

            StartCoroutine(AlphaRoutine(1, 0, () =>
            {
                m_group.blocksRaycasts = false;
                onFinished.Invoke();
            }));
        }

        protected IEnumerator AlphaRoutine(float from, float to, System.Action onFinished)
        {
            var time = 0f;

            m_group.alpha = from;

            while (time < duration)
            {
                time += Time.deltaTime;
                m_group.alpha = Mathf.Lerp(from, to, time / duration);
                yield return null;
            }

            m_group.alpha = to;
            onFinished?.Invoke();
        }

        protected override void Awake()
        {
            m_group = GetComponent<CanvasGroup>();
            m_group.alpha = 0;
            m_group.blocksRaycasts = false;
        }
    }
}
