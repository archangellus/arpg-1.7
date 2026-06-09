#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using PLAYERTWO.ARPGProject;

namespace PLAYERTWO.ARPGProjectEditorTools
{
    static class EntityStatsFormulaExampleBuilder
    {
        const string k_aPort = "A";
        const string k_bPort = "B";
        const string k_valuePort = "Value";


        [MenuItem("Tools/PLAYER TWO/ARPG Project/Create Built-In Stats Formula Examples")]
        public static void CreateBuiltInExamplesAsset()
        {
            var path = EditorUtility.SaveFilePanelInProject(
                "Create Built-In Stats Formula Examples",
                "Built-In Stats Formula Examples",
                "asset",
                "Choose where to save the built-in stats formula example graph."
            );

            if (string.IsNullOrEmpty(path))
                return;

            var graph = ScriptableObject.CreateInstance<EntityStatsFormulaGraph>();
            AddOrReplaceAllBuiltInExamples(graph);
            AssetDatabase.CreateAsset(graph, path);
            AssetDatabase.SaveAssets();
            Selection.activeObject = graph;
        }

        public static EntityStatsFormulaData CreateBuiltInExample(EntityStatsFormulaTarget target)
        {
            var builder = new Builder(target);
            builder.Build(target);
            return builder.Formula;
        }

        public static void AddOrReplaceAllBuiltInExamples(EntityStatsFormulaGraph graph)
        {
            foreach (EntityStatsFormulaTarget target in System.Enum.GetValues(typeof(EntityStatsFormulaTarget)))
                AddOrReplaceBuiltInExample(graph, target);
        }

        public static void AddOrReplaceBuiltInExample(
            EntityStatsFormulaGraph graph,
            EntityStatsFormulaTarget target
        )
        {
            var formula = CreateBuiltInExample(target);
            var index = graph.formulas.FindIndex(entry => entry.target == target);

            if (index >= 0)
                graph.formulas[index] = formula;
            else
                graph.formulas.Add(formula);
        }

        class Builder
        {
            readonly Dictionary<EntityStatsFormulaInput, EntityStatsFormulaNodeData> m_inputs = new();
            readonly EntityStatsFormulaData m_formula;
            int m_column;
            int m_row;

            public EntityStatsFormulaData Formula => m_formula;

            public Builder(EntityStatsFormulaTarget target)
            {
                m_formula = new EntityStatsFormulaData
                {
                    target = target,
                    enabled = true,
                };
            }

            public void Build(EntityStatsFormulaTarget target)
            {
                var output = target switch
                {
                    EntityStatsFormulaTarget.MinDamage => BuildMinDamage(),
                    EntityStatsFormulaTarget.MaxDamage => BuildMaxDamage(),
                    EntityStatsFormulaTarget.MinMagicDamage => BuildMinMagicDamage(),
                    EntityStatsFormulaTarget.MaxMagicDamage => BuildMaxMagicDamage(),
                    EntityStatsFormulaTarget.NextLevelExperience => BuildNextLevelExperience(),
                    EntityStatsFormulaTarget.MaxHealth => BuildMaxHealth(),
                    EntityStatsFormulaTarget.MaxMana => BuildMaxMana(),
                    EntityStatsFormulaTarget.AttackSpeed => BuildAttackSpeed(),
                    EntityStatsFormulaTarget.CriticalChance => BuildCriticalChance(),
                    EntityStatsFormulaTarget.Defense => BuildDefense(),
                    EntityStatsFormulaTarget.ChanceToBlock => BuildChanceToBlock(),
                    EntityStatsFormulaTarget.BlockSpeed => BuildBlockSpeed(),
                    EntityStatsFormulaTarget.StunChance => BuildStunChance(),
                    EntityStatsFormulaTarget.StunSpeed => BuildStunSpeed(),
                    EntityStatsFormulaTarget.Accuracy => BuildAccuracy(),
                    EntityStatsFormulaTarget.Evasion => BuildEvasion(),
                    _ => Constant(0),
                };

                Result(output);
            }

            EntityStatsFormulaNodeData BuildMinDamage() =>
                Add(Add(Divide(EffectiveStrength(), Constant(8)), Input(EntityStatsFormulaInput.WeaponDamageMin)), Input(EntityStatsFormulaInput.AdditionalDamage));

            EntityStatsFormulaNodeData BuildMaxDamage() =>
                Add(Add(Divide(EffectiveStrength(), Constant(4)), Input(EntityStatsFormulaInput.WeaponDamageMax)), Input(EntityStatsFormulaInput.AdditionalDamage));

            EntityStatsFormulaNodeData BuildMinMagicDamage() =>
                Add(Divide(EffectiveEnergy(), Constant(4)), Input(EntityStatsFormulaInput.AdditionalMagicDamage));

            EntityStatsFormulaNodeData BuildMaxMagicDamage() =>
                Add(Divide(EffectiveEnergy(), Constant(2)), Input(EntityStatsFormulaInput.AdditionalMagicDamage));

