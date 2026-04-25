using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PLAYERTWO.ARPGProject
{
    [AddComponentMenu("PLAYER TWO/ARPG Project/Map/Map Legend")]
    public class MapLegend : MonoBehaviour
    {
        [Header("Legend UI Elements")]
        [Tooltip("Reference to the icon Image component.")]
        public Image icon;

        [Tooltip("Reference to the description Text component.")]
        public TMP_Text description;

        [Header("Legend Settings")]
        [Tooltip("The size of the icon in the legend.")]
        public Vector2 iconSize = new(32, 32);

        /// <summary>
        /// Fills the legend UI elements with data from the given MapMarkerType.
        /// </summary>
        /// <param name="markerType">The MapMarkerType to use for filling the legend.</param>
        public virtual void Fill(MapMarkerType markerType)
        {
            icon.sprite = markerType.sprite;
            icon.color = markerType.color;
            icon.rectTransform.sizeDelta = iconSize;
            description.text = markerType.description;
        }
    }
}
