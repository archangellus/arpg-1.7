using UnityEngine;

namespace PLAYERTWO.ARPGProject
{
    [AddComponentMenu("PLAYER TWO/ARPG Project/Misc/Blob Shadow")]
    public class BlobShadow : MonoBehaviour
    {
        [Header("Blob Shadow Settings")]
        [Tooltip("The source transform to cast the ray from.")]
        public Transform source;

        [Tooltip("The maximum distance to cast the ray.")]
        public float maxGroundDistance = 10f;

        [Tooltip("The offset from the ground.")]
        public float groundOffset = 0.01f;

        [Tooltip("The layer mask to use for the ground.")]
        public LayerMask groundLayer = 1 << 0;

        protected virtual void LateUpdate()
        {
            if (source == null)
                return;

            var colliding = Physics.Raycast(
                source.position,
                Vector3.down,
                out RaycastHit hit,
                maxGroundDistance,
                groundLayer
            );

            if (colliding)
            {
                transform.position = hit.point + Vector3.up * groundOffset;

                if (!gameObject.activeSelf)
                    gameObject.SetActive(true);
            }
            else if (gameObject.activeSelf)
                gameObject.SetActive(false);
        }
    }
}
