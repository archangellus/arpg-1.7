using UnityEngine;

namespace PLAYERTWO.ARPGProject.ItemComparison
{
    /// <summary>
    /// Plugin entry point that wires item inspector comparison behaviour.
    /// </summary>
    [Plugin("item-comparison", DisplayName = "Item Comparison", Version = "1.0.0", LoadOrder = 250)]
    public class ItemComparison : IPlugin
    {
        public void Initialize()
        {
            EventBus.ItemInspectorShownEvent += OnInspectorShown;
            EventBus.ItemInspectorUpdatedEvent += OnInspectorUpdated;
            EventBus.ItemInspectorHiddenEvent += OnInspectorHidden;
            Debug.Log("[ItemComparison] Initialized");
        }

        public void Shutdown()
        {
            EventBus.ItemInspectorShownEvent -= OnInspectorShown;
            EventBus.ItemInspectorUpdatedEvent -= OnInspectorUpdated;
            EventBus.ItemInspectorHiddenEvent -= OnInspectorHidden;
            Debug.Log("[ItemComparison] Shutdown");
        }

        private static void OnInspectorShown(GUIItemInspector inspector, ItemInstance item, GUIItem guiItem)
        {
            if (inspector == null || item == null)
                return;

            var extension = inspector.gameObject.GetComponent<ItemComparisonExtension>();
            if (extension == null)
                extension = inspector.gameObject.AddComponent<ItemComparisonExtension>();

            extension.Configure(inspector, item, guiItem);
        }

        private static void OnInspectorUpdated(GUIItemInspector inspector, ItemInstance item, GUIItem guiItem)
        {
            var extension = inspector == null ? null : inspector.gameObject.GetComponent<ItemComparisonExtension>();
            extension?.Refresh(item, guiItem);
        }

        private static void OnInspectorHidden(GUIItemInspector inspector, ItemInstance item, GUIItem guiItem)
        {
            var extension = inspector == null ? null : inspector.gameObject.GetComponent<ItemComparisonExtension>();
            extension?.HandleHidden();
        }
    }
}
