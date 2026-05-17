using UnityEngine;
using UnityEngine.InputSystem;

public class PetSpawnController : MonoBehaviour
{
    [Header("Spawn")]
    public GameObject petPrefab;
    public Transform spawnAnchor;
    public Vector3 spawnOffset = new Vector3(1.5f, 0f, 0f);
    public bool onlyOnePet = true;

    [Header("Input")]
    [Tooltip("Assign an Input Action (Button) so the spawn key is fully editable in the New Input System.")]
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

        spawnPetAction.action.performed += OnSpawnPerformed;
    }

    private void OnDisable()
    {
        if (spawnPetAction?.action != null)
            spawnPetAction.action.performed -= OnSpawnPerformed;

        if (enabledByThisScript && spawnPetAction?.action != null)
            spawnPetAction.action.Disable();

        enabledByThisScript = false;
    }

    private void OnSpawnPerformed(InputAction.CallbackContext _)
    {
        if (petPrefab == null)
            return;

        if (onlyOnePet && spawnedPet != null)
            return;

        Transform anchor = spawnAnchor != null ? spawnAnchor : transform;
        Vector3 spawnPosition = anchor.position + anchor.TransformDirection(spawnOffset);
        Quaternion spawnRotation = anchor.rotation;

        spawnedPet = Instantiate(petPrefab, spawnPosition, spawnRotation);
    }
}
