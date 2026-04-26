using UnityEngine;
namespace ProjectDoors
{
    public class TriggerVisibility : MonoBehaviour
    {
        // Public bool to control the renderer state
        [Tooltip("Allow to automatically hide and show this object when entering or exiting play mode.")]
        public bool manageVisibility = true;

        // Reference to the MeshRenderer component
        private MeshRenderer meshRenderer;

        void Start()
        {
            // Get the MeshRenderer component attached to the GameObject
            meshRenderer = GetComponent<MeshRenderer>();

            // Check if the MeshRenderer exists
            if (meshRenderer != null)
            {
                // Enable or disable the renderer based on the bool
                meshRenderer.enabled = !manageVisibility;
            }
            else
            {
                Debug.LogWarning("MeshRenderer component not found on this GameObject.");
            }
        }

        // Update the renderer based on the bool during runtime if it changes
        void Update()
        {
            if (meshRenderer != null && meshRenderer.enabled != !manageVisibility)
            {
                meshRenderer.enabled = !manageVisibility;
            }
        }
    }
}