using UnityEngine;
using UnityEngine.AI;
namespace ProjectDoors
{
    [DisallowMultipleComponent]
    public class LayeredNavMeshObstacle : MonoBehaviour
    {
        [Header("Obstacle Settings")]
        [Tooltip("Specify the layer to which this obstacle applies.")]
        public LayerMask affectedLayer = 1 << 9; // 0 = Default layer 9 = Ai

        [Tooltip("Specify the priority layer to override the affected layer.")]
        public LayerMask priorityLayer = 1 << 8; // 8 = Player

        [Tooltip("Range within which objects with the selected layer will enable the obstacle.")]
        public float activationRange = 4.0f;

        private NavMeshObstacle navMeshObstacle;
        private Transform obstacleTransform;

        private void Awake()
        {
            navMeshObstacle = GetComponent<NavMeshObstacle>();
            obstacleTransform = transform;

            if (navMeshObstacle == null)
            {
                Debug.LogError("NavMeshObstacle component is required but missing.", gameObject);
                enabled = false;
                return;
            }

            navMeshObstacle.enabled = false;
        }

        private void Update()
        {
            UpdateObstacleState();
        }

        private void UpdateObstacleState()
        {
            if (IsPriorityObjectInRange())
            {
                DisableObstacle();
            }
            else if (IsAnyObjectInRange())
            {
                EnableObstacle();
            }
            else
            {
                DisableObstacle();
            }
        }

        private bool IsAnyObjectInRange()
        {
            Collider[] colliders = Physics.OverlapSphere(obstacleTransform.position, activationRange, affectedLayer);
            return colliders.Length > 0;
        }

        private bool IsPriorityObjectInRange()
        {
            Collider[] colliders = Physics.OverlapSphere(obstacleTransform.position, activationRange, priorityLayer);
            return colliders.Length > 0;
        }

        private void EnableObstacle()
        {
            if (navMeshObstacle != null && !navMeshObstacle.enabled)
            {
                navMeshObstacle.enabled = true;
            }
        }

        private void DisableObstacle()
        {
            if (navMeshObstacle != null && navMeshObstacle.enabled)
            {
                navMeshObstacle.enabled = false;
            }
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(transform.position, activationRange);
        }
    }
}
