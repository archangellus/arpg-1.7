using UnityEngine;
using UnityEngine.InputSystem;

[DisallowMultipleComponent]
public class PetSummoner : MonoBehaviour
{
    [Header("Input")]
    [Tooltip("Assign a Button action from the New Input System that summons the pet.")]
    public InputActionReference summonAction;

    [Header("Pet")]
    public GameObject petPrefab;
    public Vector3 spawnOffset = new(1.5f, 0f, -1.5f);

    private GameObject spawnedPet;

    private void OnEnable()
    {
        if (summonAction == null || summonAction.action == null)
            return;

        summonAction.action.Enable();
        summonAction.action.performed += OnSummonPerformed;
    }

    private void OnDisable()
    {
        if (summonAction == null || summonAction.action == null)
            return;

        summonAction.action.performed -= OnSummonPerformed;
        summonAction.action.Disable();
    }

    private void OnSummonPerformed(InputAction.CallbackContext _)
    {
        if (spawnedPet == null)
        {
            SpawnPet();
            return;
        }

        var gatherer = spawnedPet.GetComponent<PetGatherer>();
        if (gatherer != null)
            gatherer.TryStartGather();
    }

    private void SpawnPet()
    {
        Vector3 spawnPosition = transform.position + transform.TransformDirection(spawnOffset);
        spawnedPet = Instantiate(petPrefab, spawnPosition, Quaternion.identity);

        var ai = spawnedPet.GetComponent<AIController>();
        if (ai != null)
            ai.playerTAG = gameObject.tag;

        var gatherer = spawnedPet.GetComponent<PetGatherer>();
        if (gatherer == null)
            gatherer = spawnedPet.AddComponent<PetGatherer>();

        gatherer.SetPlayer(transform);
    }
}
