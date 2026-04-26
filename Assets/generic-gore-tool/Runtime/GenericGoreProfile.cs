using System;
using System.Collections.Generic;
using UnityEngine;

namespace GenericGore
{
    [CreateAssetMenu(menuName = "Generic Gore/Profile", fileName = "GenericGoreProfile")]
    public class GenericGoreProfile : ScriptableObject
    {
        [Header("Effects")]
        public GameObject bloodBurstPrefab;
        public GameObject woundLoopPrefab;

        [Header("Cut Surface")]
        [Tooltip("Optional URP material used on generated cut caps. If empty, the generator duplicates the last source material.")]
        public Material capMaterial;

        [Header("Physics")]
        public float explosionForce = 8f;
        public float explosionRadius = 1.2f;
        public float upwardsModifier = 0.15f;
        public float randomTorque = 8f;
        public float debrisLifetime = 8f;

        [Header("Mesh Partitioning")]
        [Tooltip("Any vertex that cannot be mapped to a group uses this index.")]
        public int fallbackGroup = 0;

        [Header("Groups")]
        public List<GoreGroupDefinition> groups = new();
    }

    [Serializable]
    public class GoreGroupDefinition
    {
        public string name = "Part";

        [Tooltip("Bone path relative to the source SkinnedMeshRenderer.rootBone. Empty = root bone.")]
        public string pivotBonePath = string.Empty;

        [Tooltip("Bone paths relative to the source SkinnedMeshRenderer.rootBone.")]
        public List<string> bonePaths = new();

        [Min(0.01f)]
        public float forceMultiplier = 1f;
    }
}
