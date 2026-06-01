#if UNITY_EDITOR
using UnityEditor;

namespace PLAYERTWO.ARPGProject
{
    [CustomEditor(typeof(ARPGInteractable))]
    [CanEditMultipleObjects]
    public sealed class ARPGInteractableEditor : Editor
    {
        SerializedProperty mode;
        SerializedProperty interactive;
        SerializedProperty interactOnce;
        SerializedProperty disableOnInteract;
        SerializedProperty linkedInteractive;
        SerializedProperty onInteract;
        SerializedProperty onSelected;
        SerializedProperty onDeselected;
        SerializedProperty holdDuration;
        SerializedProperty holdUseLimit;
        SerializedProperty promptMessage;
        SerializedProperty promptPositionMode;
        SerializedProperty promptAnchor;
        SerializedProperty promptYOffset;
        SerializedProperty useUniversalMessageSettings;
        SerializedProperty messageColor;
        SerializedProperty messageSize;
        SerializedProperty useUniversalImageSettings;
        SerializedProperty promptIcon;
        SerializedProperty promptIconSize;

        void OnEnable()
        {
            mode = serializedObject.FindProperty(nameof(ARPGInteractable.mode));
            interactive = serializedObject.FindProperty(nameof(ARPGInteractable.interactive));
            interactOnce = serializedObject.FindProperty(nameof(ARPGInteractable.interactOnce));
            disableOnInteract = serializedObject.FindProperty(nameof(ARPGInteractable.disableOnInteract));
            linkedInteractive = serializedObject.FindProperty(nameof(ARPGInteractable.linkedInteractive));
            onInteract = serializedObject.FindProperty(nameof(ARPGInteractable.onInteract));
            onSelected = serializedObject.FindProperty(nameof(ARPGInteractable.onSelected));
            onDeselected = serializedObject.FindProperty(nameof(ARPGInteractable.onDeselected));
            holdDuration = serializedObject.FindProperty(nameof(ARPGInteractable.holdDuration));
            holdUseLimit = serializedObject.FindProperty(nameof(ARPGInteractable.holdUseLimit));
            promptMessage = serializedObject.FindProperty(nameof(ARPGInteractable.promptMessage));
            promptPositionMode = serializedObject.FindProperty(nameof(ARPGInteractable.promptPositionMode));
            promptAnchor = serializedObject.FindProperty(nameof(ARPGInteractable.promptAnchor));
            promptYOffset = serializedObject.FindProperty(nameof(ARPGInteractable.promptYOffset));
            useUniversalMessageSettings = serializedObject.FindProperty(nameof(ARPGInteractable.useUniversalMessageSettings));
            messageColor = serializedObject.FindProperty(nameof(ARPGInteractable.messageColor));
            messageSize = serializedObject.FindProperty(nameof(ARPGInteractable.messageSize));
            useUniversalImageSettings = serializedObject.FindProperty(nameof(ARPGInteractable.useUniversalImageSettings));
            promptIcon = serializedObject.FindProperty(nameof(ARPGInteractable.promptIcon));
            promptIconSize = serializedObject.FindProperty(nameof(ARPGInteractable.promptIconSize));
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            DrawInteractionSection();
            DrawSelectionSection();
            DrawHoldSection();
            DrawPromptSection();

            serializedObject.ApplyModifiedProperties();
        }

        void DrawInteractionSection()
        {
            EditorGUILayout.LabelField("Interaction", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(mode);
            EditorGUILayout.PropertyField(interactive);
            EditorGUILayout.PropertyField(interactOnce);
            EditorGUILayout.PropertyField(disableOnInteract);
            EditorGUILayout.PropertyField(linkedInteractive);
            EditorGUILayout.PropertyField(onInteract);
            EditorGUILayout.Space();
        }

        void DrawSelectionSection()
        {
            EditorGUILayout.LabelField("Selection Feedback", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(onSelected);
            EditorGUILayout.PropertyField(onDeselected);
            EditorGUILayout.Space();
        }

        void DrawHoldSection()
        {
            if (mode.enumValueIndex != (int)ARPGInteractable.InteractionMode.Hold)
                return;

            EditorGUILayout.LabelField("Hold", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(holdDuration);
            EditorGUILayout.PropertyField(holdUseLimit);
            EditorGUILayout.Space();
        }

        void DrawPromptSection()
        {
            EditorGUILayout.LabelField("Prompt", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(promptMessage);
            EditorGUILayout.PropertyField(promptPositionMode);
            EditorGUILayout.PropertyField(promptAnchor);
            EditorGUILayout.PropertyField(promptYOffset);

            EditorGUILayout.Space(4f);
            EditorGUILayout.PropertyField(useUniversalMessageSettings);

            if (!useUniversalMessageSettings.boolValue || useUniversalMessageSettings.hasMultipleDifferentValues)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(messageColor);
                EditorGUILayout.PropertyField(messageSize);
                EditorGUI.indentLevel--;
            }

            EditorGUILayout.Space(4f);
            EditorGUILayout.PropertyField(useUniversalImageSettings);

            if (!useUniversalImageSettings.boolValue || useUniversalImageSettings.hasMultipleDifferentValues)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(promptIcon);
                EditorGUILayout.PropertyField(promptIconSize);
                EditorGUI.indentLevel--;
            }
        }
    }
}
#endif
