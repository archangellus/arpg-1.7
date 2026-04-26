using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace PLAYERTWO.ARPGProject
{
    public partial class PluginInspectorWindow
    {
        private void OnGUI()
        {
            EnsureStyles();
            if (!m_stylesReady) return;

            /* ----------------------------------------------------------
             * 1)  Calculate rectangles for each strip
             * ----------------------------------------------------------*/
            const float PALETTE_H = 48f;
            const float BAR_H = 24f;

            Rect full = new Rect(0, 0, position.width, position.height);
            Rect barR = new Rect(0, full.height - BAR_H, full.width, BAR_H);
            Rect palR = new Rect(0, barR.yMin - PALETTE_H, full.width, PALETTE_H);
            Rect workR = new Rect(0, 0, full.width, palR.yMin);   // area that scrolls

            /* ----------------------------------------------------------
             * 2)  Work area   (TreeView + Code editor)
             * ----------------------------------------------------------*/
            GUILayout.BeginArea(workR);
            using (var workScroll = new EditorGUILayout.ScrollViewScope(
                       m_workScroll,
                       false,
                       false,
                       GUILayout.ExpandWidth(true),
                       GUILayout.ExpandHeight(true)))
            {
                m_workScroll = workScroll.scrollPosition;

                GUILayout.BeginHorizontal();
                DrawNavigationTree();
                DrawCodeArea();
                GUILayout.EndHorizontal();
            }
            GUILayout.EndArea();

            /* ----------------------------------------------------------
             * 3)  Colour palette  (never scrolls)
             * ----------------------------------------------------------*/
            GUILayout.BeginArea(palR, EditorStyles.helpBox);
            DrawColorSettings();
            GUILayout.EndArea();

            /* ----------------------------------------------------------
             * 4)  Bottom button bar  (never scrolls)
             * ----------------------------------------------------------*/
            GUILayout.BeginArea(barR, EditorStyles.toolbar);
            DrawBottomBar();
            GUILayout.EndArea();
        }

        /* ──────────────────────────────────────────────────────────────
         * 2)  Code editor  – overlay + transparent TextArea
         * ────────────────────────────────────────────────────────────*/
        private void DrawCodeArea()
        {
            // ---- panel chrome knobs ----
            const float outerGapBase = 8f;
            const float outerGapX = outerGapBase * 2f; // 4x outside padding (left/right)
            const float outerGapY = outerGapBase * 2f; // 4x outside padding (top/bottom)
            const float border = 1f;                  // border thickness
            const float padX = 10f;                   // padding between border and content
            const float padY = 10f;                   // padding between border and content
                                                      // ----------------------------

            Color borderCol = EditorGUIUtility.isProSkin
                ? new Color(0.65f, 0.65f, 0.65f, 1f)
                : new Color(0.25f, 0.25f, 0.25f, 1f);

            // Outer margin around the whole bordered panel
            GUILayout.BeginVertical(GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
            GUILayout.Space(outerGapY);

            GUILayout.BeginHorizontal(GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
            GUILayout.Space(outerGapX);

            GUILayout.BeginVertical(GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
            {
                // Top border strip
                Rect top = GUILayoutUtility.GetRect(GUIContent.none, GUIStyle.none,
                    GUILayout.ExpandWidth(true), GUILayout.Height(border));

                // Middle: left border strip + padded content + right border strip
                GUILayout.BeginHorizontal(GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
                {
                    Rect left = GUILayoutUtility.GetRect(GUIContent.none, GUIStyle.none,
                        GUILayout.Width(border), GUILayout.ExpandHeight(true));

                    GUILayout.BeginVertical(GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
                    {
                        GUILayout.Space(padY);

                        GUILayout.BeginHorizontal(GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
                        {
                            GUILayout.Space(padX);

                            GUILayout.BeginVertical(GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
                            DrawCodeAreaContents(); // unchanged UI content, just wrapped
                            GUILayout.EndVertical();

                            GUILayout.Space(padX);
                        }
                        GUILayout.EndHorizontal();

                        GUILayout.Space(padY);
                    }
                    GUILayout.EndVertical();

                    Rect right = GUILayoutUtility.GetRect(GUIContent.none, GUIStyle.none,
                        GUILayout.Width(border), GUILayout.ExpandHeight(true));

                    if (Event.current.type == EventType.Repaint)
                    {
                        EditorGUI.DrawRect(left, borderCol);
                        EditorGUI.DrawRect(right, borderCol);
                    }
                }
                GUILayout.EndHorizontal();

                // Bottom border strip
                Rect bottom = GUILayoutUtility.GetRect(GUIContent.none, GUIStyle.none,
                    GUILayout.ExpandWidth(true), GUILayout.Height(border));

                if (Event.current.type == EventType.Repaint)
                {
                    EditorGUI.DrawRect(top, borderCol);
                    EditorGUI.DrawRect(bottom, borderCol);
                }
            }
            GUILayout.EndVertical();

            GUILayout.Space(outerGapX);
            GUILayout.EndHorizontal();

            GUILayout.Space(outerGapY);
            GUILayout.EndVertical();
        }

        private static float GetMaxLinePixelWidth(GUIStyle style, string text)
        {
            if (string.IsNullOrEmpty(text))
                return 0f;

            float max = 0f;
            int start = 0;

            for (int i = 0; i <= text.Length; i++)
            {
                bool end = i == text.Length;
                if (!end && text[i] != '\n')
                    continue;

                int len = i - start;
                if (len > 0 && text[start + len - 1] == '\r')
                    len--;

                string line = len > 0 ? text.Substring(start, len) : string.Empty;
                float w = style.CalcSize(new GUIContent(line)).x;
                if (w > max) max = w;

                start = i + 1;
            }

            return max;
        }


        /// <summary>
        /// This is your ORIGINAL DrawCodeArea body (the actual UI), unchanged.
        /// </summary>
        private void DrawCodeAreaContents()
        {
            // If we clicked a plugin root, show the Patch Builder instead of the code view
            if (!string.IsNullOrEmpty(m_selectedPath) && Directory.Exists(m_selectedPath))
            {
                string root = GetPluginRoot(m_selectedPath);
                if (!string.IsNullOrEmpty(root) && string.Equals(root, m_selectedPath, StringComparison.OrdinalIgnoreCase))
                {
                    DrawPatchBuilder(root);
                    return;
                }
            }

            if (string.IsNullOrEmpty(m_code))
            {
                GUILayout.Label("Select a plug-in file to view its code.",
                                EditorStyles.centeredGreyMiniLabel,
                                GUILayout.ExpandHeight(true));
                return;
            }

            EditorGUILayout.LabelField(
                new GUIContent("Plugin Code Editing", "Code view with syntax coloring. Use the toolbar Save to write changes."),
                EditorStyles.miniBoldLabel);

            bool showLineNumbers = ShouldShowLineNumbers();

            // Reserve the visible viewport for the editor area
            Rect viewRect = GUILayoutUtility.GetRect(
                GUIContent.none, GUIStyle.none,
                GUILayout.ExpandWidth(true),
                GUILayout.ExpandHeight(true)
            );

            // Compute widths so horizontal scrolling appears ONLY when truly needed
            float lineNumberWidth = showLineNumbers ? CalculateLineNumberWidth() : 0f;
            float maxLineWidth = GetMaxLinePixelWidth(m_editStyle, m_code) + 6f; // + a tiny caret buffer

            float desiredContentWidth = lineNumberWidth + maxLineWidth;

            // Snap to view width unless we're meaningfully wider (prevents 1px “phantom” scrollbar)
            const float epsilon = 1.0f;
            float contentWidth = desiredContentWidth <= viewRect.width + epsilon
                ? viewRect.width
                : desiredContentWidth;

            // Height (usually stable because code styles are typically non-wrapping)
            float textWidthForHeight = Mathf.Max(10f, viewRect.width - lineNumberWidth);
            float neededHeight = m_highlightStyle.CalcHeight(new GUIContent(m_highlightedCode ?? m_code), textWidthForHeight);
            float contentHeight = Mathf.Max(viewRect.height, neededHeight);

            Rect contentRect = new Rect(0f, 0f, contentWidth, contentHeight);

            m_codeScroll = UnityEngine.GUI.BeginScrollView(viewRect, m_codeScroll, contentRect, false, true);

            Rect lineRect = new Rect(0f, 0f, lineNumberWidth, contentHeight);
            Rect codeRect = new Rect(lineNumberWidth, 0f, Mathf.Max(10f, contentWidth - lineNumberWidth), contentHeight);

            if (showLineNumbers)
                DrawLineNumbers(lineRect, codeRect);

            if (Event.current.type == EventType.Repaint)
                UnityEngine.GUI.Label(codeRect, m_highlightedCode ?? m_code, m_highlightStyle);

            UnityEngine.GUI.SetNextControlName("CodeArea");
            EditorGUI.BeginChangeCheck();
            string newCode = EditorGUI.TextArea(codeRect, m_code, m_editStyle);
            if (EditorGUI.EndChangeCheck())
            {
                m_code = newCode;
                UpdateHighlightedCode();
            }

            UnityEngine.GUI.EndScrollView();

        }


        private void DrawBottomBar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

            if (GUILayout.Button(new GUIContent("Save Profile", "Save the current color palette to a .picprofile file"), EditorStyles.toolbarButton))
                SaveProfile();

            if (GUILayout.Button(new GUIContent("Load Profile", "Load a saved .picprofile and apply its colors"), EditorStyles.toolbarButton))
                LoadProfile();

            GUILayout.FlexibleSpace();

            // Creation
            if (GUILayout.Button(new GUIContent("Add Plug-in", "Create a new plug-in folder with a starter class"), EditorStyles.toolbarButton))
                TextInputPopup.Show("New Plug-in", "Name", "MyPlugin", CreatePlugin);
            // Import
            if (GUILayout.Button(new GUIContent("Import Plug-in", "Import a plug-in from a .zip archive"), EditorStyles.toolbarButton))
                ImportPluginZip();

            /* current folder (if any) */
            string selFolder = null;
            if (!string.IsNullOrEmpty(m_selectedPath))
                selFolder = Directory.Exists(m_selectedPath)
                          ? m_selectedPath
                          : Path.GetDirectoryName(m_selectedPath);

            /* inside an existing plug-in? */
            bool inPlugin = !string.IsNullOrEmpty(GetPluginRoot(selFolder));

            /* ── add file / sub-folder inside selected plug-in ──────────── */
            UnityEngine.GUI.enabled = inPlugin;
            bool exportClicked = GUILayout.Button(new GUIContent("Export Plug-in", "Zip the selected plug-in folder and save it"), EditorStyles.toolbarButton);

            if (GUILayout.Button(new GUIContent("Add Plug-in File", "Create a new .cs file inside the selected plug-in"), EditorStyles.toolbarButton))
                TextInputPopup.Show("New .cs File", "Class Name", "MyScript", n => CreatePluginFile(selFolder, n));

            if (GUILayout.Button(new GUIContent("Add Folder", "Create a subfolder inside the selected plug-in"), EditorStyles.toolbarButton))
                TextInputPopup.Show("New Folder", "Folder Name", "SubFolder", n => CreateSubFolder(selFolder, n));
            UnityEngine.GUI.enabled = true;

            /*  DELETE current selection  ------------------------------------------------*/
            bool exists = !string.IsNullOrEmpty(m_selectedPath) &&
                          (File.Exists(m_selectedPath) || Directory.Exists(m_selectedPath));

            UnityEngine.GUI.enabled = exists;
            if (GUILayout.Button(new GUIContent("Delete", "Delete the selected file/folder (and its .meta)"), EditorStyles.toolbarButton))
            {
                string name = Path.GetFileName(m_selectedPath);
                string msg = Directory.Exists(m_selectedPath)
                              ? $"Delete folder “{name}” and all its contents?"
                              : $"Delete file “{name}”?";

                if (EditorUtility.DisplayDialog("Confirm Delete", msg, "Delete", "Cancel"))
                {
                    /* get Unity-relative path (“Assets/…”) so AssetDatabase can
                       remove the asset *and* its .meta in one call                 */
                    string abs = Path.GetFullPath(m_selectedPath).Replace('\\', '/');
                    string proj = Application.dataPath.Replace('\\', '/');
                    string assetPath = abs.StartsWith(proj)
                                       ? "Assets" + abs.Substring(proj.Length)
                                       : m_selectedPath;          // fallback: already relative

                    AssetDatabase.DeleteAsset(assetPath);          // removes .meta too

                    m_selectedPath = null;
                    m_code = null;
                    AssetDatabase.Refresh();
                    Refresh();                                     // reload TreeView
                }
            }
            UnityEngine.GUI.enabled = true;

            /* ── refresh list ───────────────────────────────────────────── */
            if (GUILayout.Button(new GUIContent("Refresh", "Rescan plug-ins and rebuild the list"), EditorStyles.toolbarButton))
                Refresh();

            /* ── save current text file ─────────────────────────────────── */
            bool editable = !string.IsNullOrEmpty(m_selectedPath) &&
                            File.Exists(m_selectedPath) &&
                            IsText(m_selectedPath);

            UnityEngine.GUI.enabled = editable;
            if (GUILayout.Button(new GUIContent("Save", "Write the current file’s contents to disk"), EditorStyles.toolbarButton))
            {
                File.WriteAllText(m_selectedPath, m_code ?? string.Empty);
                AssetDatabase.Refresh();
            }

            UnityEngine.GUI.enabled = true;

            EditorGUILayout.EndHorizontal();

            if (exportClicked)
                EditorApplication.delayCall += ExportSelectedPluginZip;
        }
    }
}
