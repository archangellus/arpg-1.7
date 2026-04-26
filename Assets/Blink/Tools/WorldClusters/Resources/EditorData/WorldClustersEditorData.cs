using UnityEngine;

namespace BLINK.WorldClusters
{
    public class WorldClustersEditorData : ScriptableObject
    {
        [Header("UI")]
        public float removeButtonSize, labelWidth, labelHeight = 18;
        public string openButtonStyle, collapseButtonStyle, addButtonStyle, addSmallButtonStyle, removeButtonStyle,
            duplicateButtonStyle, buttonOffStyle, buttonSelectedStyle;

        public bool hierarchyIconsEnabled = true;

        public Texture2D iconContainsWorldClusters;
        public Texture2D iconManager;
        public Texture2D iconCluster;
        public Texture2D iconCollider;
        public Texture2D iconTrigger;
        public Texture2D iconConditions;
    }
}
