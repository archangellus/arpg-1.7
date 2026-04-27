#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using System.Collections.Generic;
using System.IO;

public class SceneSwitcher : EditorWindow
{
    internal enum StatusType
    {
        Ready,
        Info,
        Success,
        Warning,
        Error
    }

    private enum StatusAction
    {
        None,
        Reveal,
        Console,
        ReviewMissing,
        CleanMissing,
        Validate,
        UndoLoad
    }

    private struct SceneHealth
    {
        public int total;
        public int inBuildSettings;
        public int missing;
    }

    private SceneData sceneData;
    private Vector2 scrollPos;
    private string[] sceneGUIDs;

    private string statusBarMessage = "";
    private string statusBarTooltip = "";
    private StatusType statusBarType = StatusType.Ready;
    private StatusAction statusPrimaryAction = StatusAction.None;
    private string statusActionPath = "";
    private double statusMessageEndTime;
    private float statusProgress = -1f;
    private string statusProgressLabel = "";
    private SceneData lastSceneDataBeforeLoad;
    private string sceneDataFilePath = "Assets/SceneData.json";
    private const string DefaultExportFileName = "SceneData.json";
    private const string LastExportDirectoryPrefsKey = "SceneSwitcher.LastExportDirectory";
    private const string LastImageImportDirectoryPrefsKey = "SceneSwitcher.LastImageImportDirectory";
    private const string ImportedIconFolderName = "ImportedIcons";
    private const int ImportedIconSize = 43;

    private readonly Dictionary<string, Texture2D> iconTextureCache = new Dictionary<string, Texture2D>();

    private string listSearch = "";
    private bool showMissingOnly;

    private int draggedSceneIndex = -1;
    private int dragInsertIndex = -1;
    private int sceneReorderHotControl = 0;
    private Vector2 dragStartMousePosition;
    private bool isDraggingScene;

    // Styles (match Select Scene list)
    private bool stylesReady;
    private GUIStyle rowStyle;
    private GUIStyle nameStyle;
    private GUIStyle pathStyle;
    private GUIStyle iconButtonStyle;

    // Layout constants
    private const float OuterPadX = 5f;

    private const float RowPadL = 8f; // left
    private const float RowPadR = 8f; // right
    private const float RowPadT = 6f; // top
    private const float RowPadB = 6f; // bottom

    private const float RowMarginX = 2f; // left/right
    private const float RowMarginY = 4f; // top/bottom
    private const float RowGapY = -4f; // between rows
    private const float DragStartDistance = 4f;
    private const float ReorderIndicatorHeight = 3f;

    private const float LeftIconSize = 43f;
    private const float GapAfterIcon = 6f;

    private const float BtnW = 32f; // button width
    private const float BtnH = 32f; // button height
    private const float BtnGap = 4f; // gap between buttons
    private const int BtnCount = 5; // number of buttons per row
    private const float StatusBarHeight = 26f;
    private const int TitleSize = 15;


