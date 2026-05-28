using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace PLAYERTWO.ARPGProject.InfinityWardrobe
{
    /// <summary>
    /// Central runtime coordinator that attaches <see cref="InfinityWardrobeBinder"/>
    /// components to every entity item manager it can find.
    /// </summary>
    [DisallowMultipleComponent]
    internal sealed class InfinityWardrobeService : MonoBehaviour
    {
        [Tooltip(
            "Library that maps items to wardrobe objects. When left empty the " +
            "asset located at Resources/InfinityWardrobeLibrary is loaded automatically."
        )]
        [SerializeField]
        private InfinityWardrobeLibrary m_library;

        private readonly List<InfinityWardrobeBinder> m_binders = new();

        private void Awake()
        {
            if (m_library == null)
            {
                m_library = Resources.Load<InfinityWardrobeLibrary>(InfinityWardrobeLibrary.DefaultResourcePath);

                if (m_library == null)
                {
                    Debug.LogWarning(
                        "[InfinityWardrobe] No library assigned and no default asset was found in Resources."
                    );
                }
            }
        }

        private void OnEnable()
        {
            SceneManager.sceneLoaded += HandleSceneLoaded;
            EventBus.InfinityWardrobeRefreshEvent += HandleRefreshRequested;
            AttachBindersToScene();
        }

        private void OnDisable()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            EventBus.InfinityWardrobeRefreshEvent -= HandleRefreshRequested;
        }

        public void Shutdown()
        {
            foreach (var binder in m_binders)
            {
                if (binder)
                    Destroy(binder);
            }

            m_binders.Clear();
        }

        private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            AttachBindersToScene();
        }

        private void HandleRefreshRequested(Entity entity)
        {
            if (entity != null && entity.items)
            {
                if (entity.items.TryGetComponent<InfinityWardrobeBinder>(out var binder))
                    binder.ApplyImmediate();
                return;
            }

            for (int i = m_binders.Count - 1; i >= 0; i--)
            {
                if (!m_binders[i])
                {
                    m_binders.RemoveAt(i);
                    continue;
                }

                m_binders[i].ApplyImmediate();
            }
        }

        private void AttachBindersToScene()
        {
            var managers = Object.FindObjectsByType<EntityItemManager>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None
            );

            foreach (var manager in managers)
                AttachBinder(manager);
        }

        private void AttachBinder(EntityItemManager manager)
        {
            if (manager == null)
                return;

            if (!InfinityWardrobeBinder.HasWardrobeManager(manager))
                return;

            if (!manager.TryGetComponent(out InfinityWardrobeBinder binder))
                binder = manager.gameObject.AddComponent<InfinityWardrobeBinder>();

            if (!m_binders.Contains(binder))
                m_binders.Add(binder);

            binder.Configure(this, m_library);
        }

        internal void NotifyBinderDestroyed(InfinityWardrobeBinder binder)
        {
            if (binder == null)
                return;

            m_binders.Remove(binder);
        }

        internal void ApplyLibraryToBinder(InfinityWardrobeBinder binder)
        {
            if (binder == null)
                return;

            binder.Configure(this, m_library);
        }
    }
}
