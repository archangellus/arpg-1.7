using System.Collections.Generic;
using UnityEngine;

namespace PLAYERTWO.ARPGProject
{
    public static class EntityStatsFormulaEvaluator
    {
        public const string APort = "A";
        public const string BPort = "B";
        public const string CPort = "C";
        public const string ValuePort = "Value";
        public const string MinPort = "Min";
        public const string MaxPort = "Max";
        public const string TruePort = "True";
        public const string FalsePort = "False";
        public const string PercentPort = "Percent";
        public const string TPort = "T";

        public static bool TryEvaluate(
            EntityStatsFormulaData formula,
            EntityStatsFormulaContext context,
            out float value
        ) => TryEvaluate(formula, context, null, out value);

        public static bool TryEvaluate(
            EntityStatsFormulaData formula,
            EntityStatsFormulaContext context,
            HashSet<EntityStatsFormulaTarget> visitingTargets,
            out float value
        ) => TryEvaluateFormulaData(formula, context, visitingTargets, null, true, true, out value);

        public static bool TryEvaluateRaw(
            EntityStatsFormulaData formula,
            EntityStatsFormulaContext context,
            out float normalizedValue,
            out float rawValue
        )
        {
            rawValue = 0f;
            var success = TryEvaluateFormulaData(formula, context, null, null, true, false, out rawValue);
            normalizedValue = success
                ? EntityStatsFormulaTargetMetadataProvider.Get(formula.target, context).Normalize(rawValue)
                : 0f;
            return success;
        }

        public static bool TryEvaluateFunction(
            EntityStatsFormulaFunction function,
            EntityStatsFormulaContext context,
            out float value
        ) => TryEvaluateFunction(function, context, null, null, out value);

        public static bool TryEvaluateFunction(
            EntityStatsFormulaFunction function,
            EntityStatsFormulaContext context,
            HashSet<EntityStatsFormulaTarget> visitingTargets,
            HashSet<EntityStatsFormulaFunction> visitingFunctions,
            out float value
        )
        {
            value = 0f;

            if (function == null)
                return false;

            visitingFunctions ??= new HashSet<EntityStatsFormulaFunction>();

            if (!visitingFunctions.Add(function))
                return false;

            var success = TryEvaluateFormulaData(
                function.formula,
                context,
                visitingTargets,
                visitingFunctions,
                false,
                false,
                out value
            );

            visitingFunctions.Remove(function);
            return success;
        }

        static bool TryEvaluateFormulaData(
            EntityStatsFormulaData formula,
            EntityStatsFormulaContext context,
            HashSet<EntityStatsFormulaTarget> visitingTargets,
            HashSet<EntityStatsFormulaFunction> visitingFunctions,
            bool trackFormulaTarget,
            bool normalizeOutput,
            out float value
        )
        {
            value = 0f;

            if (formula == null)
                return false;

            visitingTargets ??= new HashSet<EntityStatsFormulaTarget>();

            if (trackFormulaTarget && !visitingTargets.Add(formula.target))
                return false;

            var compiled = formula.Compile();

            if (compiled.resultNode == null)
            {
                if (trackFormulaTarget)
                    visitingTargets.Remove(formula.target);
                return false;
            }

            var cache = new Dictionary<string, float>();
            var visitingNodes = new HashSet<string>();
            var success = TryEvaluateInput(
                compiled,
                compiled.resultNode,
                ValuePort,
                context.WithTarget(formula.target),
                cache,
                visitingNodes,
                visitingTargets,
                visitingFunctions,
                out value
            );

            if (success && normalizeOutput)
                value = EntityStatsFormulaTargetMetadataProvider.Get(formula.target, context).Normalize(value);

            if (trackFormulaTarget)
                visitingTargets.Remove(formula.target);
            return success;
        }

