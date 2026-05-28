// SceneSelectorWindow.cs — scene picker sub-window for Scene Switcher.
// Keep all four files in the same folder inside your Unity project.
#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

public class SceneSelectorWindow : EditorWindow
{
    // ─────────────────────────────────────────────────────────────────────────
    // State
    // ─────────────────────────────────────────────────────────────────────────

    private string[]          _sceneGUIDs;
    private readonly List<string> _filteredNames = new List<string>();
    private readonly List<string> _filteredGUIDs = new List<string>();

    private SceneData     _sceneData;
    private SceneSwitcher _sceneSwitcher;

    private string  _searchQuery = "";
    private Vector2 _scrollPos;

    private const float OuterPadX = 5f;

    // ─────────────────────────────────────────────────────────────────────────
    // Styles
    // ─────────────────────────────────────────────────────────────────────────

    private bool     _stylesReady;
    private GUIStyle _rowStyle;
    private GUIStyle _nameStyle;
    private GUIStyle _pathStyle;
    private GUIStyle _iconButtonStyle;

    // FIX #11 — reusable GUIContent instances; avoids per-row allocations in
    // the hot draw loop.
    private readonly GUIContent _reusableContent   = new GUIContent();
    private          GUIContent _cachedSceneIcon;
    private          GUIContent _cachedAddBtnContent;

    // ─────────────────────────────────────────────────────────────────────────
    // Factory
    // ─────────────────────────────────────────────────────────────────────────

