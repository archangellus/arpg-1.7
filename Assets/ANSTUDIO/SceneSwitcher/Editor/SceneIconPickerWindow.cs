// SceneIconPickerWindow.cs — icon picker sub-window for Scene Switcher.
// Keep all four files in the same folder inside your Unity project.
#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public class SceneIconPickerWindow : EditorWindow
{
    // ─────────────────────────────────────────────────────────────────────────
    // State
    // ─────────────────────────────────────────────────────────────────────────

    private readonly List<SceneIconInfo> _icons = new List<SceneIconInfo>();
    private SceneSwitcher _sceneSwitcher;
    private int           _sceneIndex;
    private string        _selectedIconId = "";
    private string        _searchQuery    = "";
    private Vector2       _scrollPos;

    // ─────────────────────────────────────────────────────────────────────────
    // Styles
    // ─────────────────────────────────────────────────────────────────────────

    private bool     _stylesReady;
    private GUIStyle _rowStyle;
    private GUIStyle _nameStyle;
    private GUIStyle _pathStyle;

    // FIX #11 — reusable GUIContent instances; avoids per-row allocations in
    // the hot draw loop.
    private readonly GUIContent _reusableContent    = new GUIContent();
    private readonly GUIContent _fallbackIconContent = new GUIContent();
    private          GUIContent _cachedSceneAssetIcon;

    // ─────────────────────────────────────────────────────────────────────────
    // Factory
    // ─────────────────────────────────────────────────────────────────────────

    public static void ShowWindow(
        List<SceneIconInfo> icons,
        SceneSwitcher       sceneSwitcher,
        int                 sceneIndex,
        string              selectedIconId)
    {
        var window = CreateInstance<SceneIconPickerWindow>();
        window._icons.Clear();
        if (icons != null) window._icons.AddRange(icons);

        window._sceneSwitcher  = sceneSwitcher;
        window._sceneIndex     = sceneIndex;
        window._selectedIconId = selectedIconId ?? "";
        window.titleContent    = new GUIContent("Load Icon", "Choose an already imported icon");
        window.minSize         = new Vector2(420, 320);
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
            wordWrap  = false,
            clipping  = TextClipping.Clip
        };

        _pathStyle = new GUIStyle(EditorStyles.wordWrappedMiniLabel)
        {
            wordWrap = true,
            clipping = TextClipping.Clip
        };

        // FIX #11 — resolve the default scene icon once and cache it.
        var raw = EditorGUIUtility.IconContent("SceneAsset Icon");
        _cachedSceneAssetIcon = (raw?.image != null)
            ? new GUIContent(raw.image)
            : new GUIContent("🎬");

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

        if (_icons.Count == 0)
        {
            EditorGUILayout.HelpBox("No imported icons were found.", MessageType.Info);
            return;
        }

        string q = (_searchQuery ?? "").Trim().ToLowerInvariant();
        _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos);

        bool anyVisible = false;
        for (int i = 0; i < _icons.Count; i++)
        {
            SceneIconInfo icon = _icons[i];
            if (icon == null) continue;

            string displayName = icon.displayName ?? "";
            string assetPath   = icon.assetPath   ?? "";

            if (!string.IsNullOrEmpty(q) &&
                !displayName.ToLowerInvariant().Contains(q) &&
                !assetPath.ToLowerInvariant().Contains(q))
                continue;

            anyVisible = true;
            EditorGUILayout.BeginHorizontal(_rowStyle);

            // ── Thumbnail ─────────────────────────────────────────────────
            Rect      iconRect = GUILayoutUtility.GetRect(43f, 43f,
                                     GUILayout.Width(43f), GUILayout.Height(43f));
            Texture2D texture  = AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);

            if (texture != null)
            {
                GUI.DrawTexture(iconRect, texture, ScaleMode.ScaleToFit, true);
            }
            else
            {
                // FIX #11 — reuse the cached fallback GUIContent.
                GUI.Label(iconRect, _cachedSceneAssetIcon);
            }

            // ── Name + path ───────────────────────────────────────────────
            EditorGUILayout.BeginVertical(GUILayout.ExpandWidth(true));

            // FIX #11 — reuse GUIContent; no per-row allocation.
            bool   isSelected  = icon.id == _selectedIconId;
            string prefix      = isSelected ? "✓ " : "";
            _reusableContent.image   = null;
            _reusableContent.text    = prefix + displayName;
            _reusableContent.tooltip = "Imported icon name";
            GUILayout.Label(_reusableContent, _nameStyle);

            _reusableContent.text    = assetPath;
            _reusableContent.tooltip = "Imported icon asset path";
            GUILayout.Label(_reusableContent, _pathStyle);

            EditorGUILayout.EndVertical();
            GUILayout.FlexibleSpace();

            // ── Use button ────────────────────────────────────────────────
            if (GUILayout.Button(
                    new GUIContent("Use", "Use this icon for the scene"),
                    EditorStyles.miniButton, GUILayout.Width(48), GUILayout.Height(24)))
            {
                _sceneSwitcher?.ApplyImportedIconToScene(_sceneIndex, icon.id);
                Close();
                GUIUtility.ExitGUI();
            }

            EditorGUILayout.EndHorizontal();
        }

        if (!anyVisible)
            EditorGUILayout.HelpBox("No imported icons match your search.", MessageType.Info);

        EditorGUILayout.EndScrollView();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Toolbar
    // ─────────────────────────────────────────────────────────────────────────

    private void DrawToolbar()
    {
        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

        GUILayout.Label(
            new GUIContent("Search", "Filter by icon name or path"),
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
