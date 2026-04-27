using System.Reflection;
using UnityEngine;
using UnityEngine.AI;

// The original namespace conflicted with Unity's CharacterController component.
// Renamed the namespace to avoid a type/namespace collision.
namespace PLAYERTWO.ARPGProject.Controllers
{
    /// <summary>
    /// Additional Entity behaviour restored from the sample Entity.cs.
    /// Provides click dead-zone handling and utilities for destination
    /// distance calculations.
    /// </summary>
    [RequireComponent(typeof(Entity))]
    public class CharacterControllerExtras : MonoBehaviour
    {
        [Header("Movement")]
        [Tooltip("Horizontal radius around the Entity where clicks are ignored (prevents tiny micro-moves under the feet).")]
        public float clickDeadZoneRadius = 1f;
        [Tooltip("How close (X-Z) the entity must be to consider a destination reached.")]
        public float destinationStopRadius = 0.15f;
        [Tooltip("Minimum (X-Z) distance between consecutive movement requests to trigger a new path calculation.")]
        public float repathDistanceThreshold = 0.08f;
        [Tooltip("Minimum (X-Z) click distance accepted by EventBus movement commands. Set to 0 to disable filtering.")]
        public float minimumCommandDistance = 0.05f;
        [Tooltip("Minimum (X-Z) distance required to trigger movement. Set to 0 to allow very small moves.")]
        public float minimumMoveDistance = 0.25f;
        [Tooltip("Maximum click-to-move range in world units (X-Z). Set to 0 for unlimited.")]
        public float maxClickMoveRange = 0f;
        [Tooltip("Layers that should not trigger point-and-click movement when clicked.")]
        public LayerMask ignoredMovementLayers = 0;

        private Entity m_entity;
        private Vector3 m_lastRequestedPoint;
        private bool m_hasLastRequestedPoint;

        // Cached reflection info for accessing internal waypoint data on Entity
        FieldInfo m_waypointsField;
        FieldInfo m_waypointsSizeField;
        FieldInfo m_currentWaypointField;
        FieldInfo m_moveSpeedField;

        void Awake()
        {
            CacheEntity();

            // Grab internal fields so we can mirror the example Entity behaviour
            var type = typeof(Entity);
            m_waypointsField = type.GetField("m_waypoints", BindingFlags.NonPublic | BindingFlags.Instance);
            m_waypointsSizeField = type.GetField("m_waypointsSize", BindingFlags.NonPublic | BindingFlags.Instance);
            m_currentWaypointField = type.GetField("m_currentWaypoint", BindingFlags.NonPublic | BindingFlags.Instance);
            m_moveSpeedField = type.GetField("m_moveSpeed", BindingFlags.NonPublic | BindingFlags.Instance);
        }

