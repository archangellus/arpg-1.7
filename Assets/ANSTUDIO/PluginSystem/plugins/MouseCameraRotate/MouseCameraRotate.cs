using UnityEngine;

namespace PLAYERTWO.ARPGProject.Plugins.MouseCameraRotate
{
    [Plugin("mouse-camera-rotate", DisplayName = "Mouse Camera Rotate", Version = "1.0.0", LoadOrder = 150)]
    public sealed class MouseCameraRotate : IPlugin
    {
        private GameObject _go;

        public void Initialize()
        {
            Debug.Log("[MouseCameraRotate] Initialize()");
            _go = new GameObject("MouseCameraRotateRunner");
            Object.DontDestroyOnLoad(_go);
            _go.AddComponent<MouseCameraRotateRunner>();
        }

        public void Shutdown()
        {
            Debug.Log("[MouseCameraRotate] Shutdown()");
            if (_go != null) Object.Destroy(_go);
            _go = null;
        }
    }
}
