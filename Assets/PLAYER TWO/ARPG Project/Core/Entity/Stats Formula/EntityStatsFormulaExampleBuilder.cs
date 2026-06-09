using System.Collections.Generic;
using UnityEngine;

namespace PLAYERTWO.ARPGProject
{
    public static class EntityStatsFormulaExampleBuilder
    {
        public static void ApplyBuiltInExamples(
            EntityStatsFormulaGraph graph,
            bool replaceExisting = true
        )
        {
            if (!graph)
                return;

            if (replaceExisting)
                graph.formulas.Clear();

            foreach (EntityStatsFormulaTarget target in System.Enum.GetValues(typeof(EntityStatsFormulaTarget)))
                ApplyBuiltInExample(graph, target);
        }

        public static void ApplyBuiltInExample(
            EntityStatsFormulaGraph graph,
            EntityStatsFormulaTarget target
        )
        {
            if (!graph)
                return;

            AddOrReplace(graph, CreateBuiltInExample(target));
        }

        public static EntityStatsFormulaData CreateBuiltInExample(EntityStatsFormulaTarget target)
        {
            switch (target)
            {
                case EntityStatsFormulaTarget.MinDamage:
                    return BuildDamage(target, true);
                case EntityStatsFormulaTarget.MaxDamage:
                    return BuildDamage(target, false);
                case EntityStatsFormulaTarget.MinMagicDamage:
                    return BuildMagicDamage(target, true);
                case EntityStatsFormulaTarget.MaxMagicDamage:
                    return BuildMagicDamage(target, false);
                case EntityStatsFormulaTarget.NextLevelExperience:
                    return BuildNextLevelExperience();
                case EntityStatsFormulaTarget.MaxHealth:
                    return BuildMaxHealth();
                case EntityStatsFormulaTarget.MaxMana:
                    return BuildMaxMana();
                case EntityStatsFormulaTarget.AttackSpeed:
                    return BuildAttackSpeed();
                case EntityStatsFormulaTarget.CriticalChance:
                    return BuildCriticalChance();
                case EntityStatsFormulaTarget.Defense:
                    return BuildDefense();
                case EntityStatsFormulaTarget.ChanceToBlock:
                    return BuildChanceToBlock();
                case EntityStatsFormulaTarget.BlockSpeed:
                    return BuildBlockSpeed();
                case EntityStatsFormulaTarget.StunChance:
                    return BuildStunChance();
                case EntityStatsFormulaTarget.StunSpeed:
                    return BuildStunSpeed();
                case EntityStatsFormulaTarget.Accuracy:
                    return BuildAccuracy();
                case EntityStatsFormulaTarget.Evasion:
                    return BuildEvasion();
                default:
                    return BuildDamage(EntityStatsFormulaTarget.MinDamage, true);
            }
        }

        static void AddOrReplace(EntityStatsFormulaGraph graph, EntityStatsFormulaData formula)
        {
            graph.formulas.RemoveAll(entry => entry.target == formula.target);
            graph.formulas.Add(formula);
        }

        static EntityStatsFormulaData BuildDamage(EntityStatsFormulaTarget target, bool useMinDamage)
        {
            var builder = new FormulaBuilder(target);
            var effectiveStrength = builder.Op(
                EntityStatsFormulaOperator.Add,
                builder.Input(EntityStatsFormulaInput.Strength, 40, 80),
                builder.Input(EntityStatsFormulaInput.AdditionalStrength, 40, 190),
                260,
                120
            );
            var strengthDamage = builder.Op(
                EntityStatsFormulaOperator.Divide,
                effectiveStrength,
                builder.Constant(useMinDamage ? 8 : 4, 260, 250),
                480,
                150
            );
            var withWeapon = builder.Op(
                EntityStatsFormulaOperator.Add,
                strengthDamage,
                builder.Input(
                    useMinDamage
                        ? EntityStatsFormulaInput.WeaponDamageMin
                        : EntityStatsFormulaInput.WeaponDamageMax,
                    480,
                    20
                ),
                700,
                90
            );
            var total = builder.Op(
                EntityStatsFormulaOperator.Add,
                withWeapon,
                builder.Input(EntityStatsFormulaInput.AdditionalDamage, 700, 220),
                920,
                130
            );
            return builder.Build(total);
        }

