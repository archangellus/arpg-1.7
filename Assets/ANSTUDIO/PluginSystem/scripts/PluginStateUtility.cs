using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace PLAYERTWO.ARPGProject
{
    /// <summary>
    /// Shared helper for loading and saving plug-in enablement flags.
    /// </summary>
    public static class PluginStateUtility
    {
        public const string PluginStatesRelativePath = "Assets/ANSTUDIO/PluginSystem/plugins/plugin_states.json";

        [Serializable]
        public class PluginState
        {
            public string plugin;
            public bool enabled = true;
        }

        [Serializable]
        public class PluginStateCollection
        {
            public List<PluginState> states = new();
        }

        public static string GetAbsoluteStatePath()
        {
#if UNITY_EDITOR
            return Path.GetFullPath(PluginStatesRelativePath);
#else
            return Path.Combine(Application.dataPath, "ANSTUDIO/PluginSystem/plugins", "plugin_states.json");
#endif
        }

        public static PluginStateCollection LoadStates()
        {
            string path = GetAbsoluteStatePath();
            if (!File.Exists(path))
                return new PluginStateCollection();

            try
            {
                return JsonUtility.FromJson<PluginStateCollection>(File.ReadAllText(path))
                    ?? new PluginStateCollection();
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[PluginStateUtility] Failed to read plugin states from {path}: {ex.Message}");
                return new PluginStateCollection();
            }
        }

        public static void SaveStates(PluginStateCollection collection)
        {
            if (collection == null)
                return;

            string path = GetAbsoluteStatePath();
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, JsonUtility.ToJson(collection, true));

#if UNITY_EDITOR
            AssetDatabase.Refresh();
#endif
        }

        public static bool TryGetState(PluginStateCollection collection, string pluginName, out PluginState state)
        {
            state = null;
            if (collection?.states == null || string.IsNullOrWhiteSpace(pluginName))
                return false;

            state = collection.states.FirstOrDefault(s =>
                string.Equals(s.plugin, pluginName, StringComparison.OrdinalIgnoreCase));

            return state != null;
        }
    }
}
