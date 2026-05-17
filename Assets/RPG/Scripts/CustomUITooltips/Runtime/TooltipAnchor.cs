using System;
using UnityEngine.UIElements;

namespace CustomUITooltips
{
    [UxmlElement]
    public partial class TooltipAnchor : VisualElement
    {
        [UxmlAttribute] public string tooltipTitle { get; set; }
        [UxmlAttribute] public string tooltipBody { get; set; }
        [UxmlAttribute] public TooltipPlacement tooltipPlacement { get; set; } = TooltipPlacement.FollowPointer;
        [UxmlAttribute] public float tooltipDelay { get; set; } = -1f;
        [UxmlAttribute] public bool tooltipOnFocus { get; set; } = true;

        private IDisposable binding;

        public TooltipAnchor()
        {
            pickingMode = PickingMode.Position;
            RegisterCallback<AttachToPanelEvent>(OnAttachToPanel);
            RegisterCallback<DetachFromPanelEvent>(OnDetachFromPanel);
        }

        private void OnAttachToPanel(AttachToPanelEvent evt)
        {
            binding?.Dispose();
            binding = TooltipService.Bind(this, CreateContent(), tooltipOnFocus);
        }

        private void OnDetachFromPanel(DetachFromPanelEvent evt)
        {
            binding?.Dispose();
            binding = null;
        }

        private TooltipContent CreateContent()
        {
            return new TooltipContent
            {
                title = tooltipTitle,
                body = tooltipBody,
                placement = tooltipPlacement,
                showDelay = tooltipDelay
            };
        }
    }
}
