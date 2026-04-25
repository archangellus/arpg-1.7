using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;

namespace PLAYERTWO.ARPGProject
{
    [AddComponentMenu("PLAYER TWO/ARPG Project/Map/Map View Pan")]
    public class MapViewPan : MonoBehaviour, IDragHandler
    {
        [Header("Map View Reference")]
        [Tooltip("References the Map View to pan.")]
        public MapView mapView;

        [Header("Movement Settings")]
        [Range(0.01f, 1f)]
        [Tooltip("Sensitivity of the pan movement.")]
        public float panSensitivity = 0.1f;

        [Tooltip("Maximum pan offset allowed.")]
        public Vector2 maxPanOffset = new(0.5f, 0.5f);

        [Header("Reset Settings")]
        [Tooltip("If true, the pan offset will reset to zero when the player starts moving.")]
        public bool resetPanOnMove = true;

        [Tooltip("If true, the pan offset will reset to zero when the component is enabled.")]
        public bool resetOnEnable = true;

        [Tooltip("Duration in seconds to reset the pan offset.")]
        public float resetDuration = 1.5f;

        protected Entity m_player;

        protected bool m_resettingPan;

        protected virtual void Start()
        {
            InitializeMapView();
            InitializePlayer();
        }

        protected virtual void Update()
        {
            HandleResetting();
        }

        protected virtual void OnEnable()
        {
            if (resetOnEnable && mapView)
                mapView.panOffset = Vector2.zero;
        }

        protected virtual void InitializeMapView()
        {
            if (!mapView)
                mapView = GetComponentInParent<MapView>();
        }

        protected virtual void InitializePlayer()
        {
            m_player = Level.instance.player;
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (m_resettingPan || (resetPanOnMove && m_player.lateralVelocity.sqrMagnitude > 0.01f))
                return;

            mapView.panOffset -= eventData.delta / (100f * mapView.zoom) * panSensitivity;
            mapView.panOffset = new Vector2(
                Mathf.Clamp(mapView.panOffset.x, -maxPanOffset.x, maxPanOffset.x),
                Mathf.Clamp(mapView.panOffset.y, -maxPanOffset.y, maxPanOffset.y)
            );
        }

        protected virtual void HandleResetting()
        {
            if (
                resetPanOnMove
                && !m_resettingPan
                && mapView.panOffset.sqrMagnitude > 0
                && m_player.lateralVelocity.sqrMagnitude > 0.01f
            )
                StartCoroutine(ResetPanRoutine());
        }

        protected virtual IEnumerator ResetPanRoutine()
        {
            m_resettingPan = true;

            var t = 0f;
            var duration = resetDuration;

            while (t < duration)
            {
                t += Time.deltaTime;
                mapView.panOffset = Vector2.Lerp(mapView.panOffset, Vector2.zero, t / duration);
                yield return null;
            }

            mapView.panOffset = Vector2.zero;
            m_resettingPan = false;
        }
    }
}
