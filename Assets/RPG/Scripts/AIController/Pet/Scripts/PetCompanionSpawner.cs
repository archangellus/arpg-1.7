using PLAYERTWO.ARPGProject;
using UnityEngine;
using UnityEngine.InputSystem;

public class PetCompanionSpawner : MonoBehaviour
{
    [Header("Input")]
    [Tooltip("Bind this to an Input Action in the New Input System (for example Keyboard/P). Pressing it toggles pet spawn/despawn.")]
    public InputActionReference spawnPetAction;

    [Header("Pet")]
    public GameObject petPrefab;
    public float spawnDistanceFromPlayer = 1.5f;

    private GameObject currentPet;

    private void OnEnable()
    {
        if (spawnPetAction?.action == null)
            return;

        spawnPetAction.action.performed += OnSpawnPet;
        spawnPetAction.action.Enable();
    }

    private void OnDisable()
    {
        if (spawnPetAction?.action == null)
            return;

        spawnPetAction.action.performed -= OnSpawnPet;
        spawnPetAction.action.Disable();
    }

    private void OnSpawnPet(InputAction.CallbackContext _)
    {
        if (currentPet != null)
        {
            Destroy(currentPet);
            currentPet = null;
            return;
        }

        if (petPrefab == null)
            return;

        var forward = transform.forward;
        forward.y = 0f;
        if (forward.sqrMagnitude <= 0.001f)
            forward = Vector3.forward;

        var spawnPosition = transform.position + forward.normalized * spawnDistanceFromPlayer;
        currentPet = Instantiate(petPrefab, spawnPosition, Quaternion.identity);

        var ownership = currentPet.GetComponent<PetSummonOwnership>();
        if (ownership == null)
            ownership = currentPet.AddComponent<PetSummonOwnership>();

        if (TryGetComponent<Entity>(out var ownerEntity))
            ownership.ownerEntityId = ownerEntity.GetInstanceID();

        if (currentPet.TryGetComponent(out AIController controller))
        {
            controller.playerTAG = tag;
        }
    }
}
