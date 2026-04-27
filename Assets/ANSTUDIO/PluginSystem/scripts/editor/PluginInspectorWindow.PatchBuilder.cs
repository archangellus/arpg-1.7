using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace PLAYERTWO.ARPGProject
{
    public partial class PluginInspectorWindow
    {
        private struct PreviewRenderData
        {
            public string HighlightedText;
            public string[] HighlightedLines;
            public int StartDisplayLine;
            public int ClampedLine;
        }

        // ─────────────────────────────────────────────────────────────
        // Resizable list heights (Existing Edits, Pending edits)
        // ─────────────────────────────────────────────────────────────
        private const float MANIFEST_LIST_MIN_HEIGHT = 60f;
        private const float MANIFEST_LIST_DEFAULT_HEIGHT = 110f;

        private const float PENDING_LIST_MIN_HEIGHT = 60f;
        private const float PENDING_LIST_DEFAULT_HEIGHT = 160f;

        private const string PREF_MANIFEST_LIST_HEIGHT = "PluginInspector.ManifestListHeight";
        private const string PREF_PENDING_LIST_HEIGHT = "PluginInspector.PendingListHeight";

        private bool m_listHeightsLoaded = false;

        private float m_manifestListHeight = MANIFEST_LIST_DEFAULT_HEIGHT;
        private bool m_manifestListHeightDragging = false;
        private float m_manifestListHeightStartY = 0f;
        private float m_manifestListHeightStartValue = 0f;

        private float m_pendingListHeight = PENDING_LIST_DEFAULT_HEIGHT;
        private bool m_pendingListHeightDragging = false;
        private float m_pendingListHeightStartY = 0f;
        private float m_pendingListHeightStartValue = 0f;


        private ManifestEditState m_inlinePreviewStatus = ManifestEditState.Unknown;
        private bool m_inlinePreviewStatusValid = false;

        private void DrawInlinePreviewStatusHelpBox()
        {
            if (!m_inlinePreviewStatusValid) return;

            switch (m_inlinePreviewStatus)
            {
                case ManifestEditState.Applied:
                    EditorGUILayout.HelpBox("This edit already appears in the target file.", MessageType.Info);
                    break;

                case ManifestEditState.NotApplied:
                    EditorGUILayout.HelpBox("This edit is not present in the target file yet.", MessageType.Info);
                    break;

                case ManifestEditState.FileMissing:
                    EditorGUILayout.HelpBox("Target file is missing; unable to verify applied state.", MessageType.Warning);
                    break;
            }
        }

        private void EnsureListHeightsLoaded()
        {
            if (m_listHeightsLoaded) return;
            m_listHeightsLoaded = true;

            m_manifestListHeight = Mathf.Max(
                MANIFEST_LIST_MIN_HEIGHT,
                EditorPrefs.GetFloat(PREF_MANIFEST_LIST_HEIGHT, MANIFEST_LIST_DEFAULT_HEIGHT)
            );

            m_pendingListHeight = Mathf.Max(
                PENDING_LIST_MIN_HEIGHT,
                EditorPrefs.GetFloat(PREF_PENDING_LIST_HEIGHT, PENDING_LIST_DEFAULT_HEIGHT)
            );
        }

        private void SaveListHeights()
        {
            EditorPrefs.SetFloat(PREF_MANIFEST_LIST_HEIGHT, m_manifestListHeight);
            EditorPrefs.SetFloat(PREF_PENDING_LIST_HEIGHT, m_pendingListHeight);
        }

        private float GetManifestListHeightLimit()
        {
            // Keep it sensible relative to the window.
            // Tweak the subtraction if you want more or less room for the rest of the UI.
            return Mathf.Max(MANIFEST_LIST_MIN_HEIGHT, position.height - 260f);
        }

        private float GetPendingListHeightLimit()
        {
            return Mathf.Max(PENDING_LIST_MIN_HEIGHT, position.height - 220f);
        }

        private void DrawManifestListHeightSplitter(Rect splitterRect)
        {
            EditorGUIUtility.AddCursorRect(splitterRect, MouseCursor.ResizeVertical);

            int controlId = GUIUtility.GetControlID("ManifestListHeightSplitter".GetHashCode(), FocusType.Passive);
            Event current = Event.current;

            bool isHovering = splitterRect.Contains(current.mousePosition);
            bool isActive = m_manifestListHeightDragging && GUIUtility.hotControl == controlId;

            // Reuse your existing splitter visuals so it matches the code block splitter
            DrawPatchSplitterVisual(splitterRect, isVertical: false, isActive: isActive, isHovering: isHovering);

            switch (current.type)
            {
                case EventType.MouseDown:
                    if (splitterRect.Contains(current.mousePosition))
                    {
                        GUIUtility.hotControl = controlId;
                        m_manifestListHeightDragging = true;
                        m_manifestListHeightStartY = current.mousePosition.y;
                        m_manifestListHeightStartValue = m_manifestListHeight;
                        current.Use();
                    }
                    break;

                case EventType.MouseDrag:
                    if (m_manifestListHeightDragging && GUIUtility.hotControl == controlId)
                    {
                        float delta = current.mousePosition.y - m_manifestListHeightStartY;
                        float limit = GetManifestListHeightLimit();
                        m_manifestListHeight = Mathf.Clamp(m_manifestListHeightStartValue + delta, MANIFEST_LIST_MIN_HEIGHT, limit);
                        current.Use();
                        Repaint();
                    }
                    break;

                case EventType.MouseUp:
                    if (m_manifestListHeightDragging && GUIUtility.hotControl == controlId)
                    {
                        m_manifestListHeightDragging = false;
                        GUIUtility.hotControl = 0;
                        SaveListHeights();
                        current.Use();
                    }
                    break;
            }
        }

        private void DrawPendingListHeightSplitter(Rect splitterRect)
        {
            EditorGUIUtility.AddCursorRect(splitterRect, MouseCursor.ResizeVertical);

            int controlId = GUIUtility.GetControlID("PendingListHeightSplitter".GetHashCode(), FocusType.Passive);
            Event current = Event.current;

            bool isHovering = splitterRect.Contains(current.mousePosition);
            bool isActive = m_pendingListHeightDragging && GUIUtility.hotControl == controlId;

            DrawPatchSplitterVisual(splitterRect, isVertical: false, isActive: isActive, isHovering: isHovering);

            switch (current.type)
            {
                case EventType.MouseDown:
                    if (splitterRect.Contains(current.mousePosition))
                    {
                        GUIUtility.hotControl = controlId;
                        m_pendingListHeightDragging = true;
                        m_pendingListHeightStartY = current.mousePosition.y;
                        m_pendingListHeightStartValue = m_pendingListHeight;
                        current.Use();
                    }
                    break;

                case EventType.MouseDrag:
                    if (m_pendingListHeightDragging && GUIUtility.hotControl == controlId)
                    {
                        float delta = current.mousePosition.y - m_pendingListHeightStartY;
                        float limit = GetPendingListHeightLimit();
                        m_pendingListHeight = Mathf.Clamp(m_pendingListHeightStartValue + delta, PENDING_LIST_MIN_HEIGHT, limit);
                        current.Use();
                        Repaint();
                    }
                    break;

                case EventType.MouseUp:
                    if (m_pendingListHeightDragging && GUIUtility.hotControl == controlId)
                    {
                        m_pendingListHeightDragging = false;
                        GUIUtility.hotControl = 0;
                        SaveListHeights();
                        current.Use();
                    }
                    break;
            }
        }


        // ── Pending staged edits (multi-apply)
        [Serializable]
        private class PendingEdit
        {
            public string path; public int line; public int column; public string code; public bool replace_line; public string search; public bool use_search = true; public int search_line_range = DEFAULT_SEARCH_LINE_RANGE;
        }

        private int CurrentLineOffset => LINE_OFFSET_STEPS[Mathf.Clamp(m_lineOffsetIndex, 0, LINE_OFFSET_STEPS.Length - 1)];

        private int GetEffectiveLine() => Mathf.Max(0, m_patchLine + CurrentLineOffset);

        private int GetClampedPreviewLine(string fileText)
        {
            return ClampToExistingLine(fileText, GetEffectiveLine());
        }

        private static int ClampToExistingLine(string text, int requestedLine)
        {
            // At least one logical line exists even for an empty file.
            int maxLineIndex = 0;
            if (!string.IsNullOrEmpty(text))
            {
                for (int i = 0; i < text.Length; i++)
                {
                    if (text[i] == '\n') maxLineIndex++;
                    else if (text[i] == '\r')
                    {
                        maxLineIndex++;
                        if (i + 1 < text.Length && text[i + 1] == '\n') i++; // skip \n after \r
                    }
                }
            }

            return Mathf.Clamp(requestedLine, 0, maxLineIndex);
        }

        private static void GetLineRangeSpan(string text, int startLine, int endLine, out int startIndex, out int endIndex)
        {
            startIndex = 0;
            endIndex = string.IsNullOrEmpty(text) ? 0 : text.Length;
            if (string.IsNullOrEmpty(text)) return;

            startLine = Mathf.Max(0, startLine);
            endLine = Mathf.Max(startLine, endLine);

            int line = 0;
            int i = 0;
            while (i < text.Length && line < startLine)
            {
                if (text[i++] == '\n') line++;
            }
            startIndex = i;

            while (i < text.Length && line <= endLine)
            {
                if (text[i++] == '\n') line++;
            }
            endIndex = i;
        }

        private static int NormalizeSearchLineRange(int range)
        {
            return range > 0 ? range : DEFAULT_SEARCH_LINE_RANGE;
        }

        private bool TryResolveLineBySearch(string fileText, string search, int targetLine, int lineRange, out int lineIndex)
        {
            lineIndex = -1;
            if (string.IsNullOrEmpty(fileText) || string.IsNullOrEmpty(search)) return false;

            string normFile = NormalizeNewlines(fileText);
            string normSearch = NormalizeNewlines(search);
            if (string.IsNullOrEmpty(normSearch)) return false;

            int normalizedRange = NormalizeSearchLineRange(lineRange);
            int desiredLine = ClampToExistingLine(normFile, targetLine);
            int minLine = Math.Max(0, desiredLine - normalizedRange);
            int maxLine = ClampToExistingLine(normFile, desiredLine + normalizedRange);
            GetLineRangeSpan(normFile, minLine, maxLine, out int windowStart, out int windowEnd);

            int bestLine = -1;
            int bestDistance = int.MaxValue;
            int idx = windowStart;
            while (idx <= windowEnd - normSearch.Length)
            {
                idx = normFile.IndexOf(normSearch, idx, windowEnd - idx, StringComparison.Ordinal);
                if (idx < 0) break;

                int candidateLine = CountLinesBeforeIndex(normFile, idx);
                int distance = Mathf.Abs(candidateLine - desiredLine);
                if (distance <= normalizedRange && distance < bestDistance)
                {
                    bestDistance = distance;
                    bestLine = candidateLine;
                    if (distance == 0) break; // exact hit inside the range
                }

                idx += Math.Max(1, normSearch.Length);
            }

            if (bestLine < 0) return false;

            lineIndex = bestLine;
            return true;
        }

        private static int CountLinesBeforeIndex(string text, int index)
        {
            if (string.IsNullOrEmpty(text)) return 0;
            index = Mathf.Clamp(index, 0, text.Length);
            int lines = 0;
            for (int i = 0; i < index; i++)
                if (text[i] == '\n') lines++;
            return lines;
        }

        private int ResolveTargetLine(string fileText, out bool usedSearch, out bool foundSearch)
        {
            usedSearch = m_useSearch && !string.IsNullOrWhiteSpace(m_patchSearchCode);
            foundSearch = false;

            int desiredLine = ClampToExistingLine(fileText, GetClampedPreviewLine(fileText));
            int searchLineRange = NormalizeSearchLineRange(m_patchSearchLineRange);

            if (usedSearch)
            {
                if (TryResolveLineBySearch(fileText, m_patchSearchCode, desiredLine, searchLineRange, out int lineFromSearch))
                {
                    foundSearch = true;
                    return ClampToExistingLine(fileText, lineFromSearch + CurrentLineOffset);
                }

                throw new InvalidOperationException($"Search text not found within ±{searchLineRange} lines of {desiredLine}.");
            }

            return GetClampedPreviewLine(fileText);
        }

        private bool TryReadTargetFile(out string text)
        {
            text = null;
            if (string.IsNullOrEmpty(m_patchTargetAssetPath)) return false;

            string abs = ToAbsolute(m_patchTargetAssetPath);
            if (!File.Exists(abs)) return false;

            try
            {
                text = File.ReadAllText(abs);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private int ResolveLineForSave(out bool usedSearch, out bool foundSearch)
        {
            usedSearch = m_useSearch && !string.IsNullOrWhiteSpace(m_patchSearchCode);
            foundSearch = false;

            if (TryReadTargetFile(out string text))
                return ResolveTargetLine(text, out usedSearch, out foundSearch);

            if (usedSearch)
                throw new InvalidOperationException("Search requires the target file to be readable.");

            return GetEffectiveLine();
        }

        private int GetDisplayLine()
        {
            if (TryReadTargetFile(out string text))
            {
                try { return ResolveTargetLine(text, out _, out _); }
                catch { return GetEffectiveLine(); }
            }

            return GetEffectiveLine();
        }

        private void UpdateLineOffsetIndex(int newIndex)
        {
            int clamped = Mathf.Clamp(newIndex, 0, LINE_OFFSET_STEPS.Length - 1);
            if (clamped == m_lineOffsetIndex) return;
            m_lineOffsetIndex = clamped;
            EditorPrefs.SetInt(PREF_LINE_OFFSET_INDEX, m_lineOffsetIndex);
        }

        private void LoadLineOffsetIndex()
        {
            int stored = EditorPrefs.GetInt(PREF_LINE_OFFSET_INDEX, DEFAULT_LINE_OFFSET_INDEX);
            m_lineOffsetIndex = Mathf.Clamp(stored, 0, LINE_OFFSET_STEPS.Length - 1);
        }

        private static string PendingKey(string pluginRoot) =>
            $"PluginInspector.Pending::{pluginRoot?.ToLowerInvariant()}";

        private static string PendingTimeKey(string pluginRoot) =>
            $"PluginInspector.PendingSavedAt::{pluginRoot?.ToLowerInvariant()}";

        [Serializable]
        private class PendingListWrapper { public List<PendingEdit> items = new List<PendingEdit>(); }

        private void SavePendingFor(string pluginRoot)
        {
            if (string.IsNullOrEmpty(pluginRoot) || m_pending == null) return;

            var w = new PendingListWrapper { items = m_pending };
            string json = EditorJsonUtility.ToJson(w, false);

            // Save the list (session + prefs)
            SessionState.SetString(PendingKey(pluginRoot), json);
            EditorPrefs.SetString(PendingKey(pluginRoot), json);

            // Save the timestamp (session + prefs)
            string when = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            SessionState.SetString(PendingTimeKey(pluginRoot), when);
            EditorPrefs.SetString(PendingTimeKey(pluginRoot), when);
            m_pendingSavedAt = when; // update in-memory status for immediate UI refresh
        }

        private void LoadPendingFor(string pluginRoot)
        {
            m_pending ??= new List<PendingEdit>();
            m_pending.Clear();
            m_pendingSavedAt = "";

            if (string.IsNullOrEmpty(pluginRoot)) return;

            // Load list
            string json = SessionState.GetString(PendingKey(pluginRoot), "");
            if (string.IsNullOrEmpty(json))
                json = EditorPrefs.GetString(PendingKey(pluginRoot), "");

            if (!string.IsNullOrEmpty(json))
            {
                var w = new PendingListWrapper();
                try { EditorJsonUtility.FromJsonOverwrite(json, w); } catch { /* ignore */ }
                if (w.items != null) m_pending = w.items;
            }

            // Load timestamp
            m_pendingSavedAt = SessionState.GetString(
                PendingTimeKey(pluginRoot),
                EditorPrefs.GetString(PendingTimeKey(pluginRoot), "")
            );
        }

        private void ClearPendingFor(string pluginRoot)
        {
            if (string.IsNullOrEmpty(pluginRoot)) return;
            SessionState.EraseString(PendingKey(pluginRoot));
            EditorPrefs.DeleteKey(PendingKey(pluginRoot));

            SessionState.EraseString(PendingTimeKey(pluginRoot));
            EditorPrefs.DeleteKey(PendingTimeKey(pluginRoot));
            m_pendingSavedAt = "";
        }

        private void ScanAndSetCoreFiles()
        {
            m_coreFiles = ScanCoreFilesUnder(m_coreFolderRoot);
            if (m_coreFiles.Count > 0)
            {
                int found = m_coreFiles.FindIndex(p => string.Equals(p, m_patchTargetAssetPath, StringComparison.OrdinalIgnoreCase));
                m_coreIndex = found >= 0 ? found : 0;
                m_patchTargetAssetPath = m_coreFiles[m_coreIndex];
            }
            Repaint();
        }

        private List<string> ScanCoreFilesUnder(string assetsFolder)
        {
            var list = new List<string>();
            if (string.IsNullOrEmpty(assetsFolder)) return list;
            if (!assetsFolder.Replace('\\', '/').StartsWith("Assets/")) return list;

            string absRoot = ToAbsolute(assetsFolder);
            if (!Directory.Exists(absRoot)) return list;

            foreach (var ext in CORE_EXTS)
                list.AddRange(Directory.GetFiles(absRoot, "*" + ext, SearchOption.AllDirectories));

            string absAssets = Application.dataPath.Replace('\\', '/');
            for (int i = 0; i < list.Count; i++)
            {
                string abs = list[i].Replace('\\', '/');
                list[i] = "Assets" + abs.Substring(absAssets.Length);
            }
            list.Sort(StringComparer.OrdinalIgnoreCase);
            return list;
        }

        private static string ToAbsolute(string assetsPath)
        {
            string projRoot = Directory.GetParent(Application.dataPath)!.FullName.Replace('\\', '/');
            return Path.Combine(projRoot, assetsPath).Replace('\\', '/');
        }

        private void DrawPluginHeader(string pluginRoot)
        {
            EnsureMetadataLoaded(pluginRoot);

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.BeginVertical(GUILayout.Width(160f));
            DrawPreviewBox(pluginRoot);
            EditorGUILayout.EndVertical();

            GUILayout.Space(8f);

            EditorGUILayout.BeginVertical();



            //Title Label Position
            string title = $"Patch Builder — {Path.GetFileName(pluginRoot)}";

            Rect titleRect = GUILayoutUtility.GetRect(new GUIContent(title), EditorStyles.boldLabel, GUILayout.ExpandWidth(true));
            titleRect.x -= 19f;   // left
            titleRect.y -= 4f;   // up
            EditorGUI.LabelField(titleRect, title, EditorStyles.boldLabel);




            DrawDescriptionField(pluginRoot);
            EditorGUILayout.EndVertical();

            EditorGUILayout.EndHorizontal();
            GUILayout.Space(6f);
        }

        private void DrawIconBox(string pluginRoot)
        {
            Rect iconRect = GUILayoutUtility.GetRect(64f, 64f, GUILayout.Width(64f), GUILayout.Height(64f));
            EditorGUI.DrawRect(iconRect, new Color32(0x1E, 0x1E, 0x1E, 0xFF));

            if (m_iconTexture != null)
            {
                UnityEngine.GUI.DrawTexture(iconRect, m_iconTexture, ScaleMode.ScaleToFit);
            }
            else
            {
                EditorGUI.LabelField(iconRect, "Icon", EditorStyles.centeredGreyMiniLabel);
            }

            float buttonWidth = 58f;
            float buttonHeight = 20f;
            Rect buttonRect = new Rect(iconRect.xMax - buttonWidth - 2f, iconRect.yMax - buttonHeight - 2f, buttonWidth, buttonHeight);
            string buttonLabel = string.IsNullOrEmpty(m_iconRelativePath) ? "load" : "clear";
            if (UnityEngine.GUI.Button(buttonRect, buttonLabel))
            {
                if (string.IsNullOrEmpty(m_iconRelativePath))
                    PickIconImage(pluginRoot);
                else
                    UnloadIconImage(pluginRoot);
            }
        }

        private void DrawPreviewBox(string pluginRoot)
        {
            Rect paddedRect = GUILayoutUtility.GetRect(144f, 140f, GUILayout.Width(144f), GUILayout.Height(140f));
            Rect previewRect = new Rect(paddedRect.x + 4f, paddedRect.y, 140f, 140f);
            EditorGUI.DrawRect(previewRect, new Color32(0x1E, 0x1E, 0x1E, 0xFF));

            if (m_previewTexture != null)
            {
                UnityEngine.GUI.DrawTexture(previewRect, m_previewTexture, ScaleMode.ScaleToFit);
            }
            else
            {
                EditorGUI.LabelField(previewRect, "Preview", EditorStyles.centeredGreyMiniLabel);
            }

            float buttonWidth = 90f;
            float buttonHeight = 22f;
            Rect buttonRect = new Rect(previewRect.xMax - buttonWidth - 2f, previewRect.yMax - buttonHeight - 2f, buttonWidth, buttonHeight);
            string buttonLabel = string.IsNullOrEmpty(m_previewImageRelativePath) ? "load image" : "unload image";
            if (UnityEngine.GUI.Button(buttonRect, buttonLabel))
            {
                if (string.IsNullOrEmpty(m_previewImageRelativePath))
                    PickPreviewImage(pluginRoot);
                else
                    UnloadPreviewImage(pluginRoot);
            }

            if (m_previewTexture != null)
            {
                EditorGUIUtility.AddCursorRect(previewRect, MouseCursor.Zoom);
                if (Event.current.type == EventType.MouseDown && Event.current.button == 0 && previewRect.Contains(Event.current.mousePosition))
                {
                    PluginPreviewWindow.Show(m_previewTexture, Path.GetFileName(pluginRoot));
                    Event.current.Use();
                }
            }
        }

        private void DrawDescriptionField(string pluginRoot)
        {
            string desc = m_pluginDescription ?? string.Empty;

            EditorGUI.BeginChangeCheck();

            const float baseHeight = 118f;
            const float extraDown = 1f;

            Rect areaRect = GUILayoutUtility.GetRect(
                GUIContent.none,
                m_descriptionStyle,
                GUILayout.Height(baseHeight + extraDown),   
                GUILayout.ExpandWidth(true));

            areaRect.xMin -= 18f;                
            areaRect.height += extraDown;  

            string newDesc = EditorGUI.TextArea(areaRect, desc, m_descriptionStyle);

            if (newDesc.Length > DescriptionCharacterLimit)
                newDesc = newDesc.Substring(0, DescriptionCharacterLimit);

            if (EditorGUI.EndChangeCheck())
            {
                m_pluginDescription = newDesc;
                SavePluginMetadata(pluginRoot);
            }
        }



        private void DrawPatchBuilder(string pluginRoot)
        {
            // swap pending/selection on plugin change (you already do pending; add manifest load)
            if (!string.Equals(m_activePluginRoot, pluginRoot, StringComparison.OrdinalIgnoreCase))
            {
                if (!string.IsNullOrEmpty(m_activePluginRoot))
                {
                    SavePendingFor(m_activePluginRoot);
                    SavePluginMetadata(m_activePluginRoot);
                }

                LoadPendingFor(pluginRoot);
                LoadManifest(pluginRoot);
                LoadPluginMetadata(pluginRoot);
                m_activePluginRoot = pluginRoot;
            }
            else
            {
                EnsureManifestSynced(pluginRoot);
                EnsureMetadataLoaded(pluginRoot);
            }

            EnsureListHeightsLoaded();

            GUILayout.Space(4);
            using (var patchScroll = new EditorGUILayout.ScrollViewScope(m_patchBuilderScroll))
            {
                m_patchBuilderScroll = patchScroll.scrollPosition;
                m_patchBuilderScroll.x = 0f; // don’t keep/accumulate horizontal drift
                DrawPluginHeader(pluginRoot);

                const float iconNudgeRight = 4f;
                const float iconNudgeDown = 3f;

                EditorGUILayout.BeginHorizontal();

                GUILayout.Space(iconNudgeRight);

                EditorGUILayout.BeginVertical(GUILayout.Width(64f));
                GUILayout.Space(iconNudgeDown);
                DrawIconBox(pluginRoot);
                EditorGUILayout.EndVertical();

                GUILayout.Space(8f - iconNudgeRight);

                EditorGUILayout.BeginVertical();
                


                // Root folder picker (single folder; default "Assets/PLAYER TWO/")
                EditorGUILayout.BeginHorizontal();
                EditorGUI.BeginChangeCheck();
                m_coreFolderRoot = EditorGUILayout.TextField(
                    TT("Core files root (Assets/…)", "Folder under Assets to scan for eligible core files."),
                    m_coreFolderRoot);
                if (GUILayout.Button(TT("Pick", "Choose a folder under Assets"), GUILayout.Width(60)))
                {
                    string abs = EditorUtility.OpenFolderPanel("Select core files root (inside Assets)", Application.dataPath, "");
                    if (!string.IsNullOrEmpty(abs))
                    {
                        abs = abs.Replace('\\', '/');
                        string absAssets = Application.dataPath.Replace('\\', '/');
                        if (!abs.StartsWith(absAssets))
                            EditorUtility.DisplayDialog("Invalid folder", "Please choose a folder INSIDE Assets/.", "OK");
                        else
                            m_coreFolderRoot = "Assets" + abs.Substring(absAssets.Length);
                    }
                }
                if (GUILayout.Button(TT("Refresh", "Rescan files under the selected root"), GUILayout.Width(70))) ScanAndSetCoreFiles();
                if (EditorGUI.EndChangeCheck())
                {
                    EditorPrefs.SetString("PluginInspector.CoreRoot", m_coreFolderRoot);
                    ScanAndSetCoreFiles();
                }
                EditorGUILayout.EndHorizontal();

                // ─────────────────────────────────────────────────────────────────────
                // Target Core File — searchable dropdown + Ping
                // ─────────────────────────────────────────────────────────────────────
                if (m_coreFiles == null) ScanAndSetCoreFiles();

                if (m_coreFiles == null || m_coreFiles.Count == 0)
                {
                    EditorGUILayout.HelpBox(
                        $"No core files found under: {m_coreFolderRoot}\nAllowed: {string.Join(", ", CORE_EXTS)}",
                        MessageType.Info
                    );
                }
                else
                {
                    if (m_coreIndex < 0 || m_coreIndex >= m_coreFiles.Count) m_coreIndex = 0;
                    if (string.IsNullOrEmpty(m_patchTargetAssetPath)) m_patchTargetAssetPath = m_coreFiles[m_coreIndex];

                    string rootPrefix = m_coreFolderRoot.EndsWith("/") ? m_coreFolderRoot : (m_coreFolderRoot + "/");
                    var labels = m_coreFiles
                        .Select(p => p.StartsWith(rootPrefix, StringComparison.OrdinalIgnoreCase) ? p.Substring(rootPrefix.Length) : p)
                        .ToList();

                    // One row: "Target Core File" [searchable dropdown button] [Ping]
                    Rect row = EditorGUILayout.GetControlRect();
                    var labelRect = row; labelRect.width = EditorGUIUtility.labelWidth;
                    EditorGUI.PrefixLabel(labelRect, TT("Target Core File", "File in your core package that will receive this edit."));

                    // Button styled like a popup
                    var popupRect = row; popupRect.xMin = labelRect.xMax; popupRect.width = row.width - EditorGUIUtility.labelWidth - 60f;
                    if (UnityEngine.GUI.Button(popupRect, new GUIContent(labels[m_coreIndex], m_coreFiles[m_coreIndex]), EditorStyles.popup))
                    {
                        PopupWindow.Show(popupRect, new SearchablePathPopup(
                            title: "Select Core File",
                            allAssetPaths: m_coreFiles,
                            shownLabels: labels,
                            selectedIndex: m_coreIndex,
                            onSelect: (idx) => { m_coreIndex = idx; m_patchTargetAssetPath = m_coreFiles[m_coreIndex]; Repaint(); }
                        ));
                    }

                    // Ping button (no Selected path field anymore)
                    var pingRect = row; pingRect.width = 50f; pingRect.x = row.xMax - pingRect.width;
                    if (UnityEngine.GUI.Button(pingRect, TT("Ping", "Ping the selected file in Project window")))
                    {
                        var obj = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(m_coreFiles[m_coreIndex]);
                        if (obj != null) EditorGUIUtility.PingObject(obj);
                    }
                }

                EditorGUILayout.EndVertical();
                EditorGUILayout.EndHorizontal();

                GUILayout.Space(6);

                EditorGUILayout.LabelField("Code Locator Type", EditorStyles.boldLabel);
                using (new EditorGUILayout.HorizontalScope())
                {
                    bool wantSearch = GUILayout.Toggle(m_useSearch, TT("Find by code", "Search the target file for a matching snippet and anchor to the first hit."), EditorStyles.miniButtonLeft);
                    bool wantLine = GUILayout.Toggle(!m_useSearch, TT("Use line number", "Skip searching and rely only on the line number."), EditorStyles.miniButtonRight);

                    if (wantSearch != m_useSearch && wantSearch)
                        m_useSearch = true;
                    else if (wantLine == true && m_useSearch)
                        m_useSearch = false;
                }

                if (m_useSearch)
                {
                    m_patchSearchCode = DrawAutoGrowCodeArea(
                       TT("Search for code", "Searches the target file and anchors to a match within the configured line window. Offset still applies."),
                       m_patchSearchCode,
                       ref m_patchSearchScroll,
                       "PatchSearchArea",
                       380f
                   );

                    // Optional: keep this field in sync if other code expects it
                    m_patchSearchHighlightedCode = m_patchSearchCode;


                    m_patchSearchLineRange = Mathf.Max(0, EditorGUILayout.IntField(
                        TT("Search line window (+/-)", "Limits matches to this many lines above or below the requested line (0 uses the default)."),
                        m_patchSearchLineRange));
                }

                // Position & Code
                using (new EditorGUILayout.HorizontalScope())
                {
                    string lineLabel = m_useSearch ? "Line number (0-based)" : "Line number (0-based)";
                    m_patchLine = EditorGUILayout.IntField(
                        TT(lineLabel, "Insert after this line, unless it is a '}' line (then insert before it). Use the offset buttons to nudge the effective line (applied relative to the found snippet when searching)."),
                        Mathf.Max(0, m_patchLine));

                    GUILayout.Space(6f);

                    int offset = CurrentLineOffset;
                    string offsetLabel = offset == 0
                        ? "Offset 0"
                        : $"Offset {offset:+#;-#}";
                    GUILayout.Label(
                        TT(offsetLabel, "Additional line offset applied when previewing and saving."),
                        EditorStyles.miniLabel,
                        GUILayout.Width(80f));

                    using (new EditorGUI.DisabledScope(m_lineOffsetIndex <= 0))
                    {
                        if (GUILayout.Button("-", EditorStyles.miniButtonLeft, GUILayout.Width(22f)))
                            UpdateLineOffsetIndex(m_lineOffsetIndex - 1);
                    }
                    using (new EditorGUI.DisabledScope(m_lineOffsetIndex >= LINE_OFFSET_STEPS.Length - 1))
                    {
                        if (GUILayout.Button("+", EditorStyles.miniButtonRight, GUILayout.Width(22f)))
                            UpdateLineOffsetIndex(m_lineOffsetIndex + 1);
                    }

                    GUILayout.Space(6f);
                    GUILayout.Label(
                        TT($"Used: {GetDisplayLine()}", "Effective line after applying the offset."),
                        EditorStyles.miniLabel,
                        GUILayout.Width(80f));
                }
                m_patchRow = EditorGUILayout.IntField(
                    TT("Row number", "Indent hint (column). Final indent = max(current line indent, this)."),
                    Mathf.Max(0, m_patchRow));

                m_replaceLine = EditorGUILayout.Toggle(
                    TT("Replace target line", "When enabled, the selected line is removed and replaced by the patch block."),
                    m_replaceLine);

                DrawInlinePatchEditor(pluginRoot);

                // ─────────────────────────────────────────────────────────
                // NEW: draw the status HelpBox HERE (full width, red line spot)
                // ─────────────────────────────────────────────────────────
                GUILayout.Space(8);

                DrawInlinePreviewStatusHelpBox();

                GUILayout.Space(8);

                using (new EditorGUILayout.VerticalScope())
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        using (new EditorGUILayout.VerticalScope("box"))
                        {
                            EditorGUILayout.LabelField("Staging", EditorStyles.boldLabel);
                            using (new EditorGUILayout.HorizontalScope())
                            {
                                if (GUILayout.Button(TT("Add edit to list", "Stage this edit (does not touch the manifest yet)"), GUILayout.Height(24)))
                                {
                                    if (string.IsNullOrWhiteSpace(m_patchTargetAssetPath))
                                    {
                                        EditorUtility.DisplayDialog("Missing target", "Please select a Target Core File.", "OK");
                                    }
                                    else
                                    {
                                        try
                                        {
                                            int resolvedLine = ResolveLineForSave(out bool usedSearch, out bool foundSearch);
                                            m_pending.Add(new PendingEdit
                                            {
                                                path = m_patchTargetAssetPath,
                                                line = resolvedLine,
                                                column = m_patchRow,
                                                code = m_patchCode,
                                                replace_line = m_replaceLine,
                                                search = m_patchSearchCode,
                                                use_search = m_useSearch,
                                                search_line_range = NormalizeSearchLineRange(m_patchSearchLineRange)
                                            });
                                            SavePendingFor(m_activePluginRoot);

                                            if (usedSearch && !foundSearch)
                                                ShowNotification(new GUIContent("Search text not found."));
                                        }
                                        catch (Exception ex)
                                        {
                                            ShowNotification(new GUIContent(ex.Message));
                                        }

                                    }
                                }

                                if (GUILayout.Button(TT("Clear list", "Clear the staged (pending) edits"), GUILayout.Height(24)))
                                {
                                    m_pending.Clear();
                                    SavePendingFor(m_activePluginRoot);
                                }
                            }
                        }

                        GUILayout.Space(6);

                        using (new EditorGUILayout.VerticalScope("box"))
                        {
                            EditorGUILayout.LabelField("Save & apply", EditorStyles.boldLabel);
                            using (new EditorGUILayout.HorizontalScope())
                            {
                                // Save (Append)
                                if (GUILayout.Button(TT("Save (Append)", "Append staged edits to plugin.patches.json"), GUILayout.Height(24)))
                                {
                                    try
                                    {
                                        if (m_pending.Count == 0)
                                            EditorUtility.DisplayDialog("Nothing to save", "Add at least one edit to the list.", "OK");
                                        else
                                        {
                                            SaveManifest(pluginRoot, m_pending, SaveMode.Append);
                                            AssetDatabase.Refresh();
                                            EditorUtility.DisplayDialog("Saved", "plugin.patches.json updated (Appended).", "OK");
                                        }
                                    }
                                    catch (Exception ex) { EditorUtility.DisplayDialog("Error", ex.Message, "OK"); }
                                }

                                // Save (Replace)
                                if (GUILayout.Button(TT("Save (Replace)", "Replace plugin.patches.json with only the staged edits"), GUILayout.Height(24)))
                                {
                                    try
                                    {
                                        SaveManifest(pluginRoot, m_pending, SaveMode.Replace);
                                        AssetDatabase.Refresh();
                                        EditorUtility.DisplayDialog("Saved", "plugin.patches.json replaced.", "OK");
                                    }
                                    catch (Exception ex) { EditorUtility.DisplayDialog("Error", ex.Message, "OK"); }
                                }
                            }

                            GUILayout.Space(4);

                            // Replace & Apply All
                            if (GUILayout.Button(TT("Replace & Apply All", "Replace manifest then apply all edits to core files"), GUILayout.Height(24)))
                            {
                                try
                                {
                                    SaveManifest(pluginRoot, m_pending, SaveMode.Replace);
                                    var rep = PluginCorePatcher.ApplyAll(pluginRoot);
                                    EditorUtility.DisplayDialog("Core Edits Applied",
                                        $"Files touched: {rep.filesTouched}\nOps applied: {rep.opsApplied}\nSkipped: {rep.opsSkipped}",
                                        "OK");
                                    m_pending.Clear();
                                }
                                catch (Exception ex) { EditorUtility.DisplayDialog("Error", ex.Message, "OK"); }
                            }
                        }
                    }
                }
                // Existing Edits in Manifest (search/select/edit/apply/revert)
                // ─────────────────────────────────────────────────────────────────────
                GUILayout.Space(8);
                EditorGUILayout.LabelField("Existing Edits (from plugin.patches.json)", EditorStyles.boldLabel);

                // Search + Reload
                EditorGUILayout.BeginHorizontal();
                m_manifestSearch = EditorGUILayout.TextField(TT("Search", "Filter by file name, id, or snippet contents"), m_manifestSearch);
                if (GUILayout.Button(TT("Reload", "Reload the manifest from disk"), GUILayout.Width(70))) LoadManifest(pluginRoot);
                EditorGUILayout.EndHorizontal();

                if (m_manifest.targets == null || m_manifest.targets.Count == 0)
                {
                    EditorGUILayout.HelpBox("No edits in manifest yet.", MessageType.Info);
                }
                else
                {
                    // Build flat filtered list: (targetIndex, opIndex, label)
                    m_manifestItems.Clear();
                    string s = (m_manifestSearch ?? "").Trim().ToLowerInvariant();
                    var fileCache = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

                    for (int t = 0; t < m_manifest.targets.Count; t++)
                    {
                        var tgt = m_manifest.targets[t];
                        if (tgt?.insert_at == null) continue;
                        for (int o = 0; o < tgt.insert_at.Count; o++)
                        {
                            var op = tgt.insert_at[o];
                            string file = tgt.path ?? "";
                            string name = Path.GetFileName(file);
                            var state = GetManifestEditState(pluginRoot, tgt, op, fileCache);
                            string mode = op.replace_line ? "Replace" : "Insert";
                            string anchor = BuildAnchorLabel(op);
                            string label = $"{StatePrefix(state)}[{mode}] {name} — {anchor} — {op.id}";
                            if (string.IsNullOrEmpty(s) || label.ToLowerInvariant().Contains(s) || file.ToLowerInvariant().Contains(s) || (op.insert ?? "").ToLowerInvariant().Contains(s))
                            {
                                m_manifestItems.Add(new ManifestListItem
                                {
                                    target = tgt,
                                    op = op,
                                    label = label,
                                    state = state
                                });
                            }
                        }
                    }

                    EnsureManifestList(pluginRoot, string.IsNullOrEmpty(s));

                    float manifestContentHeight = m_manifestList.GetHeight();
                    float manifestViewHeight = Mathf.Clamp(m_manifestListHeight, MANIFEST_LIST_MIN_HEIGHT, GetManifestListHeightLimit());
                    manifestViewHeight = Mathf.Max(manifestViewHeight, EditorGUIUtility.singleLineHeight * 2f);

                    using (var sv = new EditorGUILayout.ScrollViewScope(m_manifestScroll, GUILayout.Height(manifestViewHeight)))
                    {
                        m_manifestScroll = sv.scrollPosition;

                        Rect listRect = GUILayoutUtility.GetRect(
                            GUIContent.none,
                            GUIStyle.none,
                            GUILayout.Height(manifestContentHeight),
                            GUILayout.ExpandWidth(true));

                        m_manifestList.DoList(listRect);
                    }

                    // draggable height splitter (like the code blocks)
                    Rect manifestSplitterRect = GUILayoutUtility.GetRect(1f, 6f, GUILayout.ExpandWidth(true));
                    DrawManifestListHeightSplitter(manifestSplitterRect);


                    GUILayout.Space(0);

                    using (new EditorGUILayout.VerticalScope("box"))
                    {
                        EditorGUILayout.LabelField("Selected edit", EditorStyles.boldLabel);
                        using (new EditorGUILayout.HorizontalScope())
                        {
                            if (GUILayout.Button(TT("Apply Selected", "Apply only this edit to its target file"), GUILayout.Height(22)))
                            {
                                if (m_selTargetIdx >= 0 && m_selOpIdx >= 0)
                                {
                                    var t = m_manifest.targets[m_selTargetIdx];
                                    var o = t.insert_at[m_selOpIdx];
                                    try
                                    {
                                        // reapply without touching others
                                        PluginCorePatcher.ApplyInsertAt(pluginRoot, t.path, ToPatcherOp(o));
                                        EditorUtility.DisplayDialog("Applied", $"Applied {o.id} to {t.path}.", "OK");
                                    }
                                    catch (Exception ex) { EditorUtility.DisplayDialog("Error", ex.Message, "OK"); }
                                }
                            }

                            if (GUILayout.Button(TT("Update Selected", "Write the values from the fields above back into the selected manifest entry"), GUILayout.Height(22)))
                            {
                                if (m_selTargetIdx >= 0 && m_selOpIdx >= 0)
                                {
                                    var t = m_manifest.targets[m_selTargetIdx];
                                    var o = t.insert_at[m_selOpIdx];
                                    // Keep the SAME id; update other fields
                                    try
                                    {
                                        int resolvedLine = ResolveLineForSave(out bool usedSearch, out bool foundSearch);
                                        o.line = resolvedLine;
                                        o.column = m_patchRow;
                                        o.insert = m_patchCode;
                                        o.replace_line = m_replaceLine;
                                        o.search = m_patchSearchCode;
                                        o.use_search = m_useSearch;
                                        o.search_line_range = NormalizeSearchLineRange(m_patchSearchLineRange);

                                        // Ensure path matches dropdown (if user switched file)
                                        t.path = m_patchTargetAssetPath;

                                        SaveManifestReplace(pluginRoot);
                                        EditorUtility.DisplayDialog("Updated", $"Edit {o.id} updated in manifest.", "OK");

                                        if (usedSearch && !foundSearch)
                                            ShowNotification(new GUIContent("Search text not found."));
                                    }
                                    catch (Exception ex)
                                    {
                                        ShowNotification(new GUIContent(ex.Message));
                                    }
                                }
                            }

                            if (GUILayout.Button(TT("Revert Selected", "Remove code previously inserted by this edit id"), GUILayout.Height(22)))
                            {
                                if (m_selTargetIdx >= 0 && m_selOpIdx >= 0)
                                {
                                    var o = m_manifest.targets[m_selTargetIdx].insert_at[m_selOpIdx];
                                    try
                                    {
                                        int r = PluginCorePatcher.RevertById(pluginRoot, o.id);
                                        EditorUtility.DisplayDialog("Reverted", $"Removed {r} region(s) for {o.id}.", "OK");
                                    }
                                    catch (Exception ex) { EditorUtility.DisplayDialog("Error", ex.Message, "OK"); }
                                }
                            }

                            if (GUILayout.Button(TT("Reinstall Selected", "Revert then apply this edit again"), GUILayout.Height(22)))
                            {
                                if (m_selTargetIdx >= 0 && m_selOpIdx >= 0)
                                {
                                    var t = m_manifest.targets[m_selTargetIdx];
                                    var o = t.insert_at[m_selOpIdx];
                                    try
                                    {
                                        // revert that id, then apply again
                                        int r = PluginCorePatcher.RevertById(pluginRoot, o.id);
                                        PluginCorePatcher.ApplyInsertAt(pluginRoot, t.path, ToPatcherOp(o));
                                        EditorUtility.DisplayDialog("Reinstalled", $"Reverted {r} region(s), then applied {o.id}.", "OK");
                                    }
                                    catch (Exception ex) { EditorUtility.DisplayDialog("Error", ex.Message, "OK"); }
                                }
                            }

                        }
                    }
                }

                GUILayout.Space(6);

                using (new EditorGUILayout.VerticalScope("box"))
                {
                    m_showManifestMaintenance = EditorGUILayout.Foldout(m_showManifestMaintenance, "Manifest maintenance", true);
                    if (m_showManifestMaintenance)
                    {
                        EditorGUI.indentLevel++;
                        using (new EditorGUILayout.VerticalScope())
                        {
                            using (new EditorGUILayout.HorizontalScope())
                            {
                                if (GUILayout.Button(TT("Apply All Core Edits", "Apply all edits currently in plugin.patches.json"), GUILayout.Height(20)))
                                {
                                    try
                                    {
                                        var rep = PluginCorePatcher.ApplyAll(pluginRoot);
                                        EditorUtility.DisplayDialog("Core Edits Applied",
                                            $"Files touched: {rep.filesTouched}\nOps applied: {rep.opsApplied}\nSkipped: {rep.opsSkipped}",
                                            "OK");
                                    }
                                    catch (Exception ex)
                                    {
                                        Debug.LogException(ex);
                                        EditorUtility.DisplayDialog("Error while applying", ex.Message, "OK");
                                    }
                                }
                                if (GUILayout.Button(TT("Revert All Core Edits", "Remove all previously inserted regions for this plugin"), GUILayout.Height(20)))
                                {
                                    try
                                    {
                                        var rep = PluginCorePatcher.RevertAll(pluginRoot);
                                        EditorUtility.DisplayDialog("Core Edits Reverted",
                                            $"Reverted regions: {rep.opsReverted}\nFiles touched: {rep.filesTouched}",
                                            "OK");
                                    }
                                    catch (Exception ex)
                                    {
                                        Debug.LogException(ex);
                                        EditorUtility.DisplayDialog("Error while reverting", ex.Message, "OK");
                                    }
                                }
                            }

                            using (new EditorGUILayout.HorizontalScope())
                            {
                                if (GUILayout.Button(TT("Clear Manifest", "Delete plugin.patches.json from this plugin folder"), GUILayout.Height(20)))
                                {
                                    try
                                    {
                                        string manifestPath = Path.Combine(pluginRoot, "plugin.patches.json");
                                        if (File.Exists(manifestPath)) File.Delete(manifestPath);
                                        string manifestMetaPath = manifestPath + ".meta";
                                        if (File.Exists(manifestMetaPath)) File.Delete(manifestMetaPath);
                                        AssetDatabase.Refresh();
                                        EditorUtility.DisplayDialog("Cleared", "plugin.patches.json deleted.", "OK");
                                    }
                                    catch (Exception ex) { EditorUtility.DisplayDialog("Error", ex.Message, "OK"); }
                                }
                                if (GUILayout.Button(TT("Normalize Manifest", "Remove duplicates and restamp stable IDs"), GUILayout.Height(20)))
                                {
                                    try
                                    {
                                        NormalizeManifest(pluginRoot);
                                        AssetDatabase.Refresh();
                                        EditorUtility.DisplayDialog("Normalized", "Duplicates removed and IDs stabilized.", "OK");
                                    }
                                    catch (Exception ex) { EditorUtility.DisplayDialog("Error", ex.Message, "OK"); }
                                }
                            }
                        }
                        EditorGUI.indentLevel--;
                    }
                }

                GUILayout.Space(6);

                EditorGUILayout.LabelField($"Pending edits ({m_pending.Count})", EditorStyles.boldLabel);

                // Pending list preview
                if (m_pending.Count > 0)
                {
                    GUILayout.Space(6);

                    float pendingViewHeight = Mathf.Clamp(m_pendingListHeight, PENDING_LIST_MIN_HEIGHT, GetPendingListHeightLimit());

                    using (var pendingSv = new EditorGUILayout.ScrollViewScope(m_pendingScroll, GUILayout.Height(pendingViewHeight)))
                    {
                        m_pendingScroll = pendingSv.scrollPosition;

                        for (int i = 0; i < m_pending.Count; i++)
                        {
                            var e = m_pending[i];
                            EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
                            EditorGUILayout.LabelField(
                                $"{Path.GetFileName(e.path)}  —  {BuildAnchorLabel(e.search, e.use_search, e.line, e.column, e.search_line_range)}  —  {(e.replace_line ? "Replace" : "Insert")}",
                                GUILayout.MaxHeight(18)
                            );
                            if (GUILayout.Button(TT("Remove", "Remove this staged item"), GUILayout.Width(70)))
                            {
                                m_pending.RemoveAt(i);
                                SavePendingFor(m_activePluginRoot);
                                i--;
                            }
                            EditorGUILayout.EndHorizontal();
                        }
                    }

                    // draggable height splitter (like the code blocks)
                    Rect pendingSplitterRect = GUILayoutUtility.GetRect(1f, 6f, GUILayout.ExpandWidth(true));
                    DrawPendingListHeightSplitter(pendingSplitterRect);
                }


                GUILayout.Space(6);

                // Status line (right-aligned, tiny)
                GUILayout.Space(2);
                EditorGUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();

                string status = string.IsNullOrEmpty(m_pendingSavedAt)
                    ? $"Staged edits: {m_pending.Count} • not saved yet"
                    : $"Staged edits: {m_pending.Count} • autosaved {m_pendingSavedAt}";

                GUILayout.Label(status, EditorStyles.miniLabel);
                EditorGUILayout.EndHorizontal();

                GUILayout.Space(6);
                EditorGUILayout.HelpBox(
                    "Search-first: when enabled, the tool finds a match of your search snippet within the configured line window and applies the offset relative to that line. If no match is found, the edit cannot be staged or applied until the snippet is adjusted.\n" +
                    "Line/Row are 0-based.\n" +
                    "The edit is appended at the END of the selected line and inserts EXACTLY one newline before your code.\n" +
                    "Row (column) is used as an indent HINT; final indent is max(line’s leading spaces, Row) plus any “Indent + levels”.",
                    MessageType.Info
                );
            }
        }

        private static GUIStyle s_AutoGrowCodeAreaStyle;

        /// <summary>
        /// Auto-growing code-ish text area.
        /// - Grows to content up to maxHeight
        /// - Only uses scroll view (and scrollbars) when content overflows
        /// </summary>
        private string DrawAutoGrowCodeArea(
            GUIContent label,
            string text,
            ref Vector2 scroll,
            string controlName,
            float maxHeight)
        {
            EditorGUILayout.LabelField(label);

            text ??= string.Empty;

            s_AutoGrowCodeAreaStyle ??= new GUIStyle(EditorStyles.textArea)
            {
                wordWrap = false
            };

            string norm = NormalizeNewlines(text);

            // Probe width without consuming height
            Rect probe = EditorGUILayout.GetControlRect(false, 0f);
            float availableWidth = Mathf.Max(10f, probe.width);

            // Measure required height (no wrapping, so basically line count + padding)
            float neededHeight = s_AutoGrowCodeAreaStyle.CalcHeight(new GUIContent(norm), availableWidth);

            // Minimum: one line tall
            float minHeight = Mathf.Max(
                EditorGUIUtility.singleLineHeight + s_AutoGrowCodeAreaStyle.padding.vertical + 4f,
                18f
            );

            float viewHeight = Mathf.Clamp(neededHeight, minHeight, Mathf.Max(minHeight, maxHeight));

            // Measure required width for horizontal overflow detection
            float neededWidth = CalcMaxLineWidth(s_AutoGrowCodeAreaStyle, norm)
                + s_AutoGrowCodeAreaStyle.padding.horizontal
                + 2f;

            bool needsV = neededHeight > viewHeight + 0.5f;
            bool needsH = neededWidth > availableWidth + 0.5f;

            // Reserve the final rect once
            Rect frame = GUILayoutUtility.GetRect(
                GUIContent.none,
                s_AutoGrowCodeAreaStyle,
                GUILayout.Height(viewHeight),
                GUILayout.ExpandWidth(true)
            );

            // Draw background/border like a text area
            UnityEngine.GUI.Box(frame, GUIContent.none, EditorStyles.textArea);

            if (!needsV && !needsH)
            {
                // No scroll view at all -> no scrollbars possible
                UnityEngine.GUI.SetNextControlName(controlName);
                return EditorGUI.TextArea(frame, text, s_AutoGrowCodeAreaStyle);
            }

            // Scroll view path (scrollbars appear only because overflow exists)
            float contentW = Mathf.Max(frame.width, neededWidth);
            float contentH = Mathf.Max(frame.height, neededHeight);

            // If a scrollbar is not needed in a direction, keep that scroll at 0
            if (!needsV) scroll.y = 0f;
            if (!needsH) scroll.x = 0f;

            Rect viewRect = new Rect(0f, 0f, contentW, contentH);

            scroll = UnityEngine.GUI.BeginScrollView(frame, scroll, viewRect, false, false);

            UnityEngine.GUI.SetNextControlName(controlName);
            text = EditorGUI.TextArea(new Rect(0f, 0f, contentW, contentH), text, s_AutoGrowCodeAreaStyle);

            UnityEngine.GUI.EndScrollView();

            return text;
        }

        private static float CalcMaxLineWidth(GUIStyle style, string normalizedText)
        {
            if (string.IsNullOrEmpty(normalizedText))
                return 0f;

            float max = 0f;
            int start = 0;

            for (int i = 0; i <= normalizedText.Length; i++)
            {
                bool end = i == normalizedText.Length;
                if (!end && normalizedText[i] != '\n') continue;

                int len = i - start;
                string line = len > 0 ? normalizedText.Substring(start, len) : string.Empty;
                float w = style.CalcSize(new GUIContent(line)).x;
                if (w > max) max = w;

                start = i + 1;
            }

            return max;
        }


        private static void EnsurePreviewStyles()
        {
            if (s_PreviewBgTex == null)
            {
                s_PreviewBgTex = new Texture2D(1, 1);
                s_PreviewBgTex.SetPixel(0, 0, new Color32(0x18, 0x18, 0x18, 0xFF)); // #181818
                s_PreviewBgTex.Apply();
                s_PreviewBgTex.hideFlags = HideFlags.HideAndDontSave;
            }

            if (s_PreviewBoxStyle == null)
            {
                s_PreviewBoxStyle = new GUIStyle("box");
                s_PreviewBoxStyle.normal.background = s_PreviewBgTex;
                s_PreviewBoxStyle.margin = new RectOffset(0, 0, 0, 0);
                s_PreviewBoxStyle.padding = new RectOffset(8, 8, 8, 8);
            }

            if (s_PreviewLabelStyle == null)
            {
                s_PreviewLabelStyle = new GUIStyle(EditorStyles.label);
                s_PreviewLabelStyle.richText = true;      // enable <color> tags
                s_PreviewLabelStyle.wordWrap = false;
                s_PreviewLabelStyle.font = EditorStyles.textArea.font;
                s_PreviewLabelStyle.fontSize = EditorStyles.textArea.fontSize;
                s_PreviewLabelStyle.normal.textColor = new Color32(0xDD, 0xDD, 0xDD, 0xFF); // default text
            }
        }

        private void DrawInlinePatchEditor(string pluginRoot)
        {
            const float splitterWidth = 6f;
            const float minPaneWidth = 240f;

            // These are the two GUILayout.Space(10) calls around the splitter
            const float splitterSideGap = 10f;
            const float splitterGapsTotal = splitterSideGap * 2f;

            float pluginListWidth = m_pluginListExpanded ? PluginListExpandedWidth : PluginListCollapsedWidth;

            // Keep these IN SYNC with your DrawCodeArea chrome settings:
            const float panelOuterGapBase = 8f;
            const float panelOuterGapMult = 4f;   // 4x outside padding
            const float panelBorder = 1f;
            const float panelPadX = 10f;

            float panelOuterGapX = panelOuterGapBase * panelOuterGapMult;

            // Everything that reduces usable width inside the right panel:
            float panelChromeX = (panelOuterGapX * 2f) + (panelBorder * 2f) + (panelPadX * 2f);

            // Small fudge helps avoid 1px rounding overflow
            const float fudge = 1f;

            // totalWidth is the space available for (left pane + splitter + right pane), excluding the splitter side gaps.
            float totalWidth = Mathf.Max(
                0f,
                EditorGUIUtility.currentViewWidth
                - pluginListWidth
                - panelChromeX
                - 16f
                - splitterGapsTotal
                - fudge
            );

            // Extra safety: avoid fractional width causing a 1px overflow -> horizontal scrollbar
            totalWidth = Mathf.Floor(totalWidth);


            float maxEditorHeight = Mathf.Clamp(m_patchEditorMaxHeight, PATCH_EDITOR_MIN_HEIGHT, GetPatchEditorHeightLimit());
            m_patchEditorMaxHeight = maxEditorHeight;

            float minSplit = totalWidth > 0f ? minPaneWidth / totalWidth : 0.5f;
            float maxSplit = totalWidth > 0f ? (totalWidth - minPaneWidth - splitterWidth) / totalWidth : 0.5f;
            if (maxSplit < minSplit)
            {
                minSplit = 0.5f;
                maxSplit = 0.5f;
            }

            m_patchPreviewSplit = Mathf.Clamp(m_patchPreviewSplit, minSplit, maxSplit);

            float leftWidth;
            float rightWidth;

            if (totalWidth <= (minPaneWidth * 2f + splitterWidth))
            {
                leftWidth = Mathf.Max(0f, (totalWidth - splitterWidth) * 0.5f);
                rightWidth = leftWidth;
            }
            else
            {
                leftWidth = Mathf.Clamp(
                    m_patchPreviewSplit * totalWidth,
                    minPaneWidth,
                    Mathf.Max(minPaneWidth, totalWidth - minPaneWidth - splitterWidth));

                rightWidth = Mathf.Max(minPaneWidth, totalWidth - leftWidth - splitterWidth);
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUILayout.VerticalScope(GUILayout.Width(leftWidth)))
                {
                    m_patchCode = DrawCodeEditor(
                        TT("Code", "Snippet to insert between patch markers"),
                        m_patchCode,
                        ref m_patchHighlightedCode,
                        ref m_patchCodeScroll,
                        "PatchCodeArea",
                        maxEditorHeight,
                        out _);
                }

                GUILayout.Space(splitterSideGap);

                Rect splitterRectFull = GUILayoutUtility.GetRect(
                    splitterWidth,
                    splitterWidth,
                    GUILayout.Height(maxEditorHeight + 35f));

                const float topOffset = 20f;
                Rect splitterRect = new Rect(
                    splitterRectFull.x,
                    splitterRectFull.y + topOffset,
                    splitterRectFull.width,
                    Mathf.Max(0f, splitterRectFull.height - topOffset)
                );

                DrawPatchPreviewSplitter(splitterRect, totalWidth, minPaneWidth, splitterWidth);

                GUILayout.Space(splitterSideGap);

                using (new EditorGUILayout.VerticalScope(GUILayout.Width(rightWidth)))
                {
                    DrawPreviewBlock(pluginRoot, maxEditorHeight, rightWidth);
                }
            }

            GUILayout.Space(7);

            // IMPORTANT: do NOT force a min width here
            Rect heightSplitterRect = GUILayoutUtility.GetRect(1f, 6f, GUILayout.ExpandWidth(true));
            DrawPatchHeightSplitter(heightSplitterRect);
        }


        private void DrawPatchPreviewSplitter(Rect splitterRect, float totalWidth, float minPaneWidth, float splitterWidth)
        {
            EditorGUIUtility.AddCursorRect(splitterRect, MouseCursor.ResizeHorizontal);

            int controlId = GUIUtility.GetControlID("PatchPreviewSplitter".GetHashCode(), FocusType.Passive);
            Event current = Event.current;
            bool isHovering = splitterRect.Contains(current.mousePosition);
            bool isActive = m_patchPreviewSplitDragging && GUIUtility.hotControl == controlId;
            DrawPatchSplitterVisual(splitterRect, true, isActive, isHovering);

            switch (current.type)
            {
                case EventType.MouseDown:
                    if (splitterRect.Contains(current.mousePosition))
                    {
                        GUIUtility.hotControl = controlId;
                        m_patchPreviewSplitDragging = true;
                        m_patchPreviewSplitStartX = current.mousePosition.x;
                        m_patchPreviewSplitStartValue = m_patchPreviewSplit;
                        current.Use();
                    }
                    break;
                case EventType.MouseDrag:
                    if (m_patchPreviewSplitDragging && GUIUtility.hotControl == controlId)
                    {
                        float delta = current.mousePosition.x - m_patchPreviewSplitStartX;
                        float newLeftWidth = m_patchPreviewSplitStartValue * totalWidth + delta;
                        float maxLeftWidth = Mathf.Max(minPaneWidth, totalWidth - minPaneWidth - splitterWidth);
                        newLeftWidth = Mathf.Clamp(newLeftWidth, minPaneWidth, maxLeftWidth);
                        m_patchPreviewSplit = totalWidth > 0f ? newLeftWidth / totalWidth : m_patchPreviewSplit;
                        current.Use();
                        Repaint();
                    }
                    break;
                case EventType.MouseUp:
                    if (m_patchPreviewSplitDragging && GUIUtility.hotControl == controlId)
                    {
                        m_patchPreviewSplitDragging = false;
                        GUIUtility.hotControl = 0;
                        current.Use();
                    }
                    break;
            }
        }

        private float GetPatchEditorHeightLimit()
        {
            return Mathf.Max(PATCH_EDITOR_MIN_HEIGHT, position.height - 240f);
        }

        private void DrawPatchHeightSplitter(Rect splitterRect)
        {
            EditorGUIUtility.AddCursorRect(splitterRect, MouseCursor.ResizeVertical);

            int controlId = GUIUtility.GetControlID("PatchHeightSplitter".GetHashCode(), FocusType.Passive);
            Event current = Event.current;
            bool isHovering = splitterRect.Contains(current.mousePosition);
            bool isActive = m_patchEditorHeightDragging && GUIUtility.hotControl == controlId;
            DrawPatchSplitterVisual(splitterRect, false, isActive, isHovering);

            switch (current.type)
            {
                case EventType.MouseDown:
                    if (splitterRect.Contains(current.mousePosition))
                    {
                        GUIUtility.hotControl = controlId;
                        m_patchEditorHeightDragging = true;
                        m_patchEditorHeightStartY = current.mousePosition.y;
                        m_patchEditorHeightStartValue = m_patchEditorMaxHeight;
                        current.Use();
                    }
                    break;
                case EventType.MouseDrag:
                    if (m_patchEditorHeightDragging && GUIUtility.hotControl == controlId)
                    {
                        float delta = current.mousePosition.y - m_patchEditorHeightStartY;
                        float limit = GetPatchEditorHeightLimit();
                        m_patchEditorMaxHeight = Mathf.Clamp(m_patchEditorHeightStartValue + delta, PATCH_EDITOR_MIN_HEIGHT, limit);
                        current.Use();
                        Repaint();
                    }
                    break;
                case EventType.MouseUp:
                    if (m_patchEditorHeightDragging && GUIUtility.hotControl == controlId)
                    {
                        m_patchEditorHeightDragging = false;
                        GUIUtility.hotControl = 0;
                        current.Use();
                    }
                    break;
            }
        }

        private void DrawPatchSplitterVisual(Rect splitterRect, bool isVertical, bool isActive, bool isHovering)
        {
            Color idleFill = new Color32(0x2B, 0x4B, 0x73, 0x2B);
            Color hoverFill = new Color32(0x3C, 0x7C, 0xC6, 0x55);
            Color activeFill = new Color32(0x4A, 0x92, 0xE0, 0x88);
            Color lineColor = new Color32(0x64, 0xA8, 0xF0, 0xD6);

            Color fill = isActive ? activeFill : (isHovering ? hoverFill : idleFill);
            EditorGUI.DrawRect(splitterRect, fill);

            if (isVertical)
            {
                float lineX = splitterRect.xMin + (splitterRect.width * 0.5f) - 0.5f;
                float lineHeight = Mathf.Max(1f, splitterRect.height);
                EditorGUI.DrawRect(new Rect(lineX, splitterRect.yMin, 1f, lineHeight), lineColor);

                DrawPatchSplitterGrip(splitterRect, lineColor, true);
            }
            else
            {
                float lineY = splitterRect.yMin + (splitterRect.height * 0.5f) - 0.5f;
                float lineWidth = Mathf.Max(1f, splitterRect.width);
                EditorGUI.DrawRect(new Rect(splitterRect.xMin, lineY, lineWidth, 1f), lineColor);
                DrawPatchSplitterGrip(splitterRect, lineColor, false);
            }
        }

        private void DrawPatchSplitterGrip(Rect splitterRect, Color lineColor, bool isVertical)
        {
            const int gripCount = 3;
            const float gripThickness = 2f;
            const float gripLength = 10f;
            const float gripGap = 3f;

            if (isVertical)
            {
                float centerX = splitterRect.x + splitterRect.width * 0.5f;
                float startY = splitterRect.y + splitterRect.height * 0.5f - ((gripCount * gripThickness + (gripCount - 1) * gripGap) * 0.5f);
                for (int i = 0; i < gripCount; i++)
                {
                    float y = startY + i * (gripThickness + gripGap);
                    Rect gripRect = new Rect(centerX - gripLength * 0.5f, y, gripLength, gripThickness);
                    EditorGUI.DrawRect(gripRect, lineColor);
                }
            }
            else
            {
                float centerY = splitterRect.y + splitterRect.height * 0.5f;
                float startX = splitterRect.x + splitterRect.width * 0.5f - ((gripCount * gripThickness + (gripCount - 1) * gripGap) * 0.5f);
                for (int i = 0; i < gripCount; i++)
                {
                    float x = startX + i * (gripThickness + gripGap);
                    Rect gripRect = new Rect(x, centerY - gripLength * 0.5f, gripThickness, gripLength);
                    EditorGUI.DrawRect(gripRect, lineColor);
                }
            }
        }

        private float DrawPreviewBlock(string pluginRoot, float maxHeight, float availableWidth)
        {
            // ─────────────────────────────────────────────────────────────────────
            // Code Preview (enclosing block, read-only, tinted, respects existing edits)
            // ─────────────────────────────────────────────────────────────────────
            EditorGUILayout.LabelField(
                TT("Preview (enclosing block)", "Read-only preview around the insertion."),
                EditorStyles.boldLabel);

            string previewErr;
            ManifestEditState previewState;
            PreviewRenderData? previewData = BuildAndColorizePreview(pluginRoot, out previewErr, out previewState);
            float previewHeight = EditorGUIUtility.singleLineHeight * 2f;

            // ─────────────────────────────────────────────────────────
            // NEW: reset + cache status for the full-width HelpBox
            // ─────────────────────────────────────────────────────────
            m_inlinePreviewStatusValid = false;
            m_inlinePreviewStatus = ManifestEditState.Unknown;

            if (string.IsNullOrEmpty(previewErr) && previewData.HasValue)
            {
                m_inlinePreviewStatusValid = true;
                m_inlinePreviewStatus = previewState;
            }

            if (!string.IsNullOrEmpty(previewErr))
            {
                EditorGUILayout.HelpBox(previewErr, MessageType.Info);
            }
            if (previewData.HasValue)
            {
                EnsureStyles();
                EnsurePreviewStyles();

                float outerHeight = maxHeight;
                // Make the inner scroll view fill the box, not expand it
                float innerHeight = Mathf.Max(
                    EditorGUIUtility.singleLineHeight * 2f,
                    outerHeight - s_PreviewBoxStyle.padding.vertical
                );

                // Force the preview box to be exactly the same height as the code panel
                GUILayout.BeginVertical(s_PreviewBoxStyle, GUILayout.Height(outerHeight));

                var data = previewData.Value;
                string highlightedPreview = data.HighlightedText ?? string.Empty;
                string[] previewLines = data.HighlightedLines ?? Array.Empty<string>();
                int startLineNumber = Mathf.Max(1, data.StartDisplayLine);
                int maxLineNumber = Mathf.Max(startLineNumber, startLineNumber + previewLines.Length - 1);

                float lineNumberWidth = CalculateLineNumberWidth(maxLineNumber);
                float textWidth = Mathf.Max(10f, availableWidth - lineNumberWidth - s_PreviewBoxStyle.padding.horizontal - 12f);

                // Keep your existing scroll-reset logic
                using (var scroll = new EditorGUILayout.ScrollViewScope(m_previewScroll, false, false, GUILayout.Height(innerHeight)))
                {
                    m_previewScroll = scroll.scrollPosition;

                    bool previewChanged =
                        !string.Equals(highlightedPreview, m_lastPreviewSnapshot, StringComparison.Ordinal) ||
                        data.ClampedLine != m_lastPreviewLine ||
                        !string.Equals(m_patchTargetAssetPath, m_lastPreviewTargetPath, StringComparison.OrdinalIgnoreCase);

                    if (previewChanged)
                    {
                        m_previewScroll = Vector2.zero;
                        m_lastPreviewSnapshot = highlightedPreview;
                        m_lastPreviewLine = data.ClampedLine;
                        m_lastPreviewTargetPath = m_patchTargetAssetPath ?? string.Empty;
                    }

                    Rect content = GUILayoutUtility.GetRect(new GUIContent(highlightedPreview), m_highlightStyle, GUILayout.ExpandWidth(true));

                    float contentTextWidth = Mathf.Max(10f, content.width - lineNumberWidth);
                    float neededHeight = m_highlightStyle.CalcHeight(new GUIContent(highlightedPreview), contentTextWidth);
                    if (neededHeight > content.height)
                        content.height = neededHeight;

                    Rect gutterRect = new Rect(content.x, content.y, lineNumberWidth, content.height);
                    Rect codeRect = new Rect(content.x + lineNumberWidth, content.y, Mathf.Max(10f, content.width - lineNumberWidth), content.height);

                    DrawLineNumbers(gutterRect, codeRect, previewLines, previewLines, startLineNumber);

                    if (Event.current.type == EventType.Repaint)
                        UnityEngine.GUI.Label(codeRect, highlightedPreview, m_highlightStyle);
                }

                GUILayout.EndVertical();

                // return a height that matches the code side
                return outerHeight;


            }

            return previewHeight;
        }

        // Safe-ish for rich text (avoid tag parsing on generic <T>)
        private static string RichEscape(string s)
        {
            if (string.IsNullOrEmpty(s)) return s;
            return s.Replace("<", "\u200B<").Replace(">", ">\u200B");
        }

        // Find // comment start (ignores leading whitespace). Returns -1 if none.
        private static int IndexOfLineComment(string line)
        {
            int i = 0;
            while (i < line.Length && (line[i] == ' ' || line[i] == '\t')) i++;
            if (i + 1 < line.Length && line[i] == '/' && line[i + 1] == '/') return i;
            return -1;
        }

        private static string NormalizeNewlines(string value)
        {
            return string.IsNullOrEmpty(value)
                ? string.Empty
                : value.Replace("\r\n", "\n").Replace("\r", "\n");
        }

        private static string PluginRootOf(string path)
        {
            if (string.IsNullOrEmpty(path)) return null;
            return Directory.Exists(path) ? path                    // path *is* a folder
                                          : Directory.GetParent(path)?.FullName; // path is a file
        }

        private static string GetPluginRoot(string path)
        {
            if (string.IsNullOrEmpty(path)) return null;

            string absPlugins = Path.GetFullPath(PluginsPath);
            string full = Directory.Exists(path)
                                ? Path.GetFullPath(path)
                                : Path.GetFullPath(Path.GetDirectoryName(path));

            if (!full.StartsWith(absPlugins)) return null;

            string rel = full.Substring(absPlugins.Length).Trim(Path.DirectorySeparatorChar);
            string root = rel.Split(Path.DirectorySeparatorChar).FirstOrDefault();
            return string.IsNullOrEmpty(root) ? null : Path.Combine(PluginsPath, root);
        }

        /// <summary>
        /// Preview the effect of inserting the patch code into the target file
        /// </summary>
        private string BuildPreviewSnippet(
            string pluginRoot,
            out string error,
            out int firstLineIndex0,
            out int startMarkerLine,
            out int codeStartLine,
            out int codeLineCount,
            out int endMarkerLine)
        {
            error = "";
            firstLineIndex0 = 0;
            startMarkerLine = codeStartLine = codeLineCount = endMarkerLine = -1;

            if (string.IsNullOrEmpty(m_patchTargetAssetPath))
            {
                error = "Select a Target Core File to preview.";
                return null;
            }

            string abs = ToAbsolute(m_patchTargetAssetPath);
            if (!File.Exists(abs))
            {
                error = $"File not found: {m_patchTargetAssetPath}";
                return null;
            }

            string text = File.ReadAllText(abs);
            int targetLine = ClampToExistingLine(text, GetEffectiveLine());
            GetLineStartEnd(text, targetLine, out int lineStart, out int lineEnd);

            bool isClosingBrace = IsClosingBraceLine(text, lineStart, lineEnd);
            string nl = DetectNewline(text);

            // Decide insertion index like the patcher does
            int insertIdx;
            string leading = "";

            if (isClosingBrace)
            {
                insertIdx = lineStart; // before the '}'
            }
            else
            {
                insertIdx = lineEnd;
                bool hasTerminator = (insertIdx < text.Length) && (text[insertIdx] == '\r' || text[insertIdx] == '\n');
                if (hasTerminator)
                {
                    if (insertIdx < text.Length && text[insertIdx] == '\r') insertIdx++;
                    if (insertIdx < text.Length && text[insertIdx] == '\n') insertIdx++;
                }
                else
                {
                    leading = nl; // ensure exactly one newline before marker if no terminator
                }
            }

            // Indentation
            int baseIndentSpaces = CountIndentSpaces(text, lineStart);
            int desiredIndentSpaces = Mathf.Max(baseIndentSpaces, Mathf.Max(0, m_patchRow));
            string indent = new string(' ', desiredIndentSpaces);

            // Markers
            string pluginName = Path.GetFileName(pluginRoot.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
            string id = StableId(m_patchTargetAssetPath, targetLine, m_patchRow, m_patchCode, m_replaceLine, m_patchSearchCode, m_useSearch, m_patchSearchLineRange);
            string startMarker = $"// >>> PLUGIN_PATCH:{pluginName}::{id}";
            string endMarker = $"// <<< PLUGIN_PATCH:{pluginName}::{id}";

            // Build payload
            string codeIndented = IndentEachLine(m_patchCode, indent);
            string payload =
                  leading
                + indent + startMarker + nl
                + codeIndented + nl
                + indent + endMarker + nl;

            // Insert into a synthetic buffer
            string withInsert = text.Insert(insertIdx, payload);

            // Compute line indices of the special lines *inside withInsert*
            int payloadStart = insertIdx; // leading (maybe empty) starts here
            int afterLeading = payloadStart + leading.Length;

            int startMarkerIdx = afterLeading;                       // start of indent + startMarker
            int codeStartIdx = startMarkerIdx + indent.Length + startMarker.Length + nl.Length;
            int endMarkerIdx = codeStartIdx + codeIndented.Length + nl.Length; // indent + endMarker begins here

            int startMarkerLineAbs = LineIndexAt(withInsert, startMarkerIdx);
            int codeStartLineAbs = LineIndexAt(withInsert, codeStartIdx);
            int endMarkerLineAbs = LineIndexAt(withInsert, endMarkerIdx);
            int codeLines = string.IsNullOrEmpty(m_patchCode)
                                       ? 0
                                       : m_patchCode.Replace("\r\n", "\n").Replace("\r", "\n").Split('\n').Length;

            // Extract enclosing block to show
            int blockStart, blockEnd;
            if (!FindEnclosingBlock(withInsert, codeStartIdx, out blockStart, out blockEnd))
            {
                // Fallback to ±5 lines
                int around = 5;
                int targetAbs = LineIndexAt(withInsert, codeStartIdx);
                int from = Mathf.Max(0, targetAbs - around);
                int to = targetAbs + around;
                GetLineStartEndByLineSpan(withInsert, from, to, out blockStart, out blockEnd);
            }

            blockStart = Mathf.Clamp(blockStart, 0, withInsert.Length);
            blockEnd = Mathf.Clamp(blockEnd, blockStart, withInsert.Length);

            if (blockEnd <= blockStart)
            {
                int targetAbs = LineIndexAt(withInsert, codeStartIdx);
                int maxLine = ClampToExistingLine(withInsert, int.MaxValue);
                int from = Mathf.Max(0, targetAbs - 3);
                int to = Mathf.Min(maxLine, targetAbs + 3);
                GetLineStartEndByLineSpan(withInsert, from, to, out blockStart, out blockEnd);

                blockStart = Mathf.Clamp(blockStart, 0, withInsert.Length);
                blockEnd = Mathf.Clamp(blockEnd, blockStart, withInsert.Length);
            }

            firstLineIndex0 = LineIndexAt(withInsert, blockStart);

            // Remap absolute line indices to snippet-local line indices
            startMarkerLine = startMarkerLineAbs - firstLineIndex0;
            codeStartLine = codeStartLineAbs - firstLineIndex0;
            endMarkerLine = endMarkerLineAbs - firstLineIndex0;
            codeLineCount = codeLines;

            string snippet = withInsert.Substring(blockStart, Mathf.Max(0, blockEnd - blockStart));
            return snippet;
        }

        // Build final rich-text with line numbers + colors
        private string ColorizePreview(string snippet,
                                       int firstLineIndex0,
                                       int startMarkerLine,
                                       int codeStartLine,
                                       int codeLineCount,
                                       int endMarkerLine)
        {
            string[] lines = snippet.Replace("\r\n", "\n").Replace("\r", "\n").Split('\n');
            int total = lines.Length;

            var sb = new StringBuilder(snippet.Length + total * 32);
            int displayLine = firstLineIndex0 + 1;

            for (int i = 0; i < total; i++, displayLine++)
            {
                string raw = lines[i];

                bool isCodeLine = (i >= codeStartLine && i < codeStartLine + codeLineCount);

                // Split comment part (// …) if present at the start of code text (after leading spaces)
                int commentIdx = IndexOfLineComment(raw);

                // Escape content for rich-text
                string escaped = RichEscape(raw);

                string colored;
                if (commentIdx >= 0)
                {
                    // before //  and from // onward
                    string before = escaped.Substring(0, commentIdx);
                    string comment = escaped.Substring(commentIdx);

                    if (isCodeLine)
                        before = $"<color=#d5d184>{before}</color>"; // added code color

                    comment = $"<color=#608b4e>{comment}</color>";   // comment color

                    colored = before + comment;
                }
                else
                {
                    colored = isCodeLine ? $"<color=#d5d184>{escaped}</color>" : escaped;
                }

                // prepend line numbers (1-based view)
                sb.Append((displayLine).ToString().PadLeft(5)).Append(" | ").Append(colored);
                if (i < total - 1) sb.Append('\n');
            }

            return sb.ToString();
        }

        private string GetPreviewForCurrent(string pluginRoot, out string error)
        {
            error = "";
            if (string.IsNullOrEmpty(m_patchTargetAssetPath))
            {
                error = "Select a Target Core File to preview.";
                return null;
            }

            // Load file
            string abs = ToAbsolute(m_patchTargetAssetPath);
            if (!File.Exists(abs))
            {
                error = $"File not found: {m_patchTargetAssetPath}";
                return null;
            }

            string text = File.ReadAllText(abs);
            string pluginName = Path.GetFileName(pluginRoot.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));

            // Simulate insertion using the same rules as ApplyAll's insert_at:
            // - If target line is a closing brace line, insert BEFORE it (inside the block)
            // - else insert at the beginning of the NEXT line
            // - exactly one newline before code; newline after end marker
            // - indent = max(line leading spaces, column)
            int targetLine = ResolveTargetLine(text, out _, out _);

            GetLineStartEnd(text, targetLine, out int lineStart, out int lineEnd);
            bool isClosingBrace = IsClosingBraceLine(text, lineStart, lineEnd);

            int insertIdx;
            string nl = DetectNewline(text);
            string leading = "";

            if (isClosingBrace)
            {
                insertIdx = lineStart; // before the '}'
            }
            else
            {
                insertIdx = lineEnd;
                bool hasTerminator = (insertIdx < text.Length) && (text[insertIdx] == '\r' || text[insertIdx] == '\n');
                if (hasTerminator)
                {
                    if (insertIdx < text.Length && text[insertIdx] == '\r') insertIdx++;
                    if (insertIdx < text.Length && text[insertIdx] == '\n') insertIdx++;
                }
                else
                {
                    leading = nl; // ensure exactly one newline before the marker
                }
            }

            int baseIndentSpaces = CountIndentSpaces(text, lineStart);
            int desiredIndentSpaces = Mathf.Max(baseIndentSpaces, Mathf.Max(0, m_patchRow));

            string indent = new string(' ', desiredIndentSpaces);

            // Build payload (with the same marker format used by the patcher)
            string id = StableId(m_patchTargetAssetPath, targetLine, m_patchRow, m_patchCode, m_replaceLine, m_patchSearchCode, m_useSearch, m_patchSearchLineRange);
            string startMarker = $"// >>> PLUGIN_PATCH:{pluginName}::{id}";
            string endMarker = $"// <<< PLUGIN_PATCH:{pluginName}::{id}";

            string codeIndented = IndentEachLine(m_patchCode, indent);
            string payload =
                  leading
                + indent + startMarker + nl
                + codeIndented + nl
                + indent + endMarker + nl;

            // Create a synthetic text that includes the insertion
            string withInsert = text.Insert(insertIdx, payload);

            // Extract enclosing block around the insertion (nice, focused preview)
            int blockStart, blockEnd;
            if (!FindEnclosingBlock(withInsert, insertIdx + leading.Length, out blockStart, out blockEnd))
            {
                // Fallback: show 10 lines around target line
                int fromLine = Mathf.Max(0, targetLine - 5);
                int toLine = targetLine + 5;
                GetLineStartEndByLineSpan(withInsert, fromLine, toLine, out blockStart, out blockEnd);
            }

            // Format with line numbers for clarity
            string snippet = withInsert.Substring(blockStart, Mathf.Max(0, blockEnd - blockStart));
            int firstLine = LineIndexAt(withInsert, blockStart);
            return AddLineNumbers(snippet, firstLine);
        }

        private PreviewRenderData? BuildAndColorizePreview(string pluginRoot, out string error, out ManifestEditState state)
        {
            error = "";
            state = ManifestEditState.Unknown;
            if (string.IsNullOrEmpty(m_patchTargetAssetPath))
            {
                error = "Select a Target Core File to preview.";
                return null;
            }

            string abs = ToAbsolute(m_patchTargetAssetPath);
            if (!File.Exists(abs))
            {
                error = $"File not found: {m_patchTargetAssetPath}";
                state = ManifestEditState.FileMissing;
                return null;
            }

            string fileText = File.ReadAllText(abs);
            string nl = DetectNewline(fileText);

            // Compute plugin/id for *this* staged edit
            string pluginName = GetPluginName(pluginRoot);

            string markerId = null;
            int clampedLine;
            bool usedSearch;
            bool foundSearch;
            try
            {
                clampedLine = ResolveTargetLine(fileText, out usedSearch, out foundSearch);
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return null;
            }
            if (m_selTargetIdx >= 0 && m_selTargetIdx < m_manifest.targets.Count)
            {
                var selT = m_manifest.targets[m_selTargetIdx];
                if (selT != null && selT.insert_at != null && m_selOpIdx >= 0 && m_selOpIdx < selT.insert_at.Count)
                {
                    if (string.Equals(selT.path, m_patchTargetAssetPath, StringComparison.OrdinalIgnoreCase))
                        markerId = selT.insert_at[m_selOpIdx]?.id;
                }
            }

            if (string.IsNullOrEmpty(markerId))
                markerId = StableId(m_patchTargetAssetPath, clampedLine, m_patchRow, m_patchCode, m_replaceLine, m_patchSearchCode, m_useSearch, m_patchSearchLineRange);

            string startMarker = $"// >>> PLUGIN_PATCH:{pluginName}::{markerId}";
            string endMarker = $"// <<< PLUGIN_PATCH:{pluginName}::{markerId}";

            // If this exact edit is already applied, preview the file as-is.
            bool hasThis = false;
            if (!string.IsNullOrEmpty(pluginName) && !string.IsNullOrEmpty(markerId))
            {
                hasThis = fileText.IndexOf(startMarker, StringComparison.Ordinal) >= 0 &&
                          fileText.IndexOf(endMarker, StringComparison.Ordinal) >= 0;
                state = hasThis ? ManifestEditState.Applied : ManifestEditState.NotApplied;
            }
            else
            {
                state = ManifestEditState.Unknown;
            }

            // Otherwise, simulate insertion using the current rules (after target line, or before '}' line),
            // with exactly one newline before code and one after the end marker.
            string withInsert = fileText;
            int insertNearIdx = 0;
            if (!hasThis)
            {
                int targetLine = clampedLine;
                GetLineStartEnd(fileText, targetLine, out int lineStart, out int lineEnd);
                int baseIndentSpaces = CountIndentSpaces(fileText, lineStart);
                bool isClosingBrace = IsClosingBraceLine(fileText, lineStart, lineEnd);

                int insertIdx;
                string leading = "";

                if (m_replaceLine)
                {
                    int lineEndWithNl = FindLineEnd(fileText, lineEnd);
                    fileText = fileText.Remove(lineStart, lineEndWithNl - lineStart);
                    insertIdx = lineStart;
                }
                else if (isClosingBrace)
                {
                    insertIdx = lineStart; // before the '}'
                }
                else
                {
                    insertIdx = lineEnd;
                    bool hasTerminator = (insertIdx < fileText.Length) &&
                                         (fileText[insertIdx] == '\r' || fileText[insertIdx] == '\n');
                    if (hasTerminator)
                    {
                        if (insertIdx < fileText.Length && fileText[insertIdx] == '\r') insertIdx++;
                        if (insertIdx < fileText.Length && fileText[insertIdx] == '\n') insertIdx++;
                    }
                    else
                    {
                        leading = nl; // ensure exactly one newline before marker if no terminator
                    }
                }

                int desiredIndentSpaces = Mathf.Max(baseIndentSpaces, Mathf.Max(0, m_patchRow));
                string indent = new string(' ', desiredIndentSpaces);

                string codeIndented = IndentEachLine(m_patchCode, indent);
                string payload =
                      leading
                    + indent + startMarker + nl
                    + codeIndented + nl
                    + indent + endMarker + nl;

                withInsert = fileText.Insert(insertIdx, payload);
                insertNearIdx = insertIdx + leading.Length + indent.Length; // near start marker
            }
            else
            {
                // Find the existing block to center the preview around
                int startIdx = fileText.IndexOf(startMarker, StringComparison.Ordinal);
                insertNearIdx = Math.Max(0, startIdx);
            }

            // Determine which lines in the WHOLE buffer are "patch code" lines (for *all* existing patches)
            var allPatchCodeLinesAbs = ComputePatchCodeLineSet(withInsert);

            // Extract a focused snippet: enclosing block around our insertion (or existing block).
            if (!FindEnclosingBlock(withInsert, insertNearIdx, out int blockStart, out int blockEnd))
            {
                // Fallback ±5 lines
                int nearLine = LineIndexAt(withInsert, insertNearIdx);
                int from = Mathf.Max(0, nearLine - 5);
                int to = nearLine + 5;
                GetLineStartEndByLineSpan(withInsert, from, to, out blockStart, out blockEnd);
            }

            // Guard against zero-length extractions when the requested window sits past the
            // available lines (e.g., selecting line numbers beyond the file end).
            blockStart = Mathf.Clamp(blockStart, 0, withInsert.Length);
            blockEnd = Mathf.Clamp(blockEnd, blockStart, withInsert.Length);

            if (blockEnd <= blockStart)
            {
                int nearLine = LineIndexAt(withInsert, insertNearIdx);
                int maxLine = ClampToExistingLine(withInsert, int.MaxValue);
                int fallbackFrom = Mathf.Max(0, nearLine - 3);
                int fallbackTo = Mathf.Min(maxLine, nearLine + 3);
                GetLineStartEndByLineSpan(withInsert, fallbackFrom, fallbackTo, out blockStart, out blockEnd);

                blockStart = Mathf.Clamp(blockStart, 0, withInsert.Length);
                blockEnd = Mathf.Clamp(blockEnd, blockStart, withInsert.Length);
            }

            // Build snippet + color it
            int firstLineIndex0 = LineIndexAt(withInsert, blockStart);
            string snippet = withInsert.Substring(blockStart, Mathf.Max(0, blockEnd - blockStart));
            return ColorizePreview(snippet, firstLineIndex0, allPatchCodeLinesAbs, clampedLine);
        }

        // Returns the set of absolute line indices (0-based) that are inside ANY patch region body:
        // between "// >>> PLUGIN_PATCH:...::<id>" and "// <<< PLUGIN_PATCH:...::<id>"
        private static HashSet<int> ComputePatchCodeLineSet(string text)
        {
            const string START_HEAD = "// >>> PLUGIN_PATCH:";
            const string END_HEAD = "// <<< PLUGIN_PATCH:";

            var set = new HashSet<int>();
            int search = 0;

            while (true)
            {
                int a = text.IndexOf(START_HEAD, search, StringComparison.Ordinal);
                if (a < 0) break;
                int startLine = LineIndexAt(text, a);

                int startLineEnd = FindLineEnd(text, a);
                string idPart = text.Substring(a + START_HEAD.Length, Math.Max(0, startLineEnd - (a + START_HEAD.Length))).Trim();
                string endMarker = END_HEAD + idPart;

                int b = text.IndexOf(endMarker, startLineEnd, StringComparison.Ordinal);
                if (b < 0)
                {
                    search = startLineEnd;
                    continue; // malformed pair; skip
                }
                int endLine = LineIndexAt(text, b);

                // Body lines are (startLine+1) .. (endLine-1)
                for (int L = startLine + 1; L <= endLine - 1; L++) set.Add(L);

                search = FindLineEnd(text, b + endMarker.Length);
            }

            return set;
        }

        // Build final rich-text with line numbers + colors.
        //  - 'patchCodeLinesAbs' are absolute line numbers in the full buffer that belong to ANY patch region body.
        //  - Mapped snippet-local lines and colored width #d5d184.
        //  - Any // comments are colored #608b4e.
        private PreviewRenderData ColorizePreview(string snippet, int firstLineIndex0, HashSet<int> patchCodeLinesAbs, int clampedLine)
        {
            string[] lines = snippet.Replace("\r\n", "\n").Replace("\r", "\n").Split('\n');
            int total = lines.Length;

            var sb = new StringBuilder(snippet.Length + total * 16);
            int startDisplayLine = firstLineIndex0 + 1;
            string[] coloredLines = new string[total];

            for (int i = 0; i < total; i++)
            {
                string raw = lines[i];
                int absLine = firstLineIndex0 + i;

                bool isPatchCode = patchCodeLinesAbs != null && patchCodeLinesAbs.Contains(absLine);

                int commentIdx = IndexOfLineComment(raw);
                string escaped = RichEscape(raw);

                string colored;
                if (commentIdx >= 0)
                {
                    string before = escaped.Substring(0, commentIdx);
                    string comment = escaped.Substring(commentIdx);

                    if (isPatchCode) before = $"<color=#d5d184>{before}</color>";
                    comment = $"<color=#608b4e>{comment}</color>";

                    colored = before + comment;
                }
                else
                {
                    colored = isPatchCode ? $"<color=#d5d184>{escaped}</color>" : escaped;
                }

                coloredLines[i] = colored;
                sb.Append(colored);
                if (i < total - 1) sb.Append('\n');
            }

            return new PreviewRenderData
            {
                HighlightedText = sb.ToString(),
                HighlightedLines = coloredLines,
                StartDisplayLine = startDisplayLine,
                ClampedLine = clampedLine
            };
        }

        private static void GetLineStartEnd(string text, int targetLine, out int start, out int end)
        {
            if (targetLine < 0) targetLine = 0;
            int i = 0, line = 0, n = text.Length;
            while (i < n && line < targetLine)
            {
                char c = text[i++];
                if (c == '\r') { if (i < n && text[i] == '\n') i++; line++; }
                else if (c == '\n') { line++; }
            }
            start = i;
            while (i < n && text[i] != '\r' && text[i] != '\n') i++;
            end = i;
        }

        private static int CountIndentSpaces(string text, int lineStart)
        {
            int spaces = 0;
            for (int i = lineStart; i < text.Length; i++)
            {
                char c = text[i];
                if (c == ' ') spaces++;
                else if (c == '\t') spaces += 4;
                else break;
            }
            return spaces;
        }

        private static string DetectNewline(string text)
        {
            int idx = text.IndexOf('\n');
            if (idx > 0 && text[idx - 1] == '\r') return "\r\n";
            return "\n";
        }

        private static string IndentEachLine(string code, string indent)
        {
            if (string.IsNullOrEmpty(code)) return code;
            string norm = code.Replace("\r\n", "\n").Replace("\r", "\n");
            var lines = norm.Split('\n');
            for (int i = 0; i < lines.Length; i++)
                if (lines[i].Length > 0) lines[i] = indent + lines[i];
            return string.Join("\n", lines);
        }

        private static bool IsClosingBraceLine(string text, int lineStart, int lineEnd)
        {
            for (int i = lineStart; i < lineEnd; i++)
            {
                char c = text[i];
                if (c == ' ' || c == '\t') continue;
                return c == '}';
            }
            return false;
        }

        // Finds the smallest block { ... } that encloses 'nearIdx'.
        // Returns [blockStart, blockEnd) byte indices (line-aligned).
        private static bool FindEnclosingBlock(string text, int nearIdx, out int blockStart, out int blockEnd)
        {
            blockStart = blockEnd = 0;
            if (string.IsNullOrEmpty(text)) return false;

            // Find the last '{' before nearIdx
            int open = text.LastIndexOf('{', Mathf.Clamp(nearIdx, 0, text.Length - 1));
            if (open < 0) return false;

            // From that '{', walk forward to find matching '}'
            int depth = 0;
            for (int i = open; i < text.Length; i++)
            {
                char c = text[i];
                if (c == '{') depth++;
                else if (c == '}')
                {
                    depth--;
                    if (depth == 0)
                    {
                        // Align to full lines
                        blockStart = FindLineStart(text, open);
                        blockEnd = FindLineEnd(text, i + 1);
                        return true;
                    }
                }
            }
            return false;
        }

        private static int FindLineStart(string s, int idx)
        {
            idx = Mathf.Clamp(idx, 0, s.Length);
            while (idx > 0)
            {
                char c = s[idx - 1];
                if (c == '\n' || c == '\r') break;
                idx--;
            }
            return idx;
        }

        private static int FindLineEnd(string s, int idx)
        {
            int n = s.Length;
            idx = Mathf.Clamp(idx, 0, n);
            while (idx < n && s[idx] != '\r' && s[idx] != '\n') idx++;
            if (idx < n && s[idx] == '\r') idx++;
            if (idx < n && s[idx] == '\n') idx++;
            return idx;
        }

        // Fallback span by line numbers
        private static void GetLineStartEndByLineSpan(string text, int fromLine, int toLine, out int start, out int end)
        {
            GetLineStartEnd(text, Mathf.Max(0, fromLine), out start, out _);
            GetLineStartEnd(text, Mathf.Max(0, toLine + 1), out _, out end); // end exclusive
        }

        // Compute 0-based line index at a character position
        private static int LineIndexAt(string text, int pos)
        {
            pos = Mathf.Clamp(pos, 0, text.Length);
            int lines = 0;
            for (int i = 0; i < pos; i++)
                if (text[i] == '\n') lines++;
                else if (text[i] == '\r')
                {
                    if (i + 1 < text.Length && text[i + 1] == '\n') i++;
                    lines++;
                }
            return lines;
        }

        // Adds line numbers (1-based display) to a multi-line snippet
        private static string AddLineNumbers(string snippet, int firstLineIndex0)
        {
            string norm = snippet.Replace("\r\n", "\n").Replace("\r", "\n");
            var lines = norm.Split('\n');
            int num = firstLineIndex0 + 1; // show 1-based
            var sb = new StringBuilder(snippet.Length + lines.Length * 8);
            for (int i = 0; i < lines.Length; i++, num++)
            {
                // keep trailing empty line formatting
                string line = lines[i];
                if (i < lines.Length - 1 || line.Length > 0)
                    sb.Append(num.ToString().PadLeft(5)).Append(" | ").Append(line).Append('\n');
            }
            return sb.ToString();
        }
    }
}
