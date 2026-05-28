using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

#if UNITY_6000_0_OR_NEWER
using SurfacePhysicsMaterial = UnityEngine.PhysicsMaterial;
#else
using SurfacePhysicsMaterial = UnityEngine.PhysicMaterial;
#endif

/// <summary>
/// Spawns quad-based footprint prefabs from foot/leg spherecasts.
/// Designed for Unity 6 and compatible with Terrain texture layers, renderer materials/textures,
/// physics materials, layers, and tags.
///
/// Default Unity Quad note:
/// A Unity Quad's local +Z is its surface normal and local +Y is usually the texture's vertical/toe direction.
/// This script aligns local +Z to the ground normal and local +Y to the character/root forward projected onto the ground.
/// If your prefab root has an X rotation offset, enable Use Prefab Rotation As Quad Offset or set Quad Local Euler Offset.
/// </summary>
[DisallowMultipleComponent]
public sealed class FootprintSpherecastSurfaceQuads : MonoBehaviour
{
    [Serializable]
    public class Leg
    {
        [Header("Leg")]
        public string legName = "Foot";
        public Transform bone;

        [Tooltip("Used if the matched surface group has no per-leg or generic footprint prefabs.")]
        public List<GameObject> fallbackRandomFootprintQuads = new List<GameObject>();

        [Header("Spherecast")]
        public Vector3 castLocalOffset = new Vector3(0f, 0.12f, 0f);

        [Min(0.001f)]
        public float sphereRadius = 0.07f;

        [Min(0.001f)]
        public float castDistance = 0.35f;

        [Header("Step Triggering")]
        public bool leaveOnGroundContact = true;
        public bool leaveWhileGroundedByDistance = true;

        [Min(0f)]
        public float minStepInterval = 0.12f;

        [Min(0f)]
        public float minStepDistance = 0.25f;

        [Header("Placement")]
        [Tooltip("Yaw around the surface normal. Use 180 if this leg's footprint prefab points backward.")]
        public float yawOffsetDegrees;

        [Tooltip("Extra per-leg position offset in the final footprint local space.")]
        public Vector3 localPositionOffset;

        [NonSerialized] internal bool initialized;
        [NonSerialized] internal bool wasGrounded;
        [NonSerialized] internal bool hasLastStepPoint;
        [NonSerialized] internal Vector3 lastStepPoint;
        [NonSerialized] internal float nextAllowedStepTime;
        [NonSerialized] internal GameObject lastFootprintPrefab;

        [NonSerialized] internal bool debugHasHit;
        [NonSerialized] internal Vector3 debugHitPoint;
    }

    [Serializable]
    public class LegFootprintList
    {
        [Tooltip("Optional. If >= 0, this list only matches that leg index in the Legs list.")]
        public int legIndex = -1;

        [Tooltip("Optional. If Leg Index is -1, this matches the Leg Name, case-insensitive.")]
        public string legName = "";

        [Tooltip("Random Quad prefab list for this specific leg on this surface.")]
        public List<GameObject> randomFootprintQuads = new List<GameObject>();

