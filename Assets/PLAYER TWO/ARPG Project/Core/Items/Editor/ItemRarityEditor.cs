using System.Collections.Generic;
using UnityEditor;

namespace PLAYERTWO.ARPGProject
{
    [CustomEditor(typeof(ItemRarity))]
    public class ItemRarityEditor : Editor
    {
        static readonly HashSet<string> k_affixDependentFields =
            new()
            {
                "tier",
                "affixesMode",
                "bothAffixChance",
                "minAffixes",
                "maxAffixes",
                "valueWeight",
            };

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            var affixesModeProp = serializedObject.FindProperty("affixesMode");
            var affixesProp = serializedObject.FindProperty("affixes");
            var mode = (ItemRarity.AffixesMode)affixesModeProp.enumValueIndex;
            bool hasAffixes = affixesProp.objectReferenceValue != null;

            var iterator = serializedObject.GetIterator();
            bool enterChildren = true;

            while (iterator.NextVisible(enterChildren))
            {
                enterChildren = false;

                if (iterator.propertyPath == "m_Script")
                {
                    using (new EditorGUI.DisabledScope(true))
                        EditorGUILayout.PropertyField(iterator);
                    continue;
                }

                if (!hasAffixes && k_affixDependentFields.Contains(iterator.name))
                    continue;

                if (iterator.name == "bothAffixChance" && mode != ItemRarity.AffixesMode.Paired)
                    continue;

                if (
                    (iterator.name == "minAffixes" || iterator.name == "maxAffixes")
                    && mode == ItemRarity.AffixesMode.Paired
                )
                    continue;

                EditorGUILayout.PropertyField(iterator, true);
            }

            serializedObject.ApplyModifiedProperties();
        }
    }
}
