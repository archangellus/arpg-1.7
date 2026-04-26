using UnityEngine;

namespace PLAYERTWO.ARPGProject.ArcDrop
{
    [Plugin("arc-drop", DisplayName = "Arc Drop", Version = "1.0.0", LoadOrder = 200)]
    public class ArcDropPlugin : IPlugin
    {
        private GameObject m_runtimeHost;

        public void Initialize()
        {
            var existingRuntime = Object.FindFirstObjectByType<ArcDropRuntime>(FindObjectsInactive.Include);

            if (existingRuntime)
            {
                m_runtimeHost = existingRuntime.gameObject;
                Debug.Log("[ArcDropPlugin] Using existing ArcDropRuntime in scene");
            }
            else
            {
                m_runtimeHost = new GameObject("[ArcDropPlugin]");
                m_runtimeHost.AddComponent<ArcDropRuntime>();
                Debug.Log("[ArcDropPlugin] Created ArcDropRuntime host");
            }

            Object.DontDestroyOnLoad(m_runtimeHost);
            Debug.Log("[ArcDropPlugin] Initialized");
        }

        public void Shutdown()
        {
            if (m_runtimeHost != null)
            {
                Object.Destroy(m_runtimeHost);
                m_runtimeHost = null;
            }

            Debug.Log("[ArcDropPlugin] Shutdown");
        }
    }
}