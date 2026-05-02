using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace PLAYERTWO.ARPGProject
{
    public partial class EnemyModelReplacerWindow
    {
        private class ProfileSelectionWindow : EditorWindow
        {
            private EnemyModelReplacerWindow owner;
            private string searchedFolder;
            private List<EnemyModelReplacerProfile> profiles = new List<EnemyModelReplacerProfile>();
            private Vector2 scrollPosition;

            public static void Open(EnemyModelReplacerWindow owner, string searchedFolder, List<EnemyModelReplacerProfile> profiles)
            {
                var window = CreateInstance<ProfileSelectionWindow>();
                window.owner = owner;
                window.searchedFolder = searchedFolder;
                window.profiles = profiles ?? new List<EnemyModelReplacerProfile>();
                window.titleContent = new GUIContent("Load Profile");
                window.minSize = new Vector2(440f, 260f);
                window.ShowUtility();
            }

            private void OnGUI()
            {
                EditorGUILayout.LabelField("Enemy Model Replacer Profiles", EditorStyles.boldLabel);
                EditorGUILayout.LabelField("Folder", string.IsNullOrWhiteSpace(searchedFolder) ? "Assets" : searchedFolder);
                EditorGUILayout.Space(4);

                if (owner == null)
                {
                    EditorGUILayout.HelpBox("The Enemy Model Replacer window was closed. Reopen it and try again.", MessageType.Warning);
                    if (GUILayout.Button("Close"))
                        Close();
                    return;
                }

                if (profiles == null || profiles.Count == 0)
                {
                    EditorGUILayout.HelpBox("No Enemy Model Replacer profiles were found in this folder. Check the Profile Folder path or save a new profile there first.", MessageType.Info);

                    using (new EditorGUILayout.HorizontalScope())
                    {
                        if (GUILayout.Button("Refresh"))
                            profiles = owner.FindProfilesInProfileFolder(owner.profileFolder);

                        if (GUILayout.Button("Close"))
                            Close();
                    }

                    return;
                }

                scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

                foreach (var profile in profiles)
                {
                    if (profile == null)
                        continue;

                    using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                    {
                        EditorGUILayout.LabelField(profile.name, EditorStyles.boldLabel);
                        EditorGUILayout.LabelField(AssetDatabase.GetAssetPath(profile), EditorStyles.miniLabel);

                        using (new EditorGUILayout.HorizontalScope())
                        {
                            if (GUILayout.Button("Load"))
                            {
                                owner.SelectAndLoadProfile(profile);
                                Close();
                                GUIUtility.ExitGUI();
                            }

                            if (GUILayout.Button("Ping"))
                            {
                                EditorUtility.FocusProjectWindow();
                                EditorGUIUtility.PingObject(profile);
                                Selection.activeObject = profile;
                            }
                        }
                    }
                }

                EditorGUILayout.EndScrollView();

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Refresh"))
                        profiles = owner.FindProfilesInProfileFolder(owner.profileFolder);

                    if (GUILayout.Button("Close"))
                        Close();
                }
            }
        }

        private void DrawProfileControls()
        {
            EditorGUILayout.Space(4);

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                var normalizedProfileFolder = NormalizeAssetFolder(profileFolder);
                var folderExists = AssetDatabase.IsValidFolder(normalizedProfileFolder);
                var profilesInFolder = FindProfilesInProfileFolder(profileFolder);

                BeginTwoColumnPanel("Action Buttons");

                if (DrawLeftIconButton($"Load Profile ({profilesInFolder.Count})", "Folder Icon"))
                    ShowProfileSelectionWindow(profilesInFolder);

                if (DrawLeftIconButton("Save Profile", "SaveAs", selectedProfile != null))
                    SaveSelectedProfile();

                if (DrawLeftIconButton("Save As New", "ScriptableObject Icon"))
                    SaveAsNewProfile();

                if (DrawLeftIconButton("Reload Selected", "Refresh", selectedProfile != null))
                    LoadSelectedProfile();

                if (DrawLeftIconButton("Ping Profile", "ViewToolZoom", selectedProfile != null))
                {
                    EditorUtility.FocusProjectWindow();
                    EditorGUIUtility.PingObject(selectedProfile);
                    Selection.activeObject = selectedProfile;
                }

                if (DrawLeftIconButton("Use Selection As Profile", "FilterByType"))
                {
                    var selectedAsset = Selection.activeObject as EnemyModelReplacerProfile;
                    if (selectedAsset != null)
                        selectedProfile = selectedAsset;
                    else
                        Debug.LogWarning("[EnemyModelReplacer] Select an Enemy Model Replacer Profile asset in the Project window first.");
                }

                SwitchToTwoColumnContent("Profile Settings");

                selectedProfile = (EnemyModelReplacerProfile)EditorGUILayout.ObjectField(
                    "Selected Profile",
                    selectedProfile,
                    typeof(EnemyModelReplacerProfile),
                    false);

                newProfileName = EditorGUILayout.TextField("New Profile Name", newProfileName);
                profileFolder = EditorGUILayout.TextField("Profile Folder", profileFolder);

                if (folderExists)
                    EditorGUILayout.HelpBox($"Found {profilesInFolder.Count} Enemy Model Replacer profile(s) in '{normalizedProfileFolder}'.", MessageType.None);
                else
                    EditorGUILayout.HelpBox($"Profile folder '{normalizedProfileFolder}' does not exist. Save As New will create it, but Load Profile cannot find profiles there yet.", MessageType.Warning);

                EditorGUILayout.HelpBox("Profiles save and load all window settings. Load Profile scans the Profile Folder and lets you choose a profile directly. Scene objects can only be restored when their scene is currently loaded.", MessageType.None);

                EndTwoColumnPanel();
            }
        }


        private void ShowProfileSelectionWindow(List<EnemyModelReplacerProfile> profilesInFolder)
        {
            var normalizedProfileFolder = NormalizeAssetFolder(profileFolder);
            ProfileSelectionWindow.Open(this, normalizedProfileFolder, profilesInFolder);
        }

        private List<EnemyModelReplacerProfile> FindProfilesInProfileFolder(string folder)
        {
            var normalizedFolder = NormalizeAssetFolder(folder);
            if (!AssetDatabase.IsValidFolder(normalizedFolder))
                return new List<EnemyModelReplacerProfile>();

            return AssetDatabase
                .FindAssets("t:EnemyModelReplacerProfile", new[] { normalizedFolder })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<EnemyModelReplacerProfile>)
                .Where(profile => profile != null)
                .OrderBy(profile => profile.name, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private void SelectAndLoadProfile(EnemyModelReplacerProfile profile)
        {
            if (profile == null)
                return;

            selectedProfile = profile;
            LoadSelectedProfile();
        }

        private void SaveAsNewProfile()
        {
            var targetFolder = NormalizeAssetFolder(profileFolder);
            if (!EnsureAssetFolderExists(targetFolder))
            {
                Debug.LogWarning($"[EnemyModelReplacer] Profile was not saved because the folder '{targetFolder}' could not be created or found.");
                return;
            }

            var safeName = MakeSafeAssetFileName(string.IsNullOrWhiteSpace(newProfileName) ? "Enemy Model Replacer Profile" : newProfileName);
            var assetPath = AssetDatabase.GenerateUniqueAssetPath($"{targetFolder}/{safeName}.asset");

            var profile = CreateInstance<EnemyModelReplacerProfile>();
            CaptureProfile(profile);

            AssetDatabase.CreateAsset(profile, assetPath);
            AssetDatabase.SaveAssets();

            selectedProfile = profile;

            EditorUtility.FocusProjectWindow();
            Selection.activeObject = profile;
            EditorGUIUtility.PingObject(profile);

            Debug.Log($"[EnemyModelReplacer] Saved profile at '{assetPath}'.", profile);
        }

        private void SaveSelectedProfile()
        {
            if (selectedProfile == null)
                return;

            Undo.RecordObject(selectedProfile, "Save Enemy Model Replacer Profile");
            CaptureProfile(selectedProfile);
            EditorUtility.SetDirty(selectedProfile);
            AssetDatabase.SaveAssets();

            Debug.Log($"[EnemyModelReplacer] Saved profile '{selectedProfile.name}'.", selectedProfile);
        }

        private void LoadSelectedProfile()
        {
            if (selectedProfile == null)
                return;

            ApplyProfile(selectedProfile);
            SceneView.RepaintAll();
            Repaint();

            Debug.Log($"[EnemyModelReplacer] Loaded profile '{selectedProfile.name}'.", selectedProfile);
        }

        private void CaptureProfile(EnemyModelReplacerProfile profile)
        {
            if (profile == null)
                return;

            profile.enemyRootGlobalId = GetObjectGlobalId(enemyRoot);
            profile.newModelPrefabGlobalId = GetObjectGlobalId(newModelPrefab);

            profile.windowScrollPosition = windowScrollPosition;
            profile.selectedTab = (int)selectedTab;
            profile.previewYaw = previewYaw;
            profile.modelContainerName = modelContainerName;
            profile.autoApplyToChildren = autoApplyToChildren;
            profile.removePreviousModel = removePreviousModel;

            profile.forceNewModelY = forceNewModelY;
            profile.forcedNewModelLocalY = forcedNewModelLocalY;

            profile.createAnimatorOverrideController = createAnimatorOverrideController;
            profile.animatorOverrideControllerName = animatorOverrideControllerName;
            profile.animatorOverrideControllerFolder = animatorOverrideControllerFolder;
            profile.animatorOverrideMirrorSourceGlobalId = GetObjectGlobalId(animatorOverrideMirrorSource);
            profile.showAnimatorOverrideClipFields = showAnimatorOverrideClipFields;
            profile.animatorOverrideClipFieldsScrollPosition = animatorOverrideClipFieldsScrollPosition;

            if (profile.animatorOverrideClipFields == null)
                profile.animatorOverrideClipFields = new List<EnemyModelReplacerProfileClipField>();
            else
                profile.animatorOverrideClipFields.Clear();

            if (animatorOverrideClipFields != null)
            {
                foreach (var field in animatorOverrideClipFields)
                {
                    if (field == null)
                        continue;

                    profile.animatorOverrideClipFields.Add(new EnemyModelReplacerProfileClipField
                    {
                        originalClipName = field.originalClipName,
                        originalClipGlobalId = GetObjectGlobalId(field.originalClip),
                        replacementClipGlobalId = GetObjectGlobalId(field.replacementClip)
                    });
                }
            }

            profile.createStandaloneDuplicates = createStandaloneDuplicates;
            profile.duplicateCount = duplicateCount;
            profile.duplicateNamePrefix = duplicateNamePrefix;
            profile.duplicateMinRadius = duplicateMinRadius;
            profile.duplicateMaxRadius = duplicateMaxRadius;
            profile.duplicateMinSpacing = duplicateMinSpacing;
            profile.duplicatePlacementAttempts = duplicatePlacementAttempts;

            profile.avoidSpawnCollisionLayers = avoidSpawnCollisionLayers;
            profile.blockedSpawnLayers = blockedSpawnLayers.value;
            profile.spawnCollisionCheckRadius = spawnCollisionCheckRadius;
            profile.spawnCollisionCheckYOffset = spawnCollisionCheckYOffset;
            profile.spawnCollisionTriggerInteraction = spawnCollisionTriggerInteraction;

            profile.duplicateRangeGizmoYOffset = duplicateRangeGizmoYOffset;
        }

        private void ApplyProfile(EnemyModelReplacerProfile profile)
        {
            if (profile == null)
                return;

            var resolvedEnemyRoot = ResolveObjectGlobalId<GameObject>(profile.enemyRootGlobalId);
            enemyRoot = IsSceneGameObject(resolvedEnemyRoot) ? resolvedEnemyRoot : null;
            newModelPrefab = ResolveObjectGlobalId<GameObject>(profile.newModelPrefabGlobalId);

            windowScrollPosition = profile.windowScrollPosition;
            selectedTab = (EnemyModelReplacerTab)Mathf.Clamp(profile.selectedTab, 0, 4);
            previewYaw = profile.previewYaw;
            modelContainerName = profile.modelContainerName;
            autoApplyToChildren = profile.autoApplyToChildren;
            removePreviousModel = profile.removePreviousModel;

            forceNewModelY = profile.forceNewModelY;
            forcedNewModelLocalY = profile.forcedNewModelLocalY;

            createAnimatorOverrideController = profile.createAnimatorOverrideController;
            animatorOverrideControllerName = profile.animatorOverrideControllerName;
            animatorOverrideControllerFolder = profile.animatorOverrideControllerFolder;
            animatorOverrideMirrorSource = ResolveObjectGlobalId<RuntimeAnimatorController>(profile.animatorOverrideMirrorSourceGlobalId);
            showAnimatorOverrideClipFields = profile.showAnimatorOverrideClipFields;
            animatorOverrideClipFieldsScrollPosition = profile.animatorOverrideClipFieldsScrollPosition;

            if (animatorOverrideClipFields == null)
                animatorOverrideClipFields = new List<AnimatorOverrideClipField>();
            else
                animatorOverrideClipFields.Clear();

            if (profile.animatorOverrideClipFields != null)
            {
                foreach (var profileField in profile.animatorOverrideClipFields)
                {
                    if (profileField == null)
                        continue;

                    animatorOverrideClipFields.Add(new AnimatorOverrideClipField
                    {
                        originalClipName = profileField.originalClipName,
                        originalClip = ResolveObjectGlobalId<AnimationClip>(profileField.originalClipGlobalId),
                        replacementClip = ResolveObjectGlobalId<AnimationClip>(profileField.replacementClipGlobalId)
                    });
                }
            }

            createStandaloneDuplicates = profile.createStandaloneDuplicates;
            duplicateCount = Mathf.Max(0, profile.duplicateCount);
            duplicateNamePrefix = profile.duplicateNamePrefix;
            duplicateMinRadius = Mathf.Max(0f, profile.duplicateMinRadius);
            duplicateMaxRadius = Mathf.Max(duplicateMinRadius, profile.duplicateMaxRadius);
            duplicateMinSpacing = Mathf.Max(0f, profile.duplicateMinSpacing);
            duplicatePlacementAttempts = Mathf.Max(1, profile.duplicatePlacementAttempts);

            avoidSpawnCollisionLayers = profile.avoidSpawnCollisionLayers;
            blockedSpawnLayers = profile.blockedSpawnLayers;
            spawnCollisionCheckRadius = Mathf.Max(0.01f, profile.spawnCollisionCheckRadius);
            spawnCollisionCheckYOffset = profile.spawnCollisionCheckYOffset;
            spawnCollisionTriggerInteraction = profile.spawnCollisionTriggerInteraction;

            duplicateRangeGizmoYOffset = profile.duplicateRangeGizmoYOffset;

            WarnIfProfileReferenceMissing(profile.enemyRootGlobalId, enemyRoot, "Enemy Root");
            WarnIfProfileReferenceMissing(profile.newModelPrefabGlobalId, newModelPrefab, "New Model");
            WarnIfProfileReferenceMissing(profile.animatorOverrideMirrorSourceGlobalId, animatorOverrideMirrorSource, "Mirror From Controller");
        }

        private static string GetObjectGlobalId(UnityEngine.Object target)
        {
            if (target == null)
                return string.Empty;

            try
            {
                return GlobalObjectId.GetGlobalObjectIdSlow(target).ToString();
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[EnemyModelReplacer] Could not save a reference to '{target.name}': {exception.Message}", target);
                return string.Empty;
            }
        }

        private static T ResolveObjectGlobalId<T>(string globalId) where T : UnityEngine.Object
        {
            if (string.IsNullOrWhiteSpace(globalId))
                return null;

            if (!GlobalObjectId.TryParse(globalId, out var parsedId))
                return null;

            return GlobalObjectId.GlobalObjectIdentifierToObjectSlow(parsedId) as T;
        }

        private static void WarnIfProfileReferenceMissing(string globalId, UnityEngine.Object resolvedObject, string label)
        {
            if (!string.IsNullOrWhiteSpace(globalId) && resolvedObject == null)
                Debug.LogWarning($"[EnemyModelReplacer] Profile reference '{label}' could not be restored. If this is a scene object, open the scene that contains it and load the profile again.");
        }

    }
}
