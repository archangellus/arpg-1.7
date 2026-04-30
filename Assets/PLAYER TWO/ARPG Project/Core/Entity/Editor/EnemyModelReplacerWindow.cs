using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace PLAYERTWO.ARPGProject
{
    public class EnemyModelReplacerWindow : EditorWindow
    {
        private GameObject m_enemyRoot;
        private GameObject m_oldModelRoot;
        private GameObject m_newModelPrefab;
        private string m_newModelName = "Model";

        [MenuItem("PLAYER TWO/ARPG Project/Tools/Enemy Model Replacer")]
        public static void OpenWindow()
        {
            var window = GetWindow<EnemyModelReplacerWindow>("Enemy Model Replacer");
            window.minSize = new Vector2(460, 260);
        }

        private void OnGUI()
        {
            EditorGUILayout.HelpBox(
                "Drag your enemy prefab/instance and the old/new model roots. The tool replaces the model and remaps Transform/GameObject references used by existing enemy components.",
                MessageType.Info
            );

            m_enemyRoot = (GameObject)EditorGUILayout.ObjectField(
                "Enemy Root",
                m_enemyRoot,
                typeof(GameObject),
                true
            );
            m_oldModelRoot = (GameObject)EditorGUILayout.ObjectField(
                "Old Model Root",
                m_oldModelRoot,
                typeof(GameObject),
                true
            );
            m_newModelPrefab = (GameObject)EditorGUILayout.ObjectField(
                "New Model Prefab",
                m_newModelPrefab,
                typeof(GameObject),
                false
            );
            m_newModelName = EditorGUILayout.TextField("New Model Name", m_newModelName);

            using (new EditorGUI.DisabledScope(!CanReplace()))
            {
                if (GUILayout.Button("Replace Enemy Model", GUILayout.Height(32)))
                {
                    ReplaceModel();
                }
            }
        }

        private bool CanReplace() => m_enemyRoot && m_oldModelRoot && m_newModelPrefab;

        private void ReplaceModel()
        {
            if (!m_oldModelRoot.transform.IsChildOf(m_enemyRoot.transform))
            {
                EditorUtility.DisplayDialog(
                    "Invalid old model",
                    "Old Model Root must be a child of Enemy Root.",
                    "Ok"
                );
                return;
            }

            Undo.IncrementCurrentGroup();
            Undo.SetCurrentGroupName("Replace Enemy Model");

            var oldTransforms = BuildTransformMap(m_oldModelRoot.transform);
            var oldAnimator = m_oldModelRoot.GetComponentInChildren<Animator>(true);

            var newModelInstance = (GameObject)PrefabUtility.InstantiatePrefab(m_newModelPrefab);
            if (!newModelInstance)
            {
                newModelInstance = Instantiate(m_newModelPrefab);
                newModelInstance.name = m_newModelPrefab.name;
            }

            Undo.RegisterCreatedObjectUndo(newModelInstance, "Create New Enemy Model");

            newModelInstance.transform.SetParent(m_enemyRoot.transform, false);
            newModelInstance.name = string.IsNullOrWhiteSpace(m_newModelName)
                ? m_newModelPrefab.name
                : m_newModelName;

            newModelInstance.transform.SetSiblingIndex(m_oldModelRoot.transform.GetSiblingIndex());
            newModelInstance.transform.localPosition = m_oldModelRoot.transform.localPosition;
            newModelInstance.transform.localRotation = m_oldModelRoot.transform.localRotation;
            newModelInstance.transform.localScale = m_oldModelRoot.transform.localScale;

            var newTransforms = BuildTransformMap(newModelInstance.transform);
            RemapSerializedReferences(m_enemyRoot, oldTransforms, newTransforms);
            CopyAnimatorRuntimeData(oldAnimator, newModelInstance.GetComponentInChildren<Animator>(true));

            Undo.DestroyObjectImmediate(m_oldModelRoot);

            EditorUtility.SetDirty(m_enemyRoot);
            PrefabUtility.RecordPrefabInstancePropertyModifications(m_enemyRoot);
            Undo.CollapseUndoOperations(Undo.GetCurrentGroup());

            Selection.activeObject = m_enemyRoot;
            EditorUtility.DisplayDialog(
                "Enemy model replaced",
                "Model replaced and component references were remapped. Verify humanoid mapping/animation clips if your rig differs from the original.",
                "Done"
            );
        }

        private static Dictionary<string, Transform> BuildTransformMap(Transform root)
        {
            var map = new Dictionary<string, Transform> { [string.Empty] = root };

            foreach (var transform in root.GetComponentsInChildren<Transform>(true))
            {
                var path = AnimationUtility.CalculateTransformPath(transform, root);
                map[path] = transform;
            }

            return map;
        }

        private static void RemapSerializedReferences(
            GameObject enemyRoot,
            Dictionary<string, Transform> oldTransforms,
            Dictionary<string, Transform> newTransforms
        )
        {
            foreach (var component in enemyRoot.GetComponentsInChildren<Component>(true))
            {
                if (!component)
                    continue;

                var so = new SerializedObject(component);
                var property = so.GetIterator();
                var enterChildren = true;
                var modified = false;

                while (property.NextVisible(enterChildren))
                {
                    enterChildren = false;

                    if (property.propertyType != SerializedPropertyType.ObjectReference)
                        continue;

                    var current = property.objectReferenceValue;
                    if (!current)
                        continue;

                    if (current is Transform transformRef)
                    {
                        if (TryRemapTransform(oldTransforms, newTransforms, transformRef, out var mapped))
                        {
                            property.objectReferenceValue = mapped;
                            modified = true;
                        }
                    }
                    else if (current is GameObject gameObjectRef)
                    {
                        var transform = gameObjectRef.transform;
                        if (TryRemapTransform(oldTransforms, newTransforms, transform, out var mapped))
                        {
                            property.objectReferenceValue = mapped.gameObject;
                            modified = true;
                        }
                    }
                }

                if (modified)
                    so.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        private static bool TryRemapTransform(
            Dictionary<string, Transform> oldTransforms,
            Dictionary<string, Transform> newTransforms,
            Transform oldReference,
            out Transform mapped
        )
        {
            mapped = null;

            foreach (var pair in oldTransforms)
            {
                if (pair.Value != oldReference)
                    continue;

                return newTransforms.TryGetValue(pair.Key, out mapped);
            }

            return false;
        }

        private static void CopyAnimatorRuntimeData(Animator oldAnimator, Animator newAnimator)
        {
            if (!oldAnimator || !newAnimator)
                return;

            newAnimator.runtimeAnimatorController = oldAnimator.runtimeAnimatorController;
            newAnimator.applyRootMotion = oldAnimator.applyRootMotion;
            newAnimator.updateMode = oldAnimator.updateMode;
            newAnimator.cullingMode = oldAnimator.cullingMode;
        }
    }
}
