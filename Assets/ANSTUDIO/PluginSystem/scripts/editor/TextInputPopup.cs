using System;
using UnityEditor;
using UnityEngine;

namespace PLAYERTWO.ARPGProject
{
    /// <summary>
    /// Simple text input popup used for creating and renaming plugins.
    /// </summary>
    public class TextInputPopup : EditorWindow
    {
        private string m_label;
        private string m_text;
        private Action<string> m_onClose;
        private Vector2 m_scroll;


        public static void Show(string title, string label, string defaultText, Action<string> onClose)
        {
            var window = CreateInstance<TextInputPopup>();
            window.titleContent = new GUIContent(title);
            window.m_label = label;
            window.m_text = defaultText;
            window.m_onClose = onClose;
            window.position = new Rect(Screen.width / 2f, Screen.height / 2f, 300, 80);
            window.ShowUtility();
        }

        private void OnGUI()
        {
            using (var sv = new EditorGUILayout.ScrollViewScope(m_scroll))
            {
                m_scroll = sv.scrollPosition;

                GUILayout.Label(new GUIContent(m_label, "Enter a value, then press OK to confirm or Cancel to close without changes."));
                m_text = EditorGUILayout.TextField(new GUIContent(" ", "Text value"), m_text);

                GUILayout.Space(10);
                GUILayout.BeginHorizontal();
                if (GUILayout.Button(new GUIContent("OK", "Confirm and apply this value")))
                {
                    m_onClose?.Invoke(m_text);
                    Close();
                }
                if (GUILayout.Button(new GUIContent("Cancel", "Close without applying changes")))
                {
                    Close();
                }
                GUILayout.EndHorizontal();
            }
        }

    }
}