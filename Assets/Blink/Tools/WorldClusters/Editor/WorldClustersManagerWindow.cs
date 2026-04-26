using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using BLINK.WorldClusters;

public class WorldClustersManagerWindow : EditorWindow
{
    private ScriptableObject scriptableObj;
    private SerializedObject serialObj;
    private GUISkin _skin;
    private WorldClustersEditorData _editorData;
    private SerializedObject _editorDataSO;
    private Vector2 viewScrollPosition;

    private enum Categories
    {
        Home = 0,
        Scene = 1,
        Utilities = 2
    }

    private Categories currentCategory;

    [MenuItem("BLINK/World Clusters/Manager")]
    private static void OpenWindow()
    {
        var window = (WorldClustersManagerWindow)GetWindow(typeof(WorldClustersManagerWindow), false,
            "World Clusters Manager");
        window.minSize = new Vector2(400, 250);
        GUI.contentColor = Color.white;
        window.Show();
    }

    private void OnGUI()
    {
        if (_skin == null) return;
        DrawManagerWindow();
    }

    private void OnEnable()
    {
        scriptableObj = this;
        serialObj = new SerializedObject(scriptableObj);
        _skin = Resources.Load<GUISkin>("EditorData/WorldClustersEditorSkin");
        _editorData = Resources.Load<WorldClustersEditorData>("EditorData/WorldClustersEditorData");
        if (_editorData != null) _editorDataSO = new SerializedObject(_editorData);

        Selection.selectionChanged += OnSelectionChanged;
        EditorApplication.hierarchyChanged += OnHierarchyChanged;
        Undo.undoRedoPerformed += OnUndoRedoPerformed;
    }

    private void OnDisable()
    {
        Selection.selectionChanged -= OnSelectionChanged;
        EditorApplication.hierarchyChanged -= OnHierarchyChanged;
        Undo.undoRedoPerformed -= OnUndoRedoPerformed;
    }

    private void OnSelectionChanged()
    {
        Repaint();
    }

    private void OnHierarchyChanged()
    {
        Repaint();
    }

    private void OnUndoRedoPerformed()
    {
        Repaint();
    }

    private void DrawManagerWindow()
    {
        viewScrollPosition = EditorGUILayout.BeginScrollView(viewScrollPosition, false, false);

        GUILayout.Space(10);
        EditorGUILayout.BeginHorizontal();
        GUILayout.Space(5);
        if (GUILayout.Button("HOME",
            currentCategory == Categories.Home
                ? _skin.GetStyle(_editorData.buttonSelectedStyle)
                : _skin.GetStyle(_editorData.buttonOffStyle),
            GUILayout.ExpandWidth(true)))
        {
            currentCategory = Categories.Home;
        }
        GUILayout.Space(5);

        if (GUILayout.Button("SCENE",
currentCategory == Categories.Scene
    ? _skin.GetStyle(_editorData.buttonSelectedStyle)
    : _skin.GetStyle(_editorData.buttonOffStyle),
GUILayout.ExpandWidth(true)))
        {
            currentCategory = Categories.Scene;
        }


        GUILayout.Space(5);
        if (GUILayout.Button("UTILITIES",
            currentCategory == Categories.Utilities
                ? _skin.GetStyle(_editorData.buttonSelectedStyle)
                : _skin.GetStyle(_editorData.buttonOffStyle),
            GUILayout.ExpandWidth(true)))
        {
            currentCategory = Categories.Utilities;
        }

        GUILayout.Space(5);
        EditorGUILayout.EndHorizontal();

        switch (currentCategory)
        {
            case Categories.Home:
                DrawHome();
                break;
            case Categories.Scene:
                DrawScene();
                break;
            case Categories.Utilities:
                DrawUtilities();
                break;
        }
        bool changed = GUI.changed;
        serialObj.ApplyModifiedProperties();
        if (changed)
        {
            Repaint();
        }
        GUILayout.Space(20);
        GUILayout.EndScrollView();
    }

    private void DrawHome()
    {
        GUILayout.Space(15);

        EditorGUILayout.LabelField("Watch these videos:", GetStyle("title"),
            GUILayout.ExpandWidth(true));
        GUILayout.Space(5);

        EditorGUILayout.BeginHorizontal();
        GUILayout.Space(15);
        if (GUILayout.Button("Introduction to World Cluster", _skin.GetStyle(_editorData.addButtonStyle),
            GUILayout.Height(30), GUILayout.ExpandWidth(true)))
        {
            Application.OpenURL("https://youtu.be/7x1fe55qsxo");
        }
        GUILayout.Space(15);
        EditorGUILayout.EndHorizontal();
    }

