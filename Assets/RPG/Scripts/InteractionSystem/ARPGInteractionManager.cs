using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace PLAYERTWO.ARPGProject
{
    [DisallowMultipleComponent]
    [AddComponentMenu("ANSTUDIO/Interaction System/Interaction Manager")]
    public class ARPGInteractionManager : MonoBehaviour
    {
        public enum DetectionMode
        {
            PlayerRadius,

            [System.Obsolete("CameraAim is kept for old scenes. It now behaves like CursorHover for top-down interaction.")]
            CameraAim,

            [System.Obsolete("PlayerRadiusAndCameraAim is kept for old scenes. It now behaves like CursorHoverWithinPlayerRadius with a radius fallback.")]
            PlayerRadiusAndCameraAim,

            CursorHover,
            CursorHoverWithinPlayerRadius,
            ClosestToCursorWithinPlayerRadius,
        }

        public enum InteractionMouseButton
        {
            Left,
            Right,
            Middle,
        }

        [Header("Important Fields")]
        [Tooltip("The player entity that triggers interactions. If empty, the manager searches for Player Tag.")]
        [HideInInspector]
        public Entity player;

        [ARPGTagSelector]
        [Tooltip("Tag used to find the player Entity when Player is not assigned.")]
        public string playerTag = GameTags.Player;

        [Tooltip("Keyboard key required to interact with objects.")]
        public Key keyToInteract = Key.E;

        [Header("Main Settings")]
        [Tooltip("How interactables are selected. Closest To Cursor Within Player Radius is recommended for top-down Diablo-like games.")]
        public DetectionMode detectionMode = DetectionMode.ClosestToCursorWithinPlayerRadius;

        [Min(0.1f)]
        [Tooltip("Maximum XZ distance from the player to an interactable.")]
        public float interactionDistance = 3f;

        [Tooltip("Layer mask used when scanning for interactable colliders.")]
        public LayerMask interactionLayers = Physics.DefaultRaycastLayers;

        [Tooltip("Whether trigger colliders are included in scans and cursor ray checks.")]
        public QueryTriggerInteraction triggerInteraction = QueryTriggerInteraction.Collide;

        [Tooltip("Camera used by cursor detection and prompt projection. If empty, Camera.main is cached.")]
        public Camera interactionCamera;

        [Min(0.1f)]
        [Tooltip("Maximum ray distance for cursor hover and cursor world projection.")]
        public float aimDistance = 100f;

        [Tooltip("Ignore interaction input while the pointer is over uGUI.")]
        public bool ignoreInputOverUI = true;

        [Header("Top-Down Selection")]
        [Tooltip("When true, interaction distance ignores vertical height and measures only XZ distance.")]
        public bool useFlatDistance = true;

        [Min(0f)]
        [Tooltip("World-space distance bonus given to the current target so prompts do not flicker between nearby objects.")]
        public float currentTargetStickiness = 0.35f;

        [Tooltip("Layers used to project the cursor onto the world for Closest To Cursor selection. If nothing is hit, a flat plane at player height is used.")]
        public LayerMask cursorProjectionLayers = Physics.DefaultRaycastLayers;

        [Tooltip("When true, the cursor is projected against scene colliders before falling back to a flat plane at player height.")]
        public bool projectCursorAgainstSceneColliders = true;

        [Header("Mouse Interaction")]
        [Tooltip("Allow the selected interactable to be triggered with a mouse button as well as the keyboard key.")]
        public bool allowMouseClickInteract = true;

        [Tooltip("Mouse button used for click and hold interactions when mouse interaction is enabled.")]
        public InteractionMouseButton mouseButtonToInteract = InteractionMouseButton.Left;

        [Header("Player Facing")]
        [Tooltip("Rotate the player toward the selected interactable only after interaction input starts, not while the prompt is merely active.")]
        public bool rotatePlayerToFaceInteractable;

        [Tooltip("Optional transform to rotate instead of the player Entity transform. Use this when only a model child should turn.")]
        public Transform playerRotationTransform;

        [Tooltip("When true, ignore height differences and rotate only around the Y axis. Recommended for top-down ARPG cameras.")]
        public bool useFlatPlayerFacing = true;

        [Tooltip("When true, face the interactable instantly when interaction input starts. When false, rotate using Player Rotation Speed until the target is faced.")]
        public bool snapPlayerRotationToInteractable;

        [Min(1f)]
        [Tooltip("Degrees per second used when Snap Player Rotation To Interactable is disabled.")]
        public float playerRotationSpeed = 720f;

        [Header("Universal Prompt Settings")]
        public ARPGInteractionPrompt prompt;
        public Sprite universalPromptIcon;
        public Vector2 universalPromptIconSize = new(48f, 48f);
        public Color universalPromptMessageColor = Color.white;

        [Min(1)]
        public int universalPromptMessageSize = 24;

        [Tooltip("Prompt used for existing Interactive components that do not also have an ARPGInteractable.")]
        public string defaultPromptMessage = "Interact";

        [Tooltip("World-space vertical offset used when showing prompts for existing Interactive components.")]
        public float defaultPromptYOffset = 1.75f;

        [Tooltip("Optional collider-bounds prompt placement for existing Interactive components that do not have ARPGInteractable.")]
        public bool defaultUseColliderBoundsPromptPosition;

        [Header("Legacy Compatibility")]
        [Tooltip("Runtime-only system default: OFF. When false, only objects with ARPGInteractable can be selected. Existing Interactive components are ignored unless they are linked from an ARPGInteractable.")]
        public bool includeLegacyInteractive;

        protected readonly Collider[] m_scanBuffer = new Collider[64];
        protected readonly RaycastHit[] m_raycastBuffer = new RaycastHit[32];

        protected ARPGInteractable m_current;
        protected Interactive m_currentLegacy;
        protected float m_holdTime;
        protected bool m_holdCompletedThisPress;
        protected string m_lastInvalidPlayerTag;
        protected Camera m_cachedCamera;
        protected Transform m_playerFacingTarget;
        protected bool m_playerFacingActive;

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
            CacheCamera();
            InitializePrompt();
        }

        protected virtual void OnEnable()
        {
            CacheCamera();
        }

        protected virtual void OnDisable()
        {
            ClearSelection();

            if (prompt)
                prompt.Hide();
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
            UpdatePlayerFacing();
        }

        protected virtual void ResolvePlayer()
        {
            if (player || string.IsNullOrWhiteSpace(playerTag))
                return;

            GameObject playerObject;

            try
            {
                playerObject = GameObject.FindGameObjectWithTag(playerTag);
                m_lastInvalidPlayerTag = null;
            }
            catch (UnityException)
            {
                if (m_lastInvalidPlayerTag != playerTag)
                {
                    Debug.LogWarning($"Cannot resolve player because tag '{playerTag}' is not defined.", this);
                    m_lastInvalidPlayerTag = playerTag;
                }

                return;
            }

            if (playerObject)
                playerObject.TryGetComponent(out player);
        }

        protected virtual void InitializePrompt()
        {
            if (prompt)
                return;

            if (TryGetComponent(out prompt))
                return;

            prompt = FindFirstObjectByType<ARPGInteractionPrompt>();

            if (!prompt)
                prompt = gameObject.AddComponent<ARPGInteractionPrompt>();
        }

        protected virtual void CacheCamera()
        {
            m_cachedCamera = interactionCamera ? interactionCamera : Camera.main;
        }

        protected virtual Camera GetInteractionCamera()
        {
            if (interactionCamera)
            {
                m_cachedCamera = interactionCamera;
                return m_cachedCamera;
            }

            if (!m_cachedCamera)
                m_cachedCamera = Camera.main;

            return m_cachedCamera;
        }

        protected virtual void RefreshCurrentInteractable()
        {
            var previous = m_current;
            var previousLegacy = m_currentLegacy;

            ARPGInteractable next = null;
            Interactive nextLegacy = null;

            if (playerTransform)
                FindNextInteractable(previous, previousLegacy, out next, out nextLegacy);

            SetCurrentInteractable(next, nextLegacy, previous, previousLegacy);
        }

        protected virtual void FindNextInteractable(
            ARPGInteractable previous,
            Interactive previousLegacy,
            out ARPGInteractable next,
            out Interactive nextLegacy
        )
        {
            next = null;
            nextLegacy = null;

            switch (detectionMode)
            {
                case DetectionMode.CursorHover:
#pragma warning disable 0618
                case DetectionMode.CameraAim:
#pragma warning restore 0618
                    TryGetCursorInteractable(false, out next, out nextLegacy);
                    break;

                case DetectionMode.CursorHoverWithinPlayerRadius:
                    TryGetCursorInteractable(true, out next, out nextLegacy);
                    break;

                case DetectionMode.ClosestToCursorWithinPlayerRadius:
                    if (TryGetCursorInteractable(true, out next, out nextLegacy))
                        return;

                    if (TryGetClosestToCursorInteractable(previous, previousLegacy, out next, out nextLegacy))
                        return;

                    TryGetClosestInteractable(previous, previousLegacy, out next, out nextLegacy);
                    break;

#pragma warning disable 0618
                case DetectionMode.PlayerRadiusAndCameraAim:
#pragma warning restore 0618
                    if (TryGetCursorInteractable(true, out next, out nextLegacy))
                        return;

                    TryGetClosestInteractable(previous, previousLegacy, out next, out nextLegacy);
                    break;

                case DetectionMode.PlayerRadius:
                default:
                    TryGetClosestInteractable(previous, previousLegacy, out next, out nextLegacy);
                    break;
            }
        }

        protected virtual bool TryGetCursorInteractable(
            bool requirePlayerDistance,
            out ARPGInteractable interactable,
            out Interactive legacy
        )
        {
            interactable = null;
            legacy = null;

            if (!TryGetCursorRay(out var ray))
                return false;

            var hitCount = Physics.RaycastNonAlloc(
                ray,
                m_raycastBuffer,
                aimDistance,
                interactionLayers,
                triggerInteraction
            );

            var closestRayDistance = float.PositiveInfinity;

            for (int i = 0; i < hitCount; i++)
            {
                var hit = m_raycastBuffer[i];

                if (!TryReadInteractable(hit.collider, out var candidate, out var legacyCandidate))
                    continue;

                var candidateTransform = GetTargetTransform(candidate, legacyCandidate);

                if (requirePlayerDistance && !IsWithinPlayerDistance(candidateTransform))
                    continue;

                if (hit.distance >= closestRayDistance)
                    continue;

                closestRayDistance = hit.distance;
                interactable = candidate;
                legacy = legacyCandidate;
            }

            return interactable || legacy;
        }

        protected virtual bool TryGetClosestToCursorInteractable(
            ARPGInteractable previous,
            Interactive previousLegacy,
            out ARPGInteractable interactable,
            out Interactive legacy
        )
        {
            interactable = null;
            legacy = null;

            if (!TryGetCursorWorldPoint(out var cursorWorldPoint))
                return false;

            var count = Physics.OverlapSphereNonAlloc(
                playerTransform.position,
                interactionDistance,
                m_scanBuffer,
                interactionLayers,
                triggerInteraction
            );

            var bestScore = float.PositiveInfinity;

            for (int i = 0; i < count; i++)
            {
                if (!TryReadInteractable(m_scanBuffer[i], out var candidate, out var legacyCandidate))
                    continue;

                var candidateTransform = GetTargetTransform(candidate, legacyCandidate);

                if (!IsWithinPlayerDistance(candidateTransform))
                    continue;

                var score = GetDistance(candidateTransform.position, cursorWorldPoint);
                score = ApplyStickiness(score, candidate, legacyCandidate, previous, previousLegacy);

                if (score >= bestScore)
                    continue;

                bestScore = score;
                interactable = candidate;
                legacy = legacyCandidate;
            }

            return interactable || legacy;
        }

        protected virtual bool TryGetClosestInteractable(
            ARPGInteractable previous,
            Interactive previousLegacy,
            out ARPGInteractable interactable,
            out Interactive legacy
        )
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

                var candidateTransform = GetTargetTransform(candidate, legacyCandidate);
                var distance = GetDistance(candidateTransform.position, playerTransform.position);
                distance = ApplyStickiness(distance, candidate, legacyCandidate, previous, previousLegacy);

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

            if (!interactable)
                interactable = source.GetComponentInParent<ARPGInteractable>();

            if (interactable)
            {
                if (!interactable.canInteract)
                {
                    interactable = null;
                    return false;
                }

                // ARPGInteractable is now the only default target type.
                // It may still call an existing Interactive through linkedInteractive when it succeeds.
                return true;
            }

            if (!includeLegacyInteractive)
                return false;

            source.TryGetComponent(out legacy);

            if (!legacy)
                legacy = source.GetComponentInParent<Interactive>();

            if (legacy && !legacy.interactive)
                legacy = null;

            return legacy;
        }

        protected virtual bool TryGetCursorRay(out Ray ray)
        {
            ray = default;

            var cam = GetInteractionCamera();
            var mouse = Mouse.current;

            if (!cam || mouse == null)
                return false;

            ray = cam.ScreenPointToRay(mouse.position.ReadValue());
            return true;
        }

        protected virtual bool TryGetCursorWorldPoint(out Vector3 worldPoint)
        {
            worldPoint = default;

            if (!TryGetCursorRay(out var ray))
                return false;

            if (projectCursorAgainstSceneColliders && Physics.Raycast(
                    ray,
                    out var hit,
                    aimDistance,
                    cursorProjectionLayers,
                    triggerInteraction
                ))
            {
                worldPoint = hit.point;
                return true;
            }

            var planeHeight = playerTransform ? playerTransform.position.y : 0f;
            var plane = new Plane(Vector3.up, new Vector3(0f, planeHeight, 0f));

            if (!plane.Raycast(ray, out var enter))
                return false;

            worldPoint = ray.GetPoint(enter);
            return true;
        }

        protected virtual bool IsWithinPlayerDistance(Transform target)
        {
            if (!target || !playerTransform)
                return false;

            return GetDistance(target.position, playerTransform.position) <= interactionDistance;
        }

        protected virtual float GetDistance(Vector3 a, Vector3 b)
        {
            if (useFlatDistance)
            {
                a.y = 0f;
                b.y = 0f;
            }

            return Vector3.Distance(a, b);
        }

        protected virtual float ApplyStickiness(
            float score,
            ARPGInteractable candidate,
            Interactive legacyCandidate,
            ARPGInteractable previous,
            Interactive previousLegacy
        )
        {
            if (currentTargetStickiness <= 0f)
                return score;

            if (!IsSameTarget(candidate, legacyCandidate, previous, previousLegacy))
                return score;

            return Mathf.Max(0f, score - currentTargetStickiness);
        }

        protected virtual bool IsSameTarget(
            ARPGInteractable a,
            Interactive aLegacy,
            ARPGInteractable b,
            Interactive bLegacy
        )
        {
            return (a && a == b) || (!a && aLegacy && aLegacy == bLegacy);
        }

        protected virtual Transform GetTargetTransform(ARPGInteractable interactable, Interactive legacy)
        {
            if (interactable)
                return interactable.transform;

            return legacy ? legacy.transform : null;
        }

        protected virtual Vector3 GetTargetPosition(ARPGInteractable interactable, Interactive legacy)
        {
            if (interactable)
                return interactable.transform.position;

            return legacy ? legacy.transform.position : Vector3.zero;
        }

        protected virtual void SetCurrentInteractable(
            ARPGInteractable next,
            Interactive nextLegacy,
            ARPGInteractable previous,
            Interactive previousLegacy
        )
        {
            m_current = next;
            m_currentLegacy = nextLegacy;

            if (IsSameTarget(next, nextLegacy, previous, previousLegacy))
                return;

            ResetHold();

            if (previous)
                previous.Deselect(player);

            if (next)
                next.Select(player);
        }

        protected virtual void ClearSelection()
        {
            var previous = m_current;

            m_current = null;
            m_currentLegacy = null;
            ResetHold();

            if (previous)
                previous.Deselect(player);
        }

        protected virtual Vector3 GetCurrentPromptWorldPosition()
        {
            if (m_current)
                return m_current.GetPromptWorldPosition();

            if (!m_currentLegacy)
                return Vector3.zero;

            if (defaultUseColliderBoundsPromptPosition && m_currentLegacy.TryGetComponent(out Collider collider))
                return collider.bounds.center + Vector3.up * (collider.bounds.extents.y + defaultPromptYOffset);

            return m_currentLegacy.transform.position + Vector3.up * defaultPromptYOffset;
        }

        protected virtual Camera GetPromptCamera() => GetInteractionCamera();

        protected virtual Transform GetPlayerRotationTransform() =>
            playerRotationTransform ? playerRotationTransform : playerTransform;

        protected virtual void StartPlayerFacingInteraction()
        {
            if (!rotatePlayerToFaceInteractable)
                return;

            var rotationTransform = GetPlayerRotationTransform();
            var target = GetTargetTransform(m_current, m_currentLegacy);

            if (!rotationTransform || !target)
                return;

            m_playerFacingTarget = target;
            m_playerFacingActive = true;
            UpdatePlayerFacing();
        }

        protected virtual void StopPlayerFacingInteraction()
        {
            m_playerFacingActive = false;
            m_playerFacingTarget = null;
        }

        protected virtual void UpdatePlayerFacing()
        {
            if (!m_playerFacingActive)
                return;

            if (!rotatePlayerToFaceInteractable)
            {
                StopPlayerFacingInteraction();
                return;
            }

            var rotationTransform = GetPlayerRotationTransform();

            if (!rotationTransform || !m_playerFacingTarget)
            {
                StopPlayerFacingInteraction();
                return;
            }

            var direction = m_playerFacingTarget.position - rotationTransform.position;

            if (useFlatPlayerFacing)
                direction.y = 0f;

            if (direction.sqrMagnitude <= 0.0001f)
            {
                StopPlayerFacingInteraction();
                return;
            }

            var targetRotation = Quaternion.LookRotation(direction.normalized, Vector3.up);

            if (snapPlayerRotationToInteractable)
            {
                rotationTransform.rotation = targetRotation;
                StopPlayerFacingInteraction();
                return;
            }

            var step = playerRotationSpeed * Time.deltaTime;
            rotationTransform.rotation = Quaternion.RotateTowards(
                rotationTransform.rotation,
                targetRotation,
                step
            );

            if (Quaternion.Angle(rotationTransform.rotation, targetRotation) <= 0.1f)
                StopPlayerFacingInteraction();
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

            var keyName = GetInteractionInputName();
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

            prompt.ShowAtWorldPosition(
                message,
                icon,
                color,
                size,
                iconSize,
                GetCurrentPromptWorldPosition(),
                GetPromptCamera()
            );
        }

        protected virtual string GetInteractionInputName()
        {
            if (!allowMouseClickInteract)
                return keyToInteract.ToString();

            return $"{keyToInteract}/{mouseButtonToInteract} Click";
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

            if (WasInteractionInputPressedThisFrame())
            {
                StartPlayerFacingInteraction();
                CompleteInteraction();
            }
        }

        protected virtual void HandleHoldInput()
        {
            var inputPressed = IsInteractionInputPressed();

            if (inputPressed && !m_holdCompletedThisPress)
            {
                if (m_holdTime <= 0f)
                    StartPlayerFacingInteraction();

                m_holdTime += Time.deltaTime;

                if (m_holdTime >= m_current.holdDuration)
                {
                    CompleteInteraction();
                    m_holdCompletedThisPress = true;
                }
            }

            if (!inputPressed)
            {
                ResetHold();
                StopPlayerFacingInteraction();
            }
        }

        protected virtual bool IsInteractionInputPressed() =>
            IsInteractionKeyPressed() || IsInteractionMouseButtonPressed();

        protected virtual bool WasInteractionInputPressedThisFrame() =>
            WasInteractionKeyPressedThisFrame() || WasInteractionMouseButtonPressedThisFrame();

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

        protected virtual bool IsInteractionMouseButtonPressed()
        {
            if (!allowMouseClickInteract)
                return false;

            var mouse = Mouse.current;

            if (mouse == null)
                return false;

            return mouseButtonToInteract switch
            {
                InteractionMouseButton.Right => mouse.rightButton.isPressed,
                InteractionMouseButton.Middle => mouse.middleButton.isPressed,
                _ => mouse.leftButton.isPressed,
            };
        }

        protected virtual bool WasInteractionMouseButtonPressedThisFrame()
        {
            if (!allowMouseClickInteract)
                return false;

            var mouse = Mouse.current;

            if (mouse == null)
                return false;

            return mouseButtonToInteract switch
            {
                InteractionMouseButton.Right => mouse.rightButton.wasPressedThisFrame,
                InteractionMouseButton.Middle => mouse.middleButton.wasPressedThisFrame,
                _ => mouse.leftButton.wasPressedThisFrame,
            };
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
            var targetTransform = playerTransform;

            if (!targetTransform)
                return;

            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(targetTransform.position, interactionDistance);
        }
#endif
    }
}
