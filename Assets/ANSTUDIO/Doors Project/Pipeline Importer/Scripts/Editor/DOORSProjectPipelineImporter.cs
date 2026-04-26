using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;
using System.Collections.Generic;
using UnityEditor.Presets;
using UnityEditor.SceneManagement;

namespace ProjectDoors
{
    public class DOORSProjectPipelineImporter : EditorWindow
    {
        // Window size
        private const float WindowWidth = 460f;
        private const float WindowHeight = 408f;

        // Background image
        private static Texture2D logoTexture;

        private const string AiNavVersion = "2.0.8";
        private static readonly string[] requiredPackages = {
        $"com.unity.ai.navigation@{AiNavVersion}",
        "com.unity.inputsystem"
};

        // Possible wizard steps
        private enum WizardStep { Welcome }
        private WizardStep currentStep = WizardStep.Welcome;

        private Queue<string> missingPackages;
        // Pipeline detection
        private string pipelineType;
        // Package Manager requests
        private ListRequest listRequest;
        private AddRequest addRequest;

        private bool installClicked = false;
        private bool installationSucceeded = false;
        //private const string kTagManagerPromptDisabledKey = "DoorsProject_DoNotPromptTagManagerPreset";
        // Keep the prompt preference per project
        private static string TagManagerPromptKey
            => "DoorsProject_DoNotPromptTagManagerPreset_" + Application.dataPath;

        // ──────────────────────────────────────────────────────────────
        //  0.  Add a unique prefs-key once, near the top of the class
        // ──────────────────────────────────────────────────────────────
        private static readonly string InstallFlagKey =
            "DOORSProjectImporter_Installing_" + Application.dataPath;

        // 1.  Keep a helper to build the key in one place
        private static string LaunchedKey
        => "DOORSProjectImporter_Launched_" + Application.dataPath;

        // remembers a successful update across editor sessions
        private static readonly string UpdatedKey =
            "DOORSProjectImporter_Updated_" + Application.dataPath;

        private static string WelcomeFlagKey
    => "DoorsProject_ShowWelcome_" + Application.dataPath;

        // -----------------------------------------------------------
        // 2.  Show the window first, *then* mark it as launched
        // -----------------------------------------------------------
        [MenuItem("Tools/DOORS Project/Pipeline Importer")]
        public static void ShowInstallerWindow()
        {
            // prevent double‑opening
            if (HasOpenInstances<DOORSProjectPipelineImporter>()) return;

            // Create the window
            var window = CreateInstance<DOORSProjectPipelineImporter>();
            window.pipelineType = CurrentPipelineName();
            window.titleContent = new GUIContent("");
            window.minSize = new Vector2(WindowWidth, WindowHeight);
            window.maxSize = new Vector2(WindowWidth, WindowHeight);
            CenterOnMainWin(window, WindowWidth, WindowHeight);
            window.ShowUtility();

            EditorPrefs.SetBool(LaunchedKey, true);   // mark “launched” after window exists

            // Load the logo once
            if (logoTexture == null)
            {
                logoTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(
                    "Assets/ANSTUDIO/Doors Project/Pipeline Importer/Textures/Logo.png"
                );
                if (logoTexture == null)
                    Debug.LogWarning("Logo.png not found at the expected path.");
            }
        }





        // -----------------------------------------------------------
        // 3.  Initialise: schedule the window but DON’T set the flag
        // -----------------------------------------------------------
        [InitializeOnLoadMethod]
        private static void AutoOpenInstaller()
        {
            if (!NeedsPipelineUpdate()) return;

            if (EditorPrefs.GetBool(LaunchedKey)) return;

            void TryOpen()
            {
                if (EditorApplication.isCompiling || EditorApplication.isUpdating) return;
                EditorApplication.update -= TryOpen;
                ShowInstallerWindow();
            }
            EditorApplication.update += TryOpen;
        }



