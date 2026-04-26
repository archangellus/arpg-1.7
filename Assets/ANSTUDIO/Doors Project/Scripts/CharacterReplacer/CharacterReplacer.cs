using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine.InputSystem; // Added for PlayerInput
using System.Collections;

namespace ProjectDoors
{
    public class CharacterReplacer : MonoBehaviour
    {
        [HideInInspector]
        public GameObject newCharacterPrefab;

        private GameObject currentCharacter;
        private GameObject newCharacter;
        [HideInInspector]
        public bool characterReplaced = false; // Track whether character replacement has been completed

        private void OnValidate()
        {
            currentCharacter = gameObject;
        }

        public void ReplaceCharacter()
        {
#if UNITY_EDITOR
            if (newCharacterPrefab == null)
            {
                Debug.LogWarning("The Player Prefab/Model is not assigned, you need to drag a model or prefab!");
                return;
            }

            if (currentCharacter == null)
            {
                Debug.LogWarning("The current character has already been instantiated once, to setup another player Close and reopen the Player Setup.");
                return;
            }

            currentCharacter.transform.GetPositionAndRotation(out Vector3 position, out Quaternion rotation);
            Transform parent = currentCharacter.transform.parent;

            newCharacter = (GameObject)PrefabUtility.InstantiatePrefab(newCharacterPrefab);

            if (currentCharacter == null || currentCharacter.transform == null)
            {
                Debug.LogWarning("Current character's transform is missing. Close and reopen the Player Setup.");
                return;
            }

            if (newCharacter == null)
            {
                Debug.LogWarning("New character was destroyed before setup completed. Restarting process...");
                ReplaceCharacter();
                return;
            }

            newCharacter.transform.SetPositionAndRotation(position, rotation);
            newCharacter.transform.parent = parent;

            // Assign a unique name if another character with the same name exists
            newCharacter.name = GetUniqueName(newCharacter.name);

            newCharacter.tag = "Player";
            newCharacter.layer = LayerMask.NameToLayer("Player");
            Debug.Log($"Tag and Layer assigned before adding components. New character name: {newCharacter.name}");

            EnsureComponent<CapsuleCollider>(newCharacter);
            EnsureComponent<PlayerActions>(newCharacter);
            EnsureComponent<ClickAgentController>(newCharacter);
            EnsureRigidbodyWithConstraints(newCharacter);

            AddPlayerInputWithDelay();

            if (newCharacter.GetComponent<CharacterReplacer>() == null)
            {
                newCharacter.AddComponent<CharacterReplacer>();
            }

            Undo.DestroyObjectImmediate(currentCharacter);
            Selection.activeGameObject = newCharacter;

            characterReplaced = true;

            if (this != null) // Ensure the script is not destroyed before setting dirty
            {
                EditorUtility.SetDirty(this);
            }

            Debug.Log("Character replaced successfully with all required components ensured.");
#else
            Debug.LogError("ReplaceCharacter can only be called in the editor.");
#endif
        }

        private string GetUniqueName(string baseName)
        {
            string uniqueName = baseName;
            int counter = 1;
            while (GameObject.Find(uniqueName) != null)
            {
                uniqueName = $"{baseName} ({counter})";
                counter++;
            }
            return uniqueName;
        }

#if UNITY_EDITOR
        private void EnsureComponent<T>(GameObject obj) where T : Component
        {
            if (obj == null)
            {
                Debug.LogWarning("Attempted to add a component, but the object was destroyed. Close and reopen Player Setup.");
                return;
            }
            if (!obj.TryGetComponent<T>(out _))
            {
                obj.AddComponent<T>();
                Debug.Log($"Added missing component: {typeof(T).Name}");
            }
        }

        private void EnsureRigidbodyWithConstraints(GameObject obj)
        {
            if (obj == null)
            {
                Debug.LogWarning("Attempted to add a Rigidbody component, but the object was destroyed. Close and reopen Player Setup.");
                return;
            }
            Rigidbody rb;
            if (!obj.TryGetComponent<Rigidbody>(out rb))
            {
                rb = obj.AddComponent<Rigidbody>();
                Debug.Log("Added missing Rigidbody component.");
            }
            rb.freezeRotation = true;
        }

