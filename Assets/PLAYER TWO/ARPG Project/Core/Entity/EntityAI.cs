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
            m_originalMoveSpeed = m_entity.moveSpeed;
        }

        protected virtual void InitializeCallback()
        {
            m_entity.onDamage.AddListener(OnDamage);
            m_entity.onDie.AddListener(OnDie);
        }

        protected virtual void InitializeLeader()
        {
            if (!leader)
                return;

            if (dieWithLeader)
                leader.onDie.AddListener(() => m_entity.Die());
        }

        protected virtual void RegisterAI() => LevelAIManager.instance.RegisterAI(this);

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

        /// <summary>
        /// Returns true if the AI should use a skill for the next attack.
        /// When the configured skill is on cooldown (or otherwise unavailable),
        /// the AI falls back to the basic attack automatically.
        /// </summary>
        protected virtual bool ShouldUseSkill() => useSkill && m_entity.skills && m_entity.skills.CanUseSkill();

        /// <summary>
        /// Updates the Entity attack mode for this frame based on skill availability.
        /// </summary>
        protected virtual void UpdateAttackMode() => m_entity.useSkill = ShouldUseSkill();

        /// <summary>
        /// Returns the duration of the currently started attack.
        /// </summary>
        protected virtual float GetCurrentAttackDuration() => m_entity.useSkill ? m_entity.skillDuration : m_entity.attackDuration;

        protected virtual void HandleAttack()
        {
            if (!m_entity.target || m_entity.isAttacking || !canMove)
                return;

            UpdateAttackMode();

            if (m_entity.IsCloseToAttackTarget())
            {
                if (Time.time - m_lastAttackTime > attackCoolDown)
                {
                    m_entity.Attack();

                    if (m_entity.isAttacking)
                        m_lastAttackTime = Time.time + GetCurrentAttackDuration();

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
            m_entity.moveSpeed =
                distanceFromLeader > 1f ? m_originalMoveSpeed : leader.moveSpeed - 0.1f;
            m_entity.MoveTo(destination);
        }

        protected virtual void OnDamage(int amount, Vector3 source, bool critical)
        {
            if (m_entity.target)
                return;

            if (m_entity.GetDistanceTo(source) < spotRadius)
            {
                SearchTarget(true);
            }
            else if (searchDamageSource)
            {
                StopAllCoroutines();
                StartCoroutine(SearchDamageSourceRoutine(source));
            }
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
