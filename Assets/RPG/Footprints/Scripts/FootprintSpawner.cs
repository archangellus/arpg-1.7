using UnityEngine;

public class FootprintSpawner : MonoBehaviour
{
    public enum FootSide
    {
        Left,
        Right
    }

    [System.Serializable]
    public class FootprintSetup
    {
        [Header("Foot Bone")]
        public Transform footBone;

        [Header("Footprint Prefab")]
        public GameObject footprintPrefab;

        [Header("Offset Alignment")]
        [Tooltip("X = right/left on surface, Y = forward/back on surface, Z = extra surface offset. Final height is still clamped safely.")]
        public Vector3 positionOffset = Vector3.zero;

        [Tooltip("Extra rotation after aligning the quad to the ground.")]
        public Vector3 rotationOffset = Vector3.zero;

        [Tooltip("Optional scale multiplier for this footprint.")]
        public Vector3 scaleMultiplier = Vector3.one;
    }

    [Header("Character")]
    [SerializeField] private Transform characterRoot;

    [Header("Feet")]
    [SerializeField] private FootprintSetup leftFoot;
    [SerializeField] private FootprintSetup rightFoot;

    [Header("Hierarchy")]
    [SerializeField] private Transform footprintParent;

    [Header("Ground Detection")]
    [SerializeField] private LayerMask groundLayers = ~0;

    [SerializeField] private float rayStartOffset = 0.35f;
    [SerializeField] private float rayDistance = 0.8f;
    [SerializeField] private float groundContactHeight = 0.08f;
    [SerializeField] private float requiredLiftHeight = 0.12f;

    [Header("Surface Placement")]
    [Tooltip("Maximum world-space height above the detected ground surface.")]
    [SerializeField] private float surfaceOffset = 0.003f;

    [Tooltip("Height above the wanted footprint position used for the final surface snap.")]
    [SerializeField] private float surfaceSnapStartHeight = 1.0f;

    [Tooltip("Distance used for the final surface snap.")]
    [SerializeField] private float surfaceSnapDistance = 2.0f;

    [Tooltip("Reject steep or downward-facing hits. 0.2 allows steep slopes, 0.7 allows gentler slopes.")]
    [SerializeField] private float minimumGroundNormalY = 0.2f;

    [Header("Spawn Rules")]
    [SerializeField] private bool automaticFootDetection = true;
    [SerializeField] private float minimumTimeBetweenSameFootprints = 0.18f;
    [SerializeField] private bool retryFailedSpawnsWhileGrounded = true;
    [SerializeField] private float spawnRetryInterval = 0.02f;
    [SerializeField] private int maxSpawnRetriesPerStep = 6;
    [SerializeField] private float footprintLifetime = 20f;

    [Header("Debug")]
    [SerializeField] private bool logSurfaceSnap = false;
    [SerializeField] private bool drawDebugRays = true;

    private readonly FootState leftFootState = new FootState();
    private readonly FootState rightFootState = new FootState();

    private readonly RaycastHit[] hitBuffer = new RaycastHit[24];

    private class FootState
    {
        public bool wasGrounded;
        public bool hasLifted = true;

        public bool pendingSpawn;
        public float nextRetryTime;
        public int retryCount;

        public float lastSpawnTime = -999f;

        public void ClearPendingSpawn()
        {
            pendingSpawn = false;
            retryCount = 0;
            nextRetryTime = 0f;
        }
    }

    private void Awake()
    {
        if (characterRoot == null)
            characterRoot = transform;

        surfaceOffset = Mathf.Max(0f, surfaceOffset);
        minimumGroundNormalY = Mathf.Clamp01(minimumGroundNormalY);
    }

    private void Update()
    {
        if (!automaticFootDetection)
            return;

        UpdateFoot(leftFoot, leftFootState);
        UpdateFoot(rightFoot, rightFootState);
    }

    private void UpdateFoot(FootprintSetup foot, FootState state)
    {
        if (foot == null || foot.footBone == null || foot.footprintPrefab == null)
            return;

        bool hasGroundHit = TryGetFootGroundHit(
            foot.footBone,
            out RaycastHit hit,
            out float footHeightFromGround
        );

        if (!hasGroundHit)
        {
            state.wasGrounded = false;
            state.hasLifted = true;
            state.ClearPendingSpawn();
            return;
        }

        bool isGrounded = footHeightFromGround <= groundContactHeight;
        bool isLifted = footHeightFromGround >= requiredLiftHeight;

        if (isLifted)
        {
            state.hasLifted = true;
            state.ClearPendingSpawn();
        }

        bool justTouchedGround = isGrounded && !state.wasGrounded;

        if (justTouchedGround && state.hasLifted)
        {
            bool spawned = TrySpawnFootprint(foot, state, hit);

            if (!spawned)
                QueueSpawnRetry(state);

            state.hasLifted = false;
        }

        if (isGrounded && state.pendingSpawn)
            RetryPendingSpawn(foot, state);

        state.wasGrounded = isGrounded;
    }

