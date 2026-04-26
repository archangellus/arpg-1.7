using UnityEngine;
using UnityEngine.AI;
using System.Collections;

namespace ProjectDoors
{
    public class ClickAgentController : MonoBehaviour
    {
        private NavMeshAgent agent;
        private CharacterController characterController;
        private bool isCharacterControllerStopped = false; // Flag to stop CharacterController movement
        [SerializeField] private MovementControlEventChannel movementControlChannel = MovementControlEventChannel.Instance;

        private void Awake()
        {
            if (movementControlChannel == null)
            {
                movementControlChannel = Resources.Load<MovementControlEventChannel>("MovementControlEventChannel");
            }

            StartCoroutine(InitializeControllersWithWait());
        }

        private void OnEnable()
        {
            if (movementControlChannel == null)
            {
                movementControlChannel = MovementControlEventChannel.Instance ??
                    Resources.Load<MovementControlEventChannel>("MovementControlEventChannel");
            }
        }

        private IEnumerator InitializeControllersWithWait()
        {
            // Wait until either NavMeshAgent or CharacterController is available
            yield return new WaitUntil(() =>
            {
                agent = GetComponent<NavMeshAgent>();
                characterController = GetComponent<CharacterController>();
                return agent != null || characterController != null;
            });

            //Debug.Log("ClickAgentController: NavMeshAgent or CharacterController found successfully.");

            // If neither component is found after waiting, log an error
            if (agent == null && characterController == null)
            {
                Debug.LogError("No NavMeshAgent or CharacterController component found!");
            }
        }

        // Stop movement for both NavMeshAgent and CharacterController
        public void StopAgentMovement()
        {
            if (agent != null)
            {
                agent.isStopped = true;
                agent.ResetPath();
            }
            if (characterController != null)
            {
                isCharacterControllerStopped = true;
            }
            movementControlChannel?.RaiseStopMovement();
        }

        // Resume movement for both NavMeshAgent and CharacterController
        public void ResumeAgentMovement()
        {
            if (agent != null && agent.isOnNavMesh && agent.isStopped)
            {
                agent.isStopped = false;
            }
            if (characterController != null)
            {
                isCharacterControllerStopped = false;
            }
            movementControlChannel?.RaiseResumeMovement();
        }

        public bool IsMovementAllowed()
        {
            return !isCharacterControllerStopped;
        }
    }
}
