using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace PLAYERTWO.ARPGProject
{
    public partial class EnemyModelReplacerWindow
    {
        private bool TryCreateAndAssignAnimatorOverrideController(GameObject root, Transform modelRoot)
        {
            var overrideControllerName = animatorOverrideControllerName?.Trim();
            if (string.IsNullOrEmpty(overrideControllerName))
            {
                Debug.LogWarning("[EnemyModelReplacer] Animator Override Controller skipped because the override controller name is empty.", root);
                return false;
            }

            var entityAnimator = FindEntityAnimatorComponent(root);
            if (entityAnimator == null)
            {
                Debug.LogWarning("[EnemyModelReplacer] Animator Override Controller skipped because no Entity Animator component was found on the enemy root or its children.", root);
                return false;
            }

            if (!TryFindDefaultAnimationsPropertyPath(entityAnimator, out var defaultAnimationsPropertyPath))
            {
                Debug.LogWarning($"[EnemyModelReplacer] Animator Override Controller skipped because the Entity Animator component on '{entityAnimator.gameObject.name}' does not have an assignable Default Animations controller field.", entityAnimator);
                return false;
            }

            var existingOverrideController = FindAnimatorOverrideControllerWithExactName(overrideControllerName, out var existingAssetPath);
            if (existingOverrideController != null)
                return ReuseAndAssignAnimatorOverrideController(root, modelRoot, entityAnimator, defaultAnimationsPropertyPath, existingOverrideController, existingAssetPath);

            var baseController = FindBaseRuntimeAnimatorController(root, modelRoot, entityAnimator, defaultAnimationsPropertyPath);
            if (baseController == null)
            {
                Debug.LogWarning("[EnemyModelReplacer] Animator Override Controller creation skipped because no base Runtime Animator Controller could be found on Entity Animator Default Animations or the new model Animator.", root);
                return false;
            }

            var targetFolder = NormalizeAssetFolder(animatorOverrideControllerFolder);
            if (!EnsureAssetFolderExists(targetFolder))
            {
                Debug.LogWarning($"[EnemyModelReplacer] Animator Override Controller creation skipped because the folder '{targetFolder}' could not be created or found.", root);
                return false;
            }

            var assetName = MakeSafeAssetFileName(overrideControllerName);
            var assetPath = $"{targetFolder}/{assetName}.overrideController";
            var controllerAtPath = AssetDatabase.LoadAssetAtPath<AnimatorOverrideController>(assetPath);
            if (controllerAtPath != null)
                return ReuseAndAssignAnimatorOverrideController(root, modelRoot, entityAnimator, defaultAnimationsPropertyPath, controllerAtPath, assetPath);

            if (!string.IsNullOrEmpty(AssetDatabase.AssetPathToGUID(assetPath)))
            {
                Debug.LogWarning($"[EnemyModelReplacer] Animator Override Controller creation skipped because a non-Animator Override Controller asset already exists at '{assetPath}'.", root);
                return false;
            }

            var overrideController = new AnimatorOverrideController(baseController)
            {
                name = overrideControllerName
            };

            ApplyAnimatorOverrideClipFields(overrideController);

            AssetDatabase.CreateAsset(overrideController, assetPath);
            AssetDatabase.SaveAssets();
            Undo.RegisterCreatedObjectUndo(overrideController, "Create Animator Override Controller");

            if (!AssignOverrideControllerToDefaultAnimations(entityAnimator, defaultAnimationsPropertyPath, overrideController))
            {
                Debug.LogWarning($"[EnemyModelReplacer] Created Animator Override Controller at '{assetPath}', but it could not be assigned to Entity Animator Default Animations.", root);
                return false;
            }

            EditorUtility.FocusProjectWindow();
            Selection.activeObject = overrideController;

            Debug.Log($"[EnemyModelReplacer] Created Animator Override Controller '{overrideControllerName}' at '{assetPath}' and assigned it to Entity Animator Default Animations.", root);
            return true;
        }

        private bool ReuseAndAssignAnimatorOverrideController(GameObject root, Transform modelRoot, Component entityAnimator, string defaultAnimationsPropertyPath, AnimatorOverrideController overrideController, string assetPath)
        {
            if (overrideController == null)
                return false;

            Undo.RecordObject(overrideController, "Reuse Animator Override Controller");

            if (overrideController.runtimeAnimatorController == null)
            {
                var baseController = FindBaseRuntimeAnimatorController(root, modelRoot, entityAnimator, defaultAnimationsPropertyPath);
                if (baseController == null)
                {
                    Debug.LogWarning($"[EnemyModelReplacer] Existing Animator Override Controller '{overrideController.name}' could not be reused because it has no base controller and no base Runtime Animator Controller could be found.", overrideController);
                    return false;
                }

                overrideController.runtimeAnimatorController = baseController;
            }

            ApplyAnimatorOverrideClipFields(overrideController);

            if (!AssignOverrideControllerToDefaultAnimations(entityAnimator, defaultAnimationsPropertyPath, overrideController))
            {
                Debug.LogWarning($"[EnemyModelReplacer] Existing Animator Override Controller '{overrideController.name}' was found but could not be assigned to Entity Animator Default Animations.", root);
                return false;
            }

            EditorUtility.SetDirty(overrideController);
            AssetDatabase.SaveAssets();
            EditorUtility.FocusProjectWindow();
            Selection.activeObject = overrideController;

            var location = string.IsNullOrEmpty(assetPath) ? AssetDatabase.GetAssetPath(overrideController) : assetPath;
            Debug.Log($"[EnemyModelReplacer] Reused existing Animator Override Controller '{overrideController.name}' at '{location}' and assigned it to Entity Animator Default Animations.", root);
            return true;
        }

        private RuntimeAnimatorController PrepareAnimatorOverrideSourceController()
        {
            var detectedSource = DetectAnimatorOverrideSourceController();

            if (animatorOverrideMirrorSource == null && detectedSource != null)
            {
                animatorOverrideMirrorSource = detectedSource;
                RefreshAnimatorOverrideClipFields(animatorOverrideMirrorSource);
            }

            return detectedSource;
        }

        private void DrawAnimatorOverrideActionButtons(RuntimeAnimatorController detectedSource)
        {
            if (DrawLeftIconButton("Use Detected Controller", "AnimatorController Icon", detectedSource != null))
            {
                animatorOverrideMirrorSource = detectedSource;
                RefreshAnimatorOverrideClipFields(animatorOverrideMirrorSource);
            }

            if (DrawLeftIconButton("Refresh Animation Fields", "Refresh", animatorOverrideMirrorSource != null))
                RefreshAnimatorOverrideClipFields(animatorOverrideMirrorSource);

            if (DrawLeftIconButton("Clear Replacements", "TreeEditor.Trash", animatorOverrideMirrorSource != null))
                ClearAnimatorOverrideReplacementClips();
        }

        private void DrawAnimatorOverrideClipFields()
        {
            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("Override Animation Clips", EditorStyles.boldLabel);

            EditorGUI.BeginChangeCheck();
            var selectedSource = (RuntimeAnimatorController)EditorGUILayout.ObjectField(
                "Mirror From Controller",
                animatorOverrideMirrorSource,
                typeof(RuntimeAnimatorController),
                false);

            if (EditorGUI.EndChangeCheck())
            {
                animatorOverrideMirrorSource = selectedSource;
                RefreshAnimatorOverrideClipFields(animatorOverrideMirrorSource);
            }

            if (animatorOverrideMirrorSource == null)
            {
                EditorGUILayout.HelpBox("Assign or detect a Runtime Animator Controller / Animator Override Controller to show override clip fields here. The Entity Animator Default Animations controller is used automatically when available.", MessageType.Info);
                return;
            }

            if (animatorOverrideClipFields == null || animatorOverrideClipFields.Count == 0)
            {
                EditorGUILayout.HelpBox("No animation clips were found on the selected controller.", MessageType.Warning);
                return;
            }

            showAnimatorOverrideClipFields = EditorGUILayout.Foldout(showAnimatorOverrideClipFields, $"Animation Overrides ({animatorOverrideClipFields.Count})", true);
            if (!showAnimatorOverrideClipFields)
                return;

            const float maxScrollHeight = 280f;
            var scrollHeight = Mathf.Min(maxScrollHeight, Mathf.Max(70f, animatorOverrideClipFields.Count * 24f + 8f));

            animatorOverrideClipFieldsScrollPosition = EditorGUILayout.BeginScrollView(animatorOverrideClipFieldsScrollPosition, GUILayout.Height(scrollHeight));

            for (var i = 0; i < animatorOverrideClipFields.Count; i++)
            {
                var field = animatorOverrideClipFields[i];
                if (field == null)
                    continue;

                using (new EditorGUILayout.HorizontalScope())
                {
                    using (new EditorGUI.DisabledScope(true))
                    {
                        EditorGUILayout.ObjectField(
                            GUIContent.none,
                            field.originalClip,
                            typeof(AnimationClip),
                            false,
                            GUILayout.MinWidth(130f));
                    }

                    var label = string.IsNullOrEmpty(field.originalClipName) ? "Override Clip" : field.originalClipName;
                    field.replacementClip = (AnimationClip)EditorGUILayout.ObjectField(
                        new GUIContent(label),
                        field.replacementClip,
                        typeof(AnimationClip),
                        false);
                }
            }

            EditorGUILayout.EndScrollView();
        }


        private RuntimeAnimatorController DetectAnimatorOverrideSourceController()
        {
            if (enemyRoot != null)
            {
                var entityAnimator = FindEntityAnimatorComponent(enemyRoot);
                if (entityAnimator != null && TryFindDefaultAnimationsPropertyPath(entityAnimator, out var defaultAnimationsPropertyPath))
                {
                    var defaultController = GetControllerFromSerializedProperty(entityAnimator, defaultAnimationsPropertyPath);
                    if (defaultController != null)
                        return defaultController;
                }
            }

            var prefabAnimator = newModelPrefab != null ? newModelPrefab.GetComponentInChildren<Animator>(true) : null;
            if (prefabAnimator != null && prefabAnimator.runtimeAnimatorController != null)
                return prefabAnimator.runtimeAnimatorController;

            var rootAnimator = enemyRoot != null ? enemyRoot.GetComponentInChildren<Animator>(true) : null;
            if (rootAnimator != null && rootAnimator.runtimeAnimatorController != null)
                return rootAnimator.runtimeAnimatorController;

            return null;
        }

        private void RefreshAnimatorOverrideClipFields(RuntimeAnimatorController sourceController)
        {
            if (animatorOverrideClipFields == null)
                animatorOverrideClipFields = new List<AnimatorOverrideClipField>();

            animatorOverrideClipFields.Clear();

            if (sourceController == null)
                return;

            foreach (var pair in GetAnimatorOverrideClipPairs(sourceController))
            {
                if (pair.Key == null)
                    continue;

                animatorOverrideClipFields.Add(new AnimatorOverrideClipField
                {
                    originalClipName = pair.Key.name,
                    originalClip = pair.Key,
                    replacementClip = pair.Value != null && pair.Value != pair.Key ? pair.Value : null
                });
            }
        }

        private void ClearAnimatorOverrideReplacementClips()
        {
            if (animatorOverrideClipFields == null)
                return;

            foreach (var field in animatorOverrideClipFields)
            {
                if (field != null)
                    field.replacementClip = null;
            }
        }

        private void ApplyAnimatorOverrideClipFields(AnimatorOverrideController overrideController)
        {
            if (overrideController == null || animatorOverrideClipFields == null || animatorOverrideClipFields.Count == 0)
                return;

            var clipsByOriginalClip = animatorOverrideClipFields
                .Where(field => field != null && field.originalClip != null)
                .GroupBy(field => field.originalClip)
                .ToDictionary(group => group.Key, group => group.Last().replacementClip);

            if (clipsByOriginalClip.Count == 0)
                return;

            var overrides = new List<KeyValuePair<AnimationClip, AnimationClip>>(overrideController.overridesCount);
            overrideController.GetOverrides(overrides);

            for (var i = 0; i < overrides.Count; i++)
            {
                var originalClip = overrides[i].Key;
                if (originalClip != null && clipsByOriginalClip.TryGetValue(originalClip, out var replacementClip))
                    overrides[i] = new KeyValuePair<AnimationClip, AnimationClip>(originalClip, replacementClip != null ? replacementClip : originalClip);
            }

            overrideController.ApplyOverrides(overrides);
            EditorUtility.SetDirty(overrideController);
        }

        private static List<KeyValuePair<AnimationClip, AnimationClip>> GetAnimatorOverrideClipPairs(RuntimeAnimatorController controller)
        {
            var pairs = new List<KeyValuePair<AnimationClip, AnimationClip>>();
            if (controller == null)
                return pairs;

            if (controller is AnimatorOverrideController overrideController)
            {
                overrideController.GetOverrides(pairs);
                return pairs;
            }

            var temporaryOverrideController = new AnimatorOverrideController(controller);
            temporaryOverrideController.GetOverrides(pairs);
            UnityEngine.Object.DestroyImmediate(temporaryOverrideController);
            return pairs;
        }

        private static AnimatorOverrideController FindAnimatorOverrideControllerWithExactName(string controllerName, out string assetPath)
        {
            assetPath = null;

            if (string.IsNullOrWhiteSpace(controllerName))
                return null;

            var guids = AssetDatabase.FindAssets("t:AnimatorOverrideController");
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var controller = AssetDatabase.LoadAssetAtPath<AnimatorOverrideController>(path);
                if (controller == null || !string.Equals(controller.name, controllerName, StringComparison.Ordinal))
                    continue;

                assetPath = path;
                return controller;
            }

            return null;
        }

        private static Component FindEntityAnimatorComponent(GameObject root)
        {
            if (root == null)
                return null;

            return root
                .GetComponentsInChildren<Component>(true)
                .FirstOrDefault(component =>
                {
                    if (component == null)
                        return false;

                    var typeName = component.GetType().Name;
                    return string.Equals(typeName, "EntityAnimator", StringComparison.Ordinal)
                        || string.Equals(ObjectNames.NicifyVariableName(typeName), "Entity Animator", StringComparison.OrdinalIgnoreCase);
                });
        }

        private static bool TryFindDefaultAnimationsPropertyPath(Component entityAnimator, out string propertyPath)
        {
            propertyPath = null;
            if (entityAnimator == null)
                return false;

            var serializedObject = new SerializedObject(entityAnimator);
            serializedObject.Update();

            var directProperty = serializedObject.FindProperty("defaultAnimations");
            if (IsAssignableControllerProperty(directProperty))
            {
                propertyPath = directProperty.propertyPath;
                return true;
            }

            var iterator = serializedObject.GetIterator();
            var enterChildren = true;
            while (iterator.NextVisible(enterChildren))
            {
                enterChildren = false;
                if (iterator.propertyPath == "m_Script")
                    continue;

                var normalizedName = Normalize(iterator.name);
                var normalizedPropertyPath = Normalize(iterator.propertyPath);
                var propertyLooksLikeDefaultAnimations = string.Equals(iterator.displayName, "Default Animations", StringComparison.OrdinalIgnoreCase)
                    || normalizedName.Contains("defaultanimations")
                    || normalizedPropertyPath.Contains("defaultanimations");

                if (propertyLooksLikeDefaultAnimations && IsAssignableControllerProperty(iterator))
                {
                    propertyPath = iterator.propertyPath;
                    return true;
                }
            }

            return false;
        }

        private static bool IsAssignableControllerProperty(SerializedProperty property)
        {
            if (property == null || property.propertyType != SerializedPropertyType.ObjectReference)
                return false;

            if (property.objectReferenceValue is RuntimeAnimatorController)
                return true;

            var propertyType = property.type ?? string.Empty;
            return propertyType.Contains("RuntimeAnimatorController")
                || propertyType.Contains("AnimatorOverrideController")
                || propertyType.Contains("AnimatorController");
        }

        private static RuntimeAnimatorController FindBaseRuntimeAnimatorController(GameObject root, Transform modelRoot, Component entityAnimator, string defaultAnimationsPropertyPath)
        {
            var defaultController = GetControllerFromSerializedProperty(entityAnimator, defaultAnimationsPropertyPath);
            if (defaultController != null)
                return UnwrapAnimatorOverrideController(defaultController);

            var modelAnimator = modelRoot != null ? modelRoot.GetComponentInChildren<Animator>(true) : null;
            if (modelAnimator != null && modelAnimator.runtimeAnimatorController != null)
                return UnwrapAnimatorOverrideController(modelAnimator.runtimeAnimatorController);

            var rootAnimator = root != null ? root.GetComponentInChildren<Animator>(true) : null;
            if (rootAnimator != null && rootAnimator.runtimeAnimatorController != null)
                return UnwrapAnimatorOverrideController(rootAnimator.runtimeAnimatorController);

            return null;
        }

        private static RuntimeAnimatorController GetControllerFromSerializedProperty(Component component, string propertyPath)
        {
            if (component == null || string.IsNullOrEmpty(propertyPath))
                return null;

            var serializedObject = new SerializedObject(component);
            serializedObject.Update();

            var property = serializedObject.FindProperty(propertyPath);
            return property != null ? property.objectReferenceValue as RuntimeAnimatorController : null;
        }

        private static RuntimeAnimatorController UnwrapAnimatorOverrideController(RuntimeAnimatorController controller)
        {
            while (controller is AnimatorOverrideController overrideController && overrideController.runtimeAnimatorController != null)
                controller = overrideController.runtimeAnimatorController;

            return controller;
        }

        private static bool AssignOverrideControllerToDefaultAnimations(Component entityAnimator, string defaultAnimationsPropertyPath, AnimatorOverrideController overrideController)
        {
            if (entityAnimator == null || string.IsNullOrEmpty(defaultAnimationsPropertyPath) || overrideController == null)
                return false;

            var serializedObject = new SerializedObject(entityAnimator);
            serializedObject.Update();

            var property = serializedObject.FindProperty(defaultAnimationsPropertyPath);
            if (!IsAssignableControllerProperty(property))
                return false;

            Undo.RecordObject(entityAnimator, "Assign Animator Override Controller");
            property.objectReferenceValue = overrideController;
            serializedObject.ApplyModifiedProperties();
            MarkObjectDirtyAndRecordPrefab(entityAnimator);
            return true;
        }

    }
}
