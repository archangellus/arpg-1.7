using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace PLAYERTWO.ARPGProject.ArcDrop
{
    /// <summary>
    /// Handles item drop requests with an arcing trajectory via the plugin EventBus.
    /// </summary>
    public class ArcDropRuntime : MonoBehaviour
    {
        [Header("Item Drop Settings")]
        [Tooltip("The Layer Mask of the ground to drop items. Falls back to the Level drop layer when unset.")]
        public LayerMask dropGroundLayer;

        [Tooltip("The prefab instantiated when dropping an Item on the ground. Falls back to Game.collectibleItemPrefab when unset.")]
        public CollectibleItem droppedItemPrefab;

        [Tooltip("Speed at which dropped items move towards the ground impact point.")]
        public float dropSpeed = 2f;

        [Tooltip("Height multiplier for the drop arc.")]
        public float arcHeight = 2f;

        [Tooltip("Curve defining the shape of the arc over normalized time (0-1).")]
        public AnimationCurve arcCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 0f);

        [Header("Rotation Settings")]
        [Tooltip("Enable rotation on the X axis when dropping items.")]
        public bool rotateOnX = true;

        [Tooltip("Enable rotation on the Y axis when dropping items.")]
        public bool rotateOnY = true;

        [Tooltip("Enable rotation on the Z axis when dropping items.")]
        public bool rotateOnZ = true;

        [Tooltip("Rotation speed in degrees per second for dropped items.")]
        public float rotationSpeed = 250f;

        [Tooltip("The maximum range around the player to drop items.")]
        public float dropRange = 2f;

        private Action<object> m_dropHandler;

        private void ApplyAutomaticDefaults()
        {
            AssignTerrainLayer();
            AssignCollectiblePrefab();
            ApplyDefaultSpeeds();
        }

        private void AssignTerrainLayer()
        {
            if (dropGroundLayer.value != 0)
                return;

            var terrain = Terrain.activeTerrain;
            if (terrain)
                dropGroundLayer = 1 << terrain.gameObject.layer;
        }

        private void AssignCollectiblePrefab()
        {
            if (droppedItemPrefab)
                return;

            foreach (var candidate in Resources.FindObjectsOfTypeAll<CollectibleItem>())
            {
                if (candidate && candidate.name == "Collectible Item")
                {
                    droppedItemPrefab = candidate;
                    break;
                }
            }
        }

        private void ApplyDefaultSpeeds()
        {
            if (dropSpeed <= 0f)
                dropSpeed = 2f;

            if (rotationSpeed <= 0f)
                rotationSpeed = 250f;
        }

        private void EnsureCurve()
        {
            if (arcCurve == null || arcCurve.length <= 1)
            {
                arcCurve = new AnimationCurve(
                    new Keyframe(0f, 0f),
                    new Keyframe(0.25f, 0.5f),
                    new Keyframe(0.5f, 1f),
                    new Keyframe(0.75f, 0.5f),
                    new Keyframe(1f, 0f)
                );
            }
        }

        private void Awake()
        {
            ApplyAutomaticDefaults();
            EnsureCurve();
            dropSpeed = Mathf.Max(0.01f, dropSpeed);
            rotationSpeed = Mathf.Max(0f, rotationSpeed);
            m_dropHandler = HandleArcDropRequested;
            EventBus.Subscribe(EventBus.ArcDropRequested, m_dropHandler);
        }

        private void OnDestroy()
        {
            if (m_dropHandler != null)
                EventBus.Unsubscribe(EventBus.ArcDropRequested, m_dropHandler);
        }

        private void HandleArcDropRequested(object payload)
        {
            if (payload is not object[] args || args.Length < 6)
                return;

            if (args[0] is not GUI gui || !gui)
                return;

            if (args[1] is not GUIItem guiItem || !guiItem)
                return;

            if (args[2] is not Entity entity || !entity)
                return;

            var onDropCompleted = args[3] as Action;
            var onDropFailed = args[4] as Action;
            var markHandled = args[5] as Action<bool>;
            var isHandled = args.Length > 6 ? args[6] as Func<bool> : null;

            if (onDropCompleted == null || markHandled == null)
                return;

            if (isHandled?.Invoke() == true)
                return;

            var layerToUse = dropGroundLayer.value != 0
                ? dropGroundLayer
                : (Level.instance ? Level.instance.dropGroundLayer : Physics.DefaultRaycastLayers);

            if (!entity.inputs.MouseRaycast(out var hit, layerToUse))
            {
#if UNITY_ANDROID || UNITY_IOS
                onDropFailed?.Invoke();
#endif
                return;
            }

            Vector3 direction = (hit.point - entity.transform.position).normalized;
            float distance = Mathf.Min(Vector3.Distance(entity.transform.position, hit.point), dropRange);
            Vector3 targetPosition = entity.transform.position + direction * distance;

            var prefab = droppedItemPrefab ? droppedItemPrefab : Game.instance.collectibleItemPrefab;
            var collectible = Instantiate(prefab, entity.transform.position, Quaternion.identity);
            collectible.SetItem(guiItem.item);
            RegisterDroppedItem(collectible);

            markHandled?.Invoke(true);
            onDropCompleted?.Invoke();

            Vector3 axis = new Vector3(
                rotateOnX ? UnityEngine.Random.Range(-1f, 1f) : 0f,
                rotateOnY ? UnityEngine.Random.Range(-1f, 1f) : 0f,
                rotateOnZ ? UnityEngine.Random.Range(-1f, 1f) : 0f
            );

            if (axis == Vector3.zero)
                axis = Vector3.up;

            axis.Normalize();

            StartCoroutine(AnimateDropItem(collectible.transform, targetPosition, axis, rotationSpeed));
        }

        private void RegisterDroppedItem(CollectibleItem collectible)
        {
            if (!Level.instance || collectible == null)
                return;

            if (Level.instance.droppedItems == null)
                Level.instance.droppedItems = new List<CollectibleItem>();

            collectible.onCollect.AddListener(() => Level.instance.droppedItems.Remove(collectible));
            Level.instance.droppedItems.Add(collectible);
        }

        private IEnumerator AnimateDropItem(
            Transform itemTransform,
            Vector3 targetPosition,
            Vector3 rotateAxis,
            float rotateSpeed
        )
        {
            Vector3 startPosition = itemTransform.position;
            float startY = startPosition.y;
            float endY = targetPosition.y;
            float distance = Vector3.Distance(startPosition, targetPosition);
            float animationDuration = distance / Mathf.Max(0.01f, dropSpeed);
            float elapsedTime = 0f;

            while (elapsedTime < animationDuration)
            {
                elapsedTime += Time.deltaTime;
                float t = Mathf.Clamp01(elapsedTime / animationDuration);

                Vector3 flatPos = Vector3.Lerp(
                    new Vector3(startPosition.x, 0f, startPosition.z),
                    new Vector3(targetPosition.x, 0f, targetPosition.z),
                    t
                );

                float heightOffset = arcCurve.Evaluate(t) * arcHeight;
                float currentY = Mathf.Lerp(startY, endY, t) + heightOffset;

                itemTransform.position = new Vector3(flatPos.x, currentY, flatPos.z);
                itemTransform.Rotate(rotateAxis, rotateSpeed * Time.deltaTime, Space.World);

                yield return null;
            }

            itemTransform.position = targetPosition;
        }
    }
}