        private void AddPlayerInputWithDelay()
        {
            int delayFrames = 20; // Adjust as needed
            int counter = 0;

            void WaitForFrames()
            {
                counter++;
                if (counter >= delayFrames)
                {
                    if (newCharacter != null && !newCharacter.TryGetComponent<PlayerInput>(out _))
                    {
                        newCharacter.AddComponent<PlayerInput>();
                        Debug.Log("Delayed addition of PlayerInput component.");
                    }
                    EditorApplication.update -= WaitForFrames; // Stop the loop
                }
            }
            EditorApplication.update += WaitForFrames; // Start waiting for frames
        }
#endif
    } // End of CharacterReplacer class

#if UNITY_EDITOR
    [CustomEditor(typeof(CharacterReplacer))]
    public class CharacterReplacerEditor : Editor
    {
        private Texture2D dropAreaBackground;
        private Texture2D prefabPreview;
        private GameObject lastDroppedPrefab;

        private void OnEnable()
        {
            dropAreaBackground = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/ANSTUDIO/Doors Project/Scripts/CharacterReplacer/UI/Humanoid_robot.png");
        }

        public override void OnInspectorGUI()
        {
            CharacterReplacer script = (CharacterReplacer)target;

            GUILayout.Space(10);

            float dropAreaWidth = 165;
            float dropAreaHeight = 190;

            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            Rect dropArea = GUILayoutUtility.GetRect(dropAreaWidth, dropAreaHeight, GUILayout.ExpandWidth(false));
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();

            if (prefabPreview != null)
            {
                GUI.DrawTexture(dropArea, prefabPreview, ScaleMode.StretchToFill);
            }
            else if (dropAreaBackground != null)
            {
                GUI.DrawTexture(dropArea, dropAreaBackground, ScaleMode.StretchToFill);
            }

            GUIStyle centeredStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontStyle = FontStyle.Bold,
                normal = { textColor = Color.white }
            };

            if (prefabPreview == null)
            {
                GUI.Label(dropArea, "Drop Character Model", centeredStyle);
            }

            Event evt = Event.current;
            if (evt.type == EventType.DragUpdated || evt.type == EventType.DragPerform)
            {
                if (dropArea.Contains(evt.mousePosition))
                {
                    DragAndDrop.visualMode = DragAndDropVisualMode.Copy;

                    if (evt.type == EventType.DragPerform)
                    {
                        DragAndDrop.AcceptDrag();

                        foreach (Object draggedObject in DragAndDrop.objectReferences)
                        {
                            if (draggedObject is GameObject go && PrefabUtility.IsPartOfPrefabAsset(go))
                            {
                                script.newCharacterPrefab = go;
                                lastDroppedPrefab = go;
                                prefabPreview = AssetPreview.GetAssetPreview(go);
                                if (prefabPreview == null)
                                {
                                    EditorApplication.update += WaitForPrefabPreview;
                                }
                                EditorUtility.SetDirty(script);
                            }
                        }
                    }
                    Event.current.Use();
                }
            }

            GUILayout.Space(10);

            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();

            EditorGUI.BeginDisabledGroup(script.newCharacterPrefab == null || script.characterReplaced);
            GUIContent buttonContent = new GUIContent("SETUP CHARACTER", script.newCharacterPrefab == null || script.characterReplaced
                ? "You need to drag and drop a model/prefab to activate this button!"
                : string.Empty);

            if (GUILayout.Button(buttonContent, GUILayout.Width(160), GUILayout.Height(30)))
            {
                script.ReplaceCharacter();
            }
            EditorGUI.EndDisabledGroup();

            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
        }

        private void WaitForPrefabPreview()
        {
            if (lastDroppedPrefab != null)
            {
                prefabPreview = AssetPreview.GetAssetPreview(lastDroppedPrefab);
                if (prefabPreview != null)
                {
                    EditorApplication.update -= WaitForPrefabPreview; // Stop checking once preview is available
                    Repaint();
                }
            }
        }
    }
#endif
} // End of namespace ProjectDoors