        static EntityStatsFormulaData BuildMagicDamage(EntityStatsFormulaTarget target, bool useMinDamage)
        {
            var builder = new FormulaBuilder(target);
            var effectiveEnergy = builder.Op(
                EntityStatsFormulaOperator.Add,
                builder.Input(EntityStatsFormulaInput.Energy, 40, 80),
                builder.Input(EntityStatsFormulaInput.AdditionalEnergy, 40, 190),
                260,
                120
            );
            var energyDamage = builder.Op(
                EntityStatsFormulaOperator.Divide,
                effectiveEnergy,
                builder.Constant(useMinDamage ? 4 : 2, 260, 250),
                480,
                150
            );
            var total = builder.Op(
                EntityStatsFormulaOperator.Add,
                energyDamage,
                builder.Input(EntityStatsFormulaInput.AdditionalMagicDamage, 480, 20),
                700,
                90
            );
            return builder.Build(total);
        }

        static EntityStatsFormulaData BuildNextLevelExperience()
        {
            var builder = new FormulaBuilder(EntityStatsFormulaTarget.NextLevelExperience);
            var levelMinusOne = builder.Op(
                EntityStatsFormulaOperator.Subtract,
                builder.Input(EntityStatsFormulaInput.Level, 40, 80),
                builder.Constant(1, 40, 190),
                260,
                120
            );
            var levelBonus = builder.Op(
                EntityStatsFormulaOperator.Multiply,
                builder.Input(EntityStatsFormulaInput.ExperiencePerLevel, 260, 250),
                levelMinusOne,
                480,
                150
            );
            var total = builder.Op(
                EntityStatsFormulaOperator.Add,
                builder.Input(EntityStatsFormulaInput.BaseExperience, 480, 20),
                levelBonus,
                700,
                90
            );
            return builder.Build(total);
        }

        static EntityStatsFormulaData BuildMaxHealth()
        {
            var builder = new FormulaBuilder(EntityStatsFormulaTarget.MaxHealth);
            var levelHealth = builder.Op(
                EntityStatsFormulaOperator.Multiply,
                builder.Input(EntityStatsFormulaInput.Level, 40, 20),
                builder.Constant(10, 40, 130),
                260,
                70
            );
            var effectiveVitality = builder.Op(
                EntityStatsFormulaOperator.Add,
                builder.Input(EntityStatsFormulaInput.Vitality, 40, 260),
                builder.Input(EntityStatsFormulaInput.AdditionalVitality, 40, 370),
                260,
                310
            );
            var vitalityHealth = builder.Op(
                EntityStatsFormulaOperator.Multiply,
                effectiveVitality,
                builder.Constant(2, 260, 440),
                480,
                350
            );
            var baseAndVitality = builder.Op(
                EntityStatsFormulaOperator.Add,
                levelHealth,
                vitalityHealth,
                700,
                170
            );
            var withFlat = builder.Op(
                EntityStatsFormulaOperator.Add,
                baseAndVitality,
                builder.Input(EntityStatsFormulaInput.AdditionalHealth, 700, 330),
                920,
                220
            );
            var total = builder.Op(
                EntityStatsFormulaOperator.Multiply,
                withFlat,
                builder.Input(EntityStatsFormulaInput.HealthMultiplier, 920, 60),
                1140,
                150
            );
            return builder.Build(total);
        }

