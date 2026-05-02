using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace PLAYERTWO.ARPGProject
{
    public partial class EnemyModelReplacerWindow
    {
        private static string NormalizeAssetFolder(string folder)
        {
            if (string.IsNullOrWhiteSpace(folder))
                return "Assets";

            folder = folder.Trim().Replace('\\', '/').TrimEnd('/');
            return folder.StartsWith("Assets", StringComparison.Ordinal) ? folder : "Assets";
        }

        private static bool EnsureAssetFolderExists(string folder)
        {
            if (string.IsNullOrEmpty(folder) || !folder.StartsWith("Assets", StringComparison.Ordinal))
                return false;

            if (AssetDatabase.IsValidFolder(folder))
                return true;

            var parts = folder.Split('/');
            if (parts.Length == 0 || parts[0] != "Assets")
                return false;

            var current = "Assets";
            for (var i = 1; i < parts.Length; i++)
            {
                if (string.IsNullOrWhiteSpace(parts[i]))
                    continue;

                var next = $"{current}/{parts[i]}";
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, parts[i]);

                current = next;
            }

            return AssetDatabase.IsValidFolder(folder);
        }

        private static string MakeSafeAssetFileName(string assetName)
        {
            if (string.IsNullOrWhiteSpace(assetName))
                return "Animator Override Controller";

            var invalidChars = Path.GetInvalidFileNameChars();
            var safeName = new string(assetName.Select(character => invalidChars.Contains(character) ? '_' : character).ToArray());
            return string.IsNullOrWhiteSpace(safeName) ? "Animator Override Controller" : safeName.Trim();
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
                if (ShouldSkipModelCopyComponent(component))
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

        private static bool ShouldSkipModelCopyComponent(Component component)
        {
            if (component == null)
                return true;

            return component is Transform
                || component is Animator
                || component is Renderer
                || component is MeshFilter
                || component is LODGroup
                || component is Cloth;
        }

        private static void RebindModelReferences(GameObject root, Transform modelRoot, Transform previousModelRoot = null, bool previousModelWillBeRemoved = false)
        {
            var map = BuildTransformLookup(modelRoot);
            var animator = modelRoot.GetComponentInChildren<Animator>(true);
            var skinnedRenderers = GetNewModelSkinnedMeshRenderers(modelRoot);

            var items = root.GetComponent<EntityItemManager>();
            if (items != null)
            {
                Undo.RecordObject(items, "Rebind Item/Bone References");
                items.rightHandSlot = ResolveBoneOrName(animator, HumanBodyBones.RightHand, map, "righthand", "hand.r", "right_hand");
                items.leftHandSlot = ResolveBoneOrName(animator, HumanBodyBones.LeftHand, map, "lefthand", "hand.l", "left_hand");
                items.leftHandShieldSlot = ResolveBoneOrName(animator, HumanBodyBones.LeftLowerArm, map, "leftshield", "shield", "forearm.l");
                items.projectileOrigin = ResolveBoneOrName(animator, HumanBodyBones.RightHand, map, "projectile", "muzzle", "cast", "weapon_tip");

                items.helmRenderer = FindRenderer(skinnedRenderers, "head", "helm");
                items.chestRenderer = FindRenderer(skinnedRenderers, "chest", "torso", "body");
                items.pantsRenderer = FindRenderer(skinnedRenderers, "pant", "legs", "hips");
                items.glovesRenderer = FindRenderer(skinnedRenderers, "glove", "hand", "arm");
                items.bootsRenderer = FindRenderer(skinnedRenderers, "boot", "foot", "leg");

                MarkObjectDirtyAndRecordPrefab(items);
            }

            var skills = root.GetComponent<EntitySkillManager>();
            if (skills != null)
            {
                Undo.RecordObject(skills, "Rebind Skill References");
                skills.hands = ResolveSkillHandsTransform(root, modelRoot, previousModelRoot, previousModelWillBeRemoved, animator, map);
                MarkObjectDirtyAndRecordPrefab(skills);
            }

            RebindEntityFeedbackRenderers(root, skinnedRenderers);
        }


        private static Transform ResolveSkillHandsTransform(
            GameObject root,
            Transform modelRoot,
            Transform previousModelRoot,
            bool previousModelWillBeRemoved,
            Animator animator,
            Dictionary<string, Transform> modelMap)
        {
            var previousHands = FindCastingOriginsHands(previousModelRoot);
            if (previousHands != null)
            {
                if (!previousModelWillBeRemoved)
                    return previousHands;

                var existingModelHands = FindCastingOriginsHands(modelRoot);
                if (existingModelHands != null)
                    return existingModelHands;

                var preservedHands = CloneCastingOriginsForSkillHands(previousHands, modelRoot);
                if (preservedHands != null)
                    return preservedHands;
            }

            var rootHands = FindCastingOriginsHands(root != null ? root.transform : null, modelRoot);
            if (rootHands != null)
                return rootHands;

            var modelHands = FindCastingOriginsHands(modelRoot);
            if (modelHands != null)
                return modelHands;

            if (modelMap != null)
            {
                if (modelMap.TryGetValue(Normalize("hands"), out var handsByName))
                    return handsByName;

                if (modelMap.TryGetValue(Normalize("hand_r"), out var rightHandByName))
                    return rightHandByName;

                if (modelMap.TryGetValue(Normalize("righthand"), out var rightHandCompactByName))
                    return rightHandCompactByName;
            }

            return ResolveBoneOrName(animator, HumanBodyBones.RightHand, modelMap, "hands", "hand_r", "righthand");
        }

        private static Transform CloneCastingOriginsForSkillHands(Transform previousHands, Transform modelRoot)
        {
            if (previousHands == null || modelRoot == null)
                return null;

            var castingOrigins = previousHands.parent;
            if (castingOrigins == null || Normalize(castingOrigins.name) != "castingorigins")
                return null;

            var clone = UnityEngine.Object.Instantiate(castingOrigins.gameObject);
            clone.name = castingOrigins.name;
            clone.transform.SetParent(modelRoot, true);
            Undo.RegisterCreatedObjectUndo(clone, "Preserve Casting Origins");

            return FindCastingOriginsHands(clone.transform) ?? clone.transform.Find(previousHands.name);
        }

        private static Transform FindCastingOriginsHands(Transform searchRoot, Transform excludedRoot = null)
        {
            if (searchRoot == null)
                return null;

            foreach (var transform in searchRoot.GetComponentsInChildren<Transform>(true))
            {
                if (excludedRoot != null && IsSameOrChildOf(transform, excludedRoot))
                    continue;

                if (Normalize(transform.name) != "hands")
                    continue;

                var parent = transform.parent;
                if (parent != null && Normalize(parent.name) == "castingorigins")
                    return transform;
            }

            return null;
        }

        private static bool IsSameOrChildOf(Transform transform, Transform possibleParent)
        {
            var current = transform;
            while (current != null)
            {
                if (current == possibleParent)
                    return true;

                current = current.parent;
            }

            return false;
        }

        private static SkinnedMeshRenderer[] GetNewModelSkinnedMeshRenderers(Transform modelRoot)
        {
            if (modelRoot == null)
                return new SkinnedMeshRenderer[0];

            var renderersWithMeshes = modelRoot
                .GetComponentsInChildren<SkinnedMeshRenderer>(true)
                .Where(renderer => renderer != null && renderer.sharedMesh != null)
                .ToArray();

            if (renderersWithMeshes.Length > 0)
                return renderersWithMeshes;

            return modelRoot
                .GetComponentsInChildren<SkinnedMeshRenderer>(true)
                .Where(renderer => renderer != null)
                .ToArray();
        }

        private static void RebindEntityFeedbackRenderers(GameObject root, SkinnedMeshRenderer[] newModelRenderers)
        {
            if (root == null || newModelRenderers == null || newModelRenderers.Length == 0)
                return;

            var feedbackComponents = root.GetComponentsInChildren<EntityFeedback>(true);
            foreach (var feedback in feedbackComponents)
            {
                if (feedback == null)
                    continue;

                Undo.RecordObject(feedback, "Rebind Feedback Renderers");
                feedback.meshRenderers = newModelRenderers;
                MarkObjectDirtyAndRecordPrefab(feedback);
            }
        }

        private static void MarkObjectDirtyAndRecordPrefab(UnityEngine.Object target)
        {
            if (target == null)
                return;

            EditorUtility.SetDirty(target);

            if (PrefabUtility.IsPartOfPrefabInstance(target))
                PrefabUtility.RecordPrefabInstancePropertyModifications(target);
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
