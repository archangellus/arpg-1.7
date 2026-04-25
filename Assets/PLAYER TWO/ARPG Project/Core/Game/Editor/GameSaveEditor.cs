using System.IO;
using UnityEditor;
using UnityEngine;

namespace PLAYERTWO.ARPGProject
{
    [CustomEditor(typeof(GameSave))]
    public class GameSaveEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            EditorGUILayout.Space();

            if (GUILayout.Button("Open Save Folder"))
                OpenSaveFolder();

            if (GUILayout.Button("Clear Save Data"))
                PromptClearSaveData();
        }

        protected void OpenSaveFolder()
        {
            EditorUtility.RevealInFinder(Application.persistentDataPath);
        }

        protected void PromptClearSaveData()
        {
            var confirmed = EditorUtility.DisplayDialog(
                "Clear Save Data",
                "Are you sure you want to delete all save data? This action cannot be undone.",
                "Yes",
                "No"
            );

            if (confirmed)
                ClearSaveData();
        }

        protected void ClearSaveData()
        {
            var gameSave = (GameSave)target;
            var prefix = Application.isEditor ? "dev_" : "";
            var saveKey = $"{prefix}{gameSave.fileName}_{gameSave.saveVersion}";

            DeleteFile(saveKey, "bin");
            DeleteFile(saveKey, "json");
            ClearPlayerPrefs(saveKey);

            Debug.Log("[GameSave] Save data cleared.");
        }

        protected void DeleteFile(string saveKey, string extension)
        {
            var path = Path.Combine(Application.persistentDataPath, $"{saveKey}.{extension}");

            if (!File.Exists(path))
                return;

            File.Delete(path);
            Debug.Log($"[GameSave] Deleted save file: {path}");
        }

        protected void ClearPlayerPrefs(string saveKey)
        {
            if (!PlayerPrefs.HasKey(saveKey))
                return;

            PlayerPrefs.DeleteKey(saveKey);
            PlayerPrefs.Save();
            Debug.Log($"[GameSave] Deleted PlayerPrefs key: {saveKey}");
        }
    }
}
