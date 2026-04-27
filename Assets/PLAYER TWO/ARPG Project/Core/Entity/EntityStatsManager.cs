using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace PLAYERTWO.ARPGProject
{
    [AddComponentMenu("PLAYER TWO/ARPG Project/Entity/Entity Stats Manager")]
    public partial class EntityStatsManager : MonoBehaviour
    {
        public UnityEvent onLevelUp;
        public UnityEvent onRecalculate;
        public UnityEvent onHealthChanged;
        public UnityEvent onManaChanged;
        public UnityEvent onExperienceChanged;

        [Header("General Settings")]
        [Tooltip("The initial level of this Entity.")]
        public int level = 1;

        [Tooltip("The initial strength of this Entity.")]
        public int strength = 20;

        [Tooltip("The initial dexterity of this Entity.")]
        public int dexterity = 15;

        [Tooltip("The initial vitality of this Entity.")]
        public int vitality = 15;

        [Tooltip("The initial energy of this Entity.")]
        public int energy = 10;

        [Header("Combo Settings")]
        [Tooltip("If true, the Entity will always use the base combo stats.")]
        public bool alwaysUseBaseComboStats;

        [Tooltip("The base maximum number of combos.")]
        public int baseMaxCombos = 1;

        [Tooltip("The base time it takes to stop a combo if no attack is performed.")]
        public float baseTimeToStopCombo = 1f;

        [Tooltip("The delay before performing the next combo when an attack is triggered.")]
        public float baseNextComboDelay = 0.1f;

        [Header("Bot Settings")]
        [Tooltip("If true, the Entity will be able to gain experience.")]
        public bool canGainExperience = true;

        [Tooltip("If true, the current health will never decrease.")]
        public bool infiniteHealth;

        [Tooltip("If true, the current mana will never decrease.")]
        public bool infiniteMana;

        [Tooltip("If true, the Entity will be immune to stun.")]
        public bool immuneToStun;

        protected int m_health;
        protected int m_mana;
        protected int m_experience;

        /// <summary>
        /// Returns true if the component was initialized.
        /// </summary>
        public bool initialized { get; protected set; }

        /// <summary>
        /// Returns the amount of experience to get to the next level.
        /// </summary>
        public int nextLevelExp { get; protected set; }

        /// <summary>
        /// Returns the amount of available distribution points.
        /// </summary>
        public int availablePoints { get; protected set; }

        /// <summary>
        /// Returns the maximum health points.
        /// </summary>
        public int maxHealth { get; protected set; }

        /// <summary>
        /// Returns the maximum mana points.
        /// </summary>
        public int maxMana { get; protected set; }

        /// <summary>
        /// Returns the minimum normal attack damage.
        /// </summary>
        public int minDamage { get; protected set; }

        /// <summary>
        /// Returns the minimum normal attack damage.
        /// </summary>
        public int maxDamage { get; protected set; }

        /// <summary>
        /// Returns the minimum magic damage.
        /// </summary>
        public int minMagicDamage { get; protected set; }

        /// <summary>
        /// Returns the maximum magic damage.
        /// </summary>
        public int maxMagicDamage { get; protected set; }

        /// <summary>
        /// Returns the maximum number of combos.
        /// </summary>
        public int maxCombos { get; protected set; }

        /// <summary>
        /// Returns the time it takes to stop a combo if no attack is performed.
        /// </summary>
        public float timeToStopCombo { get; protected set; }

        /// <summary>
        /// Returns the time it takes to perform the next combo.
        /// </summary>
        public float nextComboDelay { get; protected set; }

        /// <summary>
        /// Returns the current defense points.
        /// </summary>
        public int defense { get; protected set; }

        /// <summary>
        /// Returns the current attack speed.
        /// </summary>
        public int attackSpeed { get; protected set; }

        /// <summary>
        /// Returns the current critical chance in percentage.
        /// </summary>
        public float criticalChance { get; protected set; }

        /// <summary>
        /// Returns the current chance to block incoming attacks.
        /// </summary>
        public float chanceToBlock { get; protected set; }

        /// <summary>
        /// Returns the current block recover speed.
        /// </summary>
        public int blockSpeed { get; protected set; }

        /// <summary>
        /// Returns the current chance to stun an enemy when attacking.
        /// </summary>
        public float stunChance { get; protected set; }

        /// <summary>
        /// Returns the current stun recover speed.
        /// </summary>
        public int stunSpeed { get; protected set; }

        /// <summary>
        /// Get or set the experience points.
        /// </summary>
        public int experience
        {
            get { return m_experience; }
            protected set
            {
                m_experience = value;
                onExperienceChanged?.Invoke();
            }
        }

        /// <summary>
        /// Get or set the health points.
        /// </summary>
        public int health
        {
            get
            {
                if (infiniteHealth)
                    return maxHealth;
                return m_health;
            }
            set
            {
                m_health = Mathf.Clamp(value, 0, maxHealth);
                onHealthChanged?.Invoke();
            }
        }

        /// <summary>
        /// Get or set the mana points.
        /// </summary>
        public int mana
        {
            get
            {
                if (infiniteMana)
                    return maxMana;

                return m_mana;
            }
            set
            {
                m_mana = Mathf.Clamp(value, 0, maxMana);
                onManaChanged?.Invoke();
            }
        }

        protected EntityItemManager m_items;
        protected EntitySkillManager m_skills;
        protected ItemFinalAttributes m_additionalAttributes;
        protected List<Entity> m_defeatedEntities = new();

        /// <summary>
        /// Returns true if the Entity is using a weapon.
        /// </summary>
        protected bool m_isUsingWeapon => m_items && m_items.IsUsingWeapon();

        /// <summary>
        /// Returns true if the Entity reached the maximum level.
        /// </summary>
        protected bool m_reachedMaxLevel => level >= Game.instance.maxLevel;

        /// <summary>
        /// Initializes the Stats Manager.
        /// </summary>
        public virtual void Initialize()
        {
            if (initialized)
                return;

            InitializeItems();
            InitializeSkills();
            Recalculate();
            Revitalize();

            initialized = true;
        }

        protected virtual void InitializeItems()
        {
            m_items = GetComponent<EntityItemManager>();

            if (m_items)
                m_items.onChanged.AddListener(Recalculate);
        }

        protected virtual void InitializeSkills() => m_skills = GetComponent<EntitySkillManager>();

        /// <summary>
        /// Bulk update all stats points and recalculate the stats.
        /// </summary>
        public virtual void BulkUpdate(
            int level,
            int strength,
            int dexterity,
            int vitality,
            int energy,
            int availablePoints,
            int experience
        )
        {
            this.level = level;
            this.strength = strength;
            this.dexterity = dexterity;
            this.vitality = vitality;
            this.energy = energy;
            this.availablePoints = availablePoints;
            this.experience = experience;
            Recalculate();
        }

        /// <summary>
        /// Bulk distribute the available distribution points and recalculate the stats.
        /// </summary>
        public virtual void BulkDistribute(int strength, int dexterity, int vitality, int energy)
        {
            this.strength += strength;
            this.dexterity += dexterity;
            this.vitality += vitality;
            this.energy += energy;
            this.availablePoints -= strength + dexterity + vitality + energy;
            Recalculate();
        }

        /// <summary>
        /// Gets the current health in a 0 to 1 range.
        /// </summary>
        public virtual float GetHealthPercent() => health / (float)maxHealth;

        /// <summary>
        /// Gets the current mana in a 0 to 1 range.
        /// </summary>
        public virtual float GetManaPercent() => mana / (float)maxMana;

        /// <summary>
        /// Returns the current experience in a 0 to 1 range.
        /// </summary>
        public virtual float GetExperiencePercent() => experience / (float)nextLevelExp;

        /// <summary>
        /// Calculates the normal attack damage points with the critical multiplier.
        /// </summary>
        /// <param name="critical">If true, the damage is critical.</param>
        public virtual int GetDamage(out bool critical) =>
            (int)(GetCriticalMultiplier(out critical) * GetFinalDamage());

        /// <summary>
        /// Calculates the magic attack damage points using a given skill with the critical multiplier.
        /// </summary>
        /// <param name="skill">The skill you want to calculate damage for.</param>
        /// <param name="critical">If true, the damage is critical.</param>
        public virtual int GetSkillDamage(Skill skill, out bool critical) =>
            (int)(GetCriticalMultiplier(out critical) * GetSkillDamage(skill));

        /// <summary>
        /// Return the attack animation speed multiplier based on the attack speed stat.
        /// </summary>
        public virtual float GetAnimationAttackSpeed() =>
            attackSpeed / (float)Game.instance.maxAttackSpeed;

        /// <summary>
        /// Return the block animation speed multiplier based on the block speed stat.
        /// </summary>
        public virtual float GetAnimationBlockSpeed() =>
            blockSpeed / (float)Game.instance.maxBlockSpeed;

        /// <summary>
        /// Returns the stun speed multiplier based on the stun speed stat.
        /// </summary>
        public virtual float GetStunAnimationSpeed() =>
            stunSpeed / (float)Game.instance.maxStunSpeed;

        /// <summary>
        /// Returns the final normal damage points.
        /// </summary>
        protected virtual int GetFinalDamage() =>
            (int)(Random.Range(minDamage, maxDamage) * m_additionalAttributes.damageMultiplier);

        /// <summary>
        /// Returns the final magical damage points.
        /// </summary>
        protected virtual int GetFinalMagicDamage() =>
            (int)(
                Random.Range(minMagicDamage, maxMagicDamage)
                * m_additionalAttributes.damageMultiplier
            );

        /// <summary>
        /// Returns the magic damage points given a skill.
        /// </summary>
        /// <param name="skill">The skill you want to calculate magic damage from.</param>
        protected virtual int GetSkillDamage(Skill skill)
        {
            if (!skill || !skill.IsAttack())
                return 0;

            var damage = skill.AsAttack().GetDamage();

            switch (skill.AsAttack().damageMode)
            {
                default:
                case SkillAttack.DamageMode.Regular:
                    damage += GetFinalDamage();
                    break;
                case SkillAttack.DamageMode.Magic:
                    damage += GetFinalMagicDamage();
                    break;
            }

            return damage;
        }

        /// <summary>
        /// Returns the maximum number of combos based on the equipped weapon.
        /// </summary>
        protected virtual int GetItemsMaxCombos()
        {
            if (alwaysUseBaseComboStats || !m_isUsingWeapon)
                return baseMaxCombos;

            return m_items.GetWeapon().maxCombos;
        }

        /// <summary>
        /// Returns the time it takes to stop a combo based on the equipped weapon.
        /// </summary>
        protected float GetTimeToStopCombo()
        {
            if (alwaysUseBaseComboStats || !m_isUsingWeapon)
                return baseTimeToStopCombo;

            return m_items.GetWeapon().timeToStopCombo;
        }

        /// <summary>
        /// Returns the time it takes to perform the next combo based on the equipped weapon.
        /// </summary>
        protected float GetNextComboDelay()
        {
            if (alwaysUseBaseComboStats || !m_isUsingWeapon)
                return baseNextComboDelay;

            return m_items.GetWeapon().nextComboDelay;
        }

        /// <summary>
        /// Returns the minimum and maximum damage calculated from the equipped items.
        /// </summary>
        protected virtual MinMax GetItemsDamage()
        {
            if (!m_items)
                return MinMax.Zero;

            return m_items.GetDamage();
        }

        /// <summary>
        /// Returns the defense points calculated from the equipped items.
        /// </summary>
        protected virtual int GetItemsDefense()
        {
            if (!m_items)
                return 0;

            return m_items.GetDefense();
        }

        /// <summary>
        /// Returns the attack speed points calculated from the equipped items.
        /// </summary>
        protected virtual int GetItemsAttackSpeed()
        {
            if (!m_items)
                return 0;

            return m_items.GetAttackSpeed();
        }

        /// <summary>
        /// Returns the chance to block from the equipped items.
        /// </summary>
        protected virtual float GetItemsChanceToBlock()
        {
            if (!m_items)
                return 0;

            return m_items.GetChanceToBlock();
        }

        /// <summary>
        /// Sets all the attribute points from the equipped items.
        /// </summary>
        protected virtual void SetAdditionalAttributes()
        {
            m_additionalAttributes = m_items ? m_items.GetFinalAttributes() : new();
        }

        /// <summary>
        /// Calculates and return the critical multiplier in percentage.
        /// </summary>
        /// <param name="success">If true, the critical is successful.</param>
        protected virtual float GetCriticalMultiplier(out bool success)
        {
            success = Random.value > 1 - criticalChance;
            return success ? Game.instance.criticalMultiplier : 1;
        }

        /// <summary>
        /// Recalculates the points for all the Entity's dynamic stats.
        /// </summary>
        public virtual void Recalculate()
        {
            SetAdditionalAttributes();

            var magicDamage = CalculateMagicDamage();
            var damage = CalculateDamage();

            nextLevelExp = CalculateNextLevelExperience();
            maxHealth = CalculateMaxHealth();
            maxMana = CalculateMaxMana();
            minDamage = damage.min;
            maxDamage = damage.max;
            maxCombos = GetItemsMaxCombos();
            timeToStopCombo = GetTimeToStopCombo();
            nextComboDelay = GetNextComboDelay();
            attackSpeed = CalculateAttackSpeed();
            minMagicDamage = magicDamage.min;
            maxMagicDamage = magicDamage.max;
            criticalChance = CalculateCriticalChance();
            chanceToBlock = CalculateChanceToBlock();
            blockSpeed = CalculateBlockSpeed();
            defense = CalculateDefense();
            stunChance = CalculateStunChance();
            stunSpeed = CalculateStunSpeed();
            health = Mathf.Min(health, maxHealth);
            mana = Mathf.Min(mana, maxMana);
            onRecalculate?.Invoke();
        }

        /// <summary>
        /// Restores the current health and mana points to its maximum values.
        /// </summary>
        public virtual void Revitalize()
        {
            ResetHealth();
            ResetMana();
        }

        /// <summary>
        /// Sets the health points to its maximum value.
        /// </summary>
        public virtual void ResetHealth() => health = maxHealth;

        /// <summary>
        /// Sets the mana points to its maximum value.
        /// </summary>
        public virtual void ResetMana() => mana = maxMana;

        /// <summary>
        /// Level Up the Entity, consuming the experience points, and recalculating all its dynamic stats.
        /// </summary>
        protected virtual void LevelUp()
        {
            if (m_reachedMaxLevel)
                return;

            while (experience >= nextLevelExp)
            {
                level++;
                experience -= nextLevelExp;
                availablePoints += Game.instance.levelUpPoints;
                nextLevelExp = CalculateNextLevelExperience();
            }

            Recalculate();
            Revitalize();

            onLevelUp?.Invoke();
        }

        /// <summary>
        /// Add experience points to the Entity.
        /// </summary>
        /// <param name="amount">The amount of experience points you want to add.</param>
        public virtual void AddExperience(int amount)
        {
            if (!canGainExperience || m_reachedMaxLevel)
                return;

            experience += amount;

            if (experience >= nextLevelExp)
                LevelUp();
        }

        /// <summary>
        /// Set the current experience points to zero.
        /// </summary>
        public virtual void ResetExperience() => experience = 0;

        /// <summary>
        /// Calculates and sets the experience points acquired by defeating a given Entity.
        /// </summary>
        /// <param name="other">The Entity you want to use as a base to the calculation.</param>
        public virtual void OnDefeatEntity(Entity other)
        {
            if (m_defeatedEntities.Contains(other))
                return;

            m_defeatedEntities.Add(other);
            AddExperience(Game.instance.baseEnemyDefeatExperience * other.stats.level);
        }

        protected virtual void Start() => Initialize();
    }
}
