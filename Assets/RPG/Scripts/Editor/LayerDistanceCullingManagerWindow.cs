#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public class LayerDistanceCullingManagerWindow : EditorWindow
{
    private Vector2 scroll;
    private LayerDistanceCullingManager manager;
    private SerializedObject serializedManager;

    [MenuItem("Tools/LayerDistanceCullingManager")]
    public static void OpenWindow()
    {
        LayerDistanceCullingManagerWindow window = GetWindow<LayerDistanceCullingManagerWindow>();
        window.titleContent = new GUIContent("Layer Distance Culling");
        window.minSize = new Vector2(480f, 560f);
        window.Show();
    }

    private void OnEnable()
    {
        FindOrCreateManager();
    }

    private void OnHierarchyChange()
    {
        if (manager == null)
            FindOrCreateManager();

        Repaint();
    }

    private void FindOrCreateManager()
    {
#if UNITY_2023_1_OR_NEWER
        manager = FindFirstObjectByType<LayerDistanceCullingManager>();
#else
        manager = FindObjectOfType<LayerDistanceCullingManager>();
#endif

        if (manager == null)
        {
            GameObject go = new GameObject("LayerDistanceCullingManager");
            Undo.RegisterCreatedObjectUndo(go, "Create LayerDistanceCullingManager");
            manager = go.AddComponent<LayerDistanceCullingManager>();
            EditorSceneManager.MarkSceneDirty(go.scene);
        }

        serializedManager = new SerializedObject(manager);
    }

    private void OnGUI()
    {
        if (manager == null || serializedManager == null)
            FindOrCreateManager();

        serializedManager.Update();

        DrawHeader();
        DrawCameraInfo();
        DrawSearchRoots();
        DrawRules();
        DrawRuntimeSettings();
        DrawActions();

        serializedManager.ApplyModifiedProperties();
    }

    private void DrawHeader()
    {
        EditorGUILayout.Space(8);
        EditorGUILayout.LabelField("Layer Distance Culling Manager", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "LODGroup-aware culling. Uses Camera.main automatically. For your top-down game, PlanarXZ is usually the best mode.",
            MessageType.Info);

        EditorGUILayout.ObjectField("Scene Manager", manager, typeof(LayerDistanceCullingManager), true);
        EditorGUILayout.LabelField("Cached Entries", manager.CachedEntryCount.ToString());

        string duplicateWarning = manager.GetDuplicateLayerWarning();
        if (!string.IsNullOrEmpty(duplicateWarning))
            EditorGUILayout.HelpBox(duplicateWarning, MessageType.Warning);

        EditorGUILayout.Space(6);
    }

    private void DrawCameraInfo()
    {
        Camera mainCam = Camera.main;

        using (new EditorGUILayout.VerticalScope("box"))
        {
            EditorGUILayout.LabelField("Camera", EditorStyles.boldLabel);

            if (mainCam == null)
            {
                EditorGUILayout.HelpBox(
                    "No enabled Main Camera found. Tag the real Unity Camera that has CinemachineBrain as MainCamera.",
                    MessageType.Error);
            }
            else
            {
                EditorGUILayout.ObjectField("Main Camera", mainCam, typeof(Camera), true);
            }
        }

        EditorGUILayout.Space(4);
    }

    private void DrawSearchRoots()
    {
        SerializedProperty searchRootsProp = serializedManager.FindProperty("searchRoots");

        using (new EditorGUILayout.VerticalScope("box"))
        {
            EditorGUILayout.LabelField("Search Roots", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Only LODGroups/renderers under these roots will be considered. Leave empty to scan the whole scene.",
                MessageType.None);

            EditorGUILayout.PropertyField(searchRootsProp, true);
        }

        EditorGUILayout.Space(4);
    }

    private void DrawRules()
    {
        SerializedProperty rulesProp = serializedManager.FindProperty("rules");

        using (new EditorGUILayout.VerticalScope("box"))
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Rules", EditorStyles.boldLabel);

            if (GUILayout.Button("Add Rule", GUILayout.Width(90f)))
            {
                int index = rulesProp.arraySize;
                rulesProp.InsertArrayElementAtIndex(index);

                SerializedProperty element = rulesProp.GetArrayElementAtIndex(index);
                element.FindPropertyRelative("label").stringValue = $"Rule {index + 1}";
                element.FindPropertyRelative("enabled").boolValue = true;
                element.FindPropertyRelative("layers").intValue = 0;
                element.FindPropertyRelative("maxDistance").floatValue = 20f;
            }

            EditorGUILayout.EndHorizontal();
            EditorGUILayout.Space(4);

            scroll = EditorGUILayout.BeginScrollView(scroll, GUILayout.MinHeight(220f));

            for (int i = 0; i < rulesProp.arraySize; i++)
            {
                SerializedProperty rule = rulesProp.GetArrayElementAtIndex(i);
                SerializedProperty labelProp = rule.FindPropertyRelative("label");
                SerializedProperty enabledProp = rule.FindPropertyRelative("enabled");
                SerializedProperty layersProp = rule.FindPropertyRelative("layers");
                SerializedProperty maxDistanceProp = rule.FindPropertyRelative("maxDistance");

                using (new EditorGUILayout.VerticalScope("box"))
                {
                    EditorGUILayout.BeginHorizontal();

                    enabledProp.boolValue = EditorGUILayout.Toggle(enabledProp.boolValue, GUILayout.Width(18f));

                    string title = string.IsNullOrWhiteSpace(labelProp.stringValue)
                        ? $"Rule {i + 1}"
                        : labelProp.stringValue;

                    EditorGUILayout.LabelField(title, EditorStyles.boldLabel);

                    GUI.enabled = i > 0;
                    if (GUILayout.Button("↑", GUILayout.Width(28f)))
                        rulesProp.MoveArrayElement(i, i - 1);

                    GUI.enabled = i < rulesProp.arraySize - 1;
                    if (GUILayout.Button("↓", GUILayout.Width(28f)))
                        rulesProp.MoveArrayElement(i, i + 1);

                    GUI.enabled = true;
                    if (GUILayout.Button("X", GUILayout.Width(28f)))
                    {
                        rulesProp.DeleteArrayElementAtIndex(i);
                        break;
                    }

                    EditorGUILayout.EndHorizontal();

                    EditorGUILayout.PropertyField(labelProp);
                    EditorGUILayout.PropertyField(layersProp);
                    EditorGUILayout.PropertyField(maxDistanceProp, new GUIContent("Max Distance"));
                }
            }

            EditorGUILayout.EndScrollView();
        }

        EditorGUILayout.Space(4);
    }

    private void DrawRuntimeSettings()
    {
        SerializedProperty refreshIntervalProp = serializedManager.FindProperty("refreshInterval");
        SerializedProperty distanceModeProp = serializedManager.FindProperty("distanceMode");
        SerializedProperty useClosestPointProp = serializedManager.FindProperty("useClosestPointOnBoundsForStandaloneRenderers");
        SerializedProperty autoRebuildProp = serializedManager.FindProperty("autoRebuildOnEnable");
        SerializedProperty includeInactiveProp = serializedManager.FindProperty("includeInactiveWhenBuildingCache");

        using (new EditorGUILayout.VerticalScope("box"))
        {
            EditorGUILayout.LabelField("Runtime Settings", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(refreshIntervalProp);
            EditorGUILayout.PropertyField(distanceModeProp);
            EditorGUILayout.PropertyField(useClosestPointProp, new GUIContent("Use Closest Point On Bounds (Standalone Only)"));
            EditorGUILayout.PropertyField(autoRebuildProp);
            EditorGUILayout.PropertyField(includeInactiveProp, new GUIContent("Include Inactive When Building Cache"));
        }

        EditorGUILayout.Space(4);
    }

    private void DrawActions()
    {
        using (new EditorGUILayout.VerticalScope("box"))
        {
            EditorGUILayout.LabelField("Actions", EditorStyles.boldLabel);

            EditorGUILayout.BeginHorizontal();

            if (GUILayout.Button("Rebuild Cache", GUILayout.Height(28f)))
            {
                serializedManager.ApplyModifiedProperties();
                Undo.RecordObject(manager, "Rebuild Layer Distance Culling Cache");
                manager.RebuildCache();
                manager.ForceRefresh();
                EditorUtility.SetDirty(manager);
                EditorSceneManager.MarkSceneDirty(manager.gameObject.scene);
            }

            if (GUILayout.Button("Force Refresh", GUILayout.Height(28f)))
            {
                serializedManager.ApplyModifiedProperties();
                manager.ForceRefresh();
                EditorUtility.SetDirty(manager);
            }

            if (GUILayout.Button("Restore Rendering", GUILayout.Height(28f)))
            {
                serializedManager.ApplyModifiedProperties();
                manager.RestoreAllRendering();
                EditorUtility.SetDirty(manager);
            }

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(4);

            if (GUILayout.Button("Select Manager Object"))
                Selection.activeObject = manager.gameObject;
        }
    }
}
#endif