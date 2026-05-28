#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace PLAYERTWO.ARPGProject.InfinityWardrobe
{
    [CustomPropertyDrawer(typeof(InfinityWardrobeObjectAction))]
    public sealed class InfinityWardrobeObjectActionDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);

            float lineHeight = EditorGUIUtility.singleLineHeight;
            float spacing = EditorGUIUtility.standardVerticalSpacing;

            SerializedProperty indicesProp = property.FindPropertyRelative("m_objectIndices");
            SerializedProperty enableOnEquipProp = property.FindPropertyRelative("m_enableOnEquip");
            SerializedProperty enableOnUnequipProp = property.FindPropertyRelative("m_enableOnUnequip");
            SerializedProperty noteProp = property.FindPropertyRelative("m_note");
            SerializedProperty disableOthersProp = property.FindPropertyRelative("m_disableOtherObjectIndices");

            Rect current = new(position.x, position.y, position.width, lineHeight);

            float indicesHeight = EditorGUI.GetPropertyHeight(indicesProp, new GUIContent("Object Indices"), true);
            current.height = indicesHeight;
            EditorGUI.PropertyField(current, indicesProp, new GUIContent("Object Indices"), true);

            current.y += indicesHeight + spacing;
            current.height = lineHeight;

            bool equipValue = enableOnEquipProp.boolValue;
            EditorGUI.BeginChangeCheck();
            bool newEquip = EditorGUI.ToggleLeft(current, new GUIContent("Enable On Equip", enableOnEquipProp.tooltip), equipValue);
            if (EditorGUI.EndChangeCheck())
            {
                enableOnEquipProp.boolValue = newEquip;
                if (newEquip)
                    enableOnUnequipProp.boolValue = false;
            }

            current.y += lineHeight + spacing;

            bool unequipValue = enableOnUnequipProp.boolValue;
            EditorGUI.BeginChangeCheck();
            bool newUnequip = EditorGUI.ToggleLeft(current, new GUIContent("Enable On Unequip", enableOnUnequipProp.tooltip), unequipValue);
            if (EditorGUI.EndChangeCheck())
            {
                enableOnUnequipProp.boolValue = newUnequip;
                if (newUnequip)
                    enableOnEquipProp.boolValue = false;
            }

            current.y += lineHeight + spacing;

            float noteHeight = EditorGUI.GetPropertyHeight(noteProp, new GUIContent("Note"), true);
            current.height = noteHeight;
            EditorGUI.PropertyField(current, noteProp, new GUIContent("Note"));

            current.y += noteHeight + spacing;
            float disableOthersHeight = EditorGUI.GetPropertyHeight(disableOthersProp, new GUIContent("Disable Other Object Indices"), true);
            current.height = disableOthersHeight;
            EditorGUI.PropertyField(current, disableOthersProp, new GUIContent("Disable Other Object Indices"), true);

            EditorGUI.EndProperty();
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            float lineHeight = EditorGUIUtility.singleLineHeight;
            float spacing = EditorGUIUtility.standardVerticalSpacing;

            SerializedProperty indicesProp = property.FindPropertyRelative("m_objectIndices");
            SerializedProperty noteProp = property.FindPropertyRelative("m_note");
            SerializedProperty disableOthersProp = property.FindPropertyRelative("m_disableOtherObjectIndices");

            float total = 0f;

            total += EditorGUI.GetPropertyHeight(indicesProp, new GUIContent("Object Indices"), true);
            total += spacing;

            total += lineHeight; // Enable On Equip
            total += spacing;

            total += lineHeight; // Enable On Unequip
            total += spacing;

            total += EditorGUI.GetPropertyHeight(noteProp, new GUIContent("Note"), true);
            total += spacing;

            total += EditorGUI.GetPropertyHeight(disableOthersProp, new GUIContent("Disable Other Object Indices"), true);

            return total;
        }
    }

    [CustomEditor(typeof(InfinityWardrobeLibrary))]
    public sealed class InfinityWardrobeLibraryEditor : Editor
    {
        private SerializedProperty m_rulesProperty;
        private readonly Dictionary<string, bool> m_foldoutStates = new();
        private string m_searchValue = string.Empty;
        private Vector2 m_scrollPosition;
        private int m_pendingRemovalIndex = -1;
        private string m_pendingRemovalPath;
        private static readonly List<string> s_ItemNames = new();

        private const string NoRulesMessage = "Add a rule to start mapping armor to wardrobe groups.";

        private void OnEnable()
        {
            m_rulesProperty = serializedObject.FindProperty("m_rules");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            DrawIntro();
            DrawSearchField();
            DrawToolbar();
            DrawRulesList();

            serializedObject.ApplyModifiedProperties();
        }

        private void DrawIntro()
        {
            EditorGUILayout.HelpBox(
                "Rules link ItemArmor assets to Prefab & Object Manager groups. Configure the group data, set" +
                " the actions to run, and Infinity Wardrobe keeps everything in sync.",
                MessageType.Info
            );
        }

        private void DrawSearchField()
        {
            Rect rect = GUILayoutUtility.GetRect(0, EditorGUIUtility.singleLineHeight, GUILayout.ExpandWidth(true));
            rect.y += 2f;
            rect.height = EditorGUIUtility.singleLineHeight;

            GUIStyle textStyle = UnityEngine.GUI.skin.FindStyle("ToolbarSeachTextField") ?? UnityEngine.GUI.skin.textField;
            GUIStyle cancelStyle = UnityEngine.GUI.skin.FindStyle("ToolbarSeachCancelButton") ?? UnityEngine.GUI.skin.button;

            Rect textRect = rect;
            textRect.width -= rect.height;

            string newSearch = EditorGUI.TextField(textRect, m_searchValue, textStyle);
            if (newSearch != m_searchValue)
            {
                m_searchValue = newSearch ?? string.Empty;
            }

            Rect cancelRect = rect;
            cancelRect.xMin = textRect.xMax;

            if (UnityEngine.GUI.Button(cancelRect, GUIContent.none, cancelStyle))
            {
                m_searchValue = string.Empty;
                UnityEngine.GUI.FocusControl(null);
            }

            GUILayout.Space(2f);
        }

        private void DrawToolbar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

            if (GUILayout.Button(new GUIContent("Add Rule", "Insert a new rule at the end of the list."), EditorStyles.toolbarButton))
            {
                AddRule();
            }

            if (GUILayout.Button(new GUIContent("Expand All", "Expand every visible rule."), EditorStyles.toolbarButton))
            {
                ToggleFoldouts(true);
            }

            if (GUILayout.Button(new GUIContent("Collapse All", "Collapse every visible rule."), EditorStyles.toolbarButton))
            {
                ToggleFoldouts(false);
            }

            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
        }

        private void DrawRulesList()
        {
            if (m_rulesProperty == null)
            {
                EditorGUILayout.HelpBox("Could not find the rules list.", MessageType.Error);
                return;
            }

            int totalRules = m_rulesProperty.arraySize;
            int matchCount = 0;

            using (var scroll = new EditorGUILayout.ScrollViewScope(m_scrollPosition, GUILayout.ExpandHeight(true)))
            {
                m_scrollPosition = scroll.scrollPosition;

                for (int i = 0; i < totalRules; i++)
                {
                    SerializedProperty ruleProp = m_rulesProperty.GetArrayElementAtIndex(i);
                    if (!MatchesSearch(ruleProp))
                    {
                        continue;
                    }

                    matchCount++;
                    DrawRule(i, ruleProp);
                }

                if (matchCount == 0)
                {
                    string message = totalRules == 0 ? NoRulesMessage : "No rules match the current search.";
                    EditorGUILayout.HelpBox(message, MessageType.Warning);
                }
            }

            if (m_pendingRemovalIndex >= 0)
            {
                RemoveRule(m_pendingRemovalIndex);
                m_pendingRemovalIndex = -1;
                m_pendingRemovalPath = null;
                GUIUtility.ExitGUI();
            }

            EditorGUILayout.Space(4f);
            EditorGUILayout.LabelField($"Showing {matchCount} of {totalRules} rules", EditorStyles.miniLabel);
        }

        private void DrawRule(int index, SerializedProperty ruleProp)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            string foldoutKey = ruleProp.propertyPath;
            bool expanded = GetFoldoutState(foldoutKey);

            EditorGUI.BeginChangeCheck();
            expanded = EditorGUILayout.Foldout(expanded, BuildRuleLabel(index, ruleProp), true);
            if (EditorGUI.EndChangeCheck())
            {
                SetFoldoutState(foldoutKey, expanded);
            }

            if (expanded)
            {
                DrawRuleButtons(index);

                EditorGUI.indentLevel++;
                SerializedProperty itemsProp = ruleProp.FindPropertyRelative("m_items");
                EditorGUILayout.PropertyField(itemsProp, new GUIContent("Item Armors"), true);
                if (itemsProp != null && itemsProp.arraySize == 0)
                {
                    EditorGUILayout.HelpBox("Add at least one ItemArmor so the rule can run.", MessageType.Warning);
                }
                EditorGUILayout.PropertyField(ruleProp.FindPropertyRelative("m_description"), new GUIContent("Description"));
                EditorGUILayout.PropertyField(ruleProp.FindPropertyRelative("m_groupType"), new GUIContent("Group Type"));
                EditorGUILayout.PropertyField(ruleProp.FindPropertyRelative("m_groupName"), new GUIContent("Group Name"));
                EditorGUILayout.PropertyField(ruleProp.FindPropertyRelative("m_groupIndex"), new GUIContent("Group Index"));
                EditorGUILayout.PropertyField(ruleProp.FindPropertyRelative("m_clearGroupBeforeApply"), new GUIContent("Clear Group Before Apply"));
                EditorGUILayout.PropertyField(ruleProp.FindPropertyRelative("m_clearGroupOnUnequip"), new GUIContent("Clear Group On Unequip"));
                EditorGUILayout.PropertyField(ruleProp.FindPropertyRelative("m_revertOnUnequip"), new GUIContent("Revert On Unequip"));
                EditorGUILayout.PropertyField(ruleProp.FindPropertyRelative("m_actions"), new GUIContent("Actions"), true);
                EditorGUI.indentLevel--;
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawRuleButtons(int index)
        {
            EditorGUILayout.BeginHorizontal();

            UnityEngine.GUI.enabled = index > 0;
            if (GUILayout.Button(new GUIContent("▲", "Move this rule up."), GUILayout.Width(28)))
            {
                MoveRule(index, -1);
            }

            UnityEngine.GUI.enabled = index < m_rulesProperty.arraySize - 1;
            if (GUILayout.Button(new GUIContent("▼", "Move this rule down."), GUILayout.Width(28)))
            {
                MoveRule(index, 1);
            }

            UnityEngine.GUI.enabled = true;
            GUILayout.FlexibleSpace();
            if (GUILayout.Button(new GUIContent("Delete", "Remove this rule from the library.")))
            {
                m_pendingRemovalIndex = index;
                m_pendingRemovalPath = m_rulesProperty.GetArrayElementAtIndex(index)?.propertyPath;
            }

            EditorGUILayout.EndHorizontal();
        }

        private void AddRule()
        {
            serializedObject.Update();

            int newIndex = m_rulesProperty.arraySize;
            m_rulesProperty.InsertArrayElementAtIndex(newIndex);
            SerializedProperty newRule = m_rulesProperty.GetArrayElementAtIndex(newIndex);
            ResetRule(newRule);
            SetFoldoutState(newRule.propertyPath, true);

            serializedObject.ApplyModifiedProperties();
        }

        private void ResetRule(SerializedProperty rule)
        {
            SerializedProperty items = rule.FindPropertyRelative("m_items");
            if (items != null)
            {
                items.ClearArray();
                items.InsertArrayElementAtIndex(0);
                items.GetArrayElementAtIndex(0).objectReferenceValue = null;
            }
            rule.FindPropertyRelative("m_description").stringValue = string.Empty;
            rule.FindPropertyRelative("m_groupType").stringValue = "Wardrobe";
            rule.FindPropertyRelative("m_groupName").stringValue = "Wardrobe0";
            rule.FindPropertyRelative("m_groupIndex").intValue = -1;
            rule.FindPropertyRelative("m_clearGroupBeforeApply").boolValue = false;
            rule.FindPropertyRelative("m_clearGroupOnUnequip").boolValue = false;
            rule.FindPropertyRelative("m_revertOnUnequip").boolValue = true;

            SerializedProperty actions = rule.FindPropertyRelative("m_actions");
            actions.ClearArray();
        }

        private void MoveRule(int index, int direction)
        {
            serializedObject.Update();

            int targetIndex = Mathf.Clamp(index + direction, 0, m_rulesProperty.arraySize - 1);
            if (targetIndex == index)
            {
                return;
            }

            m_rulesProperty.MoveArrayElement(index, targetIndex);
            serializedObject.ApplyModifiedProperties();
        }

        private void RemoveRule(int index)
        {
            serializedObject.Update();
            if (!string.IsNullOrEmpty(m_pendingRemovalPath))
            {
                m_foldoutStates.Remove(m_pendingRemovalPath);
            }

            m_rulesProperty.DeleteArrayElementAtIndex(index);
            serializedObject.ApplyModifiedProperties();
        }

        private void ToggleFoldouts(bool expand)
        {
            if (m_rulesProperty == null)
            {
                return;
            }

            for (int i = 0; i < m_rulesProperty.arraySize; i++)
            {
                SerializedProperty ruleProp = m_rulesProperty.GetArrayElementAtIndex(i);
                SetFoldoutState(ruleProp.propertyPath, expand);
            }
        }

        private bool GetFoldoutState(string key)
        {
            if (string.IsNullOrEmpty(key))
            {
                return true;
            }

            if (!m_foldoutStates.TryGetValue(key, out bool value))
            {
                value = true;
                m_foldoutStates[key] = value;
            }

            return value;
        }

        private void SetFoldoutState(string key, bool value)
        {
            if (string.IsNullOrEmpty(key))
            {
                return;
            }

            m_foldoutStates[key] = value;
        }

        private bool MatchesSearch(SerializedProperty ruleProp)
        {
            if (string.IsNullOrEmpty(m_searchValue))
            {
                return true;
            }

            string needle = m_searchValue.Trim();
            if (string.IsNullOrEmpty(needle))
            {
                return true;
            }

            CollectItemNames(ruleProp, s_ItemNames);
            foreach (string itemName in s_ItemNames)
            {
                if (itemName.IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;
            }

            string description = ruleProp.FindPropertyRelative("m_description").stringValue ?? string.Empty;

            return description.IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static void CollectItemNames(SerializedProperty ruleProp, List<string> buffer)
        {
            buffer.Clear();

            SerializedProperty itemsProp = ruleProp.FindPropertyRelative("m_items");
            if (itemsProp == null)
                return;

            for (int i = 0; i < itemsProp.arraySize; i++)
            {
                SerializedProperty element = itemsProp.GetArrayElementAtIndex(i);
                UnityEngine.Object reference = element?.objectReferenceValue;
                if (reference != null)
                    buffer.Add(reference.name);
            }
        }

        private string BuildRuleLabel(int index, SerializedProperty ruleProp)
        {
            string itemName = BuildItemsSummary(ruleProp);
            string description = ruleProp.FindPropertyRelative("m_description").stringValue;
            string suffix = string.IsNullOrEmpty(description) ? string.Empty : $" — {description}";
            return $"Rule {index + 1}: {itemName}{suffix}";
        }

        private string BuildItemsSummary(SerializedProperty ruleProp)
        {
            CollectItemNames(ruleProp, s_ItemNames);
            if (s_ItemNames.Count == 0)
                return "No Items";

            if (s_ItemNames.Count == 1)
                return s_ItemNames[0];

            return $"{s_ItemNames[0]} (+{s_ItemNames.Count - 1} more)";
        }
    }
}
#endif
