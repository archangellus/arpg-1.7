using UnityEngine;
using System.Collections;
using UnityEngine.InputSystem;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace ProjectDoors
{
    // [ExecuteAlways] ensures that OnValidate is called even when not in play mode.
    //[ExecuteAlways]
    public class ProximityStopper : MonoBehaviour
    {
        [SerializeField] private ProximityEventChannel proximityEventChannel;
        [SerializeField] private float stopRange = 1.44f;
        [SerializeField] private float releaseDelay = 0.1f; // Delay before resuming player movement

        private Transform playerTransform;
        private ClickAgentController clickAgentController;
        private PlayerController playerController;
        private bool isPlayerInRange = false;
        private bool isPlayerStoppedCompletely = false;
        private bool playerHasClickedOnObject = false;
        private Vector3 storedPlayerPosition;
        private GameObject player;

        private Camera mainCamera;

        /// <summary>
        /// This method is called in the editor whenever a value is changed in the Inspector.
        /// It will automatically assign the ProximityEventChannel if it is not already set.
        /// </summary>
        private void OnValidate()
        {
#if UNITY_EDITOR
            if (proximityEventChannel == null)
            {
                // Make sure the path matches exactly the location and filename of your asset.
                const string assetPath = "Assets/ANSTUDIO/Doors Project/Demo/EventChannels/ProximityTypes/Default Door Proximity Event Channel.asset";
                proximityEventChannel = AssetDatabase.LoadAssetAtPath<ProximityEventChannel>(assetPath);
                if (proximityEventChannel == null)
                {
                    Debug.LogError($"Could not load ProximityEventChannel asset at path: {assetPath}");
                }
            }
#endif
        }

        private void Start()
        {
            InitializePlayerReferences();
            mainCamera = Camera.main;
        }

        private void Update()
        {
            if (!ArePlayerComponentsInitialized()) return;

            HandleProximity();
            HandlePlayerInput();
        }

        private void HandleProximity()
        {
            float distanceToPlayer = GetDistanceToPlayer();

            if (distanceToPlayer <= stopRange)
            {
                if (!isPlayerInRange)
                {
                    isPlayerInRange = true;
                    proximityEventChannel?.RaiseEnterProximity(transform.position);
                    StopPlayerMovement();
                }
            }
            else
            {
                if (isPlayerInRange)
                {
                    isPlayerInRange = false;
                    proximityEventChannel?.RaiseExitProximity(transform.position);
                    ResumePlayerMovement();
                }
            }
        }

        private void HandlePlayerInput()
        {
            if (!isPlayerInRange) return;

            // Check if the mouse is held
            if (Mouse.current.leftButton.isPressed)
            {
                if (IsClickOnThisObject())
                {
                    // Stop the player if the cursor is on the object
                    StopPlayerMovement();
                    playerHasClickedOnObject = true;
                    storedPlayerPosition = playerTransform.position; // Register current position
                    playerController.agent.Warp(storedPlayerPosition); // Lock the player's position
                }
                else
                {
                    // Allow movement if the cursor is not on the object
                    ResumePlayerMovement();
                }
            }
            else if (!Mouse.current.leftButton.isPressed && playerHasClickedOnObject && isPlayerStoppedCompletely)
            {
                // If the mouse button is released after clicking the object, allow movement after a delay
                StartCoroutine(ReleasePlayerAfterDelay());
            }

            if (playerHasClickedOnObject && playerController.agent.remainingDistance > 0.1f)
            {
                // Warp back if the player attempts to move after clicking the object
                playerController.agent.Warp(storedPlayerPosition);
            }
        }

        private void StopPlayerMovement()
        {
            clickAgentController.StopAgentMovement();
            
            if (playerController && playerController.agent)
            {
                playerController.agent.isStopped = true;
            }
            playerController.agent.ResetPath();
            isPlayerStoppedCompletely = true;
        }

        private void ResumePlayerMovement()
        {
            clickAgentController.ResumeAgentMovement();
            if (playerController && playerController.agent)
            {
                playerController.agent.isStopped = false;
            }
            isPlayerStoppedCompletely = false;
            playerHasClickedOnObject = false;
        }

        private IEnumerator ReleasePlayerAfterDelay()
        {
            yield return new WaitForSeconds(releaseDelay);

            if (!IsClickOnThisObject())
            {
                ResumePlayerMovement();
            }
            else
            {
                StopPlayerMovement();
                playerController.agent.Warp(storedPlayerPosition); // Warp back to stored position
            }
        }

        private bool IsClickOnThisObject()
        {
            Ray ray = mainCamera.ScreenPointToRay(Mouse.current.position.ReadValue());
            RaycastHit[] hits = Physics.RaycastAll(ray);
            foreach (RaycastHit hit in hits)
            {
                if (hit.transform == transform)
                {
                    return true;
                }
            }
            return false;
        }

        private float GetDistanceToPlayer()
        {
            return Vector3.Distance(transform.position, playerTransform.position);
        }

        private bool ArePlayerComponentsInitialized()
        {
            return playerTransform != null && clickAgentController != null && playerController != null;
        }

        private void InitializePlayerReferences()
        {
            player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
            {
                playerTransform = player.transform;
                clickAgentController = player.GetComponent<ClickAgentController>();
                playerController = player.GetComponent<PlayerController>();
                if (clickAgentController == null)
                {
                    Debug.LogError("ClickAgentController not found on the player object.");
                }
                if (playerController == null)
                {
                    Debug.LogError("PlayerController not found on the player object.");
                }
            }
            else
            {
                Debug.LogError("Player object with tag 'Player' not found.");
            }
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, stopRange);
        }
    }
}
