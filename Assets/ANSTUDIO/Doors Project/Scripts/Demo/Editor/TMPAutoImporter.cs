#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.Presets;
using UnityEngine;
using TMPro;
using System.IO;

namespace ProjectDoors
{
    [InitializeOnLoad]
    public static class TMPAutoImporter
    {
        // Key to track if the TagManager preset prompt was declined.
        private const string kTagManagerPromptDisabledKey = "DoorsProject_DoNotPromptTagManagerPreset";

        static TMPAutoImporter()
        {
            Application.logMessageReceived += FilterTMPMissingError;
            EditorApplication.delayCall += DelayedInit;
        }

        private static void DelayedInit()
        {
            // --- TMP Essentials Import Check ---
            if (TMP_Settings.instance == null)
            {
                Debug.LogWarning("TMP Settings instance is null. Skipping TMP Essentials import check.");
            }
            else if (TMP_Settings.defaultFontAsset == null)
            {
                if (EditorUtility.DisplayDialog("Import TMP Essentials",
                    "This asset requires TextMeshPro Essentials. Would you like to import them now?",
                    "Yes", "No"))
                {
                    try
                    {
                        EditorApplication.ExecuteMenuItem("Window/TextMeshPro/Import TMP Essentials");
                    }
                    catch (System.Exception ex)
                    {
                        Debug.LogError("Failed to import TMP Essentials: " + ex.Message);
                    }
                }
            }
        }

        // Checks whether the TagManager preset is already applied.
        // For a simple (but not foolproof) check we compare the file contents.
        private static bool IsTagManagerPresetApplied()
        {
            string presetAssetPath = "Assets/ANSTUDIO/Doors Project/Demo/Presets/DoorsProject_TagsAndLayers.preset";
            string tagManagerFullPath = Path.Combine(Application.dataPath, "../ProjectSettings/TagManager.asset");

            if (!File.Exists(presetAssetPath) || !File.Exists(tagManagerFullPath))
                return false;

            try
            {
                string presetContent = File.ReadAllText(presetAssetPath);
                string tagManagerContent = File.ReadAllText(tagManagerFullPath);
                return presetContent.Equals(tagManagerContent);
            }
            catch (System.Exception ex)
            {
                Debug.LogError("Failed to compare TagManager preset: " + ex.Message);
                return false;
            }
        }

        // Uses Unity's Preset API to apply the preset to the TagManager asset.
        private static void ApplyTagManagerPreset()
        {
            string presetAssetPath = "Assets/ANSTUDIO/Doors Project/Demo/Presets/DoorsProject_TagsAndLayers.preset";

            Preset preset = AssetDatabase.LoadAssetAtPath<Preset>(presetAssetPath);
            if (preset == null)
            {
                Debug.LogError("Failed to load TagManager preset from " + presetAssetPath);
                return;
            }

            Object tagManager = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0];
            if (tagManager == null)
            {
                Debug.LogError("Failed to load TagManager asset from ProjectSettings/TagManager.asset");
                return;
            }

            if (preset.ApplyTo(tagManager))
            {
                Debug.Log("TagManager preset applied successfully.");
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            }
            else
            {
                Debug.LogError("Failed to apply TagManager preset.");
            }
        }

        // Filters out the specific TMP error message.
        private static void FilterTMPMissingError(string logString, string stackTrace, LogType type)
        {
            if (type == LogType.Error && logString.Contains("TextMesh Pro Essential Resources are missing"))
            {
                return;
            }
        }

        // Manual menu option for applying the preset.
        [MenuItem("Tools/DOORS Project/Import Tags and Layers")]
        private static void ManualImportTagsAndLayers()
        {
            // Reset the prompt flag so the manual import always applies the preset.
            EditorPrefs.SetBool(kTagManagerPromptDisabledKey, false);
            ApplyTagManagerPreset();
        }
    }
}
#endif
