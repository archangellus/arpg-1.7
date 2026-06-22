using System;
using System.Collections.Generic;
using UnityEngine;

namespace PLAYERTWO.ARPGProject
{
    public delegate bool EntityStatsFormulaReferenceResolver(
        EntityStatsFormulaTarget target,
        HashSet<EntityStatsFormulaTarget> visitingTargets,
        out float value
    );

    public readonly struct EntityStatsFormulaContext
    {
        readonly Func<EntityStatsFormulaInput, float> m_getValue;
        readonly EntityStatsFormulaReferenceResolver m_resolveReference;

        public readonly EntityStatsFormulaTarget target;
        public readonly float builtInValue;
        public readonly bool hasBuiltInValue;

        public EntityStatsFormulaContext(Func<EntityStatsFormulaInput, float> getValue)
            : this(default, 0f, false, getValue, null) { }

        public EntityStatsFormulaContext(
            EntityStatsFormulaTarget target,
            float builtInValue,
            Func<EntityStatsFormulaInput, float> getValue,
            EntityStatsFormulaReferenceResolver resolveReference = null
        ) : this(target, builtInValue, true, getValue, resolveReference) { }

        EntityStatsFormulaContext(
            EntityStatsFormulaTarget target,
            float builtInValue,
            bool hasBuiltInValue,
            Func<EntityStatsFormulaInput, float> getValue,
            EntityStatsFormulaReferenceResolver resolveReference
        )
        {
            this.target = target;
            this.builtInValue = builtInValue;
            this.hasBuiltInValue = hasBuiltInValue;
            m_getValue = getValue;
            m_resolveReference = resolveReference;
        }

        public float Get(EntityStatsFormulaInput input)
        {
            if (input == EntityStatsFormulaInput.BuiltInValue)
                return hasBuiltInValue ? builtInValue : 0f;

            return m_getValue != null ? m_getValue(input) : 0f;
        }

        public bool TryResolveFormulaReference(
            EntityStatsFormulaTarget referenceTarget,
            HashSet<EntityStatsFormulaTarget> visitingTargets,
            out float value
        )
        {
            value = 0f;
            return m_resolveReference != null
                && m_resolveReference(referenceTarget, visitingTargets, out value);
        }

        public EntityStatsFormulaContext WithTarget(EntityStatsFormulaTarget newTarget) =>
            new EntityStatsFormulaContext(
                newTarget,
                builtInValue,
                hasBuiltInValue,
                m_getValue,
                m_resolveReference
            );

        public EntityStatsFormulaContext WithBuiltInValue(
            EntityStatsFormulaTarget newTarget,
            float newBuiltInValue
        ) => new EntityStatsFormulaContext(
            newTarget,
            newBuiltInValue,
            true,
            m_getValue,
            m_resolveReference
        );

        public EntityStatsFormulaContext WithReferenceResolver(
            EntityStatsFormulaReferenceResolver resolver
        ) => new EntityStatsFormulaContext(
            target,
            builtInValue,
            hasBuiltInValue,
            m_getValue,
            resolver
        );

        public static EntityStatsFormulaContext Preview =>
            new EntityStatsFormulaContext(
                EntityStatsFormulaTarget.MaxHealth,
                100f,
                input =>
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
                        case EntityStatsFormulaInput.MaxStunChance:
                            return 0.75f;
                        case EntityStatsFormulaInput.AccuracyBase:
                            return 300f;
                        case EntityStatsFormulaInput.BuiltInValue:
                            return 100f;
                        default:
                            return 0f;
                    }
                },
                (
                    EntityStatsFormulaTarget referenceTarget,
                    HashSet<EntityStatsFormulaTarget> visitingTargets,
                    out float value
                ) =>
                {
                    value = 0f;
                    return false;
                }
            );
    }
}
