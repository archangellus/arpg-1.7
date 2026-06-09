using System;
using UnityEngine;

namespace PLAYERTWO.ARPGProject
{
    public readonly struct EntityStatsFormulaContext
    {
        readonly Func<EntityStatsFormulaInput, float> m_getValue;

        public EntityStatsFormulaContext(Func<EntityStatsFormulaInput, float> getValue)
        {
            m_getValue = getValue;
        }

        public float Get(EntityStatsFormulaInput input) => m_getValue != null ? m_getValue(input) : 0f;

        public static EntityStatsFormulaContext Preview =>
            new EntityStatsFormulaContext(input =>
            {
                switch (input)
                {
                    case EntityStatsFormulaInput.Level:
                        return 1f;
                    case EntityStatsFormulaInput.Strength:
                        return 20f;
                    case EntityStatsFormulaInput.Dexterity:
                    case EntityStatsFormulaInput.Vitality:
                        return 15f;
                    case EntityStatsFormulaInput.Energy:
                        return 10f;
                    case EntityStatsFormulaInput.WeaponDamageMin:
                        return 2f;
                    case EntityStatsFormulaInput.WeaponDamageMax:
                        return 5f;
                    case EntityStatsFormulaInput.HealthMultiplier:
                    case EntityStatsFormulaInput.ManaMultiplier:
                    case EntityStatsFormulaInput.DamageMultiplier:
                    case EntityStatsFormulaInput.MagicDamageMultiplier:
                    case EntityStatsFormulaInput.CriticalMultiplier:
                    case EntityStatsFormulaInput.DefenseMultiplier:
                        return 1f;
                    case EntityStatsFormulaInput.BaseExperience:
                        return 100f;
                    case EntityStatsFormulaInput.ExperiencePerLevel:
                        return 50f;
                    case EntityStatsFormulaInput.MaxAttackSpeed:
                    case EntityStatsFormulaInput.MaxBlockSpeed:
                    case EntityStatsFormulaInput.MaxStunSpeed:
                        return 100f;
                    case EntityStatsFormulaInput.MaxBlockChance:
                        return 0.75f;
                    default:
                        return 0f;
                }
            });
    }
}
