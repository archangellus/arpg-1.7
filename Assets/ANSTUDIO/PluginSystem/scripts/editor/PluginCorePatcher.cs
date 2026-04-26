using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace PLAYERTWO.ARPGProject
{
    /// <summary>
    /// Applies and reverts code patches described by plugin-local JSON manifests.
    /// Now supports:
    ///  - insert_once (anchor-based)
    ///  - replace_region (anchor-based)
    ///  - insert_at (line + column based)
    /// </summary>
    public static class PluginCorePatcher
    {
        [Serializable] public class PatchRecipe { public List<Target> targets = new(); }
        [Serializable]
        public class Target
        {
            public string path;
            public List<InsertOnceOp> insert_once = new();
            public List<ReplaceRegionOp> replace_region = new();
            public List<InsertAtOp> insert_at = new();     // NEW
        }

        [Serializable]
        public class InsertOnceOp
        {
            public string id;
            public string anchor_regex;
            public string placement = "after"; // "after" | "before" | "after_last_match" | "end_of_file"
            public int indent = 0;
            public string insert;
        }

        [Serializable]
        public class ReplaceRegionOp
        {
            public string id;
            public string start_regex;
            public string end_regex;
            public string replacement;
        }

        [Serializable]
        public class InsertAtOp
        {
            public string id;
            public int line;            // 0-based line where we append (end of that line)
            public int column;          // unused for placement; used only to compute target indent (see below)
            public string insert;       // the code snippet (multi-line ok)
            public int order;
            public bool replace_line;
            public string search;
            public bool use_search = true;
            public int search_line_range = DefaultSearchLineRange;
        }

        private const int DefaultSearchLineRange = 10;
        private const string ReplaceMetadataPrefix = "// __PLUGIN_REPLACE_ORIGINAL:";


        private struct LineShift
        {
            public int pivotLine;
            public int delta;
        }







        public struct PatchReport
        {
            public int filesTouched, opsApplied, opsSkipped, opsReverted;
            public List<string> messages;
        }

        private class FilePatchContext
        {
            public string assetPath;
            public string absPath;
            public string text;
            public string original;
            public bool changed;
            public bool missing;
            public DateTime lastReadUtc;
        }

        public static PatchReport ApplyAll(string pluginRoot)
        {
            var report = new PatchReport { messages = new List<string>() };

            if (string.IsNullOrEmpty(pluginRoot) || !Directory.Exists(pluginRoot))
            {
                report.messages.Add("No plugin selected or folder missing.");
                return report;
            }

            string pluginName = Path.GetFileName(pluginRoot.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
            string manifestPath = Path.Combine(pluginRoot, "plugin.patches.json");

            if (!File.Exists(manifestPath))
            {
                report.messages.Add($"No manifest found at: {manifestPath}");
                return report;
            }

            var recipe = JsonUtility.FromJson<PatchRecipe>(File.ReadAllText(manifestPath));
            if (recipe == null || recipe.targets == null || recipe.targets.Count == 0)
            {
                report.messages.Add("Empty or invalid patch manifest.");
                return report;
            }

            EnsureInsertAtOrders(recipe);

            var contexts = new Dictionary<string, FilePatchContext>(StringComparer.OrdinalIgnoreCase);

            FilePatchContext GetContext(Target target)
            {
                if (target == null || string.IsNullOrEmpty(target.path)) return null;
                if (contexts.TryGetValue(target.path, out var cached)) return cached;

                string abs = ToAbsolutePath(target.path);
                if (!File.Exists(abs))
                {
                    report.messages.Add($"Missing target file: {target.path}");
                    contexts[target.path] = new FilePatchContext { assetPath = target.path, absPath = abs, missing = true };
                    return null;
                }

                string text = File.ReadAllText(abs);
                var ctx = new FilePatchContext
                {
                    assetPath = target.path,
                    absPath = abs,
                    text = text,
                    original = text,
                    changed = false,
                    missing = false,
                    lastReadUtc = File.GetLastWriteTimeUtc(abs)
                };

                contexts[target.path] = ctx;
                BackupOnce(abs);
                return ctx;
            }

            void RefreshContextFromDisk(FilePatchContext ctx)
            {
                if (ctx == null || ctx.missing || ctx.changed) return;

                DateTime diskWrite = File.GetLastWriteTimeUtc(ctx.absPath);
                if (diskWrite <= ctx.lastReadUtc) return;

                string fresh = File.ReadAllText(ctx.absPath);
                ctx.text = fresh;
                ctx.original = fresh;
                ctx.lastReadUtc = diskWrite;
            }

            // Pass 1: insert_once + replace_region (per file, order preserved)
            foreach (var target in recipe.targets)
            {
                var ctx = GetContext(target);
                if (ctx == null || ctx.missing) continue;

                RefreshContextFromDisk(ctx);

                string text = ctx.text;
                bool changed = ctx.changed;

                foreach (var op in target.insert_once ?? Enumerable.Empty<InsertOnceOp>())
                {
                    string startMarker = MarkerStart(pluginName, op.id);
                    string endMarker = MarkerEnd(pluginName, op.id);

                    if (text.Contains(startMarker) && text.Contains(endMarker))
                    {
                        report.opsSkipped++;
                        continue;
                    }

                    string insertion = WrapWithMarkers(pluginName, op.id, Indent(op.insert, op.indent));

                    if (string.Equals(op.placement, "end_of_file", StringComparison.OrdinalIgnoreCase))
                    {
                        text += EnsureTrailingNewline(text) + insertion + DetectNewline(text);
                        changed = true; report.opsApplied++;
                        continue;
                    }

                    try
                    {
                        var rx = new System.Text.RegularExpressions.Regex(op.anchor_regex, System.Text.RegularExpressions.RegexOptions.Multiline);
                        var matches = rx.Matches(text);
                        if (matches.Count == 0)
                        {
                            report.messages.Add($"[{Path.GetFileName(ctx.absPath)}] Anchor not found: {op.id}");
                            continue;
                        }

                        int index;
                        if (string.Equals(op.placement, "before", StringComparison.OrdinalIgnoreCase))
                        {
                            index = matches[0].Index;
                        }
                        else if (string.Equals(op.placement, "after_last_match", StringComparison.OrdinalIgnoreCase))
                        {
                            var m = matches[matches.Count - 1];
                            index = m.Index + m.Length;
                        }
                        else // "after" first match
                        {
                            var m = matches[0];
                            index = m.Index + m.Length;
                        }

                        text = text.Insert(index, SurroundWithNewlines(text, index, insertion));
                        changed = true; report.opsApplied++;
                    }
                    catch (Exception ex)
                    {
                        report.messages.Add($"[{Path.GetFileName(ctx.absPath)}] insert_once failed for {op.id}: {ex.Message}");
                    }
                }

                foreach (var op in target.replace_region ?? Enumerable.Empty<ReplaceRegionOp>())
                {
                    string startMarker = MarkerStart(pluginName, op.id);
                    string endMarker = MarkerEnd(pluginName, op.id);

                    text = StripMarkedRegion(text, startMarker, endMarker);

                    try
                    {
                        var startRx = new System.Text.RegularExpressions.Regex(op.start_regex, System.Text.RegularExpressions.RegexOptions.Multiline);
                        var endRx = new System.Text.RegularExpressions.Regex(op.end_regex, System.Text.RegularExpressions.RegexOptions.Multiline);

                        var start = startRx.Match(text);
                        var end = endRx.Match(text, start.Success ? start.Index + start.Length : 0);

                        if (!start.Success || !end.Success || end.Index < start.Index)
                        {
                            report.messages.Add($"[{Path.GetFileName(ctx.absPath)}] Region not found for {op.id}");
                            continue;
                        }

                        int len = end.Index + end.Length - start.Index;
                        string replacement = WrapWithMarkers(pluginName, op.id, op.replacement);
                        text = text.Remove(start.Index, len).Insert(start.Index, replacement);
                        changed = true; report.opsApplied++;
                    }
                    catch (Exception ex)
                    {
                        report.messages.Add($"[{Path.GetFileName(ctx.absPath)}] replace_region failed for {op.id}: {ex.Message}");
                    }
                }

                ctx.text = text;
                ctx.changed = changed;
            }

            // Pass 2: insert_at in explicit manifest order across all targets
            var orderedInsertOps = recipe.targets
                .Select((t, ti) => new { target = t, targetIndex = ti })
                .Where(x => x.target?.insert_at != null)
                .SelectMany(x => x.target.insert_at.Select((op, oi) => new
                {
                    target = x.target,
                    op,
                    x.targetIndex,
                    opIndex = oi
                }))
                .OrderBy(x => x.op.order)
                .ThenBy(x => x.targetIndex)
                .ThenBy(x => x.opIndex)
                .ToList();

            var lineShiftMap = new Dictionary<string, List<LineShift>>(StringComparer.OrdinalIgnoreCase);

            foreach (var entry in orderedInsertOps)
            {
                var ctx = GetContext(entry.target);
                if (ctx == null || ctx.missing) continue;

                RefreshContextFromDisk(ctx);

                var op = entry.op;
                var shifts = GetOrCreateLineShifts(lineShiftMap, ctx.assetPath);

                if (shifts.Count == 0)
                {
                    int initialDelta = CountTotalLines(ctx.text) - CountTotalLines(ctx.original);
                    RecordLineShift(shifts, 0, initialDelta);
                }

                int adjustedLine = GetAdjustedLine(op.line, shifts);
                string text = ctx.text;
                string startPrefix = MarkerPrefix(pluginName);
                string endPrefix = MarkerSuffix(pluginName);
                string startMarker = startPrefix + op.id;
                string endMarker = endPrefix + op.id;

                if (text.Contains(startMarker) && text.Contains(endMarker))
                {
                    report.opsSkipped++;
                    report.messages.Add($"[{Path.GetFileName(ctx.absPath)}] Skipped (already applied): {op.id}");
                    continue;
                }

                try
                {
                    int targetLine = ResolveTargetLine(text, op, ctx.absPath, report.messages, adjustedLine);
                    int lineCountBefore = CountTotalLines(text);
                    GetLineStartEnd(text, targetLine, out int lineStart, out int lineEnd);
                    int baseIndentSpaces = CountIndentSpaces(text, lineStart);

                    bool isClosingBraceLine = IsClosingBraceLine(text, lineStart, lineEnd);

                    int insertIdx;
                    string nl = DetectNewline(text);
                    string leading = "";
                    string replacedLine = null;

                    if (op.replace_line)
                    {
                        int lineEndWithNl = FindLineEnd(text, lineEnd);
                        replacedLine = text.Substring(lineStart, lineEndWithNl - lineStart);
                        text = text.Remove(lineStart, lineEndWithNl - lineStart);
                        insertIdx = lineStart;
                    }
                    else if (isClosingBraceLine)
                    {
                        insertIdx = lineStart;
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
                            leading = nl;
                        }
                    }

                    int defaultInsideIndent = isClosingBraceLine ? (baseIndentSpaces + 4) : baseIndentSpaces;

                    int desiredIndentSpaces = Math.Max(defaultInsideIndent, Math.Max(0, op.column));

                    string indent = new string(' ', desiredIndentSpaces);
                    string codeIndented = IndentEachLine(op.insert, indent);
                    string metadata = BuildReplaceMetadata(replacedLine, indent, nl);

                    string payload =
                          leading
                        + indent + startMarker + nl
                        + metadata
                        + codeIndented + nl
                        + indent + endMarker + nl;

                    text = text.Insert(insertIdx, payload);
                    ctx.changed = true; report.opsApplied++;
                    ctx.text = text;

                    int lineCountAfter = CountTotalLines(text);
                    RecordLineShift(shifts, op.line, lineCountAfter - lineCountBefore);
                }
                catch (Exception ex)
                {
                    report.messages.Add($"[{Path.GetFileName(ctx.absPath)}] insert_at failed for {op.id}: {ex.Message}");
                }
            }

            foreach (var ctx in contexts.Values)
            {
                if (ctx.missing || !ctx.changed || ctx.text == ctx.original) continue;
                File.WriteAllText(ctx.absPath, ctx.text, Encoding.UTF8);
                ctx.lastReadUtc = File.GetLastWriteTimeUtc(ctx.absPath);
                report.filesTouched++;
            }

            AssetDatabase.Refresh();
            return report;
        }

        private static int EnsureInsertAtOrders(PatchRecipe recipe)
        {
            if (recipe?.targets == null) return 1;
            int maxOrder = 0;
            foreach (var t in recipe.targets)
                foreach (var op in t.insert_at ?? Enumerable.Empty<InsertAtOp>())
                    maxOrder = Math.Max(maxOrder, op.order);

            int next = Math.Max(1, maxOrder + 1);
            foreach (var t in recipe.targets)
            {
                if (t?.insert_at == null) continue;
                foreach (var op in t.insert_at)
                {
                    if (op.order <= 0) op.order = next++;
                }
            }

            return next;
        }

        private static string RepeatNewlines(string nl, int count)
        {
            if (count <= 0) return string.Empty;
            var sb = new System.Text.StringBuilder(nl.Length * count);
            for (int i = 0; i < count; i++) sb.Append(nl);
            return sb.ToString();
        }


        public static PatchReport RevertAll(string pluginRoot)
        {
            var report = new PatchReport { messages = new List<string>() };
            if (string.IsNullOrEmpty(pluginRoot) || !Directory.Exists(pluginRoot)) return report;

            string pluginName = Path.GetFileName(pluginRoot.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
            string startPrefix = MarkerPrefix(pluginName);
            string endPrefix = MarkerSuffix(pluginName);

            bool IsEligible(string p) =>
                p.EndsWith(".cs") || p.EndsWith(".shader") || p.EndsWith(".cginc") || p.EndsWith(".hlsl") ||
                p.EndsWith(".compute") || p.EndsWith(".uxml") || p.EndsWith(".uss") || p.EndsWith(".json") || p.EndsWith(".txt");

            foreach (var file in Directory.GetFiles(Application.dataPath, "*.*", SearchOption.AllDirectories))
            {
                if (!IsEligible(file)) continue;

                string text = File.ReadAllText(file);
                string before = text;
                bool changed = false;
                int safety = 0;

                while (true)
                {
                    if (++safety > 10000) break;
                    int a = text.IndexOf(startPrefix, StringComparison.Ordinal);
                    if (a < 0) break;

                    // Find the matching end marker using the same id captured from the start marker
                    int startIdBeg = a + startPrefix.Length;
                    int startIdEnd = FindLineEnd(text, startIdBeg);
                    string id = text.Substring(startIdBeg, startIdEnd - startIdBeg).Trim();
                    string endMarker = endPrefix + id;
                    string replacedLine = TryExtractReplacedLine(text, startIdEnd);

                    int b = text.IndexOf(endMarker, startIdEnd, StringComparison.Ordinal);
                    if (b < 0)
                    {
                        // If the end marker is missing, remove just the inline start marker token + trailing newline if any
                        int endOfStartLine = FindLineEnd(text, a);
                        int removeLen = endOfStartLine - a;
                        if (replacedLine != null)
                        {
                            int metaEnd = FindLineEnd(text, startIdEnd);
                            removeLen = metaEnd - a;
                        }

                        text = text.Remove(a, removeLen);
                        if (replacedLine != null)
                        {
                            text = text.Insert(a, replacedLine);
                        }
                        changed = true; report.opsReverted++;
                        continue;
                    }

                    // Remove from the *marker index* (not line start) through end of the end-marker line
                    int endOfEndLine = FindLineEnd(text, b + endMarker.Length);
                    int len = endOfEndLine - a;
                    text = text.Remove(a, len);
                    if (replacedLine != null)
                    {
                        text = text.Insert(a, replacedLine);
                    }
                    changed = true; report.opsReverted++;
                }

                if (changed && text != before)
                {
                    BackupOnce(file);
                    File.WriteAllText(file, text, Encoding.UTF8);
                    report.filesTouched++;
                }
            }

            AssetDatabase.Refresh();
            return report;
        }



        // ───────────────────────────── helpers ─────────────────────────────

        public static void ApplyInsertAt(string pluginRoot, string assetPath, InsertAtOp op)
        {
            string pluginName = System.IO.Path.GetFileName(pluginRoot.TrimEnd(System.IO.Path.DirectorySeparatorChar, System.IO.Path.AltDirectorySeparatorChar));
            string abs = ToAbsolutePath(assetPath);
            if (!System.IO.File.Exists(abs)) throw new Exception($"Target file not found: {assetPath}");

            string text = System.IO.File.ReadAllText(abs);
            string startPrefix = MarkerPrefix(pluginName);
            string endPrefix = MarkerSuffix(pluginName);
            string startMarker = startPrefix + op.id;
            string endMarker = endPrefix + op.id;

            if (text.Contains(startMarker) && text.Contains(endMarker)) return; // already applied

            // --- SAME placement as in your latest insert_at logic (after target line, except '}' -> before)
            int targetLine = ResolveTargetLine(text, op, abs, null);
            GetLineStartEnd(text, targetLine, out int lineStart, out int lineEnd);
            int baseIndentSpaces = CountIndentSpaces(text, lineStart);
            bool isClosingBrace = IsClosingBraceLine(text, lineStart, lineEnd);

            int insertIdx;
            string nl = DetectNewline(text);
            string leading = "";
            string replacedLine = null;

            if (op.replace_line)
            {
                int lineEndWithNl = FindLineEnd(text, lineEnd);
                replacedLine = text.Substring(lineStart, lineEndWithNl - lineStart);
                text = text.Remove(lineStart, lineEndWithNl - lineStart);
                insertIdx = lineStart;
            }
            else if (isClosingBrace)
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
                else leading = nl;
            }

            int desiredIndentSpaces = Math.Max(baseIndentSpaces, Math.Max(0, op.column));
            string indent = new string(' ', desiredIndentSpaces);

            string codeIndented = IndentEachLine(op.insert, indent);
            string metadata = BuildReplaceMetadata(replacedLine, indent, nl);
            string payload =
                  leading
                + indent + startMarker + nl
                + metadata
                + codeIndented + nl
                + indent + endMarker + nl;

            BackupOnce(abs);
            text = text.Insert(insertIdx, payload);
            System.IO.File.WriteAllText(abs, text, System.Text.Encoding.UTF8);
            AssetDatabase.Refresh();
        }

        private static int ResolveTargetLine(string text, InsertAtOp op, string absPath, List<string> messages, int? desiredLineOverride = null)
        {
            if (op == null) return 0;

            int desiredLine = ClampToExistingLine(text, Math.Max(0, desiredLineOverride ?? op.line));
            if (op.use_search && !string.IsNullOrEmpty(op.search))
            {
                if (TryResolveLineBySearch(text, op, desiredLine, out int matchLine))
                {
                    int offset = op.line - matchLine;
                    return ClampToExistingLine(text, matchLine + offset);
                }

                int normalizedRange = NormalizeSearchLineRange(op.search_line_range);
                string message = $"[{Path.GetFileName(absPath)}] Search text not found for {op.id} within ±{normalizedRange} lines of {desiredLine}.";
                if (messages != null && !string.IsNullOrEmpty(absPath)) messages.Add(message);
                throw new Exception(message);
            }

            return desiredLine;
        }

        private static bool TryResolveLineBySearch(string text, InsertAtOp op, int desiredLine, out int lineIndex)
        {
            lineIndex = -1;
            if (op == null || string.IsNullOrEmpty(text) || !op.use_search || string.IsNullOrEmpty(op.search)) return false;

            string normFile = NormalizeNewlines(text);
            string normSearch = NormalizeNewlines(op.search);
            if (string.IsNullOrEmpty(normSearch)) return false;

            int normalizedRange = NormalizeSearchLineRange(op.search_line_range);
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
                int distance = Math.Abs(candidateLine - desiredLine);
                if (distance <= normalizedRange && distance < bestDistance)
                {
                    bestDistance = distance;
                    bestLine = candidateLine;
                    if (distance == 0) break;
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
            if (index < 0) index = 0;
            if (index > text.Length) index = text.Length;
            int lines = 0;
            for (int i = 0; i < index; i++)
                if (text[i] == '\n') lines++;
            return lines;
        }

        private static int NormalizeSearchLineRange(int range)
        {
            return range > 0 ? range : DefaultSearchLineRange;
        }

        private static int ClampToExistingLine(string text, int requestedLine)
        {
            int maxLineIndex = 0;
            if (!string.IsNullOrEmpty(text))
            {
                for (int i = 0; i < text.Length; i++)
                {
                    if (text[i] == '\n') maxLineIndex++;
                    else if (text[i] == '\r')
                    {
                        maxLineIndex++;
                        if (i + 1 < text.Length && text[i + 1] == '\n') i++;
                    }
                }
            }

            if (requestedLine < 0) return 0;
            return Math.Min(requestedLine, maxLineIndex);
        }

        private static string NormalizeNewlines(string value)
        {
            return string.IsNullOrEmpty(value)
                ? string.Empty
                : value.Replace("\r\n", "\n").Replace("\r", "\n");
        }

        private static string BuildReplaceMetadata(string removedLine, string indent, string nl)
        {
            if (string.IsNullOrEmpty(removedLine)) return string.Empty;
            string encoded = Convert.ToBase64String(Encoding.UTF8.GetBytes(removedLine));
            return indent + ReplaceMetadataPrefix + encoded + nl;
        }

        private static string TryExtractReplacedLine(string text, int metaLineStart)
        {
            if (metaLineStart >= text.Length) return null;
            int metaLineEnd = FindLineEnd(text, metaLineStart);
            string line = text.Substring(metaLineStart, metaLineEnd - metaLineStart).Trim();
            if (!line.StartsWith(ReplaceMetadataPrefix, StringComparison.Ordinal)) return null;

            string encoded = line.Substring(ReplaceMetadataPrefix.Length).Trim();
            try
            {
                return Encoding.UTF8.GetString(Convert.FromBase64String(encoded));
            }
            catch
            {
                return null;
            }
        }

        public static int RevertById(string pluginRoot, string id)
        {
            if (string.IsNullOrEmpty(id)) return 0;
            string pluginName = System.IO.Path.GetFileName(pluginRoot.TrimEnd(System.IO.Path.DirectorySeparatorChar, System.IO.Path.AltDirectorySeparatorChar));
            string startMarker = MarkerPrefix(pluginName) + id;
            string endMarker = MarkerSuffix(pluginName) + id;

            int removed = 0;

            bool IsEligible(string p) =>
                p.EndsWith(".cs") || p.EndsWith(".shader") || p.EndsWith(".cginc") || p.EndsWith(".hlsl") ||
                p.EndsWith(".compute") || p.EndsWith(".uxml") || p.EndsWith(".uss") || p.EndsWith(".json") || p.EndsWith(".txt");

            foreach (var file in System.IO.Directory.GetFiles(Application.dataPath, "*.*", SearchOption.AllDirectories))
            {
                if (!IsEligible(file)) continue;

                string text = System.IO.File.ReadAllText(file);
                int a = text.IndexOf(startMarker, StringComparison.Ordinal);
                if (a < 0) continue;

                string replacedLine = TryExtractReplacedLine(text, FindLineEnd(text, a));
                int endIdx = text.IndexOf(endMarker, a, StringComparison.Ordinal);
                if (endIdx < 0)
                {
                    // remove just the start marker line if malformed
                    int eol = FindLineEnd(text, a);
                    int removeLen = eol - a;
                    if (replacedLine != null)
                    {
                        int metaEnd = FindLineEnd(text, eol);
                        removeLen = metaEnd - a;
                    }

                    text = text.Remove(a, removeLen);
                    if (replacedLine != null)
                    {
                        text = text.Insert(a, replacedLine);
                    }
                    BackupOnce(file);
                    System.IO.File.WriteAllText(file, text, System.Text.Encoding.UTF8);
                    removed++;
                    continue;
                }

                int endOfEndLine = FindLineEnd(text, endIdx + endMarker.Length);
                text = text.Remove(a, endOfEndLine - a);
                if (replacedLine != null)
                {
                    text = text.Insert(a, replacedLine);
                }
                BackupOnce(file);
                System.IO.File.WriteAllText(file, text, System.Text.Encoding.UTF8);
                removed++;
            }

            AssetDatabase.Refresh();
            return removed;
        }


        private static bool IsClosingBraceLine(string text, int lineStart, int lineEnd)
        {
            for (int i = lineStart; i < lineEnd; i++)
            {
                char c = text[i];
                if (c == ' ' || c == '\t') continue;
                return c == '}'; // first non-ws is a closing brace
            }
            return false;
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

        private static List<LineShift> GetOrCreateLineShifts(Dictionary<string, List<LineShift>> map, string assetPath)
        {
            if (map.TryGetValue(assetPath, out var list)) return list;

            list = new List<LineShift>();
            map[assetPath] = list;
            return list;
        }

        private static int GetAdjustedLine(int originalLine, List<LineShift> shifts)
        {
            int adjusted = originalLine;
            if (shifts != null)
            {
                foreach (var shift in shifts)
                {
                    if (originalLine >= shift.pivotLine) adjusted += shift.delta;
                }
            }

            return adjusted;
        }

        private static void RecordLineShift(List<LineShift> shifts, int pivotLine, int delta)
        {
            if (shifts == null || delta == 0) return;
            shifts.Add(new LineShift { pivotLine = pivotLine, delta = delta });
        }

        private static int CountTotalLines(string text)
        {
            if (string.IsNullOrEmpty(text)) return 0;
            string norm = NormalizeNewlines(text);
            return CountLinesBeforeIndex(norm, norm.Length);
        }

        private static void GetLineRangeSpan(string text, int startLine, int endLine, out int startIndex, out int endIndex)
        {
            startIndex = 0;
            endIndex = string.IsNullOrEmpty(text) ? 0 : text.Length;
            if (string.IsNullOrEmpty(text)) return;

            startLine = Math.Max(0, startLine);
            endLine = Math.Max(startLine, endLine);

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

        private static string IndentEachLine(string code, string indent)
        {
            if (string.IsNullOrEmpty(code)) return code;
            string norm = code.Replace("\r\n", "\n").Replace("\r", "\n");
            var lines = norm.Split('\n');
            for (int i = 0; i < lines.Length; i++)
                if (lines[i].Length > 0) lines[i] = indent + lines[i];
            return string.Join("\n", lines);
        }


        private static int FindLineEnd(string s, int idx)
        {
            int n = s.Length;
            while (idx < n && s[idx] != '\n' && s[idx] != '\r') idx++;
            if (idx < n && s[idx] == '\r') idx++;
            if (idx < n && s[idx] == '\n') idx++;
            return idx;
        }

        // Behavior:
        //  - "none":    no extra newlines
        //  - "before":  add newline only before if needed
        //  - "after":   add newline only after if needed
        //  - "both":    add before AND after if needed
        //  - "auto":    NEVER add before; add one AFTER if next char isn't a newline
        private static string ApplyNewlineMode(string text, int idx, string marked, string mode, string nl)
        {
            mode = string.IsNullOrEmpty(mode) ? "auto" : mode.ToLowerInvariant();
            bool prevIsNl = idx > 0 && (text[idx - 1] == '\n' || text[idx - 1] == '\r');
            bool nextIsNl = idx < text.Length && (text[idx] == '\n' || text[idx] == '\r');

            string before = "", after = "";

            switch (mode)
            {
                case "none":
                    break;

                case "before":
                    if (!prevIsNl) before = nl;
                    break;

                case "after":
                    if (!nextIsNl) after = nl;
                    break;

                case "both":
                    if (!prevIsNl) before = nl;
                    if (!nextIsNl) after = nl;
                    break;

                case "auto":
                default:
                    // Key change: never add BEFORE in auto mode.
                    if (!nextIsNl) after = nl;
                    break;
            }

            return before + marked + after;
        }



        private static int IndexFromLineColumn(string text, int targetLine, int targetColumn)
        {
            if (targetLine < 0) targetLine = 0;
            if (targetColumn < 0) targetColumn = 0;

            int i = 0, line = 0, n = text.Length;
            while (i < n && line < targetLine)
            {
                char c = text[i++];
                if (c == '\r') { if (i < n && text[i] == '\n') i++; line++; }
                else if (c == '\n') line++;
            }
            int lineStart = i;
            while (i < n && text[i] != '\r' && text[i] != '\n') i++;
            int lineEnd = i;
            int col = Math.Min(targetColumn, lineEnd - lineStart);
            return lineStart + col;
        }

        private static string EnsureTrailingNewline(string s) => s.EndsWith("\n") ? "" : "\n";

        private static string SurroundWithNewlines(string text, int insertIndex, string payload)
        {
            var sb = new StringBuilder();
            if (insertIndex > 0 && text[insertIndex - 1] != '\n') sb.Append('\n');
            sb.Append(payload);
            if (insertIndex < text.Length && text[insertIndex] != '\n') sb.Append('\n');
            return sb.ToString();
        }

        private static string Indent(string code, int levels)
        {
            if (levels <= 0) return code;
            string pad = new string(' ', 4 * levels);
            var lines = code.Replace("\r\n", "\n").Split('\n');
            for (int i = 0; i < lines.Length; i++)
                if (lines[i].Length > 0) lines[i] = pad + lines[i];
            return string.Join("\n", lines);
        }

        private static string ToAbsolutePath(string assetRelative)
        {
            if (assetRelative.StartsWith("Assets/"))
                return Path.Combine(Directory.GetParent(Application.dataPath)!.FullName, assetRelative).Replace('\\', '/');
            return assetRelative;
        }

        private static string MarkerPrefix(string plugin) => $"// >>> PLUGIN_PATCH:{plugin}::";
        private static string MarkerSuffix(string plugin) => $"// <<< PLUGIN_PATCH:{plugin}::";
        private static string MarkerStart(string plugin, string id) => $"{MarkerPrefix(plugin)}{id}";
        private static string MarkerEnd(string plugin, string id) => $"{MarkerSuffix(plugin)}{id}";
        private static string WrapWithMarkers(string plugin, string id, string body) =>
            $"{MarkerStart(plugin, id)}\n{body}\n{MarkerEnd(plugin, id)}";

        private static string StripMarkedRegion(string text, string startMarker, string endMarker)
        {
            int a = text.IndexOf(startMarker, StringComparison.Ordinal);
            if (a < 0) return text;
            int b = text.IndexOf(endMarker, a, StringComparison.Ordinal);
            if (b < 0) return text;
            b += endMarker.Length;
            if (b < text.Length && (text[b] == '\n' || text[b] == '\r')) b++;
            return text.Remove(a, b - a);
        }

        private static readonly HashSet<string> s_backedUp = new();
        private static void BackupOnce(string absPath)
        {
            if (s_backedUp.Contains(absPath)) return;
            string backup = absPath + ".before_plugin_patch";
            if (!File.Exists(backup)) File.Copy(absPath, backup, overwrite: false);
            s_backedUp.Add(absPath);
        }
    }
}
