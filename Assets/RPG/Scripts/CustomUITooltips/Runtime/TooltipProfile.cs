using UnityEngine;

namespace CustomUITooltips
{
    [CreateAssetMenu(menuName = "Custom Tooltips/Tooltip Profile", fileName = "Tooltip Profile")]
    public sealed class TooltipProfile : ScriptableObject
    {
        [Header("Behavior")]
        [Min(0f)] public float defaultShowDelay = 0.25f;
        public bool useUnscaledTime = true;

        [Header("Layout")]
        [Min(40f)] public float minWidth = 120f;
        [Min(80f)] public float maxWidth = 360f;
        [Min(0f)] public float padding = 12f;
        [Min(0f)] public float gap = 8f;
        [Min(0f)] public float screenMargin = 10f;
        [Min(0f)] public float cornerRadius = 8f;
        [Min(0f)] public float borderWidth = 1f;
        [Min(0f)] public float iconSize = 24f;

        [Header("Text")]
        public bool enableRichText = true;
        [Min(1)] public int titleFontSize = 14;
        [Min(1)] public int bodyFontSize = 12;

        [Header("Colors")]
        public Color backgroundColor = new Color(0.055f, 0.058f, 0.066f, 0.97f);
        public Color borderColor = new Color(1f, 1f, 1f, 0.15f);
        public Color titleColor = Color.white;
        public Color bodyColor = new Color(0.86f, 0.88f, 0.92f, 1f);

        public static TooltipProfile CreateRuntimeDefault()
        {
            TooltipProfile profile = CreateInstance<TooltipProfile>();
            profile.name = "Runtime Tooltip Profile";
            profile.hideFlags = HideFlags.HideAndDontSave;
            return profile;
        }
    }
}
