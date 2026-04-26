using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using UnityEngine.InputSystem;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace ProjectDoors
{
    public class DoorLever : MonoBehaviour
    {
        [Tooltip("Reference to the door controlled by this lever.")]
        [SerializeField] private Door controlledDoor;
        [Tooltip("Event channel for door interactions.")]
        [SerializeField] private DoorEventChannel doorEventChannel;

        [Tooltip("Distance to activate the lever.")]
        [SerializeField]
        public float activationDistance = 2.0f;

        [Tooltip("Tag to identify the player.")]
        [SerializeField]
        private string playerTag = "Player";

        [Tooltip("Layers to ignore in raycasts.")]
        [SerializeField]
        private LayerMask raycastIgnoreLayers;

        [Tooltip("Adjustment for the Y-axis offset of the interaction text.")]
        [SerializeField]
        private float textYOffset = 0.5f;

        [Tooltip("Object to be rotated (If not it defaults to rotating the GameObject to which is attached).")]
        [SerializeField]
        private Transform objectToRotate;

        [Tooltip("Rotation angle for the lever when toggled.")]
        [SerializeField]
        private float leverRotationAngle = 30f;

        [Tooltip("Speed of the lever rotation.")]
        [SerializeField]
        private float leverRotationSpeed = 1f;

        [Tooltip("Match rotation speed to sound length (if this is false it defaults to simple speed value).")]
        [SerializeField]
        private bool matchRotationSpeedToSound = false;

        [Tooltip("List of sounds to play during lever movement.")]
        [SerializeField]
        private List<AudioClip> leverSounds;

        private AudioSource audioSource;
        private Camera mainCamera;
        private TextMeshPro interactionText;
        private bool isPlayerWithinRange = false;
        private Quaternion initialRotation;
        private Quaternion targetRotation;
        private Coroutine rotationCoroutine;
        private bool isLeverToggled = false;
        private bool isInitialized = false;
        public bool IsActivated { get; private set; }

        public void SetControlledDoor(Door door)
        {
            controlledDoor = door;
        }

        public void AssignDoor(Door door)
        {
            controlledDoor = door;
        }

        private void Awake()
        {
            // Start the delayed assignment coroutine for runtime and editor
            StartCoroutine(InitializeWithForcedRefresh());
        }

        private void Start()
        {
            // Fallback check to ensure controlledDoor is assigned
            if (controlledDoor == null)
            {
                AssignControlledDoor();
            }

            mainCamera = Camera.main;
            initialRotation = objectToRotate != null ? objectToRotate.rotation : transform.rotation;

            interactionText = FindInactiveObjectByTag<TextMeshPro>("usableTAG");
            if (interactionText == null)
            {
                Debug.LogError("No interaction text (TextMeshPro) found! Ensure the tag 'usableTAG' is assigned.");
            }

            if (audioSource == null)
            {
                audioSource = gameObject.AddComponent<AudioSource>();
            }
        }

        private void Update()
        {
            HandleProximityInteraction();
            HandleMouseClickInteraction();
        }

        private void LateUpdate()
        {
            if (!isInitialized)
            {
                AssignControlledDoor();
                isInitialized = true;
            }
        }

        private void OnValidate()
        {
            if (gameObject.activeInHierarchy)
            {
                StartCoroutine(InitializeWithForcedRefresh());
            }
            else
            {
                AssignControlledDoor();
                ForceInspectorRefresh();
            }

            if (controlledDoor != null && doorEventChannel == null)
            {
                doorEventChannel = controlledDoor.GetEventChannel();
                if (doorEventChannel == null)
                {
                    Debug.LogWarning($"No DoorEventChannel assigned for the Door '{controlledDoor.name}'. Please assign it manually.");
                }
            }
        }

        private IEnumerator InitializeWithForcedRefresh()
        {
            // Wait for one frame to ensure the hierarchy is initialized
            yield return null;

            // Force a refresh by toggling the serialized property
            ForceInspectorRefresh();

            // Assign controlledDoor and any other dependencies
            AssignControlledDoor();
        }

        private void ForceInspectorRefresh()
        {
            // Toggle the serialized boolean property to force Unity to refresh the object
            bool originalValue = matchRotationSpeedToSound;
            matchRotationSpeedToSound = !originalValue;
            matchRotationSpeedToSound = originalValue;
        }

        private void AssignControlledDoor()
        {
            if (controlledDoor == null)
            {
                controlledDoor = FindControlledDoorInParentHierarchy();
                if (controlledDoor != null)
                {
                    Debug.Log($"Controlled Door automatically assigned: {controlledDoor.name} for {gameObject.name}");
                }
            }

            // Assign the door event channel if missing
            if (controlledDoor != null && doorEventChannel == null)
            {
                doorEventChannel = controlledDoor.GetEventChannel();
                Debug.Log($"Door Event Channel automatically assigned for {gameObject.name}");
            }
        }

        private Door FindControlledDoorInParentHierarchy()
        {
            // Step 1: Check the immediate parent for a Door component
            Transform parent = transform.parent;
            if (parent != null)
            {
                Door parentDoor = parent.GetComponent<Door>();
                if (parentDoor != null)
                {
                    return parentDoor;
                }
            }

            // Step 2: Check all siblings and their children for a Door component
            if (parent != null)
            {
                foreach (Transform sibling in parent)
                {
                    Door siblingDoor = sibling.GetComponentInChildren<Door>();
                    if (siblingDoor != null)
                    {
                        return siblingDoor;
                    }
                }
            }

            // Step 3: Check the grandparent hierarchy (if applicable)
            Transform rootParent = parent != null ? parent.parent : null;
            if (rootParent != null)
            {
                Door rootParentDoor = rootParent.GetComponentInChildren<Door>();
                if (rootParentDoor != null)
                {
                    return rootParentDoor;
                }
            }

            return null; // No Door component found
        }

        private void HandleProximityInteraction()
        {
            Transform player = FindPlayerInRange();
            if (player != null)
            {
                float distanceToLever = Vector3.Distance(player.position, transform.position);

                if (!isPlayerWithinRange && distanceToLever <= activationDistance)
                {
                    isPlayerWithinRange = true;
                }

                ShowInteractionText(transform.position + Vector3.up * textYOffset);
            }
            else
            {
                if (isPlayerWithinRange)
                {
                    isPlayerWithinRange = false;
                    HideInteractionText();
                }
            }
        }

        private void HandleMouseClickInteraction()
        {
            if (mainCamera == null || !isPlayerWithinRange) return;

            // Using new InputSystem
            Ray ray = mainCamera.ScreenPointToRay(Mouse.current.position.ReadValue());

            if (Physics.Raycast(ray, out RaycastHit hit, Mathf.Infinity, ~raycastIgnoreLayers) &&
                hit.collider.gameObject == gameObject)
            {
                if (Mouse.current.leftButton.wasPressedThisFrame)
                {
                    ToggleLever();
                }
            }
        }

        private Transform FindPlayerInRange()
        {
            Collider[] colliders = Physics.OverlapSphere(transform.position, activationDistance);
            foreach (var collider in colliders)
            {
                if (collider.CompareTag(playerTag))
                {
                    return collider.transform;
                }
            }
            return null;
        }

        private void ShowInteractionText(Vector3 position)
        {
            if (interactionText != null)
            {
                interactionText.SetText(controlledDoor.IsOpen ? "Close \"Click\"" : "Open \"Click\"");
                interactionText.gameObject.SetActive(true);
                interactionText.transform.position = position;
            }
        }

        private void HideInteractionText()
        {
            if (interactionText != null)
            {
                interactionText.gameObject.SetActive(false);
            }
        }

        private void ToggleLever()
        {
            if (controlledDoor == null || doorEventChannel == null) return;

            if (rotationCoroutine != null)
            {
                StopCoroutine(rotationCoroutine);
            }

            isLeverToggled = !isLeverToggled;
            targetRotation = isLeverToggled
                ? Quaternion.Euler(initialRotation.eulerAngles.x, initialRotation.eulerAngles.y, initialRotation.eulerAngles.z + leverRotationAngle)
                : initialRotation;

            rotationCoroutine = StartCoroutine(RotateLever(() =>
            {
                if (controlledDoor.IsOpen)
                {
                    doorEventChannel.RaiseClose(true);
                }
                else
                {
                    doorEventChannel.RaiseOpen(transform.position, true);
                }
            }));
        }

        private IEnumerator RotateLever(System.Action onComplete)
        {
            Quaternion startRotation = objectToRotate != null ? objectToRotate.rotation : transform.rotation;
            float elapsedTime = 0f;
            float duration = 1f / leverRotationSpeed;

            if (leverSounds != null && leverSounds.Count > 0 && audioSource != null)
            {
                AudioClip randomClip = leverSounds[Random.Range(0, leverSounds.Count)];
                // Added null check for the selected AudioClip
                if (randomClip != null)
                {
                    audioSource.PlayOneShot(randomClip);

                    if (matchRotationSpeedToSound)
                    {
                        duration = randomClip.length;
                    }
                }
                else
                {
                    Debug.LogWarning("The assigned lever sounds are missing. Please check for missing sounds on: " + gameObject.name);
                }
            }

            while (elapsedTime < duration)
            {
                if (objectToRotate != null)
                {
                    objectToRotate.rotation = Quaternion.Slerp(startRotation, targetRotation, elapsedTime / duration);
                }
                else
                {
                    transform.rotation = Quaternion.Slerp(startRotation, targetRotation, elapsedTime / duration);
                }
                elapsedTime += Time.deltaTime;
                yield return null;
            }

            if (objectToRotate != null)
            {
                objectToRotate.rotation = targetRotation;
            }
            else
            {
                transform.rotation = targetRotation;
            }

            onComplete?.Invoke();
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

        private void OnDrawGizmos()
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(transform.position, activationDistance);
        }
    }
}
