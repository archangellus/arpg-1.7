using UnityEngine;

namespace PLAYERTWO.ARPGProject
{
    public partial class EntityStatsManager
    {
        /// <summary>
        /// Calculates the minimum and maximum damage of the entity.
        /// </summary>
        protected virtual MinMax CalculateDamage()
        {
            var fallback = CalculateDefaultDamage();
            var hasMinOverride = TryEvaluateFormula(
                EntityStatsFormulaTarget.MinDamage,
                fallback.min,
                out var minOverride
            );
            var hasMaxOverride = TryEvaluateFormula(
                EntityStatsFormulaTarget.MaxDamage,
                fallback.max,
                out var maxOverride
            );

            return new MinMax
            {
                min = hasMinOverride ? Mathf.RoundToInt(minOverride) : fallback.min,
                max = hasMaxOverride ? Mathf.RoundToInt(maxOverride) : fallback.max,
            };
        }

        protected virtual MinMax CalculateDefaultDamage()
        {
            var weaponDamage = GetItemsDamage();
            var effectiveStrength =
                strength + m_additionalAttributes[ItemAttributes.AttributeType.Strength];

            return new MinMax
            {
                min =
                    (effectiveStrength / 8)
                    + weaponDamage.min
                    + m_additionalAttributes[ItemAttributes.AttributeType.Damage],
                max =
                    (effectiveStrength / 4)
                    + weaponDamage.max
                    + m_additionalAttributes[ItemAttributes.AttributeType.Damage],
            };
        }

        /// <summary>
        /// Calculates the minimum and maximum magic damage of the entity.
        /// </summary>
        protected virtual MinMax CalculateMagicDamage()
        {
            var fallback = CalculateDefaultMagicDamage();
            var hasMinOverride = TryEvaluateFormula(
                EntityStatsFormulaTarget.MinMagicDamage,
                fallback.min,
                out var minOverride
            );
            var hasMaxOverride = TryEvaluateFormula(
                EntityStatsFormulaTarget.MaxMagicDamage,
                fallback.max,
                out var maxOverride
            );

            return new MinMax
            {
                min = hasMinOverride ? Mathf.RoundToInt(minOverride) : fallback.min,
                max = hasMaxOverride ? Mathf.RoundToInt(maxOverride) : fallback.max,
            };
        }

        protected virtual MinMax CalculateDefaultMagicDamage()
        {
            var effectiveEnergy =
                energy + m_additionalAttributes[ItemAttributes.AttributeType.Energy];
            var flatBonus = m_additionalAttributes[ItemAttributes.AttributeType.MagicDamage];

            return new MinMax
            {
                min = (effectiveEnergy / 4) + flatBonus,
                max = (effectiveEnergy / 2) + flatBonus,
            };
        }

        /// <summary>
        /// Calculates the amount of experience points needed to reach the next level.
        /// </summary>
        protected virtual int CalculateNextLevelExperience()
        {
            var fallback = CalculateDefaultNextLevelExperience();
            return TryEvaluateFormula(EntityStatsFormulaTarget.NextLevelExperience, fallback, out var value)
                ? Mathf.RoundToInt(value)
                : fallback;
        }

        protected virtual int CalculateDefaultNextLevelExperience() =>
            Game.instance.baseExperience + (Game.instance.experiencePerLevel * (level - 1));

        /// <summary>
        /// Calculates the max health points of the entity.
        /// </summary>
        protected virtual int CalculateMaxHealth()
        {
            var fallback = CalculateDefaultMaxHealth();
            return TryEvaluateFormula(EntityStatsFormulaTarget.MaxHealth, fallback, out var value)
                ? Mathf.RoundToInt(value)
                : fallback;
        }

        protected virtual int CalculateDefaultMaxHealth()
        {
            var effectiveVitality =
                vitality + m_additionalAttributes[ItemAttributes.AttributeType.Vitality];

            return (int)(
                (
                    level * 10
                    + effectiveVitality * 2
                    + m_additionalAttributes[ItemAttributes.AttributeType.Health]
                ) * m_additionalAttributes.GetHealthMultiplier()
            );
        }

        /// <summary>
        /// Calculates the max mana points of the entity.
        /// </summary>
        protected virtual int CalculateMaxMana()
        {
            var fallback = CalculateDefaultMaxMana();
            return TryEvaluateFormula(EntityStatsFormulaTarget.MaxMana, fallback, out var value)
                ? Mathf.RoundToInt(value)
                : fallback;
        }