        static bool TryEvaluateNode(
            EntityStatsFormulaCompiledData compiled,
            EntityStatsFormulaNodeData node,
            EntityStatsFormulaContext context,
            Dictionary<string, float> cache,
            HashSet<string> visitingNodes,
            HashSet<EntityStatsFormulaTarget> visitingTargets,
            HashSet<EntityStatsFormulaFunction> visitingFunctions,
            out float value
        )
        {
            value = 0f;

            if (node == null)
                return false;

            if (cache.TryGetValue(node.guid, out value))
                return true;

            if (!visitingNodes.Add(node.guid))
                return false;

            var success = true;

            switch (node.type)
            {
                case EntityStatsFormulaNodeType.Input:
                    value = context.Get(node.input);
                    break;
                case EntityStatsFormulaNodeType.BuiltInValue:
                    value = context.builtInValue;
                    success = context.hasBuiltInValue;
                    break;
                case EntityStatsFormulaNodeType.Constant:
                    value = node.constant;
                    break;
                case EntityStatsFormulaNodeType.PercentConstant:
                    value = node.constant / 100f;
                    break;
                case EntityStatsFormulaNodeType.FormulaFunction:
                    success = TryEvaluateFunction(node.function, context, visitingTargets, visitingFunctions, out value);
                    break;
                case EntityStatsFormulaNodeType.FormulaReference:
                    success = context.TryResolveFormulaReference(
                        node.formulaTarget,
                        visitingTargets,
                        out value
                    );
                    break;
                case EntityStatsFormulaNodeType.Reroute:
                    success = TryEvaluateInput(
                        compiled,
                        node,
                        ValuePort,
                        context,
                        cache,
                        visitingNodes,
                        visitingTargets,
                        visitingFunctions,
                        out value
                    );
                    break;
                case EntityStatsFormulaNodeType.Operator:
                    success = TryEvaluateOperator(
                        compiled,
                        node,
                        context,
                        cache,
                        visitingNodes,
                        visitingTargets,
                        visitingFunctions,
                        out value
                    );
                    break;
                case EntityStatsFormulaNodeType.Result:
                    success = TryEvaluateInput(
                        compiled,
                        node,
                        ValuePort,
                        context,
                        cache,
                        visitingNodes,
                        visitingTargets,
                        visitingFunctions,
                        out value
                    );
                    break;
                case EntityStatsFormulaNodeType.Comment:
                default:
                    success = false;
                    break;
            }

            visitingNodes.Remove(node.guid);

            if (!success || float.IsNaN(value) || float.IsInfinity(value))
                return false;

            cache[node.guid] = value;
            return true;
        }

