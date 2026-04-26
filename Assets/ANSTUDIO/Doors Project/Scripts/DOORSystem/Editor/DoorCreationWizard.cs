#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using System.IO;
namespace ProjectDoors
{
    public class DoorSetupWizard : EditorWindow
    {
        private const float WindowWidth = 460f;
        private const float WindowHeight = 408f;

        // Background image
        private static Texture2D logoTexture;

        // Possible wizard steps
        private enum WizardStep
        {
            Welcome,
            PlayerQuestion,
            CharacterReplacer,
            DoorReplacer   // <-- NEW STEP
        }

        private WizardStep currentStep = WizardStep.Welcome;

        // For CharacterReplacer
        private Editor characterReplacerEditor;
        private GameObject tempCharacterGO;
        private bool characterReplacerExists;

        // For DoorReplacer (new)
        private Editor doorReplacerEditor;
        private GameObject tempDoorGO;
        private bool doorReplacerExists;

        // We'll track scrolling in the CharacterReplacer step (though currently unused)
        private Vector2 replacerScrollPos;

        [MenuItem("Tools/DOORS Project/Setup Wizard")]
        public static void OpenWindow()
        {
            var window = GetWindow<DoorSetupWizard>(true, "Setup Wizard");

            // Lock the window size
            window.minSize = new Vector2(WindowWidth, WindowHeight);
            window.maxSize = new Vector2(WindowWidth, WindowHeight);

            // Center on the main Editor window
            Rect mainPos = EditorGUIUtility.GetMainWindowPosition();
            float centerX = mainPos.x + (mainPos.width - WindowWidth) * 0.5f;
            float centerY = mainPos.y + (mainPos.height - WindowHeight) * 0.5f;
            window.position = new Rect(centerX, centerY, WindowWidth, WindowHeight);

            // Load the background image if not already loaded
            if (logoTexture == null)
            {
                logoTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(
                    "Assets/ANSTUDIO/Doors Project/Demo/Textures/General/Logo.png"
                );
                if (logoTexture == null)
                {
                    Debug.LogWarning("Logo.png not found at the expected path.");
                }
            }

            window.Show();
        }

        private void OnEnable()
        {
            // --- Load CharacterReplacer ---
            string relativePathChar = "Assets/ANSTUDIO/Doors Project/Scripts/CharacterReplacer/CharacterReplacer.cs";
            string absolutePathChar = Path.Combine(Application.dataPath, "ANSTUDIO/Doors Project/Scripts/CharacterReplacer/CharacterReplacer.cs");
            characterReplacerExists = File.Exists(absolutePathChar);

            if (characterReplacerExists)
            {
                var scriptObjChar = AssetDatabase.LoadAssetAtPath<MonoScript>(relativePathChar);
                if (scriptObjChar != null)
                {
                    // Create a hidden GameObject for the CharacterReplacer component
                    tempCharacterGO = new GameObject("Temp_CharacterReplacer");
                    tempCharacterGO.hideFlags = HideFlags.HideAndDontSave | HideFlags.NotEditable;

                    System.Type typeChar = scriptObjChar.GetClass();
                    if (typeChar != null)
                    {
                        var component = tempCharacterGO.AddComponent(typeChar);
                        if (component != null)
                        {
                            characterReplacerEditor = Editor.CreateEditor(component);
                        }
                    }
                }
            }

            // We will load DoorReplacer **only** when user presses "DOOR SETUP" (see method PrepareDoorReplacer).
        }

        private void OnDisable()
        {
            // Clean up CharacterReplacer
            if (characterReplacerEditor != null)
            {
                DestroyImmediate(characterReplacerEditor);
            }
            if (tempCharacterGO != null)
            {
                DestroyImmediate(tempCharacterGO);
            }

            // Clean up DoorReplacer
            if (doorReplacerEditor != null)
            {
                DestroyImmediate(doorReplacerEditor);
            }
            if (tempDoorGO != null)
            {
                DestroyImmediate(tempDoorGO);
            }
        }