    public static void ShowWindow(
        string[]      sceneGUIDs,
        SceneData     sceneData,
        SceneSwitcher sceneSwitcher)
    {
        var window = CreateInstance<SceneSelectorWindow>();
        window._sceneGUIDs    = sceneGUIDs;
        window._sceneData     = sceneData;
        window._sceneSwitcher = sceneSwitcher;
        window.titleContent   = new GUIContent("Select Scene", "Pick a scene to add to the quick list");
        window.minSize        = new Vector2(460, 420);
        window.ShowUtility();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Style initialisation
    // ─────────────────────────────────────────────────────────────────────────

    private void EnsureStyles()
    {
        if (_stylesReady) return;
        if (EditorStyles.label == null || EditorStyles.wordWrappedMiniLabel == null) return;

        _rowStyle = new GUIStyle("HelpBox")
        {
            padding = new RectOffset(8, 8, 6, 6),
            margin  = new RectOffset(6, 6, 4, 4)
        };

        _nameStyle = new GUIStyle(EditorStyles.label)
        {
            fontStyle = FontStyle.Bold,
            wordWrap  = false
        };

        _pathStyle = new GUIStyle(EditorStyles.wordWrappedMiniLabel)
        {
            wordWrap = true
        };

        _iconButtonStyle = new GUIStyle(EditorStyles.miniButton)
        {
            fixedWidth  = 28,
            fixedHeight = 22,
            margin      = new RectOffset(2, 2, 0, 0),
            padding     = new RectOffset(0, 0, 0, 0)
        };

        // FIX #11 — resolve and cache icon GUIContent instances once.
        var rawSceneIcon = EditorGUIUtility.IconContent("SceneAsset Icon");
        _cachedSceneIcon = (rawSceneIcon?.image != null)
            ? new GUIContent(rawSceneIcon.image, "Scene asset")
            : new GUIContent("🎬", "Scene asset");

        var rawAddIcon = EditorGUIUtility.IconContent("Toolbar Plus");
        _cachedAddBtnContent = (rawAddIcon?.image != null)
            ? new GUIContent(rawAddIcon.image, "Add this scene to the Scene Switcher list")
            : new GUIContent("+", "Add this scene to the Scene Switcher list");

        _stylesReady = true;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // OnGUI
    // ─────────────────────────────────────────────────────────────────────────

    private void OnGUI()
    {
        EnsureStyles();
        if (!_stylesReady)
        {
            EditorGUILayout.HelpBox(
                "Editor UI is still initialising. Please wait a moment.", MessageType.Info);
            return;
        }

        DrawToolbar();
        EditorGUILayout.Space(6);

        EditorGUILayout.BeginHorizontal();
        GUILayout.Space(OuterPadX);
        EditorGUILayout.BeginVertical();

        // ── Build filtered list ───────────────────────────────────────────
        _filteredNames.Clear();
        _filteredGUIDs.Clear();

        string q = (_searchQuery ?? "").Trim().ToLowerInvariant();

        for (int i = 0; i < _sceneGUIDs.Length; i++)
        {
            string path      = AssetDatabase.GUIDToAssetPath(_sceneGUIDs[i]);
            string sceneName = Path.GetFileNameWithoutExtension(path);

            if (string.IsNullOrEmpty(q) ||
                sceneName.ToLowerInvariant().Contains(q) ||
                path.ToLowerInvariant().Contains(q))
            {
                _filteredNames.Add(sceneName);
                _filteredGUIDs.Add(_sceneGUIDs[i]);
            }
        }

        _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos);

        if (_filteredNames.Count == 0)
        {
            EditorGUILayout.HelpBox("No scenes found for that search.", MessageType.Info);
        }
        else
        {
            for (int i = 0; i < _filteredNames.Count; i++)
            {
                string guid = _filteredGUIDs[i];
                string path = AssetDatabase.GUIDToAssetPath(guid);
                string name = _filteredNames[i];

                EditorGUILayout.BeginHorizontal(_rowStyle);

                // FIX #11 — use the cached scene icon; no per-row allocation.
                GUILayout.Label(_cachedSceneIcon, GUILayout.Width(18), GUILayout.Height(18));

                EditorGUILayout.BeginVertical(GUILayout.ExpandWidth(true));

                // FIX #11 — reuse GUIContent.
                _reusableContent.image   = null;
                _reusableContent.text    = name;
                _reusableContent.tooltip = "Scene name";
                GUILayout.Label(_reusableContent, _nameStyle);

                _reusableContent.text    = path;
                _reusableContent.tooltip = "Full scene path";
                GUILayout.Label(_reusableContent, _pathStyle);

                EditorGUILayout.EndVertical();
                GUILayout.FlexibleSpace();

                // FIX #11 — use the cached add-button icon.
                if (GUILayout.Button(_cachedAddBtnContent, _iconButtonStyle))
                {
                    if (_sceneData.SceneExists(path))
                    {
                        _sceneSwitcher.SetStatusBarMessage(
                            "Scene already in the list", 3,
                            SceneSwitcher.StatusType.Info);
                    }
                    else
                    {
                        _sceneData.AddScene(name, path);
                        _sceneSwitcher.SetStatusBarMessage(
                            "Scene added to the list", 3,
                            SceneSwitcher.StatusType.Success);
                        Close();
                        GUIUtility.ExitGUI();
                    }
                }

                EditorGUILayout.EndHorizontal();
            }
        }

        EditorGUILayout.EndScrollView();
        EditorGUILayout.EndVertical();
        GUILayout.Space(OuterPadX);
        EditorGUILayout.EndHorizontal();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Toolbar
    // ─────────────────────────────────────────────────────────────────────────

    private void DrawToolbar()
    {
        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

        GUILayout.Label(
            new GUIContent("Search", "Filter by scene name or path"),
            GUILayout.Width(50));

        var searchField      = GUI.skin.FindStyle("ToolbarSearchTextField")
                            ?? GUI.skin.FindStyle("ToolbarSeachTextField");
        var searchCancel     = GUI.skin.FindStyle("ToolbarSearchCancelButton");
        var searchCancelEmpty = GUI.skin.FindStyle("ToolbarSearchCancelButtonEmpty");

        _searchQuery = GUILayout.TextField(_searchQuery, searchField, GUILayout.MinWidth(160));

        var cancelStyle = string.IsNullOrEmpty(_searchQuery) ? searchCancelEmpty : searchCancel;
        if (GUILayout.Button(new GUIContent("", "Clear search"), cancelStyle))
        {
            _searchQuery = "";
            GUI.FocusControl(null);
        }

        GUILayout.FlexibleSpace();

        if (GUILayout.Button(
                new GUIContent("Close", "Close this window"),
                EditorStyles.toolbarButton, GUILayout.Width(50)))
            Close();

        EditorGUILayout.EndHorizontal();
    }
}
#endif
