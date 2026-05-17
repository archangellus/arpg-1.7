using System;
using System.Collections.Generic;
using UnityEngine;

#if UNITY_6000_0_OR_NEWER
using SurfacePhysicsMaterial = UnityEngine.PhysicsMaterial;
#else
using SurfacePhysicsMaterial = UnityEngine.PhysicMaterial;
#endif

[DisallowMultipleComponent]
public sealed class FootstepSpherecastSurfaceAudio : MonoBehaviour
{
    [Serializable]
    public class Leg
    {
        [Header("Leg")]
        public string legName = "Foot";
        public Transform bone;

        [Tooltip("Optional. If empty, one AudioSource will be created under the leg bone.")]
        public AudioSource audioSource;

        [Header("Spherecast")]
        public Vector3 castLocalOffset = new Vector3(0f, 0.12f, 0f);

        [Min(0.001f)]
        public float sphereRadius = 0.07f;

        [Min(0.001f)]
        public float castDistance = 0.35f;

        [Header("Step Triggering")]
        public bool playOnGroundContact = true;
        public bool playWhileGroundedByDistance = true;

        [Min(0f)]
        public float minStepInterval = 0.12f;

        [Min(0f)]
        public float minStepDistance = 0.25f;

        [Range(0f, 2f)]
        public float volumeMultiplier = 1f;

        [NonSerialized] internal bool initialized;
        [NonSerialized] internal bool wasGrounded;
        [NonSerialized] internal bool hasLastStepPoint;
        [NonSerialized] internal Vector3 lastStepPoint;
        [NonSerialized] internal float nextAllowedStepTime;
        [NonSerialized] internal AudioClip lastClip;

        [NonSerialized] internal bool debugHasHit;
        [NonSerialized] internal Vector3 debugHitPoint;
    }

    [Serializable]
    public class SurfaceAudioGroup
    {
        [Header("Group")]
        public string groupName = "Surface";

        [Tooltip("Random clip list for this surface type.")]
        public List<AudioClip> randomClips = new List<AudioClip>();

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

        [Header("Randomization")]
        public Vector2 volumeRange = new Vector2(0.85f, 1f);
        public Vector2 pitchRange = new Vector2(0.95f, 1.05f);

        public bool HasClips
        {
            get
            {
                if (randomClips == null) return false;

                for (int i = 0; i < randomClips.Count; i++)
                {
                    if (randomClips[i] != null)
                        return true;
                }

                return false;
            }
        }

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
                string hitTag = info.tag;

