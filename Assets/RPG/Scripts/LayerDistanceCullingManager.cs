using System;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class LayerDistanceCullingManager : MonoBehaviour
{
    public enum DistanceMode
    {
        World3D,
        PlanarXZ
    }

    [Serializable]
    public class LayerCullRule
    {
        public string label = "New Rule";
        public bool enabled = true;
        public LayerMask layers;
        [Min(0f)] public float maxDistance = 20f;
    }

    private sealed class CacheEntry
    {
        public LODGroup lodGroup;
        public Renderer standaloneRenderer;
        public Renderer[] renderers;
        public float maxDistance;
        public float maxDistanceSqr;
        public string debugName;
    }

    [Header("Search Roots")]
    [Tooltip("If empty, the whole loaded scene is scanned.")]
    [SerializeField] private Transform[] searchRoots;

    [Header("Rules")]
    [SerializeField] private LayerCullRule[] rules;

    [Header("Runtime")]
    [SerializeField, Min(0.02f)] private float refreshInterval = 0.15f;
    [SerializeField] private DistanceMode distanceMode = DistanceMode.PlanarXZ;
    [SerializeField] private bool useClosestPointOnBoundsForStandaloneRenderers = true;
    [SerializeField] private bool autoRebuildOnEnable = true;
    [SerializeField] private bool includeInactiveWhenBuildingCache = false;

    private readonly List<CacheEntry> cachedEntries = new();
    private float refreshTimer;
    private Camera cachedMainCamera;

    public Transform[] SearchRoots
    {
        get => searchRoots;
        set => searchRoots = value;
    }

    public LayerCullRule[] Rules
    {
        get => rules;
        set => rules = value;
    }

    public float RefreshInterval
    {
        get => refreshInterval;
        set => refreshInterval = Mathf.Max(0.02f, value);
    }

    public DistanceMode CurrentDistanceMode
    {
        get => distanceMode;
        set => distanceMode = value;
    }

    public bool UseClosestPointOnBoundsForStandaloneRenderers
    {
        get => useClosestPointOnBoundsForStandaloneRenderers;
        set => useClosestPointOnBoundsForStandaloneRenderers = value;
    }

    public bool AutoRebuildOnEnable
    {
        get => autoRebuildOnEnable;
        set => autoRebuildOnEnable = value;
    }

    public bool IncludeInactiveWhenBuildingCache
    {
        get => includeInactiveWhenBuildingCache;
        set => includeInactiveWhenBuildingCache = value;
    }

    public int CachedEntryCount => cachedEntries.Count;

    public Camera TargetCamera => GetMainCamera();

    private void Reset()
    {
        CacheMainCamera();
    }

    private void OnEnable()
    {
        CacheMainCamera();

        if (autoRebuildOnEnable)
            RebuildCache();

        ForceRefresh();
    }

    private void Update()
    {
        Camera cam = GetMainCamera();
        if (cam == null)
            return;

        refreshTimer -= Time.unscaledDeltaTime;
        if (refreshTimer > 0f)
            return;

        refreshTimer = refreshInterval;
        ApplyCulling(cam);
    }

    private void OnDisable()
    {
        RestoreAllRendering();
    }

    public void RebuildCache()
    {
        RestoreAllRendering();
        cachedEntries.Clear();

        float[] perLayerDistances = BuildPerLayerDistanceTable();
        bool[] usedLayers = BuildUsedLayerTable(perLayerDistances);

        HashSet<LODGroup> uniqueLodGroups = new();
        HashSet<Renderer> renderersBelongingToLodGroups = new();
        HashSet<Renderer> uniqueStandaloneRenderers = new();

        // First: cache LODGroups as one unit
        if (searchRoots != null && searchRoots.Length > 0)
        {
            foreach (Transform root in searchRoots)
            {
                if (root == null)
                    continue;

                LODGroup[] lodGroups = root.GetComponentsInChildren<LODGroup>(includeInactiveWhenBuildingCache);
                for (int i = 0; i < lodGroups.Length; i++)
                    TryCacheLodGroup(lodGroups[i], usedLayers, perLayerDistances, uniqueLodGroups, renderersBelongingToLodGroups);
            }
        }
        else
        {
#if UNITY_2023_1_OR_NEWER
            FindObjectsInactive inactiveMode = includeInactiveWhenBuildingCache
                ? FindObjectsInactive.Include
                : FindObjectsInactive.Exclude;

            LODGroup[] lodGroups = FindObjectsByType<LODGroup>(inactiveMode, FindObjectsSortMode.None);
#else
            LODGroup[] lodGroups = FindObjectsOfType<LODGroup>(includeInactiveWhenBuildingCache);
#endif
            for (int i = 0; i < lodGroups.Length; i++)
                TryCacheLodGroup(lodGroups[i], usedLayers, perLayerDistances, uniqueLodGroups, renderersBelongingToLodGroups);
        }

        // Second: cache non-LOD renderers
        if (searchRoots != null && searchRoots.Length > 0)
        {
            foreach (Transform root in searchRoots)
            {
                if (root == null)
                    continue;

                Renderer[] renderers = root.GetComponentsInChildren<Renderer>(includeInactiveWhenBuildingCache);
                for (int i = 0; i < renderers.Length; i++)
                    TryCacheStandaloneRenderer(renderers[i], usedLayers, perLayerDistances, renderersBelongingToLodGroups, uniqueStandaloneRenderers);
            }
        }
        else
        {
#if UNITY_2023_1_OR_NEWER
            FindObjectsInactive inactiveMode = includeInactiveWhenBuildingCache
                ? FindObjectsInactive.Include
                : FindObjectsInactive.Exclude;

            Renderer[] renderers = FindObjectsByType<Renderer>(inactiveMode, FindObjectsSortMode.None);
#else
            Renderer[] renderers = FindObjectsOfType<Renderer>(includeInactiveWhenBuildingCache);
#endif
            for (int i = 0; i < renderers.Length; i++)
                TryCacheStandaloneRenderer(renderers[i], usedLayers, perLayerDistances, renderersBelongingToLodGroups, uniqueStandaloneRenderers);
        }

        refreshTimer = 0f;
    }

    public void ForceRefresh()
    {
        refreshTimer = 0f;

        Camera cam = GetMainCamera();
        if (cam != null)
            ApplyCulling(cam);
    }

    public void RestoreAllRendering()
    {
        for (int i = 0; i < cachedEntries.Count; i++)
        {
            CacheEntry entry = cachedEntries[i];
            if (entry == null || entry.renderers == null)
                continue;

            for (int r = 0; r < entry.renderers.Length; r++)
            {
                if (entry.renderers[r] != null)
                    entry.renderers[r].forceRenderingOff = false;
            }
        }
    }

    public string GetDuplicateLayerWarning()
    {
        if (rules == null || rules.Length == 0)
            return string.Empty;

        Dictionary<int, List<string>> layerToRules = new();

        for (int i = 0; i < rules.Length; i++)
        {
            LayerCullRule rule = rules[i];
            if (rule == null || !rule.enabled)
                continue;

            int mask = rule.layers.value;

            for (int layer = 0; layer < 32; layer++)
            {
                if ((mask & (1 << layer)) == 0)
                    continue;

                if (!layerToRules.TryGetValue(layer, out List<string> labels))
                {
                    labels = new List<string>();
                    layerToRules.Add(layer, labels);
                }

                labels.Add(string.IsNullOrWhiteSpace(rule.label) ? $"Rule {i + 1}" : rule.label);
            }
        }

        List<string> conflicts = new();

        foreach (var kvp in layerToRules)
        {
            if (kvp.Value.Count > 1)
            {
                string layerName = LayerMask.LayerToName(kvp.Key);
                conflicts.Add($"{layerName}: {string.Join(", ", kvp.Value)}");
            }
        }

        return conflicts.Count == 0
            ? string.Empty
            : "Duplicate layer assignments detected. Last matching rule wins:\n- " + string.Join("\n- ", conflicts);
    }

    private void ApplyCulling(Camera cam)
    {
        Vector3 camPos = cam.transform.position;
        Vector2 camPosXZ = new Vector2(camPos.x, camPos.z);

        for (int i = cachedEntries.Count - 1; i >= 0; i--)
        {
            CacheEntry entry = cachedEntries[i];
            if (entry == null)
            {
                cachedEntries.RemoveAt(i);
                continue;
            }

            if (entry.lodGroup != null)
            {
                if (entry.lodGroup.gameObject == null)
                {
                    cachedEntries.RemoveAt(i);
                    continue;
                }

                if (!entry.lodGroup.gameObject.activeInHierarchy || !entry.lodGroup.enabled)
                {
                    SetEntryRendering(entry, true);
                    continue;
                }

                Vector3 samplePoint = entry.lodGroup.transform.TransformPoint(entry.lodGroup.localReferencePoint);
                bool shouldRender = IsWithinDistance(camPos, camPosXZ, samplePoint, entry.maxDistance, entry.maxDistanceSqr);
                SetEntryRendering(entry, shouldRender);
            }
            else if (entry.standaloneRenderer != null)
            {
                Renderer renderer = entry.standaloneRenderer;

                if (renderer == null)
                {
                    cachedEntries.RemoveAt(i);
                    continue;
                }

                if (!renderer.enabled || !renderer.gameObject.activeInHierarchy)
                {
                    renderer.forceRenderingOff = false;
                    continue;
                }

                Bounds bounds = renderer.bounds;
                Vector3 samplePoint = useClosestPointOnBoundsForStandaloneRenderers
                    ? bounds.ClosestPoint(camPos)
                    : bounds.center;

                bool shouldRender = IsWithinDistance(camPos, camPosXZ, samplePoint, entry.maxDistance, entry.maxDistanceSqr);
                renderer.forceRenderingOff = !shouldRender;
            }
            else
            {
                cachedEntries.RemoveAt(i);
            }
        }
    }

    private bool IsWithinDistance(Vector3 camPos, Vector2 camPosXZ, Vector3 samplePoint, float maxDistance, float maxDistanceSqr)
    {
        switch (distanceMode)
        {
            case DistanceMode.PlanarXZ:
            {
                Vector2 pointXZ = new Vector2(samplePoint.x, samplePoint.z);
                return (pointXZ - camPosXZ).sqrMagnitude <= maxDistanceSqr;
            }

            default:
                return (samplePoint - camPos).sqrMagnitude <= maxDistanceSqr;
        }
    }

    private void SetEntryRendering(CacheEntry entry, bool shouldRender)
    {
        if (entry.renderers == null)
            return;

        for (int i = 0; i < entry.renderers.Length; i++)
        {
            Renderer r = entry.renderers[i];
            if (r == null)
                continue;

            if (!r.gameObject.activeInHierarchy)
            {
                r.forceRenderingOff = false;
                continue;
            }

            r.forceRenderingOff = !shouldRender;
        }
    }

    private void TryCacheLodGroup(
        LODGroup lodGroup,
        bool[] usedLayers,
        float[] perLayerDistances,
        HashSet<LODGroup> uniqueLodGroups,
        HashSet<Renderer> renderersBelongingToLodGroups)
    {
        if (lodGroup == null || !uniqueLodGroups.Add(lodGroup))
            return;

        LOD[] lods = lodGroup.GetLODs();
        List<Renderer> groupRenderers = new();
        float longestDistance = 0f;

        for (int i = 0; i < lods.Length; i++)
        {
            Renderer[] renderers = lods[i].renderers;
            if (renderers == null)
                continue;

            for (int r = 0; r < renderers.Length; r++)
            {
                Renderer renderer = renderers[r];
                if (renderer == null)
                    continue;

                groupRenderers.Add(renderer);
                renderersBelongingToLodGroups.Add(renderer);

                int layer = renderer.gameObject.layer;
                if (layer < 0 || layer >= 32 || !usedLayers[layer])
                    continue;

                float dist = perLayerDistances[layer];
                if (dist > longestDistance)
                    longestDistance = dist;
            }
        }

        if (groupRenderers.Count == 0 || longestDistance <= 0f)
            return;

        cachedEntries.Add(new CacheEntry
        {
            lodGroup = lodGroup,
            renderers = groupRenderers.ToArray(),
            maxDistance = longestDistance,
            maxDistanceSqr = longestDistance * longestDistance,
            debugName = lodGroup.name
        });
    }

    private void TryCacheStandaloneRenderer(
        Renderer renderer,
        bool[] usedLayers,
        float[] perLayerDistances,
        HashSet<Renderer> renderersBelongingToLodGroups,
        HashSet<Renderer> uniqueStandaloneRenderers)
    {
        if (renderer == null || !uniqueStandaloneRenderers.Add(renderer))
            return;

        if (renderersBelongingToLodGroups.Contains(renderer))
            return;

        int layer = renderer.gameObject.layer;
        if (layer < 0 || layer >= 32 || !usedLayers[layer])
            return;

        float maxDistance = perLayerDistances[layer];
        if (maxDistance <= 0f)
            return;

        cachedEntries.Add(new CacheEntry
        {
            standaloneRenderer = renderer,
            renderers = new[] { renderer },
            maxDistance = maxDistance,
            maxDistanceSqr = maxDistance * maxDistance,
            debugName = renderer.name
        });
    }

    private float[] BuildPerLayerDistanceTable()
    {
        float[] distances = new float[32];

        if (rules == null)
            return distances;

        for (int i = 0; i < rules.Length; i++)
        {
            LayerCullRule rule = rules[i];
            if (rule == null || !rule.enabled)
                continue;

            int mask = rule.layers.value;
            for (int layer = 0; layer < 32; layer++)
            {
                if ((mask & (1 << layer)) != 0)
                    distances[layer] = rule.maxDistance;
            }
        }

        return distances;
    }

    private bool[] BuildUsedLayerTable(float[] perLayerDistances)
    {
        bool[] used = new bool[32];
        for (int i = 0; i < perLayerDistances.Length; i++)
            used[i] = perLayerDistances[i] > 0f;

        return used;
    }

    private void CacheMainCamera()
    {
        if (cachedMainCamera == null || !cachedMainCamera.isActiveAndEnabled)
            cachedMainCamera = Camera.main;
    }

    private Camera GetMainCamera()
    {
        CacheMainCamera();
        return cachedMainCamera;
    }
}