using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ProjectDoors
{
    public class DOORSWelcomeWindow : EditorWindow
    {
        private const float WindowWidth = 460f;
        private const float WindowHeight = 408f;

        private static Texture2D logoTexture;
        private Texture2D discordIcon;
        private Texture2D globeIcon;
        private Texture2D mailIcon;

        [MenuItem("Tools/DOORS Project/Welcome and Support")]

        /// <summary>
        /// Opens the DOORS Welcome window.
        /// </summary>
        public static void ShowWindow()
        {
            var window = CreateInstance<DOORSWelcomeWindow>();
            window.titleContent = new GUIContent("");
            window.minSize = new Vector2(WindowWidth, WindowHeight);
            window.maxSize = new Vector2(WindowWidth, WindowHeight);
            CenterOnMainWin(window, WindowWidth, WindowHeight);
            window.ShowUtility();
        }

        /// <summary>
        /// Initializes the window and loads background and icons.
        /// </summary>
        private void OnEnable()
        {
            if (logoTexture == null)
                logoTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(
                    "Assets/ANSTUDIO/Doors Project/Pipeline Importer/Textures/Logo.png");
            if (discordIcon == null)
                discordIcon = AssetDatabase.LoadAssetAtPath<Texture2D>(
                    "Assets/ANSTUDIO/Doors Project/Welcome/Textures/discord-icon1.png");
            if (globeIcon == null)
                globeIcon = AssetDatabase.LoadAssetAtPath<Texture2D>(
                    "Assets/ANSTUDIO/Doors Project/Welcome/Textures/website-icon1.png");
            if (mailIcon == null)
                mailIcon = AssetDatabase.LoadAssetAtPath<Texture2D>(
                    "Assets/ANSTUDIO/Doors Project/Welcome/Textures/mail-icon1.png");
        }

        /// <summary>
        /// Draws the GUI for the welcome window.
        /// </summary>
        private void OnGUI()
        {
            if (logoTexture != null)
            {
                float x = (position.width - logoTexture.width) * .5f;
                float y = (position.height - logoTexture.height) * .5f;
                GUI.DrawTexture(new Rect(x, y, logoTexture.width, logoTexture.height), logoTexture, ScaleMode.StretchToFill);
            }

            // Begin layout
            EditorGUILayout.BeginVertical();
            GUILayout.Space(10);
            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(10);
            EditorGUILayout.BeginVertical();

            // Title Section
            GUILayout.Label("Welcome and Support", TitleStyle);
            Rect titleRect = GUILayoutUtility.GetLastRect();
            Color lineCol = EditorGUIUtility.isProSkin ? Color.gray : Color.black;
            EditorGUI.DrawRect(new Rect(titleRect.x, titleRect.yMax + 2, titleRect.width, 1f), lineCol);
            GUILayout.Space(5);
            GUILayout.Label("Thank you for purchasing DOOR's Project!", CenteredStyle);
            GUILayout.Space(20);
            GUILayout.Label("Take the next steps:", CenteredStyle);
            GUILayout.Space(2);

            // Steps Section
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            GUILayout.Label("Load Example Scene ", StepStyle);
            if (GUILayout.Button("Load Scene", GUILayout.Width(100)))
                LoadDemoScene();
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();

            // Door Creation Wizard Section
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            GUILayout.Label("Door Creation Wizard", StepStyle);
            if (GUILayout.Button("Open Wizard", GUILayout.Width(100)))
                DoorSetupWizard.OpenWindow();
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();

            // Manuals Section
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Read the manuals for detailed guidance", LinkStyle))
                SelectManualsFolder();
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
            GUILayout.FlexibleSpace();

            // Support Section
            GUILayout.Label("SUPPORT", CenteredStyle);
            GUILayout.Space(4);
            GUILayout.BeginVertical(GUI.skin.box);
            DrawLink(discordIcon, "Discord Support", "https://discord.gg/GcRVthbz4F");
            DrawLink(globeIcon, "Visit Our Website", "https://www.anstudio.ro");
            DrawLink(mailIcon, "support@anstudio.ro", "mailto:support@anstudio.ro");
            GUILayout.EndVertical();



            // Footer
            GUILayout.Space(12);
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Close", GUILayout.Width(100), GUILayout.Height(32)))
                Close();
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();

            // End layout
            EditorGUILayout.EndVertical();
            GUILayout.Space(10);
            EditorGUILayout.EndHorizontal();
            GUILayout.Space(10);
            EditorGUILayout.EndVertical();
        }

        /// <summary>
        /// Draws a link with an icon and label.
        /// </summary>
        private void DrawLink(Texture2D icon, string label, string url)
        {
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            GUILayout.Label(icon, GUILayout.Width(24), GUILayout.Height(24));
            if (GUILayout.Button(label, EditorStyles.linkLabel))
                Application.OpenURL(url);
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
        }

        /// <summary>
        /// Loads the demo scene from the specified path.
        /// </summary>
        private void LoadDemoScene()
        {
            string[] guids = AssetDatabase.FindAssets("t:Scene", new[] { "Assets/ANSTUDIO/Doors Project/Demo/Scene" });
            if (guids.Length > 0)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[0]);
                if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                    EditorSceneManager.OpenScene(path);
            }
            else
            {
                EditorUtility.DisplayDialog("Scene not found", "No demo scene found at expected path.", "OK");
            }
        }

        /// <summary>
        /// Selects the Manuals folder in the Project window.
        /// </summary>
        private void SelectManualsFolder()
        {
            const string folderPath = "Assets/ANSTUDIO/Doors Project/Manuals";
            var folder = AssetDatabase.LoadAssetAtPath<DefaultAsset>(folderPath);
            if (folder != null)
            {
                EditorUtility.FocusProjectWindow();
                Selection.activeObject = folder;
                EditorGUIUtility.PingObject(folder);

                var projectBrowserType = typeof(Editor).Assembly.GetType("UnityEditor.ProjectBrowser");
                var projectBrowser = EditorWindow.GetWindow(projectBrowserType);
                var showFolderContents = projectBrowserType.GetMethod("ShowFolderContents", BindingFlags.Instance | BindingFlags.NonPublic);
                showFolderContents?.Invoke(projectBrowser, new object[] { folder.GetInstanceID(), false });
            }
        }

        /// <summary>
        /// Returns the GUIStyle for links.
        /// </summary>
        private GUIStyle LinkStyle => new GUIStyle(EditorStyles.linkLabel)
        {
            alignment = TextAnchor.MiddleLeft,
            fontSize = 14,
            wordWrap = true
        };

        /// <summary>
        /// Returns the GUIStyle for the title.
        /// </summary>
        private GUIStyle TitleStyle => new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = 18,
            fontStyle = FontStyle.Bold
        };

        /// <summary>
        /// Returns the GUIStyle for centered text.
        /// </summary>
        private GUIStyle CenteredStyle => new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = 14,
            richText = true,
            wordWrap = true
        };

        /// <summary>
        /// Returns the GUIStyle for step descriptions.
        /// </summary>
        private GUIStyle StepStyle => new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleLeft,
            fontSize = 14,
            richText = true,
            wordWrap = true
        };

        /// <summary>
        /// Centers the window on the main editor window.
        /// </summary>
        private static void CenterOnMainWin(EditorWindow window, float w, float h)
        {
            var main = EditorGUIUtility.GetMainWindowPosition();
            window.position = new Rect(
                main.x + (main.width - w) * .5f,
                main.y + (main.height - h) * .5f,
                w, h
            );
        }
    }
}