        private void OnEnable()
        {
            pipelineType = DetectRenderPipeline();

            // has this project already been updated in a previous session?
            if (EditorPrefs.GetBool(UpdatedKey, false))
                installationSucceeded = true;

            // Did we crash-land in the middle of an install?
            if (EditorPrefs.GetBool(InstallFlagKey, false))
            {
                installClicked = true;
                CheckDependencies();


            }
        }

        private void OnGUI()
        {
            // Draw background logo
            if (logoTexture != null)
            {
                float x = (position.width - logoTexture.width) * .5f;
                float y = (position.height - logoTexture.height) * .5f;
                GUI.DrawTexture(new Rect(x, y, logoTexture.width, logoTexture.height), logoTexture, ScaleMode.StretchToFill);
            }

            if (currentStep == WizardStep.Welcome)
                DrawWelcomeScreen();
        }

        // -------------------------------------------------------
        // Styles
        // -------------------------------------------------------
        private GUIStyle TitleStyle => new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = 18,
            fontStyle = FontStyle.Bold
        };

        private GUIStyle CenteredStyle => new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = 14,
            richText = true,
            wordWrap = true
        };

        // -------------------------------------------------------
        // WELCOME SCREEN
        // -------------------------------------------------------
        private void DrawWelcomeScreen()
        {
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

            GUILayout.Label("PIPELINE IMPORTER", TitleStyle);
            // fetch the rect Unity just used for that label
            Rect titleRect = GUILayoutUtility.GetLastRect();

            // pick a colour that works in both light / dark skins
            Color lineCol = EditorGUIUtility.isProSkin ? Color.gray : Color.black;

            // draw an underline 2 px below the text
            EditorGUI.DrawRect(
                new Rect(titleRect.x, titleRect.yMax + 2, titleRect.width, 1f),
                lineCol);
            GUILayout.Space(5);
            GUILayout.Label("DOOR's Project Pipeline Importer", CenteredStyle);
            GUILayout.Space(10);

            /* =========================================================
             *  CASE A – project already updated: show success + Close
             * ========================================================= */
            if (installationSucceeded && installClicked)
            {
                GUILayout.BeginVertical(GUI.skin.box);
                GUILayout.Label("This project has already been updated successfully.",
                CenteredStyle);
                GUILayout.EndVertical();

                GUILayout.Space(12);

                GUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("Close", GUILayout.Width(100), GUILayout.Height(32)))
                    Close();
                GUILayout.FlexibleSpace();
                GUILayout.EndHorizontal();

                GUILayout.Space(4);
                GUILayout.Label("You can close this window now.", bottomStyle);

                // early‑out so none of the other UI renders
                EditorGUILayout.EndVertical();       // inner vert
                GUILayout.Space(10);
                EditorGUILayout.EndHorizontal();     // outer horiz
                GUILayout.Space(10);
                EditorGUILayout.EndVertical();       // outer vert
                return;
            }

            /* ───────────────────────────────────────────────
             *  Show orange banner only if update still needed
             * ───────────────────────────────────────────── */
            if (pipelineType != "Standard" && !installationSucceeded)
            {
                GUILayout.Label(
                $"<color=#FFA500>DOOR's Project detected that you are using the {pipelineType} pipeline, " +
                "press the Import button to load the correct pipeline.</color>",
                CenteredStyle);
                GUILayout.Space(6);
            }

            /* ───────────────────────────────────────────────
             *  Pipeline‑specific description
             * ───────────────────────────────────────────── */
            GUILayout.BeginVertical(GUI.skin.box);
            if (installationSucceeded)
            {
                GUILayout.Label("This project has been updated successfully.",
                CenteredStyle);
            }
            else if (pipelineType != "Standard")
            {
                GUILayout.Label(
                "This updater configures DOORS Project for the detected render pipeline " +
                "and completes setup by installing all required packages, tags, and layers.",
                CenteredStyle);
            }
            else   /* Built‑in & not updated flag (should never need update) */
            {
                GUILayout.Label("Built‑in Render Pipeline detected.", CenteredStyle);
                GUILayout.Label("No update is required.", CenteredStyle);
            }
            GUILayout.EndVertical();




            GUILayout.Space(10);
            GUILayout.FlexibleSpace();





            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();

            /* ───────────────────────────────────────────────
+           *  NEW: Built‑in pipeline needs no update
+           * ───────────────────────────────────────────── */
            if (pipelineType == "Standard" && !installationSucceeded)
            {
                GUILayout.BeginVertical();
                /*
                GUILayout.Label("Built‑in Render Pipeline detected.", CenteredStyle);
                GUILayout.Label("No update is required.", CenteredStyle);
                GUILayout.Space(8);
                */
                /* centre the button */
                GUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("Close", GUILayout.Width(100), GUILayout.Height(32)))
                    Close();
                GUILayout.FlexibleSpace();
                GUILayout.EndHorizontal();

                GUILayout.EndVertical();
            }






            else   /* HDRP / URP branch */
            {
                if (installationSucceeded)   /* already updated -> Load Demo | Close */
                    {


                  /* ----------------------------------------------
                   *  close the current horizontal (action row),
                   *  show the text centred on its own line,
                   *  then open a fresh horizontal for the buttons
                   * ---------------------------------------------- */
                    EditorGUILayout.EndHorizontal();          // ← close outer row
                    
                    GUILayout.Space(4);
                    GUILayout.Label("Do you want to load the Demo Scene?", CenteredStyle);
                    GUILayout.Space(6);                    
                    EditorGUILayout.BeginHorizontal();
                    GUILayout.FlexibleSpace();
                    if (GUILayout.Button("Load Demo", GUILayout.Width(100), GUILayout.Height(32)))
                        LoadDemoScene();
                    GUILayout.Space(10);
                    if (GUILayout.Button("Close", GUILayout.Width(100), GUILayout.Height(32)))
                        Close();
                    GUILayout.FlexibleSpace();
                    EditorGUILayout.EndHorizontal();
                }





                else if (!installClicked)    /* Import / Cancel */
                {
                    GUILayout.BeginHorizontal();
                    if (GUILayout.Button("Import", GUILayout.Width(100), GUILayout.Height(32)))
                    {
                        installClicked = true;
                        installationSucceeded = false;
                        EditorPrefs.SetBool(InstallFlagKey, true);

                        InstallDoorsPackage();
                        CheckDependencies();
                    }

                    GUILayout.Space(10);
                    if (GUILayout.Button("Cancel", GUILayout.Width(100), GUILayout.Height(32)))
                    {
                        Close();
                        return;
                    }
                    GUILayout.EndHorizontal();
                }
                else   /* import in progress */
                {
                    GUILayout.Label("Importing the pipeline in progress…", CenteredStyle);
                }
            }

                if (!installationSucceeded)      // outer row was already closed in success path
                {
                GUILayout.FlexibleSpace();
                EditorGUILayout.EndHorizontal();
                }

            GUILayout.Space(1);


            if (pipelineType == "Standard")
            {
                GUILayout.Label("This project is already using the Built‑In pipeline.", bottomStyle);
            }
            else if (!installationSucceeded)
            {
                if (!installClicked)
                    GUILayout.Label("Press the Import button to continue…", bottomStyle);
                else if (!installationSucceeded)
                    GUILayout.Label("Press Finish to close the window after import.",
                        bottomStyle);
            }
            else  /* updated success */
                GUILayout.Label("You can close this window now.", bottomStyle);




            EditorGUILayout.EndVertical();
            GUILayout.Space(10);
            EditorGUILayout.EndHorizontal();
            GUILayout.Space(10);
            EditorGUILayout.EndVertical();
        }

        /// <summary>
        /// Import the DOORS Project package for the correct pipeline.
        /// </summary>
        // -------------------------------------------------------
        // INSTALLATION LOGIC
        // -------------------------------------------------------
        private void InstallDoorsPackage()
        {
            if (pipelineType == "Standard")
            {
                Debug.Log("Built-in pipeline detected – no additional package required.");
                return;
            }

            string path = ChoosePackageBasedOnPipeline();
            if (!string.IsNullOrEmpty(path))
                AssetDatabase.ImportPackage(path, false);
        }


        private void CheckDependencies()
        {
            listRequest = Client.List(true);
            missingPackages = new Queue<string>();
            EditorApplication.update += OnListRequestProgress;
        }

        private static string ShortName(string id)
        {
            int at = id.IndexOf('@');
            return at >= 0 ? id[..at] : id;
        }

        private void OnListRequestProgress()
        {
            if (listRequest is null || !listRequest.IsCompleted) return;
            EditorApplication.update -= OnListRequestProgress;

            if (listRequest.Status == StatusCode.Success)
            {
                var installed = new HashSet<string>();
                foreach (var pkg in listRequest.Result) installed.Add(pkg.name);

                foreach (var p in requiredPackages)
                    if (!installed.Contains(ShortName(p)))
                        missingPackages.Enqueue(p);

                if (missingPackages.Count == 0) FinalizeInstallation(true);
                else InstallNextMissingPackage();
            }
            else FinalizeInstallation(false);
        }

        private void InstallNextMissingPackage()
        {
            if (missingPackages.Count == 0)
            {
                FinalizeInstallation(true);
                return;
            }
            addRequest = Client.Add(missingPackages.Peek());
            EditorApplication.update += OnAddRequestProgress;
        }

        private void OnAddRequestProgress()
        {
            if (addRequest is null || !addRequest.IsCompleted) return;
            EditorApplication.update -= OnAddRequestProgress;

            if (addRequest.Status == StatusCode.Success)
                missingPackages.Dequeue();// successfully added the package
            else
            {
                Debug.LogWarning(
                    $"Failed to add {missingPackages.Peek()} – {addRequest.Error?.message}");

                // one-off fallback: try again without @version
                string failed = missingPackages.Dequeue();
                string retryId = ShortName(failed);
                if (retryId != failed) missingPackages.Enqueue(retryId);
            }

            InstallNextMissingPackage();// continue with the next package
        }

        // ----------------------------------------
        // FINAL STEP
        // ----------------------------------------
        private void FinalizeInstallation(bool success)
        {
            if (success)
            {
                ShowTagManagerPresetDialogOnce();
                installationSucceeded = true;
                installClicked = false;
                EditorPrefs.SetBool(UpdatedKey, true); // <── remember that we updated this project
                EditorPrefs.SetBool(LaunchedKey, true);
                EditorPrefs.DeleteKey(InstallFlagKey);   // <── installation is over
                EditorPrefs.SetBool(WelcomeFlagKey, true);
                Repaint();// force a repaint to update the UI
            }
            else
            {
                Debug.LogError("Installation failed. Please check the console for errors.");
                installationSucceeded = false;
            }
        }

        private void OnDestroy()
        {
            if (EditorPrefs.GetBool(WelcomeFlagKey, false))
            {
                EditorPrefs.DeleteKey(WelcomeFlagKey);
                if (pipelineType != "Standard")
                    DOORSWelcomeWindow.ShowWindow();
            }
        }

        // ──────────────────────────────────────────
        //  Load Demo helper
        // ──────────────────────────────────────────
        private static void LoadDemoScene()
        {
            string[] guids = AssetDatabase.FindAssets(
                "t:Scene", new[] { "Assets/ANSTUDIO/Doors Project/Demo/Scene" });
            if (guids.Length == 0)
            {
                EditorUtility.DisplayDialog("Demo Scene",
                "No scene found in:\nAssets/ANSTUDIO/Doors Project/Demo/Scene",
                "OK");
                return;
            }
            string path = AssetDatabase.GUIDToAssetPath(guids[0]);
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                EditorSceneManager.OpenScene(path);
        }

