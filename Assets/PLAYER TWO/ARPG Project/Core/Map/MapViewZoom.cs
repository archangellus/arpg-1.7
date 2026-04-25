using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace PLAYERTWO.ARPGProject
{
    [AddComponentMenu("PLAYER TWO/ARPG Project/Map/Map View Zoom")]
    public class MapViewZoom : MonoBehaviour, IScrollHandler
    {
        [Header("Map View Reference")]
        [Tooltip("References the Map View to zoom.")]
        public MapView mapView;

        [Header("UI References")]
        [Tooltip("References the button used to zoom in.")]
        public Button zoomInButton;

        [Tooltip("References the button used to zoom out.")]
        public Button zoomOutButton;

        [Header("Zoom Settings")]
        [Tooltip("The sensitivity of zooming with the scroll wheel.")]
        [Range(0.1f, 5f)]
        public float zoomScrollSensitivity = 1f;

        [Tooltip("The amount to zoom in or out when using the zoom buttons.")]
        public float zoomButtonStep = 0.1f;

        [Header("Reset Settings")]
        [Tooltip("If true, the zoom level will reset to the initial zoom when enabled.")]
        public bool resetZoomOnEnable = true;

        protected bool m_zooming;

        protected virtual void Start()
        {
            InitializeMapView();
            InitializeButtons();
        }

        protected virtual void OnEnable()
        {
            if (mapView && resetZoomOnEnable)
                mapView.ResetZoom();
        }

        public virtual void OnScroll(PointerEventData eventData)
        {
            mapView.zoom += eventData.scrollDelta.y * 0.01f * zoomScrollSensitivity;
        }

        protected virtual void InitializeMapView()
        {
            if (!mapView)
                mapView = GetComponentInParent<MapView>();

            mapView.onZoomChanged.AddListener(UpdateButtons);
        }

        protected virtual void InitializeButtons()
        {
            if (zoomInButton)
                zoomInButton.onClick.AddListener(ZoomIn);

            if (zoomOutButton)
                zoomOutButton.onClick.AddListener(ZoomOut);
        }

        /// <summary>
        /// Updates the interactable state of the zoom buttons based on the current zoom level.
        /// </summary>
        /// <param name="zoom">The current zoom level.</param>
        public virtual void UpdateButtons(float zoom)
        {
            if (zoomInButton)
                zoomInButton.interactable = zoom < mapView.maxZoom;

            if (zoomOutButton)
                zoomOutButton.interactable = zoom > mapView.minZoom;
        }

        /// <summary>
        /// Zooms in the map view by increasing the zoom level.
        /// </summary>
        public virtual void ZoomIn()
        {
            if (!m_zooming)
                StartCoroutine(ZoomRoutine(mapView.zoom + zoomButtonStep));
        }

        /// <summary>
        /// Zooms out the map view by decreasing the zoom level.
        /// </summary>
        public virtual void ZoomOut()
        {
            if (!m_zooming)
                StartCoroutine(ZoomRoutine(mapView.zoom - zoomButtonStep));
        }

        protected virtual IEnumerator ZoomRoutine(float targetZoom, float duration = 0.2f)
        {
            m_zooming = true;

            float startZoom = mapView.zoom;
            float elapsedTime = 0f;

            while (elapsedTime < duration)
            {
                elapsedTime += Time.deltaTime;
                mapView.zoom = Mathf.Lerp(startZoom, targetZoom, elapsedTime / duration);
                yield return null;
            }

            mapView.zoom = targetZoom;
            m_zooming = false;
        }
    }
}