        static EntityStatsFormulaData BuildMaxMana()
        {
            var builder = new FormulaBuilder(EntityStatsFormulaTarget.MaxMana);
            var levelMana = builder.Op(
                EntityStatsFormulaOperator.Multiply,
                builder.Input(EntityStatsFormulaInput.Level, 40, 20),
                builder.Constant(5, 40, 130),
                260,
                70
            );
            var effectiveEnergy = builder.Op(
                EntityStatsFormulaOperator.Add,
                builder.Input(EntityStatsFormulaInput.Energy, 40, 260),
                builder.Input(EntityStatsFormulaInput.AdditionalEnergy, 40, 370),
                260,
                310
            );
            var energyMana = builder.Op(
                EntityStatsFormulaOperator.Multiply,
                effectiveEnergy,
                builder.Constant(2, 260, 440),
                480,
                350
            );
            var baseAndEnergy = builder.Op(
                EntityStatsFormulaOperator.Add,
                levelMana,
                energyMana,
                700,
                170
            );
            var withFlat = builder.Op(
                EntityStatsFormulaOperator.Add,
                baseAndEnergy,
                builder.Input(EntityStatsFormulaInput.AdditionalMana, 700, 330),
                920,
                220
            );
            var total = builder.Op(
                EntityStatsFormulaOperator.Multiply,
                withFlat,
                builder.Input(EntityStatsFormulaInput.ManaMultiplier, 920, 60),
                1140,
                150
            );
            return builder.Build(total);
        }

        static EntityStatsFormulaData BuildAttackSpeed()
        {
            var builder = new FormulaBuilder(EntityStatsFormulaTarget.AttackSpeed);
            var effectiveDexterity = EffectiveDexterity(builder, 40, 70);
            var withItemSpeed = builder.Op(
                EntityStatsFormulaOperator.Add,
                effectiveDexterity,
                builder.Input(EntityStatsFormulaInput.ItemAttackSpeed, 260, 220),
                480,
                120
            );
            var divided = builder.Op(
                EntityStatsFormulaOperator.Divide,
                withItemSpeed,
                builder.Constant(10, 480, 260),
                700,
                150
            );
            var withFlat = builder.Op(
                EntityStatsFormulaOperator.Add,
                divided,
                builder.Input(EntityStatsFormulaInput.AdditionalAttackSpeed, 700, 20),
                920,
                90
            );
            var total = builder.Op(
                EntityStatsFormulaOperator.Min,
                withFlat,
                builder.Input(EntityStatsFormulaInput.MaxAttackSpeed, 920, 240),
                1140,
                130
            );
            return builder.Build(total);
        }

        static EntityStatsFormulaData BuildCriticalChance()
        {
            var builder = new FormulaBuilder(EntityStatsFormulaTarget.CriticalChance);
            var effectiveDexterity = EffectiveDexterity(builder, 40, 70);
            var dexterityBonus = builder.Op(
                EntityStatsFormulaOperator.Divide,
                effectiveDexterity,
                builder.Constant(10, 260, 220),
                480,
                120
            );
            var withBase = builder.Op(
                EntityStatsFormulaOperator.Add,
                dexterityBonus,
                builder.Constant(20, 480, 260),
                700,
                150
            );
            var percent = builder.Op(
                EntityStatsFormulaOperator.Divide,
                withBase,
                builder.Constant(100, 700, 20),
                920,
                90
            );
            var total = builder.Op(
                EntityStatsFormulaOperator.Multiply,
                percent,
                builder.Input(EntityStatsFormulaInput.CriticalMultiplier, 920, 240),
                1140,
                130
            );
            return builder.Build(total);
        }

        static EntityStatsFormulaData BuildDefense()
        {
            var builder = new FormulaBuilder(EntityStatsFormulaTarget.Defense);
            var effectiveDexterity = EffectiveDexterity(builder, 40, 70);
            var dexterityDefense = builder.Op(
                EntityStatsFormulaOperator.Divide,
                effectiveDexterity,
                builder.Constant(4, 260, 220),
                480,
                120
            );
            var withItem = builder.Op(
                EntityStatsFormulaOperator.Add,
                dexterityDefense,
                builder.Input(EntityStatsFormulaInput.ItemDefense, 480, 260),
                700,
                150
            );
            var withFlat = builder.Op(
                EntityStatsFormulaOperator.Add,
                withItem,
                builder.Input(EntityStatsFormulaInput.AdditionalDefense, 700, 20),
                920,
                90
            );
            var total = builder.Op(
                EntityStatsFormulaOperator.Multiply,
                withFlat,
                builder.Input(EntityStatsFormulaInput.DefenseMultiplier, 920, 240),
                1140,
                130
            );
            return builder.Build(total);
        }

