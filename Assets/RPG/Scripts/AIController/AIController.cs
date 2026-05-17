using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public class AIController : MonoBehaviour
{
    public AIProfile currentProfile;

    [Header("Player Search")]
    public float playerSearchInterval = 1f;
    public string playerTAG = "Player";

    [Header("Animation")]
    public string animatorParameter = "Speed";

    [Header("Character Controller Movement")]
    [Tooltip("Used only when this GameObject has a CharacterController instead of a NavMeshAgent.")]
    public float characterControllerTurnSpeed = 720f;
    public float gravity = -9.81f;

    [Tooltip("Small buffer that prevents tiny stop/start corrections when the pet is already close enough to its target.")]
    public float arrivalTolerance = 0.05f;

    [Header("Gizmos")]
    [Tooltip("Turns all AI profile gizmos on or off for this controller.")]
    public bool showGizmos = true;

    [Tooltip("When enabled, range gizmos are sampled down onto the ground so they follow uneven terrain instead of drawing as a perfectly flat disk.")]
    public bool alignGizmosToGround = true;

    [Tooltip("Layers that are allowed to count as ground for ground-aligned gizmos. Exclude the pet/player layers if their colliders get in the way.")]
    public LayerMask gizmoGroundMask = ~0;

    [Tooltip("Whether ground-alignment raycasts should hit trigger colliders.")]
    public QueryTriggerInteraction gizmoTriggerInteraction = QueryTriggerInteraction.Ignore;

    [Tooltip("How far above each sampled gizmo point the ground raycast starts.")]
    public float gizmoGroundRaycastHeight = 10f;

    [Tooltip("How far below each sampled gizmo point the ground raycast searches.")]
    public float gizmoGroundRaycastDepth = 50f;

    [Tooltip("Fallback number of samples used by controller helper gizmos when a profile does not provide its own segment count.")]
    public int gizmoGroundSampleSegments = 48;

    private float stoppingDistance = 0.1f;
    private float movementSpeed = 3.5f;
    private float verticalVelocity;

    private Vector3 startPoint;
    private Vector3 initialStartPoint;
    private Vector3 currentTargetPosition;
    private Vector3 currentVelocity;
    private Vector3 lastPosition;

    private NavMeshAgent agent;
    private CharacterController characterController;
    private Animator animator;
    private Transform playerTransform;

    private bool isWaiting;
    private bool animationReset;
    private bool movementPaused;

    private static Material gizmoMaterial;

    public Vector3 initialPosition { get; private set; }

    private bool HasNavMeshAgent => agent != null && agent.enabled && agent.isOnNavMesh;
    private bool HasCharacterController => characterController != null && characterController.enabled;

    private void Awake()
    {
        TryGetComponent(out agent);
        TryGetComponent(out characterController);
        TryGetComponent(out animator);
    }

    private void Start()
    {
        startPoint = transform.position;
        initialStartPoint = startPoint;
        currentTargetPosition = startPoint;
        lastPosition = transform.position;

        if (initialPosition == Vector3.zero)
        {
            initialPosition = transform.position;
        }

        stoppingDistance = currentProfile != null ? currentProfile.stoppingDistance : stoppingDistance;
        movementSpeed = agent != null ? agent.speed : movementSpeed;

        if (agent != null)
        {
            agent.stoppingDistance = stoppingDistance;
        }

        if (animator != null)
        {
            animator.SetFloat(animatorParameter, 0f);
        }

        StartCoroutine(FindPlayer());
    }

    private void Update()
    {
        if (currentProfile == null)
        {
            return;
        }

        Vector3 targetPosition = currentProfile.GetTargetPosition(currentTargetPosition, startPoint, this);
        SetDestination(targetPosition);

        UpdateMovement();
        UpdateProfileAnimationState();
    }

    private void UpdateMovement()
    {
        Vector3 positionBeforeMove = transform.position;

        if (HasCharacterController && !HasNavMeshAgent)
        {
            MoveWithCharacterController();
        }

        if (HasNavMeshAgent)
        {
            currentVelocity = agent.velocity;
        }
        else
        {
            float deltaTime = Mathf.Max(Time.deltaTime, 0.0001f);
            currentVelocity = (transform.position - positionBeforeMove) / deltaTime;
        }

        lastPosition = transform.position;
    }

    private void MoveWithCharacterController()
    {
        Vector3 toTarget = currentTargetPosition - transform.position;
        toTarget.y = 0f;

        Vector3 horizontalDelta = Vector3.zero;
        float distance = toTarget.magnitude;
        float stopDistance = GetStoppingDistance();
        float stopBuffer = Mathf.Max(0f, arrivalTolerance);

        if (!movementPaused && distance > stopDistance + stopBuffer)
        {
            float maxStep = movementSpeed * Time.deltaTime;
            float step = Mathf.Min(maxStep, distance - stopDistance);

            if (step > 0.0001f)
            {
                Vector3 direction = toTarget / distance;
                horizontalDelta = direction * step;

                if (direction.sqrMagnitude > 0.0001f)
                {
                    Quaternion targetRotation = Quaternion.LookRotation(direction, Vector3.up);
                    transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRotation, characterControllerTurnSpeed * Time.deltaTime);
                }
            }
        }

        if (characterController.isGrounded && verticalVelocity < 0f)
        {
            verticalVelocity = -2f;
        }

        verticalVelocity += gravity * Time.deltaTime;
        Vector3 verticalDelta = Vector3.up * (verticalVelocity * Time.deltaTime);

        CollisionFlags flags = characterController.Move(horizontalDelta + verticalDelta);
        if ((flags & CollisionFlags.Below) != 0 && verticalVelocity < 0f)
        {
            verticalVelocity = -2f;
        }
    }

    private void UpdateProfileAnimationState()
    {
        if (animator == null || isWaiting || animationReset)
        {
            return;
        }

        if (currentProfile is AIWaypointsProfile waypointProfile)
        {
            string currentAnimation = waypointProfile.GetCurrentAnimation();
            if (!animator.GetCurrentAnimatorStateInfo(0).IsName(waypointProfile.defaultAnimation) &&
                !animator.GetCurrentAnimatorStateInfo(0).IsName(currentAnimation))
            {
                animator.Play(waypointProfile.defaultAnimation);
                animationReset = true;
            }
        }
        else
        {
            UpdateAnimationBasedOnSpeed();
        }
    }

    private void UpdateAnimationBasedOnSpeed()
    {
        if (animator == null)
        {
            return;
        }

        float horizontalSpeed = new Vector3(currentVelocity.x, 0f, currentVelocity.z).magnitude;

        if (horizontalSpeed > 1.5f)
        {
            animator.SetFloat(animatorParameter, 2f);
        }
        else if (horizontalSpeed > 0.1f)
        {
            animator.SetFloat(animatorParameter, 1f);
        }
        else
        {
            animator.SetFloat(animatorParameter, 0f);
        }
    }

    private IEnumerator FindPlayer()
    {
        while (playerTransform == null)
        {
            GameObject player = GameObject.FindGameObjectWithTag(playerTAG);

            if (player != null)
            {
                playerTransform = player.transform;
            }
            else
            {
                yield return new WaitForSeconds(playerSearchInterval);
            }
        }
    }

    public void SetDestination(Vector3 targetPosition)
    {
        currentTargetPosition = targetPosition;

        if (HasNavMeshAgent && !movementPaused)
        {
            agent.stoppingDistance = GetStoppingDistance();

            Vector3 flatTarget = targetPosition;
            flatTarget.y = transform.position.y;
            float flatDistance = Vector3.Distance(transform.position, flatTarget);

            if (flatDistance <= GetStoppingDistance() + Mathf.Max(0f, arrivalTolerance))
            {
                agent.ResetPath();
                agent.velocity = Vector3.zero;
                return;
            }

            if (agent.isStopped)
            {
                agent.isStopped = false;
            }

            agent.SetDestination(targetPosition);
        }
    }

    public Vector3 StopAtCurrentPosition()
    {
        currentTargetPosition = transform.position;
        currentVelocity = Vector3.zero;

        if (HasNavMeshAgent && !movementPaused)
        {
            agent.ResetPath();
            agent.velocity = Vector3.zero;
        }

        return currentTargetPosition;
    }

    public void SetMoveSpeed(float speed)
    {
        movementSpeed = Mathf.Max(0f, speed);

        if (agent != null)
        {
            agent.speed = movementSpeed;
        }
    }

    public float GetMoveSpeed()
    {
        return movementSpeed;
    }

    public float GetStoppingDistance()
    {
        return currentProfile != null ? currentProfile.stoppingDistance : stoppingDistance;
    }

    public float GetRemainingDistance()
    {
        if (HasNavMeshAgent)
        {
            if (agent.pathPending)
            {
                return Mathf.Infinity;
            }

            if (agent.hasPath)
            {
                return agent.remainingDistance;
            }
        }

        Vector3 toTarget = currentTargetPosition - transform.position;
        toTarget.y = 0f;
        float distance = toTarget.magnitude;

        if (distance <= GetStoppingDistance() + Mathf.Max(0f, arrivalTolerance))
        {
            return GetStoppingDistance();
        }

        return distance;
    }

    public Vector3 GetCurrentTargetPosition()
    {
        return currentTargetPosition;
    }

    public Vector3 GetMovementVelocity()
    {
        return currentVelocity;
    }

    public float GetMovementSpeedMagnitude()
    {
        Vector3 horizontalVelocity = new Vector3(currentVelocity.x, 0f, currentVelocity.z);
        return horizontalVelocity.magnitude;
    }

    public float GetPlayerSpeed()
    {
        return GetSpeedFromTransform(playerTransform);
    }

    public float GetSpeedFromTransform(Transform target)
    {
        if (target == null)
        {
            return 0f;
        }

        if (target.TryGetComponent(out NavMeshAgent targetAgent))
        {
            Vector3 velocity = targetAgent.velocity;
            velocity.y = 0f;
            return velocity.magnitude;
        }

        if (target.TryGetComponent(out CharacterController targetCharacterController))
        {
            Vector3 velocity = targetCharacterController.velocity;
            velocity.y = 0f;
            return velocity.magnitude;
        }

        return 0f;
    }

    private void SetMovementPaused(bool paused)
    {
        movementPaused = paused;

        if (HasNavMeshAgent)
        {
            agent.isStopped = paused;
        }
    }

    private void OnDrawGizmos()
    {
        if (!showGizmos || currentProfile == null)
        {
            return;
        }

        if (Application.isPlaying)
        {
            currentProfile.DrawGizmos(initialStartPoint, this);
        }
        else
        {
            currentProfile.DrawGizmos(transform.position, this);
        }
    }

    public bool ShouldDrawGizmos()
    {
        return showGizmos;
    }

    public Vector3 GetGroundAlignedGizmoPoint(Vector3 worldPosition, float yOffset = 0f)
    {
        if (!alignGizmosToGround)
        {
            return worldPosition + Vector3.up * yOffset;
        }

        float rayHeight = Mathf.Max(0.01f, gizmoGroundRaycastHeight);
        float rayDepth = Mathf.Max(0.01f, gizmoGroundRaycastDepth);
        Vector3 rayOrigin = worldPosition + Vector3.up * rayHeight;
        float rayDistance = rayHeight + rayDepth;

        RaycastHit[] hits = Physics.RaycastAll(rayOrigin, Vector3.down, rayDistance, gizmoGroundMask, gizmoTriggerInteraction);
        if (hits != null && hits.Length > 0)
        {
            Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

            foreach (RaycastHit hit in hits)
            {
                if (hit.collider == null)
                {
                    continue;
                }

                Transform hitTransform = hit.collider.transform;
                if (hitTransform == transform || hitTransform.IsChildOf(transform))
                {
                    continue;
                }

                return hit.point + Vector3.up * yOffset;
            }
        }

        return worldPosition + Vector3.up * yOffset;
    }

    public void DrawGroundAlignedFilledCircle(Vector3 center, float radius, Color color, int segments, float yOffset = 0f)
    {
        if (radius <= 0f || !PrepareGizmoMaterial())
        {
            return;
        }

        int safeSegments = Mathf.Max(8, segments > 0 ? segments : gizmoGroundSampleSegments);
        Vector3 alignedCenter = GetGroundAlignedGizmoPoint(center, yOffset);

        GL.PushMatrix();
        GL.Begin(GL.TRIANGLES);
        GL.Color(color);

        for (int i = 0; i < safeSegments; i++)
        {
            float angle1 = Mathf.Deg2Rad * (360f / safeSegments) * i;
            float angle2 = Mathf.Deg2Rad * (360f / safeSegments) * (i + 1);

            Vector3 vertex1 = center + new Vector3(Mathf.Cos(angle1) * radius, 0f, Mathf.Sin(angle1) * radius);
            Vector3 vertex2 = center + new Vector3(Mathf.Cos(angle2) * radius, 0f, Mathf.Sin(angle2) * radius);

            GL.Vertex(alignedCenter);
            GL.Vertex(GetGroundAlignedGizmoPoint(vertex1, yOffset));
            GL.Vertex(GetGroundAlignedGizmoPoint(vertex2, yOffset));
        }

        GL.End();
        GL.PopMatrix();
    }

    public void DrawGroundAlignedFilledRing(Vector3 center, float innerRadius, float outerRadius, Color color, int segments, float yOffset = 0f)
    {
        if (outerRadius <= 0f || !PrepareGizmoMaterial())
        {
            return;
        }

        innerRadius = Mathf.Clamp(innerRadius, 0f, outerRadius);
        if (innerRadius <= 0.0001f)
        {
            DrawGroundAlignedFilledCircle(center, outerRadius, color, segments, yOffset);
            return;
        }

        int safeSegments = Mathf.Max(8, segments > 0 ? segments : gizmoGroundSampleSegments);

        GL.PushMatrix();
        GL.Begin(GL.TRIANGLES);
        GL.Color(color);

        for (int i = 0; i < safeSegments; i++)
        {
            float angle1 = Mathf.Deg2Rad * (360f / safeSegments) * i;
            float angle2 = Mathf.Deg2Rad * (360f / safeSegments) * (i + 1);

            Vector3 outer1 = GetGroundAlignedGizmoPoint(center + new Vector3(Mathf.Cos(angle1) * outerRadius, 0f, Mathf.Sin(angle1) * outerRadius), yOffset);
            Vector3 outer2 = GetGroundAlignedGizmoPoint(center + new Vector3(Mathf.Cos(angle2) * outerRadius, 0f, Mathf.Sin(angle2) * outerRadius), yOffset);
            Vector3 inner1 = GetGroundAlignedGizmoPoint(center + new Vector3(Mathf.Cos(angle1) * innerRadius, 0f, Mathf.Sin(angle1) * innerRadius), yOffset);
            Vector3 inner2 = GetGroundAlignedGizmoPoint(center + new Vector3(Mathf.Cos(angle2) * innerRadius, 0f, Mathf.Sin(angle2) * innerRadius), yOffset);

            GL.Vertex(outer1);
            GL.Vertex(outer2);
            GL.Vertex(inner2);

            GL.Vertex(outer1);
            GL.Vertex(inner2);
            GL.Vertex(inner1);
        }

        GL.End();
        GL.PopMatrix();
    }

    public void DrawGroundAlignedFilledSquare(Vector3 center, float sideLength, Color color, int subdivisions, float yOffset = 0f)
    {
        if (sideLength <= 0f || !PrepareGizmoMaterial())
        {
            return;
        }

        int safeSubdivisions = Mathf.Max(1, subdivisions);
        float half = sideLength * 0.5f;
        float step = sideLength / safeSubdivisions;

        GL.PushMatrix();
        GL.Begin(GL.TRIANGLES);
        GL.Color(color);

        for (int x = 0; x < safeSubdivisions; x++)
        {
            for (int z = 0; z < safeSubdivisions; z++)
            {
                Vector3 p00 = center + new Vector3(-half + x * step, 0f, -half + z * step);
                Vector3 p10 = center + new Vector3(-half + (x + 1) * step, 0f, -half + z * step);
                Vector3 p01 = center + new Vector3(-half + x * step, 0f, -half + (z + 1) * step);
                Vector3 p11 = center + new Vector3(-half + (x + 1) * step, 0f, -half + (z + 1) * step);

                p00 = GetGroundAlignedGizmoPoint(p00, yOffset);
                p10 = GetGroundAlignedGizmoPoint(p10, yOffset);
                p01 = GetGroundAlignedGizmoPoint(p01, yOffset);
                p11 = GetGroundAlignedGizmoPoint(p11, yOffset);

                GL.Vertex(p00);
                GL.Vertex(p10);
                GL.Vertex(p11);

                GL.Vertex(p00);
                GL.Vertex(p11);
                GL.Vertex(p01);
            }
        }

        GL.End();
        GL.PopMatrix();
    }

    public void DrawGroundAlignedWireCircle(Vector3 center, float radius, Color color, int segments, float yOffset = 0f)
    {
        if (radius <= 0f)
        {
            return;
        }

        int safeSegments = Mathf.Max(8, segments > 0 ? segments : gizmoGroundSampleSegments);
        Gizmos.color = color;

        Vector3 previousPoint = Vector3.zero;
        Vector3 firstPoint = Vector3.zero;

        for (int i = 0; i <= safeSegments; i++)
        {
            float angle = Mathf.Deg2Rad * (360f / safeSegments) * i;
            Vector3 rawPoint = center + new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
            Vector3 point = GetGroundAlignedGizmoPoint(rawPoint, yOffset);

            if (i == 0)
            {
                firstPoint = point;
            }
            else
            {
                Gizmos.DrawLine(previousPoint, point);
            }

            previousPoint = point;
        }

        if (safeSegments > 0)
        {
            Gizmos.DrawLine(previousPoint, firstPoint);
        }
    }

    public void DrawGroundAlignedWireSquare(Vector3 center, float sideLength, Color color, int pointsPerSide, float yOffset = 0f)
    {
        if (sideLength <= 0f)
        {
            return;
        }

        int safePointsPerSide = Mathf.Max(1, pointsPerSide);
        float half = sideLength * 0.5f;
        Gizmos.color = color;

        Vector3 previousPoint = Vector3.zero;
        Vector3 firstPoint = Vector3.zero;
        bool hasPreviousPoint = false;

        for (int side = 0; side < 4; side++)
        {
            for (int i = 0; i <= safePointsPerSide; i++)
            {
                float t = i / (float)safePointsPerSide;
                Vector3 rawPoint;

                switch (side)
                {
                    case 0:
                        rawPoint = center + new Vector3(Mathf.Lerp(-half, half, t), 0f, half);
                        break;
                    case 1:
                        rawPoint = center + new Vector3(half, 0f, Mathf.Lerp(half, -half, t));
                        break;
                    case 2:
                        rawPoint = center + new Vector3(Mathf.Lerp(half, -half, t), 0f, -half);
                        break;
                    default:
                        rawPoint = center + new Vector3(-half, 0f, Mathf.Lerp(-half, half, t));
                        break;
                }

                Vector3 point = GetGroundAlignedGizmoPoint(rawPoint, yOffset);
                if (!hasPreviousPoint)
                {
                    firstPoint = point;
                    hasPreviousPoint = true;
                }
                else
                {
                    Gizmos.DrawLine(previousPoint, point);
                }

                previousPoint = point;
            }
        }

        if (hasPreviousPoint)
        {
            Gizmos.DrawLine(previousPoint, firstPoint);
        }
    }

    private static bool PrepareGizmoMaterial()
    {
        if (gizmoMaterial == null)
        {
            Shader shader = Shader.Find("Hidden/Internal-Colored");
            if (shader == null)
            {
                return false;
            }

            gizmoMaterial = new Material(shader);
            gizmoMaterial.hideFlags = HideFlags.HideAndDontSave;
            gizmoMaterial.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            gizmoMaterial.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            gizmoMaterial.SetInt("_Cull", (int)UnityEngine.Rendering.CullMode.Off);
            gizmoMaterial.SetInt("_ZWrite", 0);
        }

        gizmoMaterial.SetPass(0);
        return true;
    }

    public NavMeshAgent GetAgent()
    {
        return agent;
    }

    public CharacterController GetCharacterController()
    {
        return characterController;
    }

    public bool UsesNavMeshAgent()
    {
        return HasNavMeshAgent;
    }

    public bool UsesCharacterController()
    {
        return HasCharacterController && !HasNavMeshAgent;
    }

    public Transform GetPlayerTransform()
    {
        return playerTransform;
    }

    public IEnumerator WaitAndResume(float waitTime, float resumeSpeed, List<string> waitingAnimationNames)
    {
        isWaiting = true;
        SetMoveSpeed(0f);
        SetMovementPaused(true);

        if (animator != null)
        {
            animator.SetFloat(animatorParameter, 0f);

            if (waitingAnimationNames != null && waitingAnimationNames.Count > 0)
            {
                string selectedAnimation = waitingAnimationNames[UnityEngine.Random.Range(0, waitingAnimationNames.Count)];
                if (!string.IsNullOrEmpty(selectedAnimation))
                {
                    animator.Play(selectedAnimation);
                }
            }
        }

        yield return new WaitForSeconds(waitTime);

        SetMoveSpeed(resumeSpeed);
        SetMovementPaused(false);

        if (animator != null)
        {
            animator.SetFloat(animatorParameter, resumeSpeed);
        }

        isWaiting = false;
    }

    public IEnumerator WaitAndAnimate(float waitTime, string animationName, float speed, AIController controller)
    {
        isWaiting = true;
        SetMoveSpeed(0f);
        SetMovementPaused(true);

        if (animator != null)
        {
            animator.SetFloat(animatorParameter, 0f);

            if (!string.IsNullOrEmpty(animationName))
            {
                animator.Play(animationName);
                yield return new WaitForSeconds(waitTime);

                while (animator.GetCurrentAnimatorStateInfo(0).normalizedTime < 1f)
                {
                    yield return null;
                }
            }
            else
            {
                yield return new WaitForSeconds(waitTime);
            }
        }
        else
        {
            yield return new WaitForSeconds(waitTime);
        }

        SetMoveSpeed(speed);
        SetMovementPaused(false);

        if (animator != null)
        {
            if (speed > 1.5f)
            {
                animator.SetFloat(animatorParameter, 2f);
            }
            else if (speed > 0.1f)
            {
                animator.SetFloat(animatorParameter, 1f);
            }
            else
            {
                animator.SetFloat(animatorParameter, 0f);
            }
        }

        isWaiting = false;
        animationReset = false;
    }

    public bool IsWaiting()
    {
        return isWaiting;
    }

    public Animator GetAnimator()
    {
        return animator;
    }
}
