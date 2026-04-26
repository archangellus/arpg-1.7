using UnityEngine;
using UnityEngine.AI;
using System.Collections;
using UnityEngine.InputSystem;

namespace ProjectDoors
{
    [RequireComponent(typeof(NavMeshAgent))]
    public class PlayerController : MonoBehaviour
    {
        private Camera mainCamera;
        [HideInInspector] public NavMeshAgent agent;

        /* ─────────── Movement ─────────── */
        [Header("Movement")]
        public float agentSpeed = 3.5f;
        [Tooltip("How quickly the character turns (deg / sec).")]
        public float rotationSpeed = 720f;
        [SerializeField] private float moveDelay = 0.5f;

        /* ─────────── Animation ─────────── */
        [Header("Animation")]
        [SerializeField] private bool enableAnimations = true;
        [SerializeField] private string speedParam = "Speed";
        [SerializeField] private float moveThreshold = 0.1f;

        /* ─────────── Grounding ─────────── */
        [Header("Grounding")]
        [SerializeField] private bool autoGround = true;

        [Header("Terrain Mask")]
        public LayerMask terrainLayerMask;

        /* ─────────── Internals ─────────── */
        private Animator animator;          // null when animations disabled
        private bool isMoving;
        private bool isMoveInitiated;

        #region Unity Lifecycle
        void Start()
        {
            mainCamera = Camera.main;

            if (!TryGetComponent(out agent))
            {
                Debug.LogError("NavMeshAgent component not found!");
                enabled = false;
                return;
            }

            agent.speed = agentSpeed;
            agent.updateRotation = false;   // we control rotation
            agent.updateUpAxis = false;   // stay upright

            if (autoGround) SetBaseOffsetToNavMesh();

            if (enableAnimations)
            {
                animator = GetComponent<Animator>();
                if (!animator)
                    Debug.LogWarning("enableAnimations is TRUE but no Animator found.");
            }
        }

        void Update()
        {
            HandleClickToMove();
            if (enableAnimations) DriveAnimator();
        }

        void LateUpdate()
        {
            RotateIfMoving();

            if (!Mouse.current.leftButton.isPressed && isMoving)
            {
                agent.isStopped = true;
                isMoving = false;
            }
        }
        #endregion

        #region Input / Movement
        private void HandleClickToMove()
        {
            if (!Mouse.current.leftButton.isPressed) return;

            Ray ray = mainCamera.ScreenPointToRay(Mouse.current.position.ReadValue());
            if (!Physics.Raycast(ray, out var hit, Mathf.Infinity, terrainLayerMask)) return;

            if (!agent.isOnNavMesh)
            {
                Debug.LogWarning("NavMeshAgent is not on a NavMesh.");
                return;
            }

            if (isMoving)
            {
                agent.SetDestination(hit.point);
            }
            else if (!isMoveInitiated)
            {
                StartCoroutine(BeginMoveAfterDelay(hit.point));
            }
        }

        private IEnumerator BeginMoveAfterDelay(Vector3 destination)
        {
            isMoveInitiated = true;
            yield return new WaitForSeconds(moveDelay);
            agent.isStopped = false;
            agent.SetDestination(destination);
            isMoving = true;
            isMoveInitiated = false;
        }
        #endregion

        #region Rotation
        private void RotateIfMoving()
        {
            if (!HasSignificantMovement()) return;

            /* ---------- SAFE DIRECTION SELECTION ---------- */
            Vector3 dir = agent.desiredVelocity.sqrMagnitude > 0.0001f
                          ? agent.desiredVelocity
                          : agent.velocity;                        // fall back if desiredVel is zero
            dir.y = 0f;

            if (dir.sqrMagnitude < 0.0001f) return;               // still too small → skip

            Quaternion target = Quaternion.LookRotation(dir, Vector3.up);
            transform.rotation = Quaternion.RotateTowards(
                transform.rotation,
                target,
                rotationSpeed * Time.deltaTime);
        }

        private bool HasSignificantMovement()
        {
            // use BOTH velocity magnitudes so we never rotate on a zero dir
            float vMag = agent.velocity.magnitude;
            float dMag = agent.desiredVelocity.magnitude;

            return agent.hasPath &&
                   !agent.isStopped &&
                   (vMag > moveThreshold || dMag > moveThreshold) &&
                   agent.remainingDistance > 0.05f;
        }
        #endregion

        #region Animation
        private void DriveAnimator()
        {
            if (!animator) return;

            float speed = agent.velocity.magnitude;
            animator.SetFloat(speedParam, speed > moveThreshold ? agentSpeed : 0f);
        }
        #endregion

        #region Grounding
        private void SetBaseOffsetToNavMesh()
        {
            if (!NavMesh.SamplePosition(transform.position, out var hit, 2f, agent.areaMask))
                return;

            float groundY = hit.position.y;
            float dist = Mathf.Clamp(transform.position.y - groundY, 0f, agent.height);

            agent.baseOffset = dist;

            if (TryGetComponent(out CapsuleCollider cap))
                cap.center = new Vector3(cap.center.x, dist + cap.height * 0.5f, cap.center.z);
        }
        #endregion
    }
}
