using System.Collections.Generic;
using UnityEngine;

namespace PLAYERTWO.ARPGProject
{
    [RequireComponent(typeof(ParticleSystem))]
    [AddComponentMenu("PLAYER TWO/ARPG Project/Skill/Skill Particle")]
    public class SkillParticle : MonoBehaviour
    {
        [Header("Settings")]
        [Tooltip(
            "If true, the particle will automatically be destroyed when the particle system finishes."
        )]
        public bool destroyWhenParticleStop = true;

        [Tooltip(
            "If true, the particle will try to read collisions from the 'On Particle Collision' events."
        )]
        public bool useParticleCollisionEvents;

        [Tooltip("If true, the particle can collide only once with the another Game Object.")]
        public bool collideOnce;

        protected Collider m_collider;
        protected ParticleSystem m_particle;
        protected Entity m_entity;
        protected Entity m_target;
        protected Destructible m_destructible;
        protected Skill m_skill;
        protected EntityEffect[] m_targetEffects;
        protected float m_targetEffectChance = 1f;

        protected List<GameObject> m_targets = new List<GameObject>();
        protected List<ParticleCollisionEvent> m_events = new List<ParticleCollisionEvent>();

        protected virtual void InitializeCollider()
        {
            if (useParticleCollisionEvents)
                return;

            m_collider = GetComponent<Collider>();
            m_collider.isTrigger = true;
        }

        protected virtual void InitializeParticle()
        {
            m_particle = GetComponent<ParticleSystem>();
            var collision = m_particle.collision;
            collision.dampen = 0;
        }

        /// <summary>
        /// Sets the caster Entity and the Skill data.
        /// </summary>
        /// <param name="entity">The Entity of the caster.</param>
        /// <param name="skill">The Skill data.</param>
        public virtual void SetSkills(Entity entity, Skill skill)
        {
            m_entity = entity;
            m_skill = skill;
        }

        /// <summary>
        /// Sets the effects and their application chance to apply to entities hit by this particle.
        /// </summary>
        /// <param name="effects">The effect assets to apply on hit.</param>
        /// <param name="chance">Probability (0 to 1) of applying all effects.</param>
        public virtual void SetTargetEffect(EntityEffect[] effects, float chance)
        {
            m_targetEffects = effects;
            m_targetEffectChance = chance;
        }

        protected virtual void Start()
        {
            InitializeCollider();
            InitializeParticle();
        }

        protected virtual void LateUpdate()
        {
            if (!destroyWhenParticleStop || !m_particle.isStopped)
                return;

            Destroy(gameObject);
        }

        protected virtual void OnEnable() => m_targets.Clear();

        protected virtual bool ValidCollision(GameObject other) =>
            (other.transform.InTagList(m_entity.targetTags) || other.transform.IsDestructible())
            && (!collideOnce || !m_targets.Contains(other));

        protected virtual void HandleEntityAttack(GameObject other, int damage, bool critical)
        {
            if (!other.TryGetComponent(out m_target))
                return;

            var damageType = m_skill is SkillAttack skillAttack
                ? skillAttack.damageType
                : DamageType.Normal;
            var skillLayers = new List<DamageLayer> { new DamageLayer(damageType, damage) };

            m_target.Damage(
                m_entity,
                new EntityDamageInfo(skillLayers, critical, m_targetEffects, m_targetEffectChance)
            );
        }

        protected virtual void HandleDestructibleAttack(GameObject other, int damage)
        {
            if (
                m_entity.IsPlayer()
                && other.transform.IsDestructible()
                && other.TryGetComponent(out m_destructible)
            )
                m_destructible.Damage(damage);
        }

        protected virtual void HandleAttack(GameObject other, int damage, bool critical)
        {
            HandleEntityAttack(other, damage, critical);
            HandleDestructibleAttack(other, damage);
        }

        protected virtual void OnTriggerStay(Collider other)
        {
            if (!ValidCollision(other.gameObject))
                return;

            m_targets.Add(other.gameObject);

            var damage = m_entity.stats.GetSkillDamage(m_skill, out var critical);

            HandleAttack(other.gameObject, damage, critical);
        }

        protected virtual void OnParticleCollision(GameObject other)
        {
            if (!ValidCollision(other))
                return;

            var collisions = m_particle.GetCollisionEvents(other, m_events);
            var damage = m_entity.stats.GetSkillDamage(m_skill, out var critical);

            for (int i = 0; i < collisions; i++)
            {
                m_targets.Add(other);
                HandleAttack(other, damage, critical);
            }
        }
    }
}