    private void DrawScene()
    {
        if (_editorData == null || _editorDataSO == null)
        {
            EditorGUILayout.HelpBox("WorldClustersEditorData not found at Resources/EditorData/WorldClustersEditorData.",
                MessageType.Error);
            return;
        }

        GUILayout.Space(15);

        // Utilities -> Scene (same placement style)
        EditorGUILayout.BeginHorizontal();
        GUILayout.Space(5);
        if (GUILayout.Button("ADD MANAGER TO SCENE", _skin.GetStyle(_editorData.addButtonStyle), GUILayout.Height(30),
                GUILayout.ExpandWidth(true)))
        {
            if (FindWorldClustersManager() == null)
            {
                GameObject manager = new()
                {
                    name = "WorldCluster_MANAGER"
                };
                manager.AddComponent<WorldClustersManager>();
                EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            }
            else
            {
                EditorUtility.DisplayDialog("Hey!", "A World Cluster Manager is already in the scene", "OK");
            }
        }
        GUILayout.Space(5);
        EditorGUILayout.EndHorizontal();

        GUILayout.Space(10);

        EditorGUILayout.LabelField("Hierarchy Icons", GetStyle("title"), GUILayout.ExpandWidth(true));
        GUILayout.Space(10);

        _editorDataSO.Update();

        /*
        EditorGUILayout.PropertyField(_editorDataSO.FindProperty("hierarchyIconsEnabled"),
            new GUIContent("Enable Hierarchy Icons"));
        */

        GUILayout.Space(10);
        EditorGUILayout.PropertyField(_editorDataSO.FindProperty("iconContainsWorldClusters"),
            new GUIContent("Default Parent Icon (PNG)"));
        EditorGUILayout.PropertyField(_editorDataSO.FindProperty("iconManager"),
            new GUIContent("Manager Icon (PNG)"));
        EditorGUILayout.PropertyField(_editorDataSO.FindProperty("iconCluster"),
            new GUIContent("Cluster Icon (PNG)"));
        EditorGUILayout.PropertyField(_editorDataSO.FindProperty("iconCollider"),
            new GUIContent("Collider Icon (PNG)"));
        EditorGUILayout.PropertyField(_editorDataSO.FindProperty("iconTrigger"),
            new GUIContent("Trigger Icon (PNG)"));
        EditorGUILayout.PropertyField(_editorDataSO.FindProperty("iconConditions"),
            new GUIContent("Conditions Icon (PNG)"));

        GUILayout.Space(10);

        EditorGUILayout.BeginHorizontal();
        GUILayout.Space(5);
        if (GUILayout.Button("APPLY ICONS", _skin.GetStyle(_editorData.addButtonStyle),
                GUILayout.Height(30), GUILayout.ExpandWidth(true)))
        {
            _editorDataSO.ApplyModifiedProperties();
            EditorUtility.SetDirty(_editorData);
            AssetDatabase.SaveAssets();

            // Refresh cached textures + repaint windows
            WorldClustersHierarchyIcons.Reload();
        }
        GUILayout.Space(5);
        EditorGUILayout.EndHorizontal();

        _editorDataSO.ApplyModifiedProperties();
    }