    private float ButtonsBlockWidth => (BtnCount * BtnW) + ((BtnCount - 1) * BtnGap);

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
        stylesReady = false; // build lazily in OnGUI
        EditorApplication.update += ClearStatusBar;
    }

    private void OnDisable()
    {
        SaveSceneData();
        EditorApplication.update -= ClearStatusBar;
    }

    private void EnsureStyles()
    {
        if (stylesReady) return;

        // Guard: can be null during certain editor states
        if (EditorStyles.label == null || EditorStyles.wordWrappedMiniLabel == null)
            return;

        rowStyle = new GUIStyle("HelpBox")
        {
            padding = new RectOffset(8, 8, 6, 6),
            margin = new RectOffset(6, 6, 4, 4)
        };

        nameStyle = new GUIStyle(EditorStyles.label)
        {
            fontStyle = FontStyle.Bold,
            wordWrap = false,
            clipping = TextClipping.Clip,
            fontSize = TitleSize
        };

        pathStyle = new GUIStyle(EditorStyles.wordWrappedMiniLabel)
        {
            wordWrap = true,
            clipping = TextClipping.Clip
        };

        iconButtonStyle = new GUIStyle(EditorStyles.miniButton)
        {
            fixedWidth = BtnW,
            fixedHeight = BtnH,
            margin = new RectOffset(0, 0, 0, 0),
            padding = new RectOffset(0, 0, 0, 0)
        };

        stylesReady = true;
    }

    private void LoadSceneData()
    {
        if (File.Exists(sceneDataFilePath))
        {
            string json = File.ReadAllText(sceneDataFilePath);
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

        var dir = Path.GetDirectoryName(sceneDataFilePath);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            Directory.CreateDirectory(dir);

        File.WriteAllText(sceneDataFilePath, json);
        AssetDatabase.Refresh();
    }

    private void EnsureSceneDataLists(SceneData data)
    {
        if (data == null)
            return;

        if (data.sceneInfos == null)
            data.sceneInfos = new List<SceneInfo>();

        if (data.importedImages == null)
            data.importedImages = new List<SceneIconInfo>();

        if (data.embeddedImages == null)
            data.embeddedImages = new List<SceneIconEmbeddedData>();

        for (int i = 0; i < data.sceneInfos.Count; i++)
        {
            if (data.sceneInfos[i] != null && data.sceneInfos[i].customIconId == null)
                data.sceneInfos[i].customIconId = "";
        }
    }

    private string GetLastExportDirectory()
    {
        string fallback = Application.dataPath;
        string directory = EditorPrefs.GetString(LastExportDirectoryPrefsKey, fallback);

        return Directory.Exists(directory) ? directory : fallback;
    }

    private void RememberExportDirectory(string filePath)
    {
        string directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(directory))
            EditorPrefs.SetString(LastExportDirectoryPrefsKey, directory);
    }

    private string GetLastImageImportDirectory()
    {
        string fallback = Application.dataPath;
        string directory = EditorPrefs.GetString(LastImageImportDirectoryPrefsKey, fallback);

        return Directory.Exists(directory) ? directory : fallback;
    }

    private void RememberImageImportDirectory(string filePath)
    {
        string directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(directory))
            EditorPrefs.SetString(LastImageImportDirectoryPrefsKey, directory);
    }

    private void ExportSceneData()
    {
        if (sceneData == null)
            sceneData = new SceneData();

        EnsureSceneDataLists(sceneData);
        DiscoverImportedIcons();

        bool includeIconData = false;
        if (HasImportedIcons())
        {
            int choice = EditorUtility.DisplayDialogComplex(
                "Export Imported Icons?",
                "Scene Switcher found imported icon images. Do you want to embed their image data in this exported JSON so they can be restored when the export is loaded?",
                "Include Icons",
                "JSON Only",
                "Cancel");

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
            DefaultExportFileName,
            "json");

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

            string iconSuffix = embeddedIconCount > 0 ? $" with {embeddedIconCount} icon{(embeddedIconCount == 1 ? "" : "s")}" : "";
            SetStatusBarMessage(
                $"Exported scene list{iconSuffix} to {Path.GetFileName(exportPath)}",
                4.0,
                StatusType.Success,
                StatusAction.Reveal,
                exportPath);
        }
        catch (System.Exception ex)
        {
            Debug.LogException(ex);
            EditorUtility.DisplayDialog("Export Failed", $"Could not export Scene Switcher data.\n\n{ex.Message}", "OK");
            SetStatusBarMessage("Export failed", 4.0, StatusType.Error, StatusAction.Console);
        }
    }

    private void LoadExportedSceneData()
    {
        string importPath = EditorUtility.OpenFilePanel(
            "Load Scene Switcher Export",
            GetLastExportDirectory(),
            "json");

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
                "Load",
                "Cancel");

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

            string iconSuffix = embeddedIconCount > 0 ? $" and restored {embeddedIconCount} icon{(embeddedIconCount == 1 ? "" : "s")}" : "";
            SetStatusBarMessage(
                $"Loaded {sceneData.sceneInfos.Count} scene(s){iconSuffix} from export",
                4.0,
                StatusType.Success,
                StatusAction.UndoLoad);
            Repaint();
        }
        catch (System.Exception ex)
        {
            Debug.LogException(ex);
            EditorUtility.DisplayDialog("Load Failed", $"Could not load Scene Switcher data.\n\n{ex.Message}", "OK");
            SetStatusBarMessage("Load failed", 4.0, StatusType.Error, StatusAction.Console);
        }
    }

    internal void SetStatusBarMessage(string message, double duration)
    {
        SetStatusBarMessage(message, duration, StatusType.Info);
    }

    internal void SetStatusBarMessage(string message, double duration, StatusType type)
    {
        SetStatusBarMessage(message, duration, type, StatusAction.None);
    }

    private void SetStatusBarMessage(
        string message,
        double duration,
        StatusType type,
        StatusAction primaryAction = StatusAction.None,
        string actionPath = "",
        string tooltip = "",
        bool showWindowNotification = true)
    {
        statusBarMessage = message ?? "";
        statusBarTooltip = string.IsNullOrEmpty(tooltip) ? statusBarMessage : tooltip;
        statusBarType = type;
        statusPrimaryAction = primaryAction;
        statusActionPath = actionPath ?? "";
        statusMessageEndTime = EditorApplication.timeSinceStartup + duration;
        statusProgress = -1f;
        statusProgressLabel = "";

        if (showWindowNotification && ShouldShowWindowNotification(type, statusBarMessage))
            ShowNotification(new GUIContent(GetNotificationText(statusBarMessage)));

        Repaint();
    }

    private bool ShouldShowWindowNotification(StatusType type, string message)
    {
        if (string.IsNullOrEmpty(message))
            return false;

        return type == StatusType.Success || type == StatusType.Warning || type == StatusType.Error;
    }

    private string GetNotificationText(string message)
    {
        const int maxLength = 64;

        if (string.IsNullOrEmpty(message) || message.Length <= maxLength)
            return message;

        return message.Substring(0, maxLength - 1) + "…";
    }

    private void ClearStatusBarMessage()
    {
        statusBarMessage = "";
        statusBarTooltip = "";
        statusBarType = StatusType.Ready;
        statusPrimaryAction = StatusAction.None;
        statusActionPath = "";
        statusProgress = -1f;
        statusProgressLabel = "";
        Repaint();
    }

    private void ClearStatusBar()
    {
        if (statusProgress >= 0f)
            return;

        if (!string.IsNullOrEmpty(statusBarMessage) && EditorApplication.timeSinceStartup >= statusMessageEndTime)
            ClearStatusBarMessage();
    }

    private void BeginStatusProgress(string label, StatusType type = StatusType.Info)
    {
        statusProgress = 0f;
        statusProgressLabel = label ?? "";
        statusBarMessage = statusProgressLabel;
        statusBarTooltip = statusProgressLabel;
        statusBarType = type;
        statusPrimaryAction = StatusAction.None;
        statusActionPath = "";
        Repaint();
    }

    private void SetStatusProgress(float progress, string label = "")
    {
        statusProgress = Mathf.Clamp01(progress);

        if (!string.IsNullOrEmpty(label))
        {
            statusProgressLabel = label;
            statusBarMessage = label;
            statusBarTooltip = label;
        }

        Repaint();
    }

    private void EndStatusProgress()
    {
        statusProgress = -1f;
        statusProgressLabel = "";
        Repaint();
    }

    private void OnGUI()
    {
        EnsureStyles();
        if (!stylesReady)
        {
            EditorGUILayout.HelpBox("Editor UI is still initialising. Please wait a moment.", MessageType.Info);
            return;
        }

        DrawToolbar();

        // After toolbar Y
        float topY = GUILayoutUtility.GetLastRect().yMax + 6f;

        // Padded content area
        Rect contentRect = new Rect(
            OuterPadX,
            topY,
            position.width - (OuterPadX * 2f),
            position.height - topY - StatusBarHeight - OuterPadX
        );

        // Bottom status bar pinned to window bottom
        Rect statusRect = new Rect(
            OuterPadX,
            position.height - StatusBarHeight - OuterPadX,
            position.width - (OuterPadX * 2f),
            StatusBarHeight
        );

        // Header inside content rect
        float headerH = 22f;
        Rect headerRect = new Rect(contentRect.x, contentRect.y, contentRect.width, headerH);

        const float countW = 120f;
        const float headerGap = 6f;

        // Left: title takes everything except the count box
        Rect titleRect = new Rect(headerRect.x, headerRect.y, headerRect.width - countW - headerGap, headerRect.height);

        // Right: count box is pinned to the right edge (no gap)
        Rect countRect = new Rect(headerRect.xMax - countW, headerRect.y, countW, headerRect.height);

        GUI.Label(titleRect,
            new GUIContent("Scene Switcher", "Quick-open scenes from your list"),
            EditorStyles.boldLabel);

        var countStyle = new GUIStyle(EditorStyles.centeredGreyMiniLabel)
        {
            alignment = TextAnchor.MiddleRight
        };

        GUI.Label(countRect,
            new GUIContent($"{sceneData.sceneInfos.Count} scene(s)", "Scenes in your quick list"),
            countStyle);


        // List gets the rest (so it can’t eat the status bar)
        Rect listRect = new Rect(
            contentRect.x,
            headerRect.yMax + 6f,
            contentRect.width,
            contentRect.height - headerH - 6f
        );

        DrawSceneListRect(listRect);
        DrawStatusBar(statusRect);
    }


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

        var searchField = GUI.skin.FindStyle("ToolbarSearchTextField") ?? GUI.skin.FindStyle("ToolbarSeachTextField");
        var searchCancel = GUI.skin.FindStyle("ToolbarSearchCancelButton");
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
            EditorStyles.toolbarButton,
            GUILayout.Width(64));

        if (newShowMissingOnly != showMissingOnly)
        {
            showMissingOnly = newShowMissingOnly;
            if (showMissingOnly)
                SetStatusBarMessage("Showing missing scene assets only", 2.5, StatusType.Info, showWindowNotification: false);
            else
                ClearStatusBarMessage();
        }

        GUILayout.FlexibleSpace();

        if (GUILayout.Button(new GUIContent("Export", "Export this scene list to a JSON file"), EditorStyles.toolbarButton, GUILayout.Width(58)))
        {
            ExportSceneData();
        }

        if (GUILayout.Button(new GUIContent("Load", "Load a previously exported Scene Switcher JSON file"), EditorStyles.toolbarButton, GUILayout.Width(48)))
        {
            LoadExportedSceneData();
        }

        EditorGUILayout.EndHorizontal();
    }

    private void DrawSceneListRect(Rect listRect)
    {
        // If no items, show info directly in listRect
        if (sceneData.sceneInfos.Count == 0)
        {
            EditorGUI.HelpBox(listRect, "No scenes yet. Click + to add scenes to your quick list.", MessageType.Info);
            return;
        }

        // Build Settings lookup
        var buildSet = new HashSet<string>();
        foreach (var s in EditorBuildSettings.scenes)
        {
            if (s != null && !string.IsNullOrEmpty(s.path))
                buildSet.Add(s.path);
        }

        string q = (listSearch ?? "").Trim().ToLowerInvariant();
        bool listIsFiltered = showMissingOnly || !string.IsNullOrEmpty(q);
        int reorderControlId = GUIUtility.GetControlID(FocusType.Passive);
        Event currentEvent = Event.current;

        float viewW = listRect.width;
        float viewH = listRect.height;

        float vScrollW = (GUI.skin.verticalScrollbar != null && GUI.skin.verticalScrollbar.fixedWidth > 0)
            ? GUI.skin.verticalScrollbar.fixedWidth
            : 14f;

        // Build visible indices
        List<int> visibleIndices = new List<int>();
        for (int i = 0; i < sceneData.sceneInfos.Count; i++)
        {
            var info = sceneData.sceneInfos[i];
            if (info == null) continue;

            string name = info.sceneName ?? "";
            string path = info.scenePath ?? "";

            if (showMissingOnly && !IsMissingScene(path))
                continue;

            if (!string.IsNullOrEmpty(q))
            {
                if (!name.ToLowerInvariant().Contains(q) && !path.ToLowerInvariant().Contains(q))
                    continue;
            }

            visibleIndices.Add(i);
        }

        if (visibleIndices.Count == 0)
        {
            string emptyMessage = showMissingOnly
                ? "No missing scenes found."
                : "No matches for your search.";

            EditorGUI.HelpBox(listRect, emptyMessage, MessageType.Info);
            scrollPos = Vector2.zero;
            return;
        }

        // Determine if we need vertical scrollbar (compute once without it)
        float contentH_noScroll = CalcContentHeight(viewW, 0f, visibleIndices);
        bool needsVScroll = contentH_noScroll > viewH;

        float contentW = viewW - (needsVScroll ? vScrollW : 0f);
        float contentH = CalcContentHeight(contentW, needsVScroll ? vScrollW : 0f, visibleIndices);

        // Clamp scroll state so phantom scrollbars don’t stick around
        scrollPos.x = 0f;
        if (!needsVScroll) scrollPos.y = 0f;

        Rect contentRect = new Rect(0, 0, contentW, Mathf.Max(contentH, viewH));

        if (currentEvent.type == EventType.MouseDown && currentEvent.button == 0 && listRect.Contains(currentEvent.mousePosition))
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
                contentW - (RowMarginX * 2f) - (RowPadL + RowPadR) - LeftIconSize - GapAfterIcon - ButtonsBlockWidth - 10f
            );

            float nameH = nameStyle.CalcHeight(new GUIContent(name), textW);
            float pathH = pathStyle.CalcHeight(new GUIContent(path), textW);

            float contentInnerH = Mathf.Max(LeftIconSize, nameH + 2f + pathH);
            float rowH = RowPadT + contentInnerH + RowPadB;

            y += RowMarginY;

            Rect rowRect = new Rect(RowMarginX, y, contentW - (RowMarginX * 2f), rowH);
            GUI.Box(rowRect, GUIContent.none, rowStyle);

            if (IsSceneReorderActive() && isDraggingScene && i == draggedSceneIndex)
                EditorGUI.DrawRect(rowRect, new Color(1f, 1f, 1f, EditorGUIUtility.isProSkin ? 0.08f : 0.14f));

            Rect inner = new Rect(
                rowRect.x + RowPadL,
                rowRect.y + RowPadT,
                rowRect.width - (RowPadL + RowPadR),
                rowRect.height - (RowPadT + RowPadB)
            );

            // Icon
            Rect iconRect = new Rect(inner.x, inner.y + 1f, LeftIconSize, LeftIconSize);
            Texture2D customIconTexture = GetSceneIconTexture(info);
            if (customIconTexture != null)
            {
                GUI.DrawTexture(iconRect, customIconTexture, ScaleMode.ScaleToFit, true);
                GUI.Label(iconRect, new GUIContent("", "Custom scene icon. Left-click to change it."));
            }
            else
            {
                var sceneIcon = EditorGUIUtility.IconContent("SceneAsset Icon");
                sceneIcon.tooltip = "Scene asset. Left-click to change this icon.";
                GUI.Label(iconRect, sceneIcon);
            }

            EditorGUIUtility.AddCursorRect(iconRect, MouseCursor.Link);
            if (Event.current.type == EventType.MouseDown && Event.current.button == 0 && iconRect.Contains(Event.current.mousePosition))
            {
                ShowIconMenu(i);
                Event.current.Use();
            }

            float xText = iconRect.xMax + GapAfterIcon;
            float xButtons = inner.xMax - ButtonsBlockWidth;

            // Text rect
            Rect textRect = new Rect(xText, inner.y, xButtons - xText - 8f, inner.height);
            Rect buttonBlockRect = new Rect(xButtons, inner.y, ButtonsBlockWidth, inner.height);
            Rect reorderDragRect = new Rect(textRect.x, rowRect.y, Mathf.Max(0f, buttonBlockRect.x - textRect.x - 8f), rowRect.height);
            HandleSceneReorderEvents(reorderControlId, i, rowRect, reorderDragRect, buttonBlockRect, iconRect, listIsFiltered);

            // Title
            Rect titleRect = new Rect(textRect.x, textRect.y, textRect.width, nameH);
            EditorGUI.LabelField(titleRect, new GUIContent(name, "Scene name"), nameStyle);

            // Path (wrapped)
            Rect pRect = new Rect(textRect.x, titleRect.yMax + 2f, textRect.width, pathH);
            EditorGUI.LabelField(pRect, new GUIContent(path, "Full scene path"), pathStyle);

            DrawSceneReorderIndicator(rowRect, i, sceneData.sceneInfos.Count);

            bool missingScene = IsMissingScene(path);
            if (missingScene)
            {
                Rect missingRect = new Rect(rowRect.xMax - 18f, rowRect.y + 4f, 14f, 14f);
                GUIContent missingIcon = GetStatusIcon(StatusType.Warning);
                missingIcon.tooltip = "This scene asset was not found at the saved path";
                GUI.Label(missingRect, missingIcon);
            }

            // Buttons
            bool inBuild = buildSet.Contains(path);
            float btnY = inner.y + (inner.height - BtnH) * 0.5f;

            // 1) Open
            Rect b0 = new Rect(xButtons, btnY, BtnW, BtnH);
            GUIContent openC = EditorGUIUtility.IconContent("d_PlayButton");
            if (openC.image == null) openC = EditorGUIUtility.IconContent("PlayButton");
            if (openC.image == null) openC = new GUIContent("▶");
            openC.tooltip = missingScene
                ? "Scene asset was not found at that path"
                : "Open this scene (prompts to save modified scenes)";
            EditorGUI.BeginDisabledGroup(missingScene);
            if (GUI.Button(b0, openC, iconButtonStyle))
            {
                if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                    EditorSceneManager.OpenScene(path);
            }
            EditorGUI.EndDisabledGroup();

            // 2) Ping
            Rect b1 = new Rect(b0.xMax + BtnGap, btnY, BtnW, BtnH);
            GUIContent pingC = EditorGUIUtility.IconContent("d_Search Icon");
            if (pingC.image == null) pingC = EditorGUIUtility.IconContent("Search Icon");
            if (pingC.image == null) pingC = new GUIContent("🔍");
            pingC.tooltip = "Ping and select the scene asset in the Project window";
            if (GUI.Button(b1, pingC, iconButtonStyle))
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
                    SetStatusBarMessage("Scene asset not found at that path", 4.0, StatusType.Warning, StatusAction.ReviewMissing);
                }
            }

            // 3) Add to Build Settings
            Rect b2 = new Rect(b1.xMax + BtnGap, btnY, BtnW, BtnH);
            GUIContent buildAddC = EditorGUIUtility.IconContent("BuildSettings.Editor.Small");
            if (buildAddC.image == null) buildAddC = new GUIContent("B+");
            buildAddC.tooltip = missingScene
                ? "Scene asset was not found at that path"
                : (inBuild ? "Already in Build Settings" : "Add this scene to Build Settings");
            EditorGUI.BeginDisabledGroup(inBuild || missingScene);
            if (GUI.Button(b2, buildAddC, iconButtonStyle))
            {
                bool already = AddSceneToBuild(path);
                SetStatusBarMessage(
                    already ? $"Already in Build Settings: {name}" : $"Added to Build Settings: {name}",
                    3.5,
                    already ? StatusType.Info : StatusType.Success);
            }
            EditorGUI.EndDisabledGroup();

            // 4) Remove from Build Settings (NEW)
            Rect b3 = new Rect(b2.xMax + BtnGap, btnY, BtnW, BtnH);
            GUIContent buildRemoveC = EditorGUIUtility.IconContent("Toolbar Minus");
            if (buildRemoveC.image == null) buildRemoveC = new GUIContent("B-");
            buildRemoveC.tooltip = inBuild ? "Remove this scene from Build Settings" : "Scene is not in Build Settings";
            EditorGUI.BeginDisabledGroup(!inBuild);
            if (GUI.Button(b3, buildRemoveC, iconButtonStyle))
            {
                bool removed = RemoveSceneFromBuild(path);
                SetStatusBarMessage(
                    removed ? $"Removed from Build Settings: {name}" : $"Not found in Build Settings: {name}",
                    3.5,
                    removed ? StatusType.Success : StatusType.Warning,
                    removed ? StatusAction.None : StatusAction.ReviewMissing);
            }
            EditorGUI.EndDisabledGroup();

            // 5) Remove from list
            Rect b4 = new Rect(b3.xMax + BtnGap, btnY, BtnW, BtnH);
            GUIContent removeListC = EditorGUIUtility.IconContent("d_TreeEditor.Trash");
            if (removeListC.image == null) removeListC = EditorGUIUtility.IconContent("TreeEditor.Trash");
            if (removeListC.image == null) removeListC = new GUIContent("-");
            removeListC.tooltip = "Remove this scene from the quick list";
            if (GUI.Button(b4, removeListC, iconButtonStyle))
            {
                sceneData.RemoveScene(i);
                SetStatusBarMessage("Removed scene from list", 2.5, StatusType.Success);
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
        else if (currentEvent.type == EventType.KeyDown && currentEvent.keyCode == KeyCode.Escape && IsSceneReorderActive())
        {
            CancelSceneReorder(true);
            currentEvent.Use();
        }
    }

    private bool IsSceneReorderActive()
    {
        return sceneReorderHotControl != 0 && GUIUtility.hotControl == sceneReorderHotControl && draggedSceneIndex >= 0;
    }

    private void HandleSceneReorderEvents(
        int reorderControlId,
        int sceneIndex,
        Rect rowRect,
        Rect reorderDragRect,
        Rect buttonBlockRect,
        Rect iconRect,
        bool listIsFiltered)
    {
        Event evt = Event.current;

        if (!listIsFiltered)
            EditorGUIUtility.AddCursorRect(reorderDragRect, MouseCursor.MoveArrow);

        if (evt.type == EventType.MouseDown && evt.button == 0 && reorderDragRect.Contains(evt.mousePosition) &&
            !iconRect.Contains(evt.mousePosition) && !buttonBlockRect.Contains(evt.mousePosition))
        {
            if (listIsFiltered)
            {
                SetStatusBarMessage("Clear search and filters before reordering scenes", 3.0, StatusType.Info, showWindowNotification: false);
                evt.Use();
                return;
            }

            draggedSceneIndex = sceneIndex;
            dragInsertIndex = sceneIndex;
            dragStartMousePosition = evt.mousePosition;
            isDraggingScene = false;
            sceneReorderHotControl = reorderControlId;
            GUIUtility.hotControl = sceneReorderHotControl;
            evt.Use();
            return;
        }

        if (!IsSceneReorderActive())
            return;

        if (evt.type == EventType.MouseDrag && evt.button == 0)
        {
            if (!isDraggingScene && Vector2.Distance(evt.mousePosition, dragStartMousePosition) >= DragStartDistance)
                isDraggingScene = true;

            if (isDraggingScene && rowRect.Contains(evt.mousePosition))
            {
                UpdateSceneDragInsertIndex(sceneIndex, rowRect, evt.mousePosition);
                Repaint();
                evt.Use();
            }
        }
    }

    private void UpdateSceneDragInsertIndex(int sceneIndex, Rect rowRect, Vector2 mousePosition)
    {
        if (sceneData == null || sceneData.sceneInfos == null)
            return;

        if (rowRect.Contains(mousePosition))
        {
            dragInsertIndex = mousePosition.y < rowRect.center.y ? sceneIndex : sceneIndex + 1;
            dragInsertIndex = Mathf.Clamp(dragInsertIndex, 0, sceneData.sceneInfos.Count);
        }
    }

    private void DrawSceneReorderIndicator(Rect rowRect, int sceneIndex, int sceneCount)
    {
        if (!IsSceneReorderActive() || !isDraggingScene || dragInsertIndex < 0 || sceneIndex == draggedSceneIndex)
            return;

        if (dragInsertIndex == sceneIndex)
        {
            DrawSceneReorderIndicatorLine(rowRect.yMin, rowRect);
        }
        else if (sceneIndex == sceneCount - 1 && dragInsertIndex == sceneCount)
        {
            DrawSceneReorderIndicatorLine(rowRect.yMax, rowRect);
        }
    }

    private void DrawSceneReorderIndicatorLine(float y, Rect rowRect)
    {
        Rect lineRect = new Rect(rowRect.x + 4f, y - (ReorderIndicatorHeight * 0.5f), rowRect.width - 8f, ReorderIndicatorHeight);
        EditorGUI.DrawRect(lineRect, GetStatusColor(StatusType.Info));
    }

    private void CompleteSceneReorder()
    {
        int fromIndex = draggedSceneIndex;
        int toIndex = dragInsertIndex;
        bool wasDragging = isDraggingScene;

        CancelSceneReorder(false);

        if (!wasDragging || sceneData == null)
            return;

        bool moved = sceneData.MoveScene(fromIndex, toIndex);
        if (moved)
        {
            SaveSceneData();
            SetStatusBarMessage("Scene order updated", 2.5, StatusType.Success, showWindowNotification: false);
        }

        Repaint();
    }

    private void CancelSceneReorder(bool repaint)
    {
        if (sceneReorderHotControl != 0 && GUIUtility.hotControl == sceneReorderHotControl)
            GUIUtility.hotControl = 0;

        draggedSceneIndex = -1;
        dragInsertIndex = -1;
        sceneReorderHotControl = 0;
        dragStartMousePosition = Vector2.zero;
        isDraggingScene = false;

        if (repaint)
            Repaint();
    }


    private void ShowIconMenu(int sceneIndex)
    {
        GenericMenu menu = new GenericMenu();
        menu.AddItem(new GUIContent("Load"), false, () => LoadIconForScene(sceneIndex));
        menu.AddItem(new GUIContent("Import"), false, () => ImportIconForScene(sceneIndex));

        bool hasCustomIcon = sceneData != null && sceneData.sceneInfos != null &&
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
        List<SceneIconInfo> availableIcons = GetAvailableImportedIcons();

        if (availableIcons.Count == 0)
        {
            EditorUtility.DisplayDialog(
                "No Imported Icons",
                $"No imported icons were found. Use Import first to add images to:\n\n{GetImportedIconFolderAssetPath()}",
                "OK");
            SetStatusBarMessage("No imported icons found", 3.0, StatusType.Info, showWindowNotification: false);
            return;
        }

        string selectedIconId = "";
        if (sceneData != null && sceneData.sceneInfos != null && sceneIndex >= 0 && sceneIndex < sceneData.sceneInfos.Count && sceneData.sceneInfos[sceneIndex] != null)
            selectedIconId = sceneData.sceneInfos[sceneIndex].customIconId ?? "";

        SceneIconPickerWindow.ShowWindow(availableIcons, this, sceneIndex, selectedIconId);
    }

    private void ImportIconForScene(int sceneIndex)
    {
        string sourcePath = EditorUtility.OpenFilePanelWithFilters(
            "Import Scene Icon",
            GetLastImageImportDirectory(),
            new[] { "Image files", "png,jpg,jpeg,tga,bmp,gif,psd,tif,tiff,exr", "All files", "*" });

        if (string.IsNullOrEmpty(sourcePath))
        {
            SetStatusBarMessage("Icon import canceled", 2.0, StatusType.Info, showWindowNotification: false);
            return;
        }

        if (!IsSupportedImageExtension(sourcePath))
        {
            EditorUtility.DisplayDialog("Unsupported Image", "Please choose a PNG, JPG, JPEG, TGA, BMP, GIF, PSD, TIF, TIFF, or EXR image.", "OK");
            SetStatusBarMessage("Unsupported image type", 3.0, StatusType.Warning);
            return;
        }

        try
        {
            RememberImageImportDirectory(sourcePath);

            string iconFolder = EnsureImportedIconFolder();
            string displayName = Path.GetFileNameWithoutExtension(sourcePath);
            string iconAssetPath = AssetDatabase.GenerateUniqueAssetPath(CombineAssetPaths(iconFolder, SanitizeFileName(displayName) + ".png"));
            string iconId = Path.GetFileNameWithoutExtension(iconAssetPath);

            Texture2D fittedIcon = LoadAndFitExternalImage(sourcePath, iconFolder);
            byte[] pngBytes = fittedIcon.EncodeToPNG();
            UnityEngine.Object.DestroyImmediate(fittedIcon);

            File.WriteAllBytes(AssetPathToFullPath(iconAssetPath), pngBytes);
            AssetDatabase.ImportAsset(iconAssetPath);
            ConfigureIconImporter(iconAssetPath);

            SceneIconInfo iconInfo = new SceneIconInfo(iconId, displayName, iconAssetPath);
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
            EditorUtility.DisplayDialog("Icon Import Failed", $"Could not import the selected image.\n\n{ex.Message}", "OK");
            SetStatusBarMessage("Icon import failed", 4.0, StatusType.Error, StatusAction.Console);
        }
    }

    private void DeleteIconForScene(int sceneIndex)
    {
        if (sceneData == null || sceneData.sceneInfos == null || sceneIndex < 0 || sceneIndex >= sceneData.sceneInfos.Count || sceneData.sceneInfos[sceneIndex] == null)
            return;

        sceneData.sceneInfos[sceneIndex].customIconId = "";
        SaveSceneData();
        SetStatusBarMessage("Reverted scene icon to default", 2.5, StatusType.Success);
        Repaint();
    }

    internal void ApplyImportedIconToScene(int sceneIndex, string iconId, bool saveAfterApply = true)
    {
        if (sceneData == null || sceneData.sceneInfos == null || sceneIndex < 0 || sceneIndex >= sceneData.sceneInfos.Count || sceneData.sceneInfos[sceneIndex] == null)
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
        if (info == null || string.IsNullOrEmpty(info.customIconId))
            return null;

        SceneIconInfo iconInfo = FindImportedIconById(info.customIconId);
        if (iconInfo == null || string.IsNullOrEmpty(iconInfo.assetPath))
            return null;

        Texture2D cached;
        if (iconTextureCache.TryGetValue(iconInfo.assetPath, out cached) && cached != null)
            return cached;

        Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(iconInfo.assetPath);
        if (texture != null)
            iconTextureCache[iconInfo.assetPath] = texture;

        return texture;
    }

    private SceneIconInfo FindImportedIconById(string iconId)
    {
        if (sceneData == null || sceneData.importedImages == null || string.IsNullOrEmpty(iconId))
            return null;

        for (int i = 0; i < sceneData.importedImages.Count; i++)
        {
            SceneIconInfo icon = sceneData.importedImages[i];
            if (icon != null && icon.id == iconId)
                return icon;
        }

        return null;
    }

    private void DiscoverImportedIcons()
    {
        EnsureSceneDataLists(sceneData);
        if (sceneData == null)
            return;

        string folder = GetImportedIconFolderAssetPath();
        List<SceneIconInfo> discoveredIcons = new List<SceneIconInfo>();

        if (AssetDatabase.IsValidFolder(folder))
        {
            string[] textureGuids = AssetDatabase.FindAssets("t:Texture2D", new[] { folder });
            for (int i = 0; i < textureGuids.Length; i++)
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(textureGuids[i]);
                if (string.IsNullOrEmpty(assetPath) || !IsSupportedImageExtension(assetPath))
                    continue;

                string id = Path.GetFileNameWithoutExtension(assetPath);
                string displayName = id;
                SceneIconInfo existing = FindImportedIconById(id);
                if (existing != null && !string.IsNullOrEmpty(existing.displayName))
                    displayName = existing.displayName;

                discoveredIcons.Add(new SceneIconInfo(id, displayName, assetPath));
            }
        }

        sceneData.importedImages = discoveredIcons;
        iconTextureCache.Clear();
    }

    private List<SceneIconInfo> GetAvailableImportedIcons()
    {
        List<SceneIconInfo> availableIcons = new List<SceneIconInfo>();
        if (sceneData == null || sceneData.importedImages == null)
            return availableIcons;

        for (int i = 0; i < sceneData.importedImages.Count; i++)
        {
            SceneIconInfo icon = sceneData.importedImages[i];
            if (icon == null || string.IsNullOrEmpty(icon.assetPath))
                continue;

            if (AssetDatabase.LoadAssetAtPath<Texture2D>(icon.assetPath) != null)
                availableIcons.Add(icon);
        }

        return availableIcons;
    }

    private bool HasImportedIcons()
    {
        return GetAvailableImportedIcons().Count > 0;
    }

    private void AddOrUpdateImportedIcon(SceneIconInfo iconInfo)
    {
        EnsureSceneDataLists(sceneData);
        if (sceneData == null || iconInfo == null || string.IsNullOrEmpty(iconInfo.id))
            return;

        for (int i = 0; i < sceneData.importedImages.Count; i++)
        {
            if (sceneData.importedImages[i] != null && sceneData.importedImages[i].id == iconInfo.id)
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

        List<SceneIconInfo> availableIcons = GetAvailableImportedIcons();
        for (int i = 0; i < availableIcons.Count; i++)
        {
            SceneIconInfo icon = availableIcons[i];
            string fullPath = AssetPathToFullPath(icon.assetPath);
            if (string.IsNullOrEmpty(fullPath) || !File.Exists(fullPath))
                continue;

            byte[] imageBytes = File.ReadAllBytes(fullPath);
            string iconFileName = SanitizeFileName(icon.id) + ".png";

            dataToExport.importedImages.Add(new SceneIconInfo(icon.id, icon.displayName, CombineAssetPaths(GetImportedIconFolderAssetPath(), iconFileName)));
            dataToExport.embeddedImages.Add(new SceneIconEmbeddedData(icon.id, icon.displayName, iconFileName, System.Convert.ToBase64String(imageBytes)));
        }

        return dataToExport.embeddedImages.Count;
    }

    private void ImportEmbeddedImagesFromData(SceneData data, bool showStatus)
    {
        EnsureSceneDataLists(data);
        if (data == null || data.embeddedImages == null || data.embeddedImages.Count == 0)
            return;

        string iconFolder = EnsureImportedIconFolder();
        int restoredCount = 0;

        for (int i = 0; i < data.embeddedImages.Count; i++)
        {
            SceneIconEmbeddedData embedded = data.embeddedImages[i];
            if (embedded == null || string.IsNullOrEmpty(embedded.id) || string.IsNullOrEmpty(embedded.imageDataBase64))
                continue;

            try
            {
                byte[] imageBytes = System.Convert.FromBase64String(embedded.imageDataBase64);
                string fileName = SanitizeFileName(embedded.id) + ".png";
                string assetPath = CombineAssetPaths(iconFolder, fileName);
                File.WriteAllBytes(AssetPathToFullPath(assetPath), imageBytes);
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
            SetStatusBarMessage($"Restored {restoredCount} imported icon{(restoredCount == 1 ? "" : "s")}", 3.0, StatusType.Success);
    }

    private void AddOrUpdateIconInData(SceneData data, SceneIconInfo iconInfo)
    {
        EnsureSceneDataLists(data);
        if (data == null || iconInfo == null || string.IsNullOrEmpty(iconInfo.id))
            return;

        for (int i = 0; i < data.importedImages.Count; i++)
        {
            if (data.importedImages[i] != null && data.importedImages[i].id == iconInfo.id)
            {
                data.importedImages[i] = iconInfo;
                return;
            }
        }

        data.importedImages.Add(iconInfo);
    }

    private Texture2D LoadAndFitExternalImage(string sourcePath, string tempFolderAssetPath)
    {
        Texture2D sourceTexture = null;
        Texture2D directTexture = new Texture2D(2, 2, TextureFormat.RGBA32, false);

        try
        {
            byte[] sourceBytes = File.ReadAllBytes(sourcePath);
            if (directTexture.LoadImage(sourceBytes))
            {
                sourceTexture = directTexture;
                directTexture = null;
            }
        }
        catch
        {
            // Fall back to Unity's asset importer below for formats Texture2D.LoadImage cannot read directly.
        }
        finally
        {
            if (directTexture != null)
                UnityEngine.Object.DestroyImmediate(directTexture);
        }

        string tempAssetPath = "";
        if (sourceTexture == null)
        {
            string extension = Path.GetExtension(sourcePath).ToLowerInvariant();
            tempAssetPath = AssetDatabase.GenerateUniqueAssetPath(CombineAssetPaths(tempFolderAssetPath, "__SceneSwitcherIconImportTemp" + extension));
            File.Copy(sourcePath, AssetPathToFullPath(tempAssetPath), true);
            AssetDatabase.ImportAsset(tempAssetPath);

            TextureImporter tempImporter = AssetImporter.GetAtPath(tempAssetPath) as TextureImporter;
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

        Texture2D fittedTexture = CreateFittedIconTexture(sourceTexture);

        if (!string.IsNullOrEmpty(tempAssetPath))
            AssetDatabase.DeleteAsset(tempAssetPath);
        else if (sourceTexture != null)
            UnityEngine.Object.DestroyImmediate(sourceTexture);

        return fittedTexture;
    }

    private Texture2D CreateFittedIconTexture(Texture2D sourceTexture)
    {
        if (sourceTexture == null || sourceTexture.width <= 0 || sourceTexture.height <= 0)
            throw new InvalidDataException("The selected image does not have a valid size.");

        float scale = Mathf.Min((float)ImportedIconSize / sourceTexture.width, (float)ImportedIconSize / sourceTexture.height);
        int targetWidth = Mathf.Clamp(Mathf.RoundToInt(sourceTexture.width * scale), 1, ImportedIconSize);
        int targetHeight = Mathf.Clamp(Mathf.RoundToInt(sourceTexture.height * scale), 1, ImportedIconSize);

        RenderTexture previous = RenderTexture.active;
        RenderTexture renderTexture = RenderTexture.GetTemporary(targetWidth, targetHeight, 0, RenderTextureFormat.ARGB32);
        Texture2D scaledTexture = new Texture2D(targetWidth, targetHeight, TextureFormat.RGBA32, false);

        try
        {
            Graphics.Blit(sourceTexture, renderTexture);
            RenderTexture.active = renderTexture;
            scaledTexture.ReadPixels(new Rect(0, 0, targetWidth, targetHeight), 0, 0);
            scaledTexture.Apply();
        }
        finally
        {
            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(renderTexture);
        }

        Texture2D fittedTexture = new Texture2D(ImportedIconSize, ImportedIconSize, TextureFormat.RGBA32, false);
        Color[] clearPixels = new Color[ImportedIconSize * ImportedIconSize];
        for (int i = 0; i < clearPixels.Length; i++)
            clearPixels[i] = new Color(0f, 0f, 0f, 0f);

        fittedTexture.SetPixels(clearPixels);

        int offsetX = (ImportedIconSize - targetWidth) / 2;
        int offsetY = (ImportedIconSize - targetHeight) / 2;

        for (int y = 0; y < targetHeight; y++)
        {
            for (int x = 0; x < targetWidth; x++)
                fittedTexture.SetPixel(offsetX + x, offsetY + y, scaledTexture.GetPixel(x, y));
        }

        fittedTexture.Apply();
        UnityEngine.Object.DestroyImmediate(scaledTexture);
        return fittedTexture;
    }

    private void ConfigureIconImporter(string assetPath)
    {
        TextureImporter importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
        if (importer == null)
            return;

        importer.isReadable = true;
        importer.mipmapEnabled = false;
        importer.alphaIsTransparency = true;
        importer.npotScale = TextureImporterNPOTScale.None;
        importer.SaveAndReimport();
    }

    private string EnsureImportedIconFolder()
    {
        string folder = GetImportedIconFolderAssetPath();
        if (AssetDatabase.IsValidFolder(folder))
            return folder;

        string parent = GetScriptFolderAssetPath();
        if (!AssetDatabase.IsValidFolder(parent))
            parent = "Assets";

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
    {
        return CombineAssetPaths(GetScriptFolderAssetPath(), ImportedIconFolderName);
    }

    private string GetScriptFolderAssetPath()
    {
        string scriptPath = "";

        try
        {
            MonoScript script = MonoScript.FromScriptableObject(this);
            if (script != null)
                scriptPath = AssetDatabase.GetAssetPath(script);
        }
        catch
        {
            scriptPath = "";
        }

        if (string.IsNullOrEmpty(scriptPath))
        {
            string[] scriptGuids = AssetDatabase.FindAssets("SceneSwitcher t:Script");
            for (int i = 0; i < scriptGuids.Length; i++)
            {
                string candidatePath = AssetDatabase.GUIDToAssetPath(scriptGuids[i]);
                if (Path.GetFileName(candidatePath) == "SceneSwitcher.cs")
                {
                    scriptPath = candidatePath;
                    break;
                }
            }
        }

        if (string.IsNullOrEmpty(scriptPath))
            return "Assets";

        string folder = Path.GetDirectoryName(scriptPath);
        if (string.IsNullOrEmpty(folder))
            return "Assets";

        return NormalizeAssetPath(folder);
    }

    private string AssetPathToFullPath(string assetPath)
    {
        string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        return Path.GetFullPath(Path.Combine(projectRoot, assetPath));
    }

    private string CombineAssetPaths(string left, string right)
    {
        if (string.IsNullOrEmpty(left))
            return NormalizeAssetPath(right);

        if (string.IsNullOrEmpty(right))
            return NormalizeAssetPath(left);

        return NormalizeAssetPath(left.TrimEnd('/', '\\') + "/" + right.TrimStart('/', '\\'));
    }

    private string NormalizeAssetPath(string path)
    {
        return (path ?? "").Replace('\\', '/');
    }

    private string SanitizeFileName(string fileName)
    {
        string safe = string.IsNullOrEmpty(fileName) ? "Icon" : fileName;
        char[] invalidChars = Path.GetInvalidFileNameChars();
        for (int i = 0; i < invalidChars.Length; i++)
            safe = safe.Replace(invalidChars[i], '_');

        safe = safe.Trim();
        if (string.IsNullOrEmpty(safe))
            safe = "Icon";

        return safe.Length > 80 ? safe.Substring(0, 80) : safe;
    }

    private bool IsSupportedImageExtension(string pathOrExtension)
    {
        string extension = Path.GetExtension(pathOrExtension);
        if (string.IsNullOrEmpty(extension) && !string.IsNullOrEmpty(pathOrExtension) && pathOrExtension.StartsWith("."))
            extension = pathOrExtension;

        extension = (extension ?? "").TrimStart('.').ToLowerInvariant();

        switch (extension)
        {
            case "png":
            case "jpg":
            case "jpeg":
            case "tga":
            case "bmp":
            case "gif":
            case "psd":
            case "tif":
            case "tiff":
            case "exr":
                return true;
            default:
                return false;
        }
    }

    private bool RemoveSceneFromBuild(string scenePath)
    {
        var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);

        int index = scenes.FindIndex(s => s.path == scenePath);
        if (index < 0)
            return false;

        scenes.RemoveAt(index);
        EditorBuildSettings.scenes = scenes.ToArray();
        return true;
    }


    private float CalcContentHeight(float availableW, float vScrollW, List<int> visibleIndices)
    {
        float contentW = availableW;
        float y = 0f;

        for (int vi = 0; vi < visibleIndices.Count; vi++)
        {
            var info = sceneData.sceneInfos[visibleIndices[vi]];
            if (info == null) continue;

            string name = info.sceneName ?? "";
            string path = info.scenePath ?? "";

            float textW = Mathf.Max(
                140f,
                contentW - (RowMarginX * 2f) - (RowPadL + RowPadR) - LeftIconSize - GapAfterIcon - ButtonsBlockWidth - 10f
            );

            float nameH = nameStyle.CalcHeight(new GUIContent(name), textW);
            float pathH = pathStyle.CalcHeight(new GUIContent(path), textW);

            float contentInnerH = Mathf.Max(LeftIconSize, nameH + 2f + pathH);
            float rowH = RowPadT + contentInnerH + RowPadB;

            y += RowMarginY + rowH + RowMarginY + RowGapY;
        }

        return Mathf.Max(0f, y);
    }

    private void DrawStatusBar(Rect r)
    {
        SceneHealth health = GetSceneHealth();
        bool hasMessage = !string.IsNullOrEmpty(statusBarMessage);
        bool hasProgress = statusProgress >= 0f;

        StatusType displayType = hasMessage || hasProgress
            ? statusBarType
            : (health.missing > 0 ? StatusType.Warning : StatusType.Ready);

        string msg = hasProgress
            ? (string.IsNullOrEmpty(statusProgressLabel) ? statusBarMessage : statusProgressLabel)
            : (hasMessage ? statusBarMessage : GetReadyStatusText(health));

        string tooltip = string.IsNullOrEmpty(statusBarTooltip) ? msg : statusBarTooltip;

        // Background
        GUI.Box(r, GUIContent.none, EditorStyles.helpBox);
        EditorGUI.DrawRect(new Rect(r.x + 2f, r.y + 2f, 3f, r.height - 4f), GetStatusColor(displayType));

        // Inner layout
        Rect inner = new Rect(r.x + 8f, r.y + 4f, r.width - 16f, r.height - 8f);
        float xMax = inner.xMax;

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
                if (DrawStatusButton(ref xMax, inner, "Clean", "Remove missing scene entries after confirmation", 54f))
                    CleanMissingSceneEntries();

                if (DrawStatusButton(ref xMax, inner, "Review", "Show only missing scene entries", 58f))
                    ReviewMissingScenes();
            }
            else
            {
                if (DrawStatusButton(ref xMax, inner, "Validate", "Check scene paths and Build Settings status", 66f))
                    ValidateSceneList();
            }
        }

        Rect iconRect = new Rect(inner.x, inner.y + 1f, 16f, inner.height - 2f);
        GUIContent icon = GetStatusIcon(displayType);
        icon.tooltip = tooltip;
        GUI.Label(iconRect, icon);

        Rect labelRect = new Rect(iconRect.xMax + 5f, inner.y, Mathf.Max(0f, xMax - iconRect.xMax - 9f), inner.height);

        if (hasProgress)
        {
            EditorGUI.ProgressBar(labelRect, Mathf.Clamp01(statusProgress), msg);
        }
        else
        {
            EditorGUI.LabelField(labelRect, new GUIContent(msg, tooltip), EditorStyles.label);
        }
    }

    private bool DrawStatusButton(ref float xMax, Rect inner, string label, string tooltip, float width)
    {
        xMax -= width;
        Rect buttonRect = new Rect(xMax, inner.y, width, inner.height);
        bool clicked = GUI.Button(buttonRect, new GUIContent(label, tooltip), EditorStyles.miniButton);
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
                if (DrawStatusButton(ref xMax, inner, "Clean", "Remove missing scene entries after confirmation", 54f))
                    HandleStatusAction(StatusAction.CleanMissing);
                break;

            case StatusAction.Validate:
                if (DrawStatusButton(ref xMax, inner, "Validate", "Check scene paths and Build Settings status", 66f))
                    HandleStatusAction(StatusAction.Validate);
                break;

            case StatusAction.UndoLoad:
                if (DrawStatusButton(ref xMax, inner, "Undo", "Restore the scene list from before the last load", 52f))
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

            case StatusAction.ReviewMissing:
                ReviewMissingScenes();
                break;

            case StatusAction.CleanMissing:
                CleanMissingSceneEntries();
                break;

            case StatusAction.Validate:
                ValidateSceneList();
                break;

            case StatusAction.UndoLoad:
                UndoLastLoadedSceneData();
                break;
        }
    }

    private GUIContent GetStatusIcon(StatusType type)
    {
        switch (type)
        {
            case StatusType.Ready:
            case StatusType.Success:
                return TryIconContent("TestPassed", "CollabNew", "d_TestPassed", "d_CollabNew", "console.infoicon");

            case StatusType.Warning:
                return TryIconContent("console.warnicon", "d_console.warnicon", "console.warnicon.sml", "d_console.warnicon.sml");

            case StatusType.Error:
                return TryIconContent("console.erroricon", "d_console.erroricon", "console.erroricon.sml", "d_console.erroricon.sml");

            case StatusType.Info:
            default:
                return TryIconContent("console.infoicon", "d_console.infoicon", "console.infoicon.sml", "d_console.infoicon.sml");
        }
    }

    private GUIContent TryIconContent(params string[] iconNames)
    {
        foreach (string iconName in iconNames)
        {
            GUIContent content = EditorGUIUtility.IconContent(iconName);
            if (content != null && content.image != null)
                return content;
        }

        return new GUIContent("•");
    }

    private Color GetStatusColor(StatusType type)
    {
        float alpha = EditorGUIUtility.isProSkin ? 0.8f : 0.65f;

        switch (type)
        {
            case StatusType.Success:
            case StatusType.Ready:
                return new Color(0.18f, 0.65f, 0.28f, alpha);

            case StatusType.Warning:
                return new Color(0.95f, 0.68f, 0.15f, alpha);

            case StatusType.Error:
                return new Color(0.85f, 0.22f, 0.18f, alpha);

            case StatusType.Info:
            default:
                return new Color(0.25f, 0.48f, 0.95f, alpha);
        }
    }

    private string GetReadyStatusText(SceneHealth health)
    {
        return $"Ready • {health.total} scene(s) • {health.inBuildSettings} in Build Settings • {health.missing} missing";
    }

    private SceneHealth GetSceneHealth()
    {
        SceneHealth health = new SceneHealth();

        if (sceneData == null || sceneData.sceneInfos == null)
            return health;

        HashSet<string> buildSet = GetBuildSettingsScenePathSet();

        for (int i = 0; i < sceneData.sceneInfos.Count; i++)
        {
            SceneInfo info = sceneData.sceneInfos[i];
            if (info == null)
                continue;

            health.total++;

            string path = info.scenePath ?? "";

            if (buildSet.Contains(path))
                health.inBuildSettings++;

            if (IsMissingScene(path))
                health.missing++;
        }

        return health;
    }

    private HashSet<string> GetBuildSettingsScenePathSet()
    {
        HashSet<string> buildSet = new HashSet<string>();

        foreach (EditorBuildSettingsScene scene in EditorBuildSettings.scenes)
        {
            if (scene != null && !string.IsNullOrEmpty(scene.path))
                buildSet.Add(scene.path);
        }

        return buildSet;
    }

    private bool IsMissingScene(string scenePath)
    {
        return string.IsNullOrEmpty(scenePath) || AssetDatabase.LoadAssetAtPath<SceneAsset>(scenePath) == null;
    }

    private List<int> GetMissingSceneIndices()
    {
        List<int> missingIndices = new List<int>();

        if (sceneData == null || sceneData.sceneInfos == null)
            return missingIndices;

        for (int i = 0; i < sceneData.sceneInfos.Count; i++)
        {
            SceneInfo info = sceneData.sceneInfos[i];
            if (info == null || IsMissingScene(info.scenePath))
                missingIndices.Add(i);
        }

        return missingIndices;
    }

    private void ReviewMissingScenes()
    {
        showMissingOnly = true;
        listSearch = "";
        SetStatusBarMessage("Showing missing scene assets only", 3.0, StatusType.Info, showWindowNotification: false);
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
            "Remove",
            "Cancel");

        if (!clean)
        {
            SetStatusBarMessage("Clean canceled", 2.0, StatusType.Info, showWindowNotification: false);
            return;
        }

        for (int i = missingIndices.Count - 1; i >= 0; i--)
            sceneData.RemoveScene(missingIndices[i]);

        showMissingOnly = false;
        SaveSceneData();

        SetStatusBarMessage(
            $"Removed {missingIndices.Count} missing scene entr{(missingIndices.Count == 1 ? "y" : "ies")}",
            4.0,
            StatusType.Success);

        Repaint();
        GUIUtility.ExitGUI();
    }

    private void ValidateSceneList()
    {
        BeginStatusProgress("Validating scenes...", StatusType.Info);

        SceneHealth health = GetSceneHealth();
        SetStatusProgress(1f, "Validation complete");
        EndStatusProgress();

        if (health.missing > 0)
        {
            SetStatusBarMessage(
                $"Validation complete: {health.missing} missing scene asset{(health.missing == 1 ? "" : "s")}",
                5.0,
                StatusType.Warning,
                StatusAction.ReviewMissing);
        }
        else
        {
            SetStatusBarMessage(
                $"Validation complete: {health.total} scene{(health.total == 1 ? "" : "s")} OK",
                4.0,
                StatusType.Success);
        }
    }

    private SceneData CloneSceneData(SceneData source)
    {
        if (source == null)
            return new SceneData();

        SceneData clone = JsonUtility.FromJson<SceneData>(JsonUtility.ToJson(source)) ?? new SceneData();
        EnsureSceneDataLists(clone);
        return clone;
    }

    private void UndoLastLoadedSceneData()
    {
        if (lastSceneDataBeforeLoad == null)
        {
            SetStatusBarMessage("Nothing to undo", 2.0, StatusType.Info, showWindowNotification: false);
            return;
        }

        sceneData = CloneSceneData(lastSceneDataBeforeLoad);
        lastSceneDataBeforeLoad = null;
        showMissingOnly = false;
        SaveSceneData();

        SetStatusBarMessage("Restored the previous scene list", 4.0, StatusType.Success);
        Repaint();
    }


    private bool AddSceneToBuild(string scenePath)
    {
        List<EditorBuildSettingsScene> buildScenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);

        bool sceneAlreadyInBuild = false;
        foreach (var buildScene in buildScenes)
        {
            if (buildScene.path == scenePath)
            {
                sceneAlreadyInBuild = true;
                break;
            }
        }

        if (!sceneAlreadyInBuild)
        {
            buildScenes.Add(new EditorBuildSettingsScene(scenePath, true));
            EditorBuildSettings.scenes = buildScenes.ToArray();
        }

        return sceneAlreadyInBuild;
    }
}


