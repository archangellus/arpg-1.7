#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace PLAYERTWO.ARPGProject
{
    public static class ARPGInteractionManagerMenu
    {
        [MenuItem("Tools/PLAYER TWO/ARPG Project/Create Interaction Manager")]
        public static void CreateInteractionManager()
        {
            var existing = Object.FindObjectOfType<ARPGInteractionManager>();

            if (existing)
            {
                Selection.activeObject = existing.gameObject;
                EditorGUIUtility.PingObject(existing.gameObject);
                return;
            }

            var managerObject = new GameObject("ARPG Interaction Manager");
            var manager = managerObject.AddComponent<ARPGInteractionManager>();
            managerObject.AddComponent<ARPGInteractionPrompt>();

            var playerObject = GameObject.FindGameObjectWithTag(GameTags.Player);

            if (playerObject)
                playerObject.TryGetComponent(out manager.player);

            Undo.RegisterCreatedObjectUndo(managerObject, "Create ARPG Interaction Manager");
            Selection.activeObject = managerObject;
        }
    }
}
#endif
