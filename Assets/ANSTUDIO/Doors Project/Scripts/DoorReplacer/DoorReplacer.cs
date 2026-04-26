using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

using UnityEngine.InputSystem; // Added for PlayerInput
using System.Collections;
using UnityEngine.AI;

namespace ProjectDoors
{
    public class DoorReplacer : MonoBehaviour
    {
        [HideInInspector]
        public GameObject newDoorSource;

        private GameObject currentDoor;
        private GameObject newDoor;
        [HideInInspector]
        public bool doorReplaced = false; // Track whether door replacement has been completed

        private void OnValidate()
        {
            currentDoor = gameObject;
        }

        public void ReplaceDoor()
        {
#if UNITY_EDITOR
            if (newDoorSource == null)
            {
                Debug.LogWarning("The Player Prefab/Model is not assigned, you need to drag a model or prefab!");
                return;
            }

            if (currentDoor == null)
            {
                Debug.LogWarning("The current door has already been instantiated once, to setup another player Close and reopen the Player Setup.");
                return;
            }

             /* ------------------------------------------------------------------
             * Work out the transform we will apply to the replacement:
             *   • Prefab asset  – stick to current door’s position (previous behaviour)
             *   • Scene object – copy the dragged object’s world transform
             * -----------------------------------------------------------------*/
            Vector3 targetPos, targetScale;
            Quaternion targetRot;
            Transform parent = currentDoor.transform.parent;
            if (PrefabUtility.IsPartOfPrefabAsset(newDoorSource))
            {
                currentDoor.transform.GetPositionAndRotation(out targetPos, out targetRot);
                targetScale = currentDoor.transform.lossyScale;
            }
            else
            {
                Transform srcT = newDoorSource.transform;
                targetPos = srcT.position;
                targetRot = srcT.rotation;
                targetScale = srcT.lossyScale;
            }

            /*
            * InstantiatePrefab only works on **assets**.  
            * If the user dragged a scene object, fall back to `Instantiate`
            * and register the new object with the undo system.
            */
            // ——— Spawn the new door (prefab or scene object) ———
            if (PrefabUtility.IsPartOfPrefabAsset(newDoorSource))
                newDoor = (GameObject)PrefabUtility.InstantiatePrefab(newDoorSource, parent);
            else
            {
                newDoor = Instantiate(newDoorSource, parent);
                Undo.RegisterCreatedObjectUndo(newDoor, "Duplicate Scene Door");
            }
            
            // ——— NOW that the new door is in the scene, ask about deleting the original source ———
            if (!PrefabUtility.IsPartOfPrefabAsset(newDoorSource))
            {
                // Cache name before we potentially destroy it
                string originalName = newDoorSource.name;
                
                bool deleteOriginal = EditorUtility.DisplayDialog(
                "Delete Original Door?",
                $"You’ve just spawned a copy of '{originalName}'.\n\nDelete the original source object?",
                "Yes",
                "No"
                );
                if (deleteOriginal)
                {
                    Undo.DestroyObjectImmediate(newDoorSource);
                    Debug.Log($"Original scene object '{originalName}' was deleted.");
                }
                else
                {
                    Debug.Log($"Original scene object '{originalName}' was retained.");
                }
                
                // Clear the reference so the Inspector stops trying to preview it
                newDoorSource = null;
                EditorUtility.SetDirty(this);
            }


            if (currentDoor == null || currentDoor.transform == null)
            {
                Debug.LogWarning("Current door's transform is missing. Close and reopen the Player Setup.");
                return;
            }

            if (newDoor == null)
            {
                Debug.LogWarning("New door was destroyed before setup completed. Restarting process...");
                ReplaceDoor();
                return;
            }

            // Apply the chosen world transform
            newDoor.transform.SetPositionAndRotation(targetPos, targetRot);
            newDoor.transform.localScale = targetScale;

            // Assign a unique name if another door with the same name exists
            newDoor.name = GetUniqueName(newDoor.name);

            // (2) Break the prefab link so this door is no longer a prefab instance
            if (PrefabUtility.IsPartOfPrefabInstance(newDoor))
            {
                PrefabUtility.UnpackPrefabInstance(
                    newDoor,
                    PrefabUnpackMode.Completely,
                    InteractionMode.AutomatedAction
                );
            }

            // Find the object that contains a MeshRenderer or SkinnedMeshRenderer
            GameObject targetForComponents = null;
            MeshRenderer meshRenderer = newDoor.GetComponentInChildren<MeshRenderer>();
            if (meshRenderer != null)
            {
                targetForComponents = meshRenderer.gameObject;
            }
            else
            {
                SkinnedMeshRenderer skinnedMesh = newDoor.GetComponentInChildren<SkinnedMeshRenderer>();
                if (skinnedMesh != null)
                {
                    targetForComponents = skinnedMesh.gameObject;
                }
            }

            if (targetForComponents == null)
            {
                Debug.LogWarning("No MeshRenderer or SkinnedMeshRenderer found on the new door. Components were not added.");
            }
            else
            {
                int usableLayer = LayerMask.NameToLayer("Usable");
                if (usableLayer < 0)
                {
                    Debug.LogWarning("Layer 'Usable' doesn't exist. Please create it or use a valid layer name.");
                }
                else
                {
                    targetForComponents.layer = usableLayer;
                    Debug.Log($"Tag and Layer assigned before adding components. New door name: {newDoor.name}");
                }
                targetForComponents.tag = "Untagged";
                // Add 2 BoxColliders (one of them set as trigger)
                BoxCollider boxCollider1 = targetForComponents.AddComponent<BoxCollider>();
                BoxCollider boxCollider2 = targetForComponents.AddComponent<BoxCollider>();
                boxCollider2.isTrigger = true;

                // Add ProximityStopper component
                targetForComponents.AddComponent<ProximityStopper>();

                // Add NavMeshObstacle component and set it to carve.
                NavMeshObstacle navMeshObstacle = targetForComponents.AddComponent<NavMeshObstacle>();
                navMeshObstacle.carving = true;
                navMeshObstacle.carveOnlyStationary = false;  // Ensure carveOnlyStationary is false

                // Add AudioSource component with Spatial Blend set as 1 and Max Distance set as 3
                AudioSource audioSource = targetForComponents.AddComponent<AudioSource>();
                audioSource.spatialBlend = 1f;
                audioSource.maxDistance = 3f;

                // Add LayeredNavMeshObstacle component
                targetForComponents.AddComponent<LayeredNavMeshObstacle>();

                // Add Door component
                targetForComponents.AddComponent<Door>();
            }

            Undo.DestroyObjectImmediate(currentDoor);
            Selection.activeGameObject = newDoor;

            doorReplaced = true;

            if (this != null) // Ensure the script is not destroyed before setting dirty
            {
                EditorUtility.SetDirty(this);
            }

            Debug.Log("Character replaced successfully with all required components ensured.");

            // Show reminder window after prefab replacement
            DoorEventChannelReminderWindow.ShowWindow();
#else
            Debug.LogError("ReplaceDoor can only be called in the editor.");
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

        private void EnsureComponent<T>(GameObject obj) where T : Component
        {
#if UNITY_EDITOR
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
#endif
        }

#if UNITY_EDITOR
        private void EnsureRigidbodyWithConstraints(GameObject obj)
        {
            if (obj == null)
            {
                Debug.LogWarning("Attempted to add a Rigidbody component, but the object was destroyed. Close and reopen Player Setup.");
                return;
            }

            if (!obj.TryGetComponent<Rigidbody>(out var rb))
            {
                rb = obj.AddComponent<Rigidbody>();
                Debug.Log("Added missing Rigidbody component.");
            }

            rb.freezeRotation = true;
        }
#endif

#if UNITY_EDITOR
        private void AddPlayerInputWithDelay()
        {
            int delayFrames = 20; // Adjust as needed (this delays the addition of PlayerInput by 10 frames)
            int counter = 0;

            void WaitForFrames()
            {
                counter++;
                if (counter >= delayFrames)
                {
                    if (newDoor != null && !newDoor.TryGetComponent<PlayerInput>(out _))
                    {
                        newDoor.AddComponent<PlayerInput>();
                        Debug.Log("Delayed addition of PlayerInput component.");
                    }
                    EditorApplication.update -= WaitForFrames; // Stop the loop
                }
            }

            EditorApplication.update += WaitForFrames; // Start waiting for frames
        }
#endif
    }

#if UNITY_EDITOR
    [CustomEditor(typeof(DoorReplacer))]
    public class DoorReplacerEditor : Editor
    {
        private Texture2D dropAreaBackground;
        private Texture2D prefabPreview;
        private GameObject lastDroppedPrefab;

