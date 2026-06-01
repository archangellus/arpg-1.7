using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
using UnityEditorInternal;
#endif

namespace PLAYERTWO.ARPGProject
{
    /// <summary>
    /// Draws a string field as a Unity tag dropdown in the inspector.
    /// </summary>
    public sealed class ARPGTagSelectorAttribute : PropertyAttribute
    {
    }

#if UNITY_EDITOR
    [CustomPropertyDrawer(typeof(ARPGTagSelectorAttribute))]
    public sealed class ARPGTagSelectorDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            if (property.propertyType != SerializedPropertyType.String)
            {
                EditorGUI.PropertyField(position, property, label);
                return;
            }

            EditorGUI.BeginProperty(position, label, property);

            var tags = InternalEditorUtility.tags;

            if (tags == null || tags.Length == 0)
            {
                EditorGUI.PropertyField(position, property, label);
                EditorGUI.EndProperty();
                return;
            }

            var currentTag = property.stringValue;
            var currentIndex = System.Array.IndexOf(tags, currentTag);

            if (currentIndex >= 0)
            {
                var options = System.Array.ConvertAll(tags, tag => new GUIContent(tag));
                var selectedIndex = EditorGUI.Popup(position, label, currentIndex, options);
                property.stringValue = tags[selectedIndex];
            }
            else
            {
                var options = new GUIContent[tags.Length + 1];
                options[0] = new GUIContent(string.IsNullOrEmpty(currentTag) ? "<No Tag Selected>" : $"{currentTag} (Missing)");

                for (int i = 0; i < tags.Length; i++)
                    options[i + 1] = new GUIContent(tags[i]);

                var selectedIndex = EditorGUI.Popup(position, label, 0, options);

                if (selectedIndex > 0)
                    property.stringValue = tags[selectedIndex - 1];
            }

            EditorGUI.EndProperty();
        }
    }
#endif
}
