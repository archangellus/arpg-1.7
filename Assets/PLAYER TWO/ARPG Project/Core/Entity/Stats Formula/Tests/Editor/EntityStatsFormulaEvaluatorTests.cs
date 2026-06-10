#if UNITY_EDITOR && UNITY_INCLUDE_TESTS
using NUnit.Framework;
using PLAYERTWO.ARPGProject;
using UnityEngine;

namespace PLAYERTWO.ARPGProject.Tests
{
    public class EntityStatsFormulaEvaluatorTests
    {
        [Test]
        public void EvaluatesBuiltInValueMultiplier()
        {
            var formula = CreateFormula(EntityStatsFormulaTarget.MaxHealth);
            var builtIn = AddNode(formula, EntityStatsFormulaNodeType.BuiltInValue);
            var multiplier = AddNode(formula, EntityStatsFormulaNodeType.Constant, constant: 1.25f);
            var multiply = AddNode(formula, EntityStatsFormulaNodeType.Operator, EntityStatsFormulaOperator.Multiply);
            Connect(formula, builtIn, multiply, EntityStatsFormulaEvaluator.APort);
            Connect(formula, multiplier, multiply, EntityStatsFormulaEvaluator.BPort);
            Connect(formula, multiply, formula.GetResultNode(), EntityStatsFormulaEvaluator.ValuePort);

            Assert.IsTrue(EntityStatsFormulaEvaluator.TryEvaluate(
                formula,
                new EntityStatsFormulaContext(EntityStatsFormulaTarget.MaxHealth, 100f, _ => 0f),
                out var value
            ));
            Assert.AreEqual(125f, value);
        }

        [Test]
        public void DivideByZeroReturnsZero()
        {
            var formula = CreateFormula(EntityStatsFormulaTarget.Defense);
            var numerator = AddNode(formula, EntityStatsFormulaNodeType.Constant, constant: 10f);
            var denominator = AddNode(formula, EntityStatsFormulaNodeType.Constant, constant: 0f);
            var divide = AddNode(formula, EntityStatsFormulaNodeType.Operator, EntityStatsFormulaOperator.Divide);
            Connect(formula, numerator, divide, EntityStatsFormulaEvaluator.APort);
            Connect(formula, denominator, divide, EntityStatsFormulaEvaluator.BPort);
            Connect(formula, divide, formula.GetResultNode(), EntityStatsFormulaEvaluator.ValuePort);

            Assert.IsTrue(EntityStatsFormulaEvaluator.TryEvaluate(
                formula,
                EntityStatsFormulaContext.Preview,
                out var value
            ));
            Assert.AreEqual(0f, value);
        }

        [Test]
        public void ValidatorReportsMissingRequiredInput()
        {
            var formula = CreateFormula(EntityStatsFormulaTarget.MaxMana);
            var add = AddNode(formula, EntityStatsFormulaNodeType.Operator, EntityStatsFormulaOperator.Add);
            Connect(formula, add, formula.GetResultNode(), EntityStatsFormulaEvaluator.ValuePort);

            var validation = EntityStatsFormulaValidator.Validate(formula);

            Assert.IsTrue(validation.hasErrors);
        }

        [Test]
        public void PercentTargetsAreClamped()
        {
            var formula = CreateFormula(EntityStatsFormulaTarget.CriticalChance);
            var constant = AddNode(formula, EntityStatsFormulaNodeType.Constant, constant: 25f);
            Connect(formula, constant, formula.GetResultNode(), EntityStatsFormulaEvaluator.ValuePort);

            Assert.IsTrue(EntityStatsFormulaEvaluator.TryEvaluate(
                formula,
                EntityStatsFormulaContext.Preview,
                out var value
            ));
            Assert.AreEqual(1f, value);
        }


        [Test]
        public void PercentConstantOutputsNormalizedValue()
        {
            var formula = CreateFormula(EntityStatsFormulaTarget.CriticalChance);
            var percent = AddNode(formula, EntityStatsFormulaNodeType.PercentConstant, constant: 25f);
            Connect(formula, percent, formula.GetResultNode(), EntityStatsFormulaEvaluator.ValuePort);

            Assert.IsTrue(EntityStatsFormulaEvaluator.TryEvaluate(
                formula,
                EntityStatsFormulaContext.Preview,
                out var value
            ));
            Assert.AreEqual(0.25f, value);
        }

        [Test]
        public void FormulaFunctionNodeEvaluatesReusableFunctionAsset()
        {
            var function = ScriptableObject.CreateInstance<EntityStatsFormulaFunction>();
            function.formula = CreateFormula(EntityStatsFormulaTarget.MaxHealth);
            var functionConstant = AddNode(function.formula, EntityStatsFormulaNodeType.Constant, constant: 12f);
            Connect(function.formula, functionConstant, function.formula.GetResultNode(), EntityStatsFormulaEvaluator.ValuePort);

            var formula = CreateFormula(EntityStatsFormulaTarget.MaxHealth);
            var functionNode = AddNode(formula, EntityStatsFormulaNodeType.FormulaFunction);
            functionNode.function = function;
            Connect(formula, functionNode, formula.GetResultNode(), EntityStatsFormulaEvaluator.ValuePort);

            Assert.IsTrue(EntityStatsFormulaEvaluator.TryEvaluate(
                formula,
                EntityStatsFormulaContext.Preview,
                out var value
            ));
            Assert.AreEqual(12f, value);
        }

        static EntityStatsFormulaData CreateFormula(EntityStatsFormulaTarget target)
        {
            var formula = new EntityStatsFormulaData { target = target };
            formula.nodes.Add(new EntityStatsFormulaNodeData
            {
                type = EntityStatsFormulaNodeType.Result,
                position = new Rect(300, 100, 120, 72),
            });
            return formula;
        }

        static EntityStatsFormulaNodeData AddNode(
            EntityStatsFormulaData formula,
            EntityStatsFormulaNodeType type,
            EntityStatsFormulaOperator operation = EntityStatsFormulaOperator.Add,
            float constant = 0f
        )
        {
            var node = new EntityStatsFormulaNodeData
            {
                type = type,
                operation = operation,
                constant = constant,
            };
            formula.nodes.Add(node);
            return node;
        }

        static void Connect(
            EntityStatsFormulaData formula,
            EntityStatsFormulaNodeData output,
            EntityStatsFormulaNodeData input,
            string inputPortName
        ) => formula.connections.Add(new EntityStatsFormulaConnectionData
        {
            outputNodeGuid = output.guid,
            inputNodeGuid = input.guid,
            inputPortName = inputPortName,
        });
    }
}
#endif
