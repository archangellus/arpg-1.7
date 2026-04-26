using System.IO;
using UnityEditor;
using UnityEngine;

namespace PLAYERTWO.ARPGProject
{
    internal sealed class PluginDependencyReminderWindow : EditorWindow
    {
        internal const string PreferenceKey = "PluginInspector.DependencyReminderEnabled";

        private string m_pluginName;
        private string m_pluginRoot;

        internal static bool Enabled
        {
            get => EditorPrefs.GetBool(PreferenceKey, true);
            set => EditorPrefs.SetBool(PreferenceKey, value);
        }

        public static void Show(string pluginName, string pluginRoot)
        {
            if (!Enabled)
                return;

            var window = CreateInstance<PluginDependencyReminderWindow>();
            window.titleContent = new GUIContent("Manifest Reminder");
            window.m_pluginName = pluginName;
            window.m_pluginRoot = pluginRoot;
            window.minSize = new Vector2(360f, 200f);
            window.ShowUtility();
            window.Focus();
        }

        private void OnGUI()
        {
            GUILayout.Label("Import Manifest Dependencies", EditorStyles.boldLabel);

            EditorGUILayout.HelpBox(
                $"Before working with \"{m_pluginName}\", If plugins have manifest dependecies defined in plugin.patches.json don't forget to apply them.",
                MessageType.Info);

            if (!string.IsNullOrEmpty(m_pluginRoot))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.Label("Plug-in Folder", GUILayout.Width(110f));
                    EditorGUILayout.SelectableLabel(m_pluginRoot, EditorStyles.textField, GUILayout.Height(EditorGUIUtility.singleLineHeight));
                }
            }

            GUILayout.Space(8f);

            bool enabled = Enabled;
            bool updated = EditorGUILayout.ToggleLeft("Show this reminder after importing plug-ins", enabled);
            if (updated != enabled)
            {
                Enabled = updated;
            }

            GUILayout.FlexibleSpace();

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Close"))
                {
                    Close();
                }
            }
        }
    }
}
