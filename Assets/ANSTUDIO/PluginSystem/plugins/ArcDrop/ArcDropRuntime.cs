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
        public AnimationCurve arcCurve;

        [Tooltip("The maximum range around the player to drop items.")]
        public float dropRange = 2f;

        [Header("Target Safety")]
        [Tooltip("Minimum horizontal distance used when the raycast hits the player/entity itself. Prevents instant self-pickup drops.")]
        public float selfTargetMinimumDistance = 0.75f;

        [Tooltip("Temporarily disables the dropped item's colliders while the arc animation is running. This prevents instant pickup/destruction while the coroutine still owns the Transform.")]
        public bool disablePickupCollidersDuringDrop = true;

        [Header("Rotation Settings")]
        [Tooltip("Enable rotation on the X axis when dropping items.")]
        public bool rotateOnX = true;

        [Tooltip("Enable rotation on the Y axis when dropping items.")]
        public bool rotateOnY = true;

        [Tooltip("Enable rotation on the Z axis when dropping items.")]
        public bool rotateOnZ = true;

        [Tooltip("Rotation speed in degrees per second for dropped items.")]
        public float rotationSpeed = 250f;

        [Header("Timing")]
        [Tooltip("Use unscaled time so drop motion stays identical even if Time.timeScale changes.")]
        public bool useUnscaledTime = false;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        [Header("Debug")]
        public bool logRuntimeValues = false;