public class SceneIconPickerWindow : EditorWindow
{
    private readonly List<SceneIconInfo> icons = new List<SceneIconInfo>();
    private SceneSwitcher sceneSwitcher;
    private int sceneIndex;
    private string selectedIconId = "";
    private string searchQuery = "";
    private Vector2 scrollPos;

    private GUIStyle rowStyle;
    private GUIStyle nameStyle;
    private GUIStyle pathStyle;
    private bool stylesReady;

    public static void ShowWindow(List<SceneIconInfo> icons, SceneSwitcher sceneSwitcher, int sceneIndex, string selectedIconId)
    {
        var window = CreateInstance<SceneIconPickerWindow>();
        window.icons.Clear();
        if (icons != null)
            window.icons.AddRange(icons);

        window.sceneSwitcher = sceneSwitcher;
        window.sceneIndex = sceneIndex;
        window.selectedIconId = selectedIconId ?? "";
        window.titleContent = new GUIContent("Load Icon", "Choose an already imported icon");
        window.minSize = new Vector2(420, 320);
        window.ShowUtility();
    }

    private void EnsureStyles()
    {
        if (stylesReady) return;

        if (EditorStyles.label == null || EditorStyles.wordWrappedMiniLabel == null)
            return;

        rowStyle = new GUIStyle("HelpBox")
        {
            padding = new RectOffset(8, 8, 6, 6),
            margin = new RectOffset(6, 6, 4, 4)
        };

        nameStyle = new GUIStyle(EditorStyles.label)
        {
            fontStyle = FontStyle.Bold,
            wordWrap = false,
            clipping = TextClipping.Clip
        };

        pathStyle = new GUIStyle(EditorStyles.wordWrappedMiniLabel)
        {
            wordWrap = true,
            clipping = TextClipping.Clip
        };

        stylesReady = true;
    }

