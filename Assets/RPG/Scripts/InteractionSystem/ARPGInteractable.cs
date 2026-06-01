using UnityEngine;
using UnityEngine.Events;

namespace PLAYERTWO.ARPGProject
{
    [RequireComponent(typeof(Collider))]
    [AddComponentMenu("PLAYER TWO/ARPG Project/Interaction/ARPG Interactable")]
    public class ARPGInteractable : MonoBehaviour
    {
        public enum InteractionMode
        {
            Press,
            Hold,
        }

        [Header("Interaction")]
        [Tooltip("How this object should respond to the interaction key. Grab and Inspection modes are intentionally not included.")]
        public InteractionMode mode = InteractionMode.Press;

        [Tooltip("If true, this object can currently be selected by the interaction manager.")]
        public bool interactive = true;

        [Tooltip("If true, this object stops accepting interactions after a successful interaction.")]
        public bool interactOnce;

        [Tooltip("If true, this GameObject is disabled after a successful interaction.")]
        public bool disableOnInteract;

        [Tooltip("Optional existing ARPG Interactive to trigger when this component succeeds. If empty, one on this GameObject is used automatically.")]
        public Interactive linkedInteractive;

        [Tooltip("Events invoked when this interactable succeeds.")]
        public UnityEvent onInteract;

        [Header("Hold")]
        [Min(0.05f)]
        [Tooltip("Seconds the interaction key must be held before the event fires.")]
        public float holdDuration = 1f;

        [Tooltip("Maximum successful hold interactions. Use 0 for unlimited.")]
        public int holdUseLimit;

        [Header("Prompt")]
        [Tooltip("Text shown while the player can interact with this object.")]
        public string promptMessage = "Interact";

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

        public virtual bool canInteract =>
            interactive && CanCompleteHold() && (!linkedInteractive || linkedInteractive.interactive);

        protected virtual void Awake()
        {
            if (!linkedInteractive)
                TryGetComponent(out linkedInteractive);
        }

        public virtual string GetPromptMessage(string keyName)
        {
            if (string.IsNullOrWhiteSpace(promptMessage))
                return $"Press {keyName}";

            if (mode == InteractionMode.Hold)
                return $"Hold {keyName} - {promptMessage}";

            return $"Press {keyName} - {promptMessage}";
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
