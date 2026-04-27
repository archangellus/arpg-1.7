using System.Collections.Generic;
using UnityEngine;

namespace PLAYERTWO.ARPGProject
{
    [RequireComponent(typeof(Collider))]
    [AddComponentMenu("PLAYER TWO/ARPG Project/Misc/Projectile")]
    public class Projectile : MonoBehaviour
    {
        [Header("Movement Settings")]
        [Tooltip("The maximum distance this Projectile can reach.")]
        public float maxDistance = 15f;

        [Tooltip("The speed at which this Projectile moves.")]
        public float speed = 15f;

        [Tooltip(
            "If true, the Projectile will be destroyed upon impact with"
                + "solid objects (e.g., walls, terrain, etc.)."
        )]
        public bool destroyOnImpact = true;

        [Header("Ground Settings")]
        [Tooltip("If true, the Projectile will adjust its position relative to the ground.")]
        public bool adjustToGround = true;

        [Tooltip(
            "The minimum distance this Projectile can reach from the ground to start adjusting its position."
        )]
        public float minimumGroundDistance = 1f;

        [Tooltip(
            "The speed at which the Projectile adjusts its position downwards when it exceeds the minimum ground distance."
        )]
        public float downwardAdjustmentSpeed = 10f;

        [Tooltip("Ground layer mask for the Projectile to adjust its position.")]
        public LayerMask groundLayer = ~0;

        protected List<DamageLayer> m_damageLayers;
        protected bool m_critical;
        protected List<string> m_targets;
        protected EntityEffect[] m_targetEffects;
        protected float m_targetEffectChance = 1f;

        protected Vector3 m_origin;

        protected Entity m_entity;
        protected Entity m_otherEntity;
        protected Destructible m_destructible;

        protected Collider m_collider;
        protected Rigidbody m_rigidbody;

        /// <summary>
        /// Sets the damage data for this Projectile.
        /// </summary>
        /// <param name="entity">The Entity casting this Projectile.</param>
        /// <param name="layers">The per-type damage layers for this Projectile.</param>
        /// <param name="critical">If true, the Projectile damage will be considered critical.</param>
        /// <param name="targets">The list of targets' tags for this Projectile to interact with.</param>
        public virtual void SetDamage(
            Entity entity,
            List<DamageLayer> layers,
            bool critical,
            List<string> targets
        )
        {
            m_entity = entity;
            m_damageLayers = layers;
            m_critical = critical;
            m_targets = new List<string>(targets);
        }

        /// <summary>
        /// Sets the effects and their application chance to apply to entities hit by this Projectile.
        /// </summary>
        /// <param name="effects">The effect assets to apply on hit.</param>
        /// <param name="chance">Probability (0 to 1) of applying all effects.</param>
        public virtual void SetEffect(EntityEffect[] effects, float chance)
        {
            m_targetEffects = effects;
            m_targetEffectChance = chance;
        }

        protected virtual void Start()
        {
            InitializeCollider();
            InitializeRigidbody();
        }

        protected virtual void Update()
        {
            HandleMovement();
            HandleDistanceCulling();
            HandleGroundDistance();
        }

        protected virtual void OnEnable() => m_origin = transform.position;

        protected virtual void OnTriggerEnter(Collider other)
        {
            HandleEntityAttack(other);
            HandleDestructibleAttack(other);
            HandleImpact(other);
        }

        protected virtual void InitializeCollider()
        {
            m_collider = GetComponent<Collider>();
            m_collider.isTrigger = true;
        }

        protected virtual void InitializeRigidbody()
        {
            if (!TryGetComponent(out m_rigidbody))
                m_rigidbody = gameObject.AddComponent<Rigidbody>();

            m_rigidbody.isKinematic = true;
        }

        protected virtual void HandleEntityAttack(Collider other)
        {
            if (!other.InTagList(m_targets) || !other.TryGetComponent(out m_otherEntity))
                return;

            gameObject.SetActive(false);
            m_otherEntity.Damage(
                m_entity,
                new EntityDamageInfo(
                    m_damageLayers,
                    m_critical,
                    m_targetEffects,
                    m_targetEffectChance
                )
            );
            Destroy(gameObject);
        }

        protected virtual void HandleDestructibleAttack(Collider other)
        {
            if (
                !m_entity.IsPlayer()
                || !other.IsDestructible()
                || !other.TryGetComponent(out m_destructible)
            )
                return;

            var totalDamage = 0;

            foreach (var layer in m_damageLayers)
                totalDamage += layer.amount;

            m_destructible.Damage(totalDamage);
            Destroy(gameObject);
        }

        protected virtual void HandleImpact(Collider other)
        {
            if (!other.isTrigger && other.gameObject != m_entity.gameObject && destroyOnImpact)
                Destroy(gameObject);
        }

        protected virtual void HandleMovement() =>
            transform.position += speed * Time.deltaTime * transform.forward;

        protected virtual void HandleDistanceCulling()
        {
            if (Vector3.Distance(m_origin, transform.position) >= maxDistance)
                Destroy(gameObject);
        }

        protected virtual void HandleGroundDistance()
        {
            if (!adjustToGround)
                return;

            var start = transform.position;
            var end = start + Vector3.down * minimumGroundDistance;

            if (
                Physics.Linecast(
                    start,
                    end,
                    out var hit,
                    groundLayer,
                    QueryTriggerInteraction.Ignore
                )
            )
            {
                var safeDistance = Vector3.Distance(hit.point, end);
                transform.position += Vector3.up * safeDistance;
            }
            else
                transform.position -= downwardAdjustmentSpeed * Time.deltaTime * Vector3.up;
        }
    }
}