        static bool TryEvaluateOperator(
            EntityStatsFormulaCompiledData compiled,
            EntityStatsFormulaNodeData node,
            EntityStatsFormulaContext context,
            Dictionary<string, float> cache,
            HashSet<string> visitingNodes,
            HashSet<EntityStatsFormulaTarget> visitingTargets,
            HashSet<EntityStatsFormulaFunction> visitingFunctions,
            out float value
        )
        {
            value = 0f;

            bool Input(string port, out float input) => TryEvaluateInput(
                compiled,
                node,
                port,
                context,
                cache,
                visitingNodes,
                visitingTargets,
                visitingFunctions,
                out input
            );

            switch (node.operation)
            {
                case EntityStatsFormulaOperator.Add:
                    if (!Input(APort, out var addA) || !Input(BPort, out var addB)) return false;
                    value = addA + addB;
                    return true;
                case EntityStatsFormulaOperator.Subtract:
                    if (!Input(APort, out var subA) || !Input(BPort, out var subB)) return false;
                    value = subA - subB;
                    return true;
                case EntityStatsFormulaOperator.Multiply:
                    if (!Input(APort, out var mulA) || !Input(BPort, out var mulB)) return false;
                    value = mulA * mulB;
                    return true;
                case EntityStatsFormulaOperator.Divide:
                    if (!Input(APort, out var divA) || !Input(BPort, out var divB)) return false;
                    value = Mathf.Approximately(divB, 0f) ? 0f : divA / divB;
                    return true;
                case EntityStatsFormulaOperator.Min:
                    if (!Input(APort, out var minA) || !Input(BPort, out var minB)) return false;
                    value = Mathf.Min(minA, minB);
                    return true;
                case EntityStatsFormulaOperator.Max:
                    if (!Input(APort, out var maxA) || !Input(BPort, out var maxB)) return false;
                    value = Mathf.Max(maxA, maxB);
                    return true;
                case EntityStatsFormulaOperator.Clamp:
                    if (!Input(ValuePort, out var clampValue) || !Input(MinPort, out var clampMin) || !Input(MaxPort, out var clampMax)) return false;
                    value = Mathf.Clamp(clampValue, clampMin, clampMax);
                    return true;
                case EntityStatsFormulaOperator.Abs:
                    if (!Input(ValuePort, out var absValue)) return false;
                    value = Mathf.Abs(absValue);
                    return true;
                case EntityStatsFormulaOperator.Negate:
                    if (!Input(ValuePort, out var negateValue)) return false;
                    value = -negateValue;
                    return true;
                case EntityStatsFormulaOperator.Floor:
                    if (!Input(ValuePort, out var floorValue)) return false;
                    value = Mathf.Floor(floorValue);
                    return true;
                case EntityStatsFormulaOperator.Ceil:
                    if (!Input(ValuePort, out var ceilValue)) return false;
                    value = Mathf.Ceil(ceilValue);
                    return true;
                case EntityStatsFormulaOperator.Round:
                    if (!Input(ValuePort, out var roundValue)) return false;
                    value = Mathf.Round(roundValue);
                    return true;
                case EntityStatsFormulaOperator.Power:
                    if (!Input(APort, out var powerA) || !Input(BPort, out var powerB)) return false;
                    value = Mathf.Pow(powerA, powerB);
                    return true;
                case EntityStatsFormulaOperator.Sqrt:
                    if (!Input(ValuePort, out var sqrtValue)) return false;
                    value = Mathf.Sqrt(Mathf.Max(0f, sqrtValue));
                    return true;
                case EntityStatsFormulaOperator.Log:
                    if (!Input(ValuePort, out var logValue)) return false;
                    value = logValue <= 0f ? 0f : Mathf.Log(logValue);
                    return true;
                case EntityStatsFormulaOperator.PercentOf:
                    if (!Input(ValuePort, out var percentValue) || !Input(PercentPort, out var percent)) return false;
                    value = percentValue * percent / 100f;
                    return true;
                case EntityStatsFormulaOperator.Lerp:
                    if (!Input(APort, out var lerpA) || !Input(BPort, out var lerpB) || !Input(TPort, out var lerpT)) return false;
                    value = Mathf.Lerp(lerpA, lerpB, lerpT);
                    return true;
                case EntityStatsFormulaOperator.IfGreater:
                    if (!Input(APort, out var greaterA) || !Input(BPort, out var greaterB) || !Input(TruePort, out var greaterTrue) || !Input(FalsePort, out var greaterFalse)) return false;
                    value = greaterA > greaterB ? greaterTrue : greaterFalse;
                    return true;
                case EntityStatsFormulaOperator.IfLess:
                    if (!Input(APort, out var lessA) || !Input(BPort, out var lessB) || !Input(TruePort, out var lessTrue) || !Input(FalsePort, out var lessFalse)) return false;
                    value = lessA < lessB ? lessTrue : lessFalse;
                    return true;
                case EntityStatsFormulaOperator.Select:
                    if (!Input(ValuePort, out var selectCondition) || !Input(TruePort, out var selectTrue) || !Input(FalsePort, out var selectFalse)) return false;
                    value = selectCondition > 0f ? selectTrue : selectFalse;
                    return true;
                case EntityStatsFormulaOperator.NormalizePercent:
                    if (!Input(ValuePort, out var normalizeValue)) return false;
                    value = normalizeValue / 100f;
                    return true;
                default:
                    return false;
            }
        }

        static bool TryEvaluateInput(
            EntityStatsFormulaCompiledData compiled,
            EntityStatsFormulaNodeData node,
            string inputPortName,
            EntityStatsFormulaContext context,
            Dictionary<string, float> cache,
            HashSet<string> visitingNodes,
            HashSet<EntityStatsFormulaTarget> visitingTargets,
            HashSet<EntityStatsFormulaFunction> visitingFunctions,
            out float value
        )
        {
            value = 0f;
            var key = EntityStatsFormulaCompiledData.GetConnectionKey(node.guid, inputPortName);

            if (!compiled.inputConnections.TryGetValue(key, out var connection) || connection == null)
                return false;

            return compiled.nodesByGuid.TryGetValue(connection.outputNodeGuid, out var outputNode)
                && TryEvaluateNode(
                    compiled,
                    outputNode,
                    context,
                    cache,
                    visitingNodes,
                    visitingTargets,
                    visitingFunctions,
                    out value
                );
        }
    }
}
