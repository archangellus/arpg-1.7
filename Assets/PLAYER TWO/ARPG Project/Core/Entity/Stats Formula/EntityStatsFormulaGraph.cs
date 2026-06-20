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

    public enum EntityStatsFormulaValueType
    {
        Float,
        Integer,
        Percent01,
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
        MaxStunChance,
        AccuracyBase,
        BuiltInValue,
    }

    public enum EntityStatsFormulaNodeType
    {
        Input,
        Constant,
        Operator,
        Result,
        BuiltInValue,
        FormulaReference,
        FormulaFunction,
        PercentConstant,
        Reroute,
        Comment,
    }

    public enum EntityStatsFormulaOperator
    {
        Add,
        Subtract,
        Multiply,
        Divide,
        Min,
        Max,
        Clamp,
        Abs,
        Negate,
        Floor,
        Ceil,
        Round,
        Power,
        Sqrt,
        Log,
        PercentOf,
        Lerp,
        IfGreater,
        IfLess,
        Select,
        NormalizePercent,
    }

    public enum EntityStatsFormulaDiagnosticSeverity
    {
        Info,
        Warning,
        Error,
    }

    [Serializable]
    public class EntityStatsFormulaNodeData
    {
        public string guid = Guid.NewGuid().ToString();
        public EntityStatsFormulaNodeType type;
        public EntityStatsFormulaInput input;
        public EntityStatsFormulaTarget formulaTarget;
        public EntityStatsFormulaFunction function;
        public EntityStatsFormulaOperator operation = EntityStatsFormulaOperator.Add;
        public float constant;
        public string title;
        [TextArea] public string note;
        public string groupId;
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
    public class EntityStatsFormulaGroupData
    {
        public string guid = Guid.NewGuid().ToString();
        public string title = "Group";
        public string description;
        public bool collapsed;
        public Color color = new Color(0.25f, 0.25f, 0.25f, 0.35f);
        public Rect position = new Rect(80, 80, 320, 240);
    }

    [Serializable]
    public class EntityStatsFormulaData
    {
        public EntityStatsFormulaTarget target;
        public bool enabled = true;
        public List<EntityStatsFormulaNodeData> nodes = new();
        public List<EntityStatsFormulaConnectionData> connections = new();
        public List<EntityStatsFormulaGroupData> groups = new();
        public string description;

        [NonSerialized] EntityStatsFormulaCompiledData m_compiled;

        public EntityStatsFormulaNodeData GetResultNode() =>
            nodes.Find(node => node.type == EntityStatsFormulaNodeType.Result);

        public EntityStatsFormulaCompiledData Compile()
        {
            if (m_compiled == null)
                m_compiled = new EntityStatsFormulaCompiledData(this);

            return m_compiled;
        }

        public void InvalidateCache() => m_compiled = null;

        public bool EnsureValid(string ownerName = null)
        {
            nodes ??= new List<EntityStatsFormulaNodeData>();
            connections ??= new List<EntityStatsFormulaConnectionData>();
            groups ??= new List<EntityStatsFormulaGroupData>();

            var repaired = false;
            var seenNodes = new HashSet<string>();
            var resultFound = false;

            foreach (var node in nodes)
            {
                if (node == null)
                    continue;

                if (string.IsNullOrEmpty(node.guid) || !seenNodes.Add(node.guid))
                {
                    node.guid = Guid.NewGuid().ToString();
                    repaired = true;
                }

                if (node.type == EntityStatsFormulaNodeType.Result)
                {
                    if (resultFound)
                    {
                        node.type = EntityStatsFormulaNodeType.Reroute;
                        repaired = true;
                    }
                    else
                    {
                        resultFound = true;
                    }
                }
            }

            if (!resultFound)
            {
                nodes.Add(new EntityStatsFormulaNodeData
                {
                    type = EntityStatsFormulaNodeType.Result,
                    position = new Rect(520, 180, 120, 72),
                });
                repaired = true;
            }

            var validNodeGuids = new HashSet<string>();
            foreach (var node in nodes)
            {
                if (node != null)
                    validNodeGuids.Add(node.guid);
            }

            var seenInputs = new HashSet<string>();
            for (var i = connections.Count - 1; i >= 0; i--)
            {
                var connection = connections[i];

                if (
                    connection == null
                    || !validNodeGuids.Contains(connection.outputNodeGuid)
                    || !validNodeGuids.Contains(connection.inputNodeGuid)
                )
                {
                    connections.RemoveAt(i);
                    repaired = true;
                    continue;
                }

                var key = EntityStatsFormulaCompiledData.GetConnectionKey(
                    connection.inputNodeGuid,
                    connection.inputPortName
                );

                if (!seenInputs.Add(key))
                {
                    connections.RemoveAt(i);
                    repaired = true;
                }
            }

            if (repaired)
            {
                InvalidateCache();
                Debug.LogWarning($"Repaired invalid stats formula data{(string.IsNullOrEmpty(ownerName) ? string.Empty : $" in {ownerName}")}.");
            }

            return repaired;
        }
    }

    public sealed class EntityStatsFormulaCompiledData
    {
        public readonly Dictionary<string, EntityStatsFormulaNodeData> nodesByGuid = new();
        public readonly Dictionary<string, EntityStatsFormulaConnectionData> inputConnections = new();
        public readonly EntityStatsFormulaNodeData resultNode;

        public EntityStatsFormulaCompiledData(EntityStatsFormulaData formula)
        {
            if (formula == null)
                return;

            foreach (var node in formula.nodes)
            {
                if (node == null || string.IsNullOrEmpty(node.guid))
                    continue;

                nodesByGuid[node.guid] = node;

                if (node.type == EntityStatsFormulaNodeType.Result && resultNode == null)
                    resultNode = node;
            }

            foreach (var connection in formula.connections)
            {
                if (connection == null)
                    continue;

                inputConnections[GetConnectionKey(connection.inputNodeGuid, connection.inputPortName)] = connection;
            }
        }

        public static string GetConnectionKey(string inputNodeGuid, string inputPortName) =>
            $"{inputNodeGuid}:{inputPortName}";
    }

    public readonly struct EntityStatsFormulaTargetMetadata
    {
        public readonly EntityStatsFormulaValueType valueType;
        public readonly float min;
        public readonly float max;
        public readonly bool hasMin;
        public readonly bool hasMax;
        public readonly string unitLabel;

        public EntityStatsFormulaTargetMetadata(
            EntityStatsFormulaValueType valueType,
            float min = 0f,
            float max = 0f,
            bool hasMin = true,
            bool hasMax = false,
            string unitLabel = ""
        )
        {
            this.valueType = valueType;
            this.min = min;
            this.max = max;
            this.hasMin = hasMin;
            this.hasMax = hasMax;
            this.unitLabel = unitLabel;
        }

        public float Normalize(float value)
        {
            if (float.IsNaN(value) || float.IsInfinity(value))
                value = 0f;

            if (valueType == EntityStatsFormulaValueType.Integer)
                value = Mathf.RoundToInt(value);

            if (hasMin)
                value = Mathf.Max(min, value);

            if (hasMax)
                value = Mathf.Min(max, value);

            return value;
        }

        public string Format(float value) =>
            valueType == EntityStatsFormulaValueType.Percent01
                ? $"{value:0.###} ({value * 100f:0.#}%)"
                : $"{value:0.###}{unitLabel}";
    }

    public static class EntityStatsFormulaTargetMetadataProvider
    {
        public static EntityStatsFormulaTargetMetadata Get(
            EntityStatsFormulaTarget target,
            EntityStatsFormulaContext context = default
        )
        {
            switch (target)
            {
                case EntityStatsFormulaTarget.NextLevelExperience:
                case EntityStatsFormulaTarget.MaxHealth:
                case EntityStatsFormulaTarget.MaxMana:
                case EntityStatsFormulaTarget.Defense:
                case EntityStatsFormulaTarget.Accuracy:
                case EntityStatsFormulaTarget.Evasion:
                case EntityStatsFormulaTarget.MinDamage:
                case EntityStatsFormulaTarget.MaxDamage:
                case EntityStatsFormulaTarget.MinMagicDamage:
                case EntityStatsFormulaTarget.MaxMagicDamage:
                    return new EntityStatsFormulaTargetMetadata(EntityStatsFormulaValueType.Integer);
                case EntityStatsFormulaTarget.AttackSpeed:
                    return new EntityStatsFormulaTargetMetadata(
                        EntityStatsFormulaValueType.Integer,
                        max: context.Get(EntityStatsFormulaInput.MaxAttackSpeed),
                        hasMax: true
                    );
                case EntityStatsFormulaTarget.ChanceToBlock:
                    return new EntityStatsFormulaTargetMetadata(
                        EntityStatsFormulaValueType.Percent01,
                        max: context.Get(EntityStatsFormulaInput.MaxBlockChance),
                        hasMax: true
                    );
                case EntityStatsFormulaTarget.StunChance:
                    return new EntityStatsFormulaTargetMetadata(
                        EntityStatsFormulaValueType.Percent01,
                        max: context.Get(EntityStatsFormulaInput.MaxStunChance),
                        hasMax: true
                    );
                case EntityStatsFormulaTarget.CriticalChance:
                    return new EntityStatsFormulaTargetMetadata(
                        EntityStatsFormulaValueType.Percent01,
                        max: 1f,
                        hasMax: true
                    );
                case EntityStatsFormulaTarget.BlockSpeed:
                    return new EntityStatsFormulaTargetMetadata(
                        EntityStatsFormulaValueType.Integer,
                        max: context.Get(EntityStatsFormulaInput.MaxBlockSpeed),
                        hasMax: true
                    );
                case EntityStatsFormulaTarget.StunSpeed:
                    return new EntityStatsFormulaTargetMetadata(
                        EntityStatsFormulaValueType.Integer,
                        max: context.Get(EntityStatsFormulaInput.MaxStunSpeed),
                        hasMax: true
                    );
                default:
                    return new EntityStatsFormulaTargetMetadata(EntityStatsFormulaValueType.Float);
            }
        }
    }

    [Serializable]
    public class EntityStatsFormulaDiagnostic
    {
        public EntityStatsFormulaDiagnosticSeverity severity;
        public string message;
        public string nodeGuid;
        public string portName;
    }

    public class EntityStatsFormulaValidationResult
    {
        public readonly List<EntityStatsFormulaDiagnostic> diagnostics = new();
        public bool hasErrors => diagnostics.Exists(entry => entry.severity == EntityStatsFormulaDiagnosticSeverity.Error);
        public bool hasWarnings => diagnostics.Exists(entry => entry.severity == EntityStatsFormulaDiagnosticSeverity.Warning);

        public void Add(
            EntityStatsFormulaDiagnosticSeverity severity,
            string message,
            string nodeGuid = null,
            string portName = null
        ) => diagnostics.Add(new EntityStatsFormulaDiagnostic
        {
            severity = severity,
            message = message,
            nodeGuid = nodeGuid,
            portName = portName,
        });

        public string Summary
        {
            get
            {
                if (diagnostics.Count == 0)
                    return "Valid";

                var errors = diagnostics.FindAll(entry => entry.severity == EntityStatsFormulaDiagnosticSeverity.Error).Count;
                var warnings = diagnostics.FindAll(entry => entry.severity == EntityStatsFormulaDiagnosticSeverity.Warning).Count;
                return $"{errors} error(s), {warnings} warning(s)";
            }
        }
    }

    [CreateAssetMenu(
        fileName = "New Entity Stats Formula Function",
        menuName = "PLAYER TWO/ARPG Project/Entity/Stats Formula Function"
    )]
    public class EntityStatsFormulaFunction : ScriptableObject
    {
        public EntityStatsFormulaData formula = new EntityStatsFormulaData();

        public bool TryEvaluate(EntityStatsFormulaContext context, out float value) =>
            EntityStatsFormulaEvaluator.TryEvaluateFunction(this, context, out value);

        void OnValidate()
        {
            formula ??= new EntityStatsFormulaData();
            formula.enabled = true;
            formula.EnsureValid(name);
            formula.InvalidateCache();
        }
    }

    [CreateAssetMenu(
        fileName = "New Entity Stats Formula Graph",
        menuName = "PLAYER TWO/ARPG Project/Entity/Stats Formula Graph"
    )]
    public class EntityStatsFormulaGraph : ScriptableObject
    {
        public const int CurrentSchemaVersion = 2;

        public int schemaVersion = CurrentSchemaVersion;
        public List<EntityStatsFormulaData> formulas = new();

        [NonSerialized] Dictionary<EntityStatsFormulaTarget, EntityStatsFormulaData> m_formulaByTarget;

        public bool TryEvaluate(
            EntityStatsFormulaTarget target,
            EntityStatsFormulaContext context,
            out float value
        ) => TryEvaluate(target, context.WithTarget(target), null, out value);

        public bool TryEvaluate(
            EntityStatsFormulaTarget target,
            EntityStatsFormulaContext context,
            HashSet<EntityStatsFormulaTarget> visitingTargets,
            out float value
        )
        {
            value = 0f;
            RebuildRuntimeCacheIfNeeded();

            if (
                m_formulaByTarget == null
                || !m_formulaByTarget.TryGetValue(target, out var formula)
                || formula == null
                || !formula.enabled
            )
                return false;

            return EntityStatsFormulaEvaluator.TryEvaluate(
                formula,
                context.WithTarget(target),
                visitingTargets,
                out value
            );
        }

        public EntityStatsFormulaValidationResult ValidateFormula(EntityStatsFormulaTarget target)
        {
            RebuildRuntimeCacheIfNeeded();
            return m_formulaByTarget != null && m_formulaByTarget.TryGetValue(target, out var formula)
                ? EntityStatsFormulaValidator.Validate(formula)
                : EntityStatsFormulaValidator.Validate(null);
        }

        public EntityStatsFormulaValidationResult ValidateGraph() =>
            EntityStatsFormulaValidator.ValidateGraph(this);


        public string ExportJson(bool prettyPrint = true) =>
            JsonUtility.ToJson(this, prettyPrint);

        public void ImportJson(string json)
        {
            if (string.IsNullOrEmpty(json))
                return;

            JsonUtility.FromJsonOverwrite(json, this);
            EnsureValidSerializedData();
            InvalidateRuntimeCache();
        }

        public void InvalidateRuntimeCache()
        {
            m_formulaByTarget = null;

            foreach (var formula in formulas)
                formula?.InvalidateCache();
        }

        void RebuildRuntimeCacheIfNeeded()
        {
            if (m_formulaByTarget != null)
                return;

            m_formulaByTarget = new Dictionary<EntityStatsFormulaTarget, EntityStatsFormulaData>();

            foreach (var formula in formulas)
            {
                if (formula == null)
                    continue;

                m_formulaByTarget[formula.target] = formula;
            }
        }

        void OnValidate()
        {
            schemaVersion = Mathf.Max(schemaVersion, CurrentSchemaVersion);
            EnsureValidSerializedData();
            InvalidateRuntimeCache();
        }

        public void EnsureValidSerializedData()
        {
            formulas ??= new List<EntityStatsFormulaData>();
            formulas.RemoveAll(formula => formula == null);

            var seenTargets = new HashSet<EntityStatsFormulaTarget>();

            for (var i = formulas.Count - 1; i >= 0; i--)
            {
                var formula = formulas[i];

                if (!seenTargets.Add(formula.target))
                {
                    formulas.RemoveAt(i);
                    continue;
                }

                EnsureFormulaData(formula);
            }
        }

        static void EnsureFormulaData(EntityStatsFormulaData formula) => formula?.EnsureValid();

    }
}
