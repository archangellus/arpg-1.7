using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace PLAYERTWO.ARPGProject
{
    public partial class PluginInspectorWindow
    {
        // JSON structure mirrored from the patcher
        // Manifest model (mirrors your JSON)
        [Serializable] private class PatchRecipe { public List<Target> targets = new(); }
        [Serializable] private class Target { public string path; public List<InsertAt> insert_at = new(); }
        [Serializable]
        private class InsertAt
        {
            public string id; public int line; public int column; public string insert; public bool replace_line; public string search; public bool use_search = true; public int search_line_range = DEFAULT_SEARCH_LINE_RANGE;
        }

        [Serializable]
        public class InsertAtOp
        {
            public string id;
            public int line;
            public int column;
            public string insert;
            public bool replace_line;
            public string search;
            public bool use_search = true;
            public int search_line_range = DEFAULT_SEARCH_LINE_RANGE;
        }

        private enum SaveMode { Append, Replace }

        private enum ManifestEditState
        {
            Unknown,
            FileMissing,
            Applied,
            NotApplied
        }

        [Serializable]
        private class ManifestListItem
        {
            public Target target;
            public InsertAt op;
            public string label;
            public ManifestEditState state;
        }

        private void EnsureManifestList(string pluginRoot, bool allowDrag)
        {
            if (m_manifestList == null)
            {
                m_manifestList = new ReorderableList(m_manifestItems, typeof(ManifestListItem), true, false, false, false)
                {
                    headerHeight = 0f,
                    elementHeight = EditorGUIUtility.singleLineHeight + 6f
                };
            }

            m_manifestList.list = m_manifestItems;
            m_manifestList.draggable = allowDrag;
            if (!allowDrag) m_manifestDragging = false;
            m_manifestList.onSelectCallback = list => SelectManifestItem(pluginRoot, list.index);
            m_manifestList.onReorderCallback = list => ApplyManifestReorder(pluginRoot, list.index);
            m_manifestList.onMouseDragCallback = list => m_manifestDragging = true;
            m_manifestList.onMouseUpCallback = list => m_manifestDragging = false;
            m_manifestList.drawElementCallback = (rect, index, active, focused) =>
                DrawManifestElement(pluginRoot, rect, index);
        }

        // Deterministic id + dedupe
        private static void SaveManifest(string pluginRoot, List<PendingEdit> edits, SaveMode mode)
        {
            if (edits == null) edits = new List<PendingEdit>();
            string manifestPath = Path.Combine(pluginRoot, "plugin.patches.json");

            PatchRecipe rec;
            if (mode == SaveMode.Append && File.Exists(manifestPath))
                rec = JsonUtility.FromJson<PatchRecipe>(File.ReadAllText(manifestPath)) ?? new PatchRecipe();
            else
                rec = new PatchRecipe();

            var byPath = new Dictionary<string, Target>(StringComparer.OrdinalIgnoreCase);
            foreach (var t in rec.targets ?? new List<Target>()) byPath[t.path] = t;

            foreach (var grp in edits.GroupBy(e => e.path, StringComparer.OrdinalIgnoreCase))
            {
                if (!byPath.TryGetValue(grp.Key, out var tgt))
                {
                    tgt = new Target { path = grp.Key, insert_at = new List<InsertAt>() };
                    rec.targets.Add(tgt);
                    byPath[grp.Key] = tgt;
                }

                var existing = new HashSet<string>();
                foreach (var op in tgt.insert_at ?? new List<InsertAt>())
                    existing.Add(DedupeKey(tgt.path, op.line, op.column, op.insert, op.replace_line, op.search, op.use_search, op.search_line_range));

                foreach (var e in grp)
                {
                    string key = DedupeKey(grp.Key, e.line, e.column, e.code, e.replace_line, e.search, e.use_search, e.search_line_range);
                    if (existing.Contains(key)) continue;

                    tgt.insert_at.Add(new InsertAt
                    {
                        id = StableId(grp.Key, e.line, e.column, e.code, e.replace_line, e.search, e.use_search, e.search_line_range),
                        line = e.line,
                        column = e.column,
                        insert = e.code,
                        replace_line = e.replace_line,
                        search = e.search,
                        use_search = e.use_search,
                        search_line_range = NormalizeSearchLineRange(e.search_line_range)
                    });
                    existing.Add(key);
                }
            }

            rec.targets.RemoveAll(t => t.insert_at == null || t.insert_at.Count == 0);
            File.WriteAllText(manifestPath, JsonUtility.ToJson(rec, true), Encoding.UTF8);
        }

        private static string DedupeKey(string path, int line, int column, string insert, bool replaceLine, string search, bool useSearch, int searchLineRange)
        {
            string body = NormalizeNewlines(insert ?? string.Empty);
            string anchor = (useSearch && !string.IsNullOrEmpty(search))
                ? $"search:{NormalizeNewlines(search)}|range:{NormalizeSearchLineRange(searchLineRange)}"
                : $"line:{line}|col:{column}";
            string mode = replaceLine ? "replace" : "insert";
            return $"{path.ToLowerInvariant()}|{anchor}|{mode}|{body}";
        }

        private static string StableId(string path, int line, int column, string insert, bool replaceLine, string search, bool useSearch, int searchLineRange)
        {
            string suffix = replaceLine ? "|replace" : string.Empty;
            string anchor = (useSearch && !string.IsNullOrEmpty(search))
                ? $"FIND:{NormalizeNewlines(search)}|R{NormalizeSearchLineRange(searchLineRange)}"
                : $"L{line}_C{column}";
            string payload = $"{path}|{anchor}|{insert}{suffix}";
            using (var sha1 = System.Security.Cryptography.SHA1.Create())
            {
                var hash = sha1.ComputeHash(System.Text.Encoding.UTF8.GetBytes(payload));
                string h = BitConverter.ToString(hash, 0, 4).Replace("-", "").ToLowerInvariant();
                return $"{anchor}_{h}";
            }
        }

        private static string BuildAnchorLabel(string search, bool useSearch, int line, int column, int searchLineRange)
        {
            if (useSearch && !string.IsNullOrEmpty(search))
            {
                string snippet = NormalizeNewlines(search).Split('\n').FirstOrDefault() ?? string.Empty;
                snippet = snippet.Trim();
                if (snippet.Length > 42) snippet = snippet.Substring(0, 42) + "…";
                int normalizedRange = NormalizeSearchLineRange(searchLineRange);
                return $"Find:\"{snippet}\" (L{line}, ±{normalizedRange})";
            }

            return $"L{line}, C{column}";
        }

        private static string BuildAnchorLabel(InsertAt op)
        {
            if (op == null) return string.Empty;
            return BuildAnchorLabel(op.search, op.use_search, op.line, op.column, op.search_line_range);
        }

        // Optional: clean an existing manifest of duplicates, restamp ids deterministically
        private static void NormalizeManifest(string pluginRoot)
        {
            string manifestPath = Path.Combine(pluginRoot, "plugin.patches.json");
            if (!File.Exists(manifestPath)) return;

            var rec = JsonUtility.FromJson<PatchRecipe>(File.ReadAllText(manifestPath)) ?? new PatchRecipe();
            foreach (var t in rec.targets ?? new List<Target>())
            {
                var seen = new HashSet<string>();
                var list = new List<InsertAt>();
                foreach (var op in t.insert_at ?? new List<InsertAt>())
                {
                    string key = DedupeKey(t.path, op.line, op.column, op.insert, op.replace_line, op.search, op.use_search, op.search_line_range);
                    if (!seen.Add(key)) continue;
                    op.id = StableId(t.path, op.line, op.column, op.insert, op.replace_line, op.search, op.use_search, op.search_line_range);
                    list.Add(op);
                }
                t.insert_at = list;
            }
            rec.targets.RemoveAll(t => t.insert_at == null || t.insert_at.Count == 0);
            File.WriteAllText(manifestPath, JsonUtility.ToJson(rec, true), Encoding.UTF8);
        }

        private void SelectManifestItem(string pluginRoot, int index)
        {
            if (index < 0 || index >= m_manifestItems.Count) return;

            var item = m_manifestItems[index];
            RefreshManifestSelection(item?.op);
            LoadSelectionIntoFields();
        }

        private void ApplyManifestReorder(string pluginRoot, int selectedIndex)
        {
            InsertAt selectedOp = (selectedIndex >= 0 && selectedIndex < m_manifestItems.Count)
                ? m_manifestItems[selectedIndex].op
                : null;

            var newTargets = new List<Target>();
            var byPath = new Dictionary<string, Target>(StringComparer.OrdinalIgnoreCase);

            foreach (var item in m_manifestItems)
            {
                if (item?.target == null || item.op == null) continue;

                string key = item.target.path ?? string.Empty;
                if (!byPath.TryGetValue(key, out var tgt))
                {
                    tgt = new Target { path = item.target.path, insert_at = new List<InsertAt>() };
                    byPath[key] = tgt;
                    newTargets.Add(tgt);
                }

                tgt.insert_at.Add(item.op);
            }

            m_manifest.targets = newTargets;
            RefreshManifestSelection(selectedOp);
            LoadSelectionIntoFields();
            m_manifestDragging = false;
            SaveManifestReplace(pluginRoot);
        }

        private void RefreshManifestSelection(InsertAt op)
        {
            m_selTargetIdx = -1;
            m_selOpIdx = -1;

            if (op == null || m_manifest.targets == null) return;

            for (int t = 0; t < m_manifest.targets.Count; t++)
            {
                var tgt = m_manifest.targets[t];
                if (tgt?.insert_at == null) continue;

                int found = tgt.insert_at.IndexOf(op);
                if (found >= 0)
                {
                    m_selTargetIdx = t;
                    m_selOpIdx = found;
                    return;
                }
            }
        }

        private void LoadSelectionIntoFields()
        {
            if (m_selTargetIdx < 0 || m_selOpIdx < 0) return;

            var selT = m_manifest.targets[m_selTargetIdx];
            var selO = selT.insert_at[m_selOpIdx];
            if (m_coreFiles != null)
            {
                int idx = m_coreFiles.FindIndex(p => string.Equals(p, selT.path, StringComparison.OrdinalIgnoreCase));
                if (idx >= 0)
                {
                    m_coreIndex = idx;
                    m_patchTargetAssetPath = m_coreFiles[m_coreIndex];
                }
            }

            m_patchLine = Mathf.Max(0, selO.line - CurrentLineOffset);
            m_patchRow = selO.column;
            m_patchCode = selO.insert;
            m_replaceLine = selO.replace_line;
            m_useSearch = selO.use_search;
            m_patchSearchCode = selO.search;
            m_patchSearchLineRange = NormalizeSearchLineRange(selO.search_line_range);
            UpdatePatchSearchHighlightedCode();
            m_patchSearchScroll = Vector2.zero;
            UpdatePatchHighlightedCode();
            m_patchCodeScroll = Vector2.zero;
            Repaint();
        }

        private void RemoveManifestItem(string pluginRoot, int index)
        {
            if (index < 0 || index >= m_manifestItems.Count) return;

            var item = m_manifestItems[index];
            if (item?.op == null) return;

            for (int t = 0; t < m_manifest.targets.Count; t++)
            {
                var tgt = m_manifest.targets[t];
                if (tgt?.insert_at == null) continue;

                int found = tgt.insert_at.IndexOf(item.op);
                if (found >= 0)
                {
                    tgt.insert_at.RemoveAt(found);
                    if (tgt.insert_at.Count == 0)
                        m_manifest.targets.RemoveAt(t);
                    break;
                }
            }

            m_manifestItems.RemoveAt(index);
            m_manifestList.index = Mathf.Clamp(m_manifestList.index, 0, m_manifestItems.Count - 1);
            RefreshManifestSelection(m_manifestList.index >= 0 && m_manifestList.index < m_manifestItems.Count
                ? m_manifestItems[m_manifestList.index].op
                : null);
            LoadSelectionIntoFields();
            SaveManifestReplace(pluginRoot);
        }

        private void DrawManifestElement(string pluginRoot, Rect rect, int index)
        {
            if (index < 0 || index >= m_manifestItems.Count) return;

            var item = m_manifestItems[index];
            rect.height = EditorGUIUtility.singleLineHeight;
            rect.y += 2f;

            float removeWidth = m_manifestDragging ? 0f : 70f;
            var numberRect = new Rect(rect.x + 6f, rect.y, 32f, rect.height);
            EditorGUI.LabelField(numberRect, $"{index + 1}.", EditorStyles.miniBoldLabel);

            float labelWidth = Mathf.Max(0f, rect.width - numberRect.width - removeWidth - 14f);
            var labelRect = new Rect(numberRect.xMax + 4f, rect.y, labelWidth, rect.height);
            if (UnityEngine.GUI.Button(labelRect, new GUIContent(item.label, item.target?.path), EditorStyles.label))
            {
                m_manifestList.index = index;
                SelectManifestItem(pluginRoot, index);
            }

            if (!m_manifestDragging)
            {
                var removeRect = new Rect(rect.xMax - removeWidth, rect.y - 1f, removeWidth, rect.height + 2f);
                if (UnityEngine.GUI.Button(removeRect, TT("Remove", "Remove this edit from the manifest")))
                {
                    RemoveManifestItem(pluginRoot, index);
                    GUIUtility.ExitGUI();
                }
            }
        }

        // Convert inspector UI op -> patcher op
        private static PluginCorePatcher.InsertAtOp ToPatcherOp(InsertAt src)
        {
            return new PluginCorePatcher.InsertAtOp
            {
                id = src.id,
                line = src.line,
                column = src.column,
                insert = src.insert,
                replace_line = src.replace_line,
                search = src.search,
                use_search = src.use_search,
                search_line_range = NormalizeSearchLineRange(src.search_line_range)
            };
        }

        private string ManifestPath(string pluginRoot) => Path.Combine(pluginRoot, "plugin.patches.json");

        private static string GetPluginName(string pluginRoot)
        {
            if (string.IsNullOrEmpty(pluginRoot)) return string.Empty;
            string trimmed = pluginRoot.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            return Path.GetFileName(trimmed);
        }

        private void EnsureManifestSynced(string pluginRoot)
        {
            if (string.IsNullOrEmpty(pluginRoot)) return;
            string path = ManifestPath(pluginRoot);
            long ticks = File.Exists(path) ? File.GetLastWriteTimeUtc(path).Ticks : -1;
            if (!string.Equals(m_manifestLastPath, path, StringComparison.OrdinalIgnoreCase) || m_manifestLastWriteTicks != ticks)
            {
                string prevId = null;
                if (m_selTargetIdx >= 0 && m_selTargetIdx < (m_manifest.targets?.Count ?? 0))
                {
                    var prevTarget = m_manifest.targets[m_selTargetIdx];
                    if (prevTarget?.insert_at != null && m_selOpIdx >= 0 && m_selOpIdx < prevTarget.insert_at.Count)
                        prevId = prevTarget.insert_at[m_selOpIdx]?.id;
                }

                LoadManifest(pluginRoot);

                if (!string.IsNullOrEmpty(prevId))
                {
                    for (int t = 0; t < (m_manifest.targets?.Count ?? 0); t++)
                    {
                        var tgt = m_manifest.targets[t];
                        if (tgt?.insert_at == null) continue;
                        for (int o = 0; o < tgt.insert_at.Count; o++)
                        {
                            if (string.Equals(tgt.insert_at[o]?.id, prevId, StringComparison.Ordinal))
                            {
                                m_selTargetIdx = t;
                                m_selOpIdx = o;
                                return;
                            }
                        }
                    }
                }
            }
        }

        private bool TryGetCoreFileText(string assetPath, Dictionary<string, string> cache, out string text, out bool missing)
        {
            text = null;
            missing = false;
            if (string.IsNullOrEmpty(assetPath)) return false;

            if (cache != null && cache.TryGetValue(assetPath, out text))
            {
                if (text == null) missing = true;
                return text != null;
            }

            string abs = ToAbsolute(assetPath);
            if (!File.Exists(abs))
            {
                missing = true;
                if (cache != null) cache[assetPath] = null;
                return false;
            }

            try { text = File.ReadAllText(abs); }
            catch { return false; }

            if (cache != null) cache[assetPath] = text;
            return true;
        }

        private ManifestEditState GetManifestEditState(string pluginRoot, Target target, InsertAt op, Dictionary<string, string> fileCache = null)
        {
            if (target == null || op == null) return ManifestEditState.Unknown;

            if (!TryGetCoreFileText(target.path, fileCache, out var text, out bool missing))
                return missing ? ManifestEditState.FileMissing : ManifestEditState.Unknown;

            string pluginName = GetPluginName(pluginRoot);
            if (string.IsNullOrEmpty(pluginName) || string.IsNullOrEmpty(op.id))
                return ManifestEditState.Unknown;

            string startMarker = $"// >>> PLUGIN_PATCH:{pluginName}::{op.id}";
            string endMarker = $"// <<< PLUGIN_PATCH:{pluginName}::{op.id}";

            bool hasStart = text.IndexOf(startMarker, StringComparison.Ordinal) >= 0;
            bool hasEnd = text.IndexOf(endMarker, StringComparison.Ordinal) >= 0;
            return (hasStart && hasEnd) ? ManifestEditState.Applied : ManifestEditState.NotApplied;
        }

        private static string StatePrefix(ManifestEditState state)
        {
            switch (state)
            {
                case ManifestEditState.Applied: return "[Applied] ";
                case ManifestEditState.NotApplied: return "[Not Applied] ";
                case ManifestEditState.FileMissing: return "[Missing File] ";
                default: return string.Empty;
            }
        }

        private void LoadManifest(string pluginRoot)
        {
            var path = ManifestPath(pluginRoot);
            if (File.Exists(path))
            {
                try { m_manifest = JsonUtility.FromJson<PatchRecipe>(File.ReadAllText(path)) ?? new PatchRecipe(); }
                catch { m_manifest = new PatchRecipe(); }
            }
            else m_manifest = new PatchRecipe();

            m_selTargetIdx = m_manifest.targets.Count > 0 ? 0 : -1;
            m_selOpIdx = (m_selTargetIdx >= 0 && m_manifest.targets[m_selTargetIdx].insert_at.Count > 0) ? 0 : -1;

            m_manifestLastPath = path;
            m_manifestLastWriteTicks = File.Exists(path) ? File.GetLastWriteTimeUtc(path).Ticks : -1;
        }

        private void SaveManifestReplace(string pluginRoot)
        {
            var path = ManifestPath(pluginRoot);
            // prune empties
            m_manifest.targets.RemoveAll(t => t == null || string.IsNullOrEmpty(t.path) || t.insert_at == null || t.insert_at.Count == 0);
            File.WriteAllText(path, JsonUtility.ToJson(m_manifest, true), Encoding.UTF8);
            AssetDatabase.Refresh();
        }
    }
}
