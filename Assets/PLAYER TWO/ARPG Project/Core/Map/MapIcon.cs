using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace PLAYERTWO.ARPGProject
{
    [RequireComponent(typeof(Image), typeof(Button))]
    [AddComponentMenu("PLAYER TWO/ARPG Project/Map/Map Icon")]
    public class MapIcon : MonoBehaviour
    {
        public UnityEvent onClick;

        protected Image m_image;
        protected Button m_button;

        /// <summary>
        /// The MapMarker associated with this icon.
        /// </summary>
        public MapMarker marker { get; protected set; }

        /// <summary>
        /// Gets the Image component of this icon, caching it if necessary.
        /// </summary>
        public Image image
        {
            get
            {
                if (!m_image)
                    m_image = GetComponent<Image>();

                return m_image;
            }
        }

        /// <summary>
        /// Gets the Button component of this icon, caching it if necessary and
        /// adding a listener to invoke the onClick event.
        /// </summary>
        public Button button
        {
            get
            {
                if (!m_button)
                {
                    m_button = GetComponent<Button>();
                    m_button.onClick.AddListener(onClick.Invoke);
                }

                return m_button;
            }
        }

        /// <summary>
        /// Indicates whether this icon is currently visible on the map,
        /// based on its Image component's enabled state.
        /// </summary>
        public bool isVisible => image.enabled;

        /// <summary>
        /// Initializes this icon with the given MapMarker, setting up
        /// its appearance and event listeners based on the marker's properties.
        /// </summary>
        /// <param name="marker">The MapMarker to associate with this icon.</param>
        public virtual void Fill(MapMarker marker)
        {
            this.marker = marker;
            image.sprite = marker.type.sprite;
            image.color =
                marker.state == MapMarkerState.Active
                    ? marker.type.color
                    : marker.type.colorInactive;
            image.rectTransform.sizeDelta = marker.type.size;
            image.enabled = marker.state != MapMarkerState.Hidden && marker.isVisible;
            button.interactable = marker.state == MapMarkerState.Active;
            marker.onStateChanged.AddListener(OnMarkerStateChanged);
            marker.onVisibilityChanged.AddListener(OnMapMarkerVisibilityChanged);
        }

        protected virtual void OnMarkerStateChanged(MapMarkerState state)
        {
            if (!image)
                return;

            image.enabled = state != MapMarkerState.Hidden && marker.isVisible;
            image.color =
                state == MapMarkerState.Active ? marker.type.color : marker.type.colorInactive;
            button.interactable = state == MapMarkerState.Active;
        }

        protected virtual void OnMapMarkerVisibilityChanged(bool isVisible)
        {
            if (!image)
                return;

            image.enabled = isVisible && marker.state != MapMarkerState.Hidden;
            button.interactable = isVisible && marker.state == MapMarkerState.Active;
        }
    }
}
