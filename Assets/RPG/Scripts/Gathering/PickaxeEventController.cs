using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace ShatterStone
{
    [DisallowMultipleComponent]
    public class PickaxeEventController : MonoBehaviour
    {
        public static PickaxeEventController Current { get; private set; }

        [Serializable]
        public class OreNodeUnityEvent : UnityEvent<OreNode> { }

        [Serializable]
        public class ColliderUnityEvent : UnityEvent<Collider> { }

        [Header("Runtime Registration")]
        [SerializeField] private bool registerAsCurrentOnEnable = true;

        [Header("Detection")]
        [SerializeField] private Collider hitCollider;

        [Tooltip("Optional. If disabled, any collider with an OreNode component on itself or its parents is valid.")]
        [SerializeField] private bool useOreNodeTagFilter = false;

        [SerializeField] private string oreNodeTag = "OreNode";
        [SerializeField] private LayerMask hitLayers = ~0;

        [Tooltip("Recommended ON. Makes AnimationEvent_Hit scan the hit collider area directly.")]
        [SerializeField] private bool usePhysicsOverlapOnPerformHit = true;

        [Tooltip("Size of the internal overlap buffer. Increase if you expect many colliders in the hit area.")]
        [SerializeField, Min(1)] private int overlapBufferSize = 32;

        [Header("Hit Settings")]
        [SerializeField, Min(1)] private int hitsPerImpact = 1;

        [Tooltip("If true, the pickaxe can only hit while the animation hit window is open.")]
        [SerializeField] private bool requireOpenHitWindow = true;

        [Tooltip("If true, hitting happens when the trigger enters an ore node during the hit window.")]
        [SerializeField] private bool hitOnTriggerEnter = false;

        [Tooltip("If false, each OreNode can only be hit once per swing.")]
        [SerializeField] private bool allowMultipleHitsPerSwing = false;

        [Header("Debug")]
        [SerializeField] private bool enableDebugLogs = false;

        [Header("Unity Events")]
        public UnityEvent onSwingStarted;
        public UnityEvent onSwingEnded;
        public UnityEvent onHitWindowOpened;
        public UnityEvent onHitWindowClosed;
        public OreNodeUnityEvent onOreNodeHit;
        public ColliderUnityEvent onValidTargetEntered;
        public ColliderUnityEvent onValidTargetExited;

        public event Action SwingStarted;
        public event Action SwingEnded;
        public event Action HitWindowOpened;
        public event Action HitWindowClosed;
        public event Action<OreNode> OreNodeHit;
        public event Action<Collider> ValidTargetEntered;
        public event Action<Collider> ValidTargetExited;

        private readonly HashSet<OreNode> nodesInRange = new HashSet<OreNode>();
        private readonly HashSet<OreNode> nodesHitThisSwing = new HashSet<OreNode>();
        private readonly List<OreNode> hitBuffer = new List<OreNode>();

        private Collider[] overlapBuffer;
        private bool hitWindowOpen;

        private void Start()
        {
            if (enableDebugLogs)
                Debug.Log("[PickaxeEventController] Started on: " + name, this);
        }

        private void Awake()
        {
            if (hitCollider == null)
                hitCollider = GetComponent<Collider>();

            if (hitCollider == null)
                hitCollider = GetComponentInChildren<Collider>(true);

            overlapBuffer = new Collider[overlapBufferSize];

            if (hitCollider == null)
            {
                Debug.LogWarning(
                    $"{nameof(PickaxeEventController)} could not find a hit collider. Assign one in the Inspector.",
                    this
                );
                return;
            }

            if (!hitCollider.isTrigger)
            {
                Debug.LogWarning(
                    $"{nameof(PickaxeEventController)} works best when the hit collider is set to Is Trigger.",
                    hitCollider
                );
            }
        }

        private void OnEnable()
        {
            if (registerAsCurrentOnEnable)
                RegisterAsCurrent();
        }

        private void OnDisable()
        {
            if (Current == this)
                Current = null;

            nodesInRange.Clear();
            nodesHitThisSwing.Clear();
            hitBuffer.Clear();
            hitWindowOpen = false;
        }

        public void RegisterAsCurrent()
        {
            Current = this;

            if (enableDebugLogs)
                Debug.Log("[PickaxeEventController] Registered as current: " + name, this);
        }

        public void UnregisterAsCurrent()
        {
            if (Current == this)
                Current = null;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!TryGetOreNode(other, out OreNode oreNode))
                return;

            nodesInRange.Add(oreNode);

            if (enableDebugLogs)
                Debug.Log($"Pickaxe trigger entered OreNode: {oreNode.name}", oreNode);

            ValidTargetEntered?.Invoke(other);
            onValidTargetEntered?.Invoke(other);

            if (hitOnTriggerEnter)
                TryHit(oreNode);
        }

        private void OnTriggerExit(Collider other)
        {
            if (!TryGetOreNode(other, out OreNode oreNode))
                return;

            nodesInRange.Remove(oreNode);

            if (enableDebugLogs)
                Debug.Log($"Pickaxe trigger exited OreNode: {oreNode.name}", oreNode);

            ValidTargetExited?.Invoke(other);
            onValidTargetExited?.Invoke(other);
        }

        public void BeginSwing()
        {
            nodesHitThisSwing.Clear();

            if (enableDebugLogs)
                Debug.Log("Pickaxe swing started.", this);

            SwingStarted?.Invoke();
            onSwingStarted?.Invoke();
        }

        public void EndSwing()
        {
            CloseHitWindow();

            if (enableDebugLogs)
                Debug.Log("Pickaxe swing ended.", this);

            SwingEnded?.Invoke();
            onSwingEnded?.Invoke();
        }

        public void OpenHitWindow()
        {
            hitWindowOpen = true;

            if (enableDebugLogs)
                Debug.Log("Pickaxe hit window opened.", this);

            HitWindowOpened?.Invoke();
            onHitWindowOpened?.Invoke();
        }

        public void CloseHitWindow()
        {
            if (!hitWindowOpen)
                return;

            hitWindowOpen = false;

            if (enableDebugLogs)
                Debug.Log("Pickaxe hit window closed.", this);

            HitWindowClosed?.Invoke();
            onHitWindowClosed?.Invoke();
        }

        public void PerformHit()
        {
            if (requireOpenHitWindow && !hitWindowOpen)
            {
                if (enableDebugLogs)
                    Debug.Log("PerformHit ignored because hit window is closed.", this);

                return;
            }

            hitBuffer.Clear();

            if (usePhysicsOverlapOnPerformHit)
            {
                FindOreNodesUsingPhysicsOverlap();
            }
            else
            {
                foreach (OreNode node in nodesInRange)
                {
                    if (node != null)
                        hitBuffer.Add(node);
                }
            }

            if (enableDebugLogs)
                Debug.Log($"PerformHit found {hitBuffer.Count} OreNode target(s).", this);

            foreach (OreNode node in hitBuffer)
                TryHit(node);
        }

        public void AnimationEvent_BeginSwing()
        {
            BeginSwing();
        }

        public void AnimationEvent_OpenHitWindow()
        {
            OpenHitWindow();
        }

        public void AnimationEvent_Hit()
        {
            PerformHit();
        }

        public void AnimationEvent_CloseHitWindow()
        {
            CloseHitWindow();
        }

        public void AnimationEvent_EndSwing()
        {
            EndSwing();
        }

        private void FindOreNodesUsingPhysicsOverlap()
        {
            if (hitCollider == null)
                return;

            int count = OverlapHitCollider();

            if (enableDebugLogs)
                Debug.Log($"Physics overlap found {count} collider(s).", this);

            for (int i = 0; i < count; i++)
            {
                Collider other = overlapBuffer[i];
                if (other == null)
                    continue;

                if (!TryGetOreNode(other, out OreNode oreNode))
                    continue;

                if (!hitBuffer.Contains(oreNode))
                    hitBuffer.Add(oreNode);
            }
        }

        private int OverlapHitCollider()
        {
            if (hitCollider is BoxCollider box)
            {
                Vector3 center = box.transform.TransformPoint(box.center);
                Vector3 halfExtents = Vector3.Scale(box.size * 0.5f, Abs(box.transform.lossyScale));

                return Physics.OverlapBoxNonAlloc(
                    center,
                    halfExtents,
                    overlapBuffer,
                    box.transform.rotation,
                    hitLayers,
                    QueryTriggerInteraction.Collide
                );
            }

            if (hitCollider is SphereCollider sphere)
            {
                Vector3 center = sphere.transform.TransformPoint(sphere.center);
                Vector3 scale = Abs(sphere.transform.lossyScale);
                float radius = sphere.radius * Mathf.Max(scale.x, scale.y, scale.z);

                return Physics.OverlapSphereNonAlloc(
                    center,
                    radius,
                    overlapBuffer,
                    hitLayers,
                    QueryTriggerInteraction.Collide
                );
            }

            if (hitCollider is CapsuleCollider capsule)
            {
                GetCapsuleWorldPoints(capsule, out Vector3 pointA, out Vector3 pointB, out float radius);

                return Physics.OverlapCapsuleNonAlloc(
                    pointA,
                    pointB,
                    radius,
                    overlapBuffer,
                    hitLayers,
                    QueryTriggerInteraction.Collide
                );
            }

            Bounds bounds = hitCollider.bounds;

            return Physics.OverlapBoxNonAlloc(
                bounds.center,
                bounds.extents,
                overlapBuffer,
                Quaternion.identity,
                hitLayers,
                QueryTriggerInteraction.Collide
            );
        }

        private bool TryHit(OreNode oreNode)
        {
            if (oreNode == null)
                return false;

            if (requireOpenHitWindow && !hitWindowOpen)
                return false;

            if (!allowMultipleHitsPerSwing && nodesHitThisSwing.Contains(oreNode))
            {
                if (enableDebugLogs)
                    Debug.Log($"OreNode already hit this swing: {oreNode.name}", oreNode);

                return false;
            }

            nodesHitThisSwing.Add(oreNode);

            if (enableDebugLogs)
                Debug.Log($"Calling Interact({hitsPerImpact}) on OreNode: {oreNode.name}", oreNode);

            oreNode.Interact(hitsPerImpact);

            OreNodeHit?.Invoke(oreNode);
            onOreNodeHit?.Invoke(oreNode);

            return true;
        }

        private bool TryGetOreNode(Collider other, out OreNode oreNode)
        {
            oreNode = null;

            if (other == null)
                return false;

            if (((1 << other.gameObject.layer) & hitLayers) == 0)
                return false;

            oreNode = other.GetComponent<OreNode>();

            if (oreNode == null)
                oreNode = other.GetComponentInParent<OreNode>();

            if (oreNode == null)
                return false;

            if (useOreNodeTagFilter)
            {
                bool colliderHasTag = other.CompareTag(oreNodeTag);
                bool oreNodeHasTag = oreNode.CompareTag(oreNodeTag);

                if (!colliderHasTag && !oreNodeHasTag)
                    return false;
            }

            return true;
        }

        private static Vector3 Abs(Vector3 value)
        {
            return new Vector3(
                Mathf.Abs(value.x),
                Mathf.Abs(value.y),
                Mathf.Abs(value.z)
            );
        }

        private static void GetCapsuleWorldPoints(
            CapsuleCollider capsule,
            out Vector3 pointA,
            out Vector3 pointB,
            out float radius
        )
        {
            Transform capsuleTransform = capsule.transform;
            Vector3 scale = Abs(capsuleTransform.lossyScale);

            Vector3 localDirection;
            float heightScale;
            float radiusScale;

            switch (capsule.direction)
            {
                case 0:
                    localDirection = Vector3.right;
                    heightScale = scale.x;
                    radiusScale = Mathf.Max(scale.y, scale.z);
                    break;

                case 1:
                    localDirection = Vector3.up;
                    heightScale = scale.y;
                    radiusScale = Mathf.Max(scale.x, scale.z);
                    break;

                default:
                    localDirection = Vector3.forward;
                    heightScale = scale.z;
                    radiusScale = Mathf.Max(scale.x, scale.y);
                    break;
            }

            radius = capsule.radius * radiusScale;

            float height = Mathf.Max(capsule.height * heightScale, radius * 2f);
            float halfSegment = Mathf.Max(0f, height * 0.5f - radius);

            Vector3 center = capsuleTransform.TransformPoint(capsule.center);
            Vector3 direction = capsuleTransform.TransformDirection(localDirection);

            pointA = center + direction * halfSegment;
            pointB = center - direction * halfSegment;
        }
    }
}