using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;
using Unity.VisualScripting;

namespace PLAYERTWO.ARPGProject
{
    public partial class PluginInspectorWindow
    {
        private const float PluginListCollapsedWidth = 80f;
        private const float PluginListExpandedWidth = 280f;
        private const float PluginIconSize = 28f;
        private const float PluginListHorizontalPadding = 10f;

        // TreeView item used to store plug-in paths. Uses the appropriate
        // TreeViewItem type depending on Unity version.
#if UNITY_6000_2_OR_NEWER
        private class PathItem : TreeViewItem<int>
        {
            public readonly string path;
            public PathItem(int id, int depth, string displayName, string path)
                : base(id, depth, displayName)
            {
                this.path = path;
            }
        }
#else
        private class PathItem : TreeViewItem
        {
            public readonly string path;
            public PathItem(int id, int depth, string displayName, string path)
            {
                this.id = id;
                this.depth = depth;
                this.displayName = displayName;
                this.path = path;
            }
        }
#endif

        private static void DrawRectBorder(Rect r, float thickness, Color c,
            bool top = true, bool bottom = true, bool left = true, bool right = true)
        {
            if (top)
                EditorGUI.DrawRect(new Rect(r.xMin, r.yMin, r.width, thickness), c);
            if (bottom)
                EditorGUI.DrawRect(new Rect(r.xMin, r.yMax - thickness, r.width, thickness), c);
            if (left)
                EditorGUI.DrawRect(new Rect(r.xMin, r.yMin, thickness, r.height), c);
            if (right)
                EditorGUI.DrawRect(new Rect(r.xMax - thickness, r.yMin, thickness, r.height), c);
        }



        /* ──────────────────────────────────────────────────────────────
         * 1)  Navigation tree  – let TreeView decide when to scroll
         * ────────────────────────────────────────────────────────────*/
        private void DrawNavigationTree()
        {
            float pluginListWidth = m_pluginListExpanded ? PluginListExpandedWidth : PluginListCollapsedWidth;
            GUILayout.BeginVertical(GUILayout.Width(pluginListWidth), GUILayout.ExpandHeight(true));
            DrawPluginListPanel(pluginListWidth);
            GUILayout.EndVertical();
        }

