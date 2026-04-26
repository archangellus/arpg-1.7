using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEditor;
namespace ProjectDoors
{
    [InitializeOnLoad]
    public static class PlayerInputDefaultInitializer
    {
        static PlayerInputDefaultInitializer()
        {
            // Monitor the Editor update cycle for changes
            EditorApplication.update += CheckForNewPlayerInputs;

            // This callback handles components added manually via the Editor UI
            ObjectFactory.componentWasAdded += OnComponentAdded;
        }

        private static void OnComponentAdded(Component component)
        {
            if (component is PlayerInput playerInput)
            {
                ConfigurePlayerInput(playerInput);
            }
        }

        private static void CheckForNewPlayerInputs()
        {
            // Find all PlayerInput components in the scene
            var playerInputs = Object.FindObjectsByType<PlayerInput>(FindObjectsSortMode.None);

            foreach (var playerInput in playerInputs)
            {
                // Skip already configured PlayerInput components
                if (IsPlayerInputConfigured(playerInput)) continue;

                // Configure new PlayerInput components if tagged "Player"
                if (playerInput.gameObject.CompareTag("Player"))
                {
                    ConfigurePlayerInput(playerInput);
                }
            }
        }

        private static void ConfigurePlayerInput(PlayerInput playerInput)
        {
            // Load the InputActionAsset dynamically
            var inputActionAsset = FindInputActionAsset("DoorInputs");
            if (inputActionAsset == null)
            {
                Debug.LogError($"Could not find InputActionAsset named 'DoorInputs'.");
                LogAvailableInputAssets();
                return;
            }

            // Configure the PlayerInput component
            playerInput.actions = inputActionAsset;
            playerInput.defaultActionMap = "Player";

            // Assign the InputSystemUIInputModule if available
            var inputModule = Object.FindAnyObjectByType<InputSystemUIInputModule>();
            if (inputModule != null)
            {
                playerInput.uiInputModule = inputModule;
            }
            else
            {
                Debug.LogWarning("No InputSystemUIInputModule found in the scene.");
            }

            // Assign the Main Camera if available
            var mainCamera = Camera.main;
            if (mainCamera != null)
            {
                playerInput.camera = mainCamera;
            }
            else
            {
                Debug.LogWarning("No Main Camera found in the scene.");
            }

            // Mark as configured and save changes
            EditorUtility.SetDirty(playerInput);
            Debug.Log($"PlayerInput on '{playerInput.gameObject.name}' configured with default values.");
        }

        private static InputActionAsset FindInputActionAsset(string assetName)
        {
            string[] guids = AssetDatabase.FindAssets($"t:InputActionAsset {assetName}");
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var asset = AssetDatabase.LoadAssetAtPath<InputActionAsset>(path);
                if (asset != null && asset.name == assetName)
                {
                    return asset;
                }
            }
            return null;
        }

        private static void LogAvailableInputAssets()
        {
            string[] guids = AssetDatabase.FindAssets("t:InputActionAsset");
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var asset = AssetDatabase.LoadAssetAtPath<InputActionAsset>(path);
                Debug.Log($"Found InputActionAsset: {asset.name} at path: {path}");
            }
        }

        private static bool IsPlayerInputConfigured(PlayerInput playerInput)
        {
            // Check if PlayerInput has already been configured
            return playerInput.actions != null && playerInput.defaultActionMap == "Player";
        }
    }
}