#endif

        private struct ColliderState
        {
            public Collider collider;
            public bool enabled;
        }

        private Action<object> m_dropHandler;

        private AnimationCurve m_runtimeArcCurve;
        private float m_runtimeDropSpeed;
        private float m_runtimeArcHeight;
        private float m_runtimeRotationSpeed;
        private float m_runtimeDropRange;
        private float m_runtimeSelfTargetMinimumDistance;

        private static AnimationCurve CreateDefaultArcCurve()
        {
            var curve = new AnimationCurve(
                new Keyframe(0f, 0f),
                new Keyframe(0.25f, 0.5f),
                new Keyframe(0.5f, 1f),
                new Keyframe(0.75f, 0.5f),
                new Keyframe(1f, 0f)
            );

            curve.preWrapMode = WrapMode.ClampForever;
            curve.postWrapMode = WrapMode.ClampForever;
            return curve;
        }

        private void Reset()
        {
            if (arcCurve == null || arcCurve.length == 0)
                arcCurve = CreateDefaultArcCurve();

            ApplyAutomaticDefaults();
            ValidateSerializedValues();
        }

        private void OnValidate()
        {
            if (arcCurve == null || arcCurve.length == 0)
                arcCurve = CreateDefaultArcCurve();

            ValidateSerializedValues();
        }

        private void ValidateSerializedValues()
        {
            dropSpeed = Mathf.Max(0.01f, dropSpeed);
            arcHeight = Mathf.Max(0f, arcHeight);
            rotationSpeed = Mathf.Max(0f, rotationSpeed);
            dropRange = Mathf.Max(0f, dropRange);
            selfTargetMinimumDistance = Mathf.Max(0f, selfTargetMinimumDistance);
        }

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

            if (Game.instance)
                droppedItemPrefab = Game.instance.collectibleItemPrefab;
        }

        private void ApplyDefaultSpeeds()
        {
            if (dropSpeed <= 0f)
                dropSpeed = 2f;

            if (rotationSpeed <= 0f)
                rotationSpeed = 250f;
        }

        private void CacheRuntimeValues()
        {
            if (arcCurve == null || arcCurve.length == 0)
                arcCurve = CreateDefaultArcCurve();

            m_runtimeArcCurve = new AnimationCurve(arcCurve.keys)
            {
                preWrapMode = arcCurve.preWrapMode,
                postWrapMode = arcCurve.postWrapMode
            };

            m_runtimeDropSpeed = Mathf.Max(0.01f, dropSpeed);
            m_runtimeArcHeight = Mathf.Max(0f, arcHeight);
            m_runtimeRotationSpeed = Mathf.Max(0f, rotationSpeed);
            m_runtimeDropRange = Mathf.Max(0f, dropRange);
            m_runtimeSelfTargetMinimumDistance = Mathf.Max(0f, selfTargetMinimumDistance);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (logRuntimeValues)
            {
                Debug.Log(
                    $"[ArcDropRuntime] Cached Values | " +
                    $"dropSpeed={m_runtimeDropSpeed}, " +
                    $"arcHeight={m_runtimeArcHeight}, " +
                    $"rotationSpeed={m_runtimeRotationSpeed}, " +
                    $"dropRange={m_runtimeDropRange}, " +
                    $"selfTargetMinimumDistance={m_runtimeSelfTargetMinimumDistance}, " +
                    $"curveKeys={(m_runtimeArcCurve != null ? m_runtimeArcCurve.length : 0)}, " +
                    $"dropGroundLayer={dropGroundLayer.value}, " +
                    $"prefab={(droppedItemPrefab ? droppedItemPrefab.name : "NULL")}",
                    this
                );
            }
#endif
        }

        private void Awake()
        {
            ApplyAutomaticDefaults();
            ValidateSerializedValues();
            CacheRuntimeValues();

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

            Transform entityTransform = entity.transform;
            if (!entityTransform)
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

            Vector3 startPosition = entityTransform.position;
            Vector3 targetPosition = ResolveTargetPosition(entityTransform, hit);

            var prefab = droppedItemPrefab ? droppedItemPrefab :
                (Game.instance ? Game.instance.collectibleItemPrefab : null);

            if (!prefab)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.LogWarning("[ArcDropRuntime] No dropped item prefab available.", this);
#endif
                onDropFailed?.Invoke();
                return;
            }

            var collectible = Instantiate(prefab, startPosition, Quaternion.identity);
            if (!collectible)
            {
                onDropFailed?.Invoke();
                return;
            }

            ColliderState[] colliderStates = disablePickupCollidersDuringDrop
                ? SetCollidersEnabled(collectible, false)
                : Array.Empty<ColliderState>();

            collectible.SetItem(guiItem.item);
            RegisterDroppedItem(collectible);

            markHandled?.Invoke(true);
            onDropCompleted?.Invoke();

            if (!collectible)
                return;

            Vector3 axis = new Vector3(
                rotateOnX ? UnityEngine.Random.Range(-1f, 1f) : 0f,
                rotateOnY ? UnityEngine.Random.Range(-1f, 1f) : 0f,
                rotateOnZ ? UnityEngine.Random.Range(-1f, 1f) : 0f
            );

            if (axis == Vector3.zero)
                axis = Vector3.up;

            axis.Normalize();

            StartCoroutine(AnimateDropItem(collectible, targetPosition, axis, m_runtimeRotationSpeed, colliderStates));
        }

        private Vector3 ResolveTargetPosition(Transform entityTransform, RaycastHit hit)
        {
            Vector3 entityPosition = entityTransform.position;
            Vector3 hitPoint = hit.point;

            Vector3 flatDirection = hitPoint - entityPosition;
            flatDirection.y = 0f;

            bool hitEntity = IsEntityOrChild(hit.transform, entityTransform);

            if (flatDirection.sqrMagnitude <= 0.0001f)
            {
                flatDirection = entityTransform.forward;
                flatDirection.y = 0f;

                if (flatDirection.sqrMagnitude <= 0.0001f)
                    flatDirection = Vector3.forward;
            }

            flatDirection.Normalize();

            float flatDistance = Vector3.Distance(
                new Vector3(entityPosition.x, 0f, entityPosition.z),
                new Vector3(hitPoint.x, 0f, hitPoint.z)
            );

            float distance = Mathf.Min(flatDistance, m_runtimeDropRange);

            if (hitEntity && m_runtimeDropRange > 0f)
                distance = Mathf.Max(distance, Mathf.Min(m_runtimeSelfTargetMinimumDistance, m_runtimeDropRange));

            Vector3 targetPosition = entityPosition + flatDirection * distance;
            targetPosition.y = hitPoint.y;

            return targetPosition;
        }

        private static bool IsEntityOrChild(Transform candidate, Transform entityTransform)
        {
            if (!candidate || !entityTransform)
                return false;

            return candidate == entityTransform || candidate.IsChildOf(entityTransform);
        }

        private static ColliderState[] SetCollidersEnabled(CollectibleItem collectible, bool enabled)
        {
            if (!collectible)
                return Array.Empty<ColliderState>();

            var colliders = collectible.GetComponentsInChildren<Collider>(true);
            if (colliders == null || colliders.Length == 0)
                return Array.Empty<ColliderState>();

            var states = new ColliderState[colliders.Length];

            for (int i = 0; i < colliders.Length; i++)
            {
                var collider = colliders[i];

                states[i] = new ColliderState
                {
                    collider = collider,
                    enabled = collider && collider.enabled
                };

                if (collider)
                    collider.enabled = enabled;
            }

            return states;
        }

        private static void RestoreColliderStates(ColliderState[] states)
        {
            if (states == null)
                return;

            for (int i = 0; i < states.Length; i++)
            {
                var collider = states[i].collider;
                if (collider)
                    collider.enabled = states[i].enabled;
            }
        }

        private void RegisterDroppedItem(CollectibleItem collectible)
        {
            if (!Level.instance || collectible == null)
                return;

            if (Level.instance.droppedItems == null)
                Level.instance.droppedItems = new List<CollectibleItem>();

            collectible.onCollect.AddListener(() =>
            {
                if (Level.instance && Level.instance.droppedItems != null)
                    Level.instance.droppedItems.Remove(collectible);
            });

            Level.instance.droppedItems.Add(collectible);
        }

        private IEnumerator AnimateDropItem(
            CollectibleItem collectible,
            Vector3 targetPosition,
            Vector3 rotateAxis,
            float rotateSpeed,
            ColliderState[] colliderStates
        )
        {
            if (!collectible)
                yield break;

            Transform itemTransform = collectible.transform;
            if (!itemTransform)
                yield break;

            try
            {
                Vector3 startPosition = itemTransform.position;
                float startY = startPosition.y;
                float endY = targetPosition.y;

                float distance = Vector3.Distance(startPosition, targetPosition);
                float animationDuration = distance / m_runtimeDropSpeed;

                if (animationDuration <= 0.0001f)
                {
                    if (itemTransform)
                        itemTransform.position = targetPosition;

                    yield break;
                }

                float elapsedTime = 0f;

                while (elapsedTime < animationDuration)
                {
                    if (!collectible || !itemTransform)
                        yield break;

                    float deltaTime = useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
                    elapsedTime += deltaTime;

                    float t = Mathf.Clamp01(elapsedTime / animationDuration);

                    Vector3 flatPos = Vector3.Lerp(
                        new Vector3(startPosition.x, 0f, startPosition.z),
                        new Vector3(targetPosition.x, 0f, targetPosition.z),
                        t
                    );

                    float heightOffset = m_runtimeArcCurve.Evaluate(t) * m_runtimeArcHeight;
                    float currentY = Mathf.Lerp(startY, endY, t) + heightOffset;

                    itemTransform.position = new Vector3(flatPos.x, currentY, flatPos.z);
                    itemTransform.Rotate(rotateAxis, rotateSpeed * deltaTime, Space.World);

                    yield return null;
                }

                if (itemTransform)
                    itemTransform.position = targetPosition;
            }
            finally
            {
                RestoreColliderStates(colliderStates);
            }
        }
    }
}