        protected virtual int CalculateDefaultMaxMana()
        {
            var effectiveEnergy =
                energy + m_additionalAttributes[ItemAttributes.AttributeType.Energy];

            return (int)(
                (
                    level * 5
                    + effectiveEnergy * 2
                    + m_additionalAttributes[ItemAttributes.AttributeType.Mana]
                ) * m_additionalAttributes.GetManaMultiplier()
            );
        }

        /// <summary>
        /// Calculates the attack speed which is used by the entity attack animations.
        /// </summary>
        protected virtual int CalculateAttackSpeed()
        {
            var fallback = CalculateDefaultAttackSpeed();
            return TryEvaluateFormula(EntityStatsFormulaTarget.AttackSpeed, fallback, out var value)
                ? Mathf.RoundToInt(value)
                : fallback;
        }

        protected virtual int CalculateDefaultAttackSpeed()
        {
            var effectiveDexterity =
                dexterity + m_additionalAttributes[ItemAttributes.AttributeType.Dexterity];

            return Mathf.Min(
                (effectiveDexterity + GetItemsAttackSpeed()) / 10
                    + m_additionalAttributes[ItemAttributes.AttributeType.AttackSpeed],
                Game.instance.maxAttackSpeed
            );
        }

        /// <summary>
        /// Calculates the percentage chance of performing a critical attack.
        /// </summary>
        protected virtual float CalculateCriticalChance()
        {
            var fallback = CalculateDefaultCriticalChance();
            return TryEvaluateFormula(EntityStatsFormulaTarget.CriticalChance, fallback, out var value)
                ? value
                : fallback;
        }

        protected virtual float CalculateDefaultCriticalChance()
        {
            var effectiveDexterity =
                dexterity + m_additionalAttributes[ItemAttributes.AttributeType.Dexterity];

            return (effectiveDexterity / 10 + 20)
                / 100f
                * m_additionalAttributes.GetCriticalMultiplier();
        }

        /// <summary>
        /// Calculates the defense points of the entity.
        /// </summary>
        protected virtual int CalculateDefense()
        {
            var fallback = CalculateDefaultDefense();
            return TryEvaluateFormula(EntityStatsFormulaTarget.Defense, fallback, out var value)
                ? Mathf.RoundToInt(value)
                : fallback;
        }

        protected virtual int CalculateDefaultDefense()
        {
            var effectiveDexterity =
                dexterity + m_additionalAttributes[ItemAttributes.AttributeType.Dexterity];

            return (int)(
                (
                    (effectiveDexterity / 4)
                    + GetItemsDefense()
                    + m_additionalAttributes[ItemAttributes.AttributeType.Defense]
                ) * m_additionalAttributes.GetDefenseMultiplier()
            );
        }

        /// <summary>
        /// Calculates the chance of blocking attacks and returns it in percentage.
        /// Returns zero if the entity is not wearing a shield.
        /// </summary>
        protected virtual float CalculateChanceToBlock()
        {
            if (m_items == null || !m_items.IsUsingShield() || m_items.GetLeftHand().IsBroken())
                return 0;

            var fallback = CalculateDefaultChanceToBlock();
            return TryEvaluateFormula(EntityStatsFormulaTarget.ChanceToBlock, fallback, out var value)
                ? value
                : fallback;
        }

        protected virtual float CalculateDefaultChanceToBlock()
        {
            var effectiveDexterity =
                dexterity + m_additionalAttributes[ItemAttributes.AttributeType.Dexterity];
            var baseChance = (effectiveDexterity / 20 + 5 + level) / 100f + GetItemsChanceToBlock();
            var multiplier =
                1f
                + m_additionalAttributes[ItemAttributes.AttributeType.ChanceOfBlockingPercent]
                    / 100f;

            return Mathf.Min(baseChance * multiplier, Game.instance.maxBlockChance);
        }

        /// <summary>
        /// Calculates the block recover speed which will be used by the entity block animations.
        /// </summary>
        protected virtual int CalculateBlockSpeed()
        {
            var fallback = CalculateDefaultBlockSpeed();
            return TryEvaluateFormula(EntityStatsFormulaTarget.BlockSpeed, fallback, out var value)
                ? Mathf.RoundToInt(value)
                : fallback;
        }

        protected virtual int CalculateDefaultBlockSpeed()
        {
            var effectiveDexterity =
                dexterity + m_additionalAttributes[ItemAttributes.AttributeType.Dexterity];
            var baseSpeed = effectiveDexterity / 5 + 100 + level * 10;
            var multiplier =
                1f
                + m_additionalAttributes[ItemAttributes.AttributeType.BlockRecoveryPercent] / 100f;

            return Mathf.Min((int)(baseSpeed * multiplier), Game.instance.maxBlockSpeed);
        }

