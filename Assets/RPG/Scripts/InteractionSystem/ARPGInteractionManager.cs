using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace PLAYERTWO.ARPGProject
{
    [DisallowMultipleComponent]
    [AddComponentMenu("PLAYER TWO/ARPG Project/Interaction/Interaction Manager")]
    public class ARPGInteractionManager : MonoBehaviour
    {
        public enum DetectionMode
        {
            PlayerRadius,
            CameraAim,
            PlayerRadiusAndCameraAim,
        }

        [Header("Important Fields")]
        [Tooltip("The player entity that triggers interactions. If empty, the manager searches for the player tag.")]
        public Entity player;

        [Tooltip("Key required to interact with objects.")]
        public Key keyToInteract = Key.E;

        [Header("Main Settings")]
        [Tooltip("How interactables are selected.")]
        public DetectionMode detectionMode = DetectionMode.PlayerRadius;

        [Min(0.1f)]
        [Tooltip("Maximum distance from the player to an interactable.")]
        public float interactionDistance = 3f;

        [Tooltip("Layer mask used when scanning for interactable colliders.")]
        public LayerMask interactionLayers = Physics.DefaultRaycastLayers;

        [Tooltip("Whether trigger colliders are included in scans and aim checks.")]
        public QueryTriggerInteraction triggerInteraction = QueryTriggerInteraction.Collide;

        [Tooltip("Camera used by Camera Aim detection. If empty, Camera.main is used.")]
        public Camera interactionCamera;

        [Min(0.1f)]
        [Tooltip("Maximum ray distance for Camera Aim detection.")]
        public float aimDistance = 8f;

        [Tooltip("Ignore interaction input while the pointer is over uGUI.")]
        public bool ignoreInputOverUI = true;

        [Header("Universal Prompt Settings")]
        public ARPGInteractionPrompt prompt;
        public Sprite universalPromptIcon;
        public Vector2 universalPromptIconSize = new(48f, 48f);
        public Color universalPromptMessageColor = Color.white;

        [Min(1)]
        public int universalPromptMessageSize = 24;

        [Tooltip("Prompt used for existing Interactive components that do not also have an ARPGInteractable.")]
        public string defaultPromptMessage = "Interact";

        protected readonly Collider[] m_scanBuffer = new Collider[64];

        protected ARPGInteractable m_current;
        protected Interactive m_currentLegacy;
        protected float m_holdTime;
        protected bool m_holdCompletedThisPress;

        public static ARPGInteractionManager instance { get; protected set; }

        protected Transform playerTransform => player ? player.transform : null;

        protected virtual void Awake()
        {
            if (instance && instance != this)
            {
                Debug.LogWarning("Only one ARPGInteractionManager should be active in a scene.", this);
                enabled = false;
                return;
            }

            instance = this;
            InitializePrompt();
        }

        protected virtual void OnDestroy()
        {
            if (instance == this)
                instance = null;
        }

        protected virtual void Start()
        {
            ResolvePlayer();
        }

        protected virtual void Update()
        {
            ResolvePlayer();
            RefreshCurrentInteractable();
            UpdatePrompt();
            HandleInput();
        }

        protected virtual void ResolvePlayer()
        {
            if (player)
                return;

            var playerObject = GameObject.FindGameObjectWithTag(GameTags.Player);

            if (playerObject)
                playerObject.TryGetComponent(out player);
        }

        protected virtual void InitializePrompt()
        {
            if (prompt)
                return;

            if (!TryGetComponent(out prompt))
                prompt = gameObject.AddComponent<ARPGInteractionPrompt>();
        }

        protected virtual void RefreshCurrentInteractable()
        {
            m_current = null;
            m_currentLegacy = null;

            if (!playerTransform)
                return;

            if (detectionMode == DetectionMode.CameraAim || detectionMode == DetectionMode.PlayerRadiusAndCameraAim)
            {
                if (TryGetAimedInteractable(out var aimed, out var aimedLegacy))
                {
                    m_current = aimed;
                    m_currentLegacy = aimedLegacy;

                    if (detectionMode == DetectionMode.CameraAim || IsWithinPlayerDistance(GetCurrentTransform()))
                        return;

                    m_current = null;
                    m_currentLegacy = null;
                }

                if (detectionMode == DetectionMode.PlayerRadiusAndCameraAim)
                    return;
            }

            if (detectionMode == DetectionMode.PlayerRadius)
                TryGetClosestInteractable(out m_current, out m_currentLegacy);
        }

        protected virtual bool TryGetAimedInteractable(out ARPGInteractable interactable, out Interactive legacy)
        {
            interactable = null;
            legacy = null;

            var cam = interactionCamera ? interactionCamera : Camera.main;

            if (!cam)
                return false;

            var ray = new Ray(cam.transform.position, cam.transform.forward);

            if (!Physics.Raycast(ray, out var hit, aimDistance, interactionLayers, triggerInteraction))
                return false;

            return TryReadInteractable(hit.collider, out interactable, out legacy);
        }

        protected virtual bool TryGetClosestInteractable(out ARPGInteractable interactable, out Interactive legacy)
        {
            interactable = null;
            legacy = null;

            var count = Physics.OverlapSphereNonAlloc(
                playerTransform.position,
                interactionDistance,
                m_scanBuffer,
                interactionLayers,
                triggerInteraction
            );

            var closestDistance = float.PositiveInfinity;

            for (int i = 0; i < count; i++)
            {
                if (!TryReadInteractable(m_scanBuffer[i], out var candidate, out var legacyCandidate))
                    continue;

                var candidateTransform = candidate ? candidate.transform : legacyCandidate.transform;
                var distance = Vector3.SqrMagnitude(candidateTransform.position - playerTransform.position);

                if (distance >= closestDistance)
                    continue;

                closestDistance = distance;
                interactable = candidate;
                legacy = legacyCandidate;
            }

            return interactable || legacy;
        }

        protected virtual bool TryReadInteractable(
            Collider source,
            out ARPGInteractable interactable,
            out Interactive legacy
        )
        {
            interactable = null;
            legacy = null;

            if (!source)
                return false;

            source.TryGetComponent(out interactable);
            source.TryGetComponent(out legacy);

            if (interactable && !interactable.canInteract)
                interactable = null;

            if (legacy && !legacy.interactive)
                legacy = null;

            return interactable || legacy;
        }

        protected virtual bool IsWithinPlayerDistance(Transform target)
        {
            if (!target || !playerTransform)
                return false;

            return Vector3.Distance(playerTransform.position, target.position) <= interactionDistance;
        }

        protected virtual Transform GetCurrentTransform()
        {
            if (m_current)
                return m_current.transform;

            return m_currentLegacy ? m_currentLegacy.transform : null;
        }

        protected virtual void UpdatePrompt()
        {
            if (!prompt)
                return;

            if (!m_current && !m_currentLegacy)
            {
                prompt.Hide();
                return;
            }

            var keyName = keyToInteract.ToString();
            var message = m_current
                ? m_current.GetPromptMessage(keyName)
                : $"Press {keyName} - {defaultPromptMessage}";

            if (m_current && m_current.mode == ARPGInteractable.InteractionMode.Hold && m_holdTime > 0f)
            {
                var progress = Mathf.RoundToInt(m_current.GetHoldProgress(m_holdTime) * 100f);
                message = $"{message} ({progress}%)";
            }

            var color = m_current
                ? m_current.GetMessageColor(universalPromptMessageColor)
                : universalPromptMessageColor;
            var size = m_current
                ? m_current.GetMessageSize(universalPromptMessageSize)
                : universalPromptMessageSize;
            var icon = m_current ? m_current.GetPromptIcon(universalPromptIcon) : universalPromptIcon;
            var iconSize = m_current
                ? m_current.GetPromptIconSize(universalPromptIconSize)
                : universalPromptIconSize;

            prompt.Show(message, icon, color, size, iconSize);
        }

        protected virtual void HandleInput()
        {
            if (!m_current && !m_currentLegacy)
            {
                ResetHold();
                return;
            }

            if (ignoreInputOverUI && EventSystem.current && EventSystem.current.IsPointerOverGameObject())
            {
                ResetHold();
                return;
            }

            if (m_current && m_current.mode == ARPGInteractable.InteractionMode.Hold)
            {
                HandleHoldInput();
                return;
            }

            if (WasInteractionKeyPressedThisFrame())
                CompleteInteraction();
        }

        protected virtual void HandleHoldInput()
        {
            if (IsInteractionKeyPressed() && !m_holdCompletedThisPress)
            {
                m_holdTime += Time.deltaTime;

                if (m_holdTime >= m_current.holdDuration)
                {
                    CompleteInteraction();
                    m_holdCompletedThisPress = true;
                }
            }

            if (WasInteractionKeyReleasedThisFrame())
                ResetHold();
        }

        protected virtual bool IsInteractionKeyPressed()
        {
            var keyboard = Keyboard.current;
            return keyboard != null && keyboard[keyToInteract].isPressed;
        }

        protected virtual bool WasInteractionKeyPressedThisFrame()
        {
            var keyboard = Keyboard.current;
            return keyboard != null && keyboard[keyToInteract].wasPressedThisFrame;
        }

        protected virtual bool WasInteractionKeyReleasedThisFrame()
        {
            var keyboard = Keyboard.current;
            return keyboard != null && keyboard[keyToInteract].wasReleasedThisFrame;
        }

        protected virtual void CompleteInteraction()
        {
            if (m_current)
                m_current.Interact(player);
            else if (m_currentLegacy)
                m_currentLegacy.Interact(player);

            ResetHold();
            RefreshCurrentInteractable();
        }

        protected virtual void ResetHold()
        {
            m_holdTime = 0f;
            m_holdCompletedThisPress = false;
        }

#if UNITY_EDITOR
        protected virtual void OnDrawGizmosSelected()
        {
            if (!playerTransform)
                return;

            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(playerTransform.position, interactionDistance);
        }
#endif
    }
}
