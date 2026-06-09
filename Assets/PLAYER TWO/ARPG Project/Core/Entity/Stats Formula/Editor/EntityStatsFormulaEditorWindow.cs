#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using PLAYERTWO.ARPGProject;

namespace PLAYERTWO.ARPGProjectEditorTools
{
    public class EntityStatsFormulaEditorWindow : EditorWindow
    {
        EntityStatsFormulaGraph m_graphAsset;
        EntityStatsFormulaTarget m_target;
        EntityStatsFormulaTarget m_exampleTarget;
        int m_selectedEntityIndex;
        readonly List<EntityStatsSourceOption> m_entitySourceOptions = new();
        readonly List<string> m_entitySourceNames = new();
        EntityStatsFormulaData m_formula;
        StatsFormulaGraphView m_graphView;
        Toggle m_enabledToggle;
        Label m_previewLabel;

        [MenuItem("Tools/PLAYER TWO/ARPG Project/Stats Formula Editor")]
        public static void Open()
        {
            var window = GetWindow<EntityStatsFormulaEditorWindow>();
            window.titleContent = new GUIContent("Stats Formula Editor");
            window.minSize = new Vector2(760, 420);
        }

        void OnEnable()
        {
            BuildWindow();
            Selection.selectionChanged += OnSelectionChanged;
            TryUseSelectedAsset();
        }

        void OnDisable()
        {
            Selection.selectionChanged -= OnSelectionChanged;
        }

        void BuildWindow()
        {
            rootVisualElement.Clear();
            rootVisualElement.style.flexDirection = FlexDirection.Column;

            RefreshEntitySourceOptions();

            var toolbar = new Toolbar { name = "StatsFormulaToolbar" };
            toolbar.style.flexShrink = 0f;
            var assetField = new ObjectField("Graph")
            {
                objectType = typeof(EntityStatsFormulaGraph),
                allowSceneObjects = false,
                value = m_graphAsset,
            };
            assetField.RegisterValueChangedCallback(evt =>
            {
                m_graphAsset = evt.newValue as EntityStatsFormulaGraph;
                LoadFormula();
            });
            toolbar.Add(assetField);

            var targetField = new EnumField("Stat", m_target);
            targetField.RegisterValueChangedCallback(evt =>
            {
                m_target = (EntityStatsFormulaTarget)evt.newValue;
                LoadFormula();
            });
            toolbar.Add(targetField);

            m_enabledToggle = new Toggle("Enabled") { value = m_formula?.enabled ?? true };
            m_enabledToggle.RegisterValueChangedCallback(evt =>
            {
                if (m_formula == null)
                    return;

                m_formula.enabled = evt.newValue;
                Save();
            });
            toolbar.Add(m_enabledToggle);

            toolbar.Add(new Button(CreateAsset) { text = "New Graph Asset" });

            var entityField = new PopupField<string>(
                "Entity",
                m_entitySourceNames,
                Mathf.Clamp(m_selectedEntityIndex, 0, Mathf.Max(0, m_entitySourceNames.Count - 1))
            );
            entityField.RegisterValueChangedCallback(evt =>
            {
                m_selectedEntityIndex = Mathf.Max(0, m_entitySourceNames.IndexOf(evt.newValue));
            });
            toolbar.Add(entityField);
            toolbar.Add(new Button(BuildWindow) { text = "Refresh Entities" });

            var exampleField = new EnumField("Example", m_exampleTarget);
            exampleField.RegisterValueChangedCallback(evt =>
            {
                m_exampleTarget = (EntityStatsFormulaTarget)evt.newValue;
            });
            toolbar.Add(exampleField);
            toolbar.Add(new Button(PopulateSelectedBuiltInExample) { text = "Load Example" });
            toolbar.Add(new Button(PopulateAllBuiltInExamples) { text = "Load All Examples" });
            toolbar.Add(new Button(Save) { text = "Save" });

            m_previewLabel = new Label("Preview: —") { style = { unityTextAlign = TextAnchor.MiddleLeft } };
            toolbar.Add(m_previewLabel);
            rootVisualElement.Add(toolbar);

            m_graphView = new StatsFormulaGraphView(this);
            rootVisualElement.Add(m_graphView);

            LoadFormula();
        }

