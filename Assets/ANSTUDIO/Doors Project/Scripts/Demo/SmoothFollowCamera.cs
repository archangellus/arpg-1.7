using UnityEngine;
namespace ProjectDoors
{
    public class SmoothFollowCamera : MonoBehaviour
    {
        private Transform target; // Target to follow
        public Vector3 offsetPosition = new(0, 15, -10); // Offset from the target
        public float positionSmoothTime = 0.3f; // Smoothing time for position
        public Vector3 offsetRotation = new(53, 0, 0); // Offset rotation

        private Vector3 velocity = Vector3.zero; // Velocity for smooth damp

        void Start()
        {
            // Automatically find the player object by tag
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player)
            {
                target = player.transform;
            }
        }

        void LateUpdate()
        {
            if (target)
            {
                // Smoothly move the camera towards the target's position plus the offset
                Vector3 targetPosition = target.position + offsetPosition;
                transform.position = Vector3.SmoothDamp(transform.position, targetPosition, ref velocity, positionSmoothTime);

                // Apply rotation offset
                transform.rotation = Quaternion.Euler(offsetRotation);
            }
        }
    }
}