        /// <summary>
        /// Ensures that we have a reference to the Entity component.
        /// </summary>
        /// <returns>True when an Entity reference is available.</returns>
        bool CacheEntity()
        {
            if (m_entity == null)
            {
                m_entity = GetComponent<Entity>();

                if (m_entity == null)
                {
                    Debug.LogWarning($"{nameof(CharacterControllerExtras)} on {name} requires an Entity component.");
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Returns true when point lies inside the click dead-zone (X-Z plane).
        /// </summary>
        public bool IsInsideClickDeadZone(Vector3 point)
        {
            if (!CacheEntity())
                return true;

            point.y = m_entity.position.y; // flatten
            float sqrDist = (point - m_entity.position).sqrMagnitude;
            return sqrDist <= clickDeadZoneRadius * clickDeadZoneRadius;
        }

        /// <summary>
        /// Returns true when point lies inside the EventBus command-noise zone (X-Z plane).
        /// This is a separate threshold from the click dead-zone and can be disabled.
        /// </summary>
        public bool IsInsideCommandDistance(Vector3 point)
        {
            if (!CacheEntity())
                return true;

            if (minimumCommandDistance <= 0f)
                return false;

            point.y = m_entity.position.y;
            var sqrDist = (point - m_entity.position).sqrMagnitude;
            return sqrDist <= minimumCommandDistance * minimumCommandDistance;
        }

        /// <summary>
        /// Moves the entity to a given point while respecting the click dead-zone.
        /// </summary>
        public bool MoveTo(Vector3 point)
        {
            if (!CacheEntity())
                return false;

            // Keep original Y for pathing accuracy (terrain/ramps), but use planar
            // comparisons for dead-zone and thresholds.
            Vector3 planarPoint = new Vector3(point.x, m_entity.position.y, point.z);

            // Skip tiny micro-moves near the feet
            if (IsInsideClickDeadZone(planarPoint))
            {
                StopEntityMovement();
                return false;
            }

            // Match actual movement gate with a configurable distance threshold.
            if (minimumMoveDistance > 0f)
            {
                var moveDelta = planarPoint - m_entity.position;
                moveDelta.y = 0f;

                if (moveDelta.sqrMagnitude <= minimumMoveDistance * minimumMoveDistance)
                {
                    StopEntityMovement();
                    return false;
                }
            }

            // Optional click-to-move range clamp.
            if (maxClickMoveRange > 0f)
            {
                var planarDelta = planarPoint - m_entity.position;
                planarDelta.y = 0f;

                if (planarDelta.sqrMagnitude > maxClickMoveRange * maxClickMoveRange)
                {
                    var clampedPlanar = m_entity.position + planarDelta.normalized * maxClickMoveRange;
                    point = new Vector3(clampedPlanar.x, point.y, clampedPlanar.z);

                    // Try to project the clamped point to NavMesh for better reliability.
                    if (NavMesh.SamplePosition(point, out var navHit, 3f, NavMesh.AllAreas))
                        point = navHit.position;
                }
            }

            // Prevent path recalculation spam when the same destination is requested
            // repeatedly every frame (mouse held down over terrain).
            if (m_hasLastRequestedPoint)
            {
                var repeatDelta = point - m_lastRequestedPoint;
                repeatDelta.y = 0f;

                if (repeatDelta.sqrMagnitude <= repathDistanceThreshold * repathDistanceThreshold)
                {
                    // If we are already near this destination, force a clean stop.
                    var toPoint = planarPoint - m_entity.position;
                    toPoint.y = 0f;
                    if (toPoint.sqrMagnitude <= destinationStopRadius * destinationStopRadius)
                    {
                        StopEntityMovement();
                        return false;
                    }

                    return true;
                }
            }

            m_lastRequestedPoint = point;
            m_hasLastRequestedPoint = true;

            // Use TryCalculatePath directly so the plugin controls the move
            // threshold instead of the internal hardcoded Entity minimum distance.
            if (m_entity.canUpdateDestination && m_entity.TryCalculatePath(point))
            {
                m_entity.states.ChangeTo<MoveToDestinationEntityState>();
                return true;
            }

            return false;
        }

        /// <summary>
        /// Returns true when the given layer is configured to be ignored for
        /// movement clicks.
        /// </summary>
        public bool IsMovementLayerIgnored(int layer) =>
            (ignoredMovementLayers.value & (1 << layer)) != 0;

        /// <summary>
        /// Returns the 3D distance from the Entity's current position to the
        /// last NavMesh waypoint, matching the behaviour in the example
        /// <c>Entity</c> script.
        /// </summary>
        public float GetDistanceToDestination()
        {
            if (!CacheEntity())
                return 0f;

            var waypoints = m_waypointsField?.GetValue(m_entity) as Vector3[];
            int size = m_waypointsSizeField != null ? (int)m_waypointsSizeField.GetValue(m_entity) : 0;

            if (waypoints == null || size == 0)
                return 0f;

            return Vector3.Distance(m_entity.position, waypoints[size - 1]);
        }

        /// <summary>
        /// Mirrors the waypoint movement logic from the example Entity script
        /// so plug-ins can drive navigation through the EventBus.
        /// </summary>
        public void HandleWaypointMovement()
        {
            if (!CacheEntity())
                return;

            if (m_currentWaypointField == null || m_waypointsField == null || m_waypointsSizeField == null)
                return;

            int current = (int)m_currentWaypointField.GetValue(m_entity);
            int size = (int)m_waypointsSizeField.GetValue(m_entity);
            var waypoints = m_waypointsField.GetValue(m_entity) as Vector3[];

            if (current < 0 || waypoints == null || current + 1 >= size)
                return;

            Vector3 waypoint = waypoints[current + 1];
            Vector3 point = new Vector3(waypoint.x, m_entity.position.y, waypoint.z);
            Vector3 direction = point - m_entity.position;
            float distance = direction.magnitude;

            if (distance <= destinationStopRadius)
            {
                current++;
                m_currentWaypointField.SetValue(m_entity, current);

                if (current >= size - 1)
                {
                    StopEntityMovement();
                    return;
                }

                waypoint = waypoints[current + 1];
                point = new Vector3(waypoint.x, m_entity.position.y, waypoint.z);
                direction = point - m_entity.position;
                distance = direction.magnitude;
            }

            if (distance <= 0.0001f)
            {
                StopEntityMovement();
                return;
            }

            direction = direction.normalized;
            float moveSpeed = m_moveSpeedField != null ? (float)m_moveSpeedField.GetValue(m_entity) : 0f;
            m_entity.lateralVelocity = direction * moveSpeed;
            m_entity.lookDirection = direction;
        }

        /// <summary>
        /// Smoothly rotates the entity to face a given direction, guarding
        /// against zero or NaN vectors to mirror the sample behaviour.
        /// </summary>
        public void FaceTo(Vector3 direction)
        {
            if (!CacheEntity())
                return;

            direction.y = 0f;
            if (direction.sqrMagnitude < 0.0001f || float.IsNaN(direction.x))
                return;

            float delta = m_entity.rotationSpeed * Time.deltaTime;
            var targetRot = Quaternion.LookRotation(direction, Vector3.up);
            m_entity.transform.rotation = Quaternion.RotateTowards(
                m_entity.transform.rotation,
                targetRot,
                delta
            );
        }

        void StopEntityMovement()
        {
            if (m_currentWaypointField != null)
                m_currentWaypointField.SetValue(m_entity, -1);

            m_entity.velocity = Vector3.zero;
            m_entity.lateralVelocity = Vector3.zero;
            m_hasLastRequestedPoint = false;
        }

#if UNITY_EDITOR
        // Visualise the click-dead-zone in Scene view.
        void OnDrawGizmosSelected()
        {
            // Click dead-zone only (under-player ignore area).
            Color c = new Color(1f, 1f, 0f, 0.45f);
            Gizmos.color = c;
            Gizmos.DrawWireSphere(transform.position, clickDeadZoneRadius);

            c.a = 0.07f;
            Gizmos.color = c;
            Gizmos.DrawSphere(transform.position, clickDeadZoneRadius);

            // Minimum move distance threshold.
            c = new Color(0.3f, 1f, 0.3f, 0.55f);
            Gizmos.color = c;
            Gizmos.DrawWireSphere(transform.position, minimumMoveDistance);

            // EventBus command-noise distance.
            if (minimumCommandDistance > 0f)
            {
                c = new Color(0.2f, 0.8f, 1f, 0.55f);
                Gizmos.color = c;
                Gizmos.DrawWireSphere(transform.position, minimumCommandDistance);
            }

            // Optional max click-to-move range.
            if (maxClickMoveRange > 0f)
            {
                c = new Color(1f, 0f, 1f, 0.55f);
                Gizmos.color = c;
                Gizmos.DrawWireSphere(transform.position, maxClickMoveRange);
            }
        }
#endif
    }
}
