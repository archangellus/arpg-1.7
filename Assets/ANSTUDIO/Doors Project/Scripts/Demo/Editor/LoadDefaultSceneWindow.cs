using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using System.IO;

namespace ProjectDoors
{
    /// <summary>
    /// Loads the first .unity scene found in the designated folder (and its sub-folders)
    /// when you select Tools/DOORS Project/Load Default Scene.
    /// </summary>
    public static class LoadDefaultScene
    {
        // Adjust this if your default-scene folder lives elsewhere.
        private const string FolderPath = "Assets/ANSTUDIO/Doors Project/Demo/Scene/";

        [MenuItem("Tools/DOORS Project/Load Default Scene")]
        private static void LoadScene()
        {
            if (!Directory.Exists(FolderPath))
            {
                Debug.LogWarning($"Folder not found: {FolderPath}");
                return;
            }

            // Recursively look for the first .unity file.
            string[] scenePaths = Directory.GetFiles(FolderPath, "*.unity", SearchOption.AllDirectories);
            if (scenePaths.Length == 0)
            {
                Debug.LogWarning($"No scenes found in {FolderPath} (including sub-folders).");
                return;
            }

            string sceneToLoad = scenePaths[0];   // Pick the first hit; adjust if you need specific logic.

            // Ask to save any dirty scenes before switching.
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                EditorSceneManager.OpenScene(sceneToLoad);
            }
        }
    }
}