        static EntityStatsFormulaData BuildChanceToBlock()
        {
            var builder = new FormulaBuilder(EntityStatsFormulaTarget.ChanceToBlock);
            var effectiveDexterity = EffectiveDexterity(builder, 40, 70);
            var dexterityChance = builder.Op(
                EntityStatsFormulaOperator.Divide,
                effectiveDexterity,
                builder.Constant(20, 260, 220),
                480,
                120
            );
            var withBase = builder.Op(
                EntityStatsFormulaOperator.Add,
                dexterityChance,
                builder.Constant(5, 480, 260),
                700,
                150
            );
            var withLevel = builder.Op(
                EntityStatsFormulaOperator.Add,
                withBase,
                builder.Input(EntityStatsFormulaInput.Level, 700, 20),
                920,
                90
            );
            var percent = builder.Op(
                EntityStatsFormulaOperator.Divide,
                withLevel,
                builder.Constant(100, 920, 240),
                1140,
                130
            );
            var withItem = builder.Op(
                EntityStatsFormulaOperator.Add,
                percent,
                builder.Input(EntityStatsFormulaInput.ItemChanceToBlock, 1140, 20),
                1360,
                90
            );
            var multiplier = PercentMultiplier(
                builder,
                EntityStatsFormulaInput.AdditionalChanceOfBlockingPercent,
                1360,
                240
            );
            var multiplied = builder.Op(
                EntityStatsFormulaOperator.Multiply,
                withItem,
                multiplier,
                1580,
                130
            );
            var total = builder.Op(
                EntityStatsFormulaOperator.Min,
                multiplied,
                builder.Input(EntityStatsFormulaInput.MaxBlockChance, 1580, 300),
                1800,
                170
            );
            return builder.Build(total);
        }

        static EntityStatsFormulaData BuildBlockSpeed()
        {
            var builder = new FormulaBuilder(EntityStatsFormulaTarget.BlockSpeed);
            var effectiveDexterity = EffectiveDexterity(builder, 40, 70);
            var dexteritySpeed = builder.Op(
                EntityStatsFormulaOperator.Divide,
                effectiveDexterity,
                builder.Constant(5, 260, 220),
                480,
                120
            );
            var withBase = builder.Op(
                EntityStatsFormulaOperator.Add,
                dexteritySpeed,
                builder.Constant(100, 480, 260),
                700,
                150
            );
            var levelBonus = builder.Op(
                EntityStatsFormulaOperator.Multiply,
                builder.Input(EntityStatsFormulaInput.Level, 700, 20),
                builder.Constant(10, 700, 300),
                920,
                130
            );
            var baseSpeed = builder.Op(
                EntityStatsFormulaOperator.Add,
                withBase,
                levelBonus,
                1140,
                140
            );
            var totalSpeed = builder.Op(
                EntityStatsFormulaOperator.Multiply,
                baseSpeed,
                PercentMultiplier(builder, EntityStatsFormulaInput.AdditionalBlockRecoveryPercent, 1140, 300),
                1360,
                190
            );
            var total = builder.Op(
                EntityStatsFormulaOperator.Min,
                totalSpeed,
                builder.Input(EntityStatsFormulaInput.MaxBlockSpeed, 1360, 20),
                1580,
                120
            );
            return builder.Build(total);
        }