    private void DrawUtilities()
    {
        GUILayout.Space(15);
        EditorGUILayout.BeginVertical();

        EditorGUILayout.LabelField("Select Components In Child:", GetStyle("title"),
            GUILayout.ExpandWidth(true));
        GUILayout.Space(5);
        var selectedParents = GetValidParents().ToArray();
        if (selectedParents.Length == 0)
        {
            EditorGUILayout.HelpBox("Select one or more GameObjects in the Hierarchy or Project, then click a component type below.", MessageType.Info);
        }
        else
        {
            EditorGUILayout.HelpBox($"Using {selectedParents.Length} selected object(s) as parent roots.", MessageType.None);
        }
        EditorGUILayout.EndVertical();

        GUILayout.Space(10);
        EditorGUILayout.BeginHorizontal();
        GUILayout.Space(5);
        if (GUILayout.Button("Renderers", _skin.GetStyle(_editorData.addButtonStyle),
            GUILayout.Height(30), GUILayout.ExpandWidth(true), GUILayout.MaxWidth(175)))
        {
            ClearSelection();
            List<GameObject> renderersGO = new List<GameObject>();
            foreach (var go in selectedParents)
            {
                if (go == null) continue;
                foreach (var childRenderer in go.GetComponentsInChildren<Renderer>())
                {
                    if (!IsValidRenderer(childRenderer.GetType().ToString())) continue;
                    renderersGO.Add(childRenderer.gameObject);
                }
            }
            ApplySelection(renderersGO);
        }
        GUILayout.Space(5);
        if (GUILayout.Button("Lights", _skin.GetStyle(_editorData.addButtonStyle),
            GUILayout.Height(30), GUILayout.ExpandWidth(true), GUILayout.MaxWidth(175)))
        {
            ClearSelection();
            List<GameObject> renderersGO = new List<GameObject>();
            foreach (var go in selectedParents)
            {
                if (go == null) continue;
                foreach (var childRenderer in go.GetComponentsInChildren<Light>())
                {
                    renderersGO.Add(childRenderer.gameObject);
                }
            }
            ApplySelection(renderersGO);
        }
        GUILayout.Space(5);
        if (GUILayout.Button("Particle Systems", _skin.GetStyle(_editorData.addButtonStyle),
            GUILayout.Height(30), GUILayout.ExpandWidth(true), GUILayout.MaxWidth(175)))
        {
            ClearSelection();
            List<GameObject> renderersGO = new List<GameObject>();
            foreach (var go in selectedParents)
            {
                if (go == null) continue;
                foreach (var childRenderer in go.GetComponentsInChildren<ParticleSystem>())
                {
                    renderersGO.Add(childRenderer.gameObject);
                }
            }
            ApplySelection(renderersGO);
        }
        GUILayout.Space(5);
        EditorGUILayout.EndHorizontal();

    }

    private IEnumerable<GameObject> GetValidParents()
    {
        foreach (var parent in Selection.objects)
        {
            if (parent is GameObject selectedGameObject)
            {
                yield return selectedGameObject;
            }
        }
    }

    private static void ClearSelection()
    {
        Selection.objects = Array.Empty<UnityEngine.Object>();
        Selection.activeObject = null;
    }

    private static void ApplySelection(IEnumerable<GameObject> objects)
    {
        var selection = ToSelectionArray(objects);
        Selection.objects = selection;
        Selection.activeObject = selection.Length > 0 ? selection[0] : null;
    }

    private static UnityEngine.Object[] ToSelectionArray(IEnumerable<GameObject> objects)
    {
        List<UnityEngine.Object> selection = new List<UnityEngine.Object>();
        foreach (var obj in objects)
        {
            if (obj == null) continue;
            selection.Add(obj);
        }

        return selection.ToArray();
    }

    // ✅ Unity 6.3+ compatible, with fallback for older Unity versions
    private static WorldClustersManager FindWorldClustersManager()
    {
#if UNITY_2023_1_OR_NEWER
        // New API (Unity 6.x uses this)
        return UnityEngine.Object.FindFirstObjectByType<WorldClustersManager>();
#else
            // Old API (Unity 2022 LTS and older)
            return UnityEngine.Object.FindObjectOfType<WorldClustersManager>();
#endif
    }

    private bool IsValidRenderer(string typeName)
    {
        return typeName == "UnityEngine.MeshRenderer" || typeName == "UnityEngine.SkinnedMeshRenderer";
    }

    private GUIStyle GetStyle(string styleName)
    {
        var style = new GUIStyle();
        switch (styleName)
        {
            case "title":
                style.alignment = TextAnchor.MiddleCenter;
                style.fontSize = 20;
                style.fontStyle = FontStyle.Bold;
                style.normal.textColor = Color.white;
                break;
            case "text":
                style.alignment = TextAnchor.MiddleLeft;
                style.fontSize = 17;
                style.fontStyle = FontStyle.Bold;
                style.normal.textColor = Color.white;
                break;

            case "text2":
                style.alignment = TextAnchor.UpperLeft;
                style.fontSize = 16;
                style.fontStyle = FontStyle.Bold;
                style.normal.textColor = Color.white;
                break;

            case "removeButton":
                style.normal.textColor = Color.red;
                style.fontSize = 30;
                break;

            case "collapseGroup":
                style.normal.textColor = Color.gray;
                style.fontSize = 30;
                break;

            case "openGroup":
                style.normal.textColor = Color.green;
                style.fontSize = 30;
                break;
        }

        return style;
    }
}
