using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections;
using UnityEditor;

namespace ProjectDoors
{
    public class PlayerActions : MonoBehaviour
    {
        [Tooltip("Event channel for interaction events.")]
        [SerializeField]
        private InteractionEventChannel interactionEventChannel = InteractionEventChannel.Instance;

        [Tooltip("Tag for finding the TextMeshPro object.")]
        [SerializeField]
        private string usableTextTag = "usableText"; // Tag for finding the TextMeshPro object
        private TextMeshPro UseText;

        [Tooltip("LayerMask for the interactable objects that can be used.")]
        [SerializeField] private LayerMask UseLayers = 1 << 6;

        [SerializeField]
        private InputActionReference interactActionReference;

        [Tooltip("Text to display when the object is open.")]
        [SerializeField]
        private string OpenText = "Open";

        [Tooltip("Text to display when the object is closed.")]
        [SerializeField]
        private string CloseText = "Close";

        [Tooltip("Adjustment for the Y-axis offset of the interaction text.")]
        [SerializeField]
        private float yOffset = 1.4f;
        [Tooltip("Adjustment for the X-axis offset of the interaction text.")]
        [SerializeField]
        private float xOffset = 0.1f;
        [Tooltip("Adjustment for the X-axis offset of the interaction text.")]
        [SerializeField]
        private float zOffset = 0.13f;

        [Tooltip("Distance to activate the object.")]
        [SerializeField]
        public float ActivationDistance = 1.5f;

        [SerializeField]
        private bool enableLogging = false; // Toggle for logging in the Inspector

        private Camera _camera;
        private ClickAgentController agentController; // Reference to the ClickAgentController script

        private bool hasLoggedMouseInteractionCloseEnough;
        private bool hasLoggedNoProximity;
        private bool hasLoggedNoMouseInteraction;
        private bool hasLoggedDoorDetected;
        private bool hasLoggedDoorDetectedByMouse;
        private bool hasLoggedCameraReassigned;
        private bool hasLoggedDoorInteractionCloseEnough;

        private void Update()
        {
            if (UseText == null) return; // Exit if UseText is not assigned

            HandleMouseInteraction();
            HandleKeyInteraction();
        }

        private void Start()
        {
            if (_camera == null) // Get the main camera if it's not assigned
            {
                _camera = Camera.main;
                LogOnce(ref hasLoggedCameraReassigned, "Main camera reference was missing and has been reassigned.");
            }

            UseLayers = LayerMask.GetMask("Usable"); // Set the layer mask

            // Find the TextMeshPro object even if it's inactive
            UseText = FindInactiveObjectByTag<TextMeshPro>(usableTextTag);

            // Reference to the ClickAgentController script
            agentController = GetComponent<ClickAgentController>();
            if (agentController == null)
            {
                LogError("ClickAgentController component not found!");
            }

            if (UseText == null)
            {
                LogError($"No GameObject with tag '{usableTextTag}' found or it does not contain a TextMeshPro component.");
            }

            Log("PlayerActions script initialized.");
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (interactActionReference == null)
            {
                AssignInputActionReference();
            }
        }
#endif

        private void Awake()
        {
            if (UseLayers == 0) // If not set in Inspector
            {
                UseLayers = LayerMask.GetMask("Usable"); // Set the layer mask
            }

            if (interactionEventChannel == null)
            {
                interactionEventChannel = Resources.Load<InteractionEventChannel>("PlayerInteractionEventChannel");
            }

            LoadBindingOverrides();
        }

        private void OnEnable()
        {
            if (interactActionReference?.action != null)
            {
                // Either enable just the one action:
                interactActionReference.action.Enable();

                // —or— enable the entire map:
                // interactActionReference.action.actionMap.Enable();
            }

            // Ensure the InteractionEventChannel is assigned
            if (interactionEventChannel == null)
            {
                interactionEventChannel = InteractionEventChannel.Instance ??
                    Resources.Load<InteractionEventChannel>("PlayerInteractionEventChannel");
            }
        }

