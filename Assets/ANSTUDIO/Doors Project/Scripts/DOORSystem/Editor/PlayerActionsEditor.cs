using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
namespace ProjectDoors
{
    [CustomEditor(typeof(ProjectDoors.PlayerActions))]
    public class PlayerActionsEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            // Draw the default Inspector fields
            DrawDefaultInspector();

            var playerActions = (ProjectDoors.PlayerActions)target;
            var interactActionReference = playerActions.GetInteractActionReference();

            if (interactActionReference != null && interactActionReference.action != null)
            {
                var action = interactActionReference.action;

                EditorGUILayout.Space();
                EditorGUILayout.LabelField("Input Action Bindings", EditorStyles.boldLabel);

                // Display all bindings for the action
                /*
                for (int i = 0; i < action.bindings.Count; i++)
                {
                    var binding = action.bindings[i];

                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField($"Binding {i + 1}: {InputControlPath.ToHumanReadableString(binding.effectivePath, InputControlPath.HumanReadableStringOptions.OmitDevice)}");

                    if (GUILayout.Button("Rebind", GUILayout.Width(80)))
                    {
                        StartRebinding(action, i);
                    }

                    EditorGUILayout.EndHorizontal();
                }
                */

                // Display all bindings for the action except mouse and pointer buttons
                for (int i = 0; i < action.bindings.Count; i++)
                {
                    var binding = action.bindings[i];
                    string humanReadablePath = InputControlPath.ToHumanReadableString(
                        binding.effectivePath,
                        InputControlPath.HumanReadableStringOptions.OmitDevice
                    );

                    // Skip displaying any mouse or pointer device bindings
                    if (binding.effectivePath.Contains("Mouse") || binding.effectivePath.Contains("Pointer"))
                    {
                        continue; // Skip mouse or pointer controls
                    }

                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField($"Binding {i + 1}: {humanReadablePath}");

                    if (GUILayout.Button("Rebind", GUILayout.Width(80)))
                    {
                        StartRebinding(action, i);
                    }
                    EditorGUILayout.EndHorizontal();
                }

                // Reset button to clear saved bindings
                if (GUILayout.Button("Reset Bindings to Defaults"))
                {
                    ResetBindings(action);
                }
            }
            else
            {
                EditorGUILayout.HelpBox("No Input Action Reference assigned or no action found.", MessageType.Warning);
            }
        }

        private void StartRebinding(InputAction action, int bindingIndex)
        {
            Debug.Log("Press a key to rebind...");
            action.Disable();
            action.PerformInteractiveRebinding(bindingIndex)
                .WithControlsExcluding("Mouse")
                .OnComplete(operation =>
                {
                    SaveBindingOverride(action); // Save the binding override
                    operation.Dispose();
                    action.Enable();
                })
                .Start();
        }

        private void SaveBindingOverride(InputAction action)
        {
            for (int i = 0; i < action.bindings.Count; i++)
            {
                var overridePath = action.bindings[i].overridePath;
                if (!string.IsNullOrEmpty(overridePath))
                {
                    PlayerPrefs.SetString($"{action.actionMap.name}_{action.name}_binding_{i}", overridePath);
                }
            }
            PlayerPrefs.Save();
            Debug.Log("Binding overrides saved to PlayerPrefs.");
        }

        private void ResetBindings(InputAction action)
        {
            for (int i = 0; i < action.bindings.Count; i++)
            {
                string key = $"{action.actionMap.name}_{action.name}_binding_{i}";
                if (PlayerPrefs.HasKey(key))
                {
                    PlayerPrefs.DeleteKey(key); // Remove saved binding from PlayerPrefs
                }
            }
            PlayerPrefs.Save();

            // Clear overrides in the action
            action.RemoveAllBindingOverrides();

            Debug.Log("Bindings reset to defaults.");
        }
    }
}