    private void OnGUI()
    {
        EnsureStyles();
        if (!stylesReady)
        {
            EditorGUILayout.HelpBox("Editor UI is still initialising. Please wait a moment.", MessageType.Info);
            return;
        }

        DrawToolbar();
        EditorGUILayout.Space(6);

        if (icons.Count == 0)
        {
            EditorGUILayout.HelpBox("No imported icons were found.", MessageType.Info);
            return;
        }

        string q = (searchQuery ?? "").Trim().ToLowerInvariant();
        scrollPos = EditorGUILayout.BeginScrollView(scrollPos);

        bool anyVisible = false;
        for (int i = 0; i < icons.Count; i++)
        {
            SceneIconInfo icon = icons[i];
            if (icon == null)
                continue;

            string displayName = icon.displayName ?? "";
            string assetPath = icon.assetPath ?? "";

            if (!string.IsNullOrEmpty(q) && !displayName.ToLowerInvariant().Contains(q) && !assetPath.ToLowerInvariant().Contains(q))
                continue;

            anyVisible = true;
            EditorGUILayout.BeginHorizontal(rowStyle);

            Rect iconRect = GUILayoutUtility.GetRect(43f, 43f, GUILayout.Width(43f), GUILayout.Height(43f));
            Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
            if (texture != null)
                GUI.DrawTexture(iconRect, texture, ScaleMode.ScaleToFit, true);
            else
                GUI.Label(iconRect, EditorGUIUtility.IconContent("SceneAsset Icon"));

            EditorGUILayout.BeginVertical(GUILayout.ExpandWidth(true));
            string selectedPrefix = icon.id == selectedIconId ? "✓ " : "";
            GUILayout.Label(new GUIContent(selectedPrefix + displayName, "Imported icon name"), nameStyle);
            GUILayout.Label(new GUIContent(assetPath, "Imported icon asset path"), pathStyle);
            EditorGUILayout.EndVertical();

            GUILayout.FlexibleSpace();

            if (GUILayout.Button(new GUIContent("Use", "Use this icon for the scene"), EditorStyles.miniButton, GUILayout.Width(48), GUILayout.Height(24)))
            {
                if (sceneSwitcher != null)
                    sceneSwitcher.ApplyImportedIconToScene(sceneIndex, icon.id);

                Close();
                GUIUtility.ExitGUI();
            }

            EditorGUILayout.EndHorizontal();
        }

        if (!anyVisible)
            EditorGUILayout.HelpBox("No imported icons match your search.", MessageType.Info);

        EditorGUILayout.EndScrollView();
    }