        private void OnDisable()
        {
            if (interactActionReference?.action != null)
                interactActionReference.action.Disable();
        }

#if UNITY_EDITOR
        private void AssignInputActionReference()
        {
            // Search for InputActionAsset named "DoorInput"
            InputActionAsset doorInputAsset = FindInputActionAsset("DoorInputs");

            if (doorInputAsset != null)
            {
                // Find "Player/Use" action inside DoorInput
                InputAction useAction = doorInputAsset.FindActionMap("Player")?.FindAction("Use");

                if (useAction != null)
                {
                    interactActionReference = InputActionReference.Create(useAction);
                    EditorUtility.SetDirty(this); // Mark script as changed
                }
                else
                {
                    LogError("Could not find 'Player/Use' action in DoorInput.");
                }
            }
            else
            {
                LogError("Could not find InputActionAsset named 'DoorInput'.");
            }
        }

        private InputActionAsset FindInputActionAsset(string assetName)
        {
            string[] guids = AssetDatabase.FindAssets($"t:InputActionAsset {assetName}");
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                InputActionAsset asset = AssetDatabase.LoadAssetAtPath<InputActionAsset>(path);
                if (asset != null && asset.name == assetName)
                    return asset;
            }

            return null;
        }
#endif

        private void LoadBindingOverrides()
        {
            if (interactActionReference != null && interactActionReference.action != null)
            {
                var action = interactActionReference.action;

                for (int i = 0; i < action.bindings.Count; i++)
                {
                    var savedBinding = PlayerPrefs.GetString($"{action.actionMap.name}_{action.name}_binding_{i}", string.Empty);
                    if (!string.IsNullOrEmpty(savedBinding))
                    {
                        action.ApplyBindingOverride(i, new InputBinding { overridePath = savedBinding });
                        Log($"Binding override applied for action '{action.name}', binding index {i}: {savedBinding}");
                    }
                }

                Log("Binding overrides loaded from PlayerPrefs.");
            }
            else
            {
                LogWarning("InteractActionReference or its action is null. No bindings loaded.");
            }
        }

        public InputActionReference GetInteractActionReference()
        {
            return interactActionReference;
        }

        public void HandleProximityInteraction()
        {
            Ray ray = new Ray(transform.position, transform.forward); // Create a ray from the player's position

            if (Physics.Raycast(ray, out RaycastHit hit, ActivationDistance, UseLayers)) // Check if the ray hits an object on the UseLayers
            {
                if (hit.collider.TryGetComponent<Door>(out Door door) && Vector3.Distance(transform.position, door.transform.position) <= ActivationDistance) // Check if the object is a door
                {
                    float distanceToDoor = Vector3.Distance(transform.position, door.transform.position); // Calculate the distance to the door
                    LogOnce(ref hasLoggedDoorDetected, "Door detected within range");

                    if (distanceToDoor <= ActivationDistance) // Check if the player is close enough to the door
                    {
                        agentController?.StopAgentMovement();
                        UseText.SetText(door.IsOpen ? CloseText : OpenText);
                        UseText.gameObject.SetActive(true);
                        UseText.transform.SetPositionAndRotation(hit.point + (Vector3.up * yOffset) + (transform.right * xOffset) + (hit.normal * zOffset), Quaternion.LookRotation(-hit.normal));
                        return; // Exit the method to prevent further checks
                    }
                }
            }

            UseText.gameObject.SetActive(false); // Hide the interaction text if no door is within range
            ResetConditionalLogging();
            LogOnce(ref hasLoggedNoProximity, "No interactable door within proximity.");
        }

        private void HandleKeyInteraction()
        {
            if (interactActionReference.action.triggered && !Mouse.current.leftButton.isPressed)
            {
                Log("Interact action triggered by player.");

                Vector3 sphereCenter = transform.position;
                float sphereRadius = ActivationDistance;

                Collider[] hitColliders = Physics.OverlapSphere(sphereCenter, sphereRadius, UseLayers);

                foreach (Collider hitCollider in hitColliders)
                {
                    if (hitCollider.TryGetComponent<Door>(out Door door))
                    {
                        DoorEventChannel doorEventChannel = door.GetEventChannel();

                        if (doorEventChannel != null)
                        {
                            if (door.IsOpen)
                            {
                                doorEventChannel.RaiseClose();
                                Log("Door close event raised.");
                            }
                            else
                            {
                                doorEventChannel.RaiseOpen(transform.position);
                                Log("Door open event raised.");
                            }
                        }
                        else
                        {
                            LogWarning("No DoorEventChannel found on the door.");
                        }

                        break; // Exit the loop after interacting with the first door found
                    }
                }
            }
        }

