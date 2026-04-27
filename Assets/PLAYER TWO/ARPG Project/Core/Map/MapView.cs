using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace PLAYERTWO.ARPGProject
{
    [AddComponentMenu("PLAYER TWO/ARPG Project/Map/Map View")]
    public class MapView : MonoBehaviour
    {
        [Header("General Settings")]
        [Tooltip("References the Rect Transform that will contain the icons.")]
        public RectTransform container;

        [Tooltip("References the Raw Image component used to display the Map.")]
        public RawImage rawImage;

        [Tooltip("Types of markers to exclude from the map view.")]
        public List<MapMarkerType> excludeMarkerTypes;

        [Tooltip("If true, a fog of war overlay is created and driven by the MapFogOfWar system.")]
        public bool fogOfWar = false;

        [Header("Movement Settings")]
        [Tooltip("The frame rate at which the map view updates.")]
        [Range(1, 120)]
        public int frameRate = 30;

        [Tooltip("References the target that moves the view.")]
        public Transform target;

        [Tooltip("Rotates the view by this amount changing its initial orientation.")]
        public float rotationOffset;

        [Tooltip("If true, the view will also rotate with the target's Y axis.")]
        public bool rotateWithTarget;

        [Header("Zoom Settings")]
        [Tooltip("The initial zoom level of the map view.")]
        public float initialZoom = 1f;

        [Tooltip("The maximum and minimum zoom levels allowed.")]
        public float maxZoom = 5f;

        [Tooltip("The minimum zoom level allowed.")]
        public float minZoom = 1f;

        [Space(15f)]
        public UnityEvent<float> onZoomChanged;

        protected List<MapIcon> m_icons = new();
        protected RawImage m_fogImage;

        protected float m_zoom;
        protected float m_lastUpdateTime;

        /// <summary>
        /// The current zoom level of the map view.
        /// </summary>
        public float zoom
        {
            get { return m_zoom; }
            set
            {
                m_zoom = Mathf.Clamp(value, minZoom, maxZoom);
                rawImage.rectTransform.localScale = Vector3.one * m_zoom;
                onZoomChanged?.Invoke(m_zoom);
            }
        }

        /// <summary>
        /// The current pan offset of the map view, which is added to the target's position when calculating the view's position on the map.
        /// </summary>
        public Vector2 panOffset { get; set; }

        protected virtual void Start()
        {
            InitializeTarget();
            InitializeTexture();
            InitializeIcons();
            InitializeFogOfWar();
        }

        protected virtual void LateUpdate()
        {
            UpdateTargetOffset();

            var refreshRate = 1 / (float)frameRate;

            if (Time.time > m_lastUpdateTime + refreshRate)
            {
                m_lastUpdateTime = Time.time;
                UpdateIcons();
            }
        }

        protected virtual void OnValidate()
        {
            if (!Application.isPlaying)
                return;

            zoom = initialZoom;
        }

        protected virtual void InitializeTarget()
        {
            if (target)
                return;

            target = Level.instance.player.transform;
        }

        protected virtual void InitializeTexture()
        {
            if (rawImage.texture)
                return;

            rawImage.texture = Map.instance.texture;
        }

        protected virtual void InitializeFogOfWar()
        {
            if (!fogOfWar || !MapFogOfWar.instance)
                return;

            var go = new GameObject("FogOfWar");
            var rt = go.AddComponent<RectTransform>();
            rt.SetParent(rawImage.rectTransform, false);
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            m_fogImage = go.AddComponent<RawImage>();
            m_fogImage.texture = MapFogOfWar.instance.renderTexture;
            m_fogImage.raycastTarget = false;
        }

        protected virtual void InitializeIcons()
        {
            foreach (var marker in Map.instance.markers)
            {
                if (excludeMarkerTypes.Contains(marker.type))
                    continue;

                AddIcon(marker);
            }

            Map.instance.onMarkerAdded.AddListener(AddIcon);
            Map.instance.onMarkerRemoved.AddListener(RemoveIcon);
        }

        /// <summary>
        /// Add an icon for the given marker.
        /// </summary>
        /// <param name="marker">The marker to add an icon for.</param>
        public virtual void AddIcon(MapMarker marker)
        {
            if (
                !marker
                || excludeMarkerTypes.Contains(marker.type)
                || m_icons.Exists(icon => icon.marker == marker)
            )
                return;

            var icon = marker.CreateIcon();
            icon.transform.SetParent(container, false);
            m_icons.Add(icon);
        }

        /// <summary>
        /// Remove the icon for the given marker.
        /// </summary>
        /// <param name="marker">The marker to remove the icon for.</param>
        public virtual void RemoveIcon(MapMarker marker)
        {
            if (!marker || !m_icons.Exists(icon => icon.marker == marker))
                return;

            var icon = m_icons.Find(icon => icon.marker == marker);
            Destroy(icon.gameObject);
            m_icons.Remove(icon);
        }

        /// <summary>
        /// Resets the zoom level to the initial zoom.
        /// </summary>
        public virtual void ResetZoom() => zoom = initialZoom;

        protected virtual Vector2 RotatePanOffset(Vector2 pan, float angleDegrees)
        {
            var angle = -angleDegrees * Mathf.Deg2Rad;
            var cos = Mathf.Cos(angle);
            var sin = Mathf.Sin(angle);
            return new Vector2(pan.x * cos - pan.y * sin, pan.x * sin + pan.y * cos);
        }

        protected virtual void UpdateTargetOffset()
        {
            var position = Map.instance.WorldToMapPosition(target.position);
            var rotationZ = rotateWithTarget ? target.eulerAngles.y : rotationOffset;
            var rotation = new Vector3(0, 0, rotationZ);
            var targetOffset = position + RotatePanOffset(panOffset, rotationZ);

            rawImage.uvRect = new Rect(targetOffset.x, targetOffset.y, 1, 1);
            rawImage.transform.eulerAngles = rotation;

            if (m_fogImage)
                m_fogImage.uvRect = rawImage.uvRect;
        }

        protected virtual void UpdateIcons()
        {
            foreach (var icon in m_icons)
            {
                if (!icon.isVisible)
                    continue;

                var mapSize = rawImage.rectTransform.sizeDelta;
                var mapScale = rawImage.rectTransform.localScale;
                var mapRectPosition = rawImage.uvRect.position;

                var localPosition = Map.instance.WorldToMapPosition(icon.marker.transform.position);
                var iconRotation = new Vector3(0, 0, icon.marker.type.rotationOffset);
                var scaleOffset = mapRectPosition * mapSize * mapScale;
                var pivotOffset = new Vector2(
                    (container.pivot.x - 0.5f) * container.rect.width,
                    (container.pivot.y - 0.5f) * container.rect.height
                );

                localPosition *= mapSize * mapScale;

                var viewRotation = rawImage.transform.eulerAngles.z;
                var relativePosition = RotatePanOffset(localPosition - scaleOffset, -viewRotation);

                if (icon.marker.type.rotateWithOwner)
                {
                    iconRotation.z -= icon.marker.transform.eulerAngles.y - rotationOffset;

                    if (rotateWithTarget)
                        iconRotation.z += viewRotation - rotationOffset;
                }

                icon.transform.localPosition = relativePosition - pivotOffset;
                icon.transform.eulerAngles = iconRotation;
            }
        }
    }
}
