using UnityEngine;

namespace PLAYERTWO.ARPGProject.InfinityWardrobe
{
    /// <summary>
    /// Bootstraps the Infinity Wardrobe system by creating the runtime service
    /// that watches every <see cref="EntityItemManager"/>.
    /// </summary>
    [Plugin(
        "infinity-wardrobe",
        DisplayName = "Infinity Wardrobe",
        Version = "1.0.0",
        LoadOrder = 350
    )]
    public sealed class InfinityWardrobePlugin : IPlugin
    {
        private InfinityWardrobeService m_service;

        public void Initialize()
        {
            if (m_service != null)
                return;

            var host = new GameObject("[Plugin] Infinity Wardrobe Service");
            Object.DontDestroyOnLoad(host);
            host.hideFlags = HideFlags.HideAndDontSave;
            m_service = host.AddComponent<InfinityWardrobeService>();

            Debug.Log("[InfinityWardrobe] Initialized");
        }

        public void Shutdown()
        {
            if (m_service != null)
            {
                m_service.Shutdown();
                Object.Destroy(m_service.gameObject);
                m_service = null;
            }

            Debug.Log("[InfinityWardrobe] Shutdown");
        }
    }
}
