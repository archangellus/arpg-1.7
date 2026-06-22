using System.Collections.Generic;
using System.Linq;

namespace PLAYERTWO.ARPGProject
{
    public static class EntityStatsFormulaValidator
    {
        public static EntityStatsFormulaValidationResult Validate(EntityStatsFormulaData formula)
        {
            var result = new EntityStatsFormulaValidationResult();

            if (formula == null)
            {
                result.Add(EntityStatsFormulaDiagnosticSeverity.Error, "Formula is missing.");
                return result;
            }

            if (!formula.enabled)
                result.Add(EntityStatsFormulaDiagnosticSeverity.Info, "Formula is disabled and will fall back to the built-in calculation.");

            var nodes = formula.nodes ?? new List<EntityStatsFormulaNodeData>();
            var connections = formula.connections ?? new List<EntityStatsFormulaConnectionData>();
            var nodesByGuid = new Dictionary<string, EntityStatsFormulaNodeData>();
            var resultNodes = 0;

            foreach (var node in nodes)
            {
                if (node == null)
                    continue;

                if (string.IsNullOrEmpty(node.guid))
                {
                    result.Add(EntityStatsFormulaDiagnosticSeverity.Error, "Node has no GUID.");
                    continue;
                }

                if (nodesByGuid.ContainsKey(node.guid))
                    result.Add(EntityStatsFormulaDiagnosticSeverity.Error, "Duplicate node GUID found.", node.guid);
                else
                    nodesByGuid[node.guid] = node;

                if (node.type == EntityStatsFormulaNodeType.Result)
                    resultNodes++;
            }

            if (resultNodes == 0)
                result.Add(EntityStatsFormulaDiagnosticSeverity.Error, "Formula has no Result node.");
            else if (resultNodes > 1)
                result.Add(EntityStatsFormulaDiagnosticSeverity.Error, "Formula has more than one Result node.");

            var inputConnections = new Dictionary<string, EntityStatsFormulaConnectionData>();
            var outputNodeGuids = new HashSet<string>();

            foreach (var connection in connections)
            {
                if (connection == null)
                    continue;

                if (!nodesByGuid.ContainsKey(connection.outputNodeGuid))
                {
                    result.Add(
                        EntityStatsFormulaDiagnosticSeverity.Error,
                        "Connection references a missing output node.",
                        connection.outputNodeGuid
                    );
                    continue;
                }

                if (!nodesByGuid.ContainsKey(connection.inputNodeGuid))
                {
                    result.Add(
                        EntityStatsFormulaDiagnosticSeverity.Error,
                        "Connection references a missing input node.",
                        connection.inputNodeGuid,
                        connection.inputPortName
                    );
                    continue;
                }

                var key = EntityStatsFormulaCompiledData.GetConnectionKey(
                    connection.inputNodeGuid,
                    connection.inputPortName
                );

                if (inputConnections.ContainsKey(key))
                    result.Add(
                        EntityStatsFormulaDiagnosticSeverity.Error,
                        "Input port has more than one connection.",
                        connection.inputNodeGuid,
                        connection.inputPortName
                    );
                else
                    inputConnections[key] = connection;

                outputNodeGuids.Add(connection.outputNodeGuid);
            }

            foreach (var node in nodes)
            {
                if (node == null || node.type == EntityStatsFormulaNodeType.Comment)
                    continue;

                foreach (var requiredPort in GetRequiredInputPorts(node))
                {
                    var key = EntityStatsFormulaCompiledData.GetConnectionKey(node.guid, requiredPort);
                    if (!inputConnections.ContainsKey(key))
                    {
                        result.Add(
                            EntityStatsFormulaDiagnosticSeverity.Error,
                            $"Required input '{requiredPort}' is not connected.",
                            node.guid,
                            requiredPort
                        );
                    }
                }

                if (node.type == EntityStatsFormulaNodeType.FormulaFunction && !node.function)
                    result.Add(EntityStatsFormulaDiagnosticSeverity.Error, "Formula function node has no function asset assigned.", node.guid);

                if (
                    node.type != EntityStatsFormulaNodeType.Result
                    && node.type != EntityStatsFormulaNodeType.Comment
                    && !outputNodeGuids.Contains(node.guid)
                )
                    result.Add(EntityStatsFormulaDiagnosticSeverity.Warning, "Node output is not used.", node.guid);
            }

            DetectCycles(nodesByGuid, inputConnections.Values.ToList(), result);
            return result;
        }