            EntityStatsFormulaNodeData BuildNextLevelExperience() =>
                Add(
                    Input(EntityStatsFormulaInput.BaseExperience),
                    Multiply(
                        Input(EntityStatsFormulaInput.ExperiencePerLevel),
                        Subtract(Input(EntityStatsFormulaInput.Level), Constant(1))
                    )
                );

            EntityStatsFormulaNodeData BuildMaxHealth() =>
                Multiply(
                    Add(
                        Add(
                            Multiply(Input(EntityStatsFormulaInput.Level), Constant(10)),
                            Multiply(EffectiveVitality(), Constant(2))
                        ),
                        Input(EntityStatsFormulaInput.AdditionalHealth)
                    ),
                    Input(EntityStatsFormulaInput.HealthMultiplier)
                );

            EntityStatsFormulaNodeData BuildMaxMana() =>
                Multiply(
                    Add(
                        Add(
                            Multiply(Input(EntityStatsFormulaInput.Level), Constant(5)),
                            Multiply(EffectiveEnergy(), Constant(2))
                        ),
                        Input(EntityStatsFormulaInput.AdditionalMana)
                    ),
                    Input(EntityStatsFormulaInput.ManaMultiplier)
                );

            EntityStatsFormulaNodeData BuildAttackSpeed() =>
                Min(
                    Add(
                        Divide(
                            Add(EffectiveDexterity(), Input(EntityStatsFormulaInput.ItemAttackSpeed)),
                            Constant(10)
                        ),
                        Input(EntityStatsFormulaInput.AdditionalAttackSpeed)
                    ),
                    Input(EntityStatsFormulaInput.MaxAttackSpeed)
                );

            EntityStatsFormulaNodeData BuildCriticalChance() =>
                Multiply(
                    Divide(Add(Divide(EffectiveDexterity(), Constant(10)), Constant(20)), Constant(100)),
                    Input(EntityStatsFormulaInput.CriticalMultiplier)
                );

            EntityStatsFormulaNodeData BuildDefense() =>
                Multiply(
                    Add(
                        Add(Divide(EffectiveDexterity(), Constant(4)), Input(EntityStatsFormulaInput.ItemDefense)),
                        Input(EntityStatsFormulaInput.AdditionalDefense)
                    ),
                    Input(EntityStatsFormulaInput.DefenseMultiplier)
                );

            EntityStatsFormulaNodeData BuildChanceToBlock() =>
                Min(
                    Multiply(
                        Add(
                            Divide(
                                Add(
                                    Add(Divide(EffectiveDexterity(), Constant(20)), Constant(5)),
                                    Input(EntityStatsFormulaInput.Level)
                                ),
                                Constant(100)
                            ),
                            Input(EntityStatsFormulaInput.ItemChanceToBlock)
                        ),
                        Add(
                            Constant(1),
                            Divide(
                                Input(EntityStatsFormulaInput.AdditionalChanceOfBlockingPercent),
                                Constant(100)
                            )
                        )
                    ),
                    Input(EntityStatsFormulaInput.MaxBlockChance)
                );

            EntityStatsFormulaNodeData BuildBlockSpeed() =>
                Min(
                    Multiply(
                        Add(
                            Add(Divide(EffectiveDexterity(), Constant(5)), Constant(100)),
                            Multiply(Input(EntityStatsFormulaInput.Level), Constant(10))
                        ),
                        Add(
                            Constant(1),
                            Divide(
                                Input(EntityStatsFormulaInput.AdditionalBlockRecoveryPercent),
                                Constant(100)
                            )
                        )
                    ),
                    Input(EntityStatsFormulaInput.MaxBlockSpeed)
                );

            EntityStatsFormulaNodeData BuildStunChance() =>
                Min(
                    Multiply(
                        Divide(Add(Divide(EffectiveStrength(), Constant(10)), Input(EntityStatsFormulaInput.Level)), Constant(100)),
                        Add(
                            Constant(1),
                            Divide(
                                Input(EntityStatsFormulaInput.AdditionalChanceToStunPercent),
                                Constant(100)
                            )
                        )
                    ),
                    Input(EntityStatsFormulaInput.MaxStunChance)
                );

            EntityStatsFormulaNodeData BuildStunSpeed() =>
                Min(
                    Multiply(
                        Add(
                            Add(Divide(EffectiveDexterity(), Constant(2)), Constant(100)),
                            Multiply(Input(EntityStatsFormulaInput.Level), Constant(20))
                        ),
                        Add(
                            Constant(1),
                            Divide(
                                Input(EntityStatsFormulaInput.AdditionalStunRecoveryPercent),
                                Constant(100)
                            )
                        )
                    ),
                    Input(EntityStatsFormulaInput.MaxStunSpeed)
                );

