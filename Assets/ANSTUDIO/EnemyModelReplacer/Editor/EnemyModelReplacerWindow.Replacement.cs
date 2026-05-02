using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace PLAYERTWO.ARPGProject
{
    public partial class EnemyModelReplacerWindow
    {
        private void ReplaceModel()
        {
            Undo.IncrementCurrentGroup();
            var group = Undo.GetCurrentGroup();

            var rootTransform = enemyRoot.transform;
            var modelContainer = rootTransform.Find(modelContainerName) ?? rootTransform;
            var previousModel = modelContainer.childCount > 0 ? modelContainer.GetChild(0).gameObject : null;
            var previousModelLocalPosition = previousModel ? previousModel.transform.localPosition : Vector3.zero;

            var instance = InstantiatePrefabInScene(newModelPrefab, enemyRoot.scene);

            Undo.RegisterCreatedObjectUndo(instance, "Create New Enemy Model");
            instance.name = newModelPrefab.name;
            instance.transform.SetParent(modelContainer, false);

            var newLocalPosition = previousModel ? previousModelLocalPosition : instance.transform.localPosition;

            if (forceNewModelY)
                newLocalPosition.y = forcedNewModelLocalY;

            instance.transform.localPosition = newLocalPosition;
            instance.transform.localRotation = Quaternion.identity;
            instance.transform.localScale = Vector3.one;

            if (previousModel)
            {
                CopyModelComponents(previousModel.transform, instance.transform);
                CopyAnimatorControllerKeepingAvatar(previousModel, instance);
            }

            if (autoApplyToChildren)
                RebindModelReferences(enemyRoot, instance.transform, previousModel ? previousModel.transform : null, removePreviousModel);

            if (removePreviousModel && previousModel)
                Undo.DestroyObjectImmediate(previousModel);

            var createdAndAssignedOverrideController = false;
            if (createAnimatorOverrideController)
                createdAndAssignedOverrideController = TryCreateAndAssignAnimatorOverrideController(enemyRoot, instance.transform);

            EditorUtility.SetDirty(enemyRoot);
            if (PrefabUtility.IsPartOfPrefabInstance(enemyRoot))
                PrefabUtility.RecordPrefabInstancePropertyModifications(enemyRoot);

            var createdDuplicates = 0;
            if (createStandaloneDuplicates && duplicateCount > 0)
                createdDuplicates = CreateStandaloneEnemyDuplicates(rootTransform);

            EditorSceneManager.MarkSceneDirty(enemyRoot.scene);
            Undo.CollapseUndoOperations(group);

            Debug.Log($"[EnemyModelReplacer] Model replaced and references rebound for '{enemyRoot.name}'. Animator Override Controller created and assigned: {createdAndAssignedOverrideController}. Standalone converted enemy duplicates created: {createdDuplicates}.", enemyRoot);
        }

    }
}