        void OnSelectionChanged()
        {
            if (Selection.activeObject is EntityStatsFormulaGraph selected && selected != m_graphAsset)
            {
                m_graphAsset = selected;
                BuildWindow();
            }
        }

        void TryUseSelectedAsset()
        {
            if (!m_graphAsset && Selection.activeObject is EntityStatsFormulaGraph selected)
            {
                m_graphAsset = selected;
                BuildWindow();
            }
        }

        void CreateAsset()
        {
            var path = EditorUtility.SaveFilePanelInProject(
                "Create Stats Formula Graph",
                "New Entity Stats Formula Graph",
                "asset",
                "Choose where to save the stats formula graph."
            );

            if (string.IsNullOrEmpty(path))
                return;

            m_graphAsset = CreateInstance<EntityStatsFormulaGraph>();
            AssetDatabase.CreateAsset(m_graphAsset, path);
            AssetDatabase.SaveAssets();
            Selection.activeObject = m_graphAsset;
            BuildWindow();
        }

        void PopulateSelectedBuiltInExample()
        {
            if (!ValidateGraphForExamples())
                return;

            var confirmed = EditorUtility.DisplayDialog(
                "Load Built-In Example Formula",
                $"Replace the {m_exampleTarget} formula with a disabled example imported from {GetSelectedEntityDisplayName()}?",
                "Replace",
                "Cancel"
            );

            if (!confirmed)
                return;

            Undo.RecordObject(m_graphAsset, $"Load {m_exampleTarget} Example From Entity");
            AddOrReplaceFormula(CreateEntityExampleFormula(m_exampleTarget));
            m_target = m_exampleTarget;
            EditorUtility.SetDirty(m_graphAsset);
            AssetDatabase.SaveAssets();
            BuildWindow();
        }

        void PopulateAllBuiltInExamples()
        {
            if (!ValidateGraphForExamples())
                return;

            var confirmed = EditorUtility.DisplayDialog(
                "Load All Built-In Example Formulas",
                $"Replace all formulas in this graph with disabled examples imported from {GetSelectedEntityDisplayName()}?",
                "Replace All",
                "Cancel"
            );

            if (!confirmed)
                return;

            Undo.RecordObject(m_graphAsset, "Load All Entity Example Formulas");
            m_graphAsset.formulas.Clear();

            foreach (EntityStatsFormulaTarget target in Enum.GetValues(typeof(EntityStatsFormulaTarget)))
                AddOrReplaceFormula(CreateEntityExampleFormula(target));

            m_target = m_exampleTarget;
            EditorUtility.SetDirty(m_graphAsset);
            AssetDatabase.SaveAssets();
            BuildWindow();
        }


        EntityStatsFormulaData CreateEntityExampleFormula(EntityStatsFormulaTarget target)
        {
            var formula = EntityStatsFormulaExampleBuilder.CreateBuiltInExample(target);
            ApplySelectedEntityStatsSnapshot(formula);
            return formula;
        }

        void ApplySelectedEntityStatsSnapshot(EntityStatsFormulaData formula)
        {
            var sourceStats = GetSelectedEntityStats();

            if (formula == null || !sourceStats)
                return;

            foreach (var node in formula.nodes)
            {
                if (node.type != EntityStatsFormulaNodeType.Input)
                    continue;

                if (!TryGetSelectedEntityStatsValue(sourceStats, node.input, out var value))
                    continue;

                node.type = EntityStatsFormulaNodeType.Constant;
                node.constant = value;
            }
        }

        bool TryGetSelectedEntityStatsValue(
            EntityStatsManager sourceStats,
            EntityStatsFormulaInput input,
            out float value
        )
        {
            switch (input)
            {
                case EntityStatsFormulaInput.Level:
                    value = sourceStats.level;
                    return true;
                case EntityStatsFormulaInput.Strength:
                    value = sourceStats.strength;
                    return true;
                case EntityStatsFormulaInput.Dexterity:
                    value = sourceStats.dexterity;
                    return true;
                case EntityStatsFormulaInput.Vitality:
                    value = sourceStats.vitality;
                    return true;
                case EntityStatsFormulaInput.Energy:
                    value = sourceStats.energy;
                    return true;
                default:
                    value = 0f;
                    return false;
            }
        }

