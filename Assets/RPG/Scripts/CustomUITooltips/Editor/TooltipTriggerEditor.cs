using CustomUITooltips;
using UnityEditor;
using UnityEngine;

namespace CustomUITooltips.Editor
{
    [CustomEditor(typeof(TooltipTrigger))]
    public sealed class TooltipTriggerEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            TooltipTrigger trigger = (TooltipTrigger)target;
            using (new EditorGUI.DisabledScope(!Application.isPlaying))
            {
                EditorGUILayout.Space(8f);
                if (GUILayout.Button("Preview Tooltip Now"))
                    trigger.ShowNow();

                if (GUILayout.Button("Hide Tooltip"))
                    trigger.HideNow();
            }

            if (!Application.isPlaying)
                EditorGUILayout.HelpBox("Preview buttons are available in Play Mode. In edit mode, fill the Tooltip fields and use the scene setup menu under Tools/Custom Tooltips.", MessageType.Info);
        }
    }
}
