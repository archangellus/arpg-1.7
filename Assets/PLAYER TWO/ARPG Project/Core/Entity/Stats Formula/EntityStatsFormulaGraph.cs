using System;
using System.Collections.Generic;
using UnityEngine;

namespace PLAYERTWO.ARPGProject
{
    public enum EntityStatsFormulaTarget
    {
        MinDamage,
        MaxDamage,
        MinMagicDamage,
        MaxMagicDamage,
        NextLevelExperience,
        MaxHealth,
        MaxMana,
        AttackSpeed,
        CriticalChance,
        Defense,
        ChanceToBlock,
        BlockSpeed,
        StunChance,
        StunSpeed,
        Accuracy,
        Evasion,
    }

    public enum EntityStatsFormulaInput
    {
        Level,
        Strength,
        Dexterity,
        Vitality,
        Energy,
        WeaponDamageMin,
        WeaponDamageMax,
        ItemDefense,
        ItemAttackSpeed,
        ItemChanceToBlock,
        AdditionalDamage,
        AdditionalMagicDamage,
        AdditionalHealth,
        AdditionalMana,
        AdditionalStrength,
        AdditionalDexterity,
        AdditionalVitality,
        AdditionalEnergy,
        AdditionalAttackSpeed,
        AdditionalDefense,
        AdditionalChanceOfBlockingPercent,
        AdditionalBlockRecoveryPercent,
        AdditionalChanceToStunPercent,
        AdditionalStunRecoveryPercent,
        AdditionalAccuracy,
        AdditionalAccuracyPercent,
        AdditionalEvasion,
        AdditionalEvasionPercent,
        HealthMultiplier,
        ManaMultiplier,
        DamageMultiplier,
        MagicDamageMultiplier,
        CriticalMultiplier,
        DefenseMultiplier,
        BaseExperience,
        ExperiencePerLevel,
        MaxAttackSpeed,
        MaxBlockChance,
        MaxBlockSpeed,
        MaxStunSpeed,
    }

    public enum EntityStatsFormulaNodeType
    {
        Input,
        Constant,
        Operator,
        Result,
    }

    public enum EntityStatsFormulaOperator
    {
        Add,
        Subtract,
        Multiply,
        Divide,
        Min,
        Max,
    }

    [Serializable]
    public class EntityStatsFormulaNodeData
    {
        public string guid = Guid.NewGuid().ToString();
        public EntityStatsFormulaNodeType type;
        public EntityStatsFormulaInput input;
        public EntityStatsFormulaOperator operation = EntityStatsFormulaOperator.Add;
        public float constant;
        public Rect position = new Rect(100, 100, 160, 88);
    }

    [Serializable]
    public class EntityStatsFormulaConnectionData
    {
        public string outputNodeGuid;
        public string inputNodeGuid;
        public string inputPortName;
    }

    [Serializable]
    public class EntityStatsFormulaData
    {
        public EntityStatsFormulaTarget target;
        public bool enabled = true;
        public List<EntityStatsFormulaNodeData> nodes = new();
        public List<EntityStatsFormulaConnectionData> connections = new();

        public EntityStatsFormulaNodeData GetResultNode() =>
            nodes.Find(node => node.type == EntityStatsFormulaNodeType.Result);
    }

    [CreateAssetMenu(
        fileName = "New Entity Stats Formula Graph",
        menuName = "PLAYER TWO/ARPG Project/Entity/Stats Formula Graph"
    )]
    public class EntityStatsFormulaGraph : ScriptableObject
    {
        public List<EntityStatsFormulaData> formulas = new();

        public bool TryEvaluate(
            EntityStatsFormulaTarget target,
            EntityStatsFormulaContext context,
            out float value
        )
        {
            value = 0f;
            var formula = formulas.Find(entry => entry.enabled && entry.target == target);

            if (formula == null)
                return false;

            return EntityStatsFormulaEvaluator.TryEvaluate(formula, context, out value);
        }
    }
}
