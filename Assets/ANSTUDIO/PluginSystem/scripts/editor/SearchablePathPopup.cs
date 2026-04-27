using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

internal sealed class SearchablePathPopup : PopupWindowContent
{
    private readonly string _title;
    private readonly List<string> _allPaths;   // "Assets/..."
    private readonly List<string> _labels;     // relative labels we show
    private readonly Action<int> _onSelect;
    private int _selectedIndex;

    private string _search = "";
    private Vector2 _scroll;
    private int _hover = -1;
    private bool _focusSearchOnOpen = true;

    public SearchablePathPopup(string title, IList<string> allAssetPaths, IList<string> shownLabels, int selectedIndex, Action<int> onSelect)
    {
        _title = string.IsNullOrEmpty(title) ? "Select" : title;
        _allPaths = new List<string>(allAssetPaths ?? Array.Empty<string>());
        _labels = new List<string>(shownLabels ?? Array.Empty<string>());
        _selectedIndex = Mathf.Clamp(selectedIndex, 0, _labels.Count - 1);
        _onSelect = onSelect ?? (_ => { });
    }

    public override Vector2 GetWindowSize()
    {
        // Width enough to read filenames; height capped
        float w = Mathf.Max(420f, Mathf.Min(900f, EditorGUIUtility.currentViewWidth - 80f));
        return new Vector2(w, 320f);
    }

    public override void OnOpen() { }

    public override void OnGUI(Rect rect)
    {
        // Title (with tooltip)
        GUILayout.Label(new GUIContent(_title, "Pick a core file. Type to filter, use ↑/↓ to move, Enter to select, Esc to close."), EditorStyles.boldLabel);

        // Search (toolbar style + help icon tooltip)
        GUI.SetNextControlName("SearchCorePaths");
        // Draw a styled search field with a help icon on the right
        Rect searchR = EditorGUILayout.GetControlRect(false, EditorGUIUtility.singleLineHeight);
        _search = EditorGUI.TextField(searchR, _search, EditorStyles.toolbarSearchField);
        if (_focusSearchOnOpen)
        {
            _focusSearchOnOpen = false;
            EditorGUI.FocusTextInControl("SearchCorePaths");
        }
        var help = EditorGUIUtility.IconContent("_Help");
        help.tooltip = "Filter by filename or relative path. Esc closes, Enter selects the highlighted row.";
        var helpRect = new Rect(searchR.xMax - 18, searchR.y + 2, 16, 16);
        GUI.Label(helpRect, help);

        // Filter
        var indices = FilterIndices(_labels, _search);
        int count = indices.Count;

        // Keyboard
        var e = Event.current;
        if (e.type == EventType.KeyDown)
        {
            if (e.keyCode == KeyCode.DownArrow) { _hover = Mathf.Clamp(_hover + 1, 0, count - 1); e.Use(); }
            else if (e.keyCode == KeyCode.UpArrow) { _hover = Mathf.Clamp(_hover - 1, 0, count - 1); e.Use(); }
            else if (e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter)
            {
                if (_hover >= 0 && _hover < count) Select(indices[_hover]);
                else if (count > 0) Select(indices[0]);
                e.Use();
            }
            else if (e.keyCode == KeyCode.Escape)
            {
                editorWindow.Close();
                e.Use();
            }
        }

        // List
        using (var scroll = new EditorGUILayout.ScrollViewScope(_scroll))
        {
            _scroll = scroll.scrollPosition;
            for (int i = 0; i < count; i++)
            {
                int idx = indices[i];
                bool isSel = (idx == _selectedIndex);
                bool isHover = (i == _hover);

                Rect row = EditorGUILayout.GetControlRect(false, EditorGUIUtility.singleLineHeight);
                if (row.Contains(Event.current.mousePosition))
                {
                    if (_hover != i) { _hover = i; editorWindow.Repaint(); }
                    if (Event.current.type == EventType.MouseDown && Event.current.button == 0)
                    {
                        Select(idx);
                        Event.current.Use();
                        return;
                    }
                }

                // Draw label (truncate long)
                string label = _labels[idx];
                string tooltip = (_allPaths != null && idx >= 0 && idx < _allPaths.Count) ? _allPaths[idx] : label;
                GUI.Label(row, new GUIContent(label, tooltip), isSel ? EditorStyles.boldLabel : EditorStyles.label);
            }
        }

        // Info
        EditorGUILayout.Space(4);
        GUILayout.Label(count == 0 ? new GUIContent("No matches.", "No items match the current filter.")
                                   : new GUIContent($"{count} file(s)", "Files matching your filter."),
                        EditorStyles.miniLabel);
    }

    private void Select(int idx)
    {
        _selectedIndex = idx;
        _onSelect?.Invoke(idx);
        editorWindow.Close();
    }

    private static List<int> FilterIndices(List<string> items, string search)
    {
        if (string.IsNullOrEmpty(search)) return Enumerable.Range(0, items.Count).ToList();
        string s = search.Trim().ToLowerInvariant();
        // Simple contains; could be improved with fuzzy
        return Enumerable.Range(0, items.Count)
            .Where(i => items[i].ToLowerInvariant().Contains(s))
            .ToList();
    }
}