        public static EntityStatsFormulaValidationResult ValidateGraph(EntityStatsFormulaGraph graph)
        {
            var result = new EntityStatsFormulaValidationResult();

            if (!graph)
            {
                result.Add(EntityStatsFormulaDiagnosticSeverity.Error, "Formula graph is missing.");
                return result;
            }

            var formulasByTarget = new Dictionary<EntityStatsFormulaTarget, EntityStatsFormulaData>();

            foreach (var formula in graph.formulas ?? new List<EntityStatsFormulaData>())
            {
                if (formula != null)
                    formulasByTarget[formula.target] = formula;
            }

            foreach (var formula in formulasByTarget.Values)
                DetectCrossFormulaCycles(formula, formulasByTarget, new HashSet<EntityStatsFormulaTarget>(), new HashSet<EntityStatsFormulaFunction>(), result);

            return result;
        }

        static void DetectCrossFormulaCycles(
            EntityStatsFormulaData formula,
            Dictionary<EntityStatsFormulaTarget, EntityStatsFormulaData> formulasByTarget,
            HashSet<EntityStatsFormulaTarget> visitingTargets,
            HashSet<EntityStatsFormulaFunction> visitingFunctions,
            EntityStatsFormulaValidationResult result
        )
        {
            if (formula == null || !visitingTargets.Add(formula.target))
            {
                if (formula != null)
                    result.Add(EntityStatsFormulaDiagnosticSeverity.Error, $"Formula reference cycle includes target '{formula.target}'.");
                return;
            }

            foreach (var node in formula.nodes ?? new List<EntityStatsFormulaNodeData>())
            {
                if (node == null)
                    continue;

                if (node.type == EntityStatsFormulaNodeType.FormulaReference && formulasByTarget.TryGetValue(node.formulaTarget, out var referencedFormula))
                    DetectCrossFormulaCycles(referencedFormula, formulasByTarget, visitingTargets, visitingFunctions, result);
                else if (node.type == EntityStatsFormulaNodeType.FormulaFunction)
                    DetectFunctionCycles(node.function, formulasByTarget, visitingTargets, visitingFunctions, result, node.guid);
            }

            visitingTargets.Remove(formula.target);
        }

        static void DetectFunctionCycles(
            EntityStatsFormulaFunction function,
            Dictionary<EntityStatsFormulaTarget, EntityStatsFormulaData> formulasByTarget,
            HashSet<EntityStatsFormulaTarget> visitingTargets,
            HashSet<EntityStatsFormulaFunction> visitingFunctions,
            EntityStatsFormulaValidationResult result,
            string nodeGuid
        )
        {
            if (!function)
                return;

            if (!visitingFunctions.Add(function))
            {
                result.Add(EntityStatsFormulaDiagnosticSeverity.Error, $"Formula function cycle includes '{function.name}'.", nodeGuid);
                return;
            }

            foreach (var node in function.formula?.nodes ?? new List<EntityStatsFormulaNodeData>())
            {
                if (node == null)
                    continue;

                if (node.type == EntityStatsFormulaNodeType.FormulaFunction)
                    DetectFunctionCycles(node.function, formulasByTarget, visitingTargets, visitingFunctions, result, node.guid);
                else if (node.type == EntityStatsFormulaNodeType.FormulaReference && formulasByTarget.TryGetValue(node.formulaTarget, out var referencedFormula))
                    DetectCrossFormulaCycles(referencedFormula, formulasByTarget, visitingTargets, visitingFunctions, result);
            }

            visitingFunctions.Remove(function);
        }

