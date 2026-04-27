using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace PLAYERTWO.ARPGProject
{
    [RequireComponent(typeof(Entity))]
    [AddComponentMenu("PLAYER TWO/ARPG Project/Entity/Entity AI")]
    public class EntityAI : MonoBehaviour
    {
        [Header("General Settings")]
        [Tooltip("If true, the Entity will ignore the culling system.")]
        public bool ignoreCulling;

        [Header("Detection Settings")]
        [Tooltip("The tags of the Game Objects that this Entity will identify as its target.")]
        public List<string> targetTags;

        [Tooltip("The layers of the colliders that this Entity will search for its target.")]
        public LayerMask targetLayers = ~0;

        [Tooltip("The minimum radius to spot a new target.")]
        public float spotRadius = 5f;

        [Tooltip("The minimum distance to space from the Entity view sight after being detected.")]
        public float fleeRadius = 10f;

        [Header("Attack Settings")]
        [Tooltip("If true, the Entity will always attack with the current Skill.")]
        public bool useSkill;

        [Tooltip("A delay in seconds before the Entity starts patrolling.")]
        public float resetMoveDelay = 1f;

        [Tooltip("A delay in seconds between attacks.")]
        public float attackCoolDown = 0.5f;

        [Tooltip("If true, this Entity will add itself to the attackers list of the target.")]
        public bool addToAttackersList = true;

        [Tooltip(
            "If true, this Entity will not attack another if the maximum "
                + "number of attackers has already attacked that."
        )]
        public bool limitSimultaneousAttackers = true;

        [Tooltip(
            "If the maximum number of attackers attacks the target, this Entity "
                + "will not attack it. This value is ignored if Limit Simultaneous Attackers is off."
        )]
        public int maxSimultaneousAttackers = 2;

        [Header("Combo Settings")]
        [Tooltip("If true, the Entity will attempt to perform combo attacks.")]
        public bool canUseCombo;

        [Tooltip("The probability (0 to 1) that a combo chain starts when an attack is triggered.")]
        [Range(0f, 1f)]
        public float comboChance = 0.5f;

        [Tooltip(
            "The total number of attacks to perform in a combo chain. This takes priority over the Entity's stats Max Combos setting."
        )]
        [Min(1)]
        public int maxCombos = 2;

        [Header("Search Settings")]
        [Tooltip("If true, this Entity will search the origin of the last damage it take.")]
        public bool searchDamageSource = true;

        [Tooltip("A delay in seconds before starting to look to the last damage origin.")]
        public float searchDamageSourceDelay = 0.3f;

        [Tooltip("The duration in seconds in which this Entity will search for the damage origin.")]
        public float searchDamageSourceDuration = 5f;

        [Header("Companion Settings")]
        [Tooltip("The leader of this AI. It's better to set this from code.")]
        public Entity leader;

        [Tooltip("If true, this AI will try to stay close to its leader.")]
        public bool followLeader;

        [Tooltip("If true, this AI will die with its associated leader.")]
        public bool dieWithLeader = true;

        protected Entity m_entity;
        protected Camera m_camera;

        protected int m_totalTargetsInSight;
        protected float m_lastAttackTime;
        protected float m_waitingToSearchTime;
        protected float m_nextTargetRefreshTime;
        protected float m_originalMoveSpeed;
        protected bool m_waitingToSearch;
        protected bool m_isPerformingCombo;

        protected WaitForSeconds m_resetMoveDelay;
        protected WaitForSeconds m_searchDamageSourceDuration;

        protected const float k_targetRefreshRate = 0.2f;

        /// <summary>
        /// And offset applied to the leader following target position.
        /// </summary>
        public Vector3 leaderOffset { get; set; }

        /// <summary>
        /// Returns the bounding box of this Entity.
        /// </summary>
        public virtual Bounds bounds => m_entity.controller.bounds;

        /// <summary>
        /// Returns true if this Entity is dead.
        /// </summary>
        public virtual bool isDead => m_entity.isDead;

        /// <summary>
        /// Returns true if the Entity is able to move.
        /// </summary>
        public virtual bool canMove => !m_entity.isBlocking && !m_entity.isStunned;

        protected virtual void InitializeWaits()
        {
            m_resetMoveDelay = new WaitForSeconds(resetMoveDelay);
            m_searchDamageSourceDuration = new WaitForSeconds(searchDamageSourceDuration);
        }

        protected virtual void InitializeCamera() => m_camera = Camera.main;

        protected virtual void InitializeEntity()
        {
            m_entity = GetComponent<Entity>();
            m_entity.states.ChangeTo<RandomMovementEntityState>();
            m_entity.useSkill = useSkill;
            m_entity.targetTags = targetTags;
            m_originalMoveSpeed = m_entity.stats.moveSpeed;
        }

        protected virtual void InitializeCallback()
        {
            m_entity.onDamage.AddListener(OnDamage);
            m_entity.onBlock.AddListener(OnBlock);
            m_entity.onDie.AddListener(OnDie);
        }

        protected virtual void InitializeLeader()
        {
            if (!leader)
                return;

            if (dieWithLeader)
                leader.onDie.AddListener(() => m_entity.Die());
        }

        /// <summary>
        /// Configures the Entity's hitbox to participate in the combo system
        /// when combo usage is enabled for this AI.
        /// </summary>
        protected virtual void InitializeCombo()
        {
            if (!canUseCombo || !m_entity.hitbox)
                return;

            m_entity.hitbox.incrementCombo = true;
            m_entity.onIncrementCombo.AddListener(OnComboIncrement);
        }

        protected virtual void RegisterAI() => LevelAIManager.instance.RegisterAI(this);

        /// <summary>
        /// Called when the Entity increments its combo index. Starts a combo attack
        /// coroutine if this AI decided to perform a combo.
        /// </summary>
        protected virtual void OnComboIncrement()
        {
            if (!m_isPerformingCombo)
                return;

            if (m_entity.comboIndex >= maxCombos)
            {
                m_isPerformingCombo = false;
                m_entity.CancelCombo();
                return;
            }

            StartCoroutine(PerformComboAttackRoutine());
        }

        /// <summary>
        /// Waits for the combo's next-attack delay then performs the follow-up attack.
        /// </summary>
        protected virtual IEnumerator PerformComboAttackRoutine()
        {
            yield return new WaitForSeconds(m_entity.stats.nextComboDelay);

            if (!m_entity.target || !m_isPerformingCombo || !m_entity.isPerformingCombo)
                yield break;

            m_lastAttackTime = Time.time + m_entity.attackDuration;
            m_entity.Attack();

            if (!m_entity.target || (m_entity.targetEntity && m_entity.targetEntity.isDead))
                StopAttack();
        }

        protected virtual void HandleViewSight()
        {
            if (m_entity.target || Time.time < m_nextTargetRefreshTime)
                return;

            m_nextTargetRefreshTime = Time.time + k_targetRefreshRate;
            SearchTarget();
        }

        protected virtual void SearchTarget(bool ignoreSimultaneousAttacks = false)
        {
            if (m_entity.target)
                return;

            m_totalTargetsInSight = Physics.OverlapSphereNonAlloc(
                transform.position,
                spotRadius,
                LevelAIManager.SightBuffer,
                targetLayers
            );

            for (int i = 0; i < m_totalTargetsInSight; i++)
            {
                if (!LevelAIManager.SightBuffer[i].InTagList(targetTags))
                    continue;

                SpotTarget(
                    LevelAIManager.SightBuffer[i].GetComponent<Entity>(),
                    ignoreSimultaneousAttacks
                );
            }
        }

        /// <summary>
        /// Assign a target to the Entity and handles its attackers list.
        /// </summary>
        /// <param name="target">The target you want to assign.</param>
        /// <param name="ignoreSimultaneousAttacks">If true, the Entity will attack the
        /// target even if the maximum number of attackers has already attacked it.</param>
        public virtual void SpotTarget(Entity target, bool ignoreSimultaneousAttacks = false)
        {
            if (
                !ignoreSimultaneousAttacks
                && limitSimultaneousAttackers
                && target.attackedBy.Count >= maxSimultaneousAttackers
            )
                return;

            StopAllCoroutines();
            m_entity.SetTarget(target.transform);

            if (m_entity.targetEntity && addToAttackersList)
                m_entity.targetEntity.attackedBy.Add(m_entity);
        }

        protected virtual void HandleTargetFlee()
        {
            if (!m_entity.target)
                return;

            if (m_entity.GetDistanceToTarget() > fleeRadius)
                StopAttack();
        }

        protected virtual void HandleAttack()
        {
            if (!m_entity.target || m_entity.isAttacking || !canMove)
                return;

            if (m_entity.IsCloseToAttackTarget())
            {
                if (Time.time - m_lastAttackTime > attackCoolDown)
                {
                    m_isPerformingCombo = canUseCombo && Random.value <= comboChance;
                    m_lastAttackTime =
                        Time.time
                        + (canUseCombo ? m_entity.attackDuration : m_entity.skillDuration);
                    m_entity.Attack();

                    if (!m_entity.target || (m_entity.targetEntity && m_entity.targetEntity.isDead))
                        StopAttack();
                }
                else
                {
                    m_entity.StandStill();
                }
            }
            else
            {
                m_entity.MoveToTarget();
            }
        }

        protected virtual void HandleFollowing()
        {
            if (!followLeader || !leader || m_entity.target || m_entity.isAttacking || !canMove)
                return;

            var destination = leader.position + leaderOffset;
            var distanceFromLeader = Vector3.Distance(transform.position, destination);
            m_entity.stats.moveSpeed =
                distanceFromLeader > 1f ? m_originalMoveSpeed : leader.stats.moveSpeed - 0.1f;
            m_entity.MoveTo(destination);
        }

        protected virtual void HandleDamageSource(EntityDamageInfo info)
        {
            if (m_entity.target || info.damageMode != DamageMode.Active)
                return;

            if (m_entity.GetDistanceTo(info.sourcePosition) < spotRadius)
            {
                SearchTarget(true);
            }
            else if (searchDamageSource)
            {
                StopAllCoroutines();
                StartCoroutine(SearchDamageSourceRoutine(info.sourcePosition));
            }
        }

        protected virtual void OnDamage(EntityDamageInfo info) => HandleDamageSource(info);

        protected virtual void OnBlock(EntityDamageInfo info)
        {
            StopAllCoroutines();
            StartCoroutine(OnBlockRoutine(info));
        }

        protected virtual IEnumerator OnBlockRoutine(EntityDamageInfo info)
        {
            yield return new WaitForSeconds(m_entity.blockDuration);
            HandleDamageSource(info);
        }

        protected virtual void OnDie()
        {
            StopAllCoroutines();
            LoseTarget();
        }

        /// <summary>
        /// Makes the Entity stop attacking its assigned target.
        /// </summary>
        public virtual void StopAttack()
        {
            StopAllCoroutines();
            StartCoroutine(StopAttackRoutine());
        }

        /// <summary>
        /// Removes the target of the Entity.
        /// </summary>
        public virtual void LoseTarget()
        {
            if (m_entity.targetEntity)
                m_entity.targetEntity.attackedBy.Remove(m_entity);

            m_entity.SetTarget(null);
        }

        protected virtual IEnumerator StopAttackRoutine()
        {
            LoseTarget();
            m_entity.StandStill();
            yield return m_resetMoveDelay;
            m_entity.StartRandomMovement();
        }

        protected virtual IEnumerator SearchDamageSourceRoutine(Vector3 source)
        {
            m_entity.StandStill();

            if (!m_waitingToSearch)
            {
                m_waitingToSearch = true;
                m_waitingToSearchTime = Time.time;
            }

            while (Time.time - m_waitingToSearchTime < searchDamageSourceDelay)
            {
                yield return null;
            }

            m_entity.MoveTo(source);
            yield return m_searchDamageSourceDuration;
            m_entity.StartRandomMovement();
            m_waitingToSearch = false;
        }

        protected virtual bool CanUpdateAI()
        {
            if (this == null || m_entity == null)
                return false;

            return gameObject.activeSelf && m_entity.enabled && m_entity.isActive;
        }

        public virtual void AIUpdate()
        {
            if (CanUpdateAI())
            {
                HandleViewSight();
                HandleTargetFlee();
            }
        }

        protected virtual void Start()
        {
            InitializeWaits();
            InitializeCamera();
            InitializeEntity();
            InitializeCallback();
            InitializeLeader();
            InitializeCombo();
            RegisterAI();
        }

        protected virtual void Update()
        {
            if (CanUpdateAI())
            {
                HandleAttack();
                HandleFollowing();
            }
        }
    }
}
