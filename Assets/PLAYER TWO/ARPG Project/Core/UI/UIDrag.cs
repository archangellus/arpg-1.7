using UnityEngine;
using UnityEngine.EventSystems;

namespace PLAYERTWO.ARPGProject
{
    [AddComponentMenu("PLAYER TWO/ARPG Project/UI/UI Drag")]
    public class UIDrag : MonoBehaviour, IDragHandler
    {
        public enum DragMode { Self, Parent }

        [Tooltip("The dragging mode of this UI element. You can either drag itself or its parent.")]
        public DragMode mode;

        [Tooltip("If true, the UI element will be clamped to the screen's edges.")]
        public bool clampToScreen = true;

        /// <summary>
        /// Returns the target Rect Transform that will be moved.
        /// </summary>
        public RectTransform target
        {
            get
            {
                switch (mode)
                {
                    default:
                    case DragMode.Self:
                        return (RectTransform)transform;
                    case DragMode.Parent:
                        return (RectTransform)transform.parent;
                }
            }
        }

        protected Canvas m_canvas;

        protected virtual void ClampToCanvas()
        {
            if (!clampToScreen)
                return;

            var position = target.localPosition;
            var canvasRect = ((RectTransform)m_canvas.transform).rect;
            var minPosition = canvasRect.min - target.rect.min;
            var maxPosition = canvasRect.max - target.rect.max;
            position.x = Mathf.Clamp(position.x, minPosition.x, maxPosition.x);
            position.y = Mathf.Clamp(position.y, minPosition.y, maxPosition.y);
            target.localPosition = position;
        }

        protected virtual void Start()
        {
            m_canvas = GetComponentInParent<Canvas>();
        }

        public virtual void OnDrag(PointerEventData eventData)
        {
            target.anchoredPosition += eventData.delta / m_canvas.scaleFactor;
            ClampToCanvas();
        }
    }
}
