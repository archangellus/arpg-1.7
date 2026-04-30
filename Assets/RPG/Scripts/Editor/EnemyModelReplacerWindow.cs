using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditorInternal;
using UnityEngine;

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

        [MenuItem(k_menuPath)]
        public static void Open() => GetWindow<EnemyModelReplacerWindow>("Enemy Model Replacer");

        private void OnGUI()
        {
            EditorGUILayout.HelpBox("Drag an enemy and a new model prefab, then click Replace Model & Rebind.", MessageType.Info);

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

            using (new EditorGUI.DisabledScope(enemyRoot == null || newModelPrefab == null))
            {
                if (GUILayout.Button("Replace Model & Rebind", GUILayout.Height(32)))
                    ReplaceModel();
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

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(newModelPrefab);
            if (!instance) instance = Instantiate(newModelPrefab);

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

            EditorSceneManager.MarkSceneDirty(enemyRoot.scene);
            Undo.CollapseUndoOperations(group);

            Debug.Log($"[EnemyModelReplacer] Model replaced and references rebound for '{enemyRoot.name}'.", enemyRoot);
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