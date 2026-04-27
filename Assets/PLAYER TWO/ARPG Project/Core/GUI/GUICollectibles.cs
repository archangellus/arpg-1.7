using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace PLAYERTWO.ARPGProject
{
    [AddComponentMenu("PLAYER TWO/ARPG Project/GUI/GUI Collectibles")]
    public class GUICollectibles : Singleton<GUICollectibles>
    {
        [Header("Label Settings")]
        [Tooltip("The prefab instantiated to display each collectible name label.")]
        public GUICollectibleName namePrefab;

        [Header("Show Up Settings")]
        [Tooltip("Delay in seconds before the labels fade in on scene start.")]
        public float showUpDelay = 0.1f;

        [Tooltip("Duration in seconds of the fade-in on scene start.")]
        public float showUpFadeTime = 0.15f;

        [Header("Visibility Settings")]
        [Tooltip("If true, this container's visibility can be changed.")]
        public bool canToggleVisibility = true;

        [Tooltip("Duration in seconds of the fade when toggling visibility.")]
        public float fadeTime = 0.1f;

        [Header("Overlap Settings")]
        [Tooltip("Minimum vertical padding in pixels between overlapping labels.")]
        public float overlapPadding = 2f;

        public UnityEvent<Collectible> onAdded;
        public UnityEvent<Collectible> onCollected;

        protected bool m_visible = true;
        protected int m_labelCounter;
        protected Coroutine m_fadeCoroutine;
        protected CanvasGroup m_canvasGroup;
        protected List<GUICollectibleName> m_labels = new();
        protected Vector3[] m_corners = new Vector3[4];

        /// <summary>
        /// Instantiates a name label for the given Collectible and registers it in the tracked list.
        /// </summary>
        public virtual void Add(Collectible collectible)
        {
            if (!namePrefab || !collectible)
                return;

            var label = Instantiate(namePrefab, transform);
            label.spawnOrder = m_labelCounter++;
            label.SetCollectible(collectible, collectible.GetNameColor());
            m_labels.Add(label);
            onAdded.Invoke(collectible);
        }

        /// <summary>
        /// Hides the label for the given Collectible without removing it from the tracked list or firing events.
        /// Used by auto-gathering to suppress the label while the item is in flight.
        /// </summary>
        public virtual void Hide(Collectible collectible)
        {
            var label = m_labels.Find(l => l && l.target == collectible);
            if (label)
                label.Hide();
        }

        /// <summary>
        /// Destroys the label associated with the given Collectible and removes it from the tracked list.
        /// </summary>
        public virtual void Remove(Collectible collectible)
        {
            var label = m_labels.Find(l => l && l.target == collectible);

            if (label)
            {
                m_labels.Remove(label);
                Destroy(label.gameObject);
            }

            onCollected.Invoke(collectible);
        }

        /// <summary>
        /// Toggles the visibility of all collectible labels, fading from the current
        /// alpha to the target so mid-fade reversals are always smooth.
        /// </summary>
        public virtual void Toggle()
        {
            if (!canToggleVisibility)
                return;

            m_visible = !m_visible;
            m_canvasGroup.blocksRaycasts = m_visible;

            if (m_fadeCoroutine != null)
                StopCoroutine(m_fadeCoroutine);

            m_fadeCoroutine = StartCoroutine(
                FadeRoutine(m_canvasGroup.alpha, m_visible ? 1f : 0f, fadeTime)
            );
        }

        protected virtual void InitializeCanvasGroup()
        {
            if (!TryGetComponent(out m_canvasGroup))
                m_canvasGroup = gameObject.AddComponent<CanvasGroup>();

            m_canvasGroup.alpha = 0;
            m_canvasGroup.blocksRaycasts = false;
        }

        /// <summary>
        /// Waits for <see cref="showUpDelay"/> seconds then fades the labels in over
        /// <see cref="showUpFadeTime"/> seconds. Tracked by <see cref="m_fadeCoroutine"/>
        /// so <see cref="Toggle"/> can interrupt it cleanly.
        /// </summary>
        protected virtual IEnumerator ShowUpRoutine()
        {
            if (showUpDelay > 0)
                yield return new WaitForSeconds(showUpDelay);

            m_canvasGroup.blocksRaycasts = true;
            yield return FadeRoutine(0f, 1f, showUpFadeTime);
        }

        /// <summary>
        /// Transitions the canvas group alpha from <paramref name="from"/> to <paramref name="to"/>
        /// over <paramref name="duration"/> seconds, tracking elapsed time so fades
        /// interrupted mid-way resume correctly from the current alpha.
        /// </summary>
        protected virtual IEnumerator FadeRoutine(float from, float to, float duration)
        {
            float elapsedTime = 0f;

            while (elapsedTime < duration)
            {
                elapsedTime += Time.deltaTime;
                m_canvasGroup.alpha = Mathf.Lerp(from, to, elapsedTime / duration);
                yield return null;
            }

            m_canvasGroup.alpha = to;
            m_fadeCoroutine = null;
        }

        /// <summary>
        /// Returns the screen-space bounds of a label using its actual rendered corners,
        /// which correctly accounts for canvas scaling and pivot placement.
        /// </summary>
        protected virtual Rect GetScreenRect(RectTransform rt)
        {
            rt.GetWorldCorners(m_corners);
            // corners: [0] = bottom-left, [1] = top-left, [2] = top-right, [3] = bottom-right
            return new Rect(
                m_corners[0].x,
                m_corners[0].y,
                m_corners[2].x - m_corners[0].x,
                m_corners[1].y - m_corners[0].y
            );
        }

        /// <summary>
        /// Converts each label's world position to screen space, then resolves overlaps
        /// in a single sorted pass — all positioning is owned here, GUICollectibleName
        /// does not touch transform.position.
        /// </summary>
        protected virtual void UpdatePositions()
        {
            m_labels.RemoveAll(l =>
            {
                if (l && !l.worldTransform)
                    Destroy(l.gameObject);
                return !l || !l.worldTransform;
            });

            var camera = Camera.main;

            if (!camera)
                return;

            // Step 1: write raw screen positions.
            foreach (var label in m_labels)
                label.rectTransform.position = camera.WorldToScreenPoint(
                    label.worldTransform.position + label.offset
                );

            if (m_labels.Count < 2)
                return;

            // Step 2: sort bottom-to-top by raw screen Y, using spawnOrder as a stable
            // tiebreaker so labels at identical positions don't swap each frame.
            m_labels.Sort(
                (a, b) =>
                {
                    int cmp = a.rectTransform.position.y.CompareTo(b.rectTransform.position.y);
                    return cmp != 0 ? cmp : a.spawnOrder.CompareTo(b.spawnOrder);
                }
            );

            // Step 3: for each label, find the highest yMax among ALL previous labels that
            // overlap horizontally, and push it just above that — not just above the one
            // immediately below it (which misses non-adjacent horizontal overlaps).
            for (int i = 1; i < m_labels.Count; i++)
            {
                var currRect = GetScreenRect(m_labels[i].rectTransform);
                float requiredYMin = float.MinValue;

                for (int j = 0; j < i; j++)
                {
                    var prevRect = GetScreenRect(m_labels[j].rectTransform);

                    if (currRect.xMin >= prevRect.xMax || currRect.xMax <= prevRect.xMin)
                        continue;

                    requiredYMin = Mathf.Max(requiredYMin, prevRect.yMax + overlapPadding);
                }

                if (requiredYMin == float.MinValue)
                    continue;

                float gap = currRect.yMin - requiredYMin;

                if (gap < 0)
                    m_labels[i].rectTransform.position += Vector3.up * -gap;
            }
        }

        protected override void Awake()
        {
            base.Awake();
            InitializeCanvasGroup();
        }

        protected virtual void Start()
        {
            m_fadeCoroutine = StartCoroutine(ShowUpRoutine());
        }

        protected virtual void LateUpdate()
        {
            UpdatePositions();
        }
    }
}
