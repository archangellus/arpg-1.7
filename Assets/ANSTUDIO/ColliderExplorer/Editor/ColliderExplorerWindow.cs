using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

public class ColliderExplorerWindow : EditorWindow
{
    private GameObject targetObject;
    private Vector2 scrollPosition;
    private bool includeChildren = true;

    private readonly List<Component> colliderComponents = new List<Component>();
    private readonly HashSet<int> selectedColliderIds = new HashSet<int>();

    [MenuItem("Tools/Collider Explorer")]
    public static void ShowWindow()
    {
        GetWindow<ColliderExplorerWindow>("Collider Explorer");
    }

    private void OnGUI()
    {
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Collider Explorer", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Select a GameObject from the scene, scan for Collider and Collider2D components, select entries from the list, then delete only the selected colliders.",
            MessageType.Info);

        EditorGUI.BeginChangeCheck();

        targetObject = (GameObject)EditorGUILayout.ObjectField(
            "Target GameObject",
            targetObject,
            typeof(GameObject),
            true);

        includeChildren = EditorGUILayout.Toggle("Include Children", includeChildren);

        if (EditorGUI.EndChangeCheck())
        {
            RefreshColliderList();
        }

        EditorGUILayout.Space();

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Use Current Selection"))
            {
                if (Selection.activeGameObject != null)
                {
                    targetObject = Selection.activeGameObject;
                    RefreshColliderList();
                }
            }

            if (GUILayout.Button("Refresh"))
            {
                RefreshColliderList();
            }
        }

        EditorGUILayout.Space();

        if (targetObject == null)
        {
            EditorGUILayout.HelpBox("Assign a GameObject or use the current selection.", MessageType.Warning);
            return;
        }

        DrawSelectionToolbar();

        EditorGUILayout.Space();

        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

        if (colliderComponents.Count == 0)
        {
            EditorGUILayout.HelpBox("No colliders found on the selected GameObject.", MessageType.Info);
        }
        else
        {
            for (int i = 0; i < colliderComponents.Count; i++)
            {
                DrawColliderEntry(colliderComponents[i], i);
            }
        }

        EditorGUILayout.EndScrollView();
    }

    private void DrawSelectionToolbar()
    {
        int selectedCount = colliderComponents.Count(c => c != null && selectedColliderIds.Contains(c.GetInstanceID()));

        EditorGUILayout.LabelField(
            $"Found Colliders: {colliderComponents.Count}    Selected: {selectedCount}",
            EditorStyles.boldLabel);

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Select All"))
            {
                SelectAllColliders();
            }

            if (GUILayout.Button("Clear All"))
            {
                selectedColliderIds.Clear();
                Repaint();
            }

            GUI.enabled = selectedCount > 0;
            if (GUILayout.Button("Delete Selected"))
            {
                DeleteSelectedColliders();
            }
            GUI.enabled = true;
        }
    }

    private void RefreshColliderList()
    {
        colliderComponents.Clear();

        if (targetObject == null)
        {
            selectedColliderIds.Clear();
            Repaint();
            return;
        }

        if (includeChildren)
        {
            colliderComponents.AddRange(targetObject.GetComponentsInChildren<Collider>(true));
            colliderComponents.AddRange(targetObject.GetComponentsInChildren<Collider2D>(true));
        }
        else
        {
            colliderComponents.AddRange(targetObject.GetComponents<Collider>());
            colliderComponents.AddRange(targetObject.GetComponents<Collider2D>());
        }

        HashSet<int> validIds = new HashSet<int>(
            colliderComponents
                .Where(c => c != null)
                .Select(c => c.GetInstanceID()));

        selectedColliderIds.IntersectWith(validIds);

        Repaint();
    }

    private void SelectAllColliders()
    {
        selectedColliderIds.Clear();

        foreach (Component component in colliderComponents)
        {
            if (component != null)
            {
                selectedColliderIds.Add(component.GetInstanceID());
            }
        }

        Repaint();
    }

    private void DeleteSelectedColliders()
    {
        List<Component> toDelete = colliderComponents
            .Where(c => c != null && selectedColliderIds.Contains(c.GetInstanceID()))
            .ToList();

        if (toDelete.Count == 0)
        {
            ShowNotification(new GUIContent("No colliders selected."));
            return;
        }

        bool confirm = EditorUtility.DisplayDialog(
            "Delete Selected Colliders",
            $"Delete {toDelete.Count} selected collider component(s)?",
            "Delete",
            "Cancel");

        if (!confirm)
            return;

        Undo.IncrementCurrentGroup();
        int undoGroup = Undo.GetCurrentGroup();

        foreach (Component component in toDelete)
        {
            if (component != null)
            {
                Undo.DestroyObjectImmediate(component);
            }
        }

        Undo.CollapseUndoOperations(undoGroup);

        selectedColliderIds.Clear();
        RefreshColliderList();
    }

    private void DrawColliderEntry(Component component, int index)
    {
        if (component == null)
            return;

        GameObject go = component.gameObject;
        int instanceId = component.GetInstanceID();
        bool isSelected = selectedColliderIds.Contains(instanceId);

        Rect boxRect = EditorGUILayout.BeginVertical("box");

        using (new EditorGUILayout.HorizontalScope())
        {
            bool newSelected = EditorGUILayout.Toggle(isSelected, GUILayout.Width(18f));

            if (newSelected != isSelected)
            {
                if (newSelected)
                {
                    selectedColliderIds.Add(instanceId);
                    PingAndSelect(go);
                }
                else
                {
                    selectedColliderIds.Remove(instanceId);
                }
            }

            if (GUILayout.Button($"{index + 1}. {component.GetType().Name}  |  {go.name}", EditorStyles.miniButton))
            {
                PingAndSelect(go);
            }

            if (GUILayout.Button("Ping", GUILayout.Width(50)))
            {
                PingAndSelect(go);
            }

            if (GUILayout.Button("Select", GUILayout.Width(55)))
            {
                Selection.activeGameObject = go;
            }
        }

        EditorGUILayout.LabelField("Path", GetHierarchyPath(go.transform));

        string extraInfo = GetColliderInfo(component);
        if (!string.IsNullOrEmpty(extraInfo))
        {
            EditorGUILayout.LabelField("Info", extraInfo);
        }

        EditorGUILayout.EndVertical();

        if (Event.current.type == EventType.MouseDown &&
            boxRect.Contains(Event.current.mousePosition) &&
            Event.current.button == 0)
        {
            Repaint();
        }
    }

    private void PingAndSelect(GameObject go)
    {
        if (go == null)
            return;

        Selection.activeGameObject = go;
        EditorGUIUtility.PingObject(go);

        if (SceneView.lastActiveSceneView != null)
        {
            SceneView.lastActiveSceneView.FrameSelected();
        }
    }

    private string GetHierarchyPath(Transform current)
    {
        if (current == null)
            return string.Empty;

        string path = current.name;

        while (current.parent != null)
        {
            current = current.parent;
            path = current.name + "/" + path;
        }

        return path;
    }

    private string GetColliderInfo(Component component)
    {
        switch (component)
        {
            case Collider collider3D:
                return $"Enabled: {collider3D.enabled} | Is Trigger: {collider3D.isTrigger}";
            case Collider2D collider2D:
                return $"Enabled: {collider2D.enabled} | Is Trigger: {collider2D.isTrigger}";
            default:
                return string.Empty;
        }
    }

    private void OnSelectionChange()
    {
        Repaint();
    }
}