        private void DrawPluginListPanel(float width)
        {
            // Padding OUTSIDE the list border
            const float outsideLeftPadding = 17f;
            const float outsideBottomPadding = 17f;
            //const float ExpandCollapseButtonBorder = 0f;


            // One OUTSIDE border around the whole TreeView (no per-row borders)
            // plus padding between the border and the TreeView content.
            const float listBorder = 1f;
            const float listPaddingX = 6f;
            const float listPaddingY = 4f;

            Color borderColor = new Color(0.796f, 0.796f, 0.796f, 0.85f);

            // Wrapper: reserve full panel width, then inset content by 5px from the left
            EditorGUILayout.BeginHorizontal(GUILayout.Width(width));
            GUILayout.Space(outsideLeftPadding);
            EditorGUILayout.BeginVertical(GUILayout.Width(Mathf.Max(0f, width - outsideLeftPadding)));

            // ── header row (NO toolbar background / separator line) ──────────────
            float baseToolbarH = Mathf.Max(18f, EditorStyles.toolbar.fixedHeight);
            float paddedToolbarH = baseToolbarH + 10f;

            Rect headerRect = GUILayoutUtility.GetRect(0f, paddedToolbarH, GUILayout.ExpandWidth(true));
            Rect headerContentRect = new Rect(headerRect.x, headerRect.y + 5f, headerRect.width, baseToolbarH);

            const float toggleBtnW = 24f;
            Rect toggleRect = new Rect(
                headerContentRect.xMax - toggleBtnW - 2f,
                headerContentRect.y,
                toggleBtnW,
                headerContentRect.height);

            string toggleLabel = m_pluginListExpanded ? "<" : ">";
            bool toggled = UnityEngine.GUI.Button(
                toggleRect,
                new GUIContent(toggleLabel, "Collapse/expand the plug-in list"),
                EditorStyles.miniButton);

            // Crisp border around the button (top/bottom included)
            if (Event.current.type == EventType.Repaint)
                DrawRectBorder(toggleRect, 1f, borderColor);

            if (toggled)
            {
                m_pluginListExpanded = !m_pluginListExpanded;
                m_treeView?.Reload();
            }

            if (m_pluginListExpanded)
            {
                Rect labelRect = new Rect(headerContentRect.x + 6f, headerContentRect.y, 90f, headerContentRect.height);
                EditorGUI.LabelField(labelRect, "Plug-ins", EditorStyles.miniBoldLabel);
            }

            if (m_treeView == null)
            {
                EditorGUILayout.HelpBox(
                    "The plug-in tree failed to initialise. Close and reopen the window after fixing the underlying issue.",
                    MessageType.Info);

                EditorGUILayout.EndVertical();
                EditorGUILayout.EndHorizontal();
                return;
            }

            // ── search field (expanded mode only) ─────────────────────
            if (m_pluginListExpanded)
            {
                const float baseSearchH = 22f;
                const float extraSearchH = 10f;
                float searchH = baseSearchH + extraSearchH;

                Rect searchRect = GUILayoutUtility.GetRect(0f, searchH, GUILayout.ExpandWidth(true));
                searchRect.height = searchH;

                // Background + border (no baked-in search icon anymore)
                Color bg = EditorGUIUtility.isProSkin
                    ? new Color(0.20f, 0.20f, 0.20f, 1f)
                    : new Color(0.82f, 0.82f, 0.82f, 1f);

                EditorGUI.DrawRect(searchRect, bg);
                if (Event.current.type == EventType.Repaint)
                    DrawRectBorder(searchRect, 1f, new Color(0.796f, 0.796f, 0.796f, 0.85f));

                // Bigger magnifier icon (and ONLY this one exists)
                float iconSize = 20f; // bump this up/down as you like
                float iconLeftPad = 8f;
                float iconGap = 6f;

                var searchIcon = EditorGUIUtility.IconContent("Search Icon");
                if (searchIcon != null && searchIcon.image != null)
                {
                    Rect iconRect = new Rect(
                        searchRect.x + iconLeftPad,
                        searchRect.y + (searchRect.height - iconSize) * 0.5f,
                        iconSize,
                        iconSize);

                    UnityEngine.GUI.DrawTexture(iconRect, (Texture2D)searchIcon.image, ScaleMode.ScaleToFit);
                }

                string current = m_treeView.searchString ?? string.Empty;

                // Only reserve/paint a clear button when there's text
                float clearW = 18f;
                float rightPad = 6f;

                bool showClear = !string.IsNullOrEmpty(current);

                // Make the clear button vertically centered
                float clearH = Mathf.Min(searchRect.height - 6f, 18f);
                Rect clearRect = new Rect(
                    searchRect.xMax - rightPad - clearW,
                    searchRect.y + (searchRect.height - clearH) * 0.5f,
                    clearW,
                    clearH);

                // Text rect: after icon, and (optionally) before clear button
                Rect textRect = searchRect;
                textRect.xMin += iconLeftPad + iconSize + iconGap;
                textRect.xMax -= showClear ? (rightPad + clearW + 2f) : rightPad;
                textRect.yMin += 2f;
                textRect.yMax -= 2f;

                // Text field style: no background (we already drew it), and vertically centered text
                GUIStyle tf = new GUIStyle(EditorStyles.textField);
                tf.normal.background = null;
                tf.focused.background = null;
                tf.hover.background = null;
                tf.active.background = null;
                tf.border = new RectOffset(0, 0, 0, 0);

                // Left aligned + vertical centering
                tf.alignment = TextAnchor.MiddleLeft;

                // Pad top/bottom so the font sits centered in the available height
                int vPad = Mathf.Max(0, Mathf.RoundToInt((textRect.height - EditorGUIUtility.singleLineHeight) * 0.5f));
                tf.padding = new RectOffset(0, 0, vPad, vPad);

                UnityEngine.GUI.SetNextControlName("PluginListSearchField");
                string newSearch = EditorGUI.TextField(textRect, current, tf);

                // Clear button style: middle-center
                if (showClear)
                {
                    GUIStyle clearStyle = new GUIStyle(EditorStyles.miniButton)
                    {
                        alignment = TextAnchor.MiddleCenter,
                        padding = new RectOffset(0, 0, 0, 0)
                    };

                    // Using the multiplication sign looks nicer than a plain 'x'
                    if (UnityEngine.GUI.Button(clearRect, "×", clearStyle))
                    {
                        newSearch = string.Empty;
                        UnityEngine.GUI.FocusControl(null);
                    }
                }

                if (!string.Equals(newSearch, m_treeView.searchString, StringComparison.Ordinal))
                {
                    m_treeView.searchString = newSearch;
                    m_treeView.Reload();
                }


                GUILayout.Space(4);
            }
            else if (!string.IsNullOrEmpty(m_treeView.searchString))
            {
                m_treeView.searchString = string.Empty;
                m_treeView.Reload();
            }


            // ── TreeView content ─────────────────────────────────────
            Rect allocatedRect = GUILayoutUtility.GetRect(
                GUIContent.none,
                GUIStyle.none,
                GUILayout.ExpandWidth(true),
                GUILayout.ExpandHeight(true));

            // Leave 5px *below* the border (outside-bottom padding)
            Rect outerRect = allocatedRect;
            outerRect.height = Mathf.Max(0f, outerRect.height - outsideBottomPadding);

            Rect innerRect = new Rect(
                outerRect.xMin + listBorder + listPaddingX,
                outerRect.yMin + listBorder + listPaddingY,
                Mathf.Max(0f, outerRect.width - (listBorder * 2f + listPaddingX * 2f)),
                Mathf.Max(0f, outerRect.height - (listBorder * 2f + listPaddingY * 2f))
            );

            // Draw the TreeView (it manages its own scrolling)
            m_treeView.OnGUI(innerRect);

            // Draw ONE outside border around the whole list panel.
            if (Event.current.type == EventType.Repaint)
                DrawRectBorder(outerRect, listBorder, borderColor);

            EditorGUILayout.EndVertical();
            EditorGUILayout.EndHorizontal();
        }










