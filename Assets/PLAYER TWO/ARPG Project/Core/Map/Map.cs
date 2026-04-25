using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace PLAYERTWO.ARPGProject
{
    [AddComponentMenu("PLAYER TWO/ARPG Project/Map/Map")]
    public class Map : Singleton<Map>
    {
        [Header("Map Texture")]
        [Tooltip(
            "The texture image that represents the map. "
                + "You can generate this texture using a MapBaker."
        )]
        public Texture texture;

        [Header("Map Bounds")]
        [Tooltip("The center of the map bounds in world space.")]
        public Vector3 center;

        [Min(0f)]
        [Tooltip("The height of the map bounds (Y dimension).")]
        public float height;

        [Min(0f)]
        [Tooltip("The length of the map bounds (X and Z dimensions).")]
        public float length;

        /// <summary>
        /// All the markers present in the map.
        /// </summary>
        public List<MapMarker> markers { get; protected set; } = new();

        [Space(10f)]
        public UnityEvent<MapMarker> onMarkerAdded;
        public UnityEvent<MapMarker> onMarkerRemoved;

        /// <summary>
        /// Add a marker to the map.
        /// </summary>
        /// <param name="marker">The marker to add.</param>
        public virtual void AddMarker(MapMarker marker)
        {
            if (markers.Contains(marker))
                return;

            markers.Add(marker);
            onMarkerAdded?.Invoke(marker);
        }

        /// <summary>
        /// Remove a marker from the map.
        /// </summary>
        /// <param name="marker">The marker to remove.</param>
        public virtual void RemoveMarker(MapMarker marker)
        {
            if (!markers.Contains(marker))
                return;

            markers.Remove(marker);
            onMarkerRemoved?.Invoke(marker);
        }

        /// <summary>
        /// Return a point inside the map volume, in a -1 to 1 range.
        /// </summary>
        public virtual Vector2 WorldToMapPosition(Vector3 position)
        {
            var x = (position.x - center.x) / length;
            var z = (position.z - center.z) / length;

            return new Vector2(x, z);
        }

#if UNITY_EDITOR
        protected virtual void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireCube(center, new Vector3(length, height, length));
        }
#endif
    }
}
