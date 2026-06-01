using UnityEngine;
using UnityEngine.Events;

namespace PLAYERTWO.ARPGProject
{
    [RequireComponent(typeof(Collider))]
    [AddComponentMenu("ANSTUDIO/Interaction System/ARPG Interactable")]
    public class ARPGInteractable : MonoBehaviour
    {
        public enum InteractionMode
        {
            Press,
            Hold,
        }

        public enum PromptPositionMode
        {
            Transform,
            ColliderBoundsTop,
        }

        [Header("Interaction")]
        [Tooltip("How this object should respond to the interaction input. Grab and Inspection modes are intentionally not included.")]
        public InteractionMode mode = InteractionMode.Press;

        [Tooltip("If true, this object can currently be selected by the interaction manager.")]
        public bool interactive = true;

        [Tooltip("If true, this object stops accepting interactions after a successful interaction.")]
        public bool interactOnce;

        [Tooltip("If true, this GameObject is disabled after a successful interaction.")]
        public bool disableOnInteract;

        [Tooltip("Optional existing ARPG Interactive to trigger when this component succeeds. If empty, one on this GameObject or its parents is used automatically.")]
        public Interactive linkedInteractive;

        [Tooltip("Events invoked when this interactable succeeds.")]
        public UnityEvent onInteract;

        [Header("Selection Feedback")]
        [Tooltip("Invoked once when this interactable becomes the manager's selected target.")]
        public UnityEvent onSelected;

        [Tooltip("Invoked once when this interactable stops being the manager's selected target.")]
        public UnityEvent onDeselected;

        [Header("Hold")]
        [Min(0.05f)]
        [Tooltip("Seconds the interaction input must be held before the event fires.")]
        public float holdDuration = 1f;

        [Tooltip("Maximum successful hold interactions. Use 0 for unlimited.")]
        public int holdUseLimit;

        [Header("Prompt")]
        [Tooltip("Text shown while the player can interact with this object.")]
        public string promptMessage = "Interact";

        [Tooltip("How this object chooses the world position used by the screen-space prompt.")]
        public PromptPositionMode promptPositionMode = PromptPositionMode.Transform;

        [Tooltip("Optional transform used as the prompt's world anchor. If assigned, it takes priority over Collider Bounds Top mode.")]
        public Transform promptAnchor;

        [Tooltip("World-space vertical offset from the prompt anchor or collider top.")]
        public float promptYOffset = 1.75f;

        [Tooltip("Use manager-level text color and font size for this prompt.")]
        public bool useUniversalMessageSettings = true;

        public Color messageColor = Color.white;

        [Min(1)]
        public int messageSize = 24;

        [Tooltip("Use manager-level prompt icon and icon size for this prompt.")]
        public bool useUniversalImageSettings = true;

        public Sprite promptIcon;
        public Vector2 promptIconSize = new(48f, 48f);

        protected int m_holdUses;
        protected bool m_selected;
        protected Collider m_cachedCollider;

        public virtual bool canInteract =>
            interactive && CanCompleteHold() && (!linkedInteractive || linkedInteractive.interactive);

        protected virtual void Awake()
        {
            CacheReferences();
        }

        protected virtual void OnDisable()
        {
            if (m_selected)
                Deselect(null);
        }

        protected virtual void CacheReferences()
        {
            if (!linkedInteractive)
            {
                if (!TryGetComponent(out linkedInteractive))
                    linkedInteractive = GetComponentInParent<Interactive>();
            }

            if (!m_cachedCollider)
                TryGetComponent(out m_cachedCollider);
        }

        public virtual string GetPromptMessage(string keyName)
        {
            if (string.IsNullOrWhiteSpace(promptMessage))
                return mode == InteractionMode.Hold ? $"Hold {keyName}" : $"Press {keyName}";

            if (mode == InteractionMode.Hold)
                return $"Hold {keyName} - {promptMessage}";

            return $"Press {keyName} - {promptMessage}";
        }

        public virtual Transform GetPromptAnchor() =>
            promptAnchor ? promptAnchor : transform;

        public virtual float GetPromptYOffset() => promptYOffset;

        public virtual Vector3 GetPromptWorldPosition()
        {
            if (promptAnchor)
                return promptAnchor.position + Vector3.up * promptYOffset;

            if (promptPositionMode == PromptPositionMode.ColliderBoundsTop)
            {
                if (!m_cachedCollider)
                    TryGetComponent(out m_cachedCollider);

                if (m_cachedCollider)
                    return m_cachedCollider.bounds.center + Vector3.up * (m_cachedCollider.bounds.extents.y + promptYOffset);
            }

            return transform.position + Vector3.up * promptYOffset;
        }

        public virtual Color GetMessageColor(Color universalColor) =>
            useUniversalMessageSettings ? universalColor : messageColor;

        public virtual int GetMessageSize(int universalSize) =>
            useUniversalMessageSettings ? universalSize : messageSize;

        public virtual Sprite GetPromptIcon(Sprite universalIcon) =>
            useUniversalImageSettings ? universalIcon : promptIcon;

        public virtual Vector2 GetPromptIconSize(Vector2 universalSize) =>
            useUniversalImageSettings ? universalSize : promptIconSize;

        public virtual float GetHoldProgress(float heldTime)
        {
            if (mode != InteractionMode.Hold)
                return 0f;

            return Mathf.Clamp01(heldTime / Mathf.Max(holdDuration, 0.05f));
        }

        public virtual bool CanCompleteHold() =>
            mode != InteractionMode.Hold || holdUseLimit <= 0 || m_holdUses < holdUseLimit;

        public virtual void Select(Entity entity)
        {
            if (m_selected)
                return;

            m_selected = true;
            onSelected.Invoke();
        }

        public virtual void Deselect(Entity entity)
        {
            if (!m_selected)
                return;

            m_selected = false;
            onDeselected.Invoke();
        }

        public virtual void Interact(Entity entity)
        {
            if (!canInteract || !CanCompleteHold())
                return;

            if (mode == InteractionMode.Hold)
                m_holdUses++;

            linkedInteractive.SafeCall(interactive => interactive.Interact(entity));
            onInteract.Invoke();

            if (interactOnce)
                interactive = false;

            if (disableOnInteract)
                gameObject.SetActive(false);
        }
    }
}
