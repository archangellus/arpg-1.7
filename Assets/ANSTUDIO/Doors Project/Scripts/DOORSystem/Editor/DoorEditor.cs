using UnityEditor;
using UnityEngine;
using ProjectDoors;
using System.Collections.Generic;
namespace ProjectDoors
{

    [CustomEditor(typeof(Door))]
    public class DoorEditor : Editor
    {
        #region Fields & References
        private GameObject leverInstance; // Keep track of the created lever instance
        private int selectedTab = 0;
        private Texture2D generalIcon;
        private Texture2D hingeIcon;
        private Texture2D rotationsIcon;
        private Texture2D slidingsIcon;
        private Texture2D animationsIcon;
        private Texture2D soundsIcon;
        private Texture2D mouseIcon;
        private Texture2D interactivityIcon;
        private Texture2D knobsIcon;
        private Texture2D highlightIcon;

        // We’ll also store the GUIContent for each tab:
        private GUIContent[] tabContents;
        private SerializedObject serializedDoor;
        private SerializedProperty targetObjects;
        private bool pendingEventChannelCreation;
        private Door doorPendingChannel;
        private GUIContent[] tabContentsFull;  // icon + text
        private GUIContent[] tabContentsIcon;  // icon only


        // --- New fields for the animated text effect ---
        // For each tab we store a progress value (0 = no text, 1 = full text)
        private float[] tabTextProgress;
        // We'll update the progress using a Lerp (speed in units per second)
        private const float textLerpSpeed = 10f;
        // Used to compute delta time in the editor update callback
        private double lastUpdateTime;
        private bool isEditModeActive = false; // Tracks whether Edit Mode is active
        private int selectedKnobIndex = -1; // Tracks the currently selected knob index
        private Dictionary<GameObject, (Vector3 position, Quaternion rotation)> knobStateMemory = new Dictionary<GameObject, (Vector3, Quaternion)>();// Dictionary to store the last position and rotation of each knob
        #endregion
        #region Unity Lifecycle
        private void OnEnable()
        {
            Undo.undoRedoPerformed += OnUndoRedoPerformed;
            if (target == null)
            {
                DoorLogger.LogWarning("Target object is null in DoorEditor.");
                return;
            }

            serializedDoor = new SerializedObject(target);
            targetObjects = serializedDoor.FindProperty("targetObjects");

            ValidateSerializedObjects();

            // Load icons (adjust the paths as necessary)
            generalIcon = LoadIcon("Assets/ANSTUDIO/Doors Project/Scripts/DOORSystem/Editor/Icons/general-icon.png");
            hingeIcon = LoadIcon("Assets/ANSTUDIO/Doors Project/Scripts/DOORSystem/Editor/Icons/hinge-icon.png");
            rotationsIcon = LoadIcon("Assets/ANSTUDIO/Doors Project/Scripts/DOORSystem/Editor/Icons/rotations-icon.png");
            slidingsIcon = LoadIcon("Assets/ANSTUDIO/Doors Project/Scripts/DOORSystem/Editor/Icons/slidings-icon.png");
            animationsIcon = LoadIcon("Assets/ANSTUDIO/Doors Project/Scripts/DOORSystem/Editor/Icons/animations-icon.png");
            soundsIcon = LoadIcon("Assets/ANSTUDIO/Doors Project/Scripts/DOORSystem/Editor/Icons/sounds-icon.png");
            mouseIcon = LoadIcon("Assets/ANSTUDIO/Doors Project/Scripts/DOORSystem/Editor/Icons/mouse-icon.png");
            interactivityIcon = LoadIcon("Assets/ANSTUDIO/Doors Project/Scripts/DOORSystem/Editor/Icons/interactivity-icon.png");
            knobsIcon = LoadIcon("Assets/ANSTUDIO/Doors Project/Scripts/DOORSystem/Editor/Icons/knobs-icon.png");
            highlightIcon = LoadIcon("Assets/ANSTUDIO/Doors Project/Scripts/DOORSystem/Editor/Icons/highlight-icon.png");

            // Define the full content for each tab: (note the leading space in the text helps separate the icon)
            tabContentsFull = new GUIContent[]
            {
            new GUIContent(" GENERAL", generalIcon),
            new GUIContent(" PIVOTS", hingeIcon),
            new GUIContent(" ROTATIONS", rotationsIcon),
            new GUIContent(" SLIDINGS", slidingsIcon),
            new GUIContent(" ANIMATIONS", animationsIcon),
            new GUIContent(" SOUNDS", soundsIcon),
            new GUIContent(" MOUSE", mouseIcon),
            new GUIContent(" INTERACTIVITY", interactivityIcon),
            new GUIContent(" KNOBS", knobsIcon),
            new GUIContent(" HIGHLIGHT", highlightIcon)
            };

            // Define the icon-only version (text is empty but the tooltip is set)
            tabContentsIcon = new GUIContent[]
            {
            new GUIContent("", generalIcon, "GENERAL"),
            new GUIContent("", hingeIcon, "PIVOTS"),
            new GUIContent("", rotationsIcon, "ROTATIONS"),
            new GUIContent("", slidingsIcon, "SLIDINGS"),
            new GUIContent("", animationsIcon, "ANIMATIONS"),
            new GUIContent("", soundsIcon, "SOUNDS"),
            new GUIContent("", mouseIcon, "MOUSE"),
            new GUIContent("", interactivityIcon, "INTERACTIVITY"),
            new GUIContent("", knobsIcon, "KNOBS"),
            new GUIContent("", highlightIcon, "HIGHLIGHT")
            };

            // --- Initialize the text progress for animation ---
            tabTextProgress = new float[tabContentsFull.Length];
            for (int i = 0; i < tabTextProgress.Length; i++)
            {
                // The selected tab starts fully revealed; the rest are hidden.
                tabTextProgress[i] = (i == selectedTab) ? 1f : 0f;
            }
            lastUpdateTime = EditorApplication.timeSinceStartup;
            EditorApplication.update += UpdateTabTextAnimation;
        }

        /// <summary>
        /// Called when the editor is disabled.
        /// </summary>
        private void OnDisable()
        {
            // Ensure we don't leave Tools.hidden = true if the inspector is closed
            Tools.hidden = false;
            Undo.undoRedoPerformed -= OnUndoRedoPerformed;
        }
        #endregion
        #region Editor Callbacks
        /// <summary>
        /// Called when the inspector GUI is drawn.
        /// </summary>
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            serializedObject.ApplyModifiedProperties();

            if (Selection.activeGameObject != ((Door)target).gameObject)
            {
                isEditModeActive = false;
                selectedKnobIndex = -1;
                Tools.current = Tool.Move;
            }

            serializedDoor.Update();
            Door door = (Door)target;

            EditorGUI.BeginChangeCheck();
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(door, "Modify Door Properties");
                door.UpdateDoorBounds();
                SceneView.RepaintAll();
            }

            // *** Draw the tabs manually.
            // The text for each tab is drawn progressively based on our animated progress values.
            selectedTab = DrawTabsWithWrap(selectedTab);

            EditorGUILayout.Space();

            // Switch-case for tab content
            switch (selectedTab)
            {
                case 0: // GENERAL
                    DrawGeneralSettings(door);
                    break;
                case 1: // HINGE
                    DrawHingeSettings(door);
                    break;
                case 2: // ROTATIONS
                    DrawRotationSettings(door);
                    break;
                case 3: // SLIDINGS
                    DrawSlidingSettings(door);
                    break;
                case 4: // ANIMATIONS
                    DrawAnimationSettings(door);
                    break;
                case 5: // SOUNDS
                    DrawSoundSettings();
                    break;
                case 6: // MOUSE
                    DrawMouseSettings(door);
                    break;
                case 7: // INTERACTIVITY
                    DrawInteractivitySettings();
                    break;
                case 8: // KNOBS
                    DrawKnobSettings();
                    break;
                case 9: // MATERIALS / HIGHLIGHTING
                    DrawMaterialSettings();
                    break;
            }

            if (pendingEventChannelCreation && doorPendingChannel != null)
            {
                pendingEventChannelCreation = false;
                CreateNewDoorEventChannel(doorPendingChannel);
            }