        private void SelectPluginInTree(string pluginRoot)
        {
            if (m_treeView == null || string.IsNullOrEmpty(pluginRoot))
                return;

            var node = m_treeView.GetRows()
                .OfType<PathItem>()
                .FirstOrDefault(p => string.Equals(p.path, pluginRoot, StringComparison.OrdinalIgnoreCase));

            if (node != null)
            {
                m_treeView.SetSelection(new[] { node.id }, TreeViewSelectionOptions.RevealAndFrame);
                OpenItem(pluginRoot);
            }
            else
            {
                Refresh();
            }
        }

        // ───────────────────────────────────────────────────────────────────
        //   PLUGIN / FOLDER / FILE - hierarchy
        // ───────────────────────────────────────────────────────────────────
        private class PluginTreeView :
#if UNITY_6000_2_OR_NEWER
            TreeView<int>
#else
    TreeView
#endif

        {
            private readonly PluginInspectorWindow m_window;
            private const int kRootID = 0;

            public PluginTreeView(
#if UNITY_6000_2_OR_NEWER
                TreeViewState<int>
#else
        TreeViewState
#endif
                state, PluginInspectorWindow wnd)
                : base(state)
            {
                m_window = wnd;
                // We draw our own outer border in the window; avoid the built-in one.
                showBorder = false;
                showAlternatingRowBackgrounds = false;
                Reload();
            }

            // Build the root of the tree
#if UNITY_6000_2_OR_NEWER
            protected override TreeViewItem<int> BuildRoot()
            {
                var root = new TreeViewItem<int>(kRootID, -1, "Root")
                {
                    // TreeView requires the root item to have a non-null children list
                    children = new List<TreeViewItem<int>>()
                };
#else
    protected override TreeViewItem BuildRoot()
    {
        var root = new TreeViewItem
        {
            id = kRootID,
            depth = -1,
            displayName = "Root",
            children = new List<TreeViewItem>()
        };
#endif

                if (Directory.Exists(PluginsPath))
                {
                    int id = 1;
                    foreach (string dir in Directory.GetDirectories(PluginsPath).OrderBy(Path.GetFileName))
                        AddFolderRecursive(dir, root, ref id, 0);
                }

                SetupDepthsFromParentsAndChildren(root);
                return root;
            }

#if UNITY_6000_2_OR_NEWER
            protected override float GetCustomRowHeight(int row, TreeViewItem<int> item)
#else
protected override float GetCustomRowHeight(int row, TreeViewItem item)
#endif
            {
                // Give icon-only mode enough vertical room for a 32px icon (+ a little breathing space).
                return !m_window.m_pluginListExpanded ? 36f : base.GetCustomRowHeight(row, item);
            }


            // Build rows with support for:
            //  - icon-only mode when the list panel is collapsed
            //  - search filtering (TreeView does not filter automatically)
#if UNITY_6000_2_OR_NEWER
            protected override IList<TreeViewItem<int>> BuildRows(TreeViewItem<int> root)
#else
    protected override IList<TreeViewItem> BuildRows(TreeViewItem root)
#endif
            {
                // Collapsed panel mode: show only top-level plug-ins as icons (keeps the strip narrow).
                if (!m_window.m_pluginListExpanded)
                {
#if UNITY_6000_2_OR_NEWER
                    var rows = new List<TreeViewItem<int>>();
#else
            var rows = new List<TreeViewItem>();
#endif
                    if (root.children != null)
                    {
                        foreach (var child in root.children)
                        {
                            if (child is PathItem p && m_window.IsPluginRoot(p.path))
                            {
                                // Clone: no children -> no foldout arrow, icon-only draw.
                                var clone = new PathItem(p.id, 0, string.Empty, p.path) { icon = p.icon };

                                rows.Add(clone);
                            }
                        }
                    }

                    return rows;
                }

                // Expanded mode: apply search filtering if needed.
                string term = (searchString ?? string.Empty).Trim();
                if (string.IsNullOrEmpty(term))
                    return base.BuildRows(root);

#if UNITY_6000_2_OR_NEWER
                var filtered = new List<TreeViewItem<int>>();
#else
        var filtered = new List<TreeViewItem>();
#endif
                AddSearchMatches(root, term, filtered);
                return filtered;
            }

#if UNITY_6000_2_OR_NEWER
            private void AddSearchMatches(TreeViewItem<int> parent, string term, List<TreeViewItem<int>> rows)
#else
    private void AddSearchMatches(TreeViewItem parent, string term, List<TreeViewItem> rows)
#endif
            {
                if (parent.children == null)
                    return;

                foreach (var child in parent.children)
                {
                    var p = child as PathItem;
                    string name = p != null ? p.displayName : child.displayName;

                    bool match = !string.IsNullOrEmpty(name) &&
                                 name.IndexOf(term, StringComparison.OrdinalIgnoreCase) >= 0;

                    if (match)
                    {
                        if (p != null)
                        {
                            var clone = new PathItem(p.id, 0, p.displayName, p.path) { icon = p.icon };
                            rows.Add(clone);
                        }
                        else
                        {
                            child.depth = 0;
                            rows.Add(child);
                        }
                    }

                    if (child.hasChildren)
                        AddSearchMatches(child, term, rows);
                }
            }

#if UNITY_6000_2_OR_NEWER
            private void AddFolderRecursive(string path, TreeViewItem<int> parent, ref int id, int depth)
#else
    private void AddFolderRecursive(string path, TreeViewItem parent, ref int id, int depth)
#endif
            {
                var folderItem = new PathItem(id++, depth, Path.GetFileName(path), path);

                // Plugin root gets the plugin icon, normal folders get a folder icon.
                if (m_window.IsPluginRoot(path))
                {
                    folderItem.icon = m_window.GetPluginIconForList(path)
                        ?? EditorGUIUtility.IconContent("Prefab Icon").image as Texture2D;
                }
                else
                {
                    folderItem.icon = EditorGUIUtility.IconContent("Folder Icon").image as Texture2D;
                }

                parent.AddChild(folderItem);

                foreach (string sub in Directory.GetDirectories(path).OrderBy(Path.GetFileName))
                    AddFolderRecursive(sub, folderItem, ref id, depth + 1);

                foreach (string file in Directory.GetFiles(path)
                                                 .Where(f => !f.EndsWith(".meta", StringComparison.OrdinalIgnoreCase))
                                                 .OrderBy(Path.GetFileName))
                {
                    var fileItem = new PathItem(id++, depth + 1, Path.GetFileName(file), file);
                    fileItem.icon = AssetDatabase.GetCachedIcon(file) as Texture2D;
                    folderItem.AddChild(fileItem);
                }
            }

            protected override void RowGUI(RowGUIArgs args)
            {
                if (args.item is PathItem pathItem && m_window.IsPluginRoot(pathItem.path))
                {
                    var originalColor = UnityEngine.GUI.color;
                    bool enabled = m_window.IsPluginEnabled(pathItem.path);
                    if (!enabled)
                        UnityEngine.GUI.color = new Color(originalColor.r, originalColor.g, originalColor.b, 0.5f);

                    // Collapsed strip: monogram-only, no inline toggle.
                    if (!m_window.m_pluginListExpanded)
                    {
                        // In collapsed mode, BuildRows clones use empty displayName, so derive from path.
                        string trimmed = pathItem.path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                        string pluginTitle = Path.GetFileName(trimmed);
                        if (string.IsNullOrEmpty(pluginTitle))
                            pluginTitle = "??";

                        string monogram = BuildTwoLetterMonogram(pluginTitle);

                        var savedIcon = pathItem.icon;
                        var savedName = pathItem.displayName;

                        // Let TreeView draw selection/hover background etc. (but no text/icon)
                        pathItem.icon = null;
                        pathItem.displayName = string.Empty;
                        base.RowGUI(args);
                        pathItem.icon = savedIcon;
                        pathItem.displayName = savedName;

                        // 5px padding left/right (now safe because the panel is wider)
                        const float padX = 5f;
                        Rect monoRect = args.rowRect;
                        monoRect.xMin += padX;
                        monoRect.xMax -= padX;

                        int fontSize = Mathf.Clamp(Mathf.RoundToInt(args.rowRect.height * 0.55f), 14, 18);

                        var letterStyle = new GUIStyle(EditorStyles.boldLabel)
                        {
                            alignment = TextAnchor.MiddleCenter,
                            fontSize = fontSize,
                            clipping = TextClipping.Clip,
                            wordWrap = false
                        };

                        letterStyle.normal.textColor = EditorGUIUtility.isProSkin
                            ? new Color32(0xE6, 0xE6, 0xE6, 0xFF)
                            : new Color32(0x22, 0x22, 0x22, 0xFF);

                        // tiny shadow for legibility
                        var shadowStyle = new GUIStyle(letterStyle);
                        shadowStyle.normal.textColor = new Color(0f, 0f, 0f, 0.35f);

                        Rect shadowRect = monoRect;
                        shadowRect.x += 1f;
                        shadowRect.y += 1f;

                        UnityEngine.GUI.Label(shadowRect, monogram, shadowStyle);
                        UnityEngine.GUI.Label(monoRect, monogram, letterStyle);

                        // Tooltip with plugin name when hovered
                        UnityEngine.GUI.Label(args.rowRect, new GUIContent(string.Empty, pluginTitle), GUIStyle.none);

                        UnityEngine.GUI.color = originalColor;
                        return;
                    }

                    // Expanded mode: inline enable toggle on plugin roots.
                    Rect rowRect = args.rowRect;
                    Rect toggleRect = new Rect(
                        rowRect.xMax - INLINE_TOGGLE_WIDTH - 6f,
                        rowRect.y + (rowRect.height - INLINE_TOGGLE_HEIGHT) * 0.5f,
                        INLINE_TOGGLE_WIDTH,
                        INLINE_TOGGLE_HEIGHT);

                    rowRect.xMax = toggleRect.xMin - 4f;
                    args.rowRect = rowRect;
                    base.RowGUI(args);
                    UnityEngine.GUI.color = originalColor;

                    bool newEnabled = m_window.DrawInlineToggle(toggleRect, enabled);
                    if (newEnabled != enabled)
                        m_window.SetPluginEnabled(pathItem.path, newEnabled);

                    return;
                }

                base.RowGUI(args);
            }



            private static string GetPluginTitleFromPath(string path)
            {
                if (string.IsNullOrEmpty(path)) return "??";
                string trimmed = path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                string name = Path.GetFileName(trimmed);
                return string.IsNullOrEmpty(name) ? "??" : name;
            }

            private static string BuildTwoLetterMonogram(string title)
            {
                if (string.IsNullOrEmpty(title))
                    return "??";

                // 1) Prefer the first two uppercase letters in the title (PascalCase detection).
                char firstUpper = '\0';
                char secondUpper = '\0';

                for (int i = 0; i < title.Length; i++)
                {
                    char c = title[i];
                    if (char.IsUpper(c))
                    {
                        if (firstUpper == '\0') firstUpper = c;
                        else { secondUpper = c; break; }
                    }
                }

                if (firstUpper != '\0' && secondUpper != '\0')
                {
                    // Render as Upper + lower (e.g., ArcDrop -> 'A' + 'd' => "Ad")
                    return $"{char.ToUpperInvariant(firstUpper)}{char.ToLowerInvariant(secondUpper)}";
                }

                // 2) Fallback: first character upper, second character lower.
                char a = title.Length >= 1 ? title[0] : '?';
                char b = title.Length >= 2 ? title[1] : ' ';

                char A = char.ToUpperInvariant(a);
                if (title.Length < 2) return $"{A}";

                char B = (b == ' ') ? ' ' : char.ToLowerInvariant(b);
                return $"{A}{B}";
            }



            protected override void SingleClickedItem(int id)
            {
                var node = FindItem(id, rootItem) as PathItem;
                if (node != null)
                    m_window.OpenItem(node.path);
            }
        }

        // ───────────────────────────────────────────────────────────────────
        //   END OF PLUGIN / FOLDER / FILE - hierarchy
        // ───────────────────────────────────────────────────────────────────
        internal void OpenItem(string fullPath)
        {
            if (string.IsNullOrEmpty(fullPath)) return;

            m_selectedPath = fullPath;             // <── track selection

            if (File.Exists(fullPath) && IsText(fullPath))
                m_code = File.ReadAllText(fullPath);
            else
                m_code = "// Binary asset";

            UpdateHighlightedCode();
            Repaint();
        }

        // naive test – extend as needed
        private static bool IsText(string path)
        {
            string ext = Path.GetExtension(path).ToLowerInvariant();
            return ext == ".cs" || ext == ".txt" || ext == ".shader" || ext == ".json";
        }

        // optional: tiny side strip just for plugin-level actions
        private void DrawPluginList()
        {
            // list all *top-level* plugin folders (one line each)
            var pluginFolders = Directory.Exists(PluginsPath)
                ? Directory.GetDirectories(PluginsPath).OrderBy(Path.GetFileName).ToArray()
                : Array.Empty<string>();

            GUILayout.BeginVertical(GUILayout.Width(160), GUILayout.ExpandHeight(true));
            foreach (string dir in pluginFolders)
            {
                bool isSel = m_selectedPath != null &&
                             m_selectedPath.StartsWith(dir, StringComparison.Ordinal);

                if (GUILayout.Toggle(isSel, Path.GetFileName(dir), "Button"))
                {
                    // select in tree
                    int id = m_treeView.GetRows()
                                       .OfType<PathItem>()
                                       .First(p => p.path == dir).id;
                    m_treeView.SetSelection(new[] { id }, TreeViewSelectionOptions.RevealAndFrame);
                    OpenItem(dir);
                }
            }
            GUILayout.EndVertical();
        }
    }
}