    private void DrawToolbar()
    {
        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

        GUILayout.Label(new GUIContent("Search", "Filter by icon name or path"), GUILayout.Width(50));

        var searchField = GUI.skin.FindStyle("ToolbarSearchTextField") ?? GUI.skin.FindStyle("ToolbarSeachTextField");
        var searchCancel = GUI.skin.FindStyle("ToolbarSearchCancelButton");
        var searchCancelEmpty = GUI.skin.FindStyle("ToolbarSearchCancelButtonEmpty");

        searchQuery = GUILayout.TextField(searchQuery, searchField, GUILayout.MinWidth(160));

        var cancelStyle = string.IsNullOrEmpty(searchQuery) ? searchCancelEmpty : searchCancel;
        if (GUILayout.Button(new GUIContent("", "Clear search"), cancelStyle))
        {
            searchQuery = "";
            GUI.FocusControl(null);
        }

        GUILayout.FlexibleSpace();

        if (GUILayout.Button(new GUIContent("Close", "Close this window"), EditorStyles.toolbarButton, GUILayout.Width(50)))
            Close();

        EditorGUILayout.EndHorizontal();
    }
}

public class SceneSelectorWindow : EditorWindow
{
    private string[] sceneGUIDs;
    private readonly List<string> filteredSceneNames = new List<string>();
    private readonly List<string> filteredSceneGUIDs = new List<string>();

