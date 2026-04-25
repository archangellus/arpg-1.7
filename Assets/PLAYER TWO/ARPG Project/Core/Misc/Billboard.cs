using UnityEngine;

namespace PLAYERTWO.ARPGProject
{
    [AddComponentMenu("PLAYER TWO/ARPG Project/Misc/Billboard")]
    public class Billboard : MonoBehaviour
    {
        protected Camera m_camera;

        protected Quaternion m_originalRotation;

        protected virtual void InitializeRotation() => m_originalRotation = transform.rotation;
        protected virtual void InitializeCamera() => m_camera = Camera.main;

        protected virtual void FaceCamera()
        {
            var forward = -m_camera.transform.forward;
            transform.rotation = Quaternion.LookRotation(forward, Vector3.up);
        }

        protected virtual void Start()
        {
            InitializeRotation();
            InitializeCamera();
        }

        protected virtual void LateUpdate() => FaceCamera();
    }
}