        /// <summary>
        /// Calculates the chance of stunning the enemy after a successful attack.
        /// </summary>
        protected virtual float CalculateStunChance()
        {
            var fallback = CalculateDefaultStunChance();
            return TryEvaluateFormula(EntityStatsFormulaTarget.StunChance, fallback, out var value)
                ? value
                : fallback;
        }

        protected virtual float CalculateDefaultStunChance()
        {
            var effectiveStrength =
                strength + m_additionalAttributes[ItemAttributes.AttributeType.Strength];
            var baseChance = (effectiveStrength / 10 + level) / 100f;
            var multiplier =
                1f
                + m_additionalAttributes[ItemAttributes.AttributeType.ChanceToStunPercent] / 100f;

            return Mathf.Min(baseChance * multiplier, Game.instance.maxStunChance);
        }

        /// <summary>
        /// Calculates the stun recover speed which will be used by the entity stun animations.
        /// </summary>
        protected virtual int CalculateStunSpeed()
        {
            var fallback = CalculateDefaultStunSpeed();
            return TryEvaluateFormula(EntityStatsFormulaTarget.StunSpeed, fallback, out var value)
                ? Mathf.RoundToInt(value)
                : fallback;
        }

        protected virtual int CalculateDefaultStunSpeed()
        {
            var effectiveDexterity =
                dexterity + m_additionalAttributes[ItemAttributes.AttributeType.Dexterity];
            var baseSpeed = effectiveDexterity / 2 + 100 + level * 20;
            var multiplier =
                1f
                + m_additionalAttributes[ItemAttributes.AttributeType.StunRecoveryPercent] / 100f;

            return Mathf.Min((int)(baseSpeed * multiplier), Game.instance.maxStunSpeed);
        }

        /// <summary>
        /// Calculates the accuracy rating used in hit chance calculations against evasive targets.
        /// Scales with dexterity and level, plus a flat base defined in <see cref="Game.accuracyBase"/>.
        /// Flat <see cref="ItemAttributes.AttributeType.Accuracy"/> is added to the base sum before
        /// the <see cref="ItemAttributes.AttributeType.AccuracyPercent"/> multiplier is applied.
        /// </summary>
        protected virtual int CalculateAccuracy()
        {
            var fallback = CalculateDefaultAccuracy();
            return TryEvaluateFormula(EntityStatsFormulaTarget.Accuracy, fallback, out var value)
                ? Mathf.RoundToInt(value)
                : fallback;
        }

        protected virtual int CalculateDefaultAccuracy()
        {
            var effectiveDexterity =
                dexterity + m_additionalAttributes[ItemAttributes.AttributeType.Dexterity];
            var flatBonus = m_additionalAttributes[ItemAttributes.AttributeType.Accuracy];
            var itemMultiplier =
                1f + m_additionalAttributes[ItemAttributes.AttributeType.AccuracyPercent] / 100f;

            return Mathf.RoundToInt(
                (effectiveDexterity * 4 + level * 10 + Game.instance.accuracyBase + flatBonus)
                    * itemMultiplier
            );
        }

        /// <summary>
        /// Calculates the evasion rating used in hit chance calculations against incoming active attacks.
        /// Scales with dexterity and level.
        /// Flat <see cref="ItemAttributes.AttributeType.Evasion"/> is added to the base sum before
        /// the <see cref="ItemAttributes.AttributeType.EvasionPercent"/> multiplier is applied.
        /// </summary>
        protected virtual int CalculateEvasion()
        {
            var fallback = CalculateDefaultEvasion();
            return TryEvaluateFormula(EntityStatsFormulaTarget.Evasion, fallback, out var value)
                ? Mathf.RoundToInt(value)
                : fallback;
        }

        protected virtual int CalculateDefaultEvasion()
        {
            var effectiveDexterity =
                dexterity + m_additionalAttributes[ItemAttributes.AttributeType.Dexterity];
            var flatBonus = m_additionalAttributes[ItemAttributes.AttributeType.Evasion];
            var itemMultiplier =
                1f + m_additionalAttributes[ItemAttributes.AttributeType.EvasionPercent] / 100f;

            return Mathf.RoundToInt(
                (effectiveDexterity * 2 + level * 5 + flatBonus) * itemMultiplier
            );
        }
    }
}
