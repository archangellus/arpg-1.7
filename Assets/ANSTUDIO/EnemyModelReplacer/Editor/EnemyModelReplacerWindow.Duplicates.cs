using System.Collections.Generic;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace PLAYERTWO.ARPGProject
{
    public partial class EnemyModelReplacerWindow
    {
        private void DrawRangeGizmosInScene(SceneView sceneView)
        {
            if (enemyRoot == null || !createStandaloneDuplicates)
                return;

            var center = enemyRoot.transform.position + Vector3.up * duplicateRangeGizmoYOffset;
            var minRadius = Mathf.Max(0f, duplicateMinRadius);
            var maxRadius = Mathf.Max(minRadius, duplicateMaxRadius);
            var previousColor = Handles.color;
            var previousZTest = Handles.zTest;

            try
            {
                Handles.zTest = UnityEngine.Rendering.CompareFunction.LessEqual;

                Handles.color = new Color(0.1f, 0.65f, 1f, 0.18f);
                Handles.DrawSolidDisc(center, Vector3.up, maxRadius);

                if (minRadius > 0f)
                {
                    Handles.color = new Color(1f, 0.75f, 0.15f, 0.18f);
                    Handles.DrawSolidDisc(center, Vector3.up, minRadius);
                }

                Handles.color = new Color(0.1f, 0.65f, 1f, 0.95f);
                Handles.DrawWireDisc(center, Vector3.up, maxRadius);

                if (minRadius > 0f)
                {
                    Handles.color = new Color(1f, 0.75f, 0.15f, 0.95f);
                    Handles.DrawWireDisc(center, Vector3.up, minRadius);
                }

                Handles.color = Color.white;

                var labelOffset = Vector3.forward * Mathf.Max(1f, maxRadius) + Vector3.up * 0.5f;
                var layerAvoidanceText = avoidSpawnCollisionLayers
                    ? $"\nAvoid Layers: On | Check Radius: {Mathf.Max(0.01f, spawnCollisionCheckRadius):0.##}"
                    : "\nAvoid Layers: Off";

                Handles.Label(
                    center + labelOffset,
                    $"Duplicate Range\nMin: {minRadius:0.##} | Max: {maxRadius:0.##}\nSpacing: {Mathf.Max(0f, duplicateMinSpacing):0.##}\nGizmo Y Offset: {duplicateRangeGizmoYOffset:0.##}{layerAvoidanceText}",
                    EditorStyles.boldLabel);
            }
            finally
            {
                Handles.color = previousColor;
                Handles.zTest = previousZTest;
            }
        }

        private int CreateStandaloneEnemyDuplicates(Transform sourceRoot)
        {
            var created = 0;
            var acceptedPositions = new List<Vector3>();
            var center = sourceRoot.position;

            for (var i = 0; i < duplicateCount; i++)
            {
                if (!TryGetRandomDuplicatePosition(center, acceptedPositions, out var duplicatePosition))
                {
                    Debug.LogWarning($"[EnemyModelReplacer] Could only place {created} of {duplicateCount} converted enemy duplicates. Increase the range, lower spacing, raise placement attempts, lower the collision radius, or adjust the blocked spawn layers.", enemyRoot);
                    break;
                }

                var duplicate = InstantiateConvertedEnemyCopy(enemyRoot, enemyRoot.scene);
                Undo.RegisterCreatedObjectUndo(duplicate, "Create Duplicate Converted Enemy");

                duplicate.name = BuildDuplicateName(i);
                duplicate.transform.SetParent(null, true);
                duplicate.transform.position = duplicatePosition;
                duplicate.transform.rotation = sourceRoot.rotation;
                duplicate.transform.localScale = sourceRoot.lossyScale;

                EditorUtility.SetDirty(duplicate);
                if (PrefabUtility.IsPartOfPrefabInstance(duplicate))
                    PrefabUtility.RecordPrefabInstancePropertyModifications(duplicate);

                acceptedPositions.Add(duplicatePosition);
                created++;
            }

            return created;
        }

        private bool TryGetRandomDuplicatePosition(Vector3 center, List<Vector3> acceptedPositions, out Vector3 position)
        {
            for (var attempt = 0; attempt < duplicatePlacementAttempts; attempt++)
            {
                var distance = UnityEngine.Random.Range(duplicateMinRadius, duplicateMaxRadius);
                var angle = UnityEngine.Random.Range(0f, Mathf.PI * 2f);
                var candidate = new Vector3(
                    center.x + Mathf.Cos(angle) * distance,
                    center.y,
                    center.z + Mathf.Sin(angle) * distance);

                if (HasEnoughSpace(candidate, acceptedPositions) && !IsBlockedBySpawnLayers(candidate))
                {
                    position = candidate;
                    return true;
                }
            }

            position = center;
            return false;
        }

        private bool HasEnoughSpace(Vector3 candidate, List<Vector3> acceptedPositions)
        {
            foreach (var accepted in acceptedPositions)
            {
                var candidateXZ = new Vector2(candidate.x, candidate.z);
                var acceptedXZ = new Vector2(accepted.x, accepted.z);

                if (Vector2.Distance(candidateXZ, acceptedXZ) < duplicateMinSpacing)
                    return false;
            }

            return true;
        }

        private bool IsBlockedBySpawnLayers(Vector3 candidate)
        {
            if (!avoidSpawnCollisionLayers || blockedSpawnLayers.value == 0)
                return false;

            var checkCenter = candidate + Vector3.up * spawnCollisionCheckYOffset;
            return Physics.CheckSphere(
                checkCenter,
                Mathf.Max(0.01f, spawnCollisionCheckRadius),
                blockedSpawnLayers,
                spawnCollisionTriggerInteraction);
        }

        private static LayerMask LayerMaskField(string label, LayerMask selected)
        {
            var layers = InternalEditorUtility.layers;
            var compactMask = 0;

            for (var i = 0; i < layers.Length; i++)
            {
                var layer = LayerMask.NameToLayer(layers[i]);
                if (layer >= 0 && (selected.value & (1 << layer)) != 0)
                    compactMask |= 1 << i;
            }

            compactMask = EditorGUILayout.MaskField(label, compactMask, layers);

            var layerMask = 0;
            for (var i = 0; i < layers.Length; i++)
            {
                if ((compactMask & (1 << i)) == 0)
                    continue;

                var layer = LayerMask.NameToLayer(layers[i]);
                if (layer >= 0)
                    layerMask |= 1 << layer;
            }

            selected.value = layerMask;
            return selected;
        }

    }
}