    private bool TryGetFootGroundHit(
        Transform footBone,
        out RaycastHit hit,
        out float footHeightFromGround)
    {
        Vector3 origin = footBone.position + Vector3.up * rayStartOffset;

        bool found = TryGetFilteredGroundHit(
            origin,
            Vector3.down,
            rayDistance,
            out hit
        );

        if (!found)
        {
            footHeightFromGround = Mathf.Infinity;
            return false;
        }

        footHeightFromGround = hit.distance - rayStartOffset;
        return true;
    }

    private void QueueSpawnRetry(FootState state)
    {
        if (!retryFailedSpawnsWhileGrounded)
            return;

        state.pendingSpawn = true;
        state.retryCount = 0;
        state.nextRetryTime = Time.time + spawnRetryInterval;
    }

    private void RetryPendingSpawn(FootprintSetup foot, FootState state)
    {
        if (!retryFailedSpawnsWhileGrounded)
        {
            state.ClearPendingSpawn();
            return;
        }

        if (Time.time < state.nextRetryTime)
            return;

        if (maxSpawnRetriesPerStep > 0 && state.retryCount >= maxSpawnRetriesPerStep)
        {
            state.ClearPendingSpawn();
            return;
        }

        state.retryCount++;
        state.nextRetryTime = Time.time + spawnRetryInterval;

        if (!TryGetFootGroundHit(foot.footBone, out RaycastHit hit, out float footHeightFromGround))
            return;

        if (footHeightFromGround > groundContactHeight)
            return;

        bool spawned = TrySpawnFootprint(foot, state, hit);

        if (spawned)
            state.ClearPendingSpawn();
    }

    private bool TrySpawnFootprint(
        FootprintSetup foot,
        FootState state,
        RaycastHit originalHit)
    {
        if (Time.time - state.lastSpawnTime < minimumTimeBetweenSameFootprints)
            return false;

        bool spawned = SpawnFootprint(foot, originalHit);

        if (spawned)
            state.lastSpawnTime = Time.time;

        return spawned;
    }

    private bool SpawnFootprint(FootprintSetup foot, RaycastHit originalHit)
    {
        if (!TryCalculateSurfacePlacement(
                foot,
                originalHit,
                out Vector3 finalPosition,
                out Quaternion finalRotation))
        {
            return false;
        }

        GameObject footprint = Instantiate(
            foot.footprintPrefab,
            finalPosition,
            finalRotation,
            footprintParent
        );

        if (footprint == null)
            return false;

        footprint.transform.localScale = Vector3.Scale(
            footprint.transform.localScale,
            foot.scaleMultiplier
        );

        if (footprintLifetime > 0f)
            Destroy(footprint, footprintLifetime);

        return true;
    }

private bool TryCalculateSurfacePlacement(
    FootprintSetup foot,
    RaycastHit originalHit,
    out Vector3 finalPosition,
    out Quaternion finalRotation)
{
    finalPosition = Vector3.zero;
    finalRotation = Quaternion.identity;

    Vector3 originalNormal = originalHit.normal;

    Vector3 characterForward = characterRoot != null
        ? characterRoot.forward
        : transform.forward;

    Vector3 forwardOnSurface = Vector3.ProjectOnPlane(characterForward, originalNormal);

    if (forwardOnSurface.sqrMagnitude < 0.001f)
        forwardOnSurface = Vector3.ProjectOnPlane(transform.forward, originalNormal);

    if (forwardOnSurface.sqrMagnitude < 0.001f)
        return false;

    forwardOnSurface.Normalize();

    Vector3 rightOnSurface = Vector3.Cross(originalNormal, forwardOnSurface).normalized;

    Vector3 wantedSurfacePoint =
        originalHit.point +
        rightOnSurface * foot.positionOffset.x +
        forwardOnSurface * foot.positionOffset.y;

    Vector3 snapOrigin = wantedSurfacePoint + Vector3.up * surfaceSnapStartHeight;

    if (drawDebugRays)
        Debug.DrawRay(snapOrigin, Vector3.down * surfaceSnapDistance, Color.yellow, 0.15f);

    bool snapped = TryGetFilteredGroundHit(
        snapOrigin,
        Vector3.down,
        surfaceSnapDistance,
        out RaycastHit snappedHit
    );

    if (!snapped)
        return false;

    Vector3 surfaceNormal = snappedHit.normal;

    Vector3 finalForwardOnSurface = Vector3.ProjectOnPlane(characterForward, surfaceNormal);

    if (finalForwardOnSurface.sqrMagnitude < 0.001f)
        finalForwardOnSurface = Vector3.ProjectOnPlane(transform.forward, surfaceNormal);

    if (finalForwardOnSurface.sqrMagnitude < 0.001f)
        return false;

    finalForwardOnSurface.Normalize();

    Quaternion baseRotation = Quaternion.LookRotation(surfaceNormal, finalForwardOnSurface);
    finalRotation = baseRotation * Quaternion.Euler(foot.rotationOffset);

    /*
     * Strict surface placement:
     *
     * The footprint uses the exact snapped ground point.
     * Only a tiny WORLD-Y offset is added to avoid z-fighting.
     *
     * This means:
     * - It will never spawn underneath the ground.
     * - It will never float higher than surfaceOffset.
     * - foot.positionOffset.z cannot push it below or too far above the ground.
     */
    float requestedOffset = surfaceOffset + foot.positionOffset.z;
    float safeVerticalOffset = Mathf.Clamp(requestedOffset, 0f, surfaceOffset);

    finalPosition = snappedHit.point + Vector3.up * safeVerticalOffset;

    if (logSurfaceSnap)
    {
        Debug.Log(
            $"Footprint snapped to {snappedHit.collider.name}. " +
            $"Surface Y: {snappedHit.point.y}, " +
            $"Final Y: {finalPosition.y}, " +
            $"Difference: {finalPosition.y - snappedHit.point.y}",
            snappedHit.collider
        );
    }

    return true;
}