// ----------------------------------------
// TAG- & LAYER-PRESET HELPERS
// ----------------------------------------
private void ShowTagManagerPresetDialogOnce()
        {
            if (EditorPrefs.GetBool(TagManagerPromptKey, false))
                return;

            if (IsTagManagerPresetApplied())
            {
                EditorPrefs.SetBool(TagManagerPromptKey, true);
                return;
            }

            if (EditorUtility.DisplayDialog(
                    "Import TagManager Preset",
                    "Doors Project needs its custom Tags and Layers.\n" +
                    "Import the TagManager preset now?\n(This will overwrite your current Tags/Layers)",
                    "Import", "Skip"))
            {
                ApplyTagManagerPreset();
            }

            EditorPrefs.SetBool(TagManagerPromptKey, true);
        }

        // ----------------------------------------
        // ────────── PIPELINE HELPERS ──────────--
        // ----------------------------------------
        private static string CurrentPipelineName()
        {
            var rp = GraphicsSettings.defaultRenderPipeline;
            if (rp == null) return "Standard";
            var n = rp.GetType().Name;
            if (n.Contains("HDRenderPipelineAsset")) return "HDRP";
            if (n.Contains("UniversalRenderPipelineAsset") || n.Contains("URP")) return "URP";
            return "Standard";
        }

        private static bool NeedsPipelineUpdate() => CurrentPipelineName() != "Standard";



        private static bool IsTagManagerPresetApplied()
        {
            const string presetPath = "Assets/ANSTUDIO/Doors Project/Demo/Presets/DoorsProject_TagsAndLayers.preset";
            string tagManagerPath = System.IO.Path.Combine(Application.dataPath, "../ProjectSettings/TagManager.asset");

            if (!System.IO.File.Exists(presetPath) || !System.IO.File.Exists(tagManagerPath))
                return false;

            try
            {
                return System.IO.File.ReadAllText(presetPath)
                       == System.IO.File.ReadAllText(tagManagerPath);
            }
            catch { return false; }
        }

        private static void ApplyTagManagerPreset()
        {
            const string presetPath = "Assets/ANSTUDIO/Doors Project/Demo/Presets/DoorsProject_TagsAndLayers.preset";

            var preset = AssetDatabase.LoadAssetAtPath<Preset>(presetPath);
            if (preset == null) { Debug.LogError("Preset not found: " + presetPath); return; }

            var tagMgr = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0];
            if (tagMgr == null) { Debug.LogError("Cannot load TagManager.asset"); return; }

            if (preset.ApplyTo(tagMgr))
            {
                Debug.Log("Doors Project TagManager preset applied.");
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            }
            else
                Debug.LogError("Failed to apply TagManager preset.");
        }

        // -------------------------------------------------------
        // PIPELINE DETECTION
        // -------------------------------------------------------
        private string DetectRenderPipeline()
        {
            var rp = GraphicsSettings.defaultRenderPipeline;
            if (rp == null) return "Standard";
            var name = rp.GetType().Name;
            if (name.Contains("HDRenderPipelineAsset")) return "HDRP";
            if (name.Contains("UniversalRenderPipelineAsset") ||
                name.Contains("URP")) return "URP";
            return "Standard";
        }

        private string ChoosePackageBasedOnPipeline()
        {
            switch (pipelineType)
            {
                case "HDRP":
                    return "Assets/ANSTUDIO/Doors Project/Pipeline Importer/Pipelines/DoorsProject_HDRP.unitypackage";
                case "URP":
                    return "Assets/ANSTUDIO/Doors Project/Pipeline Importer/Pipelines/DoorsProject_URP.unitypackage";
                default:
                    return null;
            }
        }


        // -----------------------------------------------------
        // Center the Window on the Editor
        // -----------------------------------------------------
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