using UnityEngine;
using UnityEngine.InputSystem;

[DisallowMultipleComponent]
public class PetSpawnerInput : MonoBehaviour
{
    [Header("Spawn")]
    public GameObject petPrefab;
    public Transform spawnPoint;
    public float spawnDistanceFromPlayer = 1.5f;

    [Header("Input")]
    [Tooltip("Assign an action (Button) from the new Input System to spawn/despawn the pet.")]
    public InputActionReference spawnPetAction;

    private GameObject spawnedPet;
    private bool enabledByThisScript;

    private void OnEnable()
    {
        if (spawnPetAction?.action == null)
            return;

        if (!spawnPetAction.action.enabled)
        {
            spawnPetAction.action.Enable();
            enabledByThisScript = true;
        }

        spawnPetAction.action.performed += OnSpawnPressed;
    }

    private void OnDisable()
    {
        if (spawnPetAction?.action != null)
            spawnPetAction.action.performed -= OnSpawnPressed;

        if (enabledByThisScript && spawnPetAction?.action != null)
            spawnPetAction.action.Disable();

        enabledByThisScript = false;
    }

    private void OnSpawnPressed(InputAction.CallbackContext _)
    {
        if (spawnedPet == null)
            SpawnPet();
        else
            DespawnPet();
    }

    private void SpawnPet()
    {
        if (petPrefab == null)
            return;

        Vector3 position = spawnPoint != null
            ? spawnPoint.position
            : transform.position + transform.right * spawnDistanceFromPlayer;

        spawnedPet = Instantiate(petPrefab, position, Quaternion.identity);
    }

    private void DespawnPet()
    {
        if (spawnedPet == null)
            return;

        Destroy(spawnedPet);
        spawnedPet = null;
    }
}
