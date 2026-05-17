using UnityEngine;
using UnityEngine.InputSystem;

[AddComponentMenu("RPG/Pet Key Spawner")]
public class PetKeySpawner : MonoBehaviour
{
    [Header("Spawn Settings")]
    public GameObject petPrefab;
    public Transform spawnOrigin;
    public float spawnForwardOffset = 1.5f;
    public float spawnHeightOffset = 0f;

    [Header("Input Settings")]
    public Key spawnKey = Key.P;

    private GameObject currentPet;

    private void Update()
    {
        if (Keyboard.current == null || !Keyboard.current[spawnKey].wasPressedThisFrame)
            return;

        SpawnOrReplacePet();
    }

    public void SpawnOrReplacePet()
    {
        if (petPrefab == null)
            return;

        Transform origin = spawnOrigin != null ? spawnOrigin : transform;
        Vector3 position = origin.position + origin.forward * spawnForwardOffset + Vector3.up * spawnHeightOffset;

        if (currentPet != null)
            Destroy(currentPet);

        currentPet = Instantiate(petPrefab, position, origin.rotation);
    }
}