        static EntityStatsFormulaData BuildStunChance()
        {
            var builder = new FormulaBuilder(EntityStatsFormulaTarget.StunChance);
            var effectiveStrength = builder.Op(
                EntityStatsFormulaOperator.Add,
                builder.Input(EntityStatsFormulaInput.Strength, 40, 70),
                builder.Input(EntityStatsFormulaInput.AdditionalStrength, 40, 180),
                260,
                120
            );
            var strengthChance = builder.Op(
                EntityStatsFormulaOperator.Divide,
                effectiveStrength,
                builder.Constant(10, 260, 260),
                480,
                150
            );
            var withLevel = builder.Op(
                EntityStatsFormulaOperator.Add,
                strengthChance,
                builder.Input(EntityStatsFormulaInput.Level, 480, 20),
                700,
                90
            );
            var percent = builder.Op(
                EntityStatsFormulaOperator.Divide,
                withLevel,
                builder.Constant(100, 700, 240),
                920,
                130
            );
            var multiplied = builder.Op(
                EntityStatsFormulaOperator.Multiply,
                percent,
                PercentMultiplier(builder, EntityStatsFormulaInput.AdditionalChanceToStunPercent, 920, 300),
                1140,
                190
            );
            var total = builder.Op(
                EntityStatsFormulaOperator.Min,
                multiplied,
                builder.Input(EntityStatsFormulaInput.MaxStunChance, 1140, 20),
                1360,
                120
            );
            return builder.Build(total);
        }

        static EntityStatsFormulaData BuildStunSpeed()
        {
            var builder = new FormulaBuilder(EntityStatsFormulaTarget.StunSpeed);
            var effectiveDexterity = EffectiveDexterity(builder, 40, 70);
            var dexteritySpeed = builder.Op(
                EntityStatsFormulaOperator.Divide,
                effectiveDexterity,
                builder.Constant(2, 260, 220),
                480,
                120
            );
            var withBase = builder.Op(
                EntityStatsFormulaOperator.Add,
                dexteritySpeed,
                builder.Constant(100, 480, 260),
                700,
                150
            );
            var levelBonus = builder.Op(
                EntityStatsFormulaOperator.Multiply,
                builder.Input(EntityStatsFormulaInput.Level, 700, 20),
                builder.Constant(20, 700, 300),
                920,
                130
            );
            var baseSpeed = builder.Op(
                EntityStatsFormulaOperator.Add,
                withBase,
                levelBonus,
                1140,
                140
            );
            var totalSpeed = builder.Op(
                EntityStatsFormulaOperator.Multiply,
                baseSpeed,
                PercentMultiplier(builder, EntityStatsFormulaInput.AdditionalStunRecoveryPercent, 1140, 300),
                1360,
                190
            );
            var total = builder.Op(
                EntityStatsFormulaOperator.Min,
                totalSpeed,
                builder.Input(EntityStatsFormulaInput.MaxStunSpeed, 1360, 20),
                1580,
                120
            );
            return builder.Build(total);
        }

        static EntityStatsFormulaData BuildAccuracy()
        {
            var builder = new FormulaBuilder(EntityStatsFormulaTarget.Accuracy);
            var effectiveDexterity = EffectiveDexterity(builder, 40, 70);
            var dexterityPart = builder.Op(
                EntityStatsFormulaOperator.Multiply,
                effectiveDexterity,
                builder.Constant(4, 260, 220),
                480,
                120
            );
            var levelPart = builder.Op(
                EntityStatsFormulaOperator.Multiply,
                builder.Input(EntityStatsFormulaInput.Level, 480, 260),
                builder.Constant(10, 480, 370),
                700,
                310
            );
            var withLevel = builder.Op(
                EntityStatsFormulaOperator.Add,
                dexterityPart,
                levelPart,
                920,
                190
            );
            var withBase = builder.Op(
                EntityStatsFormulaOperator.Add,
                withLevel,
                builder.Input(EntityStatsFormulaInput.AccuracyBase, 920, 20),
                1140,
                110
            );
            var withFlat = builder.Op(
                EntityStatsFormulaOperator.Add,
                withBase,
                builder.Input(EntityStatsFormulaInput.AdditionalAccuracy, 1140, 260),
                1360,
                160
            );
            var total = builder.Op(
                EntityStatsFormulaOperator.Multiply,
                withFlat,
                PercentMultiplier(builder, EntityStatsFormulaInput.AdditionalAccuracyPercent, 1360, 320),
                1580,
                220
            );
            return builder.Build(total);
        }

