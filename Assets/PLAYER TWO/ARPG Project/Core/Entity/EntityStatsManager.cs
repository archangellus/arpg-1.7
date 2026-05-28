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
        [Tooltip("The maximum speed this Entity will use to move around.")]
        public float moveSpeed = 6f;

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

        [Header("Elemental Resistance")]
        [Tooltip(
            "Damage types this Entity is immune to. Incoming damage is reduced to zero and effects cannot be applied."
        )]
        public ElementalDamageTypeMask immuneToElements;

        [Tooltip(
            "Damage types this Entity is resistant to. Incoming damage from these types is halved."
        )]
        public ElementalDamageTypeMask resistantToElements;

        [Tooltip(
            "Damage types this Entity is weak to. Incoming damage from these types is doubled."
        )]
        public ElementalDamageTypeMask weakToElements;

        [Header("Bot Settings")]
        [Tooltip("If true, the Entity will be able to gain experience.")]
        public bool canGainExperience = true;

        [Tooltip("If true, the current health will never decrease.")]
        public bool infiniteHealth;

        [Tooltip("If true, the current mana will never decrease.")]
        public bool infiniteMana;

        [Tooltip("If true, the Entity will be immune to stun.")]
        public bool immuneToStun;

        [Tooltip(
            "If true, this entity's attacks always hit regardless of the accuracy vs evasion roll."
        )]
        public bool alwaysHit;

        [Tooltip("If true, this entity always evades incoming active attacks.")]
        public bool alwaysEvade;

        [Tooltip("If true, attackers cannot leech health or mana from hits against this entity.")]
        public bool immuneToLeech;

        [Tooltip("If true, this entity cannot receive reflected damage.")]
        public bool immuneToReflection;

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
        /// Returns the accuracy rating used in the hit chance calculation.
        /// Higher values increase the chance to land attacks against evasive targets.
        /// </summary>
        public int accuracy { get; protected set; }

        /// <summary>
        /// Returns the evasion rating used in the hit chance calculation.
        /// Higher values increase the chance to avoid incoming active attacks.
        /// </summary>
        public int evasion { get; protected set; }

        /// <summary>
        /// Returns the skill cooldown reduction factor derived from item attributes (0 = no reduction).
        /// </summary>
        public float skillCoolDownReduction =>
            m_additionalAttributes[ItemAttributes.AttributeType.SkillCoolDownPercent] / 100f;

        /// <summary>Flat health gained by this entity on each successful hit.</summary>
        public int healthOnHit => m_additionalAttributes[ItemAttributes.AttributeType.HealthOnHit];

        /// <summary>Flat mana gained by this entity on each successful hit.</summary>
        public int manaOnHit => m_additionalAttributes[ItemAttributes.AttributeType.ManaOnHit];

        /// <summary>Percentage of damage dealt converted into health for the attacker.</summary>
        public int healthLeechPercent =>
            m_additionalAttributes[ItemAttributes.AttributeType.HealthLeechPercent];

        /// <summary>Percentage of damage dealt converted into mana for the attacker.</summary>
        public int manaLeechPercent =>
            m_additionalAttributes[ItemAttributes.AttributeType.ManaLeechPercent];

        /// <summary>Percentage of damage taken reflected back to the attacker.</summary>
        public int damageReflectionPercent =>
            m_additionalAttributes[ItemAttributes.AttributeType.DamageReflectionPercent];

        /// <summary>Percentage of damage taken converted into mana for this entity.</summary>
        public int damageToManaPercent =>
            m_additionalAttributes[ItemAttributes.AttributeType.DamageToManaPercent];

        /// <summary>Chance (0–100) to inflict Bleed on a successful hit, from item attributes.</summary>
        public virtual int chanceToBleedPercent =>
            m_additionalAttributes[ItemAttributes.AttributeType.ChanceToBleedPercent];

        /// <summary>Chance (0–100) to inflict Burn on a successful hit, from item attributes.</summary>
        public virtual int chanceToBurnPercent =>
            m_additionalAttributes[ItemAttributes.AttributeType.ChanceToBurnPercent];

        /// <summary>Chance (0–100) to inflict Freeze on a successful hit, from item attributes.</summary>
        public virtual int chanceToFreezePercent =>
            m_additionalAttributes[ItemAttributes.AttributeType.ChanceToFreezePercent];

        /// <summary>Chance (0–100) to inflict Poison on a successful hit, from item attributes.</summary>
        public virtual int chanceToPoisonPercent =>
            m_additionalAttributes[ItemAttributes.AttributeType.ChanceToPoisonPercent];

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
        protected EntityEffectManager m_effects;
        protected ItemAttributes m_additionalAttributes;
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
            InitializeEffects();
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

        protected virtual void InitializeEffects() =>
            m_effects = GetComponent<EntityEffectManager>();

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
        /// Returns true if the entity is immune to the given damage type.
        /// </summary>
        public bool IsImmuneToElement(DamageType type) =>
            ((ElementalDamageTypeMask)(1 << (int)type) & immuneToElements)
            != ElementalDamageTypeMask.None;

        /// <summary>
        /// Returns true if the entity is resistant to the given damage type (damage is halved).
        /// </summary>
        public bool IsResistantToElement(DamageType type) =>
            ((ElementalDamageTypeMask)(1 << (int)type) & resistantToElements)
            != ElementalDamageTypeMask.None;

        /// <summary>
        /// Returns true if the entity is weak to the given damage type (damage is doubled).
        /// </summary>
        public bool IsWeakToElement(DamageType type) =>
            ((ElementalDamageTypeMask)(1 << (int)type) & weakToElements)
            != ElementalDamageTypeMask.None;

        /// <summary>
        /// Returns the item-attribute-based elemental resistance multiplier for the given damage type,
        /// clamped to [0, 1] to prevent negative damage.
        /// </summary>
        public virtual float GetElementalResistMultiplier(DamageType type) =>
            Mathf.Clamp01(m_additionalAttributes.GetElementalResistMultiplier(type));

        /// <summary>
        /// Calculates the normal attack damage points for the given elemental type with the critical multiplier.
        /// </summary>
        /// <param name="damageType">The elemental type of the attack.</param>
        /// <param name="critical">If true, the damage is critical.</param>
        public virtual int GetDamage(DamageType damageType, out bool critical) =>
            (int)(GetCriticalMultiplier(out critical) * GetFinalDamage(damageType));

        /// <summary>
        /// Calculates the normal attack damage points with the critical multiplier.
        /// </summary>
        /// <param name="critical">If true, the damage is critical.</param>
        public virtual int GetDamage(out bool critical) =>
            GetDamage(DamageType.Normal, out critical);

        /// <summary>
        /// Returns the damage layers for a weapon melee or bow attack. The first layer is
        /// always Normal (physical). Additional elemental layers are added for each damage type
        /// that has a positive flat bonus from item attribute affixes. Critical multiplier
        /// applies only to the physical layer.
        /// </summary>
        /// <param name="critical">Set to true when the attack is a critical strike.</param>
        public virtual List<DamageLayer> GetWeaponDamageLayers(out bool critical)
        {
            var critMultiplier = GetCriticalMultiplier(out critical);
            var effectMult = m_effects?.damageMultiplier ?? 1f;
            var layers = new List<DamageLayer>();

            layers.Add(
                new DamageLayer(
                    DamageType.Normal,
                    (int)(GetFinalDamage(DamageType.Normal) * critMultiplier)
                )
            );

            foreach (DamageType elementType in System.Enum.GetValues(typeof(DamageType)))
            {
                if (elementType == DamageType.Normal)
                    continue;

                var flatBonus = m_additionalAttributes.GetElementalFlatDamageBonus(elementType);

                if (flatBonus <= 0)
                    continue;

                var pctMultiplier = m_additionalAttributes.GetElementalDamageMultiplier(
                    elementType
                );
                var elementalAmount = (int)(flatBonus * pctMultiplier * effectMult);

                if (elementalAmount > 0)
                    layers.Add(new DamageLayer(elementType, elementalAmount));
            }

            return layers;
        }

        /// <summary>
        /// Calculates the magic attack damage points using a given skill with the critical multiplier.
        /// </summary>
        /// <param name="skill">The skill you want to calculate damage for.</param>
        /// <param name="critical">If true, the damage is critical.</param>
        public virtual int GetSkillDamage(Skill skill, out bool critical) =>
            (int)(GetCriticalMultiplier(out critical) * GetSkillDamage(skill));

        /// <summary>
        /// Returns the effective defense including any active debuff modifiers.
        /// </summary>
        public int effectiveDefense =>
            m_effects == null ? defense : (int)(defense * m_effects.defenseMultiplier);

        /// <summary>
        /// Returns the effective damage-taken multiplier from active buff effects.
        /// Values below 1 indicate a reduction (e.g., 0.7 = 30% less damage taken).
        /// </summary>
        public float damageTakenMultiplier =>
            m_effects == null ? 1f : m_effects.damageTakenMultiplier;

        /// <summary>
        /// Returns the multiplier for attack speed from active buff or debuff effects.
        /// Values above 1 indicate an increase (e.g., 1.2 = 20% faster attack speed),
        /// while values below 1 indicate a decrease (e.g., 0.8 = 20% slower attack speed).
        /// </summary>
        public float attackSpeedMultiplier =>
            m_effects == null ? 1f : m_effects.attackSpeedMultiplier;

        /// <summary>
        /// Returns the effective move speed including any active buff or debuff modifiers.
        /// </summary>
        public float effectiveMoveSpeed =>
            m_effects == null ? moveSpeed : moveSpeed * m_effects.moveSpeedMultiplier;

        /// <summary>
        /// Returns the effective accuracy rating including any active effect multipliers.
        /// </summary>
        public int effectiveAccuracy =>
            m_effects == null ? accuracy : (int)(accuracy * m_effects.accuracyMultiplier);

        /// <summary>
        /// Returns the effective evasion rating including any active effect multipliers.
        /// </summary>
        public int effectiveEvasion =>
            m_effects == null ? evasion : (int)(evasion * m_effects.evasionMultiplier);

        /// <summary>
        /// Return the attack animation speed multiplier based on the effective attack speed stat.
        /// </summary>
        public virtual float GetAnimationAttackSpeed() =>
            attackSpeed / (float)Game.instance.maxAttackSpeed;

        /// <summary>
        /// Return the block animation speed multiplier based on the block speed stat.
        /// </summary>
        public virtual float GetAnimationBlockSpeed() =>
            blockSpeed / (float)Game.instance.maxBlockSpeed;

        /// <summary>
        /// Returns the probability that an attack from this entity lands against the given defender.
        /// Accounts for the level difference between attacker and defender, clamped to
        /// [<see cref="Game.minHitChance"/>, <see cref="Game.maxHitChance"/>].
        /// </summary>
        /// <param name="defenderStats">The stats of the entity being attacked.</param>
        public virtual float GetHitChance(EntityStatsManager defenderStats)
        {
            if (defenderStats == null)
                return 1f;

            var levelDiff = Mathf.Max(0, defenderStats.level - level);
            var levelPenalty = 1f + levelDiff * Game.instance.evasionLevelPenaltyFactor;
            var penalizedEvasion = defenderStats.effectiveEvasion * levelPenalty;
            var raw = effectiveAccuracy / (effectiveAccuracy + penalizedEvasion);
            return Mathf.Clamp(raw, Game.instance.minHitChance, Game.instance.maxHitChance);
        }

        /// <summary>
        /// Returns the stun speed multiplier based on the stun speed stat.
        /// </summary>
        public virtual float GetStunAnimationSpeed() =>
            stunSpeed / (float)Game.instance.maxStunSpeed;

        /// <summary>
        /// Returns the final normal damage points, incorporating active effect modifiers and
        /// elemental flat bonus and percent multiplier for the given damage type.
        /// </summary>
        protected virtual int GetFinalDamage(DamageType damageType = DamageType.Normal)
        {
            var flatBonus = m_additionalAttributes.GetElementalFlatDamageBonus(damageType);
            var elementalMultiplier = m_additionalAttributes.GetElementalDamageMultiplier(
                damageType
            );

            var rolled = (int)(
                (
                    Random.Range(minDamage, maxDamage)
                        * m_additionalAttributes.GetDamageMultiplier()
                    + flatBonus
                ) * elementalMultiplier
            );

            if (m_effects == null)
                return rolled;

            return (int)(rolled * m_effects.damageMultiplier);
        }

        /// <summary>
        /// Returns the final magical damage points.
        /// </summary>
        protected virtual int GetFinalMagicDamage()
        {
            var rolled = (int)(
                Random.Range(minMagicDamage, maxMagicDamage)
                * m_additionalAttributes.GetMagicDamageMultiplier()
            );

            if (m_effects == null)
                return rolled;

            return (int)(rolled * m_effects.magicDamageMultiplier);
        }

        /// <summary>
        /// Returns the damage points given a skill, applying the skill's elemental flat bonus
        /// and percent multiplier.
        /// </summary>
        /// <param name="skill">The skill you want to calculate damage from.</param>
        protected virtual int GetSkillDamage(Skill skill)
        {
            if (!skill || !skill.IsAttack())
                return 0;

            var attack = skill.AsAttack();
            var flatBonus = m_additionalAttributes.GetElementalFlatDamageBonus(attack.damageType);
            var elementalMultiplier = m_additionalAttributes.GetElementalDamageMultiplier(
                attack.damageType
            );
            var damage = attack.GetDamage() + flatBonus;

            switch (attack.damageMode)
            {
                default:
                case SkillAttack.DamageMode.Regular:
                    damage += GetFinalDamage(attack.damageType);
                    break;
                case SkillAttack.DamageMode.Magic:
                    damage += GetFinalMagicDamage();
                    break;
            }

            return (int)(damage * elementalMultiplier);
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
        /// Returns the chance to block from the equipped items in percentage.
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
            m_additionalAttributes = m_items ? m_items.GetFinalAttributes() : new ItemAttributes();
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
            accuracy = CalculateAccuracy();
            evasion = CalculateEvasion();
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
            health += m_additionalAttributes[ItemAttributes.AttributeType.HealthOnKill];
            mana += m_additionalAttributes[ItemAttributes.AttributeType.ManaOnKill];
        }

        protected virtual void Start() => Initialize();
    }
}
