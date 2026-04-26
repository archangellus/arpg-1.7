using UnityEngine;
using UnityEngine.AI;

namespace ProjectDoors
{
    [RequireComponent(typeof(NavMeshAgent))]
    [AddComponentMenu("ProjectDoors/AI/Random Wander AI")]
    public class RandomWanderAI : MonoBehaviour
    {
        /* ─────────────  Inspector  ───────────── */
        [Header("Wander")]
        [SerializeField] float wanderRadius = 10f;
        [SerializeField] float waitTime = 5f;
        [SerializeField] float walkSpeed = 0.5f;
        [SerializeField] float runSpeed = 2f;
        [SerializeField] float stoppingDistance = 1.3f;

        [Header("Robustness")]
        [SerializeField] float pathRecheckInterval = 2f;
        [SerializeField] float stuckCheckDuration = 3f;

        [Header("Door handling")]
        [SerializeField] float doorDetectRange = 2f;    // look-ahead
        [SerializeField] float maxDoorWait = 3f;    // seconds

        [Header("Global pause")]
        [SerializeField] MovementControlEventChannel movementControl;

        [Header("Animation")]
        [SerializeField] Animator animator;
        [SerializeField] string speedParam = "Speed";

        [Header("Debug")]
        [SerializeField] Color gizmoColor = Color.yellow;

        /* ─────────────  Private  ───────────── */
        NavMeshAgent agent;
        Vector3 wanderCentre;
        Vector3 lastPos;

        float waitTimer, recheckTimer, stuckTimer;
        bool isMoving;

        // door-opening / waiting state
        Door blockingDoor;
        float doorWaitTimer;
        bool waitingForDoor;

        // door-closing state
        Door doorToClose;
        float doorInitialSide;

        readonly LayerMask usableMask = 1 << 6;   // “Usable”

        /* ═════════════  Life-cycle  ═════════════ */
        void Awake()
        {
            agent = GetComponent<NavMeshAgent>();
            if (!animator) animator = GetComponent<Animator>();
        }

        void OnEnable()
        {
            if (movementControl != null)
            {
                movementControl.OnStopMovement += () => agent.isStopped = true;
                movementControl.OnResumeMovement += () => agent.isStopped = false;
            }
        }
        void OnDisable()
        {
            if (movementControl != null)
            {
                movementControl.OnStopMovement -= () => agent.isStopped = true;
                movementControl.OnResumeMovement -= () => agent.isStopped = false;
            }
        }

        void Start()
        {
            wanderCentre = transform.position;
            lastPos = transform.position;
            waitTimer = Random.Range(0, waitTime);
            agent.stoppingDistance = stoppingDistance;
            agent.areaMask = NavMesh.AllAreas;
        }

        void Update()
        {
            float dt = Time.deltaTime;
            waitTimer += dt;
            recheckTimer += dt;

            HandleDoorClosing();          // <── NEW

            if (waitingForDoor)
            {
                HandleDoorWaiting(dt);
            }
            else
            {
                HandleStuck(dt);
                HandlePathRecheck();
                CheckDoorAhead();         // <── look-ahead open
            }

            HandleArrival();
            HandleDeparture();

            if (animator) animator.SetFloat(speedParam, agent.velocity.magnitude);
        }

        /* ═════════════  Door look-ahead / open  ═════════════ */
        void CheckDoorAhead()
        {
            if (!isMoving || agent.pathPending || agent.remainingDistance < stoppingDistance) return;

            if (!Physics.SphereCast(transform.position + Vector3.up * .5f,
                                    .35f,
                                    transform.forward,
                                    out var hit,
                                    doorDetectRange,
                                    usableMask))
                return;

            // Door in front?
            if (hit.collider.TryGetComponent(out Door door) && !door.IsOpen)
            {
                StartWaitingForDoor(door);
                ForceOpen(door);
                return;
            }

            // Lever in front?
            if (hit.collider.TryGetComponent(out DoorLever lever))
            {
                lever.gameObject.SendMessage("ToggleDoor",
                                             SendMessageOptions.DontRequireReceiver);
                Door nearest = FindNearestClosedDoor(hit.point, 3f);
                if (nearest) StartWaitingForDoor(nearest);
            }
        }

        void StartWaitingForDoor(Door door)
        {
            waitingForDoor = true;
            blockingDoor = door;
            doorWaitTimer = 0f;
            agent.isStopped = true;     // park
        }

        void HandleDoorWaiting(float dt)
        {
            doorWaitTimer += dt;

            if (blockingDoor == null || blockingDoor.IsOpen)
            {
                ResumeAfterDoor(opened: true);
                return;
            }

            if (doorWaitTimer >= maxDoorWait)
            {
                ResumeAfterDoor(opened: false);
                ChooseNewPath();        // alternate route
            }
        }

        void ResumeAfterDoor(bool opened)
        {
            if (opened && blockingDoor != null)
            {
                doorToClose = blockingDoor;
                doorInitialSide = Mathf.Sign(
                    Vector3.Dot(doorToClose.transform.forward,
                                transform.position - doorToClose.transform.position));
            }
            waitingForDoor = false;
            blockingDoor = null;
            agent.isStopped = false;
        }