        private void OnEnable()
        {
            dropAreaBackground = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/ANSTUDIO/Doors Project/Scripts/DoorReplacer/UI/Door_model_drop.png");
        }

        public override void OnInspectorGUI()
        {
            DoorReplacer script = (DoorReplacer)target;

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
                GUI.Label(dropArea, "Drop Door Model", centeredStyle);
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
                            if (draggedObject is GameObject go)  // accept prefab OR scene object
                            {
                                script.newDoorSource = go;
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

            // Disable the button if no prefab is assigned OR if the door has already been replaced
            EditorGUI.BeginDisabledGroup(script.newDoorSource == null || script.doorReplaced);
            GUIContent buttonContent = new GUIContent("SETUP DOOR", script.newDoorSource == null || script.doorReplaced
                ? "You need to drag and drop a model/prefab to activate this button!"
                : string.Empty);

            if (GUILayout.Button(buttonContent, GUILayout.Width(160), GUILayout.Height(30)))
            {
                script.ReplaceDoor();
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

#if UNITY_EDITOR
    public class DoorEventChannelReminderWindow : EditorWindow
    {
        private Texture2D reminderTexture;
        private float textHeight;
        private const string message = "The last step implies for you to create a new <b>Door Event Channel</b> with a <b>unique name</b> using the <b>NEW</b> button in the <b>GENERAL</b> section of the <b>DOOR script</b>." +
                                       "\n\nThe process is now <b>COMPLETE</b>, you can now test your door with you new door model.";

        public static bool forceShowReminder = false;
        private const string prefKey = "DoorEventChannelReminder_Show";

        public static void ShowWindow()
        {
            if (!EditorPrefs.GetBool(prefKey, true) && !forceShowReminder)
            {
                return;
            }

            DoorEventChannelReminderWindow window = CreateInstance<DoorEventChannelReminderWindow>();
            window.titleContent = new GUIContent("Door Event Channel Reminder");
            window.InitializeWindowSize();
            window.ShowUtility();
        }

        private void OnEnable()
        {
            reminderTexture = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/ANSTUDIO/Doors Project/Scripts/DoorReplacer/Ui/EventChannelNewError.png");
            InitializeWindowSize();
        }

        private void InitializeWindowSize()
        {
            if (reminderTexture != null)
            {
                float windowWidth = reminderTexture.width;
                GUIStyle style = new GUIStyle(EditorStyles.wordWrappedLabel)
                {
                    alignment = TextAnchor.MiddleLeft,
                    richText = true
                };
                textHeight = style.CalcHeight(new GUIContent(message), windowWidth);
                float checkboxHeight = EditorGUIUtility.singleLineHeight;
                float windowHeight = 10 + textHeight + 10 + reminderTexture.height + 10 + checkboxHeight + 10;
                windowWidth += 10;
                windowHeight += 10;
                minSize = new Vector2(windowWidth, windowHeight);
                maxSize = new Vector2(windowWidth, windowHeight);
            }
            else
            {
                minSize = new Vector2(300, 150);
                maxSize = new Vector2(300, 150);
            }
        }

        private void OnGUI()
        {
            if (reminderTexture == null)
            {
                EditorGUILayout.LabelField("Reminder image not found.", EditorStyles.centeredGreyMiniLabel);
                return;
            }

            Rect paddedArea = new Rect(5, 5, position.width - 10, position.height - 10);
            GUILayout.BeginArea(paddedArea);
            GUILayout.BeginVertical();
            GUILayout.Space(10);

            GUIStyle leftAlignedStyle = new GUIStyle(EditorStyles.wordWrappedLabel)
            {
                alignment = TextAnchor.MiddleLeft,
                richText = true
            };

            Rect textRect = GUILayoutUtility.GetRect(position.width - 10, textHeight);
            GUI.Label(textRect, message, leftAlignedStyle);

            GUILayout.Space(10);

            Rect imageRect = GUILayoutUtility.GetRect(reminderTexture.width, reminderTexture.height, GUILayout.ExpandWidth(false));
            GUI.DrawTexture(imageRect, reminderTexture, ScaleMode.ScaleToFit);

            GUILayout.Space(10);

            bool showReminder = EditorPrefs.GetBool(prefKey, true);
            bool doNotShow = !showReminder;
            bool newDoNotShow = EditorGUILayout.ToggleLeft("Do not show this reminder again", doNotShow);
            if (newDoNotShow != doNotShow)
            {
                EditorPrefs.SetBool(prefKey, !newDoNotShow);
            }

            GUILayout.Space(10);
            GUILayout.EndVertical();
            GUILayout.EndArea();
        }
    }
#endif

#if UNITY_EDITOR
    public static class DoorReminderDebug
    {
        [MenuItem("Tools/DOORS Project/Reset Door Reminder")]
        private static void ResetDoorReminder()
        {
            EditorPrefs.SetBool("DoorEventChannelReminder_Show", true);
            DoorEventChannelReminderWindow.forceShowReminder = true;
            Debug.Log("Door reminder flag reset to true.");
        }
    }
#endif
}