        EntityStatsManager GetSelectedEntityStats()
        {
            if (
                m_selectedEntityIndex < 0
                || m_selectedEntityIndex >= m_entitySourceOptions.Count
            )
                return null;

            return m_entitySourceOptions[m_selectedEntityIndex].stats;
        }

        string GetSelectedEntityDisplayName()
        {
            if (
                m_selectedEntityIndex < 0
                || m_selectedEntityIndex >= m_entitySourceOptions.Count
            )
                return "the selected entity";

            return m_entitySourceOptions[m_selectedEntityIndex].displayName;
        }

        void RefreshEntitySourceOptions()
        {
            m_entitySourceOptions.Clear();
            m_entitySourceNames.Clear();

            foreach (var guid in AssetDatabase.FindAssets("t:Prefab"))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);

                if (!prefab)
                    continue;

                foreach (var stats in prefab.GetComponentsInChildren<EntityStatsManager>(true))
                {
                    var displayName = stats.gameObject == prefab
                        ? prefab.name
                        : $"{prefab.name}/{stats.gameObject.name}";

                    m_entitySourceOptions.Add(new EntityStatsSourceOption
                    {
                        displayName = displayName,
                        stats = stats,
                    });
                    m_entitySourceNames.Add(displayName);
                }
            }

            if (m_entitySourceNames.Count == 0)
                m_entitySourceNames.Add("No EntityStatsManager prefabs found");