    private SceneData sceneData;
    private SceneSwitcher sceneSwitcher;

    private string searchQuery = "";
    private Vector2 scrollPos;

    private bool stylesReady;
    private GUIStyle rowStyle;
    private GUIStyle nameStyle;
    private GUIStyle pathStyle;
    private GUIStyle iconButtonStyle;

    private const float OuterPadX = 5f;

    public static void ShowWindow(string[] sceneGUIDs, SceneData sceneData, SceneSwitcher sceneSwitcher)
    {
        var window = CreateInstance<SceneSelectorWindow>();
        window.sceneGUIDs = sceneGUIDs;
        window.sceneData = sceneData;
        window.sceneSwitcher = sceneSwitcher;
        window.titleContent = new GUIContent("Select Scene", "Pick a scene to add to the quick list");
        window.minSize = new Vector2(460, 420);
        window.ShowUtility();
    }

    private void EnsureStyles()
    {
        if (stylesReady) return;

        if (EditorStyles.label == null || EditorStyles.wordWrappedMiniLabel == null)
            return;

        rowStyle = new GUIStyle("HelpBox")
        {
            padding = new RectOffset(8, 8, 6, 6),
            margin = new RectOffset(6, 6, 4, 4)
        };

        nameStyle = new GUIStyle(EditorStyles.label)
        {
            fontStyle = FontStyle.Bold,
            wordWrap = false
        };

        pathStyle = new GUIStyle(EditorStyles.wordWrappedMiniLabel)
        {
            wordWrap = true
        };

        iconButtonStyle = new GUIStyle(EditorStyles.miniButton)
        {
            fixedWidth = 28,
            fixedHeight = 22,
            margin = new RectOffset(2, 2, 0, 0),
            padding = new RectOffset(0, 0, 0, 0)
        };

        stylesReady = true;
    }

