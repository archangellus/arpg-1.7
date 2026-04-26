using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace PLAYERTWO.ARPGProject
{
    public partial class PluginInspectorWindow
    {
        [Serializable]
        private class PluginState
        {
            public string plugin;
            public bool enabled = true;
        }

        [Serializable]
        private class PluginStateCollection
        {
            public System.Collections.Generic.List<PluginState> states = new();
        }

        private string PluginStatesPath => Path.Combine(PluginsPath, PluginStatesFile);

        private void LoadPluginStates()
        {
            m_pluginStates.Clear();

            try
            {
                if (!File.Exists(PluginStatesPath))
                    return;

                var data = JsonUtility.FromJson<PluginStateCollection>(File.ReadAllText(PluginStatesPath));
                if (data?.states == null)
                    return;

                foreach (var state in data.states)
                {
                    if (!string.IsNullOrEmpty(state?.plugin))
                        m_pluginStates[state.plugin] = state;
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[PluginInspector] Failed to load plugin states: {ex.Message}");
            }
        }

        private void SavePluginStates()
        {
            try
            {
                Directory.CreateDirectory(PluginsPath);
                var data = new PluginStateCollection { states = m_pluginStates.Values.ToList() };
                File.WriteAllText(PluginStatesPath, JsonUtility.ToJson(data, true));
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[PluginInspector] Failed to save plugin states: {ex.Message}");
            }
        }

        private PluginState GetPluginStateForPath(string pluginPath)
        {
            if (string.IsNullOrEmpty(pluginPath))
                return new PluginState { plugin = string.Empty, enabled = true };

            string pluginName = Path.GetFileName(pluginPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
            if (string.IsNullOrEmpty(pluginName))
                pluginName = pluginPath;

            if (!m_pluginStates.TryGetValue(pluginName, out var state))
            {
                state = new PluginState { plugin = pluginName, enabled = true };
                m_pluginStates[pluginName] = state;
            }

            return state;
        }

        private bool IsPluginRoot(string path)
        {
            if (string.IsNullOrEmpty(path) || !Directory.Exists(path))
                return false;

            string parent = Directory.GetParent(path)?.FullName;
            string pluginsRoot = Path.GetFullPath(PluginsPath);

            return string.Equals(parent, pluginsRoot, StringComparison.OrdinalIgnoreCase);
        }

        private bool IsPluginEnabled(string path) => GetPluginStateForPath(path).enabled;

        private void SetPluginEnabled(string path, bool enabled)
        {
            var state = GetPluginStateForPath(path);
            if (state.enabled == enabled)
                return;

            state.enabled = enabled;
            SavePluginStates();
            PluginManager.SetPluginEnabled(state.plugin, enabled);
            Repaint();
        }


        private void EnsureToggleTextures()
        {
            if (m_toggleGreenCircle == null)
                m_toggleGreenCircle = CreateCircleTexture(17, new Color(0.15f, 1f, 0.15f), Color.black);

            if (m_toggleRedCircle == null)
                m_toggleRedCircle = CreateCircleTexture(17, new Color(1f, 0.2f, 0.2f), Color.black);
        }

        private static Texture2D CreateCircleTexture(int size, Color fillColor, Color borderColor)
        {
            var tex = new Texture2D(size, size, TextureFormat.ARGB32, false)
            {
                hideFlags = HideFlags.HideAndDontSave
            };

            float radius = size / 2f - 1f;
            Vector2 center = new Vector2(size / 2f, size / 2f);

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    Vector2 p = new Vector2(x + 0.5f, y + 0.5f);
                    float dist = Vector2.Distance(p, center);

                    if (dist <= radius)
                    {
                        tex.SetPixel(x, y, dist >= radius - 1f ? borderColor : fillColor);
                    }
                    else
                    {
                        tex.SetPixel(x, y, Color.clear);
                    }
                }
            }

            tex.Apply();
            return tex;
        }

        private bool DrawInlineToggle(Rect rect, bool value)
        {
            EnsureToggleTextures();

            bool newValue = value;
            EditorGUI.DrawRect(rect, Color.black);
            Rect inner = new Rect(rect.x + 1, rect.y + 1, rect.width - 2, rect.height - 2);
            EditorGUI.DrawRect(inner, Color.white);

            float circleSize = rect.height - 4f;
            Rect circleRect;

            var labelStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 9
            };
            labelStyle.normal.textColor = Color.black;

            if (value)
            {
                circleRect = new Rect(rect.xMax - circleSize - 2, rect.y + 2, circleSize, circleSize);
                if (m_toggleGreenCircle != null)
                    UnityEngine.GUI.DrawTexture(circleRect, m_toggleGreenCircle);

                Rect labelRect = new Rect(rect.x + 2, rect.y, rect.width - circleSize - 6, rect.height);
                UnityEngine.GUI.Label(labelRect, "ON", labelStyle);
            }
            else
            {
                circleRect = new Rect(rect.x + 2, rect.y + 2, circleSize, circleSize);
                if (m_toggleRedCircle != null)
                    UnityEngine.GUI.DrawTexture(circleRect, m_toggleRedCircle);

                Rect labelRect = new Rect(rect.x + circleSize + 4, rect.y, rect.width - circleSize - 6, rect.height);
                UnityEngine.GUI.Label(labelRect, "OFF", labelStyle);
            }

            if (UnityEngine.GUI.Button(rect, GUIContent.none, GUIStyle.none))
            {
                newValue = !value;
                UnityEngine.GUI.changed = true;
                Repaint();
            }

            return newValue;
        }
    }
}
