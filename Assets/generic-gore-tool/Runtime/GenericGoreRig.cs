using System;
using System.Collections.Generic;
using UnityEngine;

namespace GenericGore
{
    [DisallowMultipleComponent]
    public class GenericGoreRig : MonoBehaviour
    {
        [SerializeField] private Animator animator;
        [SerializeField] private SkinnedMeshRenderer sourceRenderer;
        [SerializeField] private GenericGoreProfile profile;
        [SerializeField] private List<GoreRuntimePart> parts = new();

        public IReadOnlyList<GoreRuntimePart> Parts => parts;
        public GenericGoreProfile Profile => profile;

        public void Sever(string partName, Vector3 hitPoint, Vector3 direction)
        {
            if (string.IsNullOrWhiteSpace(partName))
                return;

            int index = parts.FindIndex(p => string.Equals(p.name, partName, StringComparison.OrdinalIgnoreCase));
            if (index < 0)
            {
                Debug.LogWarning($"[GenericGoreRig] Part '{partName}' was not found on {name}.", this);
                return;
            }

            Sever(index, hitPoint, direction);
        }

        public void Sever(int index, Vector3 hitPoint, Vector3 direction)
        {
            if (profile == null)
            {
                Debug.LogWarning("[GenericGoreRig] Missing GenericGoreProfile.", this);
                return;
            }

            if (index < 0 || index >= parts.Count)
                return;

            GoreRuntimePart part = parts[index];
            if (part == null || part.detached || part.liveRenderer == null || !part.liveRenderer.enabled)
                return;

            Mesh bakedMesh = new Mesh { name = $"{part.name}_Baked" };
            part.liveRenderer.BakeMesh(bakedMesh);

            GameObject debris = new GameObject($"{part.name}_Debris");
            debris.transform.SetPositionAndRotation(part.liveRenderer.transform.position, part.liveRenderer.transform.rotation);
            debris.transform.localScale = part.liveRenderer.transform.lossyScale;

            MeshFilter meshFilter = debris.AddComponent<MeshFilter>();
            meshFilter.sharedMesh = bakedMesh;

            MeshRenderer meshRenderer = debris.AddComponent<MeshRenderer>();
            meshRenderer.sharedMaterials = part.liveRenderer.sharedMaterials;
            meshRenderer.shadowCastingMode = part.liveRenderer.shadowCastingMode;
            meshRenderer.receiveShadows = part.liveRenderer.receiveShadows;
            meshRenderer.lightProbeUsage = part.liveRenderer.lightProbeUsage;
            meshRenderer.reflectionProbeUsage = part.liveRenderer.reflectionProbeUsage;

            AddBestEffortCollider(debris, bakedMesh);

            Rigidbody rigidbody = debris.AddComponent<Rigidbody>();
            rigidbody.interpolation = RigidbodyInterpolation.Interpolate;
            rigidbody.mass = Mathf.Max(0.01f, part.mass);

            Vector3 pivot = part.pivot != null ? part.pivot.position : debris.transform.position;
            float force = profile.explosionForce * Mathf.Max(0.01f, part.forceMultiplier);
            rigidbody.AddExplosionForce(force, hitPoint, profile.explosionRadius, profile.upwardsModifier, ForceMode.Impulse);

            Vector3 impulseDirection = direction.sqrMagnitude > 0.0001f
                ? direction.normalized
                : (pivot - hitPoint).normalized;
            if (impulseDirection.sqrMagnitude > 0.0001f)
                rigidbody.AddForce(impulseDirection * force * 0.25f, ForceMode.Impulse);

            rigidbody.AddTorque(UnityEngine.Random.insideUnitSphere * profile.randomTorque, ForceMode.Impulse);

            if (profile.debrisLifetime > 0f)
                Destroy(debris, profile.debrisLifetime);

            SpawnEffects(part, pivot, impulseDirection);

            part.liveRenderer.enabled = false;
            part.detached = true;
        }

        public void ExplodeAll(Vector3 hitPoint)
        {
            for (int i = 0; i < parts.Count; i++)
            {
                Vector3 direction = parts[i].pivot != null
                    ? (parts[i].pivot.position - hitPoint).normalized
                    : Vector3.up;

                Sever(i, hitPoint, direction);
            }

            if (animator != null)
                animator.enabled = false;

            if (sourceRenderer != null)
                sourceRenderer.enabled = false;
        }

        public bool IsDetached(string partName)
        {
            int index = parts.FindIndex(p => string.Equals(p.name, partName, StringComparison.OrdinalIgnoreCase));
            return index >= 0 && parts[index].detached;
        }

        private void SpawnEffects(GoreRuntimePart part, Vector3 position, Vector3 direction)
        {
            Quaternion rotation = direction.sqrMagnitude > 0.0001f
                ? Quaternion.LookRotation(direction)
                : Quaternion.identity;

            if (profile.bloodBurstPrefab != null)
                Instantiate(profile.bloodBurstPrefab, position, rotation);

            if (profile.woundLoopPrefab != null)
                Instantiate(profile.woundLoopPrefab, position, rotation, transform);
        }

        private static void AddBestEffortCollider(GameObject debris, Mesh mesh)
        {
            int triangleCount = mesh.triangles.Length / 3;
            if (triangleCount > 0 && triangleCount <= 255)
            {
                MeshCollider collider = debris.AddComponent<MeshCollider>();
                collider.sharedMesh = mesh;
                collider.convex = true;
                return;
            }

            BoxCollider boxCollider = debris.AddComponent<BoxCollider>();
            boxCollider.center = mesh.bounds.center;
            boxCollider.size = mesh.bounds.size;
        }

#if UNITY_EDITOR
        public void EditorAssign(Animator assignedAnimator,
                                 SkinnedMeshRenderer assignedSourceRenderer,
                                 GenericGoreProfile assignedProfile,
                                 List<GoreRuntimePart> assignedParts)
        {
            animator = assignedAnimator;
            sourceRenderer = assignedSourceRenderer;
            profile = assignedProfile;
            parts = assignedParts;
        }
#endif
    }

    [Serializable]
    public class GoreRuntimePart
    {
        public string name;
        public SkinnedMeshRenderer liveRenderer;
        public Transform pivot;
        public float forceMultiplier = 1f;
        public float mass = 1f;
        [HideInInspector] public bool detached;
    }
}
