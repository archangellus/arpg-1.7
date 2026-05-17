using System;
using UnityEngine;

namespace CustomUITooltips
{
    [Serializable]
    public sealed class TooltipContent
    {
        [Tooltip("Optional id for databases, analytics, or scripted lookup.")]
        public string id;

        [Tooltip("Short heading displayed in bold.")]
        public string title;

        [TextArea(2, 8)]
        [Tooltip("Main tooltip copy. Rich text is supported when the active profile enables it.")]
        public string body;

        [Tooltip("Optional icon shown to the left of the text.")]
        public Sprite icon;

        [Tooltip("Optional per-tooltip visual/behavior override. Leave empty to use the manager default.")]
        public TooltipProfile overrideProfile;

        [Tooltip("Where the tooltip should appear relative to the target or pointer.")]
        public TooltipPlacement placement = TooltipPlacement.FollowPointer;

        [Tooltip("Offset in panel pixels. For Follow Pointer this is cursor offset; for anchored modes this separates the card from the target.")]
        public Vector2 offset = new Vector2(18f, 18f);

        [Tooltip("Use -1 to inherit from the active Tooltip Profile.")]
        public float showDelay = -1f;

        [Tooltip("When true, the tooltip follows pointer movement while visible.")]
        public bool followPointer = true;

        [Tooltip("When true, pointer/touch press hides the tooltip.")]
        public bool hideOnPress = true;

        public bool IsEmpty => string.IsNullOrWhiteSpace(title) && string.IsNullOrWhiteSpace(body) && icon == null;

        public static TooltipContent Text(string title, string body = null)
        {
            return new TooltipContent
            {
                title = title,
                body = body
            };
        }
    }
}
