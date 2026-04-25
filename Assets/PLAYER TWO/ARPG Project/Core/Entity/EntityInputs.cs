using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace PLAYERTWO.ARPGProject
{
    [AddComponentMenu("PLAYER TWO/ARPG Project/Entity/Entity Inputs")]
    public partial class EntityInputs : MonoBehaviour
    {
        public enum SkillActionBehavior
        {
            Equip,
            Use,
        }

        public enum MovementMode
        {
            Directional,
            PointAndClick,
        }

        [Tooltip(
            "The Input Action Asset from the New Input System containing all the possible actions."
        )]
        public InputActionAsset actions;

        [Tooltip("The movement mode for this Entity.")]
        public MovementMode movementMode = MovementMode.PointAndClick;

        [Tooltip("How the skill action should behave when a skill is selected.")]
        public SkillActionBehavior skillActionBehavior = SkillActionBehavior.Equip;

        [Tooltip("A particle system that plays when setting a destination.")]
        public ParticleSystem destinationEffect;

        [Header("Hold Target Settings")]
        [Tooltip(
            "Dot product threshold below which the cursor is considered pointing away from the "
                + "locked target, releasing the hold-attack. 0 = 90°, -0.5 = 120°."
        )]
        [Range(-1f, 1f)]
        public float holdTargetDotThreshold = 0f;

        [Tooltip(
            "Duration in seconds to lock movement after an attack target dies or an interactive "
                + "is triggered, giving the player time to release the button."
        )]
        public float postActionMoveLockDuration = 0.5f;

        protected Entity m_entity;
        protected Transform m_target;
        protected Entity m_targetEntity;
        protected Highlighter m_highlighter;
        protected Interactive m_interactive;
        protected Camera m_camera;
        protected ParticleSystem m_destinationEffect;

        protected bool m_holdMove;
        protected bool m_holdAttack;
        protected bool m_holdSkill;
        protected bool m_attackMode;
        protected bool m_pointerOverUi;
        protected bool m_setDestinationHeld;

        protected float m_lockDirectionTime;
        protected float m_postActionMoveLockTime;

        protected Interactive m_pendingInteractive;

        protected RaycastHit[] m_hitResults = new RaycastHit[32];

        public new Camera camera
        {
            get
            {
                if (m_camera == null)
                    m_camera = Camera.main;

                return m_camera;
            }
        }

        public bool moveDirectionLocked => m_lockDirectionTime > 0;

        protected virtual GamePause m_gamePause => GamePause.instance;

        protected EntityAreaScanner m_areaScanner;

        protected virtual void InitializeEntity() => m_entity = GetComponent<Entity>();

        protected virtual void InitializeAreaScanner() =>
            m_areaScanner = GetComponent<EntityAreaScanner>();

        protected virtual void InitializeCallbacks()
        {
            m_entity.onDie.AddListener(() =>
            {
                enabled = false;
                m_target = null;
            });
        }

        protected virtual void InitializeDestinationEffect()
        {
            if (destinationEffect)
            {
                m_destinationEffect = Instantiate(destinationEffect);
                m_destinationEffect.Stop();
            }
        }

        protected virtual void InitializeConsoleCallbacks()
        {
            Console.instance.onConsoleOpened.AddListener(() => actions.Disable());
            Console.instance.onConsoleClosed.AddListener(() => actions.Enable());
        }

        protected virtual void InitializeMovementMode()
        {
#if UNITY_ANDROID || UNITY_IOS
            movementMode = MovementMode.Directional;
#endif
        }

        public virtual bool MouseRaycast(
            out RaycastHit closestHit,
            int layer = Physics.DefaultRaycastLayers
        )
        {
            closestHit = new RaycastHit();

            if (m_pointerOverUi)
                return false;

            var position = GetPointerPosition();
            var ray = camera.ScreenPointToRay(position);
            var hits = Physics.RaycastNonAlloc(ray, m_hitResults, Mathf.Infinity, layer);
            var closestPoint = hits > 0 ? m_hitResults[0].point : Vector3.zero;

            for (int i = 0; i < hits; i++)
            {
                if (m_hitResults[i].transform == transform)
                    continue;
                else if (m_hitResults[i].collider.IsCollectible())
                {
                    closestHit = m_hitResults[i];
                    break;
                }
                else if (
                    closestHit.collider == null
                    || Vector3.Distance(ray.origin, m_hitResults[i].point)
                        <= Vector3.Distance(ray.origin, closestPoint)
                )
                {
                    closestHit = m_hitResults[i];
                    closestPoint = m_hitResults[i].point;

                    if (!closestHit.collider.IsUntagged())
                        closestHit.point = closestHit.transform.position;
                }
            }

            return closestHit.collider != null;
        }

        protected virtual void SetTarget(Transform target)
        {
            m_target = target;
            m_targetEntity = null;

            if (target.IsEntity())
                m_targetEntity = target.GetComponent<Entity>();
        }

        protected virtual bool TrySetTarget(Collider collider)
        {
            if (collider.IsUntagged())
                return false;

            SetTarget(collider.transform);
            return true;
        }

        protected virtual bool TryRefreshTarget()
        {
#if UNITY_STANDALONE || UNITY_WEBGL
            return MouseRaycast(out var hit) && TrySetTarget(hit.collider);
#else
            SetTarget(m_areaScanner.GetClosestTarget());
            return m_target;
#endif
        }

        protected virtual bool IsTargetAttackable() => m_target.IsTarget();

        protected virtual bool IsTargetInteractive() => m_target.IsInteractive();

        protected virtual Vector3 GetMouseDirection()
        {
            var screenPosition = GetPointerPosition();
            var ray = camera.ScreenPointToRay(screenPosition);
            var groundPlane = new Plane(Vector3.up, transform.position);

            if (groundPlane.Raycast(ray, out float distance))
            {
                var hitPoint = ray.GetPoint(distance);
                var direction = (hitPoint - transform.position).normalized;
                return new Vector3(direction.x, 0, direction.z);
            }

            return transform.forward;
        }

        protected virtual void OnSetDestination(InputAction.CallbackContext _)
        {
#if UNITY_STANDALONE || UNITY_WEBGL
            if (m_gamePause.isPaused || (!m_entity.canMove && m_entity.comboIndex == 0))
                return;

            if (m_entity.states.IsCurrent<UseSkillEntityState>())
                return;

            m_setDestinationHeld = true;
            m_target = null;
            m_entity.useSkill = false;

            if (MouseRaycast(out var hit))
            {
                var foundTarget = TrySetTarget(hit.collider);

                if (IsTargetAttackable())
                {
                    m_holdAttack = true;
                    return;
                }
                else if (IsTargetInteractive())
                {
                    m_pendingInteractive = m_target.GetComponent<Interactive>();
                    m_entity.targetInteractive = m_pendingInteractive;
                    m_entity.MoveTo(m_target.position);
                    return;
                }
                else if (movementMode != MovementMode.PointAndClick)
                    return;

                m_holdMove = true;
                ShowDestinationEffect(hit.point);
            }
#endif
        }

        protected virtual void OnSetDestinationCancelled(InputAction.CallbackContext _)
        {
            m_holdMove = m_holdAttack = m_setDestinationHeld = false;
            m_postActionMoveLockTime = 0;
            m_pendingInteractive = null;
        }

        protected virtual void OnDirectionalMovement(InputAction.CallbackContext _) =>
            m_target = null;

        protected virtual void OnSkill(InputAction.CallbackContext _)
        {
            if (
                m_gamePause.isPaused
                || !m_entity.skills.CanUseSkill()
                || m_entity.states.IsCurrent<UseSkillEntityState>()
                || m_entity.states.IsCurrent<AttackEntityState>()
                || m_holdMove
                || GetMoveDirection().sqrMagnitude > 0
            )
                return;

            m_entity.useSkill = true;

#if UNITY_STANDALONE || UNITY_WEBGL
            if (MouseRaycast(out var hit))
            {
                m_holdSkill = true;
                TrySetTarget(hit.collider);
            }
#else
            m_holdSkill = true;
#endif
        }

        protected virtual void OnSkillCancelled(InputAction.CallbackContext _) =>
            m_holdSkill = false;

        protected virtual void OnAttackMode(InputAction.CallbackContext _) => m_attackMode = true;

        protected virtual void OnAttackModeCancelled(InputAction.CallbackContext _) =>
            m_attackMode = false;

        protected virtual void OnConsumeItem0(InputAction.CallbackContext _) =>
            m_entity.items.ConsumeItem(0);

        protected virtual void OnConsumeItem1(InputAction.CallbackContext _) =>
            m_entity.items.ConsumeItem(1);

        protected virtual void OnConsumeItem2(InputAction.CallbackContext _) =>
            m_entity.items.ConsumeItem(2);

        protected virtual void OnConsumeItem3(InputAction.CallbackContext _) =>
            m_entity.items.ConsumeItem(3);

        protected virtual void OnSelectSkill0(InputAction.CallbackContext _) => EquipAndUseSkill(0);

        protected virtual void OnSelectSkill1(InputAction.CallbackContext _) => EquipAndUseSkill(1);

        protected virtual void OnSelectSkill2(InputAction.CallbackContext _) => EquipAndUseSkill(2);

        protected virtual void OnSelectSkill3(InputAction.CallbackContext _) => EquipAndUseSkill(3);

        protected virtual void OnAttack(InputAction.CallbackContext _)
        {
            m_entity.useSkill = false;
            m_holdAttack = m_attackMode = true;
        }

        void EquipAndUseSkill(int index)
        {
            m_entity.skills.ChangeTo(index);

            if (
                skillActionBehavior == SkillActionBehavior.Equip
                || m_entity.states.IsCurrent<UseSkillEntityState>()
                || m_entity.states.IsCurrent<AttackEntityState>()
                || m_holdMove
                || GetMoveDirection().sqrMagnitude > 0
            )
                return;

            m_entity.useSkill = true;

            if (m_target)
            {
                m_entity.SetTarget(m_target);
                m_entity.lookDirection = m_entity.GetDirectionToTarget();
                m_entity.Attack();
            }
            else
            {
                m_entity.lookDirection = GetMouseDirection();
                m_entity.FreeAttack();
            }
        }

        protected virtual void OnAttackCancelled(InputAction.CallbackContext _) =>
            m_holdAttack = m_attackMode = false;

        protected virtual void OnInteract(InputAction.CallbackContext _)
        {
            m_interactive = m_areaScanner.GetClosestInteractiveObject();

            if (m_interactive)
            {
                m_entity.targetInteractive = m_interactive;
                m_entity.MoveTo(m_interactive.transform.position);
            }
        }

        protected virtual void HandlePointer() =>
            m_pointerOverUi = EventSystem.current.IsPointerOverGameObject();

        protected virtual void HandleMovement()
        {
#if UNITY_STANDALONE || UNITY_WEBGL
            if (m_holdMove && m_entity.canMove)
            {
                m_entity.lookDirection = GetMouseDirection();

                if (m_attackMode)
                {
                    m_entity.FreeAttack();
                }
                else if (MouseRaycast(out var hit))
                {
                    var foundTarget = TrySetTarget(hit.collider);

                    if (foundTarget && IsTargetAttackable())
                    {
                        m_holdMove = false;
                        m_holdAttack = true;
                    }
                    else if (foundTarget && IsTargetInteractive())
                    {
                        m_holdMove = false;
                        m_pendingInteractive = m_target.GetComponent<Interactive>();
                        m_entity.targetInteractive = m_pendingInteractive;
                        m_entity.MoveTo(m_target.position);
                    }
                    else
                    {
                        m_target = null;
                        m_entity.MoveTo(hit.point);
                    }
                }
            }
#endif
        }

        protected virtual void HandleAttack()
        {
            if (!m_holdAttack && !m_holdSkill)
                return;

            if (m_attackMode || m_holdSkill && !m_entity.skills.RequireTarget())
            {
#if UNITY_STANDALONE || UNITY_WEBGL
                m_entity.lookDirection = GetMouseDirection();
                m_entity.FreeAttack();
#else
                if (IsTargetEntityActive() || TryRefreshTarget())
                    m_entity.MoveToAttack(m_target);
                else
                    m_entity.FreeAttack();
#endif
            }
            else if (IsTargetEntityActive() || TryRefreshTarget() && IsTargetAttackable())
            {
                if (!m_attackMode && !IsCursorFacingTarget())
                {
                    m_holdAttack = false;
                    m_holdMove = true;
                    return;
                }

                m_entity.MoveToAttack(m_target);
            }
            else
            {
                m_holdAttack = false;

                if (!m_holdSkill)
                    m_postActionMoveLockTime = postActionMoveLockDuration;
            }
        }

        protected virtual void HandleHighlight()
        {
#if UNITY_STANDALONE || UNITY_WEBGL
            m_highlighter.SafeCall(h => h.SetHighlight(false));

            if (!MouseRaycast(out var hit) || !hit.collider.TryGetComponent(out m_highlighter))
                return;

            if (hit.collider.IsTarget())
                SetTarget(hit.collider.transform);

            m_highlighter.SafeCall(h => h.SetHighlight(true));
#endif
        }

        protected virtual void HandleMoveDirectionUnlock()
        {
            if (!moveDirectionLocked)
                return;

            m_lockDirectionTime -= Time.deltaTime;
            m_lockDirectionTime = Mathf.Max(m_lockDirectionTime, 0);
        }

        protected virtual bool IsTargetEntityActive() => m_targetEntity && !m_targetEntity.isDead;

        protected virtual bool IsCursorFacingTarget() =>
            m_target != null && IsCursorFacingPosition(m_target.position);

        protected virtual bool IsCursorFacingPosition(Vector3 targetPosition)
        {
            var toTarget = targetPosition - transform.position;
            toTarget.y = 0;

            if (toTarget.sqrMagnitude < 0.001f)
                return true;

            return Vector3.Dot(toTarget.normalized, GetMouseDirection()) >= holdTargetDotThreshold;
        }

        protected virtual void HandlePostActionLock()
        {
#if UNITY_STANDALONE || UNITY_WEBGL
            if (m_pendingInteractive != null && m_entity.targetInteractive != null)
            {
                if (!IsCursorFacingPosition(m_pendingInteractive.transform.position))
                {
                    m_entity.targetInteractive = null;
                    m_pendingInteractive = null;
                    m_holdMove = true;
                    return;
                }
            }
#endif

            if (m_pendingInteractive != null && m_entity.targetInteractive == null)
            {
                m_pendingInteractive = null;

                if (m_setDestinationHeld)
                    m_postActionMoveLockTime = postActionMoveLockDuration;
            }

            if (m_postActionMoveLockTime <= 0)
                return;

            m_postActionMoveLockTime -= Time.deltaTime;

            if (m_postActionMoveLockTime <= 0 && m_setDestinationHeld)
                m_holdMove = true;
        }

        public virtual Vector3 GetMoveDirection()
        {
            if (
                moveDirectionLocked
                || !m_entity.canMove
                || movementMode != MovementMode.Directional
            )
                return Vector3.zero;

            var raw = m_directionalMovement.ReadValue<Vector2>();
            var direction = GetAxisWithCrossDeadzone(raw);

            if (direction.sqrMagnitude > 0)
            {
                var rotation = Quaternion.AngleAxis(camera.transform.eulerAngles.y, Vector3.up);
                direction = rotation * direction;
                direction = direction.normalized;
            }

            return direction.normalized;
        }

        /// <summary>
        /// Locks the move direction making the Entity unable to move for a while.
        /// </summary>
        /// <param name="duration">The duration in seconds to keep direction locked.</param>
        public virtual void LockMoveDirection(float duration = 0.5f) =>
            m_lockDirectionTime = duration;

        /// <summary>
        /// Remaps a given axis considering the Input System's default deadzone.
        /// This method uses a cross shape instead of a circle one to evaluate the deadzone range.
        /// </summary>
        /// <param name="axis">The axis you want to remap.</param>
        public virtual Vector3 GetAxisWithCrossDeadzone(Vector2 axis)
        {
            var deadzone = InputSystem.settings.defaultDeadzoneMin;
            axis.x = Mathf.Abs(axis.x) > deadzone ? RemapToDeadzone(axis.x, deadzone) : 0;
            axis.y = Mathf.Abs(axis.y) > deadzone ? RemapToDeadzone(axis.y, deadzone) : 0;
            return new Vector3(axis.x, 0, axis.y);
        }

        /// <summary>
        /// Remaps a value to a 0-1 range considering a given deadzone.
        /// </summary>
        /// <param name="value">The value you wants to remap.</param>
        /// <param name="deadzone">The minimum deadzone value.</param>
        protected float RemapToDeadzone(float value, float deadzone) =>
            Mathf.Sign(value) * ((Mathf.Abs(value) - deadzone) / (1 - deadzone));

        /// <summary>
        /// Returns the current position of the pointer from touch or the mouse.
        /// </summary>
        public static Vector2 GetPointerPosition()
        {
#if (UNITY_ANDROID || UNITY_IOS) && !UNITY_EDITOR
            return Touchscreen.current.primaryTouch.position.ReadValue();
#else
            return Mouse.current.position.ReadValue();
#endif
        }

        /// <summary>
        /// Shows the destination effect at a given position.
        /// </summary>
        /// <param name="position">The position where the effect should be shown.</param>
        protected virtual void ShowDestinationEffect(Vector3 position)
        {
            if (!m_destinationEffect)
                return;

            if (m_destinationEffect.isPlaying)
                m_destinationEffect.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var effectPosition = position + Vector3.up * 0.1f;
            m_destinationEffect.transform.position = effectPosition;
            m_destinationEffect.Play();
        }

        protected virtual void Start()
        {
            InitializeEntity();
            InitializeAreaScanner();
            InitializeActions();
            InitializeCallbacks();
            InitializeActionsCallbacks();
            InitializeDestinationEffect();
            InitializeConsoleCallbacks();
            InitializeMovementMode();
        }

        protected virtual void Update()
        {
            HandlePointer();
            HandleMovement();
            HandleAttack();
            HandlePostActionLock();
            HandleHighlight();
            HandleMoveDirectionUnlock();
        }

        protected virtual void OnEnable() => actions.Enable();

        protected virtual void OnDisable() => actions.Disable();

        protected virtual void OnDestroy() => FinalizeActionCallbacks();
    }
}
