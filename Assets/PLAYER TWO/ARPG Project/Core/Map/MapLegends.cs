using System.Collections.Generic;
using UnityEngine;

namespace PLAYERTWO.ARPGProject
{
    [AddComponentMenu("PLAYER TWO/ARPG Project/Map/Map Legends")]
    public class MapLegends : MonoBehaviour
    {
        [Header("Legend Settings")]
        [Tooltip("Reference to the MapLegend prefab.")]
        public MapLegend legendPrefab;

        [Tooltip("Reference to the container RectTransform for the legends.")]
        public RectTransform container;

        [Tooltip("List of all MapMarkerTypes to create legends for.")]
        public List<MapMarkerType> markerTypes;

        protected virtual void Start()
        {
            InitializeLegends();
        }

        protected virtual void InitializeLegends()
        {
            foreach (var marker in markerTypes)
            {
                var legend = Instantiate(legendPrefab, container);
                legend.Fill(marker);
            }
        }
    }
}
