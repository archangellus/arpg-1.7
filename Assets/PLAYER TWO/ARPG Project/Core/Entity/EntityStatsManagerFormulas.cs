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
            var weaponDamage = GetItemsDamage();

            return new MinMax
            {
                min = (strength / 8) + weaponDamage.min + m_additionalAttributes.damage,
                max = (strength / 4) + weaponDamage.max + m_additionalAttributes.damage
            };
        }

        /// <summary>
        /// Calculates the minimum and maximum magic damage of the entity.
        /// </summary>
        protected virtual MinMax CalculateMagicDamage()
        {
            return new MinMax
            {
                min = energy / 4,
                max = energy / 2
            };
        }

        /// <summary>
        /// Calculates the amount of experience points needed to reach the next level.
        /// </summary>
        protected virtual int CalculateNextLevelExperience()
        {
            return Game.instance.baseExperience + (Game.instance.experiencePerLevel * (level - 1));
        }

        /// <summary>
        /// Calculates the max health points of the entity.
        /// </summary>
        protected virtual int CalculateMaxHealth()
        {
            return (int)((level * 10 + vitality * 2 + m_additionalAttributes.health) * m_additionalAttributes.healthMultiplier);
        }

        /// <summary>
        /// Calculates the max mana points of the entity.
        /// </summary>
        protected virtual int CalculateMaxMana()
        {
            return (int)((level * 5 + energy * 2 + m_additionalAttributes.mana) * m_additionalAttributes.manaMultiplier);
        }

        /// <summary>
        /// Calculates the attack speed which is used by the entity attack animations.
        /// </summary>
        protected virtual int CalculateAttackSpeed()
        {
            return Mathf.Min((dexterity + GetItemsAttackSpeed()) / 10 + m_additionalAttributes.attackSpeed, Game.instance.maxAttackSpeed);
        }

        /// <summary>
        /// Calculates the percentage chance of performing a critical attack.
        /// </summary>
        protected virtual float CalculateCriticalChance()
        {
            return (dexterity / 10 + 20) / 100f * m_additionalAttributes.criticalChanceMultiplier;
        }

        /// <summary>
        /// Calculates the defense points of the entity.
        /// </summary>
        protected virtual int CalculateDefense()
        {
            return (int)(((dexterity / 4) + GetItemsDefense() + m_additionalAttributes.defense) * m_additionalAttributes.defenseMultiplier);
        }

        /// <summary>
        /// Calculates the chance of blocking attacks.
        /// </summary>
        protected virtual float CalculateChanceToBlock()
        {
            return Mathf.Min((dexterity / 20 + 5 + level) / 100f * GetItemsChanceToBlock(), Game.instance.maxBlockChance);
        }

        /// <summary>
        /// Calculates the block recover speed which will be used by the entity block animations.
        /// </summary>
        protected virtual int CalculateBlockSpeed()
        {
            return Mathf.Min(dexterity / 5 + 100 + level * 10, Game.instance.maxBlockSpeed);
        }

        /// <summary>
        /// Calculates the chance of stunning the enemy after a successful attack.
        /// </summary>
        protected virtual float CalculateStunChance()
        {
            return Mathf.Min((strength / 10 + level) / 100f, Game.instance.maxStunChance);
        }

        /// <summary>
        /// Calculates the stun recover speed which will be used by the entity stun animations.
        /// </summary>
        /// <returns></returns>
        protected virtual int CalculateStunSpeed()
        {
            return Mathf.Min(dexterity / 2 + 100 + level * 20, Game.instance.maxStunSpeed);
        }
    }
}