        void ForceOpen(Door door)
        {
            DoorEventChannel ch = door.GetEventChannel();
            if (ch != null)
                ch.RaiseOpen(transform.position, true);   // force
            else
                door.ForceOpen(transform.position);
        }

        /* ═════════════  Close-behind  ═════════════ */
        void HandleDoorClosing()
        {
            if (doorToClose == null) return;
            if (!doorToClose.IsOpen) { doorToClose = null; return; }

            float side = Mathf.Sign(
                Vector3.Dot(doorToClose.transform.forward,
                            transform.position - doorToClose.transform.position));

            if (side != doorInitialSide &&
                Vector3.Distance(transform.position, doorToClose.transform.position) > stoppingDistance + 0.5f)
            {
                DoorEventChannel ch = doorToClose.GetEventChannel();
                if (ch != null) ch.RaiseClose(true);
                else doorToClose.ForceClose();
                doorToClose = null;
            }
        }

        /* ═════════════  Stuck logic & path  ═════════════ */
        void HandleStuck(float dt)
        {
            if (!isMoving) return;

            float moved = Vector3.Distance(transform.position, lastPos);
            lastPos = transform.position;

            if (moved < 0.05f) stuckTimer += dt;
            else stuckTimer = 0f;

            if (stuckTimer >= stuckCheckDuration)
            {
                ChooseNewPath();
                stuckTimer = 0f;
            }
        }

        void HandlePathRecheck()
        {
            if (!isMoving || recheckTimer < pathRecheckInterval) return;

            if (agent.pathStatus != NavMeshPathStatus.PathComplete &&
                agent.remainingDistance > stoppingDistance * 4f)
            {
                Vector3 alt = FindBetterPoint();
                if (alt != Vector3.zero) agent.SetDestination(alt);
            }
            recheckTimer = 0f;
        }

        /* ═════════════  Arrival / departure  ═════════════ */
        void HandleArrival()
        {
            if (!isMoving || agent.pathPending) return;

            if (agent.remainingDistance <= stoppingDistance &&
                (agent.hasPath == false || agent.velocity.sqrMagnitude == 0f))
            {
                isMoving = false;
                waitTimer = 0f;
            }
        }

        void HandleDeparture()
        {
            if (isMoving || waitTimer < waitTime) return;

            if (Vector3.Distance(transform.position, wanderCentre) > wanderRadius * 1.5f)
                wanderCentre = transform.position;

            agent.speed = Random.Range(walkSpeed, runSpeed);
            agent.SetDestination(RandomPoint(wanderCentre, wanderRadius));
            isMoving = true;
            waitTimer = 0f;
        }

        /* ═════════════  Path helpers  ═════════════ */
        Vector3 RandomPoint(Vector3 origin, float dist)
        {
            for (int i = 0; i < 20; i++)
            {
                Vector3 p = Random.insideUnitSphere * dist + origin;
                if (NavMesh.SamplePosition(p, out NavMeshHit hit, dist, agent.areaMask))
                    return hit.position;
            }
            return origin;
        }

        Vector3 FindBetterPoint()
        {
            Vector3 best = Vector3.zero; float bestLen = Mathf.Infinity;
            for (int i = 0; i < 10; i++)
            {
                Vector3 p = RandomPoint(transform.position, wanderRadius);
                if (NavMesh.Raycast(transform.position, p, out _, NavMesh.AllAreas)) continue;
                float d = Vector3.Distance(transform.position, p);
                if (d < bestLen) { bestLen = d; best = p; }
            }
            return best;
        }

        void ChooseNewPath()
        {
            agent.speed = Random.Range(walkSpeed, runSpeed);
            agent.SetDestination(RandomPoint(wanderCentre, wanderRadius));
        }

        /* ═════════════  Debug  ═════════════ */
        void OnDrawGizmosSelected()
        {
            // --- Wander area --------------------------------------------------
            Gizmos.color = gizmoColor;
            Vector3 centre = Application.isPlaying ? wanderCentre : transform.position;
            Gizmos.DrawWireSphere(centre, wanderRadius);

            // --- Door-detect sweep -------------------------------------------
            Vector3 origin = transform.position + Vector3.up * 0.5f;
            Vector3 end = origin + transform.forward * doorDetectRange;

            Gizmos.color = Color.cyan;            // sweep line
            Gizmos.DrawLine(origin, end);

            Gizmos.color = Color.red;             // cast radius (0.35 m)
            Gizmos.DrawWireSphere(end, 0.35f);
        }
        /// <summary>
        /// Finds the nearest *closed* Door within <paramref name="maxRange"/> of a point.
        /// Returns null if every nearby door is already open (or none found).
        /// </summary>
        Door FindNearestClosedDoor(Vector3 from, float maxRange)
        {
            Collider[] cols = Physics.OverlapSphere(from, maxRange, usableMask);
            float best = Mathf.Infinity;
            Door bestDoor = null;

            foreach (Collider c in cols)
            {
                if (c.TryGetComponent(out Door d) && !d.IsOpen)
                {
                    float dist = Vector3.Distance(from, d.transform.position);
                    if (dist < best)
                    {
                        best = dist;
                        bestDoor = d;
                    }
                }
            }
            return bestDoor;
        }

    }
}
