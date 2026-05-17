using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace CustomUITooltips
{
    public static class TooltipService
    {
        public static void Show(TooltipContent content, Vector2 screenPosition, object owner = null)
        {
            if (TooltipManager.TryGetInstance(out TooltipManager manager))
                manager.Show(content, screenPosition, null, owner);
        }

        public static void Show(string title, string body, Vector2 screenPosition, object owner = null)
        {
            Show(TooltipContent.Text(title, body), screenPosition, owner);
        }

        public static void Hide(object owner = null)
        {
            if (TooltipManager.TryGetInstance(out TooltipManager manager, false))
                manager.Hide(owner);
        }

        public static IDisposable Bind(VisualElement element, TooltipContent content, bool showOnFocus = true)
        {
            if (element == null)
                throw new ArgumentNullException(nameof(element));
            if (content == null)
                throw new ArgumentNullException(nameof(content));

            EventCallback<PointerEnterEvent> enter = evt =>
            {
                if (!TooltipManager.TryGetInstance(out TooltipManager manager))
                    return;

                Vector2 panelPosition = ToVector2(evt.position);
                manager.ShowAtPanelPosition(content, panelPosition, element.worldBound, element);
            };

            EventCallback<PointerMoveEvent> move = evt =>
            {
                if (!TooltipManager.TryGetInstance(out TooltipManager manager, false))
                    return;

                manager.MoveAtPanelPosition(ToVector2(evt.position), element);
            };

            EventCallback<PointerLeaveEvent> leave = evt =>
            {
                if (TooltipManager.TryGetInstance(out TooltipManager manager, false))
                    manager.Hide(element);
            };

            EventCallback<PointerDownEvent> down = evt =>
            {
                if (!content.hideOnPress)
                    return;

                if (TooltipManager.TryGetInstance(out TooltipManager manager, false))
                    manager.Hide(element);
            };

            EventCallback<FocusInEvent> focusIn = evt =>
            {
                if (!showOnFocus)
                    return;

                if (!TooltipManager.TryGetInstance(out TooltipManager manager))
                    return;

                Rect world = element.worldBound;
                manager.ShowAtPanelPosition(content, world.center, world, element);
            };

            EventCallback<FocusOutEvent> focusOut = evt =>
            {
                if (!showOnFocus)
                    return;

                if (TooltipManager.TryGetInstance(out TooltipManager manager, false))
                    manager.Hide(element);
            };

            element.RegisterCallback(enter);
            element.RegisterCallback(move);
            element.RegisterCallback(leave);
            element.RegisterCallback(down);
            element.RegisterCallback(focusIn);
            element.RegisterCallback(focusOut);

            return new CallbackDisposer(() =>
            {
                element.UnregisterCallback(enter);
                element.UnregisterCallback(move);
                element.UnregisterCallback(leave);
                element.UnregisterCallback(down);
                element.UnregisterCallback(focusIn);
                element.UnregisterCallback(focusOut);
                Hide(element);
            });
        }

        private static Vector2 ToVector2(Vector3 value)
        {
            return new Vector2(value.x, value.y);
        }

        private sealed class CallbackDisposer : IDisposable
        {
            private Action dispose;

            public CallbackDisposer(Action dispose)
            {
                this.dispose = dispose;
            }

            public void Dispose()
            {
                Action action = dispose;
                dispose = null;
                action?.Invoke();
            }
        }
    }
}
