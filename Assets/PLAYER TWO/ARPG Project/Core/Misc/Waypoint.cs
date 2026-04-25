using UnityEngine;
using UnityEngine.Events;

namespace PLAYERTWO.ARPGProject
{
    [RequireComponent(typeof(Collider))]
    [AddComponentMenu("PLAYER TWO/ARPG Project/Misc/Waypoint")]
    public class Waypoint : MonoBehaviour
    {
        public UnityEvent onActive;

        [Header("Waypoint Settings")]
        [Tooltip("If true, this Waypoint will automatic activate when the Player gets close.")]
        public bool autoActive = true;

        [Tooltip("The minimum distance from the Waypoint to activate it.")]
        public float activationRadius = 10f;

        [Tooltip("The title of this Waypoint.")]
        public string title = "New Waypoint";

        protected float m_startTime;
        protected bool m_active = false;
        protected Collider m_collider;

        protected const float k_triggerActivationDelay = 0.1f;

        protected LevelRespawner m_respawner => LevelRespawner.instance;
        protected LevelWaypoints m_waypoints => LevelWaypoints.instance;

        /// <summary>
        /// Returns true if this Waypoint is activated.
        /// </summary>
        public bool active
        {
            get { return m_active; }
            set
            {
                if (!m_active && value)
                    onActive.Invoke();

                m_active = value;
            }
        }

        protected Entity m_player => Level.instance.player;

        /// <summary>
        /// Returns the position and rotation of this Waypoint.
        /// </summary>
        public virtual SpacePoint GetSpacePoint()
        {
            var position = transform.position;
            position += Vector3.up * m_player.controller.height * 0.5f;
            return new(position, transform.rotation);
        }

        protected virtual void Start()
        {
            m_collider = GetComponent<Collider>();
            m_collider.isTrigger = true;
            m_startTime = Time.time;
        }

        protected virtual void Update()
        {
            var distance = Vector3.Distance(m_player.position, transform.position);

            if (distance <= activationRadius)
            {
                m_waypoints.SetCurrentWaypoint(this);

                if (!active && autoActive)
                {
                    active = true;
                    Level.instance.UpdateSceneData();
                }
            }
        }

        protected virtual bool CanTrigger() =>
            active
            && !m_waypoints.traveling
            && !m_respawner.isRespawning
            && Time.timeSinceLevelLoad > k_triggerActivationDelay;

        protected virtual void OnTriggerEnter(Collider other)
        {
            if (!CanTrigger() || !other.IsPlayer())
                return;

            m_player.StandStill();
            m_player.inputs.LockMoveDirection();
            m_waypoints.currentWaypoint = this;
            GUIWindowsManager.instance.waypointsWindow.Show();
        }
    }
}
