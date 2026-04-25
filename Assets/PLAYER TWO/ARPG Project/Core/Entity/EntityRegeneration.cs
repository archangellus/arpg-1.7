using UnityEngine;

namespace PLAYERTWO.ARPGProject
{
    [AddComponentMenu("PLAYER TWO/ARPG Project/Entity/Entity Regeneration")]
    public class EntityRegeneration : MonoBehaviour
    {
        [Header("Regeneration Settings")]
        [Tooltip(
            "The delay in seconds after taking damage or consuming health before health regeneration resumes, and after consuming mana before mana regeneration resumes."
        )]
        public float regenDelay = 10f;

        [Tooltip(
            "The interval in seconds between each regeneration tick for both health and mana."
        )]
        public float regenInterval = 1.5f;

        [Tooltip(
            "The number of vitality or energy points required to restore 1 HP or MP per tick."
        )]
        public int statPointsPerRegen = 20;

        protected Entity m_entity;
        protected float m_lastDamageTime = float.NegativeInfinity;
        protected float m_lastManaUseTime = float.NegativeInfinity;
        protected float m_lastHealthRegenTime;
        protected float m_lastManaRegenTime;

        protected virtual void Start()
        {
            m_entity = GetComponent<Entity>();
            m_entity.onDamage.AddListener(OnDamage);

            if (m_entity.skills)
            {
                m_entity.skills.onHealthConsumed.AddListener(OnHealthConsumed);
                m_entity.skills.onManaConsumed.AddListener(OnManaConsumed);
            }
        }

        protected virtual void OnDamage(EntityDamageInfo info)
        {
            m_lastDamageTime = Time.time;
        }

        protected virtual void OnHealthConsumed()
        {
            m_lastDamageTime = Time.time;
        }

        protected virtual void OnManaConsumed()
        {
            m_lastManaUseTime = Time.time;
        }

        protected virtual void Update()
        {
            if (m_entity.isDead)
                return;

            HandleHealthRegen();
            HandleManaRegen();
        }

        protected virtual void HandleHealthRegen()
        {
            if (m_entity.stats.health >= m_entity.stats.maxHealth)
                return;

            if (Time.time < m_lastDamageTime + regenDelay)
                return;

            if (Time.time >= m_lastHealthRegenTime + regenInterval)
            {
                m_entity.stats.health += CalculateHealthRegenAmount();
                m_lastHealthRegenTime = Time.time;
            }
        }

        protected virtual void HandleManaRegen()
        {
            if (m_entity.stats.mana >= m_entity.stats.maxMana)
                return;

            if (Time.time < m_lastManaUseTime + regenDelay)
                return;

            if (Time.time >= m_lastManaRegenTime + regenInterval)
            {
                m_entity.stats.mana += CalculateManaRegenAmount();
                m_lastManaRegenTime = Time.time;
            }
        }

        /// <summary>
        /// Calculates the health points restored per tick, including any flat bonus
        /// from the <see cref="ItemAttributes.AttributeType.HealthRegeneration"/> attribute on equipped items.
        /// </summary>
        protected virtual int CalculateHealthRegenAmount()
        {
            var bonus = GetItemRegenBonus(ItemAttributes.AttributeType.HealthRegeneration);
            return Mathf.Max(1, CalculateRegenAmount(m_entity.stats.vitality) + bonus);
        }

        /// <summary>
        /// Calculates the mana points restored per tick, including any flat bonus
        /// from the <see cref="ItemAttributes.AttributeType.ManaRegeneration"/> attribute on equipped items.
        /// </summary>
        protected virtual int CalculateManaRegenAmount()
        {
            var bonus = GetItemRegenBonus(ItemAttributes.AttributeType.ManaRegeneration);
            return Mathf.Max(1, CalculateRegenAmount(m_entity.stats.energy) + bonus);
        }

        /// <summary>
        /// Calculates the amount restored per tick for a given stat.
        /// Every <see cref="statPointsPerRegen"/> points of stat restores 1 point, with a minimum of 1.
        /// </summary>
        protected virtual int CalculateRegenAmount(int stat)
        {
            return Mathf.Max(1, stat / statPointsPerRegen);
        }

        /// <summary>
        /// Returns the flat regeneration bonus from equipped items for the given attribute type.
        /// Returns 0 if the entity has no item manager.
        /// </summary>
        protected virtual int GetItemRegenBonus(ItemAttributes.AttributeType type)
        {
            if (m_entity.items == null)
                return 0;

            return m_entity.items.GetFinalAttributes()[type];
        }
    }
}