    private void OnGUI()
    {
        EnsureStyles();
        if (!stylesReady)
        {
            EditorGUILayout.HelpBox("Editor UI is still initialising. Please wait a moment.", MessageType.Info);
            return;
        }

        DrawToolbar();

        EditorGUILayout.Space(6);

        EditorGUILayout.BeginHorizontal();
        GUILayout.Space(OuterPadX);
        EditorGUILayout.BeginVertical();

        filteredSceneNames.Clear();
        filteredSceneGUIDs.Clear();

        string q = (searchQuery ?? "").Trim().ToLowerInvariant();

        for (int i = 0; i < sceneGUIDs.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(sceneGUIDs[i]);
            string sceneName = Path.GetFileNameWithoutExtension(path);

            if (string.IsNullOrEmpty(q) ||
                sceneName.ToLowerInvariant().Contains(q) ||
                path.ToLowerInvariant().Contains(q))
            {
                filteredSceneNames.Add(sceneName);
                filteredSceneGUIDs.Add(sceneGUIDs[i]);
            }
        }

        scrollPos = EditorGUILayout.BeginScrollView(scrollPos); // only shows scrollbars when needed

        if (filteredSceneNames.Count == 0)
        {
            EditorGUILayout.HelpBox("No scenes found for that search.", MessageType.Info);
        }
        else
        {
            for (int i = 0; i < filteredSceneNames.Count; i++)
            {
                string guid = filteredSceneGUIDs[i];
                string path = AssetDatabase.GUIDToAssetPath(guid);
                string name = filteredSceneNames[i];

                EditorGUILayout.BeginHorizontal(rowStyle);

                var sceneIcon = EditorGUIUtility.IconContent("SceneAsset Icon");
                sceneIcon.tooltip = "Scene asset";
                GUILayout.Label(sceneIcon, GUILayout.Width(18), GUILayout.Height(18));

                EditorGUILayout.BeginVertical(GUILayout.ExpandWidth(true));
                GUILayout.Label(new GUIContent(name, "Scene name"), nameStyle);
                GUILayout.Label(new GUIContent(path, "Full scene path"), pathStyle);
                EditorGUILayout.EndVertical();

                GUILayout.FlexibleSpace();

                var addContent = EditorGUIUtility.IconContent("Toolbar Plus");
                addContent.tooltip = "Add this scene to the Scene Switcher list";
                if (GUILayout.Button(addContent, iconButtonStyle))
                {
                    if (sceneData.SceneExists(path))
                    {
                        sceneSwitcher.SetStatusBarMessage("Scene already in the list", 3, SceneSwitcher.StatusType.Info);
                    }
                    else
                    {
                        sceneData.AddScene(name, path);
                        sceneSwitcher.SetStatusBarMessage("Scene added to the list", 3, SceneSwitcher.StatusType.Success);
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

    private void DrawToolbar()
    {
        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

        GUILayout.Label(new GUIContent("Search", "Filter by scene name or path"), GUILayout.Width(50));

        var searchField = GUI.skin.FindStyle("ToolbarSearchTextField") ?? GUI.skin.FindStyle("ToolbarSeachTextField");
        var searchCancel = GUI.skin.FindStyle("ToolbarSearchCancelButton");
        var searchCancelEmpty = GUI.skin.FindStyle("ToolbarSearchCancelButtonEmpty");

        searchQuery = GUILayout.TextField(searchQuery, searchField, GUILayout.MinWidth(160));

        var cancelStyle = string.IsNullOrEmpty(searchQuery) ? searchCancelEmpty : searchCancel;
        if (GUILayout.Button(new GUIContent("", "Clear search"), cancelStyle))
        {
            searchQuery = "";
            GUI.FocusControl(null);
        }

        GUILayout.FlexibleSpace();

        if (GUILayout.Button(new GUIContent("Close", "Close this window"), EditorStyles.toolbarButton, GUILayout.Width(50)))
            Close();

        EditorGUILayout.EndHorizontal();
    }
}

[System.Serializable]
public class SceneData
{
    public List<SceneInfo> sceneInfos = new List<SceneInfo>();
    public List<SceneIconInfo> importedImages = new List<SceneIconInfo>();
    public List<SceneIconEmbeddedData> embeddedImages = new List<SceneIconEmbeddedData>();

    public void AddScene(string sceneName, string scenePath)
    {
        if (sceneInfos == null) sceneInfos = new List<SceneInfo>();
        sceneInfos.Add(new SceneInfo(sceneName, scenePath));
    }

    public void RemoveScene(int index)
    {
        if (sceneInfos != null && index >= 0 && index < sceneInfos.Count)
            sceneInfos.RemoveAt(index);
    }

    public bool MoveScene(int fromIndex, int insertIndex)
    {
        if (sceneInfos == null || fromIndex < 0 || fromIndex >= sceneInfos.Count)
            return false;

        insertIndex = Mathf.Clamp(insertIndex, 0, sceneInfos.Count);
        if (insertIndex == fromIndex || insertIndex == fromIndex + 1)
            return false;

        SceneInfo scene = sceneInfos[fromIndex];
        sceneInfos.RemoveAt(fromIndex);

        if (insertIndex > fromIndex)
            insertIndex--;

        insertIndex = Mathf.Clamp(insertIndex, 0, sceneInfos.Count);
        sceneInfos.Insert(insertIndex, scene);
        return true;
    }

    public bool SceneExists(string scenePath)
    {
        if (sceneInfos == null) return false;
        return sceneInfos.Exists(scene => scene != null && scene.scenePath == scenePath);
    }
}

[System.Serializable]
public class SceneInfo
{
    public string sceneName;
    public string scenePath;
    public string customIconId = "";

    public SceneInfo() { } // required for JsonUtility

    public SceneInfo(string sceneName, string scenePath)
    {
        this.sceneName = sceneName;
        this.scenePath = scenePath;
        this.customIconId = "";
    }
}

[System.Serializable]
public class SceneIconInfo
{
    public string id;
    public string displayName;
    public string assetPath;

    public SceneIconInfo() { }

    public SceneIconInfo(string id, string displayName, string assetPath)
    {
        this.id = id;
        this.displayName = displayName;
        this.assetPath = assetPath;
    }
}

[System.Serializable]
public class SceneIconEmbeddedData
{
    public string id;
    public string displayName;
    public string fileName;
    public string imageDataBase64;

    public SceneIconEmbeddedData() { }

    public SceneIconEmbeddedData(string id, string displayName, string fileName, string imageDataBase64)
    {
        this.id = id;
        this.displayName = displayName;
        this.fileName = fileName;
        this.imageDataBase64 = imageDataBase64;
    }
}
#endif