        private void OnGUI()
        {
            // Draw the background image at original size, centered
            if (logoTexture != null)
            {
                float xPos = (position.width - logoTexture.width) * 0.5f;
                float yPos = (position.height - logoTexture.height) * 0.5f;
                var bgRect = new Rect(xPos, yPos, logoTexture.width, logoTexture.height);
                GUI.DrawTexture(bgRect, logoTexture, ScaleMode.StretchToFill);
            }

            switch (currentStep)
            {
                case WizardStep.Welcome:
                    DrawWelcomeScreen();
                    break;
                case WizardStep.PlayerQuestion:
                    DrawPlayerQuestionScreen();
                    break;
                case WizardStep.CharacterReplacer:
                    DrawCharacterReplacerScreen();
                    break;
                case WizardStep.DoorReplacer:
                    DrawDoorReplacerScreen(); // <-- NEW step for DoorReplacer
                    break;
            }
        }

        // -------------------------------------------------------
        // Shared Styles
        // -------------------------------------------------------
        private GUIStyle TitleStyle
        {
            get
            {
                var style = new GUIStyle(GUI.skin.label)
                {
                    alignment = TextAnchor.MiddleCenter,
                    fontSize = 18,
                    fontStyle = FontStyle.Bold
                };
                return style;
            }
        }

        private GUIStyle CenteredStyle
        {
            get
            {
                return new GUIStyle(GUI.skin.label)
                {
                    alignment = TextAnchor.MiddleCenter,
                    fontSize = 14,
                    richText = true, // Enable rich text support
                    wordWrap = true
                };
            }
        }

        // -------------------------------------------------------
        // STEP 1: WELCOME
        // -------------------------------------------------------
        private void DrawWelcomeScreen()
        {
            // A smaller text style under the button
            var bottomStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 12
            };

            EditorGUILayout.BeginVertical();
            GUILayout.Space(10);

            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(10);

            EditorGUILayout.BeginVertical();

            // Title on every page
            GUILayout.Label("Setup Wizard", TitleStyle);
            GUILayout.Space(20);

            // Intro text
            GUILayout.Label("Welcome to DOOR's Project new setup wizard", CenteredStyle);
            GUILayout.Space(10);
            // Question
            GUILayout.BeginVertical(GUI.skin.box);
            GUILayout.Label("This tool will guide you through configuring new doors or player using your own models with ease.", CenteredStyle);
            GUILayout.EndVertical();
            GUILayout.Space(10);

            GUILayout.FlexibleSpace();

            // Proceed button (centered)
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Proceed", GUILayout.Width(100), GUILayout.Height(32)))
            {
                currentStep = WizardStep.PlayerQuestion;
            }
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();

            GUILayout.Space(1);
            GUILayout.Label("Press proceed to continue...", bottomStyle);

            EditorGUILayout.EndVertical();
            GUILayout.Space(10);
            EditorGUILayout.EndHorizontal();
            GUILayout.Space(10);
            EditorGUILayout.EndVertical();
        }

        // -------------------------------------------------------
        // STEP 2: PLAYER QUESTION
        //   - The 2 buttons (SETUP PLAYER, SKIP) are now centered,
        //     stacked vertically, with minimal extra spacing.
        // -------------------------------------------------------
        private void DrawPlayerQuestionScreen()
        {
            EditorGUILayout.BeginVertical();
            GUILayout.Space(10);

            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(10);

            EditorGUILayout.BeginVertical();

            // Title
            GUILayout.Label("Door & Player Setup Wizard", TitleStyle);
            GUILayout.Space(20);// Extra space after the title

            GUILayout.Space(50);// Extra space between the question and buttons

            // Center the two buttons horizontally
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            EditorGUILayout.BeginVertical();
            GUILayout.Space(50);// Extra space before the buttons

            // Button 1: Setup Player
            if (GUILayout.Button("PLAYER SETUP", GUILayout.Width(100), GUILayout.Height(32)))
            {
                currentStep = WizardStep.CharacterReplacer;
            }
            GUILayout.Space(10);

            // Button 2: Door Setup
            if (GUILayout.Button("DOOR SETUP", GUILayout.Width(100), GUILayout.Height(32)))
            {
                // Prepare the DoorReplacer just like CharacterReplacer, then go to that step
                PrepareDoorReplacer();
                currentStep = WizardStep.DoorReplacer;
            }

            EditorGUILayout.EndVertical(); // End of the vertical buttons
            GUILayout.FlexibleSpace();     // Pushes the buttons up
            EditorGUILayout.EndHorizontal(); // End of the horizontal buttons

            // Final space before bottom
            GUILayout.FlexibleSpace();

            EditorGUILayout.EndVertical();
            GUILayout.Space(10);
            EditorGUILayout.EndHorizontal();
            GUILayout.Space(10);

            EditorGUILayout.EndVertical();
        }

