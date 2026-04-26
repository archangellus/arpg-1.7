#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using System.Collections.Generic;
using System.IO;

public class SceneSwitcher : EditorWindow
{
    private SceneData sceneData;
    private Vector2 scrollPos;
    private string[] sceneGUIDs;

    private string statusBarMessage = "";
    private double statusMessageEndTime;
    private string sceneDataFilePath = "Assets/SceneData.json";

    private string listSearch = "";

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
        w.minSize = new Vector2(460, 320);
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

        if (sceneData.sceneInfos == null)
            sceneData.sceneInfos = new List<SceneInfo>();
    }

    private void SaveSceneData()
    {
        if (sceneData == null) return;

        string json = JsonUtility.ToJson(sceneData, true);

        var dir = Path.GetDirectoryName(sceneDataFilePath);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            Directory.CreateDirectory(dir);

        File.WriteAllText(sceneDataFilePath, json);
        AssetDatabase.Refresh();
    }

    internal void SetStatusBarMessage(string message, double duration)
    {
        statusBarMessage = message;
        statusMessageEndTime = EditorApplication.timeSinceStartup + duration;
        Repaint();
    }

    private void ClearStatusBar()
    {
        if (!string.IsNullOrEmpty(statusBarMessage) && EditorApplication.timeSinceStartup >= statusMessageEndTime)
        {
            statusBarMessage = "";
            Repaint();
        }
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

        GUILayout.FlexibleSpace();

        var saveIcon = EditorGUIUtility.IconContent("SaveActive");
        saveIcon.tooltip = "Save list to Assets/SceneData.json";
        if (GUILayout.Button(saveIcon, EditorStyles.toolbarButton, GUILayout.Width(32)))
        {
            SaveSceneData();
            SetStatusBarMessage("Saved SceneData.json", 2.5);
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

            if (!string.IsNullOrEmpty(q))
            {
                if (!name.ToLowerInvariant().Contains(q) && !path.ToLowerInvariant().Contains(q))
                    continue;
            }

            visibleIndices.Add(i);
        }

        if (visibleIndices.Count == 0)
        {
            EditorGUI.HelpBox(listRect, "No matches for your search.", MessageType.Info);
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

            Rect inner = new Rect(
                rowRect.x + RowPadL,
                rowRect.y + RowPadT,
                rowRect.width - (RowPadL + RowPadR),
                rowRect.height - (RowPadT + RowPadB)
            );

            // Icon
            Rect iconRect = new Rect(inner.x, inner.y + 1f, LeftIconSize, LeftIconSize);
            var sceneIcon = EditorGUIUtility.IconContent("SceneAsset Icon");
            sceneIcon.tooltip = "Scene asset";
            GUI.Label(iconRect, sceneIcon);

            float xText = iconRect.xMax + GapAfterIcon;
            float xButtons = inner.xMax - ButtonsBlockWidth;

            // Text rect
            Rect textRect = new Rect(xText, inner.y, xButtons - xText - 8f, inner.height);

            // Title
            Rect titleRect = new Rect(textRect.x, textRect.y, textRect.width, nameH);
            EditorGUI.LabelField(titleRect, new GUIContent(name, "Scene name"), nameStyle);

            // Path (wrapped)
            Rect pRect = new Rect(textRect.x, titleRect.yMax + 2f, textRect.width, pathH);
            EditorGUI.LabelField(pRect, new GUIContent(path, "Full scene path"), pathStyle);

            // Buttons
            bool inBuild = buildSet.Contains(path);
            float btnY = inner.y + (inner.height - BtnH) * 0.5f;

            // 1) Open
            Rect b0 = new Rect(xButtons, btnY, BtnW, BtnH);
            GUIContent openC = EditorGUIUtility.IconContent("d_PlayButton");
            if (openC.image == null) openC = EditorGUIUtility.IconContent("PlayButton");
            if (openC.image == null) openC = new GUIContent("▶");
            openC.tooltip = "Open this scene (prompts to save modified scenes)";
            if (GUI.Button(b0, openC, iconButtonStyle))
            {
                if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                    EditorSceneManager.OpenScene(path);
            }

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
                    SetStatusBarMessage("Pinged scene in Project", 2.0);
                }
                else
                {
                    SetStatusBarMessage("Scene asset not found at that path", 3.0);
                }
            }

            // 3) Add to Build Settings
            Rect b2 = new Rect(b1.xMax + BtnGap, btnY, BtnW, BtnH);
            GUIContent buildAddC = EditorGUIUtility.IconContent("BuildSettings.Editor.Small");
            if (buildAddC.image == null) buildAddC = new GUIContent("B+");
            buildAddC.tooltip = inBuild ? "Already in Build Settings" : "Add this scene to Build Settings";
            EditorGUI.BeginDisabledGroup(inBuild);
            if (GUI.Button(b2, buildAddC, iconButtonStyle))
            {
                bool already = AddSceneToBuild(path);
                SetStatusBarMessage(already ? $"Already in Build Settings: {name}" : $"Added to Build Settings: {name}", 3.5);
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
                SetStatusBarMessage(removed ? $"Removed from Build Settings: {name}" : $"Not found in Build Settings: {name}", 3.5);
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
                SetStatusBarMessage("Removed scene from list", 2.5);
                GUIUtility.ExitGUI();
            }

            y += rowH + RowMarginY + RowGapY;
        }

        GUI.EndScrollView();
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
        string msg = string.IsNullOrEmpty(statusBarMessage) ? "Ready" : statusBarMessage;

        // Background
        GUI.Box(r, GUIContent.none, EditorStyles.helpBox);

        // Inner layout
        Rect inner = new Rect(r.x + 8f, r.y + 4f, r.width - 16f, r.height - 8f);

        Rect labelRect = new Rect(inner.x, inner.y, inner.width - 60f, inner.height);
        EditorGUI.LabelField(labelRect, new GUIContent($"Status: {msg}", msg), EditorStyles.label);

        Rect btnRect = new Rect(inner.xMax - 54f, inner.y, 54f, inner.height);
        EditorGUI.BeginDisabledGroup(string.IsNullOrEmpty(statusBarMessage));
        if (GUI.Button(btnRect, new GUIContent("Clear", "Clear the status message"), EditorStyles.miniButton))
        {
            statusBarMessage = "";
            Repaint();
        }
        EditorGUI.EndDisabledGroup();
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
                        sceneSwitcher.SetStatusBarMessage("Scene already in the list", 3);
                    }
                    else
                    {
                        sceneData.AddScene(name, path);
                        sceneSwitcher.SetStatusBarMessage("Scene added to the list", 3);
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

    public SceneInfo() { } // required for JsonUtility

    public SceneInfo(string sceneName, string scenePath)
    {
        this.sceneName = sceneName;
        this.scenePath = scenePath;
    }
}
#endif
