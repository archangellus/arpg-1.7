using UnityEngine;
using UnityEngine.Events;

namespace PLAYERTWO.ARPGProject
{
    [AddComponentMenu("PLAYER TWO/ARPG Project/Map/Map Marker")]
    public class MapMarker : MonoBehaviour
    {
        [Header("Marker Settings")]
        [Tooltip(
            "Reference to the Map Marker Type defining this marker's appearance and behavior."
        )]
        public MapMarkerType type;

        [Tooltip("The current state of this marker, determining its visibility")]
        [SerializeField]
        protected MapMarkerState m_state = MapMarkerState.Active;

        [Space(15f)]
        public UnityEvent onIconClicked;

        public UnityEvent<MapMarkerState> onStateChanged { get; } = new();
        public UnityEvent<bool> onVisibilityChanged { get; } = new();

        /// <summary>
        /// Indicates whether this marker is currently visible on the map.
        /// </summary>
        public bool isVisible { get; protected set; }

        /// <summary>
        /// Gets the current state of this marker.
        /// </summary>
        public MapMarkerState state => m_state;

        protected virtual void Awake()
        {
            InitializeEntity();
            AssignToMap();
        }

        protected virtual void OnEnable() => SetVisibility(true);

        protected virtual void OnDisable() => SetVisibility(false);

        /// <summary>
        /// Sets the visibility of this marker.
        /// </summary>
        /// <param name="value">The visibility state to set.</param>
        public virtual void SetVisibility(bool value)
        {
            isVisible = value;
            onVisibilityChanged.Invoke(value);
        }

        /// <summary>
        /// Sets the state of this marker, which may affect its appearance and visibility on the map.
        /// </summary>
        /// <param name="state">The new state to set for this marker.</param>
        public virtual void SetState(MapMarkerState state)
        {
            m_state = state;
            onStateChanged.Invoke(state);
        }

        /// <summary>
        /// Convenience method to set the state of this marker to Hidden.
        /// </summary>
        public virtual void SetStateToHidden() => SetState(MapMarkerState.Hidden);

        /// <summary>
        /// Convenience method to set the state of this marker to Active.
        /// </summary>
        public virtual void SetStateToActive() => SetState(MapMarkerState.Active);

        /// <summary>
        /// Convenience method to set the state of this marker to Inactive.
        /// </summary>
        public virtual void SetStateToInactive() => SetState(MapMarkerState.Inactive);

        /// <summary>
        /// Creates the Map Icon instance for this marker.
        /// </summary>
        /// <returns>The created MapIcon component.</returns>
        public virtual MapIcon CreateIcon()
        {
            var instance = Instantiate(type.iconPrefab);
            instance.Fill(this);

            if (onIconClicked != null)
                instance.onClick.AddListener(onIconClicked.Invoke);

            return instance;
        }

        protected virtual void InitializeEntity()
        {
            if (TryGetComponent(out Entity entity))
            {
                entity.onDie.AddListener(() => SetVisibility(false));
                entity.onRevive.AddListener(() => SetVisibility(true));
            }
        }

        protected virtual void AssignToMap()
        {
            if (!Map.instance)
                return;

            Map.instance.AddMarker(this);
        }
    }
}
