using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace PLAYERTWO.ARPGProject
{
    [CustomEditor(typeof(ItemAffixes))]
    public class ItemAffixesEditor : Editor
    {
        string m_nameFilter = "";
        int m_minTierFilter = 0;
        int m_maxTierFilter = 0;

        string FilterFoldoutKey => $"ARPG.ItemAffixesEditor.filterFoldout.{target.GetInstanceID()}";
        string SummaryFoldoutKey =>
            $"ARPG.ItemAffixesEditor.summaryFoldout.{target.GetInstanceID()}";
        string TierSummaryFoldoutKey =>
            $"ARPG.ItemAffixesEditor.tierSummaryFoldout.{target.GetInstanceID()}";
        ItemAffixes.AffixScope m_scopeFilter = ItemAffixes.AffixScope.None;
        int m_attributeFilterIndex = -1;

        static readonly string[] k_attributeOptions = BuildAttributeOptions();

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            using (new EditorGUI.DisabledScope(true))
                EditorGUILayout.PropertyField(serializedObject.FindProperty("m_Script"));

            DrawSearchFilters();

            if (IsFilterActive())
                DrawFilteredResults();

            EditorGUILayout.Space();
            DrawPropertiesExcluding(serializedObject, "m_Script");

            serializedObject.ApplyModifiedProperties();

            EditorGUILayout.Space();
            DrawScopeSummary();
            DrawTierSummary();
        }

        void DrawSearchFilters()
        {
            bool filterFoldout = SessionState.GetBool(FilterFoldoutKey, true);
            filterFoldout = EditorGUILayout.BeginFoldoutHeaderGroup(
                filterFoldout,
                "Search Filters"
            );
            SessionState.SetBool(FilterFoldoutKey, filterFoldout);

            if (filterFoldout)
            {
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);

                EditorGUILayout.BeginHorizontal();
                GUILayout.Label("Name", GUILayout.Width(40));
                m_nameFilter = EditorGUILayout.TextField(m_nameFilter, GUILayout.MinWidth(60));
                GUILayout.Space(8);
                GUILayout.Label("Min Tier", GUILayout.Width(54));
                m_minTierFilter = EditorGUILayout.IntField(m_minTierFilter, GUILayout.Width(40));
                if (m_minTierFilter < 0)
                    m_minTierFilter = 0;
                GUILayout.Space(8);
                GUILayout.Label("Max Tier", GUILayout.Width(54));
                m_maxTierFilter = EditorGUILayout.IntField(m_maxTierFilter, GUILayout.Width(40));
                if (m_maxTierFilter < 0)
                    m_maxTierFilter = 0;
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.BeginHorizontal();
                GUILayout.Label("Scope", GUILayout.Width(40));
                m_scopeFilter = (ItemAffixes.AffixScope)
                    EditorGUILayout.EnumPopup(m_scopeFilter, GUILayout.MinWidth(80));
                GUILayout.Space(8);
                GUILayout.Label("Attribute", GUILayout.Width(58));
                int popupSelection = m_attributeFilterIndex + 1;
                int newSelection = EditorGUILayout.Popup(
                    popupSelection,
                    k_attributeOptions,
                    GUILayout.MinWidth(80)
                );
                m_attributeFilterIndex = newSelection - 1;
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.EndVertical();
            }

            EditorGUILayout.EndFoldoutHeaderGroup();
        }

        void DrawFilteredResults()
        {
            var affixes = (ItemAffixes)target;
            var prefixIndices = GetFilteredIndices(affixes.prefixes);
            var suffixIndices = GetFilteredIndices(affixes.suffixes);

            EditorGUILayout.Space();

            if (prefixIndices.Count == 0 && suffixIndices.Count == 0)
            {
                EditorGUILayout.HelpBox("No entries match the current filters.", MessageType.Info);
                return;
            }

            var prefixesProp = serializedObject.FindProperty("prefixes");
            var suffixesProp = serializedObject.FindProperty("suffixes");

            DrawFilteredList("Filtered Prefixes", prefixIndices, affixes.prefixes, prefixesProp);
            DrawFilteredList("Filtered Suffixes", suffixIndices, affixes.suffixes, suffixesProp);
        }

        void DrawFilteredList(
            string header,
            List<int> indices,
            List<ItemAffixes.AffixEntry> sourceList,
            SerializedProperty listProp
        )
        {
            EditorGUILayout.LabelField($"{header} ({indices.Count})", EditorStyles.boldLabel);

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUI.indentLevel++;

            foreach (var index in indices)
            {
                var entryProp = listProp.GetArrayElementAtIndex(index);
                var label = new GUIContent($"[{index}] {sourceList[index].name}");
                EditorGUILayout.PropertyField(entryProp, label, true);
            }

            EditorGUI.indentLevel--;
            EditorGUILayout.EndVertical();
        }

        List<int> GetFilteredIndices(List<ItemAffixes.AffixEntry> entries)
        {
            var result = new List<int>();
            bool hasNameFilter = !string.IsNullOrEmpty(m_nameFilter);
            bool hasScopeFilter = m_scopeFilter != ItemAffixes.AffixScope.None;
            bool hasAttributeFilter = m_attributeFilterIndex >= 0;

            for (int i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];

                if (
                    hasNameFilter
                    && (
                        entry.name == null
                        || !entry.name.Contains(
                            m_nameFilter,
                            System.StringComparison.OrdinalIgnoreCase
                        )
                    )
                )
                    continue;

                if (hasScopeFilter && (entry.scope & m_scopeFilter) == 0)
                    continue;

                if (
                    hasAttributeFilter
                    && !HasAttributeType(
                        entry,
                        (ItemAttributes.AttributeType)m_attributeFilterIndex
                    )
                )
                    continue;

                if (m_minTierFilter > 0 && entry.minTier != m_minTierFilter)
                    continue;

                if (m_maxTierFilter > 0 && entry.maxTier != m_maxTierFilter)
                    continue;

                result.Add(i);
            }

            return result;
        }

        bool HasAttributeType(ItemAffixes.AffixEntry entry, ItemAttributes.AttributeType type)
        {
            foreach (var attribute in entry.attributes)
            {
                if (attribute.type == type)
                    return true;
            }

            return false;
        }

        bool IsFilterActive() =>
            !string.IsNullOrEmpty(m_nameFilter)
            || m_scopeFilter != ItemAffixes.AffixScope.None
            || m_attributeFilterIndex >= 0
            || m_minTierFilter > 0
            || m_maxTierFilter > 0;

        void DrawScopeSummary()
        {
            bool summaryFoldout = SessionState.GetBool(SummaryFoldoutKey, true);
            summaryFoldout = EditorGUILayout.BeginFoldoutHeaderGroup(
                summaryFoldout,
                "Scope Summary"
            );
            SessionState.SetBool(SummaryFoldoutKey, summaryFoldout);

            if (summaryFoldout)
            {
                var affixes = (ItemAffixes)target;

                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                DrawSummaryHeader();

                foreach (
                    ItemAffixes.AffixScope scope in System.Enum.GetValues(
                        typeof(ItemAffixes.AffixScope)
                    )
                )
                {
                    if (!IsPrimitiveScope(scope))
                        continue;

                    var prefixCount = affixes.GetPrefixIndices(scope).Count;
                    var suffixCount = affixes.GetSuffixIndices(scope).Count;
                    DrawSummaryRow(scope.ToString(), prefixCount, suffixCount);
                }

                EditorGUILayout.EndVertical();
            }

            EditorGUILayout.EndFoldoutHeaderGroup();
        }

        void DrawTierSummary()
        {
            var affixes = (ItemAffixes)target;
            int maxTier = GetMaxExplicitTier(affixes);

            if (maxTier <= 0)
                return;

            bool tierFoldout = SessionState.GetBool(TierSummaryFoldoutKey, true);
            tierFoldout = EditorGUILayout.BeginFoldoutHeaderGroup(tierFoldout, "Tier Summary");
            SessionState.SetBool(TierSummaryFoldoutKey, tierFoldout);

            if (tierFoldout)
            {
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                DrawTierSummaryHeader();

                for (int t = 1; t <= maxTier; t++)
                {
                    int pfxMin = CountExactTierField(affixes.prefixes, e => e.minTier, t);
                    int sfxMin = CountExactTierField(affixes.suffixes, e => e.minTier, t);
                    int pfxMax = CountExactTierField(affixes.prefixes, e => e.maxTier, t);
                    int sfxMax = CountExactTierField(affixes.suffixes, e => e.maxTier, t);
                    DrawTierSummaryRow($"Tier {t}", pfxMin, sfxMin, pfxMax, sfxMax);
                }

                EditorGUILayout.EndVertical();
            }

            EditorGUILayout.EndFoldoutHeaderGroup();
        }

        int GetMaxExplicitTier(ItemAffixes affixes)
        {
            int max = 0;

            foreach (var entry in affixes.prefixes)
            {
                if (entry.minTier > max)
                    max = entry.minTier;
                if (entry.maxTier > max)
                    max = entry.maxTier;
            }

            foreach (var entry in affixes.suffixes)
            {
                if (entry.minTier > max)
                    max = entry.minTier;
                if (entry.maxTier > max)
                    max = entry.maxTier;
            }

            return max;
        }

        int CountExactTierField(
            List<ItemAffixes.AffixEntry> entries,
            System.Func<ItemAffixes.AffixEntry, int> selector,
            int tier
        )
        {
            int count = 0;

            foreach (var entry in entries)
                if (selector(entry) == tier)
                    count++;

            return count;
        }

        void DrawTierSummaryHeader()
        {
            var rect = EditorGUILayout.GetControlRect();
            float w = rect.width;
            var col0 = new Rect(rect.x, rect.y, w * 0.3f, rect.height);
            var col1 = new Rect(col0.xMax, rect.y, w * 0.175f, rect.height);
            var col2 = new Rect(col1.xMax, rect.y, w * 0.175f, rect.height);
            var col3 = new Rect(col2.xMax, rect.y, w * 0.175f, rect.height);
            var col4 = new Rect(col3.xMax, rect.y, w * 0.175f, rect.height);

            EditorGUI.LabelField(col0, "Tier", EditorStyles.boldLabel);
            EditorGUI.LabelField(col1, "Pfx Min", EditorStyles.boldLabel);
            EditorGUI.LabelField(col2, "Sfx Min", EditorStyles.boldLabel);
            EditorGUI.LabelField(col3, "Pfx Max", EditorStyles.boldLabel);
            EditorGUI.LabelField(col4, "Sfx Max", EditorStyles.boldLabel);
        }

        void DrawTierSummaryRow(string label, int pfxMin, int sfxMin, int pfxMax, int sfxMax)
        {
            var rect = EditorGUILayout.GetControlRect();
            float w = rect.width;
            var col0 = new Rect(rect.x, rect.y, w * 0.3f, rect.height);
            var col1 = new Rect(col0.xMax, rect.y, w * 0.175f, rect.height);
            var col2 = new Rect(col1.xMax, rect.y, w * 0.175f, rect.height);
            var col3 = new Rect(col2.xMax, rect.y, w * 0.175f, rect.height);
            var col4 = new Rect(col3.xMax, rect.y, w * 0.175f, rect.height);

            EditorGUI.LabelField(col0, label);
            EditorGUI.LabelField(col1, pfxMin.ToString());
            EditorGUI.LabelField(col2, sfxMin.ToString());
            EditorGUI.LabelField(col3, pfxMax.ToString());
            EditorGUI.LabelField(col4, sfxMax.ToString());
        }

        void DrawSummaryHeader()
        {
            var rect = EditorGUILayout.GetControlRect();
            var scopeRect = new Rect(rect.x, rect.y, rect.width * 0.6f, rect.height);
            var prefixRect = new Rect(scopeRect.xMax, rect.y, rect.width * 0.2f, rect.height);
            var suffixRect = new Rect(prefixRect.xMax, rect.y, rect.width * 0.2f, rect.height);

            EditorGUI.LabelField(scopeRect, "Scope", EditorStyles.boldLabel);
            EditorGUI.LabelField(prefixRect, "Prefixes", EditorStyles.boldLabel);
            EditorGUI.LabelField(suffixRect, "Suffixes", EditorStyles.boldLabel);
        }

        void DrawSummaryRow(string scopeName, int prefixCount, int suffixCount)
        {
            var rect = EditorGUILayout.GetControlRect();
            var scopeRect = new Rect(rect.x, rect.y, rect.width * 0.6f, rect.height);
            var prefixRect = new Rect(scopeRect.xMax, rect.y, rect.width * 0.2f, rect.height);
            var suffixRect = new Rect(prefixRect.xMax, rect.y, rect.width * 0.2f, rect.height);

            EditorGUI.LabelField(scopeRect, scopeName);
            EditorGUI.LabelField(prefixRect, prefixCount.ToString());
            EditorGUI.LabelField(suffixRect, suffixCount.ToString());
        }

        static string[] BuildAttributeOptions()
        {
            var enumNames = System.Enum.GetNames(typeof(ItemAttributes.AttributeType));
            var options = new string[enumNames.Length + 1];
            options[0] = "All";

            for (int i = 0; i < enumNames.Length; i++)
                options[i + 1] = enumNames[i];

            return options;
        }

        /// <summary>
        /// Returns true for single-bit scope values, excluding <see cref="ItemAffixes.AffixScope.None"/>
        /// and composite convenience flags like <see cref="ItemAffixes.AffixScope.Weapon"/> and
        /// <see cref="ItemAffixes.AffixScope.Armor"/>.
        /// </summary>
        static bool IsPrimitiveScope(ItemAffixes.AffixScope scope)
        {
            int value = (int)scope;
            return value != 0 && (value & (value - 1)) == 0;
        }
    }
}
