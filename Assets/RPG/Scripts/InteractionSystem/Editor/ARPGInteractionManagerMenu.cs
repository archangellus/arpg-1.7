#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace PLAYERTWO.ARPGProject
{
    public static class ARPGInteractionManagerMenu
    {
        [MenuItem("Tools/ANSTUDIO/Interaction System/Create Interaction Manager")]
        public static void CreateInteractionManager()
        {
            var existing = Object.FindFirstObjectByType<ARPGInteractionManager>();

            if (existing)
            {
                Selection.activeObject = existing.gameObject;
                EditorGUIUtility.PingObject(existing.gameObject);
                return;
            }

            var managerObject = new GameObject("ARPG Interaction Manager");
            var manager = managerObject.AddComponent<ARPGInteractionManager>();
            managerObject.AddComponent<ARPGInteractionPrompt>();

            if (!string.IsNullOrWhiteSpace(manager.playerTag))
            {
                try
                {
                    var playerObject = GameObject.FindGameObjectWithTag(manager.playerTag);

                    if (playerObject)
                        playerObject.TryGetComponent(out manager.player);
                }
                catch (UnityException)
                {
                    Debug.LogWarning($"Cannot assign player because tag '{manager.playerTag}' is not defined.", manager);
                }
            }

            Undo.RegisterCreatedObjectUndo(managerObject, "Create ARPG Interaction Manager");
            Selection.activeObject = managerObject;
        }
    }
}
#endif
