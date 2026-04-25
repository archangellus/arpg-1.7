using UnityEditor;
using UnityEngine;

namespace PLAYERTWO.ARPGProject
{
    [CustomPropertyDrawer(typeof(ItemAffixes.AffixAttributeEntry))]
    public class ItemAffixAttributeEntryDrawer : PropertyDrawer
    {
        const float k_spacing = 4f;
        const float k_minMaxWidth = 50f;
        const float k_labelWidth = 30f;

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            return EditorGUIUtility.singleLineHeight;
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);

            var typeProp = property.FindPropertyRelative("type");
            var minProp = property.FindPropertyRelative("minValue");
            var maxProp = property.FindPropertyRelative("maxValue");

            float totalMinMaxWidth = k_labelWidth + k_minMaxWidth;
            float typeWidth = position.width - (totalMinMaxWidth + k_spacing) * 2;

            var typeRect = new Rect(position.x, position.y, typeWidth, position.height);
            var minLabelRect = new Rect(
                position.x + typeWidth + k_spacing,
                position.y,
                k_labelWidth,
                position.height
            );
            var minRect = new Rect(minLabelRect.xMax, position.y, k_minMaxWidth, position.height);
            var maxLabelRect = new Rect(
                minRect.xMax + k_spacing,
                position.y,
                k_labelWidth,
                position.height
            );
            var maxRect = new Rect(maxLabelRect.xMax, position.y, k_minMaxWidth, position.height);

            EditorGUI.PropertyField(typeRect, typeProp, GUIContent.none);

            var currentType = (ItemAttributes.AttributeType)typeProp.enumValueIndex;
            bool isPercentage = IsPercentageType(currentType);

            EditorGUI.LabelField(minLabelRect, "Min");
            EditorGUI.LabelField(maxLabelRect, "Max");

            if (isPercentage)
            {
                minProp.intValue = Mathf.Clamp(
                    EditorGUI.IntField(minRect, minProp.intValue),
                    0,
                    100
                );
                maxProp.intValue = Mathf.Clamp(
                    EditorGUI.IntField(maxRect, maxProp.intValue),
                    0,
                    100
                );
            }
            else
            {
                EditorGUI.PropertyField(minRect, minProp, GUIContent.none);
                EditorGUI.PropertyField(maxRect, maxProp, GUIContent.none);
            }

            EditorGUI.EndProperty();
        }

        static bool IsPercentageType(ItemAttributes.AttributeType type)
        {
            return type.ToString().EndsWith("Percent");
        }
    }
}
