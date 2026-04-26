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
    public partial class PluginInspectorWindow
    {
        private static readonly string[] s_keywords =
        {
            "abstract","as","base","break","case","catch","class","const","continue","default","do",
            "else","enum","event","explicit","extern","false","finally","fixed","for","foreach","goto",
            "if","implicit","in","interface","internal","is","lock","namespace","new","null","operator",
            "out","override","params","private","protected","public","readonly","ref","return","sealed",
            "sizeof","stackalloc","static","struct","switch","this","throw","true","try","typeof","using",
            "virtual","void","volatile","while"
        };

        private void SaveProfile()
        {
            string path = EditorUtility.SaveFilePanel(
                "Save Color Profile",
                Path.GetDirectoryName(EditorPrefs.GetString(PREF_LAST_PROFILE, "Assets")),
                "PluginColors",
                PROFILE_EXT);

            if (string.IsNullOrEmpty(path)) return;

            var p = new PluginColorProfile
            {
                keyword = m_keywordColor,
                comment = m_commentColor,
                str = m_stringColor,
                method = m_methodColor,
                type = m_typeColor,
                number = m_numberColor,
                iface = m_interfaceColor,
                member = m_memberColor,
                dlg = m_delegateColor
            };

            File.WriteAllText(path, JsonUtility.ToJson(p, true));
            EditorPrefs.SetString(PREF_LAST_PROFILE, path);
        }

        private void LoadProfile()
        {
            string path = EditorUtility.OpenFilePanel(
                "Load Color Profile",
                Path.GetDirectoryName(EditorPrefs.GetString(PREF_LAST_PROFILE, "Assets")),
                PROFILE_EXT);

            if (string.IsNullOrEmpty(path)) return;

            string json = File.ReadAllText(path);
            var p = JsonUtility.FromJson<PluginColorProfile>(json);
            if (p == null)
            {
                EditorUtility.DisplayDialog("Error", "Invalid profile file.", "OK");
                return;
            }

            m_keywordColor = p.keyword;
            m_commentColor = p.comment;
            m_stringColor = p.str;
            m_methodColor = p.method;
            m_typeColor = p.type;
            m_numberColor = p.number;
            m_interfaceColor = p.iface;
            m_memberColor = p.member;
            m_delegateColor = p.dlg;

            EditorPrefs.SetString(PREF_LAST_PROFILE, path);

            // persist into EditorPrefs so the palette survives reload
            SaveColor(PREF_KEYWORD, m_keywordColor);
            SaveColor(PREF_COMMENT, m_commentColor);
            SaveColor(PREF_STRING, m_stringColor);
            SaveColor(PREF_METHOD, m_methodColor);
            SaveColor(PREF_TYPE, m_typeColor);
            SaveColor(PREF_NUMBER, m_numberColor);
            SaveColor(PREF_INTERFACE, m_interfaceColor);
            SaveColor(PREF_MEMBER, m_memberColor);
            SaveColor(PREF_DELEGATE, m_delegateColor);

            UpdateHighlightedCode();
            Repaint();
        }

        private static void TryLoadColor(string key, ref Color c)
        {
            if (EditorPrefs.HasKey(key) &&
                ColorUtility.TryParseHtmlString("#" + EditorPrefs.GetString(key), out var tmp))
                c = tmp;
        }

        private static void SaveColor(string key, Color c) =>
            EditorPrefs.SetString(key, ColorUtility.ToHtmlStringRGB(c));

        private void EnsureStyles()
        {
            if (m_stylesReady) return;

            // Might still be null for the very first Layout event
            if (EditorStyles.textArea == null) return;

            m_highlightStyle = new GUIStyle(EditorStyles.textArea)
            {
                richText = true,
                wordWrap = true
            };

            m_editStyle = new GUIStyle(EditorStyles.textArea)
            {
                richText = false,
                wordWrap = true
            };



            m_lineNumberStyle = new GUIStyle(EditorStyles.miniLabel)
            {
                alignment = TextAnchor.UpperRight,
                richText = false,
                wordWrap = false
            };
            m_lineNumberStyle.normal.textColor = new Color(0.6f, 0.6f, 0.6f);
            m_lineNumberStyle.padding = new RectOffset(0, 6, 2, 0);

            var clearGlyph = new Color(1, 1, 1, 0);
            m_editStyle.normal.textColor = clearGlyph;
            m_editStyle.focused.textColor = clearGlyph;
            m_editStyle.hover.textColor = clearGlyph;
            m_editStyle.active.textColor = clearGlyph;

            UnityEngine.GUI.skin.settings.cursorColor = Color.white;
            UnityEngine.GUI.skin.settings.selectionColor = new Color(0.24f, 0.48f, 0.9f, .45f);

            var clear = new Color(0, 0, 0, 0);
            var clearTex = new Texture2D(1, 1) { hideFlags = HideFlags.HideAndDontSave };
            clearTex.SetPixel(0, 0, clear);
            clearTex.Apply();
            m_editStyle.normal.background =
            m_editStyle.hover.background =
            m_editStyle.focused.background =
            m_editStyle.active.background = clearTex;

            // Make the syntax overlay transparent too the custom panel background shows through
            m_highlightStyle.normal.background =
            m_highlightStyle.hover.background =
            m_highlightStyle.focused.background =
            m_highlightStyle.active.background = clearTex;


            m_descriptionStyle = new GUIStyle(EditorStyles.textArea)
            {
                wordWrap = true,
                padding = new RectOffset(2, 2, 2, 2)
            };

            m_stylesReady = true;
        }

        private static Texture2D s_CodeBgTex;
        private static GUIStyle s_CodeBoxStyle;

        private static void EnsureCodeBoxStyle()
        {
            if (s_CodeBgTex == null)
            {
                s_CodeBgTex = new Texture2D(1, 1);
                s_CodeBgTex.SetPixel(0, 0, new Color32(0x18, 0x18, 0x18, 0xFF)); // same vibe as preview
                s_CodeBgTex.Apply();
                s_CodeBgTex.hideFlags = HideFlags.HideAndDontSave;
            }

            if (s_CodeBoxStyle == null)
            {
                s_CodeBoxStyle = new GUIStyle("box");
                s_CodeBoxStyle.normal.background = s_CodeBgTex;
                s_CodeBoxStyle.margin = new RectOffset(0, 0, 0, 0);
                s_CodeBoxStyle.padding = new RectOffset(8, 8, 8, 8);
            }
        }



        private void UpdateHighlightedCode() => m_highlightedCode = ColorizeCode(m_code);

        private void UpdatePatchHighlightedCode() => m_patchHighlightedCode = ColorizeCode(m_patchCode);

        private void UpdatePatchSearchHighlightedCode() => m_patchSearchHighlightedCode = ColorizeCode(m_patchSearchCode);

        private bool ShouldShowLineNumbers()
        {
            if (string.IsNullOrEmpty(m_selectedPath)) return false;
            return string.Equals(Path.GetExtension(m_selectedPath), ".cs", StringComparison.OrdinalIgnoreCase);
        }

        private float CalculateLineNumberWidth()
        {
            return CalculateLineNumberWidth(CountLines(m_code));
        }

        private float CalculateLineNumberWidth(int maxLineNumber)
        {
            int digits = Mathf.Max(2, (int)Mathf.Floor(Mathf.Log10(Mathf.Max(1, maxLineNumber))) + 1);
            string sample = new string('9', digits);
            float width = m_lineNumberStyle.CalcSize(new GUIContent(sample)).x;
            return Mathf.Max(36f, width + 10f);
        }

        private static int CountLines(string text)
        {
            if (string.IsNullOrEmpty(text)) return 1;
            int count = 1;
            for (int i = 0; i < text.Length; i++)
                if (text[i] == '\n') count++;
            return count;
        }

        private void DrawLineNumbers(Rect gutterRect, Rect codeRect, string[] rawLines = null, string[] highlightedLines = null, int startDisplayLine = 1)
        {
            EditorGUI.DrawRect(gutterRect, new Color(0, 0, 0, 0.2f));

            // Use the highlighted lines (with rich text) to measure height so the gutter stays
            // aligned with the rendered syntax-highlighted overlay.
            string[] sourceRaw = rawLines ?? m_code.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
            string highlighted = highlightedLines == null ? m_highlightedCode ?? m_code : string.Join("\n", highlightedLines);
            string[] sourceHighlighted = highlightedLines ?? highlighted.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);

            int lineCount = sourceRaw.Length;
            if (sourceHighlighted.Length != lineCount)
            {
                // In case of a mismatch (shouldn’t happen), fall back to the raw text to keep counts aligned.
                sourceHighlighted = sourceRaw;
            }

            float textWidth = Mathf.Max(10f, codeRect.width - m_highlightStyle.padding.horizontal);

            // Respect the same vertical padding the text area uses so the first number
            // lines up with the first line of text instead of the control's top edge.
            float y = gutterRect.y + m_highlightStyle.padding.top;

            for (int i = 0; i < lineCount; i++)
            {
                string measure = string.IsNullOrEmpty(sourceHighlighted[i]) ? " " : sourceHighlighted[i];
                float rawHeight = m_highlightStyle.CalcHeight(new GUIContent(measure), textWidth);
                float height = Mathf.Max(0f, rawHeight - m_highlightStyle.padding.vertical);
                Rect labelRect = new Rect(gutterRect.x, y, gutterRect.width - 2f, height);
                UnityEngine.GUI.Label(labelRect, (startDisplayLine + i).ToString(), m_lineNumberStyle);
                y += height;
            }

            // Account for the style's bottom padding so the gutter matches the rendered block height.
            y += m_highlightStyle.padding.bottom;
        }

        private float CalculateCodeEditorHeight(string highlightedText, float availableWidth, float maxHeight)
        {
            float neededHeight = m_highlightStyle.CalcHeight(new GUIContent(highlightedText), Mathf.Max(10f, availableWidth));
            float viewHeight = Mathf.Min(maxHeight, neededHeight + EditorGUIUtility.singleLineHeight);
            return Mathf.Max(viewHeight, EditorGUIUtility.singleLineHeight * 2f);
        }

        private string DrawCodeEditor(
            GUIContent label,
            string text,
            ref string highlighted,
            ref Vector2 scroll,
            string controlName,
            float maxHeight,
            out float usedHeight)
        {
            EditorGUILayout.LabelField(label);
            EnsureStyles();
            EnsureCodeBoxStyle();

            string raw = text ?? string.Empty;

            if (string.IsNullOrEmpty(highlighted) && !string.IsNullOrEmpty(raw))
                highlighted = ColorizeCode(raw);

            string highlightedText = highlighted ?? string.Empty;

            string[] rawLines = raw.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
            string[] highlightedLines = highlightedText.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);

            float lineNumberWidth = CalculateLineNumberWidth(rawLines.Length);

            // IMPORTANT: reserve the full height given (this makes the big block exist like in your screenshot)
            float viewHeight = Mathf.Max(maxHeight, EditorGUIUtility.singleLineHeight * 3f);
            usedHeight = viewHeight;

            // Full-height panel (like preview block)
            Rect outer = GUILayoutUtility.GetRect(GUIContent.none, s_CodeBoxStyle, GUILayout.Height(viewHeight), GUILayout.ExpandWidth(true));
            if (Event.current.type == EventType.Repaint)
                UnityEngine.GUI.Box(outer, GUIContent.none, s_CodeBoxStyle);

            // Inner drawing area (panel padding)
            Rect inner = new Rect(
                outer.x + s_CodeBoxStyle.padding.left,
                outer.y + s_CodeBoxStyle.padding.top,
                outer.width - s_CodeBoxStyle.padding.horizontal,
                outer.height - s_CodeBoxStyle.padding.vertical
            );

            // Measure content height against the actual inner width
            float codeWidth = Mathf.Max(10f, inner.width - lineNumberWidth);
            float textWidth = Mathf.Max(10f, codeWidth - m_highlightStyle.padding.horizontal);

            float neededHeight = m_highlightStyle.CalcHeight(new GUIContent(highlightedText), textWidth);
            float contentHeight = Mathf.Max(inner.height, neededHeight);

            const float eps = 0.75f;
            bool needsV = neededHeight > inner.height + eps;

            // Avoid phantom horizontal scrollbar caused by vertical scrollbar stealing width
            float sbW = (UnityEngine.GUI.skin.verticalScrollbar != null && UnityEngine.GUI.skin.verticalScrollbar.fixedWidth > 0f)
                ? UnityEngine.GUI.skin.verticalScrollbar.fixedWidth
                : 16f;

            float contentWidth = needsV ? Mathf.Max(10f, inner.width - sbW - 2f) : inner.width;

            if (!needsV)
            {
                // No scroll view => no scrollbars possible
                scroll = Vector2.zero;

                Rect gutterRect = new Rect(inner.x, inner.y, lineNumberWidth, inner.height);
                Rect codeRect = new Rect(inner.x + lineNumberWidth, inner.y, Mathf.Max(10f, inner.width - lineNumberWidth), inner.height);

                DrawLineNumbers(gutterRect, codeRect, rawLines, highlightedLines);

                if (Event.current.type == EventType.Repaint)
                    UnityEngine.GUI.Label(codeRect, highlightedText, m_highlightStyle);

                UnityEngine.GUI.SetNextControlName(controlName);
                EditorGUI.BeginChangeCheck();
                string newCode = EditorGUI.TextArea(codeRect, raw, m_editStyle);
                if (EditorGUI.EndChangeCheck())
                {
                    highlighted = ColorizeCode(newCode);
                    return newCode;
                }

                return raw;
            }

            // Scroll view only when needed
            Rect viewRect = new Rect(0f, 0f, contentWidth, contentHeight);
            scroll = UnityEngine.GUI.BeginScrollView(inner, scroll, viewRect, false, false);

            Rect scrollContent = new Rect(0f, 0f, contentWidth, contentHeight);
            Rect scrollGutter = new Rect(scrollContent.x, scrollContent.y, lineNumberWidth, scrollContent.height);
            Rect scrollCode = new Rect(scrollContent.x + lineNumberWidth, scrollContent.y, Mathf.Max(10f, scrollContent.width - lineNumberWidth), scrollContent.height);

            DrawLineNumbers(scrollGutter, scrollCode, rawLines, highlightedLines);

            if (Event.current.type == EventType.Repaint)
                UnityEngine.GUI.Label(scrollCode, highlightedText, m_highlightStyle);

            UnityEngine.GUI.SetNextControlName(controlName);
            EditorGUI.BeginChangeCheck();
            string edited = EditorGUI.TextArea(scrollCode, raw, m_editStyle);
            bool changed = EditorGUI.EndChangeCheck();

            UnityEngine.GUI.EndScrollView();

            if (changed)
            {
                highlighted = ColorizeCode(edited);
                return edited;
            }

            return raw;
        }




        private string ColorizeCode(string source)
        {
            if (string.IsNullOrEmpty(source))
                return string.Empty;

            /* ----------------------------------------------------------------
             * 1  HTML colours
             * ----------------------------------------------------------------*/
            string commentHex = ColorUtility.ToHtmlStringRGB(m_commentColor);
            string stringHex = ColorUtility.ToHtmlStringRGB(m_stringColor);
            string numberHex = ColorUtility.ToHtmlStringRGB(m_numberColor);
            string methodHex = ColorUtility.ToHtmlStringRGB(m_methodColor);
            string typeHex = ColorUtility.ToHtmlStringRGB(m_typeColor);
            string keywordHex = ColorUtility.ToHtmlStringRGB(m_keywordColor);
            string interfaceHex = ColorUtility.ToHtmlStringRGB(m_interfaceColor);
            string memberHex = ColorUtility.ToHtmlStringRGB(m_memberColor);
            string delegateHex = ColorUtility.ToHtmlStringRGB(m_delegateColor);

            /* ----------------------------------------------------------------
             * 2  escape &, <, > so C# code shows literally
             * ----------------------------------------------------------------*/
            string escaped = EscapeForRichText(source);

            /* ----------------------------------------------------------------
             * 3  Remove strings & comments → single-char placeholders
             * ----------------------------------------------------------------*/
            var placeholders = new List<string>();
            string TakeOut(Match m, string hex)
            {
                placeholders.Add($"<color=#{hex}>{m.Value}</color>");
                return ((char)(0xE000 + placeholders.Count - 1)).ToString(); // U+E000.. tokens
            }

            // strings
            escaped = Regex.Replace(
                escaped,
                "\"(?:\\\\.|[^\"])*?\"",
                m => TakeOut(m, stringHex),
                RegexOptions.Singleline);

            // single-line comments
            escaped = Regex.Replace(
                escaped,
                @"//.*?$",
                m => TakeOut(m, commentHex),
                RegexOptions.Multiline);

            // multi-line comments
            escaped = Regex.Replace(
                escaped,
                @"/\*.*?\*/",
                m => TakeOut(m, commentHex),
                RegexOptions.Singleline);

            /* ----------------------------------------------------------------
             * 4  Colour passes – order matters (numbers → types → interfaces →
             *    members → delegates → methods → keywords)
             * ----------------------------------------------------------------*/

            // numbers
            escaped = Regex.Replace(
                escaped,
                @"(?<![#<])\b(0[xX][0-9A-Fa-f]+|0[bB][01]+|\d+(\.\d+)?([fFdDmM])?)\b",
                $"<color=#{numberHex}>$1</color>");

            // Types (class / struct / enum)  – capitalised, NOT followed by '('
            escaped = Regex.Replace(
                escaped,
                @"(?<![#<])\b([A-Z][A-Za-z0-9_]*)\b(?!\s*\()",
                $"<color=#{typeHex}>$1</color>");

            // Interfaces  – I followed by capital letter
            escaped = Regex.Replace(
                escaped,
                @"(?<![#<])\bI[A-Z][A-Za-z0-9_]*\b",
                $"<color=#{interfaceHex}>$0</color>");

            // Members / fields / properties / events  – identifier after a '.' and NOT a method call
            escaped = Regex.Replace(
                escaped,
                @"(?<![#<])(?<=\.)\b([A-Za-z_][A-Za-z0-9_]*)\b(?!\s*\()",
                $"<color=#{memberHex}>$1</color>");

            // Delegates / method-group refs  – identifier before '+=' / '-=' / ';'
            escaped = Regex.Replace(
                escaped,
                @"(?<![#<])\b([A-Za-z_][A-Za-z0-9_]*)\b(?=\s*(\+=|\-=|;))",
                $"<color=#{delegateHex}>$1</color>");

            // Methods (calls or decl) – identifier followed by '('
            escaped = Regex.Replace(
                escaped,
                @"(?<![#<])\b([A-Za-z_][A-Za-z0-9_]*)\b(?=\s*\()",
                $"<color=#{methodHex}>$1</color>");

            // Keywords
            foreach (string kw in s_keywords)
            {
                escaped = Regex.Replace(
                    escaped,
                    $@"(?<![#<])\b{Regex.Escape(kw)}\b",
                    $"<color=#{keywordHex}>{kw}</color>");
            }

            /* ----------------------------------------------------------------
             * 5  Restore string / comment placeholders
             * ----------------------------------------------------------------*/
            for (int i = 0; i < placeholders.Count; i++)
            {
                char token = (char)(0xE000 + i);
                escaped = escaped.Replace(token.ToString(), placeholders[i]);
            }

            return escaped;
        }

        /// <summary>
        /// Escapes characters that would break Unity rich-text markup while still
        /// rendering the real &lt; and &gt; glyphs in the overlay.
        /// A zero-width space (U+200B) after '&lt;' and before '&gt;' keeps the
        /// parser from treating them as tags.
        /// </summary>
        private static string EscapeForRichText(string s)
        {
            if (string.IsNullOrEmpty(s)) return string.Empty;

            const char ZWSP = '\u200B';   // zero-width space

            var sb = new StringBuilder(s.Length);
            foreach (char c in s)
            {
                switch (c)
                {
                    case '&': sb.Append("&amp;"); break;          // keep colour tags safe
                    case '<': sb.Append('<').Append(ZWSP); break;          // real '<'
                    case '>': sb.Append(ZWSP).Append('>'); break;          // real '>'
                    default: sb.Append(c); break;
                }
            }
            return sb.ToString();
        }

        private void DrawColorSettings()
        {
            const float CELL_W = 165f;

            Color[] vals =
            {
                m_keywordColor, m_commentColor, m_stringColor,
                m_methodColor,  m_typeColor,    m_numberColor,
                m_interfaceColor, m_memberColor, m_delegateColor
            };

            /* grid width adapts to window width */
            float avail = position.width - 20f;
            int cols = Mathf.Max(1, Mathf.FloorToInt(avail / CELL_W));

            EditorGUI.BeginChangeCheck();

            for (int i = 0; i < vals.Length; i++)
            {
                if (i % cols == 0)
                    EditorGUILayout.BeginHorizontal();

                vals[i] = EditorGUILayout.ColorField(
                    vals[i],
                    GUILayout.Width(CELL_W),
                    GUILayout.Height(COLOR_SWATCH_H));

                if (i % cols == cols - 1)
                    EditorGUILayout.EndHorizontal();
            }

            /* close the last row if it wasn't filled */
            if (vals.Length % cols != 0)
                EditorGUILayout.EndHorizontal();
            if (EditorGUI.EndChangeCheck())
            {
                /* write back + persist */
                m_keywordColor = vals[0]; m_commentColor = vals[1]; m_stringColor = vals[2];
                m_methodColor = vals[3]; m_typeColor = vals[4]; m_numberColor = vals[5];
                m_interfaceColor = vals[6]; m_memberColor = vals[7]; m_delegateColor = vals[8];

                SaveColor(PREF_KEYWORD, m_keywordColor);
                SaveColor(PREF_COMMENT, m_commentColor);
                SaveColor(PREF_STRING, m_stringColor);
                SaveColor(PREF_METHOD, m_methodColor);
                SaveColor(PREF_TYPE, m_typeColor);
                SaveColor(PREF_NUMBER, m_numberColor);
                SaveColor(PREF_INTERFACE, m_interfaceColor);
                SaveColor(PREF_MEMBER, m_memberColor);
                SaveColor(PREF_DELEGATE, m_delegateColor);

                UpdateHighlightedCode();
                UpdatePatchHighlightedCode();
                Repaint();
            }
        }
    }
}