        public void HandleMouseInteraction()
        {
            Ray ray = _camera.ScreenPointToRay(Mouse.current.position.ReadValue()); // Create a ray from the mouse position

            if (Physics.Raycast(ray, out RaycastHit hit, Mathf.Infinity, UseLayers) && hit.collider.TryGetComponent<Door>(out Door door)) // Check if the object is a door
            {
                float distanceToDoor = Vector3.Distance(transform.position, door.transform.position); // Calculate the distance to the door
                LogOnce(ref hasLoggedDoorDetectedByMouse, "Door detected by mouse interaction");

                UseText.SetText(door.IsOpen ? CloseText : OpenText);

                if (distanceToDoor <= ActivationDistance) // Check if the player is close enough to the door
                {
                    UseText.gameObject.SetActive(true); // Show the interaction text
                    UseText.transform.SetPositionAndRotation(hit.point + (hit.normal * 0.1f), Quaternion.LookRotation(-hit.normal));
                    agentController?.StopAgentMovement(); // Stop movement
                    LogOnce(ref hasLoggedMouseInteractionCloseEnough, "Player is close enough to interact with the door using mouse.");
                    Cursor.visible = false; // Hide the cursor
                }
                else
                {
                    UseText.gameObject.SetActive(false); // Hide the interaction text
                    agentController?.ResumeAgentMovement(); // Resume movement if out of range
                    Cursor.visible = true; // Show the cursor
                }
            }
            else
            {
                UseText.gameObject.SetActive(false); // Hide the interaction text
                agentController?.ResumeAgentMovement(); // Resume movement if not targeting any object
                Cursor.visible = true; // Show the cursor
                LogOnce(ref hasLoggedNoMouseInteraction, "No interactable door detected by mouse interaction.");
            }
        }

        public void OnUse()
        {
            if (UseText == null) return; // Exit if UseText is not assigned

            Ray ray = _camera.ScreenPointToRay(Mouse.current.position.ReadValue());
            if (Physics.Raycast(ray, out RaycastHit hit, Mathf.Infinity, UseLayers)) // Check if the ray hits an object on the UseLayers
            {
                if (hit.collider.TryGetComponent<Door>(out Door door)) // Check if the object is a door
                {
                    if (Vector3.Distance(transform.position, door.transform.position) <= ActivationDistance) // Check if the player is close enough to the door
                    {
                        HandleDoorInteraction(door);
                        Log("Door interaction handled by OnUse method.");
                    }
                }
                else
                {
                    Log("Mouse clicked on a non-usable object, exiting OnUse method.");
                }
            }
        }

        private void HandleDoorInteraction(Door door)
        {
            if (interactionEventChannel != null)
            {
                interactionEventChannel.RaiseInteractRequest(door.transform.position, door.IsOpen, door);
            }
            if (door.IsOpen)
            {
                door.Close();
                Log("Door closed by Key interaction.");
            }
            else
            {
                door.Open(transform.position);
                Log("Door opened by Key interaction.");
            }
        }

        private T FindInactiveObjectByTag<T>(string tag) where T : Component
        {
            T[] objs = Resources.FindObjectsOfTypeAll<T>();
            foreach (T obj in objs)
            {
                if (obj.CompareTag(tag))
                {
                    return obj;
                }
            }
            return null;
        }

        private void Log(string message)
        {
            if (enableLogging)
            {
                Debug.Log(message);
            }
        }

        private void LogWarning(string message)
        {
            if (enableLogging)
            {
                Debug.LogWarning(message);
            }
        }

        private void LogError(string message)
        {
            if (enableLogging)
            {
                Debug.LogError(message);
            }
        }

        private void LogOnce(ref bool hasLogged, string message)
        {
            if (enableLogging && !hasLogged)
            {
                Debug.Log(message);
                hasLogged = true;
            }
        }

        private void ResetConditionalLogging()
        {
            hasLoggedMouseInteractionCloseEnough = false;
            hasLoggedDoorDetected = false;
            hasLoggedNoMouseInteraction = false;
            hasLoggedNoProximity = false;
        }
    }
}
