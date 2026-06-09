using System.Collections.Generic;
using UnityEngine;

namespace PLAYERTWO.ARPGProject
{
    public static class EntityStatsFormulaEvaluator
    {
        const string k_aPort = "A";
        const string k_bPort = "B";
        const string k_valuePort = "Value";

        public static bool TryEvaluate(
            EntityStatsFormulaData formula,
            EntityStatsFormulaContext context,
            out float value
        )
        {
            value = 0f;

            if (formula == null)
                return false;

            var result = formula.GetResultNode();

            if (result == null)
                return false;

            var cache = new Dictionary<string, float>();
            var visiting = new HashSet<string>();
            return TryEvaluateInput(formula, result, k_valuePort, context, cache, visiting, out value);
        }

        static bool TryEvaluateNode(
            EntityStatsFormulaData formula,
            EntityStatsFormulaNodeData node,
            EntityStatsFormulaContext context,
            Dictionary<string, float> cache,
            HashSet<string> visiting,
            out float value
        )
        {
            value = 0f;

            if (node == null)
                return false;

            if (cache.TryGetValue(node.guid, out value))
                return true;

            if (!visiting.Add(node.guid))
                return false;

            switch (node.type)
            {
                case EntityStatsFormulaNodeType.Input:
                    value = context.Get(node.input);
                    break;
                case EntityStatsFormulaNodeType.Constant:
                    value = node.constant;
                    break;
                case EntityStatsFormulaNodeType.Operator:
                    if (
                        !TryEvaluateInput(formula, node, k_aPort, context, cache, visiting, out var a)
                        || !TryEvaluateInput(formula, node, k_bPort, context, cache, visiting, out var b)
                    )
                    {
                        visiting.Remove(node.guid);
                        return false;
                    }

                    value = EvaluateOperator(node.operation, a, b);
                    break;
                case EntityStatsFormulaNodeType.Result:
                    if (
                        !TryEvaluateInput(
                            formula,
                            node,
                            k_valuePort,
                            context,
                            cache,
                            visiting,
                            out value
                        )
                    )
                    {
                        visiting.Remove(node.guid);
                        return false;
                    }
                    break;
                default:
                    visiting.Remove(node.guid);
                    return false;
            }

            visiting.Remove(node.guid);
            cache[node.guid] = value;
            return true;
        }

        static bool TryEvaluateInput(
            EntityStatsFormulaData formula,
            EntityStatsFormulaNodeData node,
            string inputPortName,
            EntityStatsFormulaContext context,
            Dictionary<string, float> cache,
            HashSet<string> visiting,
            out float value
        )
        {
            value = 0f;
            var connection = formula.connections.Find(edge =>
                edge.inputNodeGuid == node.guid && edge.inputPortName == inputPortName
            );

            if (connection == null)
                return false;

            var outputNode = formula.nodes.Find(candidate =>
                candidate.guid == connection.outputNodeGuid
            );
            return TryEvaluateNode(formula, outputNode, context, cache, visiting, out value);
        }

        static float EvaluateOperator(EntityStatsFormulaOperator operation, float a, float b)
        {
            switch (operation)
            {
                case EntityStatsFormulaOperator.Add:
                    return a + b;
                case EntityStatsFormulaOperator.Subtract:
                    return a - b;
                case EntityStatsFormulaOperator.Multiply:
                    return a * b;
                case EntityStatsFormulaOperator.Divide:
                    return Mathf.Approximately(b, 0f) ? 0f : a / b;
                case EntityStatsFormulaOperator.Min:
                    return Mathf.Min(a, b);
                case EntityStatsFormulaOperator.Max:
                    return Mathf.Max(a, b);
                default:
                    return 0f;
            }
        }
    }
}