        public static IReadOnlyList<string> GetRequiredInputPorts(EntityStatsFormulaNodeData node)
        {
            if (node == null)
                return System.Array.Empty<string>();

            switch (node.type)
            {
                case EntityStatsFormulaNodeType.Result:
                case EntityStatsFormulaNodeType.Reroute:
                    return new[] { EntityStatsFormulaEvaluator.ValuePort };
                case EntityStatsFormulaNodeType.Operator:
                    return GetOperatorInputPorts(node.operation);
                default:
                    return System.Array.Empty<string>();
            }
        }

        public static IReadOnlyList<string> GetOperatorInputPorts(EntityStatsFormulaOperator operation)
        {
            switch (operation)
            {
                case EntityStatsFormulaOperator.Clamp:
                    return new[] { EntityStatsFormulaEvaluator.ValuePort, EntityStatsFormulaEvaluator.MinPort, EntityStatsFormulaEvaluator.MaxPort };
                case EntityStatsFormulaOperator.Abs:
                case EntityStatsFormulaOperator.Negate:
                case EntityStatsFormulaOperator.Floor:
                case EntityStatsFormulaOperator.Ceil:
                case EntityStatsFormulaOperator.Round:
                case EntityStatsFormulaOperator.Sqrt:
                case EntityStatsFormulaOperator.Log:
                case EntityStatsFormulaOperator.NormalizePercent:
                    return new[] { EntityStatsFormulaEvaluator.ValuePort };
                case EntityStatsFormulaOperator.PercentOf:
                    return new[] { EntityStatsFormulaEvaluator.ValuePort, EntityStatsFormulaEvaluator.PercentPort };
                case EntityStatsFormulaOperator.Lerp:
                    return new[] { EntityStatsFormulaEvaluator.APort, EntityStatsFormulaEvaluator.BPort, EntityStatsFormulaEvaluator.TPort };
                case EntityStatsFormulaOperator.IfGreater:
                case EntityStatsFormulaOperator.IfLess:
                    return new[] { EntityStatsFormulaEvaluator.APort, EntityStatsFormulaEvaluator.BPort, EntityStatsFormulaEvaluator.TruePort, EntityStatsFormulaEvaluator.FalsePort };
                case EntityStatsFormulaOperator.Select:
                    return new[] { EntityStatsFormulaEvaluator.ValuePort, EntityStatsFormulaEvaluator.TruePort, EntityStatsFormulaEvaluator.FalsePort };
                default:
                    return new[] { EntityStatsFormulaEvaluator.APort, EntityStatsFormulaEvaluator.BPort };
            }
        }

        static void DetectCycles(
            Dictionary<string, EntityStatsFormulaNodeData> nodesByGuid,
            List<EntityStatsFormulaConnectionData> connections,
            EntityStatsFormulaValidationResult result
        )
        {
            var outgoing = new Dictionary<string, List<string>>();

            foreach (var connection in connections)
            {
                if (connection == null)
                    continue;

                if (!outgoing.TryGetValue(connection.outputNodeGuid, out var list))
                {
                    list = new List<string>();
                    outgoing[connection.outputNodeGuid] = list;
                }

                list.Add(connection.inputNodeGuid);
            }

            var visited = new HashSet<string>();
            var visiting = new HashSet<string>();

            foreach (var nodeGuid in nodesByGuid.Keys)
                Visit(nodeGuid, outgoing, visited, visiting, result);
        }

        static bool Visit(
            string nodeGuid,
            Dictionary<string, List<string>> outgoing,
            HashSet<string> visited,
            HashSet<string> visiting,
            EntityStatsFormulaValidationResult result
        )
        {
            if (visited.Contains(nodeGuid))
                return false;

            if (!visiting.Add(nodeGuid))
            {
                result.Add(EntityStatsFormulaDiagnosticSeverity.Error, "Formula contains a cycle.", nodeGuid);
                return true;
            }

            if (outgoing.TryGetValue(nodeGuid, out var nextNodes))
            {
                foreach (var next in nextNodes)
                    Visit(next, outgoing, visited, visiting, result);
            }

            visiting.Remove(nodeGuid);
            visited.Add(nodeGuid);
            return false;
        }
    }
}