        // -------------------------------------------------------
        // (Utility) PREPARE DOOR REPLACER
        //    - Loads doorReplacer script, creates hidden GO, 
        //      attaches component, sets up an Editor for it.
        // -------------------------------------------------------
        private void PrepareDoorReplacer()
        {
            // Check if DoorReplacer.cs exists at the specified path
            string relativePathDoor = "Assets/ANSTUDIO/Doors Project/Scripts/DoorReplacer/DoorReplacer.cs";
            string absolutePathDoor = Path.Combine(Application.dataPath, "ANSTUDIO/Doors Project/Scripts/DoorReplacer/DoorReplacer.cs");
            doorReplacerExists = File.Exists(absolutePathDoor);

            if (doorReplacerExists)
            {
                var scriptObjDoor = AssetDatabase.LoadAssetAtPath<MonoScript>(relativePathDoor);
                if (scriptObjDoor != null)
                {
                    // Create a hidden GameObject for the DoorReplacer component
                    tempDoorGO = new GameObject("Temp_DoorReplacer");
                    tempDoorGO.hideFlags = HideFlags.HideAndDontSave | HideFlags.NotEditable;

                    System.Type typeDoor = scriptObjDoor.GetClass();
                    if (typeDoor != null)
                    {
                        var component = tempDoorGO.AddComponent(typeDoor);
                        if (component != null)
                        {
                            doorReplacerEditor = Editor.CreateEditor(component);
                        }
                    }
                }
            }
            else
            {
                Debug.LogWarning("DoorReplacer.cs not found at: " + absolutePathDoor);
            }
        }

        // -------------------------------------------------------
        // STEP 3: CHARACTER REPLACER INSPECTOR
        // -------------------------------------------------------
        private void DrawCharacterReplacerScreen()
        {
            EditorGUILayout.BeginVertical();
            GUILayout.Space(10);

            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(10);

            EditorGUILayout.BeginVertical();

            // Title
            GUILayout.Label("New Player Setup", TitleStyle);

            // Draw the CharacterReplacer editor (no scroll)
            if (characterReplacerExists && characterReplacerEditor != null)
            {
                characterReplacerEditor.OnInspectorGUI();
            }
            else
            {
                GUILayout.Label("CharacterReplacer not found or invalid. Please check the script file.");
            }

            GUILayout.FlexibleSpace();

            // Navigation: "Back"
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Back", GUILayout.Width(100), GUILayout.Height(32)))
            {
                currentStep = WizardStep.PlayerQuestion;
            }
            GUILayout.Space(320);
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndVertical();
            GUILayout.Space(10);
            EditorGUILayout.EndHorizontal();
            GUILayout.Space(10);
            EditorGUILayout.EndVertical();
        }

        // -------------------------------------------------------
        // STEP 4: DOOR REPLACER INSPECTOR (NEW)
        // -------------------------------------------------------
        private void DrawDoorReplacerScreen()
        {
            EditorGUILayout.BeginVertical();
            GUILayout.Space(10);

            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(10);

            EditorGUILayout.BeginVertical();

            // Title
            GUILayout.Label("New Door Setup", TitleStyle);

            // Draw the DoorReplacer editor
            if (doorReplacerExists && doorReplacerEditor != null)
            {
                doorReplacerEditor.OnInspectorGUI();
            }
            else
            {
                GUILayout.Label("DoorReplacer not found or invalid. Please check the script file.");
            }

            GUILayout.FlexibleSpace();

            // Navigation: "Back"
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Back", GUILayout.Width(100), GUILayout.Height(32)))
            {
                currentStep = WizardStep.PlayerQuestion;
            }
            GUILayout.Space(320);
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndVertical();
            GUILayout.Space(10);
            EditorGUILayout.EndHorizontal();
            GUILayout.Space(10);
            EditorGUILayout.EndVertical();
        }
    }
#endif
}