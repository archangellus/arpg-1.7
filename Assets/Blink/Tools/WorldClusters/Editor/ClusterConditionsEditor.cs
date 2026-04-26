using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace BLINK.WorldClusters
{
    [CustomEditor(typeof(ClusterConditions))]
    public class ClusterConditionsEditor : Editor
    {
        private ClusterConditions _ref;
        private GUISkin _skin;
        private WorldClustersEditorData _editorData;
        private ReorderableList _conditionsList;


        private void OnEnable()
        {
            _ref = (ClusterConditions) target;
            _skin = Resources.Load<GUISkin>("EditorData/WorldClustersEditorSkin");
            _editorData = Resources.Load<WorldClustersEditorData>("EditorData/WorldClustersEditorData");
            BuildConditionsList();
        }

        private void BuildConditionsList()
        {
            _conditionsList = new ReorderableList(_ref.collisionConditions, typeof(CollisionCondition), true, true, true, true);
            _conditionsList.drawHeaderCallback = rect => EditorGUI.LabelField(rect, "Conditions");
            _conditionsList.drawElementCallback = (rect, index, isActive, isFocused) =>
            {
                if (index < 0 || index >= _ref.collisionConditions.Count) return;
                var condition = _ref.collisionConditions[index];
                float y = rect.y + 2;
                float line = EditorGUIUtility.singleLineHeight;

                var typeRect = new Rect(rect.x, y, rect.width - 56, line);
                condition.type = (ClUSTER_COLLISION_CONDITION_TYPE)EditorGUI.EnumPopup(typeRect, condition.type);

                var duplicateRect = new Rect(rect.x + rect.width - 52, y, 24, line);
                if (GUI.Button(duplicateRect, GUIContent.none, _skin.GetStyle(_editorData.duplicateButtonStyle)))
                {
                    Undo.RecordObject(_ref, "Duplicate Condition");
                    _ref.collisionConditions.Insert(index + 1, CloneCondition(condition));
                    EditorUtility.SetDirty(_ref);
                }

                var deleteRect = new Rect(rect.x + rect.width - 24, y, 24, line);
                if (GUI.Button(deleteRect, "X"))
                {
                    Undo.RecordObject(_ref, "Remove Condition");
                    _ref.collisionConditions.RemoveAt(index);
                    EditorUtility.SetDirty(_ref);
                    GUIUtility.ExitGUI();
                }

                y += line + 4;
                switch (condition.type)
                {
                    case ClUSTER_COLLISION_CONDITION_TYPE.GameObjectName:
                        condition.gameObjectName = EditorGUI.TextField(new Rect(rect.x, y, rect.width, line), condition.gameObjectName);
                        break;
                    case ClUSTER_COLLISION_CONDITION_TYPE.LayerMask:
                        condition.layer = EditorGUI.LayerField(new Rect(rect.x, y, rect.width, line), condition.layer);
                        break;
                    case ClUSTER_COLLISION_CONDITION_TYPE.Tag:
                        condition.tagName = EditorGUI.TagField(new Rect(rect.x, y, rect.width, line), condition.tagName);
                        break;
                }

                y += line + 4;
                var ruleLabelRect = new Rect(rect.x, y, 50, line);
                EditorGUI.LabelField(ruleLabelRect, "RULE:");
                var ruleRect = new Rect(rect.x + 54, y, rect.width - 54, line);
                condition.requirementType = (ClUSTER_CONDITION_REQUIREMENT_TYPE)EditorGUI.EnumPopup(ruleRect, condition.requirementType);
            };
            _conditionsList.elementHeightCallback = _ => (EditorGUIUtility.singleLineHeight * 3) + 14;
            _conditionsList.onAddCallback = _ =>
            {
                Undo.RecordObject(_ref, "Add Condition");
                _ref.collisionConditions.Add(new CollisionCondition());
                EditorUtility.SetDirty(_ref);
            };
            _conditionsList.onRemoveCallback = list =>
            {
                if (list.index < 0 || list.index >= _ref.collisionConditions.Count) return;
                Undo.RecordObject(_ref, "Remove Condition");
                _ref.collisionConditions.RemoveAt(list.index);
                EditorUtility.SetDirty(_ref);
            };
            _conditionsList.onReorderCallback = _ =>
            {
                Undo.RecordObject(_ref, "Reorder Conditions");
                EditorUtility.SetDirty(_ref);
            };
        }

        private static CollisionCondition CloneCondition(CollisionCondition source)
        {
            return new CollisionCondition
            {
                type = source.type,
                requirementType = source.requirementType,
                gameObjectName = source.gameObjectName,
                tagName = source.tagName,
                layer = source.layer
            };
        }

        public override void OnInspectorGUI()
        {
            if (_skin == null) return;
            EditorGUI.BeginChangeCheck();

            if (_conditionsList == null) BuildConditionsList();
            _conditionsList.DoLayoutList();

            if (!EditorGUI.EndChangeCheck()) return;
            EditorUtility.SetDirty(_ref);
            serializedObject.ApplyModifiedProperties();
        }
    }
}