            EntityStatsFormulaNodeData BuildAccuracy() =>
                Multiply(
                    Add(
                        Add(
                            Add(
                                Multiply(EffectiveDexterity(), Constant(4)),
                                Multiply(Input(EntityStatsFormulaInput.Level), Constant(10))
                            ),
                            Input(EntityStatsFormulaInput.AccuracyBase)
                        ),
                        Input(EntityStatsFormulaInput.AdditionalAccuracy)
                    ),
                    Add(
                        Constant(1),
                        Divide(Input(EntityStatsFormulaInput.AdditionalAccuracyPercent), Constant(100))
                    )
                );

            EntityStatsFormulaNodeData BuildEvasion() =>
                Multiply(
                    Add(
                        Add(
                            Multiply(EffectiveDexterity(), Constant(2)),
                            Multiply(Input(EntityStatsFormulaInput.Level), Constant(5))
                        ),
                        Input(EntityStatsFormulaInput.AdditionalEvasion)
                    ),
                    Add(
                        Constant(1),
                        Divide(Input(EntityStatsFormulaInput.AdditionalEvasionPercent), Constant(100))
                    )
                );

            EntityStatsFormulaNodeData EffectiveStrength() =>
                Add(Input(EntityStatsFormulaInput.Strength), Input(EntityStatsFormulaInput.AdditionalStrength));

            EntityStatsFormulaNodeData EffectiveDexterity() =>
                Add(Input(EntityStatsFormulaInput.Dexterity), Input(EntityStatsFormulaInput.AdditionalDexterity));

            EntityStatsFormulaNodeData EffectiveVitality() =>
                Add(Input(EntityStatsFormulaInput.Vitality), Input(EntityStatsFormulaInput.AdditionalVitality));

            EntityStatsFormulaNodeData EffectiveEnergy() =>
                Add(Input(EntityStatsFormulaInput.Energy), Input(EntityStatsFormulaInput.AdditionalEnergy));

            EntityStatsFormulaNodeData Input(EntityStatsFormulaInput input)
            {
                if (m_inputs.TryGetValue(input, out var node))
                    return node;

                node = AddNode(EntityStatsFormulaNodeType.Input, NextInputRect());
                node.input = input;
                m_inputs[input] = node;
                return node;
            }

            EntityStatsFormulaNodeData Constant(float value)
            {
                var node = AddNode(EntityStatsFormulaNodeType.Constant, NextInputRect());
                node.constant = value;
                return node;
            }

            EntityStatsFormulaNodeData Add(EntityStatsFormulaNodeData a, EntityStatsFormulaNodeData b) =>
                Operator(EntityStatsFormulaOperator.Add, a, b);

            EntityStatsFormulaNodeData Subtract(EntityStatsFormulaNodeData a, EntityStatsFormulaNodeData b) =>
                Operator(EntityStatsFormulaOperator.Subtract, a, b);

            EntityStatsFormulaNodeData Multiply(EntityStatsFormulaNodeData a, EntityStatsFormulaNodeData b) =>
                Operator(EntityStatsFormulaOperator.Multiply, a, b);

            EntityStatsFormulaNodeData Divide(EntityStatsFormulaNodeData a, EntityStatsFormulaNodeData b) =>
                Operator(EntityStatsFormulaOperator.Divide, a, b);

            EntityStatsFormulaNodeData Min(EntityStatsFormulaNodeData a, EntityStatsFormulaNodeData b) =>
                Operator(EntityStatsFormulaOperator.Min, a, b);

            EntityStatsFormulaNodeData Operator(
                EntityStatsFormulaOperator operation,
                EntityStatsFormulaNodeData a,
                EntityStatsFormulaNodeData b
            )
            {
                var node = AddNode(EntityStatsFormulaNodeType.Operator, NextOperatorRect());
                node.operation = operation;
                Connect(a, node, k_aPort);
                Connect(b, node, k_bPort);
                return node;
            }

            void Result(EntityStatsFormulaNodeData source)
            {
                var result = AddNode(
                    EntityStatsFormulaNodeType.Result,
                    new Rect(760, 220, 120, 72)
                );
                Connect(source, result, k_valuePort);
            }

            EntityStatsFormulaNodeData AddNode(EntityStatsFormulaNodeType type, Rect position)
            {
                var node = new EntityStatsFormulaNodeData
                {
                    type = type,
                    position = position,
                };
                m_formula.nodes.Add(node);
                return node;
            }

            void Connect(
                EntityStatsFormulaNodeData output,
                EntityStatsFormulaNodeData input,
                string inputPortName
            )
            {
                m_formula.connections.Add(new EntityStatsFormulaConnectionData
                {
                    outputNodeGuid = output.guid,
                    inputNodeGuid = input.guid,
                    inputPortName = inputPortName,
                });
            }

            Rect NextInputRect()
            {
                var rect = new Rect(40 + (m_column * 190), 60 + (m_row * 88), 170, 76);
                m_row++;

                if (m_row > 5)
                {
                    m_row = 0;
                    m_column++;
                }

                return rect;
            }

            Rect NextOperatorRect() => new Rect(350 + (m_column++ * 70), 160, 120, 92);
        }
    }
}
#endif
