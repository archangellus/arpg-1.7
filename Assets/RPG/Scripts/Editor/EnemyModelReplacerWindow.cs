using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditorInternal;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace PLAYERTWO.ARPGProject
{
    public class EnemyModelReplacerWindow : EditorWindow
    {
        private const string k_menuPath = "PLAYER TWO/ARPG Project/Tools/Enemy Model Replacer";

        [SerializeField] private GameObject enemyRoot;
        [SerializeField] private GameObject newModelPrefab;
        [SerializeField] private string modelContainerName = "Model";
        [SerializeField] private bool autoApplyToChildren = true;
        [SerializeField] private bool removePreviousModel = true;

        [Header("New Model Position")]
        [SerializeField] private bool forceNewModelY = false;
        [SerializeField] private float forcedNewModelLocalY = 0f;

        [Header("Standalone Enemy Duplicates")]
        [SerializeField] private bool createStandaloneDuplicates = false;
        [SerializeField] private int duplicateCount = 0;
        [SerializeField] private string duplicateNamePrefix = "Enemy Copy";
        [SerializeField] private float duplicateMinRadius = 2f;
        [SerializeField] private float duplicateMaxRadius = 6f;
        [SerializeField] private float duplicateMinSpacing = 2f;
        [SerializeField] private int duplicatePlacementAttempts = 100;

        [Header("Duplicate Spawn Collision")]
        [SerializeField] private bool avoidSpawnCollisionLayers = false;
        [SerializeField] private LayerMask blockedSpawnLayers = 0;
        [SerializeField] private float spawnCollisionCheckRadius = 1f;
        [SerializeField] private float spawnCollisionCheckYOffset = 0.5f;
        [SerializeField] private QueryTriggerInteraction spawnCollisionTriggerInteraction = QueryTriggerInteraction.Ignore;

        [Header("Scene Gizmo Preview")]
        [SerializeField] private float duplicateRangeGizmoYOffset = 0f;

        [MenuItem(k_menuPath)]
        public static void Open() => GetWindow<EnemyModelReplacerWindow>("Enemy Model Replacer");

        private void OnEnable()
        {
            SceneView.duringSceneGui -= DrawRangeGizmosInScene;
            SceneView.duringSceneGui += DrawRangeGizmosInScene;
        }

        private void OnDisable()
        {
            SceneView.duringSceneGui -= DrawRangeGizmosInScene;
        }

        private void OnGUI()
        {
            EditorGUILayout.HelpBox("Drag an enemy and a new model prefab, then click Replace Model & Rebind.", MessageType.Info);

            EditorGUI.BeginChangeCheck();

            enemyRoot = (GameObject)EditorGUILayout.ObjectField("Enemy Root", enemyRoot, typeof(GameObject), true);
            newModelPrefab = (GameObject)EditorGUILayout.ObjectField("New Model", newModelPrefab, typeof(GameObject), false);
            modelContainerName = EditorGUILayout.TextField("Model Container Name", modelContainerName);
            autoApplyToChildren = EditorGUILayout.Toggle("Auto Rebind References", autoApplyToChildren);
            removePreviousModel = EditorGUILayout.Toggle("Remove Previous Model", removePreviousModel);

            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("New Model Position", EditorStyles.boldLabel);

            forceNewModelY = EditorGUILayout.Toggle("Force New Model Local Y", forceNewModelY);

            using (new EditorGUI.DisabledScope(!forceNewModelY))
            {
                forcedNewModelLocalY = EditorGUILayout.FloatField("Forced Local Y", forcedNewModelLocalY);
            }

            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("Standalone Enemy Duplicates", EditorStyles.boldLabel);

            createStandaloneDuplicates = EditorGUILayout.Toggle("Create Duplicates", createStandaloneDuplicates);

            using (new EditorGUI.DisabledScope(!createStandaloneDuplicates))
            {
                duplicateCount = Mathf.Max(0, EditorGUILayout.IntField("Duplicate Count", duplicateCount));
                duplicateNamePrefix = EditorGUILayout.TextField("Duplicate Enemy Name Prefix", duplicateNamePrefix);
                duplicateMinRadius = Mathf.Max(0f, EditorGUILayout.FloatField("Min Range From Enemy Root", duplicateMinRadius));
                duplicateMaxRadius = Mathf.Max(duplicateMinRadius, EditorGUILayout.FloatField("Max Range From Enemy Root", duplicateMaxRadius));
                duplicateMinSpacing = Mathf.Max(0f, EditorGUILayout.FloatField("Space Between Duplicates", duplicateMinSpacing));
                duplicatePlacementAttempts = Mathf.Max(1, EditorGUILayout.IntField("Placement Attempts", duplicatePlacementAttempts));

                EditorGUILayout.Space(4);
                EditorGUILayout.LabelField("Spawn Collision", EditorStyles.boldLabel);
                avoidSpawnCollisionLayers = EditorGUILayout.Toggle("Avoid Layers", avoidSpawnCollisionLayers);

                using (new EditorGUI.DisabledScope(!avoidSpawnCollisionLayers))
                {
                    blockedSpawnLayers = LayerMaskField("Blocked Spawn Layers", blockedSpawnLayers);
                    spawnCollisionCheckRadius = Mathf.Max(0.01f, EditorGUILayout.FloatField("Collision Check Radius", spawnCollisionCheckRadius));
                    spawnCollisionCheckYOffset = EditorGUILayout.FloatField("Collision Check Y Offset", spawnCollisionCheckYOffset);
                    spawnCollisionTriggerInteraction = (QueryTriggerInteraction)EditorGUILayout.EnumPopup("Trigger Colliders", spawnCollisionTriggerInteraction);
                }

                EditorGUILayout.HelpBox("After the selected Enemy Root is converted and rebound, the whole converted enemy object is duplicated as standalone scene objects with no parent. Enemy Root is only used as the center position for random placement. Enable layer avoidance to reject spawn points that overlap colliders on selected layers.", MessageType.None);
            }

            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("Scene Gizmo Preview", EditorStyles.boldLabel);
            duplicateRangeGizmoYOffset = EditorGUILayout.FloatField("Gizmo Y Offset", duplicateRangeGizmoYOffset);

            using (new EditorGUI.DisabledScope(enemyRoot == null))
            {
                if (GUILayout.Button("Reset Gizmo Y Offset"))
                    duplicateRangeGizmoYOffset = 0f;
            }

            if (EditorGUI.EndChangeCheck())
                SceneView.RepaintAll();

            using (new EditorGUI.DisabledScope(enemyRoot == null || newModelPrefab == null))
            {
                if (GUILayout.Button("Replace Model & Rebind", GUILayout.Height(32)))
                    ReplaceModel();
            }
        }

        private void DrawRangeGizmosInScene(SceneView sceneView)
        {
            if (enemyRoot == null || !createStandaloneDuplicates)
                return;

            var center = enemyRoot.transform.position + Vector3.up * duplicateRangeGizmoYOffset;
            var minRadius = Mathf.Max(0f, duplicateMinRadius);
            var maxRadius = Mathf.Max(minRadius, duplicateMaxRadius);
            var previousColor = Handles.color;
            var previousZTest = Handles.zTest;

            try
            {
                Handles.zTest = UnityEngine.Rendering.CompareFunction.LessEqual;

                Handles.color = new Color(0.1f, 0.65f, 1f, 0.18f);
                Handles.DrawSolidDisc(center, Vector3.up, maxRadius);

                if (minRadius > 0f)
                {
                    Handles.color = new Color(1f, 0.75f, 0.15f, 0.18f);
                    Handles.DrawSolidDisc(center, Vector3.up, minRadius);
                }

                Handles.color = new Color(0.1f, 0.65f, 1f, 0.95f);
                Handles.DrawWireDisc(center, Vector3.up, maxRadius);

                if (minRadius > 0f)
                {
                    Handles.color = new Color(1f, 0.75f, 0.15f, 0.95f);
                    Handles.DrawWireDisc(center, Vector3.up, minRadius);
                }

                Handles.color = Color.white;

                var labelOffset = Vector3.forward * Mathf.Max(1f, maxRadius) + Vector3.up * 0.5f;
                var layerAvoidanceText = avoidSpawnCollisionLayers
                    ? $"\nAvoid Layers: On | Check Radius: {Mathf.Max(0.01f, spawnCollisionCheckRadius):0.##}"
                    : "\nAvoid Layers: Off";

                Handles.Label(
                    center + labelOffset,
                    $"Duplicate Range\nMin: {minRadius:0.##} | Max: {maxRadius:0.##}\nSpacing: {Mathf.Max(0f, duplicateMinSpacing):0.##}\nGizmo Y Offset: {duplicateRangeGizmoYOffset:0.##}{layerAvoidanceText}",
                    EditorStyles.boldLabel);
            }
            finally
            {
                Handles.color = previousColor;
                Handles.zTest = previousZTest;
            }
        }

        private void ReplaceModel()
        {
            Undo.IncrementCurrentGroup();
            var group = Undo.GetCurrentGroup();

            var rootTransform = enemyRoot.transform;
            var modelContainer = rootTransform.Find(modelContainerName) ?? rootTransform;
            var previousModel = modelContainer.childCount > 0 ? modelContainer.GetChild(0).gameObject : null;
            var previousModelLocalPosition = previousModel ? previousModel.transform.localPosition : Vector3.zero;

            var instance = InstantiatePrefabInScene(newModelPrefab, enemyRoot.scene);

            Undo.RegisterCreatedObjectUndo(instance, "Create New Enemy Model");
            instance.name = newModelPrefab.name;
            instance.transform.SetParent(modelContainer, false);

            var newLocalPosition = previousModel ? previousModelLocalPosition : instance.transform.localPosition;

            if (forceNewModelY)
                newLocalPosition.y = forcedNewModelLocalY;

            instance.transform.localPosition = newLocalPosition;
            instance.transform.localRotation = Quaternion.identity;
            instance.transform.localScale = Vector3.one;

            if (previousModel)
            {
                CopyModelComponents(previousModel.transform, instance.transform);
                CopyAnimatorControllerKeepingAvatar(previousModel, instance);
            }

            if (removePreviousModel && previousModel)
                Undo.DestroyObjectImmediate(previousModel);

            if (autoApplyToChildren)
                RebindModelReferences(enemyRoot, instance.transform);

            EditorUtility.SetDirty(enemyRoot);
            if (PrefabUtility.IsPartOfPrefabInstance(enemyRoot))
                PrefabUtility.RecordPrefabInstancePropertyModifications(enemyRoot);

            var createdDuplicates = 0;
            if (createStandaloneDuplicates && duplicateCount > 0)
                createdDuplicates = CreateStandaloneEnemyDuplicates(rootTransform);

            EditorSceneManager.MarkSceneDirty(enemyRoot.scene);
            Undo.CollapseUndoOperations(group);

            Debug.Log($"[EnemyModelReplacer] Model replaced and references rebound for '{enemyRoot.name}'. Standalone converted enemy duplicates created: {createdDuplicates}.", enemyRoot);
        }

        private int CreateStandaloneEnemyDuplicates(Transform sourceRoot)
        {
            var created = 0;
            var acceptedPositions = new List<Vector3>();
            var center = sourceRoot.position;

            for (var i = 0; i < duplicateCount; i++)
            {
                if (!TryGetRandomDuplicatePosition(center, acceptedPositions, out var duplicatePosition))
                {
                    Debug.LogWarning($"[EnemyModelReplacer] Could only place {created} of {duplicateCount} converted enemy duplicates. Increase the range, lower spacing, raise placement attempts, lower the collision radius, or adjust the blocked spawn layers.", enemyRoot);
                    break;
                }

                var duplicate = InstantiateConvertedEnemyCopy(enemyRoot, enemyRoot.scene);
                Undo.RegisterCreatedObjectUndo(duplicate, "Create Duplicate Converted Enemy");

                duplicate.name = BuildDuplicateName(i);
                duplicate.transform.SetParent(null, true);
                duplicate.transform.position = duplicatePosition;
                duplicate.transform.rotation = sourceRoot.rotation;
                duplicate.transform.localScale = sourceRoot.lossyScale;

                EditorUtility.SetDirty(duplicate);
                if (PrefabUtility.IsPartOfPrefabInstance(duplicate))
                    PrefabUtility.RecordPrefabInstancePropertyModifications(duplicate);

                acceptedPositions.Add(duplicatePosition);
                created++;
            }

            return created;
        }

        private bool TryGetRandomDuplicatePosition(Vector3 center, List<Vector3> acceptedPositions, out Vector3 position)
        {
            for (var attempt = 0; attempt < duplicatePlacementAttempts; attempt++)
            {
                var distance = UnityEngine.Random.Range(duplicateMinRadius, duplicateMaxRadius);
                var angle = UnityEngine.Random.Range(0f, Mathf.PI * 2f);
                var candidate = new Vector3(
                    center.x + Mathf.Cos(angle) * distance,
                    center.y,
                    center.z + Mathf.Sin(angle) * distance);

                if (HasEnoughSpace(candidate, acceptedPositions) && !IsBlockedBySpawnLayers(candidate))
                {
                    position = candidate;
                    return true;
                }
            }

            position = center;
            return false;
        }

        private bool HasEnoughSpace(Vector3 candidate, List<Vector3> acceptedPositions)
        {
            foreach (var accepted in acceptedPositions)
            {
                var candidateXZ = new Vector2(candidate.x, candidate.z);
                var acceptedXZ = new Vector2(accepted.x, accepted.z);

                if (Vector2.Distance(candidateXZ, acceptedXZ) < duplicateMinSpacing)
                    return false;
            }

            return true;
        }

        private bool IsBlockedBySpawnLayers(Vector3 candidate)
        {
            if (!avoidSpawnCollisionLayers || blockedSpawnLayers.value == 0)
                return false;

            var checkCenter = candidate + Vector3.up * spawnCollisionCheckYOffset;
            return Physics.CheckSphere(
                checkCenter,
                Mathf.Max(0.01f, spawnCollisionCheckRadius),
                blockedSpawnLayers,
                spawnCollisionTriggerInteraction);
        }

        private static LayerMask LayerMaskField(string label, LayerMask selected)
        {
            var layers = InternalEditorUtility.layers;
            var compactMask = 0;

            for (var i = 0; i < layers.Length; i++)
            {
                var layer = LayerMask.NameToLayer(layers[i]);
                if (layer >= 0 && (selected.value & (1 << layer)) != 0)
                    compactMask |= 1 << i;
            }

            compactMask = EditorGUILayout.MaskField(label, compactMask, layers);

            var layerMask = 0;
            for (var i = 0; i < layers.Length; i++)
            {
                if ((compactMask & (1 << i)) == 0)
                    continue;

                var layer = LayerMask.NameToLayer(layers[i]);
                if (layer >= 0)
                    layerMask |= 1 << layer;
            }

            selected.value = layerMask;
            return selected;
        }

        private string BuildDuplicateName(int index)
        {
            var baseName = string.IsNullOrWhiteSpace(duplicateNamePrefix)
                ? enemyRoot.name
                : duplicateNamePrefix.Trim();

            return $"{baseName}_{index + 1:00}";
        }

        private static GameObject InstantiateConvertedEnemyCopy(GameObject source, Scene targetScene)
        {
            var duplicate = UnityEngine.Object.Instantiate(source);

            if (targetScene.IsValid() && duplicate.scene != targetScene)
                SceneManager.MoveGameObjectToScene(duplicate, targetScene);

            return duplicate;
        }

        private static GameObject InstantiatePrefabInScene(GameObject prefab, Scene targetScene)
        {
            var instance = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
            if (instance == null)
                instance = UnityEngine.Object.Instantiate(prefab);

            if (targetScene.IsValid() && instance.scene != targetScene)
                SceneManager.MoveGameObjectToScene(instance, targetScene);

            return instance;
        }

        private static void CopyAnimatorControllerKeepingAvatar(GameObject previousModel, GameObject newModel)
        {
            var oldAnimator = previousModel.GetComponentInChildren<Animator>(true);
            var newAnimator = newModel.GetComponentInChildren<Animator>(true);
            if (oldAnimator == null || newAnimator == null || oldAnimator.runtimeAnimatorController == null)
                return;

            Undo.RecordObject(newAnimator, "Copy Animator Controller");
            newAnimator.runtimeAnimatorController = oldAnimator.runtimeAnimatorController;
        }

        private static void CopyModelComponents(Transform oldRoot, Transform newRoot)
        {
            var newMap = BuildPathLookup(newRoot);

            foreach (var oldTransform in oldRoot.GetComponentsInChildren<Transform>(true))
            {
                var path = GetRelativePath(oldRoot, oldTransform);
                var targetTransform = FindByPathOrName(newRoot, newMap, path, oldTransform.name);
                if (targetTransform == null)
                    continue;

                CopyComponentsOnGameObject(oldTransform.gameObject, targetTransform.gameObject);
            }
        }

        private static void CopyComponentsOnGameObject(GameObject source, GameObject target)
        {
            var components = source.GetComponents<Component>();
            foreach (var component in components)
            {
                if (component == null || component is Transform || component is Animator)
                    continue;

                var type = component.GetType();
                var existing = target.GetComponent(type);

                ComponentUtility.CopyComponent(component);
                if (existing != null)
                {
                    Undo.RecordObject(existing, $"Copy {type.Name} Values");
                    ComponentUtility.PasteComponentValues(existing);
                }
                else
                {
                    Undo.AddComponent(target, type);
                    var added = target.GetComponent(type);
                    if (added != null)
                        ComponentUtility.PasteComponentValues(added);
                }
            }
        }

        private static void RebindModelReferences(GameObject root, Transform modelRoot)
        {
            var map = BuildTransformLookup(modelRoot);
            var animator = modelRoot.GetComponentInChildren<Animator>(true);

            var items = root.GetComponent<EntityItemManager>();
            if (items != null)
            {
                Undo.RecordObject(items, "Rebind Item/Bone References");
                items.rightHandSlot = ResolveBoneOrName(animator, HumanBodyBones.RightHand, map, "righthand", "hand.r", "right_hand");
                items.leftHandSlot = ResolveBoneOrName(animator, HumanBodyBones.LeftHand, map, "lefthand", "hand.l", "left_hand");
                items.leftHandShieldSlot = ResolveBoneOrName(animator, HumanBodyBones.LeftLowerArm, map, "leftshield", "shield", "forearm.l");
                items.projectileOrigin = ResolveBoneOrName(animator, HumanBodyBones.RightHand, map, "projectile", "muzzle", "cast", "weapon_tip");

                var renderers = modelRoot.GetComponentsInChildren<SkinnedMeshRenderer>(true);
                items.helmRenderer = FindRenderer(renderers, "head", "helm");
                items.chestRenderer = FindRenderer(renderers, "chest", "torso", "body");
                items.pantsRenderer = FindRenderer(renderers, "pant", "legs", "hips");
                items.glovesRenderer = FindRenderer(renderers, "glove", "hand", "arm");
                items.bootsRenderer = FindRenderer(renderers, "boot", "foot", "leg");
            }

            var skills = root.GetComponent<EntitySkillManager>();
            if (skills != null)
            {
                Undo.RecordObject(skills, "Rebind Skill References");
                skills.hands = ResolveBoneOrName(animator, HumanBodyBones.RightHand, map, "hands", "hand_r", "righthand");
            }

            var feedback = root.GetComponent<EntityFeedback>();
            if (feedback != null)
            {
                Undo.RecordObject(feedback, "Rebind Feedback Renderers");
                feedback.meshRenderers = modelRoot.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            }
        }

        private static Dictionary<string, Transform> BuildTransformLookup(Transform root)
        {
            var lookup = new Dictionary<string, Transform>();
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
            {
                var key = Normalize(t.name);
                if (!lookup.ContainsKey(key)) lookup.Add(key, t);
            }
            return lookup;
        }

        private static Dictionary<string, Transform> BuildPathLookup(Transform root)
        {
            var lookup = new Dictionary<string, Transform>();
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
            {
                var path = GetRelativePath(root, t);
                if (!lookup.ContainsKey(path))
                    lookup.Add(path, t);
            }
            return lookup;
        }

        private static Transform FindByPathOrName(Transform newRoot, Dictionary<string, Transform> pathMap, string path, string fallbackName)
        {
            if (pathMap.TryGetValue(path, out var byPath))
                return byPath;

            var normalizedFallback = Normalize(fallbackName);
            return newRoot.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => Normalize(t.name) == normalizedFallback);
        }

        private static string GetRelativePath(Transform root, Transform child)
        {
            if (root == child)
                return string.Empty;

            var names = new List<string>();
            var current = child;
            while (current != null && current != root)
            {
                names.Add(current.name);
                current = current.parent;
            }

            names.Reverse();
            return string.Join("/", names);
        }

        private static Transform ResolveBoneOrName(Animator animator, HumanBodyBones bone, Dictionary<string, Transform> map, params string[] fallbacks)
        {
            if (animator != null && animator.isHuman)
            {
                var boneTransform = animator.GetBoneTransform(bone);
                if (boneTransform != null) return boneTransform;
            }

            foreach (var fallback in fallbacks)
                if (map.TryGetValue(Normalize(fallback), out var result))
                    return result;

            return animator != null ? animator.transform : null;
        }

        private static SkinnedMeshRenderer FindRenderer(SkinnedMeshRenderer[] renderers, params string[] keys)
        {
            foreach (var key in keys)
            {
                var match = renderers.FirstOrDefault(r => Normalize(r.name).Contains(Normalize(key)));
                if (match != null) return match;
            }

            return renderers.FirstOrDefault();
        }

        private static string Normalize(string value) => string.IsNullOrEmpty(value)
            ? string.Empty
            : value.Replace("_", string.Empty).Replace("-", string.Empty).Replace(" ", string.Empty).ToLowerInvariant();
    }
}
