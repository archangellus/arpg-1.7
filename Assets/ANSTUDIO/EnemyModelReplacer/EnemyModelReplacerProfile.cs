using System;
using System.Collections.Generic;
using UnityEngine;

namespace PLAYERTWO.ARPGProject
{
    [Serializable]
    public class EnemyModelReplacerProfileClipField
    {
        public string originalClipName;
        public string originalClipGlobalId;
        public string replacementClipGlobalId;
    }

    [CreateAssetMenu(
        fileName = "Enemy Model Replacer Profile",
        menuName = "TOOLS/ANSTUDIO/Enemy Model Replacer Profile")]
    public class EnemyModelReplacerProfile : ScriptableObject
    {
        [Header("Object References")]
        public string enemyRootGlobalId;
        public string newModelPrefabGlobalId;

        [Header("Model Replacement")]
        public Vector2 windowScrollPosition;
        public int selectedTab;
        public float previewYaw;
        public string modelContainerName = "Model";
        public bool autoApplyToChildren = true;
        public bool removePreviousModel = true;

        [Header("New Model Position")]
        public bool forceNewModelY;
        public float forcedNewModelLocalY = -1f;

        [Header("Animator Override Controller")]
        public bool createAnimatorOverrideController;
        public string animatorOverrideControllerName = "Enemy Animator Override";
        public string animatorOverrideControllerFolder = "Assets/ANSTUDIO/Enemy Model Replacer/Enemy Model Replacer Profiles/";
        public string animatorOverrideMirrorSourceGlobalId;
        public List<EnemyModelReplacerProfileClipField> animatorOverrideClipFields = new List<EnemyModelReplacerProfileClipField>();
        public bool showAnimatorOverrideClipFields = true;
        public Vector2 animatorOverrideClipFieldsScrollPosition;

        [Header("Standalone Enemy Duplicates")]
        public bool createStandaloneDuplicates;
        public int duplicateCount;
        public string duplicateNamePrefix = "Enemy Copy";
        public float duplicateMinRadius = 2f;
        public float duplicateMaxRadius = 6f;
        public float duplicateMinSpacing = 2f;
        public int duplicatePlacementAttempts = 100;

        [Header("Duplicate Spawn Collision")]
        public bool avoidSpawnCollisionLayers;
        public int blockedSpawnLayers;
        public float spawnCollisionCheckRadius = 1f;
        public float spawnCollisionCheckYOffset = 0.5f;
        public QueryTriggerInteraction spawnCollisionTriggerInteraction = QueryTriggerInteraction.Ignore;

        [Header("Scene Gizmo Preview")]
        public float duplicateRangeGizmoYOffset;
    }
}