    private bool TryGetFilteredGroundHit(
        Vector3 origin,
        Vector3 direction,
        float distance,
        out RaycastHit bestHit)
    {
        bestHit = default;

        int hitCount = Physics.RaycastNonAlloc(
            origin,
            direction,
            hitBuffer,
            distance,
            groundLayers,
            QueryTriggerInteraction.Ignore
        );

        if (hitCount <= 0)
            return false;

        bool foundValidHit = false;
        float closestDistance = Mathf.Infinity;

        for (int i = 0; i < hitCount; i++)
        {
            RaycastHit hit = hitBuffer[i];

            if (hit.collider == null)
                continue;

            if (!IsValidGroundHit(hit))
                continue;

            if (hit.distance < closestDistance)
            {
                closestDistance = hit.distance;
                bestHit = hit;
                foundValidHit = true;
            }
        }

        return foundValidHit;
    }

    private bool IsValidGroundHit(RaycastHit hit)
    {
        if (hit.collider == null)
            return false;

        if (hit.normal.y < minimumGroundNormalY)
            return false;

        Transform hitTransform = hit.collider.transform;

        if (characterRoot != null && hitTransform.IsChildOf(characterRoot))
            return false;

        if (footprintParent != null && hitTransform.IsChildOf(footprintParent))
            return false;

        return true;
    }

    public void AnimationEvent_LeftFootstep()
    {
        SpawnFromAnimationEvent(FootSide.Left);
    }

    public void AnimationEvent_RightFootstep()
    {
        SpawnFromAnimationEvent(FootSide.Right);
    }

    public void SpawnFromAnimationEvent(FootSide footSide)
    {
        FootprintSetup foot = footSide == FootSide.Left ? leftFoot : rightFoot;
        FootState state = footSide == FootSide.Left ? leftFootState : rightFootState;

        if (foot == null || foot.footBone == null || foot.footprintPrefab == null)
            return;

        bool hasGroundHit = TryGetFootGroundHit(
            foot.footBone,
            out RaycastHit hit,
            out float footHeightFromGround
        );

        if (!hasGroundHit || footHeightFromGround > groundContactHeight)
        {
            QueueSpawnRetry(state);
            return;
        }

        bool spawned = TrySpawnFootprint(foot, state, hit);

        if (!spawned)
            QueueSpawnRetry(state);
        else
            state.ClearPendingSpawn();
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        DrawFootRay(leftFoot, Color.blue);
        DrawFootRay(rightFoot, Color.red);
    }

    private void DrawFootRay(FootprintSetup foot, Color color)
    {
        if (foot == null || foot.footBone == null)
            return;

        Gizmos.color = color;

        Vector3 origin = foot.footBone.position + Vector3.up * rayStartOffset;
        Vector3 end = origin + Vector3.down * rayDistance;

        Gizmos.DrawLine(origin, end);
        Gizmos.DrawSphere(end, 0.025f);
    }
#endif
}