        static EntityStatsFormulaData BuildEvasion()
        {
            var builder = new FormulaBuilder(EntityStatsFormulaTarget.Evasion);
            var effectiveDexterity = EffectiveDexterity(builder, 40, 70);
            var dexterityPart = builder.Op(
                EntityStatsFormulaOperator.Multiply,
                effectiveDexterity,
                builder.Constant(2, 260, 220),
                480,
                120
            );
            var levelPart = builder.Op(
                EntityStatsFormulaOperator.Multiply,
                builder.Input(EntityStatsFormulaInput.Level, 480, 260),
                builder.Constant(5, 480, 370),
                700,
                310
            );
            var withLevel = builder.Op(
                EntityStatsFormulaOperator.Add,
                dexterityPart,
                levelPart,
                920,
                190
            );
            var withFlat = builder.Op(
                EntityStatsFormulaOperator.Add,
                withLevel,
                builder.Input(EntityStatsFormulaInput.AdditionalEvasion, 920, 20),
                1140,
                110
            );
            var total = builder.Op(
                EntityStatsFormulaOperator.Multiply,
                withFlat,
                PercentMultiplier(builder, EntityStatsFormulaInput.AdditionalEvasionPercent, 1140, 260),
                1360,
                160
            );
            return builder.Build(total);
        }

        static EntityStatsFormulaNodeData EffectiveDexterity(
            FormulaBuilder builder,
            float x,
            float y
        ) =>
            builder.Op(
                EntityStatsFormulaOperator.Add,
                builder.Input(EntityStatsFormulaInput.Dexterity, x, y),
                builder.Input(EntityStatsFormulaInput.AdditionalDexterity, x, y + 110),
                x + 220,
                y + 50
            );

        static EntityStatsFormulaNodeData PercentMultiplier(
            FormulaBuilder builder,
            EntityStatsFormulaInput input,
            float x,
            float y
        )
        {
            var percent = builder.Op(
                EntityStatsFormulaOperator.Divide,
                builder.Input(input, x, y),
                builder.Constant(100, x, y + 110),
                x + 220,
                y + 50
            );
            return builder.Op(
                EntityStatsFormulaOperator.Add,
                builder.Constant(1, x + 220, y + 210),
                percent,
                x + 440,
                y + 120
            );
        }

        class FormulaBuilder
        {
            readonly EntityStatsFormulaData m_formula;

            public FormulaBuilder(EntityStatsFormulaTarget target)
            {
                m_formula = new EntityStatsFormulaData { target = target, enabled = false };
            }

            public EntityStatsFormulaNodeData Input(EntityStatsFormulaInput input, float x, float y)
            {
                var node = Node(EntityStatsFormulaNodeType.Input, x, y);
                node.input = input;
                return node;
            }

            public EntityStatsFormulaNodeData Constant(float value, float x, float y)
            {
                var node = Node(EntityStatsFormulaNodeType.Constant, x, y);
                node.constant = value;
                return node;
            }

            public EntityStatsFormulaNodeData Op(
                EntityStatsFormulaOperator operation,
                EntityStatsFormulaNodeData a,
                EntityStatsFormulaNodeData b,
                float x,
                float y
            )
            {
                var node = Node(EntityStatsFormulaNodeType.Operator, x, y);
                node.operation = operation;
                Connect(a, node, "A");
                Connect(b, node, "B");
                return node;
            }

            public EntityStatsFormulaData Build(EntityStatsFormulaNodeData resultInput)
            {
                var result = Node(EntityStatsFormulaNodeType.Result, resultInput.position.x + 240, resultInput.position.y);
                Connect(resultInput, result, "Value");
                return m_formula;
            }

            EntityStatsFormulaNodeData Node(EntityStatsFormulaNodeType type, float x, float y)
            {
                var node = new EntityStatsFormulaNodeData
                {
                    type = type,
                    position = new Rect(x, y, type == EntityStatsFormulaNodeType.Operator ? 120 : 170, 92),
                };
                m_formula.nodes.Add(node);
                return node;
            }

            void Connect(EntityStatsFormulaNodeData output, EntityStatsFormulaNodeData input, string port)
            {
                m_formula.connections.Add(new EntityStatsFormulaConnectionData
                {
                    outputNodeGuid = output.guid,
                    inputNodeGuid = input.guid,
                    inputPortName = port,
                });
            }
        }
    }
}