                for (int i = 0; i < tags.Count; i++)
                {
                    if (!string.IsNullOrWhiteSpace(tags[i]) &&
                        string.Equals(hitTag, tags[i], StringComparison.Ordinal))
                    {
                        return true;
                    }
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

            if (info.terrainDiffuseTexture != null &&
                NameContains(info.terrainDiffuseTexture.name, textureNameContains))
            {
                return true;
            }

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

        private static bool NameContains(string source, List<string> fragments)
        {
            if (string.IsNullOrEmpty(source) || fragments == null)
                return false;

            for (int i = 0; i < fragments.Count; i++)
            {
                string fragment = fragments[i];

                if (string.IsNullOrWhiteSpace(fragment))
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

    [Header("Legs")]
    public List<Leg> legs = new List<Leg>();

    [Header("Surface / Texture Type Groups")]
    public List<SurfaceAudioGroup> textureTypeGroups = new List<SurfaceAudioGroup>();

    [Header("Fallback")]
    public SurfaceAudioGroup fallbackGroup = new SurfaceAudioGroup
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

    [Tooltip("Prevents distance-based steps while the character is standing still.")]
    [Min(0f)]
    public float minimumRootSpeed = 0.05f;

    public bool useUnscaledTime;

    [Header("Randomization")]
    public bool avoidImmediateClipRepeat = true;

    [Header("Debug")]
    public bool drawDebugGizmos = true;

    private readonly RaycastHit[] hitBuffer = new RaycastHit[16];
    private Vector3 lastRootPosition;

    private void Awake()
    {
        lastRootPosition = transform.position;

        for (int i = 0; i < legs.Count; i++)
            EnsureAudioSource(legs[i]);
    }

    private void LateUpdate()
    {
        if (!automaticSteps)
            return;

        float deltaTime = useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
        float now = CurrentTime;

        float rootSpeed = 0f;
        if (deltaTime > 0f)
            rootSpeed = (transform.position - lastRootPosition).magnitude / deltaTime;

        for (int i = 0; i < legs.Count; i++)
            UpdateLeg(legs[i], now, rootSpeed);

        lastRootPosition = transform.position;
    }

    /// <summary>
    /// Optional Animation Event entry point.
    /// Add an Animation Event and pass the leg index: 0 = first leg, 1 = second leg, etc.
    /// This still uses the spherecast to find the current surface.
    /// </summary>
    public void Footstep(int legIndex)
    {
        if (legIndex < 0 || legIndex >= legs.Count)
            return;

        Leg leg = legs[legIndex];
        EnsureAudioSource(leg);

        if (TryCastLeg(leg, out RaycastHit hit))
        {
            PlayStep(leg, hit);
            leg.wasGrounded = true;
            leg.initialized = true;
        }
    }

    private void UpdateLeg(Leg leg, float now, float rootSpeed)
    {
        if (leg == null || leg.bone == null)
            return;

        EnsureAudioSource(leg);

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

        bool canPlay = now >= leg.nextAllowedStepTime;
        bool rootMoving = rootSpeed >= minimumRootSpeed;

        bool contactStep =
            leg.playOnGroundContact &&
            grounded &&
            !leg.wasGrounded;

        bool distanceStep =
            leg.playWhileGroundedByDistance &&
            grounded &&
            rootMoving &&
            leg.hasLastStepPoint &&
            Vector3.Distance(hit.point, leg.lastStepPoint) >= leg.minStepDistance;

        if (canPlay && (contactStep || distanceStep))
            PlayStep(leg, hit);

        leg.wasGrounded = grounded;
    }

    private bool TryCastLeg(Leg leg, out RaycastHit bestHit)
    {
        bestHit = default;

        if (leg == null || leg.bone == null)
            return false;

        Vector3 direction = worldCastDirection.sqrMagnitude > 0.0001f
            ? worldCastDirection.normalized
            : Vector3.down;

        Vector3 origin = leg.bone.TransformPoint(leg.castLocalOffset);

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

            if (ignoreOwnColliders && hit.collider.transform.IsChildOf(transform))
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

    private void PlayStep(Leg leg, RaycastHit hit)
    {
        SurfaceInfo surfaceInfo = BuildSurfaceInfo(hit);
        SurfaceAudioGroup group = FindSurfaceGroup(surfaceInfo);

        if (group == null || !group.HasClips)
            return;

        AudioClip clip = PickRandomClip(group.randomClips, leg.lastClip);
        if (clip == null)
            return;

        AudioSource source = leg.audioSource;
        if (source == null)
            return;

        float volume = RandomRange(group.volumeRange) * leg.volumeMultiplier;
        float pitch = RandomRange(group.pitchRange);

// Do NOT move the AudioSource transform here.
// If the assigned AudioSource is on the Player, Camera, or CharacterController,
// moving it can move the whole player/camera rig and break terrain visibility.
source.pitch = pitch;
source.PlayOneShot(clip, volume);

        leg.lastClip = clip;
        leg.lastStepPoint = hit.point;
        leg.hasLastStepPoint = true;
        leg.nextAllowedStepTime = CurrentTime + leg.minStepInterval;
    }

    private SurfaceAudioGroup FindSurfaceGroup(SurfaceInfo info)
    {
        if (textureTypeGroups != null)
        {
            for (int i = 0; i < textureTypeGroups.Count; i++)
            {
                SurfaceAudioGroup group = textureTypeGroups[i];

                if (group == null || !group.HasClips)
                    continue;

                if (group.Matches(info))
                    return group;
            }
        }

        return fallbackGroup;
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

private void EnsureAudioSource(Leg leg)
{
    if (leg == null)
        return;

    // Unity-safe null check.
    // This also catches destroyed / missing Unity object references.
    if (leg.audioSource != null)
    {
        leg.audioSource.playOnAwake = false;

        // Optional warning only after audioSource is confirmed valid.
        if (leg.audioSource.transform == transform)
        {
            Debug.LogWarning(
                $"Footstep leg '{leg.legName}' is using an AudioSource on the same GameObject as the footstep controller. " +
                "This is allowed, but it is safer to leave the Audio Source field empty and let the script create a dedicated child AudioSource.",
                this
            );
        }

        return;
    }

    Transform parent = leg.bone != null ? leg.bone : transform;

    GameObject audioObject = new GameObject(
        string.IsNullOrWhiteSpace(leg.legName)
            ? "Footstep AudioSource"
            : leg.legName + " Footstep AudioSource"
    );

    audioObject.transform.SetParent(parent, false);
    audioObject.transform.localPosition = Vector3.zero;
    audioObject.transform.localRotation = Quaternion.identity;
    audioObject.transform.localScale = Vector3.one;

    AudioSource source = audioObject.AddComponent<AudioSource>();
    source.playOnAwake = false;
    source.spatialBlend = 1f;
    source.rolloffMode = AudioRolloffMode.Logarithmic;
    source.minDistance = 1f;
    source.maxDistance = 15f;

    leg.audioSource = source;
}

    private AudioClip PickRandomClip(List<AudioClip> clips, AudioClip lastClip)
    {
        if (clips == null || clips.Count == 0)
            return null;

        if (clips.Count == 1)
            return clips[0];

        for (int attempt = 0; attempt < 12; attempt++)
        {
            AudioClip candidate = clips[UnityEngine.Random.Range(0, clips.Count)];

            if (candidate == null)
                continue;

            if (!avoidImmediateClipRepeat || candidate != lastClip)
                return candidate;
        }

        for (int i = 0; i < clips.Count; i++)
        {
            if (clips[i] != null && clips[i] != lastClip)
                return clips[i];
        }

        for (int i = 0; i < clips.Count; i++)
        {
            if (clips[i] != null)
                return clips[i];
        }

        return null;
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

            Gizmos.color = Color.yellow;
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