            serializedDoor.ApplyModifiedProperties();
            EditorUtility.SetDirty(door);
        }


        private void OnSceneGUI()
        {
            // 1) Grab the door reference
            Door door = (Door)target;
            if (door == null) return;

            // 2) Pivot adjustment logic
            if (door.IsPivotAdjustmentEnabled && door.pivot != null)
            {
                EditorGUI.BeginChangeCheck();

                // Draw the position handle at the current pivot position
                Vector3 newPivotPosition = Handles.PositionHandle(door.pivot.position, Quaternion.identity);

                if (EditorGUI.EndChangeCheck())
                {
                    // Null-check the doorObject before using it
                    if (door.doorObject != null)
                    {
                        EditorUtility.SetDirty(door);
                        door.UpdateDoorBounds();  // Force bounds update
                        SceneView.RepaintAll();   // Refresh Scene View

                        Undo.RecordObject(door.pivot, "Move Pivot");

                        // Snap pivot to grid if enabled
                        if (door.EnableGridSnapping)
                        {
                            newPivotPosition.x = Mathf.Round(newPivotPosition.x / door.GridSize) * door.GridSize;
                            newPivotPosition.y = Mathf.Round(newPivotPosition.y / door.GridSize) * door.GridSize;
                            newPivotPosition.z = Mathf.Round(newPivotPosition.z / door.GridSize) * door.GridSize;

                            // Restrict pivot within collider bounds if enabled
                            if (door.limitPivotToBounds &&
                                door.doorObject.TryGetComponent<Collider>(out Collider pivotCollider))
                            {
                                Bounds bounds = pivotCollider.bounds;
                                newPivotPosition.x = Mathf.Clamp(newPivotPosition.x, bounds.min.x, bounds.max.x);
                                newPivotPosition.y = Mathf.Clamp(newPivotPosition.y, bounds.min.y, bounds.max.y);
                                newPivotPosition.z = Mathf.Clamp(newPivotPosition.z, bounds.min.z, bounds.max.z);
                            }
                        }

                        // Calculate the offset for the door object based on the new pivot position
                        Vector3 offset = newPivotPosition - door.pivot.position;

                        // Update the pivot position
                        door.pivot.position = newPivotPosition;

                        // Adjust the door object's position to maintain relative offset
                        door.doorObject.transform.position -= offset;

                        EditorUtility.SetDirty(door);
                        door.UpdateDoorBounds();  // Force bounds update
                        SceneView.RepaintAll();   // Refresh Scene View
                    }
                    else
                    {
                        // Optional warning if doorObject is missing
                        DoorLogger.LogWarning($"{door.name}: doorObject is not assigned, so pivot movement won't affect the door's position.");
                    }
                }
            }

            // 3) Knob adjustments
            // Automatically exit Edit Mode if the user deselects this door
            if (Selection.activeGameObject != door.gameObject)
            {
                isEditModeActive = false;
                selectedKnobIndex = -1;
                Tools.current = Tool.Move;
                return;
            }

            // If we're not in Edit Mode or haven't selected a knob, bail out
            if (!isEditModeActive || selectedKnobIndex < 0) return;

            // Grab the array of attached knobs
            SerializedProperty attachedObjects = serializedDoor.FindProperty("attachedObjects");
            if (selectedKnobIndex >= attachedObjects.arraySize) return;

            // Extract fields for the selected knob
            SerializedProperty knobProperty = attachedObjects.GetArrayElementAtIndex(selectedKnobIndex);
            SerializedProperty targetObject = knobProperty.FindPropertyRelative("targetObject");
            SerializedProperty isLocked = knobProperty.FindPropertyRelative("isLocked");
            SerializedProperty xLeeway = serializedDoor.FindProperty("xLeeway");

            // If there's no actual GameObject in that knob slot, bail
            if (targetObject.objectReferenceValue == null)
            {
                Handles.Label(Vector3.zero, "No Knob Object Selected");
                return;
            }

            // Proceed with custom SceneGUI handles
            if (targetObject.objectReferenceValue is GameObject selectedKnob)
            {
                // Ensure we track this knob's original transform state
                if (!knobStateMemory.ContainsKey(selectedKnob))
                {
                    knobStateMemory[selectedKnob] = (selectedKnob.transform.position, selectedKnob.transform.rotation);
                }

                // Immediately restore the knob to its last known position/rotation
                selectedKnob.transform.position = knobStateMemory[selectedKnob].position;
                selectedKnob.transform.rotation = knobStateMemory[selectedKnob].rotation;

                Vector3 savedPosition = knobStateMemory[selectedKnob].position;
                Quaternion savedRotation = knobStateMemory[selectedKnob].rotation;

                EditorGUI.BeginChangeCheck();

                // Draw the handle for position & rotation
                Vector3 newKnobPosition = Handles.PositionHandle(savedPosition, Quaternion.identity);
                Quaternion newKnobRotation = Handles.RotationHandle(savedRotation, savedPosition);

                // If the user actually moved/rotated the knob, and the knob isn't locked
                if (EditorGUI.EndChangeCheck() && !isLocked.boolValue)
                {
                    Undo.RecordObject(selectedKnob.transform, "Move or Rotate Knob");

                    // 3a) If knob grid snapping is enabled, snap to the door.knobGridSize
                    if (door.knobEnableGridSnapping)
                    {
                        float gridSize = door.knobGridSize <= 0.01f ? 0.25f : door.knobGridSize;
                        newKnobPosition.x = Mathf.Round(newKnobPosition.x / gridSize) * gridSize;
                        newKnobPosition.y = Mathf.Round(newKnobPosition.y / gridSize) * gridSize;
                        newKnobPosition.z = Mathf.Round(newKnobPosition.z / gridSize) * gridSize;
                    }

                    // 3b) If limiting knob to bounds is enabled, clamp within door collider
                    float leeway = xLeeway.floatValue;
                    if (door.limitKnobToBounds &&
                        door.doorObject != null &&
                        door.doorObject.TryGetComponent<Collider>(out Collider knobCollider))
                    {
                        Bounds bounds = knobCollider.bounds;

                        // Expand the bounds by 'leeway' if you wish
                        newKnobPosition.x = Mathf.Clamp(newKnobPosition.x, bounds.min.x - leeway, bounds.max.x + leeway);
                        newKnobPosition.y = Mathf.Clamp(newKnobPosition.y, bounds.min.y - leeway, bounds.max.y + leeway);
                        newKnobPosition.z = Mathf.Clamp(newKnobPosition.z, bounds.min.z - leeway, bounds.max.z + leeway);
                    }

                    // Finally, update the knob's transform & memory
                    selectedKnob.transform.position = newKnobPosition;
                    selectedKnob.transform.rotation = newKnobRotation;
                    knobStateMemory[selectedKnob] = (newKnobPosition, newKnobRotation);

                    EditorUtility.SetDirty(selectedKnob);
                }
            }
        }

        /// <summary>
        /// Update method called on every editor frame to animate the tab text.
        /// </summary>
        private void UpdateTabTextAnimation()
        {
            double currentTime = EditorApplication.timeSinceStartup;
            float dt = (float)(currentTime - lastUpdateTime);
            lastUpdateTime = currentTime;
            bool changed = false;

            for (int i = 0; i < tabTextProgress.Length; i++)
            {
                float target = (i == selectedTab) ? 1f : 0f;
                float newValue = Mathf.Lerp(tabTextProgress[i], target, dt * textLerpSpeed);
                if (Mathf.Abs(newValue - tabTextProgress[i]) > 0.001f)
                {
                    tabTextProgress[i] = newValue;
                    changed = true;
                }
            }
            if (changed)
            {
                Repaint();
            }
        }
        private void OnUndoRedoPerformed()
        {
            Door door = (Door)target;
            if (door != null)
            {
                door.UpdateDoorBounds();
                SceneView.RepaintAll();
            }
        }
        #endregion
        #region Tab or UI Navigation
        /// <summary>
        /// Draws the tab buttons. Each tab's text is drawn progressively based on an animated progress value.
        /// </summary>
        private int DrawTabsWithWrap(int currentSelectedTab)
        {
            if (tabContentsFull == null || tabContentsIcon == null ||
                tabContentsFull.Length == 0 || tabContentsIcon.Length == 0)
            {
                EditorGUILayout.HelpBox("Tab contents not loaded.", MessageType.Warning);
                return currentSelectedTab;
            }

            // Create the button style.
            GUIStyle buttonStyle = new GUIStyle(GUI.skin.button)
            {
                fixedHeight = 40,
                fontSize = 18,
                padding = new RectOffset(3, 3, 3, 3)
            };

            int tabCount = tabContentsFull.Length;
            GUIContent[] finalContents = new GUIContent[tabCount];
            float[] tabWidths = new float[tabCount];

            // For each tab, compute:
            // - The displayed text (using a substring of the full text based on progress)
            // - The current width by lerping between the icon-only width and full text width.
            for (int i = 0; i < tabCount; i++)
            {
                string fullText = tabContentsFull[i].text; // e.g. " GENERAL"
                int fullLength = fullText.Length;
                int charCountToShow = Mathf.RoundToInt(fullLength * tabTextProgress[i]);
                string displayedText = fullText.Substring(0, Mathf.Clamp(charCountToShow, 0, fullLength));
                finalContents[i] = new GUIContent(displayedText, tabContentsFull[i].image, tabContentsIcon[i].tooltip);

                // Calculate widths:
                float iconWidth = buttonStyle.CalcSize(new GUIContent("", tabContentsFull[i].image)).x;
                float fullWidth = buttonStyle.CalcSize(tabContentsFull[i]).x;
                float currentWidth = Mathf.Lerp(iconWidth, fullWidth, tabTextProgress[i]);
                tabWidths[i] = currentWidth;
            }

            // Layout: we wrap to new rows if necessary.
            float viewWidth = EditorGUIUtility.currentViewWidth - 25f;
            float xPos = 0f;

            EditorGUILayout.BeginHorizontal();
            for (int i = 0; i < tabCount; i++)
            {
                if (xPos + tabWidths[i] > viewWidth)
                {
                    EditorGUILayout.EndHorizontal();
                    EditorGUILayout.BeginHorizontal();
                    xPos = 0f;
                }

                // Draw the button using the computed dynamic width.
                if (GUILayout.Button(finalContents[i], buttonStyle, GUILayout.Width(tabWidths[i])))
                {
                    currentSelectedTab = i;
                }
                xPos += tabWidths[i];
            }
            EditorGUILayout.EndHorizontal();

            return currentSelectedTab;
        }
        #endregion
        #region Inspector Section Methods
        /// <summary>
        /// General settings section.
        /// </summary>
        /// <param name="door"></param>
        private void DrawGeneralSettings(Door door)
        {
            // 1) Draw the box for the title only
            DrawTitle("-- GENERAL SETTINGS --");
            // Now a background box for warnings + speed settings
            DrawBackgroundBox(() =>
            {
                // 2) Then proceed with normal layout calls for the rest
                if (door.IsLeverControlled && door.Lever == null)
                {
                    EditorGUILayout.HelpBox(
                        "The Lever control is ENABLED but no Lever prefab assigned.",
                        MessageType.Info
                    );
                }

                if (door.isDualDoor && door.secondDoor == null)
                {
                    EditorGUILayout.HelpBox(
                        "Second Door is enabled but not assigned.",
                        MessageType.Warning
                    );
                }

                if (door.doorEventChannel == null)
                {
                    EditorGUILayout.HelpBox(
                        "No Door Event Channel is assigned.",
                        MessageType.Error
                    );
                }

                EditorGUILayout.Space();

                // Example sub-calls (just normal layout)
                DrawEventChannels(door);

                EditorGUILayout.Space();
                EditorGUILayout.LabelField("Speed Settings", EditorStyles.boldLabel);
                door.Speed = EditorGUILayout.FloatField(new GUIContent("Move Speed", "Defines how fast the door moves or rotates when opening or closing(if 'Match Speed to Sound',  is true it will overwrite this setting.)"), door.Speed);

                EditorGUILayout.Space();
                DrawDelaySettings(door);
                DrawDualDoorSettings(door);
                DrawDoorEnablers(door);
                DrawLeverSettings(door);

                // No extra EndVertical() calls here
            });
        }
        /// <summary>
        /// Draws the settings for hinge and pivot behavior.
        /// </summary>
        /// <param name="door"></param>
        private void DrawHingeSettings(Door door)
        {
            DrawTitle("-- PIVOT SETTINGS --");
            // Now a background box for warnings + speed settings
            DrawBackgroundBox(() =>
            {

                DeleteOldParent(door);

                //EditorGUILayout.LabelField(new GUIContent("-- HINGE SETTINGS --", "Settings for hinge and pivot behavior."), EditorStyles.boldLabel);

                // Check if the Door Object field is null and display a warning
                if (door.doorObject == null)
                {
                    //EditorGUILayout.HelpBox("The Door Object is not assigned. You need to assign a door object to use the Hinge settings.", MessageType.Warning);
                    // NEW Button: Assigns the door's own GameObject to the doorObject field.
                    if (GUILayout.Button(new GUIContent("ENABLE Pivot", "Assign the door's own GameObject as the pivot container and update pivot settings."), GUILayout.Width(100), GUILayout.Height(30)))
                    {
                        // Automatically assign the Door Object field with the GameObject to which this script is attached.
                        door.doorObject = door.gameObject;
                        if (door.doorObject == door.gameObject)
                        {
                            GameObject newDoorObject = door.doorObject;
                            if (newDoorObject != door.doorObject)
                            {
                                door.doorObject = newDoorObject;

                                if (door.doorObject != null)
                                {
                                    // When a new doorObject is assigned, call EnsureAttachedObject.
                                    // (Note: In your Door script, EnsureAttachedObject() already checks for a root object and exits if so.)
                                    door.EnsureAttachedObject();
                                    EditorUtility.SetDirty(door);
                                }
                                else
                                {
                                    // Clear pivot if no Door Object is assigned
                                    door.pivot = null;
                                }
                            }
                        }
                    }

                }
                // **New Check:** If the assigned doorObject has no parent (is in the root)
                else if (door.doorObject.transform.parent == null)
                {
                    DrawCustomErrorHelpBox(door,
                        "The assigned Door Object is in root (has no parent)." +
                        "Press the button bellow to automatically create a parent for the Door Object.");
                }
                else
                {
                    // Optionally, if the door has a previous parent error, display that as well.
                    if (door.HasParentError)
                    {
                        DrawCustomErrorHelpBox(door, "Failed to find the created pivot. The Door Object needs to be part of a parent object to use this function.");
                    }
                }

                //EditorGUILayout.Space();


                // Instead of displaying the Door Object field, display NEW and DELETE buttons:
                EditorGUILayout.BeginHorizontal();

                if (door.doorObject != null)
                {
                    // DELETE Button: Clears the Door Object.
                    if (GUILayout.Button(new GUIContent("DISABLE Pivot", "Clear the assigned Door Object and pivot."), GUILayout.Width(100), GUILayout.Height(30)))
                    {
                        //GameObject newDoorObject = door.doorObject;
                        door.doorObject = null;
                        //door.pivot = null;
                        EditorUtility.SetDirty(door);
                    }
                }
                EditorGUILayout.EndHorizontal();



                // Only show the Pivot Settings if a Door Object is assigned AND it has a parent
                if (door.doorObject != null && door.doorObject.transform.parent != null)
                {
                    if (door.doorObject.transform.parent != door.pivot)
                    {
                        door.UpdateDoorBounds();
                        ApplyPreset(door, PositionPreset.Center);
                        /*EditorGUILayout.HelpBox(
                        "Now you need to press one of the preset buttons available for automatic repositioning of the door as a child of the previously created pivot, " +
                        "if not you must manually move the door as a child of the pivot.", MessageType.Warning);*/
                    }

                    EditorGUILayout.Space();
                    EditorGUILayout.LabelField("Pivot Settings", EditorStyles.boldLabel);

                    // Draw the Pivot Name field
                    string originalPivotName = door.pivotName;
                    string newPivotName = EditorGUILayout.DelayedTextField(new GUIContent("Pivot Name", "The name used to search for or create the pivot."), originalPivotName);

                    if (!string.IsNullOrEmpty(newPivotName) && newPivotName != originalPivotName)
                    {
                        door.pivotName = newPivotName;

                        // Check if a pivot with the same name exists
                        Transform existingPivot = door.transform.root.Find(newPivotName);
                        if (existingPivot == null)
                        {
                            string uniquePivotName = GenerateUniquePivotName(door, "Pivot");
                            GameObject uniquePivot = new GameObject(uniquePivotName);
                            uniquePivot.transform.SetParent(door.transform, false);
                            door.pivot = uniquePivot.transform;
                            door.pivotName = uniquePivotName;
                            DoorLogger.Log($"Assigned unique pivot name: {uniquePivotName}");
                        }
                        else
                        {
                            // Assign the existing pivot
                            door.pivot = existingPivot;
                            DoorLogger.Log($"Assigned existing pivot GameObject: {newPivotName}");
                        }

                        EditorUtility.SetDirty(door);
                    }

                    // Draw the Pivot Transform field
                    door.pivot = (Transform)EditorGUILayout.ObjectField(new GUIContent("Pivot Transform", "Assign or change the pivot transform used for door rotation/sliding."), door.pivot, typeof(Transform), true);
                    EditorGUILayout.Space();
                    // Add a button to enable or disable pivot adjustment mode
                    string buttonLabel = door.IsPivotAdjustmentEnabled ? "Exit Pivot Adjustment Mode" : "Enter Pivot Adjustment Mode";
                    GUIContent adjustmentButtonContent = new GUIContent(buttonLabel, door.IsPivotAdjustmentEnabled ? "Disable pivot adjustment mode and use preset repositioning." : "Enable pivot adjustment mode to manually reposition the pivot using scene handles.");
                    if (GUILayout.Button(adjustmentButtonContent, GUILayout.Height(30)))
                    {
                        Undo.RecordObject(door, buttonLabel);
                        door.IsPivotAdjustmentEnabled = !door.IsPivotAdjustmentEnabled;
                        door.UpdateDoorBounds();
                        Tools.current = door.IsPivotAdjustmentEnabled ? Tool.None : Tool.Move;
                        EditorUtility.SetDirty(door);
                    }

                    if (door.IsPivotAdjustmentEnabled)
                    {
                        Tools.hidden = true;
                        // Display grid snapping options only when Pivot Adjustment Mode is on
                        EditorGUILayout.Space();
                        EditorGUILayout.LabelField("Grid Snapping Settings", EditorStyles.boldLabel);

                        bool enableGridSnapping = EditorGUILayout.Toggle(new GUIContent("Enable Grid Snapping", "Toggle to enable snapping of the pivot position to a grid."), door.EnableGridSnapping);
                        if (enableGridSnapping != door.EnableGridSnapping)
                        {
                            Undo.RecordObject(door, "Toggle Grid Snapping");
                            door.EnableGridSnapping = enableGridSnapping;
                            EditorUtility.SetDirty(door);
                        }

                        if (door.EnableGridSnapping)
                        {
                            float gridSize = EditorGUILayout.FloatField(new GUIContent("Grid Size", "Set the size of the grid cells for snapping. Larger values increase the spacing."), door.GridSize);
                            if (gridSize != door.GridSize && gridSize > 0)
                            {
                                Undo.RecordObject(door, "Change Grid Size");
                                door.GridSize = gridSize;
                                EditorUtility.SetDirty(door);
                            }

                            bool limitPivotToBounds = EditorGUILayout.Toggle(new GUIContent("Limit Pivot to Collider Bounds", "Restrict pivot movement within the door's collider bounds."), door.limitPivotToBounds);
                            if (limitPivotToBounds != door.limitPivotToBounds)
                            {
                                Undo.RecordObject(door, "Toggle Limit Pivot to Collider Bounds");
                                door.limitPivotToBounds = limitPivotToBounds;
                                EditorUtility.SetDirty(door);
                            }
                        }

                        DrawCustomHelpBox("Pivot Adjustment Mode is enabled. Move the pivot using the handle in the Scene view. The position will snap to the grid if enabled.");
                        SceneView.RepaintAll(); // Ensure Scene view updates
                    }
                    else
                    {
                        Tools.hidden = false;
                        // Show preset buttons only if Pivot Adjustment Mode is disabled
                        if (door.pivot != null)
                        {
                            EditorGUILayout.Space();
                            DrawPresetButtons(door);
                        }
                    }
                }
            });
        }


        /// <summary>
        /// Draws the preset buttons for automatic repositioning of the pivot.
        /// </summary>
        /// <param name="door"></param>
        private void DrawRotationSettings(Door door)
        {
            DrawTitle("-- ROTATION SETTINGS --");
            // Now a background box for warnings + speed settings
            DrawBackgroundBox(() =>
            {
                /* ❸ Disable this whole block when sliding is ON */
                EditorGUI.BeginDisabledGroup(door.activateSliding);

                // Rotation Axis
                door.rotationAxis = (Door.Axis)EditorGUILayout.EnumPopup(
                    new GUIContent(
                        "Rotation Axis",
                        "Specifies the axis around which the door will rotate (X, Y, or Z)."
                    ),
                    door.rotationAxis
                );
                // Is Rotating Door
                /*door.IsRotatingDoor = EditorGUILayout.Toggle(
                    new GUIContent(
                        "Is Rotating Door",
                        "Toggle whether the door should rotate (true) or slide (false)."
                    ),
                    door.IsRotatingDoor
                );
                */

                EditorGUILayout.LabelField("Rotation Configs", EditorStyles.boldLabel);
                // Rotation Amount
                door.RotationAmount = EditorGUILayout.FloatField(
                    new GUIContent(
                        "Rotation Amount",
                        "The total angle of rotation in degrees when the door is fully open."
                    ),
                    door.RotationAmount
                );
                // Forward Direction
                door.ForwardDirection = EditorGUILayout.FloatField(
                    new GUIContent(
                        "Forward Direction",
                        "Dot-product threshold to determine which side the user is on, affecting how the door rotates (positive vs. negative direction)."
                    ),
                    door.ForwardDirection
                );

                EditorGUILayout.LabelField("Visualize Rotations", EditorStyles.boldLabel);
                // Gizmo X Rotation
                door.gizmoXRotation = EditorGUILayout.FloatField(
                    new GUIContent(
                        "Gizmo X Rotation",
                        "Adjusts the door gizmo’s X-axis rotation in Scene view, purely for visualizing the door’s arc."
                    ),
                    door.gizmoXRotation
                );
                // Gizmo Z Rotation
                door.gizmoZRotation = EditorGUILayout.FloatField(
                    new GUIContent(
                        "Gizmo Z Rotation",
                        "Adjusts the door gizmo’s Z-axis rotation in Scene view, purely for visualizing the door’s arc."
                    ),
                    door.gizmoZRotation
                );
                // Gizmo Y Offset
                door.gizmoYOffset = EditorGUILayout.FloatField(
                    new GUIContent(
                        "Gizmo Y Offset",
                        "Vertical offset for the gizmo in the Scene view. Helps if the door is at ground level and the gizmo is obscured."
                    ),
                    door.gizmoYOffset
                );
                EditorGUI.EndDisabledGroup();
            });
        }

        /// <summary>
        /// Draws the sliding settings for the door.
        /// </summary>
        /// <param name="door"></param>
        private void DrawSlidingSettings(Door door)
        {
            DrawTitle("-- SLIDING SETTINGS --");
            // Now a background box for warnings + speed settings
            DrawBackgroundBox(() =>
            {
                /* ------------------------------ Button ---------------------------- */
                EditorGUILayout.BeginHorizontal();

                string slideButtonLabel = door.activateSliding ? "Deactivate Sliding"
                                                               : "Activate Sliding";

                // calculate the space that text + padding really need
                GUIStyle slideBtnStyle = GUI.skin.button;
                float minWidth = 100f;                                   // same base width as pivot btn
                float neededWide = slideBtnStyle.CalcSize(
                                       new GUIContent(slideButtonLabel)).x + 20f;   // + some padding
                float btnWidth = Mathf.Max(minWidth, neededWide);

                if (GUILayout.Button(
                        new GUIContent(slideButtonLabel, "Toggle sliding mode for this door."),
                        GUILayout.Width(btnWidth), GUILayout.Height(30)))
                {
                    Undo.RecordObject(door, "Toggle Sliding Mode");
                    door.activateSliding = !door.activateSliding;
                    EditorUtility.SetDirty(door);
                }

                EditorGUILayout.EndHorizontal();
                EditorGUILayout.Space();
                /* ------------------------------------------------------------------ */

                /* ❷ Disable the rest of the sliding-specific UI when sliding is OFF */
                EditorGUI.BeginDisabledGroup(!door.activateSliding);

                // Slide Direction
                door.SlideDirection = EditorGUILayout.Vector3Field(
                    new GUIContent(
                        "Slide Direction",
                        "The direction in which the door moves when sliding (e.g., Vector3.right for horizontal movement)."
                    ),
                    door.SlideDirection
                );
                // Slide Amount
                door.SlideAmount = EditorGUILayout.FloatField(
                    new GUIContent(
                        "Slide Amount",
                        "The total distance the door travels along its Slide Direction."
                    ),
                    door.SlideAmount
                );

                // Rotation During Sliding
                EditorGUILayout.Space();
                EditorGUILayout.LabelField("Rotation During Sliding", EditorStyles.boldLabel);
                // Rotate While Sliding
                door.rotateWhileSliding = EditorGUILayout.Toggle(
                    new GUIContent(
                        "Rotate While Sliding",
                        "Toggle whether the door should also rotate as it slides."
                    ),
                    door.rotateWhileSliding
                );

                if (door.rotateWhileSliding)
                {
                    // Rotation Axis During Slide
                    door.rotationAxisDuringSlide = EditorGUILayout.Vector3Field(
                        new GUIContent(
                            "Rotation Axis",
                            "Axis around which the door will rotate during its sliding motion."
                        ),
                        door.rotationAxisDuringSlide
                    );

                    // Allow Custom Rotation Speed
                    door.allowCustomRotationSpeed = EditorGUILayout.Toggle(
                        new GUIContent(
                            "Custom Rotation Speed",
                            "If enabled, you can manually set the rotation speed during sliding instead of auto-calculating."
                        ),
                        door.allowCustomRotationSpeed
                    );

                    if (door.allowCustomRotationSpeed)
                    {
                        // Rotation Speed During Slide
                        door.rotationSpeedDuringSlide = EditorGUILayout.FloatField(
                            new GUIContent(
                                "Speed (deg/s)",
                                "Degrees per second the door rotates while sliding."
                            ),
                            door.rotationSpeedDuringSlide
                        );
                    }

                    // Invert Rotation On Close
                    door.invertRotationOnClose = EditorGUILayout.Toggle(
                        new GUIContent(
                            "Reverse on Close",
                            "Reverse the direction of rotation when the door slides back (closes)."
                        ),
                        door.invertRotationOnClose
                    );
                }
                EditorGUI.EndDisabledGroup();
            });
        }

        /// <summary>
        /// Draws the animation settings for the door.
        /// </summary>
        /// <param name="door"></param>
        private void DrawAnimationSettings(Door door)
        {
            DrawTitle("-- ANIMATION SETTINGS --");
            // Now a background box for warnings + speed settings
            DrawBackgroundBox(() =>
            {
                // Door Animation On Open
                door.doorAnimationOnOpen = EditorGUILayout.CurveField(
                    new GUIContent(
                        "Door Animation On Open",
                        "This curve controls how the door transitions from closed to open over time. " +
                        "• The X-axis represents normalized time (0 to 1). " +
                        "• The Y-axis represents the fraction of the door's total movement."
                    ),
                    door.doorAnimationOnOpen
                );
                // Door Animation On Close
                door.doorAnimationOnClose = EditorGUILayout.CurveField(
                    new GUIContent(
                        "Door Animation On Close",
                        "This curve controls how the door transitions from open to closed over time. " +
                        "• The X-axis represents normalized time (0 to 1). " +
                        "• The Y-axis represents the fraction of the door's total movement."
                    ),
                    door.doorAnimationOnClose
                );
            });
        }

        /// <summary>
        /// Draws the sound settings (clips, volume, pitch) for the door.
        /// </summary>
        private void DrawSoundSettings()
        {
            DrawTitle("-- SOUND SETTINGS --");
            DrawBackgroundBox(() =>
            {
                // Serialized sound-related fields
                SerializedProperty openSounds = serializedDoor.FindProperty("OpenSounds");
                SerializedProperty closeSounds = serializedDoor.FindProperty("CloseSounds");
                SerializedProperty matchSpeedToSound = serializedDoor.FindProperty("matchSpeedToSound");
                SerializedProperty volume = serializedDoor.FindProperty("volume");

                // NEW pitch fields
                SerializedProperty useRandomPitch = serializedDoor.FindProperty("useRandomPitch");
                SerializedProperty pitchFixed = serializedDoor.FindProperty("pitch");
                SerializedProperty randomPitchMin = serializedDoor.FindProperty("randomPitchMin");
                SerializedProperty randomPitchMax = serializedDoor.FindProperty("randomPitchMax");

                /* ------------------------------------------------------------------ */
                /* Audio-clip lists                                                   */
                /* ------------------------------------------------------------------ */
                if (openSounds.arraySize == 0 && closeSounds.arraySize == 0)
                {
                    EditorGUILayout.HelpBox(
                        "No sounds are assigned for door actions. You can add sounds to enhance feedback.",
                        MessageType.Info);
                }
                EditorGUILayout.Space();

                // Open Sounds array
                EditorGUILayout.PropertyField(
                    openSounds,
                    new GUIContent(
                        "Open Sounds",
                        "A list of AudioClips randomly played when the door opens. If empty, no sound is played."
                    ),
                    true // show children (for array)
                );

                // Close Sounds array
                EditorGUILayout.PropertyField(
                    closeSounds,
                    new GUIContent(
                        "Close Sounds",
                        "A list of AudioClips randomly played when the door closes. If empty, no sound is played."
                    ),
                    true
                );

                // Match Speed To Sound
                EditorGUILayout.PropertyField(
                    matchSpeedToSound,
                    new GUIContent(
                        "Match Speed To Sound",
                        "If enabled, the door's open/close animation duration is matched to the audio clip length."
                    )
                );

                // Volume
                EditorGUILayout.PropertyField(
                    volume,
                    new GUIContent(
                        "Volume",
                        "The volume (0.0 to 1.0) at which open/close sounds are played."
                    )
                );

                /* ------------------------------------------------------------------ */
                /* Pitch section                                                      */
                /* ------------------------------------------------------------------ */
                EditorGUILayout.Space();
                EditorGUILayout.LabelField("Pitch Settings", EditorStyles.boldLabel);

                EditorGUILayout.PropertyField(
                    useRandomPitch,
                    new GUIContent("Random Pitch",
                                   "If enabled, a new random pitch is chosen for every playback."));

                if (!useRandomPitch.boolValue)
                {
                    // Fixed pitch
                    EditorGUILayout.Slider(
                        pitchFixed,
                        0.5f, 1.5f,
                        new GUIContent("Pitch",
                                       "Fixed pitch applied to every clip (0.5 – 1.5)."));
                }
                else
                {
                    // Random-range sliders
                    EditorGUILayout.Slider(
                        randomPitchMin,
                        0.5f, 1.5f,
                        new GUIContent("Min Pitch",
                                       "Lower bound of random pitch range."));

                    EditorGUILayout.Slider(
                        randomPitchMax,
                        0.5f, 1.5f,
                        new GUIContent("Max Pitch",
                                       "Upper bound of random pitch range."));
                }
            });

            // Make sure property changes are persisted
            serializedDoor.ApplyModifiedProperties();
        }


        /// <summary>
        /// Draws the mouse settings for the door.
        /// </summary>
        /// <param name="door"></param>
        private void DrawMouseSettings(Door door)
        {
            DrawTitle("-- MOUSE SETTINGS --");
            // Now a background box for warnings + speed settings
            DrawBackgroundBox(() =>
            {
                if (door.customCursor == null)
                {
                    EditorGUILayout.HelpBox("No custom cursor assigned for the door. This is optional but can improve user feedback.", MessageType.Info);
                }
                EditorGUILayout.Space();

                // Custom Cursor
                door.customCursor = (Texture2D)EditorGUILayout.ObjectField(
                    new GUIContent(
                        "Custom Cursor",
                        "The texture/image used as a cursor when hovering over the door."
                    ),
                    door.customCursor,
                    typeof(Texture2D),
                    false
                );
                // Cursor Hotspot
                door.cursorHotspot = EditorGUILayout.Vector2Field(
                    new GUIContent(
                        "Cursor Hotspot",
                        "The point within the cursor texture that acts as the 'click point'. " +
                        "For example, (0,0) is the top-left corner of the image."
                    ),
                    door.cursorHotspot
                );
            });
        }

        /// <summary>
        /// Draws the interactivity settings for the door.
        /// </summary>
        /// <param name="door"></param>"
        private void DrawInteractivitySettings()
        {
            DrawTitle("-- INTERACTIVITY SETTINGS --");
            DrawBackgroundBox(() =>
            {
                SerializedProperty componentConfigs = serializedDoor.FindProperty("componentConfigs");
                SerializedProperty objectConfigs = serializedDoor.FindProperty("objectConfigs");
                SerializedProperty onDoorOpen = serializedDoor.FindProperty("onDoorOpen");
                SerializedProperty onDoorClose = serializedDoor.FindProperty("onDoorClose");

                // Show a message if no interactivity settings are configured
                if (componentConfigs.arraySize == 0 && objectConfigs.arraySize == 0)
                {
                    EditorGUILayout.HelpBox(
                        "No interactivity settings have been configured. Add components or objects to enable interactivity.",
                        MessageType.Info
                    );
                }
                EditorGUILayout.Space();

                // Component Configs
                EditorGUILayout.PropertyField(
                    componentConfigs,
                    new GUIContent(
                        "Component Configs",
                        "List of components (e.g., scripts, colliders, renderers) that can be enabled/disabled after the door opens/closes."
                    ),
                    true
                );

                // Object Configs
                EditorGUILayout.PropertyField(
                    objectConfigs,
                    new GUIContent(
                        "Object Configs",
                        "List of GameObjects that can be activated/deactivated after the door opens/closes."
                    ),
                    true
                );

                EditorGUILayout.Space();

                // Events
                EditorGUILayout.PropertyField(
                    onDoorOpen,
                    new GUIContent(
                        "On Door Open",
                        "This UnityEvent is invoked once the door has fully opened."
                    )
                );
                EditorGUILayout.PropertyField(
                    onDoorClose,
                    new GUIContent(
                        "On Door Close",
                        "This UnityEvent is invoked once the door has fully closed."
                    )
                );
            });
        }


        /// <summary>
        /// Draws the knob settings for the door.
        /// </summary>
        /// <param name="door"></param>"
        private void DrawKnobSettings()
        {
            // Title bar
            DrawTitle("-- KNOB SETTINGS --");

            // Gray background box
            DrawBackgroundBox(() =>
            {
                // Grab relevant serialized properties
                SerializedProperty disableAtRuntime = serializedDoor.FindProperty("disableAtRuntime");
                SerializedProperty doorProp = serializedDoor.FindProperty("door");
                SerializedProperty attachedObjects = serializedDoor.FindProperty("attachedObjects");
                SerializedProperty xLeeway = serializedDoor.FindProperty("xLeeway");

                // Knob limiting & snapping properties
                SerializedProperty limitKnobToBounds = serializedDoor.FindProperty("limitKnobToBounds");
                SerializedProperty knobEnableGridSnapping = serializedDoor.FindProperty("knobEnableGridSnapping");
                SerializedProperty knobGridSize = serializedDoor.FindProperty("knobGridSize");

                // Let’s show the door reference field (optional)
                // (You can add a tooltip here too, if you want)
                EditorGUILayout.PropertyField(
                    doorProp,
                    new GUIContent("Door", "Reference to the main door GameObject for context.")
                );

                // Disable at runtime
                EditorGUILayout.PropertyField(
                    disableAtRuntime,
                    new GUIContent("Disable At Runtime", "If enabled, knob logic (or entire door logic) is skipped at runtime.")
                );

                // If no knobs
                if (attachedObjects.arraySize == 0)
                {
                    EditorGUILayout.HelpBox("No Knobs have been assigned.", MessageType.Info);
                }

                // Button to toggle “Edit Mode” for knobs
                GUIStyle buttonStyle = new GUIStyle(GUI.skin.button) { fontStyle = FontStyle.Bold };
                string editModeButtonText = isEditModeActive ? "Exit Edit Mode" : "Enter Edit Mode";
                string editModeButtonTooltip = isEditModeActive
                    ? "Stop editing knob positions in Scene view."
                    : "Begin editing knob positions in Scene view (hides default Unity handles).";

                if (GUILayout.Button(new GUIContent(editModeButtonText, editModeButtonTooltip), buttonStyle))
                {
                    isEditModeActive = !isEditModeActive;
                    Tools.current = isEditModeActive ? Tool.None : Tool.Move;
                    Tools.hidden = isEditModeActive;
                    SceneView.RepaintAll();
                }

                // If we are actively editing knobs:
                if (isEditModeActive)
                {
                    // Knob movement options (limit & grid snapping)
                    EditorGUILayout.Space();
                    EditorGUILayout.LabelField(
                        new GUIContent("Knob Movement Options", "Settings that control knob dragging constraints."),
                        EditorStyles.boldLabel
                    );

                    EditorGUILayout.Slider(
                        xLeeway,
                        0f,
                        1f,
                        new GUIContent("Leeway",
                            "Extra spacing for knob movement beyond the actual door collider bounds.")
                    );

                    EditorGUILayout.PropertyField(
                        limitKnobToBounds,
                        new GUIContent("Limit Knob to Bounds",
                            "Restrict knob movements to stay inside (or slightly around) the door’s collider.")
                    );

                    EditorGUILayout.PropertyField(
                        knobEnableGridSnapping,
                        new GUIContent("Knob Grid Snapping",
                            "If enabled, knob positions will snap to a grid when moving them in Scene view.")
                    );

                    if (knobEnableGridSnapping.boolValue)
                    {
                        EditorGUI.indentLevel++;
                        EditorGUILayout.PropertyField(
                            knobGridSize,
                            new GUIContent("Knob Grid Size",
                                "Defines the grid cell size for snapping knob movements.")
                        );
                        EditorGUI.indentLevel--;
                    }

                    EditorGUILayout.Space();
                    EditorGUILayout.LabelField("Attached Knobs", EditorStyles.boldLabel);

                    // Draw a row for each knob
                    for (int i = 0; i < attachedObjects.arraySize; i++)
                    {
                        SerializedProperty knobProperty = attachedObjects.GetArrayElementAtIndex(i);
                        SerializedProperty targetObject = knobProperty.FindPropertyRelative("targetObject");
                        SerializedProperty isLocked = knobProperty.FindPropertyRelative("isLocked");

                        Color previousColor = GUI.backgroundColor;
                        if (selectedKnobIndex == i)
                        {
                            // Indicate which knob is selected
                            GUI.backgroundColor = new Color(0.1f, 0.5f, 0.1f);
                        }

                        EditorGUILayout.BeginHorizontal(GUI.skin.box, GUILayout.ExpandWidth(true));

                        // Toggle to select/deselect the knob
                        bool isSelected = GUILayout.Toggle(
                            selectedKnobIndex == i,
                            new GUIContent("", "Select this knob for editing in the Scene view."),
                            "Toggle",
                            GUILayout.Width(20)
                        );

                        if (isSelected && selectedKnobIndex != i)
                        {
                            selectedKnobIndex = i;
                        }
                        else if (!isSelected && selectedKnobIndex == i)
                        {
                            selectedKnobIndex = -1;
                        }

                        // Knob label
                        GUILayout.Label(new GUIContent($"Knob {i + 1}", "This is knob index " + i), GUILayout.Width(80));

                        // Object field for the knob
                        GameObject knobObject = targetObject.objectReferenceValue as GameObject;
                        GameObject newKnob = (GameObject)EditorGUILayout.ObjectField(
                            new GUIContent("", "Reference to the actual knob GameObject in the scene."),
                            knobObject,
                            typeof(GameObject),
                            true,
                            GUILayout.ExpandWidth(true)
                        );

                        // If user changes the knob ref
                        if (newKnob != knobObject)
                        {
                            Undo.RecordObject(doorProp.objectReferenceValue, "Assign Knob");
                            if (newKnob != null && !newKnob.scene.IsValid())
                            {
                                // This code snippet instantiates prefabs for the knob if needed
                                GameObject instantiatedKnob = (GameObject)PrefabUtility.InstantiatePrefab(newKnob);
                                if (instantiatedKnob != null && doorProp.objectReferenceValue is GameObject doorObj)
                                {
                                    instantiatedKnob.transform.SetParent(doorObj.transform);
                                    instantiatedKnob.transform.localPosition = Vector3.zero;
                                    instantiatedKnob.transform.localRotation = Quaternion.identity;
                                    newKnob = instantiatedKnob;
                                }
                            }

                            targetObject.objectReferenceValue = newKnob;
                            EditorUtility.SetDirty(doorProp.objectReferenceValue);
                        }

                        GUILayout.Space(10);

                        // Button to lock/unlock knob editing
                        GUIStyle lockButtonStyle = new GUIStyle(GUI.skin.button) { fontStyle = FontStyle.Bold };

                        // If it's unlocked, color the button to show user it’s in “edit mode”
                        if (!isLocked.boolValue) GUI.backgroundColor = new Color(1f, 0.4f, 0f);

                        string lockButtonText = isLocked.boolValue ? "Edit Position" : "End Editing";
                        string lockButtonTooltip = isLocked.boolValue
                            ? "Click to unlock this knob for position/rotation adjustment in the Scene view."
                            : "Lock this knob position so you can’t accidentally move it.";

                        if (GUILayout.Button(new GUIContent(lockButtonText, lockButtonTooltip), lockButtonStyle, GUILayout.Width(100)))
                        {
                            // End editing for any other knobs
                            for (int j = 0; j < attachedObjects.arraySize; j++)
                            {
                                if (j == i) continue;
                                SerializedProperty otherKnobProperty = attachedObjects.GetArrayElementAtIndex(j);
                                SerializedProperty otherIsLocked = otherKnobProperty.FindPropertyRelative("isLocked");

                                // If that knob is unlocked, lock it
                                if (!otherIsLocked.boolValue)
                                {
                                    otherIsLocked.boolValue = true;
                                }
                            }

                            // Toggle locked state for the clicked knob
                            isLocked.boolValue = !isLocked.boolValue;
                            selectedKnobIndex = isLocked.boolValue ? -1 : i;
                        }

                        // Restore previous background color
                        GUI.backgroundColor = previousColor;
                        GUILayout.Space(10);

                        // Right before drawing each knob row:
                        bool isEditingThisKnob = !isLocked.boolValue; // if isLocked == false, we are "editing" the knob

                        // Choose appropriate tooltip based on edit mode:
                        GUIContent removeContent = isEditingThisKnob
                            ? new GUIContent(
                                "Remove",
                                "The Remove button is disabled while you're editing the position. \n(First press the 'End Editing' button then you can safely remove this knob)"
                              )
                            : new GUIContent(
                                "Remove",
                                "Remove this knob from the list and delete the knob GameObject from the scene."
                              );

                        // Now disable the button if we’re in edit mode for this knob:
                        EditorGUI.BeginDisabledGroup(isEditingThisKnob);
                        if (GUILayout.Button(removeContent, GUILayout.Width(80)))
                        {
                            if (knobObject != null && knobObject.scene.IsValid())
                            {
                                Undo.DestroyObjectImmediate(knobObject);
                            }
                            attachedObjects.DeleteArrayElementAtIndex(i);
                            continue;
                        }
                        EditorGUI.EndDisabledGroup();


                        EditorGUILayout.EndHorizontal();
                        GUI.backgroundColor = previousColor;
                    }

                    // Button to add a new knob
                    string addKnobTooltip = "Add a new slot for a knob GameObject.";
                    if (GUILayout.Button(new GUIContent("Add Knob", addKnobTooltip)))
                    {
                        Undo.RecordObject(doorProp.objectReferenceValue, "Add Knob");
                        attachedObjects.InsertArrayElementAtIndex(attachedObjects.arraySize);

                        // Initialize the newly inserted element
                        SerializedProperty newKnobProperty = attachedObjects.GetArrayElementAtIndex(attachedObjects.arraySize - 1);
                        SerializedProperty newTargetObject = newKnobProperty.FindPropertyRelative("targetObject");
                        SerializedProperty newIsLocked = newKnobProperty.FindPropertyRelative("isLocked");

                        newTargetObject.objectReferenceValue = null;
                        newIsLocked.boolValue = true;
                    }
                }

                // Apply modifications to the serialized fields
                serializedDoor.ApplyModifiedProperties();
            });
        }




        /// <summary>
        /// Draws the material settings for the door.
        /// </summary>
        /// <param name="door"></param>"
        private void DrawMaterialSettings()
        {
            DrawTitle("-- HIGHLIGHTING SETTINGS --");
            DrawBackgroundBox(() =>
            {
                // Grab serialized properties
                SerializedProperty mode = serializedDoor.FindProperty("mode");
                SerializedProperty targetObjects = serializedDoor.FindProperty("targetObjects");
                SerializedProperty highlightMaterial = serializedDoor.FindProperty("highlightMaterial");
                SerializedProperty brightnessFactor = serializedDoor.FindProperty("brightnessFactor");
                SerializedProperty fadeDuration = serializedDoor.FindProperty("fadeDuration");
                SerializedProperty loopBrightnessEffect = serializedDoor.FindProperty("loopBrightnessEffect");
                SerializedProperty raycastLayerMask = serializedDoor.FindProperty("raycastLayerMask");
                SerializedProperty raycastRange = serializedDoor.FindProperty("raycastRange");

                // Check for missing Renderers on assigned target objects
                for (int i = 0; i < targetObjects.arraySize; i++)
                {
                    SerializedProperty targetObjectProp = targetObjects.GetArrayElementAtIndex(i);
                    if (targetObjectProp.objectReferenceValue is GameObject targetObj &&
                        targetObj.GetComponent<Renderer>() == null)
                    {
                        EditorGUILayout.HelpBox(
                            $"The Target Object '{targetObj.name}' does not have a Renderer component. " +
                            "Materials may not render as expected.",
                            MessageType.Warning
                        );
                    }
                }

                // If no Target Objects are assigned
                if (targetObjects.arraySize == 0)
                {
                    EditorGUILayout.HelpBox(
                        "No Target Objects are assigned. Add at least one object to use materials effectively.",
                        MessageType.Info
                    );
                }

                // Draw Highlight Mode
                EditorGUILayout.PropertyField(
                    mode,
                    new GUIContent(
                        "Highlight Mode",
                        "Select how the door highlights when hovered by the mouse:\n" +
                        "• None: No highlight.\n" +
                        "• MaterialChanger: Replace original materials with a highlight material.\n" +
                        "• MaterialBrightness: Increase material brightness dynamically."
                    )
                );

                // Draw Target Objects
                EditorGUILayout.PropertyField(
                    targetObjects,
                    new GUIContent(
                        "Target Objects",
                        "These objects will be affected by the highlight or brightness effect. " +
                        "If empty, the door script automatically add its own GameObject."
                    ),
                    true
                );

                // Determine current mode
                Door.ModeSelect currentMode = (Door.ModeSelect)mode.enumValueIndex;

                // Conditionally display each section based on the mode
                switch (currentMode)
                {
                    case Door.ModeSelect.MaterialChanger:
                        // If MaterialChanger is selected but no highlight material is assigned
                        if (highlightMaterial.objectReferenceValue == null)
                        {
                            EditorGUILayout.HelpBox(
                                "No Highlight Material assigned. Please assign one for MaterialChanger mode.",
                                MessageType.Warning
                            );
                        }

                        // Highlight Material
                        EditorGUILayout.PropertyField(
                            highlightMaterial,
                            new GUIContent(
                                "Highlight Material",
                                "The material used to visually highlight objects when hovered (MaterialChanger mode)."
                            )
                        );
                        break;

                    case Door.ModeSelect.MaterialBrightness:
                        // Brightness Factor
                        EditorGUILayout.PropertyField(
                            brightnessFactor,
                            new GUIContent(
                                "Brightness Factor",
                                "How much brighter to make the material when hovered. For example, 1.2 = 20% brighter."
                            )
                        );

                        // Fade Duration
                        EditorGUILayout.PropertyField(
                            fadeDuration,
                            new GUIContent(
                                "Fade Duration",
                                "Time in seconds for the brightness to fade in/out in MaterialBrightness mode."
                            )
                        );

                        // Loop Brightness Effect
                        EditorGUILayout.PropertyField(
                            loopBrightnessEffect,
                            new GUIContent(
                                "Loop Brightness Effect",
                                "If enabled, the highlight/brightness effect repeats (fading in and out) while hovered."
                            )
                        );
                        break;

                    case Door.ModeSelect.None:
                    default:
                        EditorGUILayout.HelpBox(
                            "Highlight Mode is set to 'None'. No highlight behavior will be applied.",
                            MessageType.Info
                        );
                        break;
                }

                EditorGUILayout.Space();

                // Raycast Settings (always shown)
                EditorGUILayout.PropertyField(
                    raycastLayerMask,
                    new GUIContent(
                        "Raycast Layer Mask",
                        "Which layers are included for mouse hover detection."
                    )
                );

                EditorGUILayout.PropertyField(
                    raycastRange,
                    new GUIContent(
                        "Raycast Range",
                        "Max distance for detecting mouse hover on the door or target objects."
                    )
                );
            });
        }
        /// <summary>
        /// Draws a horizontal line in the editor layout.
        /// </summary>
        ///
        private void DrawHorizontalLine()
        {
            EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);
        }

        /// <summary>
        /// Draws the preset buttons for positioning the door.
        /// </summary>
        /// <param name="door"></param>
        private void DrawPresetButtons(Door door)
        {
            // Style for the preset buttons
            GUIStyle buttonStyle = new GUIStyle(GUI.skin.button)
            {
                fixedWidth = 100,
                fixedHeight = 50,
                alignment = TextAnchor.MiddleCenter,
                wordWrap = true,
                fontSize = 12,
                fontStyle = FontStyle.Bold
            };

            // TOP row
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();


            if (GUILayout.Button(new GUIContent("TOP LEFT", "Automatically positions the pivot at the top left corner of the door bounds."), buttonStyle))
            {
                // Update bounds BEFORE applying the preset
                door.UpdateDoorBounds();
                ApplyPreset(door, PositionPreset.TopLeft);
            }

            if (GUILayout.Button(new GUIContent("TOP MIDDLE", "Automatically positions the pivot at the top center of the door bounds."), buttonStyle))
            {
                door.UpdateDoorBounds();
                ApplyPreset(door, PositionPreset.TopMiddle);
            }

            if (GUILayout.Button(new GUIContent("TOP RIGHT", "Automatically positions the pivot at the top right corner of the door bounds."), buttonStyle))
            {
                door.UpdateDoorBounds();
                ApplyPreset(door, PositionPreset.TopRight);
            }

            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();

            // MIDDLE row
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();

            if (GUILayout.Button(new GUIContent("MIDDLE LEFT", "Automatically positions the pivot at the left center of the door bounds."), buttonStyle))
            {
                door.UpdateDoorBounds();
                ApplyPreset(door, PositionPreset.MiddleLeft);
            }

            if (GUILayout.Button(new GUIContent("CENTER", "Automatically positions the pivot at the center of the door bounds."), buttonStyle))
            {
                door.UpdateDoorBounds();
                ApplyPreset(door, PositionPreset.Center);
            }

            if (GUILayout.Button(new GUIContent("MIDDLE RIGHT", "Automatically positions the pivot at the right center of the door bounds."), buttonStyle))
            {
                door.UpdateDoorBounds();
                ApplyPreset(door, PositionPreset.RightMiddle);
            }

            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();

            // BOTTOM row
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();

            if (GUILayout.Button(new GUIContent("BOTTOM LEFT", "Automatically positions the pivot at the bottom left corner of the door bounds."), buttonStyle))
            {
                door.UpdateDoorBounds();
                ApplyPreset(door, PositionPreset.BottomLeft);
            }

            if (GUILayout.Button(new GUIContent("BOTTOM MIDDLE", "Automatically positions the pivot at the bottom center of the door bounds."), buttonStyle))
            {
                door.UpdateDoorBounds();
                ApplyPreset(door, PositionPreset.BottomMiddle);
            }

            if (GUILayout.Button(new GUIContent("BOTTOM RIGHT", "Automatically positions the pivot at the bottom right corner of the door bounds."), buttonStyle))
            {
                door.UpdateDoorBounds();
                ApplyPreset(door, PositionPreset.RightBottom);
            }

            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
        }

        /// <summary>
        /// Clone a scriptable object.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="original"></param>
        /// <returns></returns>
        public static T CloneScriptableObject<T>(T original) where T : ScriptableObject
        {
            T instance = ScriptableObject.CreateInstance<T>();
            JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(original), instance);
            return instance;
        }

        /// <summary>
        /// Draws the settings for the door delays.
        /// </summary>
        /// <param name="door"></param>
        private void DrawDelaySettings(Door door)
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Delay Settings", EditorStyles.boldLabel);
            door.OpenDelay = EditorGUILayout.FloatField(new GUIContent("Open Delay (seconds)", "Time to wait before the door begins opening."), door.OpenDelay);
            door.CloseDelay = EditorGUILayout.FloatField(new GUIContent("Close Delay (seconds)", "Time to wait before the door begins closing."), door.CloseDelay);
            EditorGUILayout.Space();
        }
        /// <summary>
        /// Draws the settings for the door enablers.
        /// </summary>
        /// <param name="door"></param>
        private void DrawDoorEnablers(Door door)
        {
            // Other General Settings
            door.disableColliderWhileMoving = EditorGUILayout.Toggle(new GUIContent("Disable Collider While Moving", "Disables the collider when the door is in motion, re-enabling it when the door stops."), door.disableColliderWhileMoving);
            door.disablenavmeshobstacle = EditorGUILayout.Toggle(new GUIContent("Disable NavMeshObstacle", "Disables NavMeshObstacle when the door is in motion, re-enabling it when the door stops."), door.disablenavmeshobstacle);
        }
        /// <summary>
        /// Draws the settings for the event channels.
        /// </summary>
        /// <param name="door"></param>
        private void DrawEventChannels(Door door)
        {
            EditorGUILayout.LabelField("Event Channels", EditorStyles.boldLabel);

            EditorGUILayout.BeginHorizontal();
            door.doorEventChannel = (DoorEventChannel)EditorGUILayout.ObjectField(
                new GUIContent("Door Event Channel", "Assign the event channel used for handling door interactions. If missing, create a new one using the 'NEW' button."),
                door.doorEventChannel,
                typeof(DoorEventChannel),
                true
            );

            // Instead of opening a panel *right now*, set a flag:
            if (GUILayout.Button("NEW", GUILayout.Width(50)))
            {
                pendingEventChannelCreation = true;
                doorPendingChannel = door;
            }
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.Space();
        }


        /// <summary>
        /// Draws the settings for the dual door feature.
        /// </summary>
        /// <param name="door"></param>
        private void DrawDualDoorSettings(Door door)
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Dual Door Settings", EditorStyles.boldLabel);

            if (!door.isDualDoor)
            {
                // Display the "Add a Second Door" option
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField(new GUIContent("Add a Second Door", "Enables linking this door to another door that opens simultaneously."));

                if (GUILayout.Button("NEW", GUILayout.Width(50)))
                {
                    // Enable dual door mode
                    door.isDualDoor = true;
                    EditorUtility.SetDirty(door); // Mark as dirty to save changes
                }
                EditorGUILayout.EndHorizontal();
            }
            else
            {
                EditorGUI.BeginChangeCheck();

                // Display the secondDoor field and the DELETE button on the same line
                EditorGUILayout.BeginHorizontal();

                // Allow the user to assign either a prefab or a GameObject from the hierarchy
                Door selectedDoor = (Door)EditorGUILayout.ObjectField(
                    new GUIContent("Second Door", "Assign the second door to be linked with this one."),
                    door.secondDoor,
                    typeof(Door),
                    true
                );

                if (selectedDoor != null && selectedDoor != door.secondDoor)
                {
                    if (PrefabUtility.IsPartOfPrefabAsset(selectedDoor.gameObject))
                    {
                        // Instantiate the prefab if it's a prefab asset
                        door.secondDoor = AddSecondDoorToHierarchy(door, selectedDoor.gameObject);
                    }
                    else
                    {
                        // Directly assign the GameObject from the hierarchy
                        door.secondDoor = selectedDoor;
                    }
                }

                // DELETE button
                if (GUILayout.Button("DELETE", GUILayout.Width(60)))
                {
                    if (door.secondDoor != null)
                    {
                        if (PrefabUtility.IsPartOfPrefabAsset(door.secondDoor.gameObject))
                        {
                            // Destroy the instantiated GameObject if it was created from a prefab
                            DestroyImmediate(door.secondDoor.gameObject);
                        }

                        // Unlink the reference
                        door.secondDoor = null;
                    }

                    // Disable dual door mode
                    door.isDualDoor = false;
                    EditorUtility.SetDirty(door); // Mark as dirty to save changes
                }

                EditorGUILayout.EndHorizontal();

                if (EditorGUI.EndChangeCheck())
                {
                    // Automatically disable dual door mode if the field is cleared manually
                    if (door.secondDoor == null)
                    {
                        door.isDualDoor = false;
                        EditorUtility.SetDirty(door); // Save changes
                    }
                }
            }
        }

        /// <summary>
        /// Draws the settings for the lever control.
        /// </summary>
        /// <param name="door"></param>
        private void DrawLeverSettings(Door door)
        {
            // Main label with tooltip
            EditorGUILayout.LabelField(
                new GUIContent("Lever Settings", "Configuration for lever-based door control."),
                EditorStyles.boldLabel
            );

            // Fetch the serialized properties
            SerializedProperty isLeverControlledProp = serializedDoor.FindProperty("IsLeverControlled");
            SerializedProperty leverProp = serializedDoor.FindProperty("Lever");

            serializedDoor.Update();

            // If the door is not lever-controlled yet
            if (!isLeverControlledProp.boolValue)
            {
                // Prompt the user to enable lever control
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField(
                    new GUIContent("Add a Lever", "Enable lever control for this door."),
                    GUILayout.Width(EditorGUIUtility.labelWidth - 4)
                );

                if (GUILayout.Button(
                    new GUIContent("NEW", "Enable lever control and assign a lever prefab or instance."),
                    GUILayout.Width(50)
                ))
                {
                    isLeverControlledProp.boolValue = true;
                }
                EditorGUILayout.EndHorizontal();
            }
            else
            {
                // If lever is enabled, show the lever object field
                EditorGUI.BeginChangeCheck();
                EditorGUILayout.BeginHorizontal();

                // Lever reference with a tooltip
                EditorGUILayout.PropertyField(
                    leverProp,
                    new GUIContent("Lever", "Reference to the lever GameObject controlling this door.")
                );

                // If the lever field changes (e.g., a new prefab is assigned)...
                if (EditorGUI.EndChangeCheck() && leverProp.objectReferenceValue is GameObject leverPrefab)
                {
                    // ... instantiate the prefab in-scene (custom logic)
                    InstantiateLeverInScene(door, leverPrefab);
                }

                // Delete button to remove the lever
                if (GUILayout.Button(
                    new GUIContent("DELETE", "Remove this lever and disable lever control."),
                    GUILayout.Width(60)
                ))
                {
                    if (leverProp.objectReferenceValue != null)
                    {
                        GameObject leverInstance = leverProp.objectReferenceValue as GameObject;
                        if (leverInstance != null && leverInstance.scene.IsValid())
                        {
                            Undo.DestroyObjectImmediate(leverInstance);
                        }
                        leverProp.objectReferenceValue = null;
                    }

                    // Turn off lever control
                    isLeverControlledProp.boolValue = false;
                }

                EditorGUILayout.EndHorizontal();
            }

            // Apply property modifications
            serializedDoor.ApplyModifiedProperties();
        }
        #endregion
        #region Helper / Utility Methods

        /// <summary>
        /// Loads an icon texture from the specified path.
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        private Texture2D LoadIcon(string path)
        {
            Texture2D tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (tex == null)
            {
                DoorLogger.LogWarning($"[DoorEditor] Could not find icon at path: {path}");
            }
            return tex;
        }

        /// <summary>
        /// Creates a new DoorEventChannel asset and assigns it to the door.
        /// </summary>
        /// <param name="door"></param>
        private void CreateNewDoorEventChannel(Door door)
        {
            // Show the file panel here - no layout mismatch occurs
            string path = EditorUtility.SaveFilePanelInProject(
                "Create Door Event Channel",
                "NewDoorEventChannel",
                "asset",
                "Specify where to save the new DoorEventChannel."
            );
            if (!string.IsNullOrEmpty(path))
            {
                DoorEventChannel newChannel = ScriptableObject.CreateInstance<DoorEventChannel>();
                AssetDatabase.CreateAsset(newChannel, path);
                AssetDatabase.SaveAssets();
                door.doorEventChannel = newChannel;
                EditorUtility.SetDirty(door);
            }
        }

        /// <summary>
        /// Adds a second door to the hierarchy and associates it with the door.
        /// </summary>
        /// <param name="door"></param>
        /// <param name="secondDoorPrefab"></param>
        /// <returns></returns>
        private Door AddSecondDoorToHierarchy(Door door, GameObject secondDoorPrefab)
        {
            if (secondDoorPrefab == null || door == null) return null;

            // Instantiate the prefab as a child of the root object
            GameObject secondDoorInstance = PrefabUtility.InstantiatePrefab(secondDoorPrefab) as GameObject;

            if (secondDoorInstance == null)
            {
                DoorLogger.LogError("Failed to instantiate the second door prefab.");
                return null;
            }

            // Parent the instance to the root object of the door
            Transform root = door.transform.root;
            secondDoorInstance.transform.SetParent(root, false);
            secondDoorInstance.name = $"{door.name}_SecondDoor";

            EditorUtility.SetDirty(secondDoorInstance);
            return secondDoorInstance.GetComponent<Door>();
        }

        /// <summary>
        /// Instantiates a lever prefab in the scene and associates it with the door.
        /// </summary>
        /// <param name="door"></param>
        /// <param name="leverPrefab"></param>
        private void InstantiateLeverInScene(Door door, GameObject leverPrefab)
        {
            if (leverPrefab == null)
            {
                DoorLogger.LogWarning("No lever prefab assigned. Please assign a prefab before instantiating.");
                return;
            }

            // Check if the prefab is already instantiated in the scene
            if (leverPrefab.scene.IsValid())
            {
                DoorLogger.LogWarning("The lever prefab is already instantiated in the scene.");
                return;
            }

            // Instantiate the prefab in the same scene as the door
            GameObject leverInstance = (GameObject)PrefabUtility.InstantiatePrefab(leverPrefab, door.gameObject.scene);

            if (leverInstance != null)
            {
                leverInstance.name = $"{door.name}_Lever";

                // Make sure we parent the lever to the same parent as the Door
                Transform doorParent = door.transform.parent;
                if (doorParent != null)
                {
                    leverInstance.transform.SetParent(doorParent, false);
                }
                else
                {
                    // If the door has no parent, the lever will remain a root object in the scene
                    leverInstance.transform.SetParent(null, false);
                }

                // Register undo for proper editor support
                Undo.RegisterCreatedObjectUndo(leverInstance, "Instantiate Lever");
                EditorUtility.SetDirty(leverInstance);

                // Update the lever field in the door
                SerializedObject serializedDoor = new SerializedObject(door);
                SerializedProperty leverProp = serializedDoor.FindProperty("Lever");
                leverProp.objectReferenceValue = leverInstance;
                serializedDoor.ApplyModifiedProperties();
            }
            else
            {
                DoorLogger.LogError("Failed to instantiate the lever prefab.");
            }
        }

        /// <summary>
        /// Adds a lever to the hierarchy and associates it with the door.
        /// </summary>
        /// <param name="door"></param>
        /// <param name="leverPrefab"></param>
        /// <returns></returns>
        private GameObject AddLeverToHierarchy(Door door, GameObject leverPrefab)
        {
            if (leverPrefab == null || door == null) return null;

            // Instantiate a new lever in the scene if it doesn't already exist
            GameObject leverInstance = leverPrefab.scene.IsValid() ? leverPrefab : PrefabUtility.InstantiatePrefab(leverPrefab) as GameObject;

            if (leverInstance == null)
            {
                DoorLogger.LogError("Failed to instantiate the lever prefab.");
                return null;
            }

            // Find the root object two levels up
            Transform parent = door.transform;
            for (int i = 0; i < 2; i++)
            {
                if (parent.parent != null)
                {
                    parent = parent.parent;
                }
                else
                {
                    DoorLogger.LogError("The door hierarchy does not have enough levels for placing the lever.");
                    DestroyImmediate(leverInstance); // Clean up the instance if placement fails
                    return null;
                }
            }

            // Parent the lever instance to the root object
            leverInstance.transform.SetParent(parent, false);
            leverInstance.name = $"{door.name}_Lever";

            EditorUtility.SetDirty(leverInstance);
            return leverInstance;
        }

        /// <summary>
        /// Creates a new lever instance and adds it to the hierarchy.
        /// </summary>
        /// <param name="door">The door to associate the lever with.</param>
        private void CreateNewLever(Door door)
        {
            if (door.IsLeverControlled || leverInstance != null) return;

            GameObject leverPrefab = Resources.Load<GameObject>("LeverPrefab"); // Ensure you have a prefab named "LeverPrefab" in a Resources folder
            if (leverPrefab == null)
            {
                DoorLogger.LogError("LeverPrefab not found in Resources. Please add a LeverPrefab to Resources folder.");
                return;
            }

            leverInstance = (GameObject)PrefabUtility.InstantiatePrefab(leverPrefab);
            AddLeverToHierarchy(door, leverInstance);

            door.IsLeverControlled = true;
        }

        /// <summary>
        /// Draws the settings for door presets.
        /// </summary>
        /// <param name="door"></param>
        /// <param name="preset"></param>
        private void ApplyPreset(Door door, ProjectDoors.PositionPreset preset)
        {
            door.positionPreset = preset;
            door.ApplyPositionPreset();
            EditorUtility.SetDirty(door); // Ensure the changes are saved
        }
        /// <summary>
        /// Draws the preset buttons for automatic repositioning of the pivot.
        /// </summary>
        /// <param name="door"></param>
        /// <param name="baseName"></param>
        /// <returns></returns>
        private string GenerateUniquePivotName(Door door, string baseName)
        {
            string uniqueName;
            int attempt = 0;
            do
            {
                uniqueName = baseName + Random.Range(1, 1001);
                attempt++;
            } while (door.transform.Find(uniqueName) != null && attempt < 1000);

            if (attempt >= 1000)
            {
                DoorLogger.LogError("Failed to generate a unique pivot name after 1000 attempts.");
                uniqueName = baseName + "Fallback";
            }

            return uniqueName;
        }

        #endregion
        #region Validation or Internal Checks
        /// <summary>
        /// Validates the serialized objects in the targetObjects list.
        /// </summary>
        private void ValidateSerializedObjects()
        {
            // Ensure all target objects in the serialized list are valid
            for (int i = 0; i < targetObjects.arraySize; i++)
            {
                SerializedProperty obj = targetObjects.GetArrayElementAtIndex(i);
                if (obj.objectReferenceValue == null)
                {
                    DoorLogger.LogWarning($"Null object at index {i} in targetObjects. Removing entry.");
                    targetObjects.DeleteArrayElementAtIndex(i);
                    i--; // Adjust index after deletion
                }
            }

            serializedDoor.ApplyModifiedProperties();
        }

        public void DeleteOldParent(Door door)
        {
            if (door.doorObject != null &&
                door.doorObject.transform.parent != null &&
                (door.pivot == null || door.doorObject.transform.parent != door.pivot))
            {
                Transform oldParent = door.doorObject.transform.parent;
                string oldParentName = oldParent.gameObject.name; // Store name before destruction
                CreateParentForDoorObject(door);
                if (oldParent != null)
                {
                    Undo.DestroyObjectImmediate(oldParent.gameObject);
                    DoorLogger.Log($"Deleted previous parent: {oldParentName}");
                }
            }

        }
        #endregion
        #region GUI Helpers
        /// <summary>
        /// Draws a titled box with a bold label at the top and then
        /// executes the given content inside a single vertical section.
        /// </summary>
        private void DrawTitle(string title)
        {
            // Optional extra spacing before
            //EditorGUILayout.Space();

            // First vertical is just for background color
            Color backgroundColor = new Color(0.30f, 0.30f, 0.30f, 1f);
            Rect sectionRect = EditorGUILayout.BeginVertical();
            EditorGUILayout.Space(2);
            sectionRect.x += 5;
            sectionRect.width -= 10;
            sectionRect.height += 15;
            EditorGUI.DrawRect(sectionRect, backgroundColor);

            // Second vertical is the "box" style
            GUIStyle borderStyle = new GUIStyle(GUI.skin.box)
            {
                padding = new RectOffset(15, 3, 3, 0),
                margin = new RectOffset(10, 10, 3, 0),
                border = new RectOffset(20, 20, 3, 0),
            };
            EditorGUILayout.BeginVertical(borderStyle);

            // Draw the actual title label
            EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
            EditorGUILayout.Space(4);
            EditorGUILayout.EndVertical();
            EditorGUILayout.EndVertical();

            // Optional spacing after
            //EditorGUILayout.Space();
        }
        /// <summary>
        /// Draws a background "box" for the provided content
        /// without introducing extra unmatched Begin/End calls.
        /// </summary>
        private void DrawBackgroundBox(System.Action content)
        {
            // Optional spacing before
            //EditorGUILayout.Space();

            // 1) Begin a vertical group with "Box" style
            //EditorGUILayout.BeginVertical("Box");
            // Define the background color
            Color backgroundColor = new Color(0.30f, 0.30f, 0.30f, 1f); // #303030

            // Reserve space for the background box
            Rect sectionRect = EditorGUILayout.BeginVertical();
            EditorGUILayout.Space(5); // Add space for rounded border
            sectionRect.x += 5; // Add horizontal padding/margin
            sectionRect.width -= 10; // Reduce width for padding/margin
            sectionRect.height += 5; // Adjust height for padding

            // Draw the background box
            EditorGUI.DrawRect(sectionRect, backgroundColor);

            // Style for the inner content (border and padding)
            GUIStyle borderStyle = new GUIStyle(GUI.skin.box)
            {
                padding = new RectOffset(15, 15, 15, 15), // Padding inside the border
                margin = new RectOffset(10, 10, 0, 10), // Margin around the border
                border = new RectOffset(20, 20, 0, 20), // Thickness for pronounced rounded corners
            };

            // Start the inner bordered section
            EditorGUILayout.BeginVertical(borderStyle);
            // Optional extra spacing inside the box
            EditorGUILayout.Space(4);

            // 2) Invoke the caller's content
            content?.Invoke();

            // Optional extra spacing at the end of the box
            EditorGUILayout.Space(4);

            // 3) End the vertical group
            //EditorGUILayout.EndVertical();
            EditorGUILayout.EndVertical();
            EditorGUILayout.EndVertical();

            // Optional spacing after
            EditorGUILayout.Space();
        }

        /// <summary>
        /// Ends the titled box vertical groups.
        /// </summary>
        private void EndTitledBox()
        {
            EditorGUILayout.EndVertical(); // end of borderStyle vertical
            EditorGUILayout.EndVertical(); // end of backgroundColor vertical
            EditorGUILayout.Space();
        }

        /// <summary>
        /// Generates a unique name for the pivot based on the provided base name.
        /// </summary>
        /// <param name="door"></param>
        /// <param name="message"></param>
        private void DrawCustomErrorHelpBox(Door door, string message)
        {
            GUIStyle errorBoxStyle = new GUIStyle(EditorStyles.helpBox)
            {
                normal = { background = Texture2D.whiteTexture, textColor = Color.white },
                padding = new RectOffset(10, 10, 10, 10),
                fontSize = 12,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft
            };

            // Save the current GUI color and change it temporarily
            Color originalColor = GUI.backgroundColor;
            GUI.backgroundColor = Color.red; // Red background

            EditorGUILayout.BeginVertical(errorBoxStyle);
            EditorGUILayout.LabelField(message, EditorStyles.wordWrappedLabel);

            // Add "Create Parent" button
            if (GUILayout.Button("Create Parent"))
            {
                CreateParentForDoorObject(door);
            }

            EditorGUILayout.EndVertical();

            // Restore the original GUI color
            GUI.backgroundColor = originalColor;
        }

        /// <summary>
        /// Generates a unique name for the pivot based on the provided base name.
        /// </summary>
        /// <param name="door"></param>
        private void CreateParentForDoorObject(Door door)
        {
            if (door.doorObject == null) return;

            // Create a new parent object
            GameObject parentObject = new GameObject($"{door.doorObject.name}_Parent");
            Undo.RegisterCreatedObjectUndo(parentObject, "Create Parent");
            parentObject.transform.position = door.doorObject.transform.position;

            // Re-parent the Door Object
            Undo.SetTransformParent(door.doorObject.transform, parentObject.transform, "Reparent Door Object");

            // Refresh the Door Object to find the proper pivot
            door.EnsureAttachedObject();
            EditorUtility.SetDirty(door);

            DoorLogger.Log($"Created parent object: {parentObject.name}, re-parented Door Object, and refreshed pivot.");
        }

        /// <summary>
        /// Draws a custom HelpBox with a light green background.
        /// </summary>
        /// <param name="message">The message to display in the HelpBox.</param>
        private void DrawCustomHelpBox(string message)
        {
            GUIStyle helpBoxStyle = new GUIStyle(EditorStyles.helpBox)
            {
                normal = { background = Texture2D.whiteTexture, textColor = new Color(1f, 1f, 1f, 0.7f) },
                padding = new RectOffset(10, 10, 10, 10),
                fontSize = 12,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft
            };

            // Save the current GUI color and change it temporarily
            Color originalColor = GUI.backgroundColor;
            GUI.backgroundColor = new Color(15 / 255f, 53 / 255f, 25 / 255f); // Light green

            EditorGUILayout.LabelField(message, helpBoxStyle);

            // Restore the original GUI color
            GUI.backgroundColor = originalColor;
        }
        #endregion
    }
}