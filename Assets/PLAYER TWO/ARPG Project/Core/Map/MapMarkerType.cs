using UnityEngine;

namespace PLAYERTWO.ARPGProject
{
    [CreateAssetMenu(
        fileName = "New Map Marker Type",
        menuName = "PLAYER TWO/ARPG Project/Map/Map Marker Type"
    )]
    public class MapMarkerType : ScriptableObject
    {
        [Header("Marker Settings")]
        [Tooltip("The icon instantiated for this marker type on the map.")]
        public MapIcon iconPrefab;

        [Tooltip("The sprite that represents the Icon on the map.")]
        public Sprite sprite;

        [Tooltip("Descriptive text for this marker type, shown in the map legend.")]
        public string description;

        [Tooltip("The color of the marker on the map.")]
        public Color color = Color.white;

        [Tooltip("The color of the marker when it is inactive.")]
        public Color colorInactive = Color.gray;

        [Tooltip("The size of the marker on the map.")]
        public Vector2 size = new(32, 32);

        [Tooltip("Rotates the icon on the viewers to adjust its orientation.")]
        public float rotationOffset;

        [Tooltip("If true, the Icon will adjust its rotation based on the transform Y rotation.")]
        public bool rotateWithOwner = false;
    }
}
