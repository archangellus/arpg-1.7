using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace PLAYERTWO.ARPGProject
{
    [RequireComponent(typeof(Collider))]
    [AddComponentMenu("PLAYER TWO/ARPG Project/Entity/Entity Hitbox")]
    public class EntityHitbox : MonoBehaviour
    {
        [Header("Settings")]
        [Tooltip(
            "If true, the Hitbox will deal area damage and stay active after hitting a target."
        )]
        public bool areaDamage;

        [Tooltip(
            "If true, the Hitbox will increment the combo index of the Entity when it hits a target."
        )]
        public bool incrementCombo;

        [Tooltip("The duration in seconds to disable the Hitbox when it gets toggled.")]
        public float defaultToggleDuration = 0.1f;

        protected int m_damage;
        protected bool m_critical;

        protected Entity m_entity;
        protected Entity m_otherEntity;
        protected Destructible m_destructible;
        protected Collider m_collider;

        protected Dictionary<int, WaitForSeconds> m_waitForToggles = new();

        protected virtual void InitializeEntity() => m_entity = GetComponentInParent<Entity>();

        protected virtual void InitializeCollider()
        {
            m_collider = GetComponent<Collider>();
            m_collider.isTrigger = true;
        }

        /// <summary>
        /// Sets the damage information of this Hitbox.
        /// </summary>
        /// <param name="amount">The amount of damage points.</param>
        /// <param name="critical">Set it to true if the damage is critical.</param>
        public virtual void SetDamage(int amount, bool critical)
        {
            m_damage = amount;
            m_critical = critical;
        }

        /// <summary>
        /// Toggles the Hitbox for a while.
        /// </summary>
        public void Toggle() => Toggle(defaultToggleDuration);

        /// <summary>
        /// Toggles the Hitbox for a given duration in seconds.
        /// </summary>
        /// <param name="duration">The duration in seconds you want the Hitbox to stay activated.</param>
        public void Toggle(float duration)
        {
            var milliseconds = (int)(duration * 1000);

            if (!m_waitForToggles.ContainsKey(milliseconds))
                m_waitForToggles.Add(milliseconds, new WaitForSeconds(duration));

            StartCoroutine(ToggleRoutine(m_waitForToggles[milliseconds]));
        }

        protected virtual void HandleCombo()
        {
            if (incrementCombo && m_entity.stats.maxCombos > 1)
                m_entity.IncrementCombo();
        }

        public IEnumerator ToggleRoutine(WaitForSeconds waitForSeconds)
        {
            m_collider.enabled = true;
            yield return waitForSeconds;
            m_collider.enabled = false;
        }

        protected virtual void HandleEntityAttack(Collider other)
        {
            if (!other.InTagList(m_entity.targetTags) || !other.TryGetComponent(out m_otherEntity))
                return;

            HandleCombo();
            m_otherEntity.Damage(m_entity, m_damage, m_critical);

            if (!areaDamage)
                m_collider.enabled = false;
        }

        protected virtual void HandleDestructibleAttack(Collider other)
        {
            if (
                !m_entity.IsPlayer()
                || !other.IsDestructible()
                || !other.TryGetComponent(out m_destructible)
            )
                return;

            HandleCombo();
            m_destructible.Damage(m_damage);

            if (!areaDamage)
                m_collider.enabled = false;
        }

        protected virtual void Start()
        {
            InitializeEntity();
            InitializeCollider();
        }

        protected virtual void OnTriggerEnter(Collider other)
        {
            if (other.transform == m_entity.transform)
                return;

            HandleEntityAttack(other);
            HandleDestructibleAttack(other);
        }
    }
}
