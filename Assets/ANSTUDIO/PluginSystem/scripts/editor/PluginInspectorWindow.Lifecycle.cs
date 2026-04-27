using System;
using System.Linq;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;

namespace PLAYERTWO.ARPGProject
{
    public partial class PluginInspectorWindow
    {
        [MenuItem("Tools/ANSTUDIO/Plugin System/Plugin System Window")]
        public static void Open()
        {
            GetWindow<PluginInspectorWindow>("Plugin System Window");
        }

        [MenuItem("Tools/ANSTUDIO/Plugin System/Show Import Dependency Reminder", false, 1)]
        private static void ToggleDependencyReminder()
        {
            PluginDependencyReminderWindow.Enabled = !PluginDependencyReminderWindow.Enabled;
        }

        [MenuItem("Tools/ANSTUDIO/Plugin System/Show Import Dependency Reminder", true)]
        private static bool ToggleDependencyReminderValidate()
        {
            Menu.SetChecked("Tools/ANSTUDIO/Plugin System/Show Import Dependency Reminder", PluginDependencyReminderWindow.Enabled);
            return true;
        }

        private void OnEnable()
        {
            LoadPluginStates();
            EnsureToggleTextures();
            Refresh();

            // ── create tree on first enable ───────────────────────────
            if (m_treeState == null)
#if UNITY_6000_2_OR_NEWER
                m_treeState = new TreeViewState<int>();
#else
                m_treeState = new TreeViewState();
#endif
            m_treeView = new PluginTreeView(m_treeState, this);
            // Default to a collapsed hierarchy (only top-level plug-ins visible).
            m_treeView.CollapseAll();
            m_treeView.SetExpanded(0, true); // keep the hidden root expanded
            m_treeView.Reload();

            m_search = new SearchField();
            m_search.downOrUpArrowKeyPressed += m_treeView.SetFocusAndEnsureSelectedItem;
            m_treeView.SetSelection(Array.Empty<int>());
            if (m_treeView != null) { m_treeView.Reload(); m_treeView.SetSelection(Array.Empty<int>()); }

            // restore saved colours (keep defaults if key absent)
            TryLoadColor(PREF_KEYWORD, ref m_keywordColor);
            TryLoadColor(PREF_COMMENT, ref m_commentColor);
            TryLoadColor(PREF_STRING, ref m_stringColor);
            TryLoadColor(PREF_METHOD, ref m_methodColor);
            TryLoadColor(PREF_TYPE, ref m_typeColor);
            TryLoadColor(PREF_NUMBER, ref m_numberColor);
            TryLoadColor(PREF_INTERFACE, ref m_interfaceColor);
            TryLoadColor(PREF_MEMBER, ref m_memberColor);
            TryLoadColor(PREF_DELEGATE, ref m_delegateColor);
            //EnsureStyles();
            // restore (optional)
            m_coreFolderRoot = EditorPrefs.GetString("PluginInspector.CoreRoot", "Assets/PLAYER TWO/");
            if (m_search == null) m_search = new SearchField();
            ScanAndSetCoreFiles();
            LoadLineOffsetIndex();
        }

        private void OnDisable()
        {
            // Persist whatever plugin was active
            if (!string.IsNullOrEmpty(m_activePluginRoot))
                SavePendingFor(m_activePluginRoot);

            if (m_toggleGreenCircle != null) DestroyImmediate(m_toggleGreenCircle);
            if (m_toggleRedCircle != null) DestroyImmediate(m_toggleRedCircle);
            m_toggleGreenCircle = null;
            m_toggleRedCircle = null;
        }

        private void Refresh()
        {
            if (m_treeView != null) m_treeView.Reload();
            m_pluginIconCache.Clear();
            Repaint();
        }

        [InitializeOnLoadMethod]
        private static void InitAssemblyReloadHook()
        {
            AssemblyReloadEvents.beforeAssemblyReload += () =>
            {
                // Find an open window instance and ask it to save
                var win = Resources.FindObjectsOfTypeAll<PluginInspectorWindow>().FirstOrDefault();
                if (win != null && !string.IsNullOrEmpty(win.m_activePluginRoot))
                    win.SavePendingFor(win.m_activePluginRoot);
            };
        }
    }
}
