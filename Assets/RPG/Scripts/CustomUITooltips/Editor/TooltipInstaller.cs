using System.IO;
using CustomUITooltips;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UIElements;

namespace CustomUITooltips.Editor
{
    public static class TooltipInstaller
    {
        private const string GeneratedFolder = "Assets/CustomUITooltips/Generated";
        private const string PanelSettingsPath = GeneratedFolder + "/Tooltip Panel Settings.asset";
        private const string ProfilePath = GeneratedFolder + "/Default Tooltip Profile.asset";

        [MenuItem("Tools/Custom Tooltips/Create Runtime Tooltip System")]
        public static void CreateRuntimeTooltipSystem()
        {
            TooltipManager existing = Object.FindFirstObjectByType<TooltipManager>();
            if (existing != null)
            {
                Selection.activeGameObject = existing.gameObject;
                EditorGUIUtility.PingObject(existing.gameObject);
                Debug.Log("A Runtime Tooltip System already exists in this scene.");
                return;
            }

            EnsureGeneratedFolder();
            PanelSettings panelSettings = LoadOrCreatePanelSettings();
            TooltipProfile profile = LoadOrCreateProfile();

            GameObject go = new GameObject("Runtime Tooltip System");
            Undo.RegisterCreatedObjectUndo(go, "Create Runtime Tooltip System");

            UIDocument document = go.AddComponent<UIDocument>();
            document.panelSettings = panelSettings;

            TooltipManager manager = go.AddComponent<TooltipManager>();
            manager.tooltipDocument = document;
            manager.defaultProfile = profile;

            EnsureEventSystemExists();

            Selection.activeGameObject = go;
            EditorSceneManager.MarkSceneDirty(go.scene);
            Debug.Log("Created Runtime Tooltip System. Add Tooltip Trigger components to uGUI elements, or add Tooltip Binder to UIDocuments for UI Toolkit elements.");
        }

        [MenuItem("Tools/Custom Tooltips/Add Tooltip Trigger To Selected uGUI Elements")]
        public static void AddTooltipTriggerToSelection()
        {
            int added = 0;
            foreach (GameObject go in Selection.gameObjects)
            {
                if (go == null || go.GetComponent<RectTransform>() == null)
                    continue;

                TooltipTrigger trigger = go.GetComponent<TooltipTrigger>();
                if (trigger == null)
                {
                    Undo.AddComponent<TooltipTrigger>(go);
                    added++;
                }
            }

            Debug.Log($"Added Tooltip Trigger to {added} selected uGUI element(s). Fill in the Tooltip fields in the Inspector.");
        }

        [MenuItem("Tools/Custom Tooltips/Add UI Toolkit Binder To Selected UIDocument")]
        public static void AddUITKBinderToSelection()
        {
            int added = 0;
            foreach (GameObject go in Selection.gameObjects)
            {
                if (go == null || go.GetComponent<UIDocument>() == null)
                    continue;

                TooltipUITKBinder binder = go.GetComponent<TooltipUITKBinder>();
                if (binder == null)
                {
                    binder = Undo.AddComponent<TooltipUITKBinder>(go);
                    binder.targetDocument = go.GetComponent<UIDocument>();
                    added++;
                }
            }

            Debug.Log($"Added Tooltip Binder to {added} selected UIDocument object(s). Add selector rows such as #PlayButton or .has-tooltip.");
        }

        [MenuItem("GameObject/UI/Custom Tooltip System", false, 10)]
        public static void CreateFromGameObjectMenu()
        {
            CreateRuntimeTooltipSystem();
        }

        private static void EnsureGeneratedFolder()
        {
            if (!AssetDatabase.IsValidFolder("Assets/CustomUITooltips"))
                AssetDatabase.CreateFolder("Assets", "CustomUITooltips");

            if (!AssetDatabase.IsValidFolder(GeneratedFolder))
                AssetDatabase.CreateFolder("Assets/CustomUITooltips", "Generated");
        }

        private static PanelSettings LoadOrCreatePanelSettings()
        {
            PanelSettings settings = AssetDatabase.LoadAssetAtPath<PanelSettings>(PanelSettingsPath);
            if (settings != null)
                return settings;

            settings = ScriptableObject.CreateInstance<PanelSettings>();
            AssetDatabase.CreateAsset(settings, PanelSettingsPath);
            AssetDatabase.SaveAssets();
            return settings;
        }

        private static TooltipProfile LoadOrCreateProfile()
        {
            TooltipProfile profile = AssetDatabase.LoadAssetAtPath<TooltipProfile>(ProfilePath);
            if (profile != null)
                return profile;

            profile = ScriptableObject.CreateInstance<TooltipProfile>();
            AssetDatabase.CreateAsset(profile, ProfilePath);
            AssetDatabase.SaveAssets();
            return profile;
        }

        private static void EnsureEventSystemExists()
        {
            if (Object.FindFirstObjectByType<EventSystem>() != null)
                return;

            GameObject eventSystemGo = new GameObject("EventSystem");
            Undo.RegisterCreatedObjectUndo(eventSystemGo, "Create EventSystem");
            eventSystemGo.AddComponent<EventSystem>();
            eventSystemGo.AddComponent<StandaloneInputModule>();
        }
    }
}