            m_selectedEntityIndex = Mathf.Clamp(
                m_selectedEntityIndex,
                0,
                Mathf.Max(0, m_entitySourceOptions.Count - 1)
            );
        }

        void AddOrReplaceFormula(EntityStatsFormulaData formula)
        {
            m_graphAsset.formulas.RemoveAll(entry => entry.target == formula.target);
            m_graphAsset.formulas.Add(formula);
        }

        bool ValidateGraphForExamples()
        {
            if (!m_graphAsset)
            {
                EditorUtility.DisplayDialog(
                "Stats Formula Editor",
                    "Create or assign a formula graph asset before loading entity examples.",
                    "OK"
                );
                return false;
            }

            if (GetSelectedEntityStats())
                return true;

            EditorUtility.DisplayDialog(
                "Stats Formula Editor",
                "Select an entity prefab with an EntityStatsManager before loading examples.",
                "OK"
            );
            return false;
        }

        void LoadFormula()
        {
            m_graphView?.ClearGraph();
            m_formula = null;

            if (!m_graphAsset)
            {
                if (m_enabledToggle != null)
                    m_enabledToggle.SetValueWithoutNotify(true);

                UpdatePreview();
                return;
            }

            m_formula = m_graphAsset.formulas.Find(entry => entry.target == m_target);

            if (m_formula == null)
            {
                Undo.RecordObject(m_graphAsset, "Create Stat Formula");
                m_formula = new EntityStatsFormulaData { target = m_target };
                m_formula.nodes.Add(new EntityStatsFormulaNodeData
                {
                    type = EntityStatsFormulaNodeType.Result,
                    position = new Rect(520, 180, 120, 72),
                });
                m_graphAsset.formulas.Add(m_formula);
                EditorUtility.SetDirty(m_graphAsset);
            }

            if (m_formula.GetResultNode() == null)
            {
                m_formula.nodes.Add(new EntityStatsFormulaNodeData
                {
                    type = EntityStatsFormulaNodeType.Result,
                    position = new Rect(520, 180, 120, 72),
                });
            }

            if (m_enabledToggle != null)
                m_enabledToggle.SetValueWithoutNotify(m_formula.enabled);

            m_graphView.Load(m_formula);
            UpdatePreview();
        }

        public EntityStatsFormulaNodeData AddNodeFromGraph(
            EntityStatsFormulaNodeType type,
            Vector2? position = null
        ) => AddNode(type, position);

        EntityStatsFormulaNodeData AddNode(EntityStatsFormulaNodeType type, Vector2? position = null)
        {
            if (!m_graphAsset || m_formula == null)
            {
                EditorUtility.DisplayDialog(
                    "Stats Formula Editor",
                    "Create or assign a formula graph asset before adding nodes.",
                    "OK"
                );
                return null;
            }

            Undo.RecordObject(m_graphAsset, "Add Formula Node");
            var nodePosition = position ?? new Vector2(160, 160);
            var node = new EntityStatsFormulaNodeData
            {
                type = type,
                position = new Rect(nodePosition.x, nodePosition.y, 170, 92),
            };

            if (type == EntityStatsFormulaNodeType.Operator)
                node.position.size = new Vector2(120, 92);

            m_formula.nodes.Add(node);
            m_graphView.AddNode(node);
            Save();
            return node;
        }

        public void Save()
        {
            if (!m_graphAsset || m_formula == null)
                return;

            m_graphView.WriteBack(m_formula);
            EditorUtility.SetDirty(m_graphAsset);
            AssetDatabase.SaveAssets();
            UpdatePreview();
        }

        public void UpdatePreview()
        {
            if (m_previewLabel == null)
                return;

            if (m_formula != null && EntityStatsFormulaEvaluator.TryEvaluate(
                m_formula,
                EntityStatsFormulaContext.Preview,
                out var value
            ))
                m_previewLabel.text = $"Preview: {value:0.###}";
            else
                m_previewLabel.text = "Preview: not connected";
        }
    }

    class FormulaEdgeConnectorListener : IEdgeConnectorListener
    {
        readonly StatsFormulaGraphView m_graphView;

        public FormulaEdgeConnectorListener(StatsFormulaGraphView graphView)
        {
            m_graphView = graphView;
        }

        public void OnDropOutsidePort(Edge edge, Vector2 position)
        {
            m_graphView.OpenNodeCreationMenuForDroppedEdge(edge, position);
        }

        public void OnDrop(GraphView graphView, Edge edge)
        {
            if (edge == null)
                return;

            RemoveSingleCapacityConnections(graphView, edge.input);
            RemoveSingleCapacityConnections(graphView, edge.output);
            graphView.AddElement(edge);
        }

        static void RemoveSingleCapacityConnections(GraphView graphView, Port port)
        {
            if (port == null || port.capacity != Port.Capacity.Single)
                return;

            foreach (var connection in port.connections.ToList())
            {
                connection.input?.Disconnect(connection);
                connection.output?.Disconnect(connection);
                graphView.RemoveElement(connection);
            }
        }
    }

    class EntityStatsSourceOption
    {
        public string displayName;
        public EntityStatsManager stats;
    }

    class StatsFormulaGraphView : GraphView
    {
        readonly EntityStatsFormulaEditorWindow m_window;
        readonly Dictionary<string, Node> m_nodesByGuid = new();
        EntityStatsFormulaData m_formula;
        FormulaEdgeConnectorListener m_edgeConnectorListener;
        bool m_isLoading;

        public StatsFormulaGraphView(EntityStatsFormulaEditorWindow window)
        {
            m_window = window;
            style.flexGrow = 1;
            style.minHeight = 0f;

            Insert(0, new GridBackground());
            this.AddManipulator(new ContentZoomer());
            this.AddManipulator(new ContentDragger());
            this.AddManipulator(new SelectionDragger());
            this.AddManipulator(new RectangleSelector());

            m_edgeConnectorListener = new FormulaEdgeConnectorListener(this);
            SetupContextMenu();

            graphViewChanged = OnGraphViewChanged;
        }

        void SetupContextMenu()
        {
            nodeCreationRequest = context =>
            {
                ShowNodeCreationMenu(
                    GetContentPositionFromScreen(context.screenMousePosition),
                    context.target as Port
                );
            };
        }

        public override void BuildContextualMenu(ContextualMenuPopulateEvent evt)
        {
            base.BuildContextualMenu(evt);

            var position = GetContentPosition(evt.localMousePosition);
            evt.menu.AppendSeparator();
            evt.menu.AppendAction(
                "Create/Get Stat",
                _ => AddNodeAndConnect(EntityStatsFormulaNodeType.Input, position, null)
            );
            evt.menu.AppendAction(
                "Create/Constant",
                _ => AddNodeAndConnect(EntityStatsFormulaNodeType.Constant, position, null)
            );
            evt.menu.AppendAction(
                "Create/Operator",
                _ => AddNodeAndConnect(EntityStatsFormulaNodeType.Operator, position, null)
            );
        }

        void ShowNodeCreationMenu(Vector2 position, Port connectedPort)
        {
            var menu = new GenericMenu();
            menu.AddItem(
                new GUIContent("Create/Get Stat"),
                false,
                () => AddNodeAndConnect(EntityStatsFormulaNodeType.Input, position, connectedPort)
            );
            menu.AddItem(
                new GUIContent("Create/Constant"),
                false,
                () => AddNodeAndConnect(EntityStatsFormulaNodeType.Constant, position, connectedPort)
            );
            menu.AddItem(
                new GUIContent("Create/Operator"),
                false,
                () => AddNodeAndConnect(EntityStatsFormulaNodeType.Operator, position, connectedPort)
            );
            menu.ShowAsContext();
        }

        void AddNodeAndConnect(
            EntityStatsFormulaNodeType type,
            Vector2 position,
            Port connectedPort
        )
        {
            var data = m_window.AddNodeFromGraph(type, position);

            if (data == null || !m_nodesByGuid.TryGetValue(data.guid, out var node))
                return;

            if (connectedPort == null)
                return;

            TryConnectDroppedPort(connectedPort, node);
            m_window.Save();
        }

        void TryConnectDroppedPort(Port connectedPort, Node newNode)
        {
            Port output;
            Port input;

            if (connectedPort.direction == Direction.Output)
            {
                output = connectedPort;
                input = newNode.inputContainer.Query<Port>().ToList().FirstOrDefault();
            }
            else
            {
                output = newNode.outputContainer.Query<Port>().ToList().FirstOrDefault();
                input = connectedPort;
            }

            if (output == null || input == null)
                return;

            var edge = output.ConnectTo(input);
            AddElement(edge);
        }

        Vector2 GetContentPosition(Vector2 localMousePosition) =>
            this.ChangeCoordinatesTo(contentViewContainer, localMousePosition);

        Vector2 GetContentPositionFromScreen(Vector2 screenMousePosition)
        {
            var windowMousePosition = screenMousePosition - m_window.position.position;
            return m_window.rootVisualElement.ChangeCoordinatesTo(
                contentViewContainer,
                windowMousePosition
            );
        }


        public void OpenNodeCreationMenuForDroppedEdge(Edge edge, Vector2 screenPosition)
        {
            if (edge == null)
                return;

            var connectedPort = edge.output ?? edge.input;

            if (connectedPort == null)
                return;

            ShowNodeCreationMenu(GetContentPositionFromScreen(screenPosition), connectedPort);
        }

        public void ClearGraph()
        {
            m_isLoading = true;

            foreach (var element in graphElements.ToList())
                RemoveElement(element);

            m_nodesByGuid.Clear();
            m_formula = null;
            m_isLoading = false;
        }

        public void Load(EntityStatsFormulaData formula)
        {
            ClearGraph();
            m_formula = formula;

            foreach (var node in m_formula.nodes)
                AddNode(node);

            foreach (var connection in m_formula.connections)
                AddConnection(connection);
        }

        public void AddNode(EntityStatsFormulaNodeData data)
        {
            var node = CreateNode(data);
            m_nodesByGuid[data.guid] = node;
            AddElement(node);
        }

        public void WriteBack(EntityStatsFormulaData formula)
        {
            foreach (var node in nodes.ToList())
            {
                if (node.userData is EntityStatsFormulaNodeData data)
                    data.position = node.GetPosition();
            }

            formula.connections.Clear();

            foreach (var edge in edges.ToList())
            {
                if (
                    edge.output?.node?.userData is EntityStatsFormulaNodeData output
                    && edge.input?.node?.userData is EntityStatsFormulaNodeData input
                )
                {
                    formula.connections.Add(new EntityStatsFormulaConnectionData
                    {
                        outputNodeGuid = output.guid,
                        inputNodeGuid = input.guid,
                        inputPortName = edge.input.portName,
                    });
                }
            }
        }

        public override List<Port> GetCompatiblePorts(Port startPort, NodeAdapter nodeAdapter)
        {
            return ports
                .ToList()
                .Where(port => port.direction != startPort.direction && port.node != startPort.node)
                .ToList();
        }

        Node CreateNode(EntityStatsFormulaNodeData data)
        {
            var node = new Node { title = GetTitle(data), userData = data };
            node.SetPosition(data.position);

            switch (data.type)
            {
                case EntityStatsFormulaNodeType.Input:
                    AddOutput(node, "Value");
                    var inputField = new EnumField("Stat", data.input);
                    inputField.RegisterValueChangedCallback(evt =>
                    {
                        data.input = (EntityStatsFormulaInput)evt.newValue;
                        node.title = GetTitle(data);
                        m_window.Save();
                    });
                    node.extensionContainer.Add(inputField);
                    break;
                case EntityStatsFormulaNodeType.Constant:
                    AddOutput(node, "Value");
                    var floatField = new FloatField("Value") { value = data.constant };
                    floatField.RegisterValueChangedCallback(evt =>
                    {
                        data.constant = evt.newValue;
                        m_window.Save();
                    });
                    node.extensionContainer.Add(floatField);
                    break;
                case EntityStatsFormulaNodeType.Operator:
                    AddInput(node, "A");
                    AddInput(node, "B");
                    AddOutput(node, "Value");
                    var opField = new EnumField(data.operation);
                    opField.RegisterValueChangedCallback(evt =>
                    {
                        data.operation = (EntityStatsFormulaOperator)evt.newValue;
                        node.title = GetTitle(data);
                        m_window.Save();
                    });
                    node.extensionContainer.Add(opField);
                    break;
                case EntityStatsFormulaNodeType.Result:
                    AddInput(node, "Value");
                    node.capabilities &= ~Capabilities.Deletable;
                    break;
            }

            node.RefreshExpandedState();
            node.RefreshPorts();
            return node;
        }

        void AddInput(Node node, string name)
        {
            var port = node.InstantiatePort(Orientation.Horizontal, Direction.Input, Port.Capacity.Single, typeof(float));
            port.portName = name;
            port.AddManipulator(new EdgeConnector<Edge>(m_edgeConnectorListener));
            node.inputContainer.Add(port);
        }

        void AddOutput(Node node, string name)
        {
            var port = node.InstantiatePort(Orientation.Horizontal, Direction.Output, Port.Capacity.Multi, typeof(float));
            port.portName = name;
            port.AddManipulator(new EdgeConnector<Edge>(m_edgeConnectorListener));
            node.outputContainer.Add(port);
        }

        void AddConnection(EntityStatsFormulaConnectionData connection)
        {
            if (
                !m_nodesByGuid.TryGetValue(connection.outputNodeGuid, out var outputNode)
                || !m_nodesByGuid.TryGetValue(connection.inputNodeGuid, out var inputNode)
            )
                return;

            var output = outputNode.outputContainer.Query<Port>().ToList().FirstOrDefault();
            var input = inputNode.inputContainer.Query<Port>().ToList()
                .FirstOrDefault(port => port.portName == connection.inputPortName);

            if (output == null || input == null)
                return;

            var edge = output.ConnectTo(input);
            AddElement(edge);
        }

        GraphViewChange OnGraphViewChanged(GraphViewChange change)
        {
            if (m_isLoading)
                return change;

            if (change.elementsToRemove != null)
            {
                foreach (var element in change.elementsToRemove)
                {
                    if (element is Node node && node.userData is EntityStatsFormulaNodeData data)
                        m_formula?.nodes.Remove(data);
                }
            }

            EditorApplication.delayCall += m_window.Save;
            return change;
        }

        static string GetTitle(EntityStatsFormulaNodeData data)
        {
            switch (data.type)
            {
                case EntityStatsFormulaNodeType.Input:
                    return "Get Stat";
                case EntityStatsFormulaNodeType.Constant:
                    return "Constant";
                case EntityStatsFormulaNodeType.Operator:
                    return data.operation switch
                    {
                        EntityStatsFormulaOperator.Add => "+",
                        EntityStatsFormulaOperator.Subtract => "−",
                        EntityStatsFormulaOperator.Multiply => "×",
                        EntityStatsFormulaOperator.Divide => "÷",
                        EntityStatsFormulaOperator.Min => "Min",
                        EntityStatsFormulaOperator.Max => "Max",
                        _ => "Operator",
                    };
                case EntityStatsFormulaNodeType.Result:
                    return "Result";
                default:
                    return data.type.ToString();
            }
        }
    }
}
#endif
