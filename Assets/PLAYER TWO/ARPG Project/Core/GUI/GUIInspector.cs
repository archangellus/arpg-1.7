using UnityEngine;
using System.Collections;

namespace PLAYERTWO.ARPGProject
{
    public class GUIInspector<T> : Singleton<T> where T : MonoBehaviour
    {
        protected Canvas m_canvas;
        protected CanvasGroup m_canvasGroup;

        protected const float k_fadeDelay = 0.15f;
        protected const float k_fadeDuration = 0.1f;

        protected Vector3[] temp_corners = new Vector3[4];

        protected Canvas canvas
        {
            get
            {
                if (!m_canvas)
                    m_canvas = GetComponentInParent<Canvas>();

                return m_canvas;
            }
        }

        protected CanvasGroup canvasGroup
        {
            get
            {
                if (!m_canvasGroup)
                    m_canvasGroup = GetComponent<CanvasGroup>();

                return m_canvasGroup;
            }
        }

        protected RectTransform m_rect => (RectTransform)transform;

        protected WaitForSeconds m_waitForFadeDelay;

        protected virtual void InitializeWaits()
        {
            m_waitForFadeDelay = new WaitForSeconds(k_fadeDelay);
        }

        /// <summary>
        /// Sets the position of this inspector to a corner relative to a given Rect Transform.
        /// </summary>
        /// <param name="other">The Rect Transform to read the corners from.</param>
        public virtual void SetPositionRelativeTo(RectTransform other)
        {
            other.GetWorldCorners(temp_corners);

            var pivot = CalculatePivotFrom(other.position);
            var x = pivot.x == 1 ? temp_corners[1].x : temp_corners[2].x;
            var y = pivot.y == 1 ? temp_corners[1].y : temp_corners[0].y;

            m_rect.pivot = pivot;
            m_rect.position = new Vector2(x, y);
        }

        protected virtual Vector2 CalculatePivotFrom(Vector2 position)
        {
            var canvasYScale = canvas.transform.localScale.y;
            var x = position.x > Screen.width / 2 ? 1 : 0;
            var y = position.y - m_rect.sizeDelta.y * canvasYScale > 0 ? 1 : 0;
            return new Vector2(x, y);
        }

        protected virtual void UpdatePivot()
        {
            var position = EntityInputs.GetPointerPosition();
            m_rect.pivot = CalculatePivotFrom(position);
        }

        protected virtual void UpdatePosition()
        {
            transform.position = EntityInputs.GetPointerPosition();
        }

        protected void FadIn(System.Action callback = null) =>
            StartCoroutine(FadeRoutine(0, 1, callback));

        protected void FadeOut(System.Action callback = null) =>
            StartCoroutine(FadeRoutine(1, 0, callback));

        protected IEnumerator FadeRoutine(float from, float to, System.Action callback)
        {
            if (canvasGroup)
            {
                canvasGroup.alpha = from;

                yield return m_waitForFadeDelay;

                var time = 0f;

                while (time <= k_fadeDuration)
                {
                    time += Time.deltaTime;
                    canvasGroup.alpha = Mathf.Lerp(from, to, time / k_fadeDuration);
                    yield return null;
                }
            }

            callback?.Invoke();
        }

        protected override void Awake()
        {
            base.Awake();
            InitializeWaits();
        }

#if UNITY_STANDALONE || UNITY_WEBGL
        protected virtual void LateUpdate()
        {
            UpdatePivot();
            UpdatePosition();
        }
#endif
    }
}
