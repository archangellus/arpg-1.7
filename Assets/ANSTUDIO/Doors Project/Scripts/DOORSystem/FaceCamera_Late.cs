using UnityEngine;
using System.Collections;

namespace ProjectDoors
{
    public class FaceCamera_Late : MonoBehaviour
    {
        [Tooltip("Reference to the Camera this object should face. If left null, defaults to Camera.main.")]
        Camera referenceCamera;

        public enum Axis { up, down, left, right, forward, back };

        [Tooltip("If true, the object will face away from the camera.")]
        public bool reverseFace = false;

        [Tooltip("Which axis of the object will be aligned toward (or away from) the camera.")]
        public Axis axis = Axis.up;

        [Header("Rotation Offset (in degrees)")]
        [Tooltip("Rotation offset around X axis (in degrees) after facing the camera.")]
        public float rotationOffsetX = 0f;
        [Tooltip("Rotation offset around Y axis (in degrees) after facing the camera.")]
        public float rotationOffsetY = 0f;
        [Tooltip("Rotation offset around Z axis (in degrees) after facing the camera.")]
        public float rotationOffsetZ = 0f;

        // Return a direction based upon the chosen axis
        public Vector3 GetAxis(Axis refAxis)
        {
            switch (refAxis)
            {
                case Axis.down:
                    return Vector3.down;
                case Axis.forward:
                    return Vector3.forward;
                case Axis.back:
                    return Vector3.back;
                case Axis.left:
                    return Vector3.left;
                case Axis.right:
                    return Vector3.right;
                // default: up
                default:
                    return Vector3.up;
            }
        }

        void Awake()
        {
            // If no camera referenced, grab the main camera
            if (referenceCamera == null)
            {
                referenceCamera = Camera.main;
            }
        }

        void LateUpdate()
        {
            // Make sure we have a valid camera
            if (referenceCamera == null)
            {
                referenceCamera = Camera.main;
                if (referenceCamera == null)
                    return; // exit if there's still no camera
            }

            // Compute the position the object should look at
            Vector3 direction = reverseFace ? Vector3.forward : Vector3.back;
            Vector3 targetPos = transform.position + referenceCamera.transform.rotation * direction;

            // Compute the 'up' vector (or chosen axis) for LookAt
            Vector3 upDirection = referenceCamera.transform.rotation * GetAxis(axis);

            // First, rotate to face the camera (or away, if reverseFace == true)
            transform.LookAt(targetPos, upDirection);

            // Then apply additional user-defined Euler rotation offsets
            // Construct a Quaternion from the X/Y/Z rotation offsets, and multiply:
            Quaternion offsetQuat = Quaternion.Euler(rotationOffsetX, rotationOffsetY, rotationOffsetZ);
            transform.rotation = transform.rotation * offsetQuat;
        }
    }
}
