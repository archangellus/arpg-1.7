// SceneSwitcher.cs — main editor window.
// Keep all four files in the same folder inside your Unity project.
#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public class SceneSwitcher : EditorWindow
{
    // ─────────────────────────────────────────────────────────────────────────
    // Enums
    // ─────────────────────────────────────────────────────────────────────────

    internal enum StatusType { Ready, Info, Success, Warning, Error }

    private enum StatusAction
    {
        None, Reveal, Console, ReviewMissing, CleanMissing, Validate, UndoLoad
    }

    private struct SceneHealth
    {
        public int total;
        public int inBuildSettings;
        public int missing;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // State
    // ─────────────────────────────────────────────────────────────────────────

    private SceneData sceneData;
    private Vector2   scrollPos;
    private string[]  sceneGUIDs;

    // FIX #6 — was a mutable field, now a proper constant.
    private const string SceneDataFilePath         = "Assets/SceneData.json";
    private const string DefaultExportFileName     = "SceneData.json";
    private const string LastExportDirectoryKey    = "SceneSwitcher.LastExportDirectory";
    private const string LastImageImportDirKey     = "SceneSwitcher.LastImageImportDirectory";
    private const string ImportedIconFolderName    = "ImportedIcons";
    private const int    ImportedIconSize          = 43;

    // ─── Status bar ──────────────────────────────────────────────────────────
    private string       statusBarMessage   = "";
    private string       statusBarTooltip   = "";
    private StatusType   statusBarType      = StatusType.Ready;
    private StatusAction statusPrimaryAction = StatusAction.None;
    private string       statusActionPath   = "";
    private double       statusMessageEndTime;
    private float        statusProgress     = -1f;
    private string       statusProgressLabel = "";

    // ─── Undo snapshot ───────────────────────────────────────────────────────
    // FIX #8 — used for all destructive operations (remove, clean, reorder,
    // and load), not just load.
    private SceneData lastSceneDataBeforeLoad;

    // ─── Drag-reorder ────────────────────────────────────────────────────────
    private int     draggedSceneIndex    = -1;
    private int     dragInsertIndex      = -1;
    private int     sceneReorderHotControl = 0;
    private Vector2 dragStartMousePosition;
    private bool    isDraggingScene;

    // ─── Filters ─────────────────────────────────────────────────────────────
    private string listSearch    = "";
    private bool   showMissingOnly;

    // ─────────────────────────────────────────────────────────────────────────
    // Caches — FIX #1, #7, #12
    // ─────────────────────────────────────────────────────────────────────────

    // FIX #1 — missing-scene path set with TTL; rebuilt at most once per second
    // instead of calling LoadAssetAtPath per row per repaint.
    private HashSet<string> _missingPathCache;
    private double          _missingCacheTime = -1.0;
    private const double    MissingCacheTTL   = 1.5;

    // Build-settings path set with TTL.
    private HashSet<string> _buildSettingsCache;
    private double          _buildCacheTime   = -1.0;
    private const double    BuildCacheTTL     = 1.0;

    // FIX #7 — icon texture cache; evicted on project change.
    private readonly Dictionary<string, Texture2D> _iconTextureCache =
        new Dictionary<string, Texture2D>();

    // FIX #2 — status-icon GUIContent cache; resolved once per StatusType.
    private readonly Dictionary<StatusType, GUIContent> _statusIconCache =
        new Dictionary<StatusType, GUIContent>();

    // ─────────────────────────────────────────────────────────────────────────
    // IMGUI styles — FIX #3, #11
    // ─────────────────────────────────────────────────────────────────────────

    private bool     stylesReady;
    private GUIStyle rowStyle;
    private GUIStyle nameStyle;
    private GUIStyle pathStyle;
    private GUIStyle iconButtonStyle;

    // FIX #3 — was allocated with `new GUIStyle(…)` inside OnGUI every frame.
    private GUIStyle _countStyle;

    // FIX #11 — reusable GUIContent instances; avoid per-row allocations in
    // the hot draw loop.  Safe in immediate-mode GUI because fields are set
    // before every use.
    private readonly GUIContent _reusableContent      = new GUIContent();
    private readonly GUIContent _statusBarIconContent = new GUIContent();
    private readonly GUIContent _missingRowIconContent = new GUIContent();

    // Cached button icon GUIContents — image resolved once in EnsureStyles(),
    // tooltip mutated safely per row (these are our own instances).
    private GUIContent _openBtnContent;
    private GUIContent _pingBtnContent;
    private GUIContent _buildAddBtnContent;
    private GUIContent _buildRemoveBtnContent;
    private GUIContent _removeFromListBtnContent;

    // Cached default scene-asset icon (used when no custom icon is assigned).
    private GUIContent _defaultSceneIconContent;

    // ─────────────────────────────────────────────────────────────────────────
    // Layout constants
    // ─────────────────────────────────────────────────────────────────────────

    private const float OuterPadX           = 5f;
    private const float RowPadL             = 8f;
    private const float RowPadR             = 8f;
    private const float RowPadT             = 6f;
    private const float RowPadB             = 6f;
    private const float RowMarginX          = 2f;
    private const float RowMarginY          = 4f;
    private const float RowGapY             = -4f;
    private const float DragStartDistance   = 4f;
    private const float ReorderIndicatorHeight = 3f;
    private const float LeftIconSize        = 43f;
    private const float GapAfterIcon        = 6f;
    private const float BtnW               = 32f;
    private const float BtnH               = 32f;
    private const float BtnGap             = 4f;
    private const int   BtnCount           = 5;
    private const float StatusBarHeight    = 26f;
    private const int   TitleSize          = 15;

    private float ButtonsBlockWidth => (BtnCount * BtnW) + ((BtnCount - 1) * BtnGap);

    // ─────────────────────────────────────────────────────────────────────────
    // Lifecycle
    // ─────────────────────────────────────────────────────────────────────────

    [MenuItem("Tools/Scene Switcher")]
    public static void ShowWindow()
    {
        var w = GetWindow<SceneSwitcher>("Scene Switcher");
        w.minSize = new Vector2(520, 320);
        w.Show();
    }

    private void OnEnable()
    {
        LoadSceneData();
        stylesReady = false;
        EditorApplication.update         += ClearStatusBar;
        // FIX #7 + #1 — invalidate caches when any asset changes.
        EditorApplication.projectChanged += OnProjectChanged;
    }

    private void OnDisable()
    {
        SaveSceneData();
        EditorApplication.update         -= ClearStatusBar;
        EditorApplication.projectChanged -= OnProjectChanged;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Cache management
    // ─────────────────────────────────────────────────────────────────────────

    private void OnProjectChanged()
    {
        InvalidateMissingCache();
        InvalidateBuildCache();
        InvalidateIconCache();
    }

    private void InvalidateMissingCache()
    {
        _missingPathCache = null;
        _missingCacheTime = -1.0;
    }

    private void InvalidateBuildCache()
    {
        _buildSettingsCache = null;
        _buildCacheTime     = -1.0;
    }

    // FIX #7 — called on projectChanged so stale/destroyed textures are evicted.
    private void InvalidateIconCache() => _iconTextureCache.Clear();

    /// <summary>
    /// Returns a cached set of scene paths that are missing from the asset
    /// database.  Rebuilt at most once every <see cref="MissingCacheTTL"/>
    /// seconds, and on any project change.
    /// FIX #1 — replaces per-row, per-repaint LoadAssetAtPath calls.
    /// </summary>
    private HashSet<string> GetMissingScenePathSet()
    {
        double now = EditorApplication.timeSinceStartup;
        if (_missingPathCache != null && now - _missingCacheTime < MissingCacheTTL)
            return _missingPathCache;

        _missingPathCache = new HashSet<string>();
        if (sceneData?.sceneInfos != null)
        {
            foreach (var info in sceneData.sceneInfos)
            {
                if (info == null) continue;
                string p = info.scenePath ?? "";
                if (string.IsNullOrEmpty(p) || AssetDatabase.LoadAssetAtPath<SceneAsset>(p) == null)
                    _missingPathCache.Add(p);
            }
        }
        _missingCacheTime = now;
        return _missingPathCache;
    }

    /// <summary>
    /// Returns a cached set of scene paths currently in Build Settings.
    /// </summary>
    private HashSet<string> GetBuildSettingsPathSet()
    {
        double now = EditorApplication.timeSinceStartup;
        if (_buildSettingsCache != null && now - _buildCacheTime < BuildCacheTTL)
            return _buildSettingsCache;

        _buildSettingsCache = new HashSet<string>();
        foreach (var s in EditorBuildSettings.scenes)
        {
            if (s != null && !string.IsNullOrEmpty(s.path))
                _buildSettingsCache.Add(s.path);
        }
        _buildCacheTime = now;
        return _buildSettingsCache;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Style initialisation — FIX #3, #11
    // ─────────────────────────────────────────────────────────────────────────

    private void EnsureStyles()
    {
        if (stylesReady) return;
        if (EditorStyles.label == null || EditorStyles.wordWrappedMiniLabel == null)
            return;

        rowStyle = new GUIStyle("HelpBox")
        {
            padding = new RectOffset(8, 8, 6, 6),
            margin  = new RectOffset(6, 6, 4, 4)
        };

        nameStyle = new GUIStyle(EditorStyles.label)
        {
            fontStyle = FontStyle.Bold,
            wordWrap  = false,
            clipping  = TextClipping.Clip,
            fontSize  = TitleSize
        };

        pathStyle = new GUIStyle(EditorStyles.wordWrappedMiniLabel)
        {
            wordWrap = true,
            clipping = TextClipping.Clip
        };

        iconButtonStyle = new GUIStyle(EditorStyles.miniButton)
        {
            fixedWidth  = BtnW,
            fixedHeight = BtnH,
            margin      = new RectOffset(0, 0, 0, 0),
            padding     = new RectOffset(0, 0, 0, 0)
        };

        // FIX #3 — was `new GUIStyle(…)` inside OnGUI every frame.
        _countStyle = new GUIStyle(EditorStyles.centeredGreyMiniLabel)
        {
            alignment = TextAnchor.MiddleRight
        };

        // FIX #11 — resolve button icons once; create our own GUIContent
        // wrappers so we can safely mutate .tooltip per row without touching
        // Unity's internal cached instances.
        _openBtnContent          = ResolveButtonIcon("d_PlayButton", "PlayButton", "▶");
        _pingBtnContent          = ResolveButtonIcon("d_Search Icon", "Search Icon", "🔍");
        _buildAddBtnContent      = ResolveButtonIcon("BuildSettings.Editor.Small", null, "B+");
        _buildRemoveBtnContent   = ResolveButtonIcon("Toolbar Minus", null, "B-");
        _removeFromListBtnContent = ResolveButtonIcon("d_TreeEditor.Trash", "TreeEditor.Trash", "-");

        // Default scene icon used when no custom icon is assigned.
        var raw = EditorGUIUtility.IconContent("SceneAsset Icon");
        _defaultSceneIconContent = (raw?.image != null)
            ? new GUIContent(raw.image)
            : new GUIContent("🎬");

        stylesReady = true;
    }

    /// <summary>
    /// Resolves an icon from Unity's built-in icon set and returns a fresh
    /// GUIContent wrapping only the image (no text/tooltip), so callers can
    /// safely set <c>.tooltip</c> without mutating Unity's internal cache.
    /// </summary>
    private static GUIContent ResolveButtonIcon(string primary, string fallback, string textFallback)
    {
        GUIContent c = EditorGUIUtility.IconContent(primary);
        if (c?.image != null) return new GUIContent(c.image);

        if (!string.IsNullOrEmpty(fallback))
        {
            c = EditorGUIUtility.IconContent(fallback);
            if (c?.image != null) return new GUIContent(c.image);
        }

        return new GUIContent(textFallback);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Persistence
    // ─────────────────────────────────────────────────────────────────────────

    private void LoadSceneData()
    {
        if (File.Exists(SceneDataFilePath))
        {
            string json = File.ReadAllText(SceneDataFilePath);
            sceneData = JsonUtility.FromJson<SceneData>(json) ?? new SceneData();
        }
        else
        {
            sceneData = new SceneData();
        }

        EnsureSceneDataLists(sceneData);
        ImportEmbeddedImagesFromData(sceneData, false);
        DiscoverImportedIcons();
    }

    private void SaveSceneData()
    {
        if (sceneData == null) return;

        EnsureSceneDataLists(sceneData);

        SceneData dataToSave = CloneSceneData(sceneData);
        dataToSave.embeddedImages.Clear();

        string json = JsonUtility.ToJson(dataToSave, true);

        var dir = Path.GetDirectoryName(SceneDataFilePath);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            Directory.CreateDirectory(dir);

        File.WriteAllText(SceneDataFilePath, json);

        // FIX #13 — use targeted ImportAsset instead of the heavier
        // AssetDatabase.Refresh(), which triggers a full reimport.
        if (!string.IsNullOrEmpty(AssetDatabase.AssetPathToGUID(SceneDataFilePath)))
            AssetDatabase.ImportAsset(SceneDataFilePath, ImportAssetOptions.ForceUpdate);
        else
            AssetDatabase.Refresh(); // first-time save: file not yet tracked
    }

    private void EnsureSceneDataLists(SceneData data)
    {
        if (data == null) return;
        if (data.sceneInfos    == null) data.sceneInfos    = new List<SceneInfo>();
        if (data.importedImages == null) data.importedImages = new List<SceneIconInfo>();
        if (data.embeddedImages == null) data.embeddedImages = new List<SceneIconEmbeddedData>();

        for (int i = 0; i < data.sceneInfos.Count; i++)
        {
            if (data.sceneInfos[i] != null && data.sceneInfos[i].customIconId == null)
                data.sceneInfos[i].customIconId = "";
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Export / import
    // ─────────────────────────────────────────────────────────────────────────

    private string GetLastExportDirectory()
    {
        string fallback = Application.dataPath;
        string directory = EditorPrefs.GetString(LastExportDirectoryKey, fallback);
        return Directory.Exists(directory) ? directory : fallback;
    }

    private void RememberExportDirectory(string filePath)
    {
        string dir = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(dir))
            EditorPrefs.SetString(LastExportDirectoryKey, dir);
    }

    private string GetLastImageImportDirectory()
    {
        string fallback = Application.dataPath;
        string directory = EditorPrefs.GetString(LastImageImportDirKey, fallback);
        return Directory.Exists(directory) ? directory : fallback;
    }

    private void RememberImageImportDirectory(string filePath)
    {
        string dir = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(dir))
            EditorPrefs.SetString(LastImageImportDirKey, dir);
    }

    private void ExportSceneData()
    {
        if (sceneData == null) sceneData = new SceneData();
        EnsureSceneDataLists(sceneData);
        DiscoverImportedIcons();

        bool includeIconData = false;
        if (HasImportedIcons())
        {
            int choice = EditorUtility.DisplayDialogComplex(
                "Export Imported Icons?",
                "Scene Switcher found imported icon images. Do you want to embed their image data in this exported JSON so they can be restored when the export is loaded?",
                "Include Icons", "JSON Only", "Cancel");

            if (choice == 2)
            {
                SetStatusBarMessage("Export canceled", 2.0, StatusType.Info, showWindowNotification: false);
                return;
            }
            includeIconData = choice == 0;
        }

        string exportPath = EditorUtility.SaveFilePanel(
            "Export Scene Switcher Data",
            GetLastExportDirectory(),
            DefaultExportFileName, "json");

        if (string.IsNullOrEmpty(exportPath))
        {
            SetStatusBarMessage("Export canceled", 2.0, StatusType.Info, showWindowNotification: false);
            return;
        }

        try
        {
            SceneData dataToExport = CloneSceneData(sceneData);
            dataToExport.embeddedImages.Clear();

            int embeddedIconCount = 0;
            if (includeIconData)
                embeddedIconCount = AddEmbeddedIconsToExportData(dataToExport);

            string json = JsonUtility.ToJson(dataToExport, true);

            string directory = Path.GetDirectoryName(exportPath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                Directory.CreateDirectory(directory);

            File.WriteAllText(exportPath, json);
            RememberExportDirectory(exportPath);

            if (exportPath.StartsWith(Application.dataPath))
                AssetDatabase.Refresh();

            string iconSuffix = embeddedIconCount > 0
                ? $" with {embeddedIconCount} icon{(embeddedIconCount == 1 ? "" : "s")}"
                : "";

            SetStatusBarMessage(
                $"Exported scene list{iconSuffix} to {Path.GetFileName(exportPath)}",
                4.0, StatusType.Success, StatusAction.Reveal, exportPath);
        }
        catch (System.Exception ex)
        {
            Debug.LogException(ex);
            EditorUtility.DisplayDialog("Export Failed",
                $"Could not export Scene Switcher data.\n\n{ex.Message}", "OK");
            SetStatusBarMessage("Export failed", 4.0, StatusType.Error, StatusAction.Console);
        }
    }

    private void LoadExportedSceneData()
    {
        string importPath = EditorUtility.OpenFilePanel(
            "Load Scene Switcher Export", GetLastExportDirectory(), "json");

        if (string.IsNullOrEmpty(importPath))
        {
            SetStatusBarMessage("Load canceled", 2.0, StatusType.Info, showWindowNotification: false);
            return;
        }

        try
        {
            string json = File.ReadAllText(importPath);
            SceneData importedData = JsonUtility.FromJson<SceneData>(json);

            if (importedData == null)
                throw new InvalidDataException("The selected file does not contain valid Scene Switcher data.");

            EnsureSceneDataLists(importedData);
            int embeddedIconCount = importedData.embeddedImages.Count;

            bool replace = EditorUtility.DisplayDialog(
                "Load Exported Scene List",
                $"Replace the current scene list with {importedData.sceneInfos.Count} scene(s) from:\n\n{importPath}",
                "Load", "Cancel");

            if (!replace)
            {
                SetStatusBarMessage("Load canceled", 2.0, StatusType.Info, showWindowNotification: false);
                return;
            }

            lastSceneDataBeforeLoad = CloneSceneData(sceneData);
            ImportEmbeddedImagesFromData(importedData, true);
            importedData.embeddedImages.Clear();
            sceneData = importedData;
            DiscoverImportedIcons();
            RememberExportDirectory(importPath);
            SaveSceneData();
            InvalidateMissingCache();

            string iconSuffix = embeddedIconCount > 0
                ? $" and restored {embeddedIconCount} icon{(embeddedIconCount == 1 ? "" : "s")}"
                : "";

            SetStatusBarMessage(
                $"Loaded {sceneData.sceneInfos.Count} scene(s){iconSuffix} from export",
                4.0, StatusType.Success, StatusAction.UndoLoad);
            Repaint();
        }
        catch (System.Exception ex)
        {
            Debug.LogException(ex);
            EditorUtility.DisplayDialog("Load Failed",
                $"Could not load Scene Switcher data.\n\n{ex.Message}", "OK");
            SetStatusBarMessage("Load failed", 4.0, StatusType.Error, StatusAction.Console);
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Status bar helpers
    // ─────────────────────────────────────────────────────────────────────────

    internal void SetStatusBarMessage(string message, double duration)
        => SetStatusBarMessage(message, duration, StatusType.Info);

    internal void SetStatusBarMessage(string message, double duration, StatusType type)
        => SetStatusBarMessage(message, duration, type, StatusAction.None);

    private void SetStatusBarMessage(
        string message, double duration, StatusType type,
        StatusAction primaryAction  = StatusAction.None,
        string       actionPath     = "",
        string       tooltip        = "",
        bool         showWindowNotification = true)
    {
        statusBarMessage    = message ?? "";
        statusBarTooltip    = string.IsNullOrEmpty(tooltip) ? statusBarMessage : tooltip;
        statusBarType       = type;
        statusPrimaryAction = primaryAction;
        statusActionPath    = actionPath ?? "";
        statusMessageEndTime = EditorApplication.timeSinceStartup + duration;
        statusProgress      = -1f;
        statusProgressLabel = "";

        if (showWindowNotification && ShouldShowWindowNotification(type, statusBarMessage))
            ShowNotification(new GUIContent(TruncateNotification(statusBarMessage)));

        Repaint();
    }

    private static bool ShouldShowWindowNotification(StatusType type, string message)
    {
        if (string.IsNullOrEmpty(message)) return false;
        return type == StatusType.Success || type == StatusType.Warning || type == StatusType.Error;
    }

    private static string TruncateNotification(string message)
    {
        const int max = 64;
        if (string.IsNullOrEmpty(message) || message.Length <= max) return message;
        return message.Substring(0, max - 1) + "…";
    }

    private void ClearStatusBarMessage()
    {
        statusBarMessage    = "";
        statusBarTooltip    = "";
        statusBarType       = StatusType.Ready;
        statusPrimaryAction = StatusAction.None;
        statusActionPath    = "";
        statusProgress      = -1f;
        statusProgressLabel = "";
        Repaint();
    }

    private void ClearStatusBar()
    {
        if (statusProgress >= 0f) return;
        if (!string.IsNullOrEmpty(statusBarMessage) &&
            EditorApplication.timeSinceStartup >= statusMessageEndTime)
            ClearStatusBarMessage();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // OnGUI
    // ─────────────────────────────────────────────────────────────────────────

    private void OnGUI()
    {
        EnsureStyles();
        if (!stylesReady)
        {
            EditorGUILayout.HelpBox("Editor UI is still initialising. Please wait a moment.",
                MessageType.Info);
            return;
        }

        DrawToolbar();

        float topY = GUILayoutUtility.GetLastRect().yMax + 6f;

        Rect contentRect = new Rect(
            OuterPadX, topY,
            position.width  - OuterPadX * 2f,
            position.height - topY - StatusBarHeight - OuterPadX);

        Rect statusRect = new Rect(
            OuterPadX,
            position.height - StatusBarHeight - OuterPadX,
            position.width  - OuterPadX * 2f,
            StatusBarHeight);

        const float headerH   = 22f;
        const float countW    = 120f;
        const float headerGap = 6f;

        Rect headerRect = new Rect(contentRect.x, contentRect.y, contentRect.width, headerH);
        Rect titleRect  = new Rect(headerRect.x, headerRect.y,
                                   headerRect.width - countW - headerGap, headerRect.height);
        Rect countRect  = new Rect(headerRect.xMax - countW, headerRect.y, countW, headerRect.height);

        GUI.Label(titleRect,
            new GUIContent("Scene Switcher", "Quick-open scenes from your list"),
            EditorStyles.boldLabel);

        // FIX #3 — _countStyle is cached in EnsureStyles, not allocated here.
        _reusableContent.text    = $"{sceneData.sceneInfos.Count} scene(s)";
        _reusableContent.tooltip = "Scenes in your quick list";
        GUI.Label(countRect, _reusableContent, _countStyle);

        Rect listRect = new Rect(
            contentRect.x, headerRect.yMax + 6f,
            contentRect.width, contentRect.height - headerH - 6f);

        DrawSceneListRect(listRect);
        DrawStatusBar(statusRect);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Toolbar
    // ─────────────────────────────────────────────────────────────────────────

    private void DrawToolbar()
    {
        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

        var addIcon = EditorGUIUtility.IconContent("Toolbar Plus");
        addIcon.tooltip = "Add a scene to this list";
        if (GUILayout.Button(addIcon, EditorStyles.toolbarButton, GUILayout.Width(32)))
        {
            sceneGUIDs = AssetDatabase.FindAssets("t:Scene");
            SceneSelectorWindow.ShowWindow(sceneGUIDs, sceneData, this);
        }

        GUILayout.Space(6);

        var searchField      = GUI.skin.FindStyle("ToolbarSearchTextField")
                            ?? GUI.skin.FindStyle("ToolbarSeachTextField");
        var searchCancel     = GUI.skin.FindStyle("ToolbarSearchCancelButton");
        var searchCancelEmpty = GUI.skin.FindStyle("ToolbarSearchCancelButtonEmpty");

        listSearch = GUILayout.TextField(listSearch, searchField, GUILayout.MinWidth(160));
        var cancelStyle = string.IsNullOrEmpty(listSearch) ? searchCancelEmpty : searchCancel;
        if (GUILayout.Button(new GUIContent("", "Clear search"), cancelStyle))
        {
            listSearch = "";
            GUI.FocusControl(null);
        }

        bool newShowMissingOnly = GUILayout.Toggle(
            showMissingOnly,
            new GUIContent("Missing", "Show only entries whose scene asset can no longer be found"),
            EditorStyles.toolbarButton, GUILayout.Width(64));

        if (newShowMissingOnly != showMissingOnly)
        {
            showMissingOnly = newShowMissingOnly;
            if (showMissingOnly)
                SetStatusBarMessage("Showing missing scene assets only", 2.5,
                    StatusType.Info, showWindowNotification: false);
            else
                ClearStatusBarMessage();
        }

        GUILayout.FlexibleSpace();

        if (GUILayout.Button(new GUIContent("Export", "Export this scene list to a JSON file"),
                EditorStyles.toolbarButton, GUILayout.Width(58)))
            ExportSceneData();

        if (GUILayout.Button(new GUIContent("Load", "Load a previously exported Scene Switcher JSON file"),
                EditorStyles.toolbarButton, GUILayout.Width(48)))
            LoadExportedSceneData();

        EditorGUILayout.EndHorizontal();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Scene list draw
    // ─────────────────────────────────────────────────────────────────────────

    private void DrawSceneListRect(Rect listRect)
    {
        if (sceneData.sceneInfos.Count == 0)
        {
            EditorGUI.HelpBox(listRect, "No scenes yet. Click + to add scenes to your quick list.",
                MessageType.Info);
            return;
        }

        // FIX #1 + build cache — both sets built once per repaint interval,
        // not once per row per repaint.
        HashSet<string> missingSet = GetMissingScenePathSet();
        HashSet<string> buildSet   = GetBuildSettingsPathSet();

        string q             = (listSearch ?? "").Trim().ToLowerInvariant();
        bool   listIsFiltered = showMissingOnly || !string.IsNullOrEmpty(q);
        int    reorderControlId = GUIUtility.GetControlID(FocusType.Passive);
        Event  currentEvent  = Event.current;

        float viewW   = listRect.width;
        float viewH   = listRect.height;
        float vScrollW = (GUI.skin.verticalScrollbar != null &&
                          GUI.skin.verticalScrollbar.fixedWidth > 0)
                        ? GUI.skin.verticalScrollbar.fixedWidth : 14f;

        // Build visible index list.
        var visibleIndices = new List<int>();
        for (int i = 0; i < sceneData.sceneInfos.Count; i++)
        {
            var info = sceneData.sceneInfos[i];
            if (info == null) continue;

            string name = info.sceneName ?? "";
            string path = info.scenePath ?? "";

            if (showMissingOnly && !missingSet.Contains(path)) continue;

            if (!string.IsNullOrEmpty(q) &&
                !name.ToLowerInvariant().Contains(q) &&
                !path.ToLowerInvariant().Contains(q))
                continue;

            visibleIndices.Add(i);
        }

        if (visibleIndices.Count == 0)
        {
            string emptyMsg = showMissingOnly
                ? "No missing scenes found."
                : "No matches for your search.";
            EditorGUI.HelpBox(listRect, emptyMsg, MessageType.Info);
            scrollPos = Vector2.zero;
            return;
        }

        // FIX #4 — vScrollW is now correctly subtracted in CalcContentHeight.
        float contentH_noScroll = CalcContentHeight(viewW, 0f, visibleIndices);
        bool  needsVScroll      = contentH_noScroll > viewH;
        float contentW          = viewW - (needsVScroll ? vScrollW : 0f);
        float contentH          = CalcContentHeight(contentW, needsVScroll ? vScrollW : 0f, visibleIndices);

        scrollPos.x = 0f;
        if (!needsVScroll) scrollPos.y = 0f;

        Rect contentRect = new Rect(0, 0, contentW, Mathf.Max(contentH, viewH));

        if (currentEvent.type == EventType.MouseDown && currentEvent.button == 0 &&
            listRect.Contains(currentEvent.mousePosition))
            CancelSceneReorder(false);

        scrollPos = GUI.BeginScrollView(listRect, scrollPos, contentRect, false, needsVScroll);

        float y = 0f;

        for (int vi = 0; vi < visibleIndices.Count; vi++)
        {
            int i = visibleIndices[vi];
            var info = sceneData.sceneInfos[i];
            if (info == null) continue;

            string name = info.sceneName ?? "";
            string path = info.scenePath ?? "";

            float textW = Mathf.Max(
                140f,
                contentW - RowMarginX * 2f - (RowPadL + RowPadR) - LeftIconSize - GapAfterIcon - ButtonsBlockWidth - 10f);

            float nameH          = nameStyle.CalcHeight(new GUIContent(name), textW);
            float pathH          = pathStyle.CalcHeight(new GUIContent(path), textW);
            float contentInnerH  = Mathf.Max(LeftIconSize, nameH + 2f + pathH);
            float rowH           = RowPadT + contentInnerH + RowPadB;

            y += RowMarginY;

            Rect rowRect = new Rect(RowMarginX, y, contentW - RowMarginX * 2f, rowH);
            GUI.Box(rowRect, GUIContent.none, rowStyle);

            if (IsSceneReorderActive() && isDraggingScene && i == draggedSceneIndex)
                EditorGUI.DrawRect(rowRect,
                    new Color(1f, 1f, 1f, EditorGUIUtility.isProSkin ? 0.08f : 0.14f));

            Rect inner = new Rect(
                rowRect.x + RowPadL, rowRect.y + RowPadT,
                rowRect.width - (RowPadL + RowPadR), rowRect.height - (RowPadT + RowPadB));

            // ── Icon ──────────────────────────────────────────────────────
            Rect      iconRect          = new Rect(inner.x, inner.y + 1f, LeftIconSize, LeftIconSize);
            Texture2D customIconTexture = GetSceneIconTexture(info);

            if (customIconTexture != null)
            {
                GUI.DrawTexture(iconRect, customIconTexture, ScaleMode.ScaleToFit, true);
                // FIX #11 — reuse GUIContent; no allocation.
                _reusableContent.image   = null;
                _reusableContent.text    = "";
                _reusableContent.tooltip = "Custom scene icon. Left-click to change it.";
                GUI.Label(iconRect, _reusableContent);
            }
            else
            {
                // FIX #11 — cached in EnsureStyles.
                _defaultSceneIconContent.tooltip = "Scene asset. Left-click to change this icon.";
                GUI.Label(iconRect, _defaultSceneIconContent);
            }

            EditorGUIUtility.AddCursorRect(iconRect, MouseCursor.Link);
            if (currentEvent.type == EventType.MouseDown && currentEvent.button == 0 &&
                iconRect.Contains(currentEvent.mousePosition))
            {
                ShowIconMenu(i);
                currentEvent.Use();
            }

            // ── Text ──────────────────────────────────────────────────────
            float xText       = iconRect.xMax + GapAfterIcon;
            float xButtons    = inner.xMax - ButtonsBlockWidth;
            Rect  textRect    = new Rect(xText, inner.y, xButtons - xText - 8f, inner.height);
            Rect  btnBlockRect = new Rect(xButtons, inner.y, ButtonsBlockWidth, inner.height);
            Rect  dragRect    = new Rect(textRect.x, rowRect.y,
                                         Mathf.Max(0f, btnBlockRect.x - textRect.x - 8f),
                                         rowRect.height);

            HandleSceneReorderEvents(reorderControlId, i, rowRect, dragRect,
                                     btnBlockRect, iconRect, listIsFiltered);

            // FIX #11 — reuse GUIContent for name and path labels.
            _reusableContent.image   = null;
            _reusableContent.text    = name;
            _reusableContent.tooltip = "Scene name";
            EditorGUI.LabelField(new Rect(textRect.x, textRect.y, textRect.width, nameH),
                _reusableContent, nameStyle);

            _reusableContent.text    = path;
            _reusableContent.tooltip = "Full scene path";
            EditorGUI.LabelField(new Rect(textRect.x, textRect.y + nameH + 2f, textRect.width, pathH),
                _reusableContent, pathStyle);

            DrawSceneReorderIndicator(rowRect, i, sceneData.sceneInfos.Count);

            // ── Missing indicator ─────────────────────────────────────────
            bool missingScene = missingSet.Contains(path);
            if (missingScene)
            {
                Rect missingRect = new Rect(rowRect.xMax - 18f, rowRect.y + 4f, 14f, 14f);
                // FIX #11 — use dedicated reusable instance; never mutates the
                // cached icon returned by GetStatusIcon.
                GUIContent baseIcon = GetStatusIcon(StatusType.Warning);
                _missingRowIconContent.image   = baseIcon.image;
                _missingRowIconContent.text    = baseIcon.text;
                _missingRowIconContent.tooltip = "This scene asset was not found at the saved path";
                GUI.Label(missingRect, _missingRowIconContent);
            }

            // ── Buttons ───────────────────────────────────────────────────
            bool  inBuild = buildSet.Contains(path);
            float btnY    = inner.y + (inner.height - BtnH) * 0.5f;

            // 1) Open
            // FIX #9 — explicit OpenSceneMode.Single makes intent clear.
            // FIX #11 — _openBtnContent reused; tooltip set per row.
            _openBtnContent.tooltip = missingScene
                ? "Scene asset was not found at that path"
                : "Open this scene (prompts to save modified scenes)";
            EditorGUI.BeginDisabledGroup(missingScene);
            if (GUI.Button(new Rect(xButtons, btnY, BtnW, BtnH), _openBtnContent, iconButtonStyle))
            {
                if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                    EditorSceneManager.OpenScene(path, OpenSceneMode.Single); // FIX #9
            }
            EditorGUI.EndDisabledGroup();

            // 2) Ping
            _pingBtnContent.tooltip = "Ping and select the scene asset in the Project window";
            if (GUI.Button(new Rect(xButtons + BtnW + BtnGap, btnY, BtnW, BtnH),
                    _pingBtnContent, iconButtonStyle))
            {
                var sceneAsset = AssetDatabase.LoadAssetAtPath<SceneAsset>(path);
                if (sceneAsset != null)
                {
                    Selection.activeObject = sceneAsset;
                    EditorGUIUtility.PingObject(sceneAsset);
                    SetStatusBarMessage("Pinged scene in Project", 2.0, StatusType.Success);
                }
                else
                {
                    SetStatusBarMessage("Scene asset not found at that path",
                        4.0, StatusType.Warning, StatusAction.ReviewMissing);
                }
            }

            // 3) Add to Build Settings
            _buildAddBtnContent.tooltip = missingScene
                ? "Scene asset was not found at that path"
                : (inBuild ? "Already in Build Settings" : "Add this scene to Build Settings");
            EditorGUI.BeginDisabledGroup(inBuild || missingScene);
            if (GUI.Button(new Rect(xButtons + (BtnW + BtnGap) * 2, btnY, BtnW, BtnH),
                    _buildAddBtnContent, iconButtonStyle))
            {
                bool already = AddSceneToBuild(path);
                InvalidateBuildCache();
                SetStatusBarMessage(
                    already ? $"Already in Build Settings: {name}" : $"Added to Build Settings: {name}",
                    3.5, already ? StatusType.Info : StatusType.Success);
            }
            EditorGUI.EndDisabledGroup();

            // 4) Remove from Build Settings
            _buildRemoveBtnContent.tooltip = inBuild
                ? "Remove this scene from Build Settings"
                : "Scene is not in Build Settings";
            EditorGUI.BeginDisabledGroup(!inBuild);
            if (GUI.Button(new Rect(xButtons + (BtnW + BtnGap) * 3, btnY, BtnW, BtnH),
                    _buildRemoveBtnContent, iconButtonStyle))
            {
                bool removed = RemoveSceneFromBuild(path);
                InvalidateBuildCache();
                SetStatusBarMessage(
                    removed ? $"Removed from Build Settings: {name}" : $"Not found in Build Settings: {name}",
                    3.5,
                    removed ? StatusType.Success : StatusType.Warning,
                    removed ? StatusAction.None   : StatusAction.ReviewMissing);
            }
            EditorGUI.EndDisabledGroup();

            // 5) Remove from list
            // FIX #8 — snapshot before removal so "Undo" can restore.
            _removeFromListBtnContent.tooltip = "Remove this scene from the quick list";
            if (GUI.Button(new Rect(xButtons + (BtnW + BtnGap) * 4, btnY, BtnW, BtnH),
                    _removeFromListBtnContent, iconButtonStyle))
            {
                lastSceneDataBeforeLoad = CloneSceneData(sceneData); // FIX #8
                sceneData.RemoveScene(i);
                InvalidateMissingCache();
                SaveSceneData();
                SetStatusBarMessage("Removed scene from list", 2.5, StatusType.Success,
                    StatusAction.UndoLoad);
                GUIUtility.ExitGUI();
            }

            y += rowH + RowMarginY + RowGapY;
        }

        if (currentEvent.type == EventType.MouseDrag && IsSceneReorderActive() && isDraggingScene)
        {
            if (currentEvent.mousePosition.y <= RowMarginY)
                dragInsertIndex = 0;
            else if (currentEvent.mousePosition.y >= y)
                dragInsertIndex = sceneData.sceneInfos.Count;

            Repaint();
            currentEvent.Use();
        }

        GUI.EndScrollView();

        if (currentEvent.type == EventType.MouseUp && IsSceneReorderActive())
        {
            CompleteSceneReorder();
            currentEvent.Use();
        }
        else if (currentEvent.type == EventType.KeyDown &&
                 currentEvent.keyCode == KeyCode.Escape  &&
                 IsSceneReorderActive())
        {
            CancelSceneReorder(true);
            currentEvent.Use();
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Drag reorder
    // ─────────────────────────────────────────────────────────────────────────

    private bool IsSceneReorderActive()
        => sceneReorderHotControl != 0 &&
           GUIUtility.hotControl == sceneReorderHotControl &&
           draggedSceneIndex >= 0;

    private void HandleSceneReorderEvents(
        int  reorderControlId, int sceneIndex,
        Rect rowRect, Rect reorderDragRect,
        Rect buttonBlockRect, Rect iconRect,
        bool listIsFiltered)
    {
        Event evt = Event.current;
        if (!listIsFiltered)
            EditorGUIUtility.AddCursorRect(reorderDragRect, MouseCursor.MoveArrow);

        if (evt.type == EventType.MouseDown && evt.button == 0 &&
            reorderDragRect.Contains(evt.mousePosition) &&
            !iconRect.Contains(evt.mousePosition) &&
            !buttonBlockRect.Contains(evt.mousePosition))
        {
            if (listIsFiltered)
            {
                SetStatusBarMessage("Clear search and filters before reordering scenes",
                    3.0, StatusType.Info, showWindowNotification: false);
                evt.Use();
                return;
            }

            draggedSceneIndex    = sceneIndex;
            dragInsertIndex      = sceneIndex;
            dragStartMousePosition = evt.mousePosition;
            isDraggingScene      = false;
            sceneReorderHotControl = reorderControlId;
            GUIUtility.hotControl  = sceneReorderHotControl;
            evt.Use();
            return;
        }

        if (!IsSceneReorderActive()) return;

        if (evt.type == EventType.MouseDrag && evt.button == 0)
        {
            if (!isDraggingScene &&
                Vector2.Distance(evt.mousePosition, dragStartMousePosition) >= DragStartDistance)
                isDraggingScene = true;

            if (isDraggingScene && rowRect.Contains(evt.mousePosition))
            {
                UpdateSceneDragInsertIndex(sceneIndex, rowRect, evt.mousePosition);
                Repaint();
                evt.Use();
            }
        }
    }

    private void UpdateSceneDragInsertIndex(int sceneIndex, Rect rowRect, Vector2 mousePos)
    {
        if (sceneData?.sceneInfos == null) return;
        if (rowRect.Contains(mousePos))
        {
            dragInsertIndex = mousePos.y < rowRect.center.y ? sceneIndex : sceneIndex + 1;
            dragInsertIndex = Mathf.Clamp(dragInsertIndex, 0, sceneData.sceneInfos.Count);
        }
    }

    private void DrawSceneReorderIndicator(Rect rowRect, int sceneIndex, int sceneCount)
    {
        if (!IsSceneReorderActive() || !isDraggingScene ||
            dragInsertIndex < 0 || sceneIndex == draggedSceneIndex)
            return;

        if (dragInsertIndex == sceneIndex)
            DrawReorderLine(rowRect.yMin, rowRect);
        else if (sceneIndex == sceneCount - 1 && dragInsertIndex == sceneCount)
            DrawReorderLine(rowRect.yMax, rowRect);
    }

    private void DrawReorderLine(float y, Rect rowRect)
    {
        Rect line = new Rect(rowRect.x + 4f, y - ReorderIndicatorHeight * 0.5f,
                             rowRect.width - 8f, ReorderIndicatorHeight);
        EditorGUI.DrawRect(line, GetStatusColor(StatusType.Info));
    }

    private void CompleteSceneReorder()
    {
        int  fromIndex  = draggedSceneIndex;
        int  toIndex    = dragInsertIndex;
        bool wasDragging = isDraggingScene;

        CancelSceneReorder(false);
        if (!wasDragging || sceneData == null) return;

        // FIX #8 — snapshot before modifying so Undo can restore.
        SceneData snapshot = CloneSceneData(sceneData);
        bool moved = sceneData.MoveScene(fromIndex, toIndex);
        if (moved)
        {
            lastSceneDataBeforeLoad = snapshot;
            SaveSceneData();
            SetStatusBarMessage("Scene order updated", 2.5, StatusType.Success,
                StatusAction.UndoLoad, showWindowNotification: false);
        }

        Repaint();
    }

    private void CancelSceneReorder(bool repaint)
    {
        if (sceneReorderHotControl != 0 && GUIUtility.hotControl == sceneReorderHotControl)
            GUIUtility.hotControl = 0;

        draggedSceneIndex    = -1;
        dragInsertIndex      = -1;
        sceneReorderHotControl = 0;
        dragStartMousePosition = Vector2.zero;
        isDraggingScene      = false;

        if (repaint) Repaint();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Icon management
    // ─────────────────────────────────────────────────────────────────────────

    private void ShowIconMenu(int sceneIndex)
    {
        GenericMenu menu = new GenericMenu();
        menu.AddItem(new GUIContent("Load"),   false, () => LoadIconForScene(sceneIndex));
        menu.AddItem(new GUIContent("Import"), false, () => ImportIconForScene(sceneIndex));

        bool hasCustomIcon = sceneData?.sceneInfos != null &&
            sceneIndex >= 0 && sceneIndex < sceneData.sceneInfos.Count &&
            sceneData.sceneInfos[sceneIndex] != null &&
            !string.IsNullOrEmpty(sceneData.sceneInfos[sceneIndex].customIconId);

        if (hasCustomIcon)
            menu.AddItem(new GUIContent("Delete"), false, () => DeleteIconForScene(sceneIndex));
        else
            menu.AddDisabledItem(new GUIContent("Delete"));

        menu.ShowAsContext();
    }

    private void LoadIconForScene(int sceneIndex)
    {
        DiscoverImportedIcons();
        List<SceneIconInfo> available = GetAvailableImportedIcons();

        if (available.Count == 0)
        {
            EditorUtility.DisplayDialog("No Imported Icons",
                $"No imported icons were found. Use Import first to add images to:\n\n{GetImportedIconFolderAssetPath()}",
                "OK");
            SetStatusBarMessage("No imported icons found", 3.0, StatusType.Info, showWindowNotification: false);
            return;
        }

        string selectedIconId = "";
        if (sceneData?.sceneInfos != null &&
            sceneIndex >= 0 && sceneIndex < sceneData.sceneInfos.Count &&
            sceneData.sceneInfos[sceneIndex] != null)
            selectedIconId = sceneData.sceneInfos[sceneIndex].customIconId ?? "";

        SceneIconPickerWindow.ShowWindow(available, this, sceneIndex, selectedIconId);
    }

    private void ImportIconForScene(int sceneIndex)
    {
        string sourcePath = EditorUtility.OpenFilePanelWithFilters(
            "Import Scene Icon", GetLastImageImportDirectory(),
            new[] { "Image files", "png,jpg,jpeg,tga,bmp,gif,psd,tif,tiff,exr", "All files", "*" });

        if (string.IsNullOrEmpty(sourcePath))
        {
            SetStatusBarMessage("Icon import canceled", 2.0, StatusType.Info, showWindowNotification: false);
            return;
        }

        if (!IsSupportedImageExtension(sourcePath))
        {
            EditorUtility.DisplayDialog("Unsupported Image",
                "Please choose a PNG, JPG, JPEG, TGA, BMP, GIF, PSD, TIF, TIFF, or EXR image.", "OK");
            SetStatusBarMessage("Unsupported image type", 3.0, StatusType.Warning);
            return;
        }

        try
        {
            RememberImageImportDirectory(sourcePath);

            string iconFolder   = EnsureImportedIconFolder();
            string displayName  = Path.GetFileNameWithoutExtension(sourcePath);
            string iconAssetPath = AssetDatabase.GenerateUniqueAssetPath(
                CombineAssetPaths(iconFolder, SanitizeFileName(displayName) + ".png"));
            string iconId = Path.GetFileNameWithoutExtension(iconAssetPath);

            Texture2D fittedIcon = LoadAndFitExternalImage(sourcePath, iconFolder);
            byte[]    pngBytes   = fittedIcon.EncodeToPNG();
            Object.DestroyImmediate(fittedIcon);

            File.WriteAllBytes(AssetPathToFullPath(iconAssetPath), pngBytes);
            AssetDatabase.ImportAsset(iconAssetPath);
            ConfigureIconImporter(iconAssetPath);

            var iconInfo = new SceneIconInfo(iconId, displayName, iconAssetPath);
            AddOrUpdateImportedIcon(iconInfo);
            ApplyImportedIconToScene(sceneIndex, iconId, false);
            SaveSceneData();
            AssetDatabase.Refresh();

            SetStatusBarMessage($"Imported icon: {displayName}", 3.0, StatusType.Success);
            Repaint();
        }
        catch (System.Exception ex)
        {
            Debug.LogException(ex);
            EditorUtility.DisplayDialog("Icon Import Failed",
                $"Could not import the selected image.\n\n{ex.Message}", "OK");
            SetStatusBarMessage("Icon import failed", 4.0, StatusType.Error, StatusAction.Console);
        }
    }

    private void DeleteIconForScene(int sceneIndex)
    {
        if (sceneData?.sceneInfos == null || sceneIndex < 0 ||
            sceneIndex >= sceneData.sceneInfos.Count || sceneData.sceneInfos[sceneIndex] == null)
            return;

        sceneData.sceneInfos[sceneIndex].customIconId = "";
        SaveSceneData();
        SetStatusBarMessage("Reverted scene icon to default", 2.5, StatusType.Success);
        Repaint();
    }

    internal void ApplyImportedIconToScene(int sceneIndex, string iconId, bool saveAfterApply = true)
    {
        if (sceneData?.sceneInfos == null || sceneIndex < 0 ||
            sceneIndex >= sceneData.sceneInfos.Count || sceneData.sceneInfos[sceneIndex] == null)
            return;

        sceneData.sceneInfos[sceneIndex].customIconId = iconId ?? "";

        if (saveAfterApply)
        {
            SaveSceneData();
            SetStatusBarMessage("Scene icon updated", 2.5, StatusType.Success);
            Repaint();
        }
    }

    private Texture2D GetSceneIconTexture(SceneInfo info)
    {
        if (info == null || string.IsNullOrEmpty(info.customIconId)) return null;

        SceneIconInfo iconInfo = FindImportedIconById(info.customIconId);
        if (iconInfo == null || string.IsNullOrEmpty(iconInfo.assetPath)) return null;

        if (_iconTextureCache.TryGetValue(iconInfo.assetPath, out Texture2D cached) && cached != null)
            return cached;

        Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(iconInfo.assetPath);
        if (texture != null)
            _iconTextureCache[iconInfo.assetPath] = texture;

        return texture;
    }

    private SceneIconInfo FindImportedIconById(string iconId)
    {
        if (sceneData?.importedImages == null || string.IsNullOrEmpty(iconId)) return null;

        for (int i = 0; i < sceneData.importedImages.Count; i++)
        {
            var icon = sceneData.importedImages[i];
            if (icon != null && icon.id == iconId) return icon;
        }
        return null;
    }

    private void DiscoverImportedIcons()
    {
        EnsureSceneDataLists(sceneData);
        if (sceneData == null) return;

        string folder = GetImportedIconFolderAssetPath();
        var    discoveredIcons = new List<SceneIconInfo>();

        if (AssetDatabase.IsValidFolder(folder))
        {
            string[] guids = AssetDatabase.FindAssets("t:Texture2D", new[] { folder });
            for (int i = 0; i < guids.Length; i++)
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(guids[i]);
                if (string.IsNullOrEmpty(assetPath) || !IsSupportedImageExtension(assetPath))
                    continue;

                string      id          = Path.GetFileNameWithoutExtension(assetPath);
                string      displayName = id;
                SceneIconInfo existing  = FindImportedIconById(id);
                if (existing != null && !string.IsNullOrEmpty(existing.displayName))
                    displayName = existing.displayName;

                discoveredIcons.Add(new SceneIconInfo(id, displayName, assetPath));
            }
        }

        sceneData.importedImages = discoveredIcons;
        InvalidateIconCache();
    }

    private List<SceneIconInfo> GetAvailableImportedIcons()
    {
        var available = new List<SceneIconInfo>();
        if (sceneData?.importedImages == null) return available;

        for (int i = 0; i < sceneData.importedImages.Count; i++)
        {
            var icon = sceneData.importedImages[i];
            if (icon == null || string.IsNullOrEmpty(icon.assetPath)) continue;
            if (AssetDatabase.LoadAssetAtPath<Texture2D>(icon.assetPath) != null)
                available.Add(icon);
        }
        return available;
    }

    private bool HasImportedIcons() => GetAvailableImportedIcons().Count > 0;

    private void AddOrUpdateImportedIcon(SceneIconInfo iconInfo)
    {
        EnsureSceneDataLists(sceneData);
        if (sceneData == null || iconInfo == null || string.IsNullOrEmpty(iconInfo.id)) return;

        for (int i = 0; i < sceneData.importedImages.Count; i++)
        {
            if (sceneData.importedImages[i]?.id == iconInfo.id)
            {
                sceneData.importedImages[i] = iconInfo;
                return;
            }
        }
        sceneData.importedImages.Add(iconInfo);
    }

    private int AddEmbeddedIconsToExportData(SceneData dataToExport)
    {
        EnsureSceneDataLists(dataToExport);
        dataToExport.embeddedImages.Clear();
        dataToExport.importedImages = new List<SceneIconInfo>();

        var available = GetAvailableImportedIcons();
        for (int i = 0; i < available.Count; i++)
        {
            var    icon     = available[i];
            string fullPath = AssetPathToFullPath(icon.assetPath);
            if (string.IsNullOrEmpty(fullPath) || !File.Exists(fullPath)) continue;

            byte[] bytes       = File.ReadAllBytes(fullPath);
            string iconFileName = SanitizeFileName(icon.id) + ".png";

            dataToExport.importedImages.Add(
                new SceneIconInfo(icon.id, icon.displayName,
                    CombineAssetPaths(GetImportedIconFolderAssetPath(), iconFileName)));
            dataToExport.embeddedImages.Add(
                new SceneIconEmbeddedData(icon.id, icon.displayName, iconFileName,
                    System.Convert.ToBase64String(bytes)));
        }
        return dataToExport.embeddedImages.Count;
    }

    private void ImportEmbeddedImagesFromData(SceneData data, bool showStatus)
    {
        EnsureSceneDataLists(data);
        if (data?.embeddedImages == null || data.embeddedImages.Count == 0) return;

        string iconFolder    = EnsureImportedIconFolder();
        int    restoredCount = 0;

        for (int i = 0; i < data.embeddedImages.Count; i++)
        {
            var embedded = data.embeddedImages[i];
            if (embedded == null || string.IsNullOrEmpty(embedded.id) ||
                string.IsNullOrEmpty(embedded.imageDataBase64))
                continue;

            try
            {
                byte[] bytes     = System.Convert.FromBase64String(embedded.imageDataBase64);
                string fileName  = SanitizeFileName(embedded.id) + ".png";
                string assetPath = CombineAssetPaths(iconFolder, fileName);
                File.WriteAllBytes(AssetPathToFullPath(assetPath), bytes);
                AssetDatabase.ImportAsset(assetPath);
                ConfigureIconImporter(assetPath);
                AddOrUpdateIconInData(data, new SceneIconInfo(embedded.id, embedded.displayName, assetPath));
                restoredCount++;
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"Scene Switcher could not restore embedded icon '{embedded.id}': {ex.Message}");
            }
        }

        AssetDatabase.Refresh();

        if (showStatus && restoredCount > 0)
            SetStatusBarMessage(
                $"Restored {restoredCount} imported icon{(restoredCount == 1 ? "" : "s")}",
                3.0, StatusType.Success);
    }

    private void AddOrUpdateIconInData(SceneData data, SceneIconInfo iconInfo)
    {
        EnsureSceneDataLists(data);
        if (data == null || iconInfo == null || string.IsNullOrEmpty(iconInfo.id)) return;

        for (int i = 0; i < data.importedImages.Count; i++)
        {
            if (data.importedImages[i]?.id == iconInfo.id)
            {
                data.importedImages[i] = iconInfo;
                return;
            }
        }
        data.importedImages.Add(iconInfo);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Image processing
    // ─────────────────────────────────────────────────────────────────────────

    private Texture2D LoadAndFitExternalImage(string sourcePath, string tempFolderAssetPath)
    {
        Texture2D sourceTexture = null;
        Texture2D directTexture = new Texture2D(2, 2, TextureFormat.RGBA32, false);

        try
        {
            byte[] bytes = File.ReadAllBytes(sourcePath);
            if (directTexture.LoadImage(bytes))
            {
                sourceTexture = directTexture;
                directTexture = null;
            }
        }
        catch { /* fall through to Unity importer path below */ }
        finally
        {
            if (directTexture != null) Object.DestroyImmediate(directTexture);
        }

        string tempAssetPath = "";
        if (sourceTexture == null)
        {
            string ext = Path.GetExtension(sourcePath).ToLowerInvariant();
            tempAssetPath = AssetDatabase.GenerateUniqueAssetPath(
                CombineAssetPaths(tempFolderAssetPath, "__SceneSwitcherIconImportTemp" + ext));
            File.Copy(sourcePath, AssetPathToFullPath(tempAssetPath), true);
            AssetDatabase.ImportAsset(tempAssetPath);

            var tempImporter = AssetImporter.GetAtPath(tempAssetPath) as TextureImporter;
            if (tempImporter != null)
            {
                tempImporter.isReadable = true;
                tempImporter.mipmapEnabled = false;
                tempImporter.alphaIsTransparency = true;
                tempImporter.npotScale = TextureImporterNPOTScale.None;
                tempImporter.SaveAndReimport();
            }

            sourceTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(tempAssetPath);
            if (sourceTexture == null)
                throw new InvalidDataException("Unity could not read the selected file as a texture.");
        }

        Texture2D fitted = CreateFittedIconTexture(sourceTexture);

        if (!string.IsNullOrEmpty(tempAssetPath))
            AssetDatabase.DeleteAsset(tempAssetPath);
        else if (sourceTexture != null)
            Object.DestroyImmediate(sourceTexture);

        return fitted;
    }

    private Texture2D CreateFittedIconTexture(Texture2D src)
    {
        if (src == null || src.width <= 0 || src.height <= 0)
            throw new InvalidDataException("The selected image does not have a valid size.");

        float scale  = Mathf.Min((float)ImportedIconSize / src.width,
                                  (float)ImportedIconSize / src.height);
        int   tw     = Mathf.Clamp(Mathf.RoundToInt(src.width  * scale), 1, ImportedIconSize);
        int   th     = Mathf.Clamp(Mathf.RoundToInt(src.height * scale), 1, ImportedIconSize);

        RenderTexture prev = RenderTexture.active;
        RenderTexture rt   = RenderTexture.GetTemporary(tw, th, 0, RenderTextureFormat.ARGB32);
        Texture2D     scaled = new Texture2D(tw, th, TextureFormat.RGBA32, false);

        try
        {
            Graphics.Blit(src, rt);
            RenderTexture.active = rt;
            scaled.ReadPixels(new Rect(0, 0, tw, th), 0, 0);
            scaled.Apply();
        }
        finally
        {
            RenderTexture.active = prev;
            RenderTexture.ReleaseTemporary(rt);
        }

        Texture2D fitted = new Texture2D(ImportedIconSize, ImportedIconSize, TextureFormat.RGBA32, false);
        Color[] clear = new Color[ImportedIconSize * ImportedIconSize];
        // (default value of Color is transparent black, so no loop needed)
        fitted.SetPixels(clear);

        int ox = (ImportedIconSize - tw) / 2;
        int oy = (ImportedIconSize - th) / 2;

        for (int py = 0; py < th; py++)
            for (int px = 0; px < tw; px++)
                fitted.SetPixel(ox + px, oy + py, scaled.GetPixel(px, py));

        fitted.Apply();
        Object.DestroyImmediate(scaled);
        return fitted;
    }

    private void ConfigureIconImporter(string assetPath)
    {
        var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
        if (importer == null) return;
        importer.isReadable          = true;
        importer.mipmapEnabled       = false;
        importer.alphaIsTransparency = true;
        importer.npotScale           = TextureImporterNPOTScale.None;
        importer.SaveAndReimport();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Path helpers
    // ─────────────────────────────────────────────────────────────────────────

    private string EnsureImportedIconFolder()
    {
        string folder = GetImportedIconFolderAssetPath();
        if (AssetDatabase.IsValidFolder(folder)) return folder;

        string parent = GetScriptFolderAssetPath();
        if (!AssetDatabase.IsValidFolder(parent)) parent = "Assets";

        string guid = AssetDatabase.CreateFolder(parent, ImportedIconFolderName);
        if (!string.IsNullOrEmpty(guid))
            folder = AssetDatabase.GUIDToAssetPath(guid);

        if (!AssetDatabase.IsValidFolder(folder))
        {
            Directory.CreateDirectory(AssetPathToFullPath(folder));
            AssetDatabase.Refresh();
        }
        return folder;
    }

    private string GetImportedIconFolderAssetPath()
        => CombineAssetPaths(GetScriptFolderAssetPath(), ImportedIconFolderName);

    private string GetScriptFolderAssetPath()
    {
        string scriptPath = "";
        try
        {
            var script = MonoScript.FromScriptableObject(this);
            if (script != null) scriptPath = AssetDatabase.GetAssetPath(script);
        }
        catch { scriptPath = ""; }

        if (string.IsNullOrEmpty(scriptPath))
        {
            string[] guids = AssetDatabase.FindAssets("SceneSwitcher t:Script");
            for (int i = 0; i < guids.Length; i++)
            {
                string candidate = AssetDatabase.GUIDToAssetPath(guids[i]);
                if (Path.GetFileName(candidate) == "SceneSwitcher.cs")
                {
                    scriptPath = candidate;
                    break;
                }
            }
        }

        if (string.IsNullOrEmpty(scriptPath)) return "Assets";
        string folder = Path.GetDirectoryName(scriptPath);
        return string.IsNullOrEmpty(folder) ? "Assets" : NormalizeAssetPath(folder);
    }

    private string AssetPathToFullPath(string assetPath)
    {
        string root = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        return Path.GetFullPath(Path.Combine(root, assetPath));
    }

    private string CombineAssetPaths(string left, string right)
    {
        if (string.IsNullOrEmpty(left))  return NormalizeAssetPath(right);
        if (string.IsNullOrEmpty(right)) return NormalizeAssetPath(left);
        return NormalizeAssetPath(left.TrimEnd('/', '\\') + "/" + right.TrimStart('/', '\\'));
    }

    private static string NormalizeAssetPath(string path)
        => (path ?? "").Replace('\\', '/');

    private static string SanitizeFileName(string fileName)
    {
        string safe = string.IsNullOrEmpty(fileName) ? "Icon" : fileName;
        foreach (char c in Path.GetInvalidFileNameChars())
            safe = safe.Replace(c, '_');
        safe = safe.Trim();
        if (string.IsNullOrEmpty(safe)) safe = "Icon";
        return safe.Length > 80 ? safe.Substring(0, 80) : safe;
    }

    private static bool IsSupportedImageExtension(string pathOrExt)
    {
        string ext = Path.GetExtension(pathOrExt);
        if (string.IsNullOrEmpty(ext) && !string.IsNullOrEmpty(pathOrExt) && pathOrExt.StartsWith("."))
            ext = pathOrExt;
        switch ((ext ?? "").TrimStart('.').ToLowerInvariant())
        {
            case "png": case "jpg": case "jpeg": case "tga": case "bmp":
            case "gif": case "psd": case "tif":  case "tiff": case "exr":
                return true;
            default:
                return false;
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Build Settings helpers
    // ─────────────────────────────────────────────────────────────────────────

    private bool AddSceneToBuild(string scenePath)
    {
        var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
        foreach (var s in scenes)
            if (s.path == scenePath) return true; // already present

        scenes.Add(new EditorBuildSettingsScene(scenePath, true));
        EditorBuildSettings.scenes = scenes.ToArray();
        return false;
    }

    private bool RemoveSceneFromBuild(string scenePath)
    {
        var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
        int index  = scenes.FindIndex(s => s.path == scenePath);
        if (index < 0) return false;
        scenes.RemoveAt(index);
        EditorBuildSettings.scenes = scenes.ToArray();
        return true;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Layout helper
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// FIX #4 — <paramref name="vScrollW"/> is now correctly subtracted from
    /// <paramref name="availableW"/> so the two-pass scroll detection is accurate.
    /// </summary>
    private float CalcContentHeight(float availableW, float vScrollW, List<int> visibleIndices)
    {
        float contentW = availableW - vScrollW; // FIX #4
        float y = 0f;

        for (int vi = 0; vi < visibleIndices.Count; vi++)
        {
            var info = sceneData.sceneInfos[visibleIndices[vi]];
            if (info == null) continue;

            float textW = Mathf.Max(
                140f,
                contentW - RowMarginX * 2f - (RowPadL + RowPadR) - LeftIconSize - GapAfterIcon - ButtonsBlockWidth - 10f);

            float nameH         = nameStyle.CalcHeight(new GUIContent(info.sceneName ?? ""), textW);
            float pathH         = pathStyle.CalcHeight(new GUIContent(info.scenePath ?? ""), textW);
            float contentInnerH = Mathf.Max(LeftIconSize, nameH + 2f + pathH);
            float rowH          = RowPadT + contentInnerH + RowPadB;

            y += RowMarginY + rowH + RowMarginY + RowGapY;
        }
        return Mathf.Max(0f, y);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Status bar draw
    // ─────────────────────────────────────────────────────────────────────────

    private void DrawStatusBar(Rect r)
    {
        SceneHealth health     = GetSceneHealth();
        bool        hasMessage = !string.IsNullOrEmpty(statusBarMessage);
        bool        hasProgress = statusProgress >= 0f;

        StatusType displayType = hasMessage || hasProgress
            ? statusBarType
            : (health.missing > 0 ? StatusType.Warning : StatusType.Ready);

        string msg = hasProgress
            ? (string.IsNullOrEmpty(statusProgressLabel) ? statusBarMessage : statusProgressLabel)
            : (hasMessage ? statusBarMessage : GetReadyStatusText(health));

        string tooltip = string.IsNullOrEmpty(statusBarTooltip) ? msg : statusBarTooltip;

        GUI.Box(r, GUIContent.none, EditorStyles.helpBox);
        EditorGUI.DrawRect(new Rect(r.x + 2f, r.y + 2f, 3f, r.height - 4f),
            GetStatusColor(displayType));

        Rect  inner = new Rect(r.x + 8f, r.y + 4f, r.width - 16f, r.height - 8f);
        float xMax  = inner.xMax;

        if (!hasProgress)
        {
            if (hasMessage)
            {
                if (DrawStatusButton(ref xMax, inner, "Clear", "Clear the status message", 54f))
                    ClearStatusBarMessage();
                DrawPrimaryStatusAction(ref xMax, inner);
            }
            else if (health.missing > 0)
            {
                if (DrawStatusButton(ref xMax, inner, "Clean",
                        "Remove missing scene entries after confirmation", 54f))
                    CleanMissingSceneEntries();
                if (DrawStatusButton(ref xMax, inner, "Review",
                        "Show only missing scene entries", 58f))
                    ReviewMissingScenes();
            }
            else
            {
                if (DrawStatusButton(ref xMax, inner, "Validate",
                        "Check scene paths and Build Settings status", 66f))
                    ValidateSceneList();
            }
        }

        Rect iconRect = new Rect(inner.x, inner.y + 1f, 16f, inner.height - 2f);

        // FIX #2 + #11 — use _statusBarIconContent so the cached GUIContent
        // returned by GetStatusIcon is never mutated.
        GUIContent baseIcon = GetStatusIcon(displayType);
        _statusBarIconContent.image   = baseIcon.image;
        _statusBarIconContent.text    = baseIcon.text;
        _statusBarIconContent.tooltip = tooltip;
        GUI.Label(iconRect, _statusBarIconContent);

        Rect labelRect = new Rect(iconRect.xMax + 5f, inner.y,
                                   Mathf.Max(0f, xMax - iconRect.xMax - 9f), inner.height);

        if (hasProgress)
            EditorGUI.ProgressBar(labelRect, Mathf.Clamp01(statusProgress), msg);
        else
        {
            _reusableContent.image   = null;
            _reusableContent.text    = msg;
            _reusableContent.tooltip = tooltip;
            EditorGUI.LabelField(labelRect, _reusableContent, EditorStyles.label);
        }
    }

    private bool DrawStatusButton(ref float xMax, Rect inner, string label, string tooltip, float width)
    {
        xMax -= width;
        bool clicked = GUI.Button(new Rect(xMax, inner.y, width, inner.height),
            new GUIContent(label, tooltip), EditorStyles.miniButton);
        xMax -= 4f;
        return clicked;
    }

    private void DrawPrimaryStatusAction(ref float xMax, Rect inner)
    {
        switch (statusPrimaryAction)
        {
            case StatusAction.Reveal:
                if (DrawStatusButton(ref xMax, inner, "Reveal", "Reveal the exported JSON file", 58f))
                    HandleStatusAction(StatusAction.Reveal);
                break;
            case StatusAction.Console:
                if (DrawStatusButton(ref xMax, inner, "Console", "Open the Console window for details", 64f))
                    HandleStatusAction(StatusAction.Console);
                break;
            case StatusAction.ReviewMissing:
                if (DrawStatusButton(ref xMax, inner, "Review", "Show only missing scene entries", 58f))
                    HandleStatusAction(StatusAction.ReviewMissing);
                break;
            case StatusAction.CleanMissing:
                if (DrawStatusButton(ref xMax, inner, "Clean",
                        "Remove missing scene entries after confirmation", 54f))
                    HandleStatusAction(StatusAction.CleanMissing);
                break;
            case StatusAction.Validate:
                if (DrawStatusButton(ref xMax, inner, "Validate",
                        "Check scene paths and Build Settings status", 66f))
                    HandleStatusAction(StatusAction.Validate);
                break;
            case StatusAction.UndoLoad:
                if (DrawStatusButton(ref xMax, inner, "Undo",
                        "Restore the scene list from before the last operation", 52f))
                    HandleStatusAction(StatusAction.UndoLoad);
                break;
        }
    }

    private void HandleStatusAction(StatusAction action)
    {
        switch (action)
        {
            case StatusAction.Reveal:
                if (!string.IsNullOrEmpty(statusActionPath))
                    EditorUtility.RevealInFinder(statusActionPath);
                break;
            case StatusAction.Console:
                if (!EditorApplication.ExecuteMenuItem("Window/General/Console"))
                    EditorApplication.ExecuteMenuItem("Window/Console");
                break;
            case StatusAction.ReviewMissing:  ReviewMissingScenes();      break;
            case StatusAction.CleanMissing:   CleanMissingSceneEntries(); break;
            case StatusAction.Validate:       ValidateSceneList();        break;
            case StatusAction.UndoLoad:       UndoLastOperation();        break;
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Icon / colour helpers — FIX #2
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Returns a cached GUIContent for the given status type.  The returned
    /// instance is owned by the cache; callers must not mutate its fields
    /// directly — use <see cref="_statusBarIconContent"/> or
    /// <see cref="_missingRowIconContent"/> wrappers instead.
    /// </summary>
    private GUIContent GetStatusIcon(StatusType type)
    {
        if (_statusIconCache.TryGetValue(type, out GUIContent cached) && cached?.image != null)
            return cached;

        GUIContent resolved;
        switch (type)
        {
            case StatusType.Ready:
            case StatusType.Success:
                resolved = TryIconContent("TestPassed", "CollabNew", "d_TestPassed",
                                          "d_CollabNew", "console.infoicon");
                break;
            case StatusType.Warning:
                resolved = TryIconContent("console.warnicon", "d_console.warnicon",
                                          "console.warnicon.sml", "d_console.warnicon.sml");
                break;
            case StatusType.Error:
                resolved = TryIconContent("console.erroricon", "d_console.erroricon",
                                          "console.erroricon.sml", "d_console.erroricon.sml");
                break;
            default:
                resolved = TryIconContent("console.infoicon", "d_console.infoicon",
                                          "console.infoicon.sml", "d_console.infoicon.sml");
                break;
        }

        _statusIconCache[type] = resolved;
        return resolved;
    }

    private static GUIContent TryIconContent(params string[] names)
    {
        foreach (string n in names)
        {
            GUIContent c = EditorGUIUtility.IconContent(n);
            if (c?.image != null) return c;
        }
        return new GUIContent("•");
    }

    private static Color GetStatusColor(StatusType type)
    {
        float a = EditorGUIUtility.isProSkin ? 0.8f : 0.65f;
        switch (type)
        {
            case StatusType.Success:
            case StatusType.Ready:   return new Color(0.18f, 0.65f, 0.28f, a);
            case StatusType.Warning: return new Color(0.95f, 0.68f, 0.15f, a);
            case StatusType.Error:   return new Color(0.85f, 0.22f, 0.18f, a);
            default:                 return new Color(0.25f, 0.48f, 0.95f, a);
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Scene health / validation
    // ─────────────────────────────────────────────────────────────────────────

    private static string GetReadyStatusText(SceneHealth h)
        => $"Ready • {h.total} scene(s) • {h.inBuildSettings} in Build Settings • {h.missing} missing";

    private SceneHealth GetSceneHealth()
    {
        var health = new SceneHealth();
        if (sceneData?.sceneInfos == null) return health;

        HashSet<string> buildSet   = GetBuildSettingsPathSet();
        HashSet<string> missingSet = GetMissingScenePathSet(); // FIX #1

        for (int i = 0; i < sceneData.sceneInfos.Count; i++)
        {
            var info = sceneData.sceneInfos[i];
            if (info == null) continue;
            health.total++;
            string path = info.scenePath ?? "";
            if (buildSet.Contains(path))   health.inBuildSettings++;
            if (missingSet.Contains(path)) health.missing++;
        }
        return health;
    }

    // IsMissingScene is kept for use in non-hot paths (e.g. CleanMissingSceneEntries).
    private bool IsMissingScene(string scenePath)
        => string.IsNullOrEmpty(scenePath) ||
           AssetDatabase.LoadAssetAtPath<SceneAsset>(scenePath) == null;

    private List<int> GetMissingSceneIndices()
    {
        var indices = new List<int>();
        if (sceneData?.sceneInfos == null) return indices;

        HashSet<string> missingSet = GetMissingScenePathSet(); // FIX #1 — use cache
        for (int i = 0; i < sceneData.sceneInfos.Count; i++)
        {
            var info = sceneData.sceneInfos[i];
            if (info == null || missingSet.Contains(info.scenePath ?? ""))
                indices.Add(i);
        }
        return indices;
    }

    private void ReviewMissingScenes()
    {
        showMissingOnly = true;
        listSearch      = "";
        SetStatusBarMessage("Showing missing scene assets only", 3.0,
            StatusType.Info, showWindowNotification: false);
        Repaint();
    }

    private void CleanMissingSceneEntries()
    {
        List<int> missingIndices = GetMissingSceneIndices();

        if (missingIndices.Count == 0)
        {
            showMissingOnly = false;
            SetStatusBarMessage("No missing scene entries to clean", 3.0, StatusType.Success);
            return;
        }

        bool clean = EditorUtility.DisplayDialog(
            "Clean Missing Scene Entries",
            $"Remove {missingIndices.Count} missing scene entr{(missingIndices.Count == 1 ? "y" : "ies")} from the Scene Switcher list?",
            "Remove", "Cancel");

        if (!clean)
        {
            SetStatusBarMessage("Clean canceled", 2.0, StatusType.Info, showWindowNotification: false);
            return;
        }

        // FIX #8 — snapshot before destructive bulk remove.
        lastSceneDataBeforeLoad = CloneSceneData(sceneData);

        for (int i = missingIndices.Count - 1; i >= 0; i--)
            sceneData.RemoveScene(missingIndices[i]);

        showMissingOnly = false;
        InvalidateMissingCache();
        SaveSceneData();

        int count = missingIndices.Count;
        SetStatusBarMessage(
            $"Removed {count} missing scene entr{(count == 1 ? "y" : "ies")}",
            4.0, StatusType.Success, StatusAction.UndoLoad); // FIX #8

        Repaint();
        GUIUtility.ExitGUI();
    }

    /// <summary>
    /// FIX #5 — The original method called BeginStatusProgress / SetStatusProgress /
    /// EndStatusProgress synchronously in one frame, so the progress bar never
    /// rendered.  It is now removed; the result message is set directly.
    /// </summary>
    private void ValidateSceneList()
    {
        SceneHealth health = GetSceneHealth();

        if (health.missing > 0)
        {
            SetStatusBarMessage(
                $"Validation complete: {health.missing} missing scene asset{(health.missing == 1 ? "" : "s")}",
                5.0, StatusType.Warning, StatusAction.ReviewMissing);
        }
        else
        {
            SetStatusBarMessage(
                $"Validation complete: {health.total} scene{(health.total == 1 ? "" : "s")} OK",
                4.0, StatusType.Success);
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Undo — FIX #8
    // ─────────────────────────────────────────────────────────────────────────

    private SceneData CloneSceneData(SceneData source)
    {
        if (source == null) return new SceneData();
        var clone = JsonUtility.FromJson<SceneData>(JsonUtility.ToJson(source)) ?? new SceneData();
        EnsureSceneDataLists(clone);
        return clone;
    }

    /// <summary>
    /// Restores the snapshot taken before the last destructive operation
    /// (remove, clean, reorder, or load).
    /// </summary>
    private void UndoLastOperation()
    {
        if (lastSceneDataBeforeLoad == null)
        {
            SetStatusBarMessage("Nothing to undo", 2.0, StatusType.Info, showWindowNotification: false);
            return;
        }

        sceneData = CloneSceneData(lastSceneDataBeforeLoad);
        lastSceneDataBeforeLoad = null;
        showMissingOnly = false;
        InvalidateMissingCache();
        SaveSceneData();

        SetStatusBarMessage("Restored the previous scene list", 4.0, StatusType.Success);
        Repaint();
    }
}
#endif