        public bool Matches(Leg leg, int index)
        {
            if (legIndex >= 0)
                return legIndex == index;

            if (leg == null || string.IsNullOrEmpty(legName))
                return false;

            return string.Equals(leg.legName, legName, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Serializable]
    public class SurfaceFootprintGroup
    {
        [Header("Group")]
        public string groupName = "Surface";

        [Tooltip("Generic random Quad prefabs for this surface. Used if no Per Leg Footprint list matches.")]
        public List<GameObject> randomFootprintQuads = new List<GameObject>();

        [Tooltip("Optional per-leg random Quad prefab lists for this surface.")]
        public List<LegFootprintList> perLegFootprints = new List<LegFootprintList>();

        [Header("Match By Layer")]
        public bool matchLayerMask;
        public LayerMask layers;

        [Header("Match By Collider Physics Material")]
        public List<SurfacePhysicsMaterial> physicsMaterials = new List<SurfacePhysicsMaterial>();

        [Header("Match By Renderer Material")]
        public List<Material> rendererMaterials = new List<Material>();
        public List<string> rendererMaterialNameContains = new List<string>();

        [Header("Match By Texture Name")]
        public List<string> textureNameContains = new List<string>();

        [Header("Match By Terrain Layer")]
        public List<TerrainLayer> terrainLayers = new List<TerrainLayer>();
        public List<string> terrainLayerNameContains = new List<string>();

        [Header("Match By Tag")]
        public List<string> tags = new List<string>();

        [Header("Spawn Randomization")]
        public Vector2 uniformScaleMultiplierRange = new Vector2(1f, 1f);
        public Vector2 randomYawDegrees = new Vector2(-2.5f, 2.5f);

        [Tooltip("If >= 0, overrides the global Surface Offset for this surface group.")]
        public float surfaceOffsetOverride = -1f;

        public bool Matches(SurfaceInfo info)
        {
            if (info.collider == null)
                return false;

            if (matchLayerMask && ((layers.value & (1 << info.layer)) != 0))
                return true;

            if (physicsMaterials != null && info.physicsMaterial != null)
            {
                for (int i = 0; i < physicsMaterials.Count; i++)
                {
                    if (physicsMaterials[i] == info.physicsMaterial)
                        return true;
                }
            }

            if (tags != null)
            {
                for (int i = 0; i < tags.Count; i++)
                {
                    string wantedTag = tags[i];
                    if (!string.IsNullOrEmpty(wantedTag) && string.Equals(info.tag, wantedTag, StringComparison.Ordinal))
                        return true;
                }
            }

            if (terrainLayers != null && info.terrainLayer != null)
            {
                for (int i = 0; i < terrainLayers.Count; i++)
                {
                    if (terrainLayers[i] == info.terrainLayer)
                        return true;
                }
            }

            if (NameContains(info.terrainLayerName, terrainLayerNameContains))
                return true;

            if (info.terrainDiffuseTexture != null && NameContains(info.terrainDiffuseTexture.name, textureNameContains))
                return true;

            if (info.rendererMaterials != null)
            {
                for (int i = 0; i < info.rendererMaterials.Length; i++)
                {
                    Material material = info.rendererMaterials[i];
                    if (material == null)
                        continue;

                    if (rendererMaterials != null)
                    {
                        for (int j = 0; j < rendererMaterials.Count; j++)
                        {
                            if (rendererMaterials[j] == material)
                                return true;
                        }
                    }

                    if (NameContains(material.name, rendererMaterialNameContains))
                        return true;

                    Texture mainTexture = material.mainTexture;
                    if (mainTexture != null && NameContains(mainTexture.name, textureNameContains))
                        return true;
                }
            }

            return false;
        }

        public List<GameObject> GetPerLegList(Leg leg, int legIndex)
        {
            if (perLegFootprints == null)
                return null;

            for (int i = 0; i < perLegFootprints.Count; i++)
            {
                LegFootprintList list = perLegFootprints[i];
                if (list != null && list.Matches(leg, legIndex) && HasAnyPrefab(list.randomFootprintQuads))
                    return list.randomFootprintQuads;
            }

            return null;
        }

        public bool HasAnyPrefabFor(Leg leg, int legIndex)
        {
            if (HasAnyPrefab(GetPerLegList(leg, legIndex)))
                return true;

            return HasAnyPrefab(randomFootprintQuads);
        }

        private static bool NameContains(string source, List<string> fragments)
        {
            if (string.IsNullOrEmpty(source) || fragments == null)
                return false;

            for (int i = 0; i < fragments.Count; i++)
            {
                string fragment = fragments[i];
                if (string.IsNullOrEmpty(fragment))
                    continue;

                if (source.IndexOf(fragment, StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;
            }

            return false;
        }
    }

    public struct SurfaceInfo
    {
        public Collider collider;
        public int layer;
        public string tag;
        public SurfacePhysicsMaterial physicsMaterial;

        public Renderer renderer;
        public Material[] rendererMaterials;

        public Terrain terrain;
        public TerrainLayer terrainLayer;
        public string terrainLayerName;
        public Texture terrainDiffuseTexture;
    }

    private sealed class ActiveFootprint
    {
        public GameObject gameObject;
        public Coroutine coroutine;
    }

    private struct FadeMaterial
    {
        public Material material;
        public string colorProperty;
        public float startAlpha;
    }

    [Header("Legs")]
    public List<Leg> legs = new List<Leg>();

    [Header("Surface / Texture Type Groups")]
    public List<SurfaceFootprintGroup> textureTypeGroups = new List<SurfaceFootprintGroup>();

    [Header("Fallback")]
    public SurfaceFootprintGroup fallbackGroup = new SurfaceFootprintGroup
    {
        groupName = "Default"
    };

    [Header("Global Cast Settings")]
    public LayerMask groundMask = ~0;
    public QueryTriggerInteraction triggerInteraction = QueryTriggerInteraction.Ignore;

    [Tooltip("Usually Vector3.down.")]
    public Vector3 worldCastDirection = Vector3.down;

    [Tooltip("Ignore colliders that are children of this character root.")]
    public bool ignoreOwnColliders = true;

    [Header("Automatic Step Detection")]
    public bool automaticSteps = true;

    [Tooltip("Prevents distance-based footprints while the character is standing still.")]
    [Min(0f)]
    public float minimumRootSpeed = 0.05f;

    public bool useUnscaledTime;

    [Header("Quad Placement")]
    [Tooltip("Optional parent for spawned footprints. If empty, the script creates one at runtime.")]
    public Transform footprintParent;

    public bool createParentIfMissing = true;

    [Tooltip("Small offset along the surface normal to prevent z-fighting.")]
    [Min(0f)]
    public float surfaceOffset = 0.01f;

    [Tooltip("Keeps the prefab root rotation as an extra offset. Useful if your Quad prefab root has X = 90 or another required setup rotation.")]
    public bool usePrefabRotationAsQuadOffset = true;

    [Tooltip("Extra local rotation offset applied after surface alignment and prefab rotation offset. Use X = 90 only if your Quad artwork requires it.")]
    public Vector3 quadLocalEulerOffset = Vector3.zero;

    [Tooltip("If enabled, footprint toes follow this character's forward direction projected onto the hit surface. If disabled, leg bone forward is preferred.")]
    public bool alignWithCharacterForward = true;

    [Tooltip("Avoid picking the same footprint prefab twice in a row for the same leg when possible.")]
    public bool avoidImmediatePrefabRepeat = true;

    [Header("Normal Correction")]
    [Tooltip("Recommended. SphereCast hit normals can be contact normals, so this casts a short ray down to get a cleaner placement normal.")]
    public bool useRaycastForSurfaceNormal = true;

    [Min(0.001f)]
    public float normalRaycastStartOffset = 0.25f;

    [Min(0.001f)]
    public float normalRaycastDistance = 0.75f;

    [Header("Foot Hit Refinement")]
    [Tooltip("Recommended. Uses a raycast from the foot bone to replace invalid SphereCast points, including the Unity edge case where overlap hits can report point (0,0,0). This keeps the footprint directly under the specific foot bone.")]
    public bool refineFootHitWithRaycast = true;

    [Min(0f)]
    [Tooltip("How far above the spherecast origin the corrective foot ray starts, opposite World Cast Direction.")]
    public float footRaycastStartOffset = 0.25f;

    [Min(0f)]
    [Tooltip("Extra ray length added beyond Cast Distance and Sphere Radius for the corrective foot ray.")]
    public float footRaycastExtraDistance = 0.25f;

    [Tooltip("Recommended if your Quad prefab root/pivot is not centered on the visible footprint. After spawning, the combined renderer bounds center is moved onto the hit point.")]
    public bool centerRendererBoundsOnHitPoint = true;

    [Header("Lifetime")]
    [Min(0f)]
    public float footprintLifetime = 12f;

    public bool fadeBeforeDestroy = true;

    [Min(0f)]
    public float fadeDuration = 1f;

    [Tooltip("0 = unlimited. Use a cap to avoid filling the scene with old footprints.")]
    [Min(0)]
    public int maxActiveFootprints = 80;

    [Header("Debug")]
    public bool drawDebugGizmos = true;

    private readonly RaycastHit[] hitBuffer = new RaycastHit[16];
    private readonly RaycastHit[] normalHitBuffer = new RaycastHit[8];
    private readonly RaycastHit[] footRayHitBuffer = new RaycastHit[8];
    private readonly List<ActiveFootprint> activeFootprints = new List<ActiveFootprint>();
    private Vector3 lastRootPosition;

    private void Awake()
    {
        lastRootPosition = transform.position;
        EnsureFootprintParent();
    }

    private void LateUpdate()
    {
        if (!automaticSteps)
            return;

        float deltaTime = useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
        float rootSpeed = 0f;
        if (deltaTime > 0f)
            rootSpeed = (transform.position - lastRootPosition).magnitude / deltaTime;

        float now = CurrentTime;

        for (int i = 0; i < legs.Count; i++)
            UpdateLeg(legs[i], i, now, rootSpeed);

        lastRootPosition = transform.position;
    }

    /// <summary>
    /// Animation Event entry point. This name intentionally matches common footstep events.
    /// Pass the leg index: 0 = first leg, 1 = second leg, etc.
    /// </summary>
    public void Footstep(int legIndex)
    {
        LeaveFootprint(legIndex);
    }

    /// <summary>
    /// Animation Event entry point with a more explicit name.
    /// Pass the leg index: 0 = first leg, 1 = second leg, etc.
    /// </summary>
    public void LeaveFootprint(int legIndex)
    {
        if (legIndex < 0 || legIndex >= legs.Count)
            return;

        Leg leg = legs[legIndex];
        if (leg == null || leg.bone == null)
            return;

        if (TryCastLeg(leg, out RaycastHit hit))
        {
            SpawnFootprint(leg, legIndex, hit);
            leg.wasGrounded = true;
            leg.initialized = true;
            leg.lastStepPoint = hit.point;
            leg.hasLastStepPoint = true;
            leg.nextAllowedStepTime = CurrentTime + leg.minStepInterval;
        }
    }

    private void UpdateLeg(Leg leg, int legIndex, float now, float rootSpeed)
    {
        if (leg == null || leg.bone == null)
            return;

        bool grounded = TryCastLeg(leg, out RaycastHit hit);

        leg.debugHasHit = grounded;
        if (grounded)
            leg.debugHitPoint = hit.point;

        if (!leg.initialized)
        {
            leg.initialized = true;
            leg.wasGrounded = grounded;

            if (grounded)
            {
                leg.lastStepPoint = hit.point;
                leg.hasLastStepPoint = true;
            }

            return;
        }

        bool canSpawn = now >= leg.nextAllowedStepTime;
        bool rootMoving = rootSpeed >= minimumRootSpeed;

        bool contactStep =
            leg.leaveOnGroundContact &&
            grounded &&
            !leg.wasGrounded;

        bool distanceStep =
            leg.leaveWhileGroundedByDistance &&
            grounded &&
            rootMoving &&
            leg.hasLastStepPoint &&
            Vector3.Distance(hit.point, leg.lastStepPoint) >= leg.minStepDistance;

        if (canSpawn && (contactStep || distanceStep))
        {
            SpawnFootprint(leg, legIndex, hit);
            leg.lastStepPoint = hit.point;
            leg.hasLastStepPoint = true;
            leg.nextAllowedStepTime = CurrentTime + leg.minStepInterval;
        }

        leg.wasGrounded = grounded;
    }

    private bool TryCastLeg(Leg leg, out RaycastHit bestHit)
    {
        bestHit = default;

        if (leg == null || leg.bone == null)
            return false;

        Vector3 direction = GetCastDirection();
        Vector3 origin = leg.bone.TransformPoint(leg.castLocalOffset);

        bool foundSphereHit = TrySphereCastLeg(leg, origin, direction, out RaycastHit sphereHit);
        bool sphereHitUsable = foundSphereHit && IsUsablePlacementHit(sphereHit, origin, leg);

        if (refineFootHitWithRaycast && TryRaycastUnderLeg(leg, origin, direction, out RaycastHit rayHit))
        {
            // Prefer the raycast because it is directly under this foot bone. This also fixes the
            // SphereCast overlap case where Unity can report a zero hit point near world origin.
            bestHit = rayHit;
            return true;
        }

        if (sphereHitUsable)
        {
            bestHit = sphereHit;
            return true;
        }

        return false;
    }

    private bool TrySphereCastLeg(Leg leg, Vector3 origin, Vector3 direction, out RaycastHit bestHit)
    {
        bestHit = default;

        int hitCount = Physics.SphereCastNonAlloc(
            origin,
            leg.sphereRadius,
            direction,
            hitBuffer,
            leg.castDistance,
            groundMask,
            triggerInteraction
        );

        bool found = false;
        float closestDistance = float.PositiveInfinity;

        for (int i = 0; i < hitCount; i++)
        {
            RaycastHit hit = hitBuffer[i];
            if (hit.collider == null)
                continue;

            if (IsOwnCollider(hit.collider))
                continue;

            if (hit.distance < closestDistance)
            {
                closestDistance = hit.distance;
                bestHit = hit;
                found = true;
            }
        }

        return found;
    }

    private bool TryRaycastUnderLeg(Leg leg, Vector3 sphereOrigin, Vector3 direction, out RaycastHit bestHit)
    {
        bestHit = default;

        // If direction is Vector3.down, this starts slightly above the foot sphere origin.
        Vector3 rayOrigin = sphereOrigin - direction * footRaycastStartOffset;
        float rayDistance = footRaycastStartOffset + leg.castDistance + leg.sphereRadius + footRaycastExtraDistance;

        int hitCount = Physics.RaycastNonAlloc(
            rayOrigin,
            direction,
            footRayHitBuffer,
            rayDistance,
            groundMask,
            triggerInteraction
        );

        bool found = false;
        float closestDistance = float.PositiveInfinity;

        for (int i = 0; i < hitCount; i++)
        {
            RaycastHit hit = footRayHitBuffer[i];
            if (hit.collider == null)
                continue;

            if (IsOwnCollider(hit.collider))
                continue;

            if (!IsUsablePlacementHit(hit, rayOrigin, leg))
                continue;

            if (hit.distance < closestDistance)
            {
                closestDistance = hit.distance;
                bestHit = hit;
                found = true;
            }
        }

        return found;
    }

    private bool IsUsablePlacementHit(RaycastHit hit, Vector3 queryOrigin, Leg leg)
    {
        if (hit.collider == null)
            return false;

        // Unity casts can report a zero point for casts that start overlapped. If the foot is not
        // actually near world origin, that point would spawn the footprint far away from the player.
        float reasonableDistance = Mathf.Max(1f, leg.castDistance + leg.sphereRadius + footRaycastStartOffset + footRaycastExtraDistance + 0.5f);
        if (hit.point == Vector3.zero && Vector3.Distance(queryOrigin, Vector3.zero) > reasonableDistance)
            return false;

        // Reject any other suspicious point far outside the local query length.
        if (Vector3.Distance(queryOrigin, hit.point) > reasonableDistance * 2f)
            return false;

        return true;
    }

    private bool IsOwnCollider(Collider hitCollider)
    {
        return ignoreOwnColliders && hitCollider != null && hitCollider.transform.IsChildOf(transform);
    }

    private void SpawnFootprint(Leg leg, int legIndex, RaycastHit hit)
    {
        SurfaceInfo surfaceInfo = BuildSurfaceInfo(hit);
        SurfaceFootprintGroup group = FindSurfaceGroup(surfaceInfo, leg, legIndex);
        GameObject prefab = PickFootprintPrefab(group, leg, legIndex, leg.lastFootprintPrefab);

        if (prefab == null)
            return;

        EnsureFootprintParent();

        Vector3 normal = GetPlacementNormal(hit);
        Vector3 footprintForward = GetFootprintForward(leg, normal);

        // Local +Z of a Unity Quad is the quad normal. Local +Y is used as the toe/forward direction.
        Quaternion surfaceRotation = Quaternion.LookRotation(normal, footprintForward);

        Vector2 yawRange = group != null ? group.randomYawDegrees : Vector2.zero;
        float yaw = leg.yawOffsetDegrees + RandomRange(yawRange);
        Quaternion yawRotation = Quaternion.AngleAxis(yaw, Vector3.forward);
        Quaternion prefabRotationOffset = usePrefabRotationAsQuadOffset && prefab.transform != null
            ? prefab.transform.localRotation
            : Quaternion.identity;
        Quaternion extraOffset = Quaternion.Euler(quadLocalEulerOffset);

        Quaternion finalRotation = surfaceRotation * yawRotation * prefabRotationOffset * extraOffset;

        float offset = surfaceOffset;
        if (group != null && group.surfaceOffsetOverride >= 0f)
            offset = group.surfaceOffsetOverride;

        Vector3 position = hit.point + normal * offset;
        position += finalRotation * leg.localPositionOffset;

        // Instantiate unparented, set the world transform explicitly, then parent with
        // worldPositionStays = true. This avoids any parent transform making the footprint
        // appear offset from the actual foot hit point.
        GameObject spawned = Instantiate(prefab);
        spawned.transform.SetPositionAndRotation(position, finalRotation);

        if (footprintParent != null)
            spawned.transform.SetParent(footprintParent, true);

        Vector2 scaleRange = group != null ? group.uniformScaleMultiplierRange : Vector2.one;
        float scaleMultiplier = RandomRange(scaleRange);
        spawned.transform.localScale = spawned.transform.localScale * scaleMultiplier;

        if (centerRendererBoundsOnHitPoint)
            CenterRendererBoundsOnPoint(spawned, hit.point + normal * offset);

        leg.lastFootprintPrefab = prefab;
        TrackFootprint(spawned);
    }

    private SurfaceFootprintGroup FindSurfaceGroup(SurfaceInfo info, Leg leg, int legIndex)
    {
        if (textureTypeGroups != null)
        {
            for (int i = 0; i < textureTypeGroups.Count; i++)
            {
                SurfaceFootprintGroup group = textureTypeGroups[i];
                if (group == null)
                    continue;

                if (group.Matches(info) && group.HasAnyPrefabFor(leg, legIndex))
                    return group;
            }
        }

        return fallbackGroup;
    }

    private GameObject PickFootprintPrefab(SurfaceFootprintGroup group, Leg leg, int legIndex, GameObject lastPrefab)
    {
        if (group != null)
        {
            GameObject perLeg = PickRandomPrefab(group.GetPerLegList(leg, legIndex), lastPrefab);
            if (perLeg != null)
                return perLeg;

            GameObject generic = PickRandomPrefab(group.randomFootprintQuads, lastPrefab);
            if (generic != null)
                return generic;
        }

        if (leg != null)
        {
            GameObject legFallback = PickRandomPrefab(leg.fallbackRandomFootprintQuads, lastPrefab);
            if (legFallback != null)
                return legFallback;
        }

        if (fallbackGroup != null)
            return PickRandomPrefab(fallbackGroup.randomFootprintQuads, lastPrefab);

        return null;
    }

    private GameObject PickRandomPrefab(List<GameObject> prefabs, GameObject lastPrefab)
    {
        if (!HasAnyPrefab(prefabs))
            return null;

        if (prefabs.Count == 1)
            return prefabs[0];

        for (int attempt = 0; attempt < 12; attempt++)
        {
            GameObject candidate = prefabs[UnityEngine.Random.Range(0, prefabs.Count)];
            if (candidate == null)
                continue;

            if (!avoidImmediatePrefabRepeat || candidate != lastPrefab)
                return candidate;
        }

        for (int i = 0; i < prefabs.Count; i++)
        {
            if (prefabs[i] != null && prefabs[i] != lastPrefab)
                return prefabs[i];
        }

        for (int i = 0; i < prefabs.Count; i++)
        {
            if (prefabs[i] != null)
                return prefabs[i];
        }

        return null;
    }

    private Vector3 GetPlacementNormal(RaycastHit sphereHit)
    {
        Vector3 fallbackNormal = sphereHit.normal.sqrMagnitude > 0.0001f ? sphereHit.normal.normalized : Vector3.up;

        if (!useRaycastForSurfaceNormal)
            return fallbackNormal;

        Vector3 direction = GetCastDirection();
        Vector3 origin = sphereHit.point - direction * normalRaycastStartOffset;

        int hitCount = Physics.RaycastNonAlloc(
            origin,
            direction,
            normalHitBuffer,
            normalRaycastDistance,
            groundMask,
            triggerInteraction
        );

        bool found = false;
        float closestDistance = float.PositiveInfinity;
        RaycastHit bestHit = default;

        for (int i = 0; i < hitCount; i++)
        {
            RaycastHit hit = normalHitBuffer[i];
            if (hit.collider == null)
                continue;

            if (IsOwnCollider(hit.collider))
                continue;

            if (hit.distance < closestDistance)
            {
                closestDistance = hit.distance;
                bestHit = hit;
                found = true;
            }
        }

        if (found && bestHit.normal.sqrMagnitude > 0.0001f)
            return bestHit.normal.normalized;

        return fallbackNormal;
    }

    private Vector3 GetFootprintForward(Leg leg, Vector3 normal)
    {
        Vector3 sourceForward = transform.forward;

        if (!alignWithCharacterForward && leg != null && leg.bone != null)
            sourceForward = leg.bone.forward;

        Vector3 forward = Vector3.ProjectOnPlane(sourceForward, normal);

        if (forward.sqrMagnitude < 0.0001f && leg != null && leg.bone != null)
            forward = Vector3.ProjectOnPlane(leg.bone.forward, normal);

        if (forward.sqrMagnitude < 0.0001f)
            forward = Vector3.ProjectOnPlane(transform.right, normal);

        if (forward.sqrMagnitude < 0.0001f)
        {
            forward = Vector3.Cross(normal, Vector3.right);
            if (forward.sqrMagnitude < 0.0001f)
                forward = Vector3.Cross(normal, Vector3.forward);
        }

        return forward.normalized;
    }

    private SurfaceInfo BuildSurfaceInfo(RaycastHit hit)
    {
        SurfaceInfo info = new SurfaceInfo
        {
            collider = hit.collider
        };

        if (hit.collider == null)
            return info;

        GameObject hitObject = hit.collider.gameObject;

        info.layer = hitObject.layer;
        info.tag = hitObject.tag;
        info.physicsMaterial = hit.collider.sharedMaterial;

        info.renderer = hit.collider.GetComponent<Renderer>();
        if (info.renderer == null)
            info.renderer = hit.collider.GetComponentInParent<Renderer>();

        if (info.renderer != null)
            info.rendererMaterials = info.renderer.sharedMaterials;

        TryFillTerrainInfo(hit, ref info);

        return info;
    }

    private void TryFillTerrainInfo(RaycastHit hit, ref SurfaceInfo info)
    {
        TerrainCollider terrainCollider = hit.collider as TerrainCollider;
        if (terrainCollider == null)
            return;

        Terrain terrain = terrainCollider.GetComponent<Terrain>();
        if (terrain == null || terrain.terrainData == null)
            return;

        TerrainData terrainData = terrain.terrainData;
        TerrainLayer[] layers = terrainData.terrainLayers;

        if (layers == null || layers.Length == 0)
            return;

        Vector3 terrainLocalPos = hit.point - terrain.transform.position;
        Vector3 terrainSize = terrainData.size;

        if (terrainSize.x <= 0f || terrainSize.z <= 0f)
            return;

        float normalizedX = Mathf.Clamp01(terrainLocalPos.x / terrainSize.x);
        float normalizedZ = Mathf.Clamp01(terrainLocalPos.z / terrainSize.z);

        int alphaX = Mathf.Clamp(
            Mathf.FloorToInt(normalizedX * terrainData.alphamapWidth),
            0,
            terrainData.alphamapWidth - 1
        );

        int alphaY = Mathf.Clamp(
            Mathf.FloorToInt(normalizedZ * terrainData.alphamapHeight),
            0,
            terrainData.alphamapHeight - 1
        );

        float[,,] alphamap = terrainData.GetAlphamaps(alphaX, alphaY, 1, 1);

        int bestLayerIndex = 0;
        float bestWeight = -1f;
        int alphaLayers = alphamap.GetLength(2);

        for (int i = 0; i < alphaLayers; i++)
        {
            float weight = alphamap[0, 0, i];
            if (weight > bestWeight)
            {
                bestWeight = weight;
                bestLayerIndex = i;
            }
        }

        if (bestLayerIndex < 0 || bestLayerIndex >= layers.Length)
            return;

        TerrainLayer dominantLayer = layers[bestLayerIndex];

        info.terrain = terrain;
        info.terrainLayer = dominantLayer;

        if (dominantLayer != null)
        {
            info.terrainLayerName = dominantLayer.name;
            info.terrainDiffuseTexture = dominantLayer.diffuseTexture;
        }
    }

    private static void CenterRendererBoundsOnPoint(GameObject spawned, Vector3 targetPoint)
    {
        if (spawned == null)
            return;

        Renderer[] renderers = spawned.GetComponentsInChildren<Renderer>(true);
        if (renderers == null || renderers.Length == 0)
            return;

        bool hasBounds = false;
        Bounds combined = default;

        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer rend = renderers[i];
            if (rend == null)
                continue;

            if (!hasBounds)
            {
                combined = rend.bounds;
                hasBounds = true;
            }
            else
            {
                combined.Encapsulate(rend.bounds);
            }
        }

        if (!hasBounds)
            return;

        Vector3 delta = targetPoint - combined.center;
        spawned.transform.position += delta;
    }

    private void TrackFootprint(GameObject spawned)
    {
        if (spawned == null)
            return;

        CleanupMissingFootprints();

        if (maxActiveFootprints > 0)
        {
            while (activeFootprints.Count >= maxActiveFootprints && activeFootprints.Count > 0)
            {
                ActiveFootprint oldest = activeFootprints[0];
                activeFootprints.RemoveAt(0);

                if (oldest != null && oldest.coroutine != null)
                    StopCoroutine(oldest.coroutine);

                if (oldest != null && oldest.gameObject != null)
                    Destroy(oldest.gameObject);
            }
        }

        ActiveFootprint active = new ActiveFootprint
        {
            gameObject = spawned
        };

        active.coroutine = StartCoroutine(FootprintLifetimeRoutine(spawned));
        activeFootprints.Add(active);
    }

    private IEnumerator FootprintLifetimeRoutine(GameObject spawned)
    {
        if (spawned == null)
            yield break;

        float lifetime = Mathf.Max(0f, footprintLifetime);
        float fade = fadeBeforeDestroy ? Mathf.Min(Mathf.Max(0f, fadeDuration), lifetime) : 0f;
        float solidTime = Mathf.Max(0f, lifetime - fade);

        float elapsed = 0f;
        while (elapsed < solidTime && spawned != null)
        {
            elapsed += useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
            yield return null;
        }

        if (fade > 0f && spawned != null)
        {
            FadeMaterial[] fadeMaterials = CollectFadeMaterials(spawned);
            elapsed = 0f;

            while (elapsed < fade && spawned != null)
            {
                elapsed += useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
                float alpha01 = 1f - Mathf.Clamp01(elapsed / fade);
                ApplyAlpha(fadeMaterials, alpha01);
                yield return null;
            }
        }

        if (spawned != null)
            Destroy(spawned);

        RemoveActiveFootprint(spawned);
    }

    private FadeMaterial[] CollectFadeMaterials(GameObject spawned)
    {
        if (spawned == null)
            return Array.Empty<FadeMaterial>();

        Renderer[] renderers = spawned.GetComponentsInChildren<Renderer>(true);
        List<FadeMaterial> results = new List<FadeMaterial>();

        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer rend = renderers[i];
            if (rend == null)
                continue;

            Material[] materials = rend.materials;
            for (int j = 0; j < materials.Length; j++)
            {
                Material material = materials[j];
                if (material == null)
                    continue;

                string property = null;
                if (material.HasProperty("_BaseColor"))
                    property = "_BaseColor";
                else if (material.HasProperty("_Color"))
                    property = "_Color";

                if (string.IsNullOrEmpty(property))
                    continue;

                Color color = material.GetColor(property);
                results.Add(new FadeMaterial
                {
                    material = material,
                    colorProperty = property,
                    startAlpha = color.a
                });
            }
        }

        return results.ToArray();
    }

    private static void ApplyAlpha(FadeMaterial[] fadeMaterials, float alpha01)
    {
        if (fadeMaterials == null)
            return;

        for (int i = 0; i < fadeMaterials.Length; i++)
        {
            FadeMaterial fadeMaterial = fadeMaterials[i];
            if (fadeMaterial.material == null || string.IsNullOrEmpty(fadeMaterial.colorProperty))
                continue;

            Color color = fadeMaterial.material.GetColor(fadeMaterial.colorProperty);
            color.a = fadeMaterial.startAlpha * alpha01;
            fadeMaterial.material.SetColor(fadeMaterial.colorProperty, color);
        }
    }

    private void RemoveActiveFootprint(GameObject spawned)
    {
        for (int i = activeFootprints.Count - 1; i >= 0; i--)
        {
            ActiveFootprint active = activeFootprints[i];
            if (active == null || active.gameObject == null || active.gameObject == spawned)
                activeFootprints.RemoveAt(i);
        }
    }

    private void CleanupMissingFootprints()
    {
        for (int i = activeFootprints.Count - 1; i >= 0; i--)
        {
            if (activeFootprints[i] == null || activeFootprints[i].gameObject == null)
                activeFootprints.RemoveAt(i);
        }
    }

    private void EnsureFootprintParent()
    {
        if (footprintParent != null || !createParentIfMissing)
            return;

        GameObject parentObject = new GameObject(name + " Footprints");
        footprintParent = parentObject.transform;
    }

    private Vector3 GetCastDirection()
    {
        return worldCastDirection.sqrMagnitude > 0.0001f ? worldCastDirection.normalized : Vector3.down;
    }

    private static bool HasAnyPrefab(List<GameObject> prefabs)
    {
        if (prefabs == null)
            return false;

        for (int i = 0; i < prefabs.Count; i++)
        {
            if (prefabs[i] != null)
                return true;
        }

        return false;
    }

    private static float RandomRange(Vector2 range)
    {
        float min = Mathf.Min(range.x, range.y);
        float max = Mathf.Max(range.x, range.y);
        return UnityEngine.Random.Range(min, max);
    }

    private float CurrentTime
    {
        get { return useUnscaledTime ? Time.unscaledTime : Time.time; }
    }

    private void OnDrawGizmosSelected()
    {
        if (!drawDebugGizmos || legs == null)
            return;

        Vector3 direction = worldCastDirection.sqrMagnitude > 0.0001f
            ? worldCastDirection.normalized
            : Vector3.down;

        for (int i = 0; i < legs.Count; i++)
        {
            Leg leg = legs[i];
            if (leg == null || leg.bone == null)
                continue;

            Vector3 origin = leg.bone.TransformPoint(leg.castLocalOffset);
            Vector3 end = origin + direction * leg.castDistance;

            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(origin, leg.sphereRadius);
            Gizmos.DrawLine(origin, end);
            Gizmos.DrawWireSphere(end, leg.sphereRadius);

            if (Application.isPlaying && leg.debugHasHit)
            {
                Gizmos.color = Color.green;
                Gizmos.DrawSphere(leg.debugHitPoint, 0.035f);
            }
        }
    }
}
