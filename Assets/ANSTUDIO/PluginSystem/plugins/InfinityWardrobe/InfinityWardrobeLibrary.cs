using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

namespace PLAYERTWO.ARPGProject.InfinityWardrobe
{
    /// <summary>
    /// Holds all mappings between <see cref="ItemArmor"/> assets and wardrobe groups.
    /// </summary>
    [CreateAssetMenu(
        fileName = "InfinityWardrobeLibrary",
        menuName = "ANSTUDIO/Plugin System/Infinity Wardrobe/New Library"
    )]
    public sealed class InfinityWardrobeLibrary : ScriptableObject
    {
        public const string DefaultResourcePath = nameof(InfinityWardrobeLibrary);

        [Tooltip("List of wardrobe rules evaluated whenever an entity equips or removes gear.")]
        [SerializeField]
        private List<InfinityWardrobeRule> m_rules = new();

        /// <summary>
        /// Read-only access to the configured rules.
        /// </summary>
        public IReadOnlyList<InfinityWardrobeRule> Rules => m_rules;
    }

    [System.Serializable]
    public sealed class InfinityWardrobeRule : ISerializationCallbackReceiver
    {
        [Tooltip("Armor assets that should toggle wardrobe objects when equipped.")]
        [SerializeField]
        private List<ItemArmor> m_items = new();

        [FormerlySerializedAs("m_item")]
        [SerializeField, HideInInspector]
        private ItemArmor m_legacyItem;

        [Tooltip("Optional label used only in the inspector to describe this rule.")]
        [SerializeField]
        private string m_description;

        [Tooltip("Prefab & Object Manager group type to inspect (e.g. 'Wardrobe').")]
        [SerializeField]
        private string m_groupType = "Wardrobe";

        [Tooltip("Specific Prefab Group name to target. Leave empty to rely on Group Index instead.")]
        [SerializeField]
        private string m_groupName = "Wardrobe0";

        [Tooltip("Group index used when the name is empty. The value is zero-based and filtered by Group Type.")]
        [SerializeField]
        private int m_groupIndex = -1;

        [Tooltip("Disable every object in the target group before enabling the configured actions.")]
        [SerializeField]
        private bool m_clearGroupBeforeApply;

        [Tooltip("Disable the entire group when the armor is unequipped. Useful for fallback outfits.")]
        [SerializeField]
        private bool m_clearGroupOnUnequip;

        [Tooltip("If enabled the actions are inverted when the armor is unequipped.")]
        [SerializeField]
        private bool m_revertOnUnequip = true;

        [Tooltip("Actions executed in the order they are defined.")]
        [SerializeField]
        private List<InfinityWardrobeObjectAction> m_actions = new();

        public IReadOnlyList<ItemArmor> Items
        {
            get
            {
                if (m_items == null)
                    m_items = new List<ItemArmor>();

                return m_items;
            }
        }
        public ItemArmor PrimaryItem => GetFirstValidItem();
        public bool HasUsableItems => GetFirstValidItem() != null;
        public string Description => m_description;
        public string GroupType => m_groupType;
        public string GroupName => m_groupName;
        public int GroupIndex => m_groupIndex;
        public bool ClearGroupBeforeApply => m_clearGroupBeforeApply;
        public bool ClearGroupOnUnequip => m_clearGroupOnUnequip;
        public bool RevertOnUnequip => m_revertOnUnequip;
        public IReadOnlyList<InfinityWardrobeObjectAction> Actions => m_actions;
        public bool MatchesEquippedItems(HashSet<Item> equipped)
        {
            if (equipped == null)
                return false;

            if (m_items == null)
                return false;

            for (int i = 0; i < m_items.Count; i++)
            {
                var armor = m_items[i];
                if (!armor)
                    continue;

                if (equipped.Contains(armor))
                    return true;
            }

            return false;
        }

        public void OnBeforeSerialize()
        {
            MigrateLegacyItem();
        }

        public void OnAfterDeserialize()
        {
            MigrateLegacyItem();
        }

        private ItemArmor GetFirstValidItem()
        {
            if (m_items == null)
                return null;

            for (int i = 0; i < m_items.Count; i++)
            {
                var armor = m_items[i];
                if (armor)
                    return armor;
            }

            return null;
        }

        private void MigrateLegacyItem()
        {
            if (!m_legacyItem)
                return;

            if (m_items == null)
                m_items = new List<ItemArmor>();

            if (!m_items.Contains(m_legacyItem))
                m_items.Insert(0, m_legacyItem);

            m_legacyItem = null;
        }
    }

    [System.Serializable]
    public sealed class InfinityWardrobeObjectAction
    {
        [Tooltip("Zero-based indices of the Prefab Group objects controlled by this action.")]
        [SerializeField]
        private List<int> m_objectIndices = new List<int> { 0 };

        [FormerlySerializedAs("m_objectIndex")]
        [SerializeField, HideInInspector]
        private int m_legacyObjectIndex = -1;

        [Tooltip("Whether the indexed object is enabled when the armor is equipped.")]
        [SerializeField]
        private bool m_enableOnEquip = true;

        [Tooltip("State to apply when the armor is unequipped. Leave disabled to simply hide the object.")]
        [SerializeField]
        private bool m_enableOnUnequip;

        [Tooltip("Optional note that appears next to the action in the inspector.")]
        [SerializeField]
        private string m_note;

        [Tooltip(
            "Optional list of other object indices to disable immediately when this action runs."
        )]
        [SerializeField]
        private List<int> m_disableOtherObjectIndices = new();

        public IReadOnlyList<int> ObjectIndices
        {
            get
            {
                EnsureObjectIndices();
                return m_objectIndices;
            }
        }
        public bool EnableOnEquip => m_enableOnEquip;
        public bool EnableOnUnequip => m_enableOnUnequip;
        public string Note => m_note;
        public IReadOnlyList<int> DisableOtherObjectIndices => m_disableOtherObjectIndices;

        private void EnsureObjectIndices()
        {
            if (m_objectIndices == null)
                m_objectIndices = new List<int>();

            if (m_legacyObjectIndex >= 0)
            {
                if (!m_objectIndices.Contains(m_legacyObjectIndex))
                    m_objectIndices.Insert(0, m_legacyObjectIndex);

                m_legacyObjectIndex = -1;
            }
        }
    }
}
