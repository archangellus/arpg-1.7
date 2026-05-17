using UnityEngine;
using UnityEngine.EventSystems;

namespace CustomUITooltips
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Custom Tooltips/Tooltip Trigger (uGUI)")]
    public sealed class TooltipTrigger : MonoBehaviour,
        IPointerEnterHandler,
        IPointerExitHandler,
        IPointerMoveHandler,
        IPointerDownHandler,
        ISelectHandler,
        IDeselectHandler
    {
        public TooltipContent tooltip = new TooltipContent();

        [Header("Input")]
        public bool showOnHover = true;
        public bool showOnFocus = true;
        public bool hideOnDisable = true;

        private Vector2 lastScreenPosition;

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (!showOnHover)
                return;

            if (!TooltipManager.TryGetInstance(out TooltipManager manager))
                return;

            lastScreenPosition = eventData.position;
            manager.Show(tooltip, lastScreenPosition, GetTargetScreenRect(), this);
        }

        public void OnPointerMove(PointerEventData eventData)
        {
            if (!TooltipManager.TryGetInstance(out TooltipManager manager, false))
                return;

            lastScreenPosition = eventData.position;
            manager.Move(lastScreenPosition, this);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (TooltipManager.TryGetInstance(out TooltipManager manager, false))
                manager.Hide(this);
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (tooltip == null || !tooltip.hideOnPress)
                return;

            if (TooltipManager.TryGetInstance(out TooltipManager manager, false))
                manager.Hide(this);
        }

        public void OnSelect(BaseEventData eventData)
        {
            if (!showOnFocus)
                return;

            if (!TooltipManager.TryGetInstance(out TooltipManager manager))
                return;

            Rect targetRect = GetTargetScreenRect();
            lastScreenPosition = targetRect.center;
            manager.Show(tooltip, lastScreenPosition, targetRect, this);
        }

        public void OnDeselect(BaseEventData eventData)
        {
            if (TooltipManager.TryGetInstance(out TooltipManager manager, false))
                manager.Hide(this);
        }

        private void OnDisable()
        {
            if (!hideOnDisable)
                return;

            if (TooltipManager.TryGetInstance(out TooltipManager manager, false))
                manager.Hide(this);
        }

        public void ShowNow()
        {
            if (!TooltipManager.TryGetInstance(out TooltipManager manager))
                return;

            Rect targetRect = GetTargetScreenRect();
            lastScreenPosition = targetRect.center;
            manager.Show(tooltip, lastScreenPosition, targetRect, this);
        }

        public void HideNow()
        {
            if (TooltipManager.TryGetInstance(out TooltipManager manager, false))
                manager.Hide(this);
        }

        private Rect GetTargetScreenRect()
        {
            RectTransform rectTransform = transform as RectTransform;
            if (rectTransform == null)
                return new Rect(lastScreenPosition.x, lastScreenPosition.y, 1f, 1f);

            Canvas canvas = GetComponentInParent<Canvas>();
            Camera cam = null;
            if (canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay)
                cam = canvas.worldCamera != null ? canvas.worldCamera : Camera.main;

            Vector3[] corners = new Vector3[4];
            rectTransform.GetWorldCorners(corners);

            Vector2 min = RectTransformUtility.WorldToScreenPoint(cam, corners[0]);
            Vector2 max = min;
            for (int i = 1; i < corners.Length; i++)
            {
                Vector2 p = RectTransformUtility.WorldToScreenPoint(cam, corners[i]);
                min = Vector2.Min(min, p);
                max = Vector2.Max(max, p);
            }

            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }
    }
}
