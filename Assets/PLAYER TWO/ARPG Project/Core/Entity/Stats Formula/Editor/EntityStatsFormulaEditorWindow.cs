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
    enum StatsFormulaPreviewMode
    {
        SampleData,
        SelectedEntity,
        AllEntities,
        CustomValues,
    }

    public class EntityStatsFormulaEditorWindow : EditorWindow
    {
        EntityStatsFormulaGraph m_graphAsset;
        EntityStatsFormulaTarget m_target;
        EntityStatsFormulaTarget m_exampleTarget;
        StatsFormulaPreviewMode m_previewMode;
        int m_selectedEntityIndex;
        readonly List<EntityStatsSourceOption> m_entitySourceOptions = new();
        readonly List<string> m_entitySourceNames = new();
        EntityStatsFormulaData m_formula;
        StatsFormulaGraphView m_graphView;
        Toggle m_enabledToggle;
        Label m_previewLabel;
        bool m_saveQueued;
        bool m_queuedSaveNeedsUndo;
        string m_queuedSaveUndoName;
        int m_customLevel = 1;
        int m_customStrength = 20;
        int m_customDexterity = 15;
        int m_customVitality = 15;
        int m_customEnergy = 10;

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
            Undo.undoRedoPerformed += OnUndoRedo;
            TryUseSelectedAsset();
        }

        void OnDisable()
        {
            FlushPendingSave();
            Selection.selectionChanged -= OnSelectionChanged;
            Undo.undoRedoPerformed -= OnUndoRedo;
        }

        void OnUndoRedo()
        {
            LoadFormula();
            UpdatePreview();
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

                Undo.RecordObject(m_graphAsset, "Toggle Formula Enabled");
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
            toolbar.Add(new Button(SaveAndPersist) { text = "Save" });
            toolbar.Add(new Button(ExportJson) { text = "Export JSON" });
            toolbar.Add(new Button(ImportJson) { text = "Import JSON" });
            toolbar.Add(new Button(() => m_graphView.OpenSearchAtCenter()) { text = "Search Nodes" });
            toolbar.Add(new Button(() => m_graphView.AutoLayout()) { text = "Auto Layout" });
            toolbar.Add(new Button(() => m_graphView.DuplicateSelection()) { text = "Duplicate" });
            toolbar.Add(new Button(() => m_graphView.FrameAllNodes()) { text = "Frame All" });

            var previewModeField = new EnumField("Preview With", m_previewMode);
            previewModeField.RegisterValueChangedCallback(evt =>
            {
                m_previewMode = (StatsFormulaPreviewMode)evt.newValue;
                UpdatePreview();
            });
            toolbar.Add(previewModeField);
            AddCustomPreviewField(toolbar, "Lvl", () => m_customLevel, value => m_customLevel = value);
            AddCustomPreviewField(toolbar, "Str", () => m_customStrength, value => m_customStrength = value);
            AddCustomPreviewField(toolbar, "Dex", () => m_customDexterity, value => m_customDexterity = value);
            AddCustomPreviewField(toolbar, "Vit", () => m_customVitality, value => m_customVitality = value);
            AddCustomPreviewField(toolbar, "Ene", () => m_customEnergy, value => m_customEnergy = value);

            m_previewLabel = new Label("Preview: —") { style = { unityTextAlign = TextAnchor.MiddleLeft } };
            toolbar.Add(m_previewLabel);
            rootVisualElement.Add(toolbar);

            m_graphView = new StatsFormulaGraphView(this);
            rootVisualElement.Add(m_graphView);

            LoadFormula();
        }

        void AddCustomPreviewField(Toolbar toolbar, string label, Func<int> getter, Action<int> setter)
        {
            var field = new IntegerField(label) { value = getter() };
            field.style.width = 72f;
            field.RegisterValueChangedCallback(evt =>
            {
                setter(Mathf.Max(0, evt.newValue));
                UpdatePreview();
            });
            toolbar.Add(field);
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

        void ExportJson()
        {
            if (!m_graphAsset)
                return;

            var path = EditorUtility.SaveFilePanel(
                "Export Stats Formula Graph JSON",
                Application.dataPath,
                m_graphAsset.name + ".json",
                "json"
            );

            if (string.IsNullOrEmpty(path))
                return;

            System.IO.File.WriteAllText(path, m_graphAsset.ExportJson());
        }

        void ImportJson()
        {
            if (!m_graphAsset)
                return;

            var path = EditorUtility.OpenFilePanel(
                "Import Stats Formula Graph JSON",
                Application.dataPath,
                "json"
            );

            if (string.IsNullOrEmpty(path))
                return;

            Undo.RecordObject(m_graphAsset, "Import Formula Graph JSON");
            m_graphAsset.ImportJson(System.IO.File.ReadAllText(path));
            EditorUtility.SetDirty(m_graphAsset);
            AssetDatabase.SaveAssets();
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
            FlushPendingSave();
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
            Vector2? position = null,
            EntityStatsFormulaOperator operation = EntityStatsFormulaOperator.Add,
            EntityStatsFormulaInput input = EntityStatsFormulaInput.Level
        ) => AddNode(type, position, operation, input);

        EntityStatsFormulaNodeData AddNode(
            EntityStatsFormulaNodeType type,
            Vector2? position = null,
            EntityStatsFormulaOperator operation = EntityStatsFormulaOperator.Add,
            EntityStatsFormulaInput input = EntityStatsFormulaInput.Level
        )
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
                operation = operation,
                input = input,
                constant = type == EntityStatsFormulaNodeType.PercentConstant ? 25f : 0f,
                position = new Rect(nodePosition.x, nodePosition.y, 170, 92),
            };

            if (type == EntityStatsFormulaNodeType.Operator)
                node.position.size = new Vector2(150, 118);
            else if (type == EntityStatsFormulaNodeType.FormulaFunction)
                node.position.size = new Vector2(220, 92);
            else if (type == EntityStatsFormulaNodeType.Comment)
                node.position.size = new Vector2(260, 140);

            m_formula.nodes.Add(node);
            m_graphView.AddNode(node);
            Save();
            return node;
        }

        public EntityStatsFormulaGraph GraphAsset => m_graphAsset;

        public void ReloadFormula() => LoadFormula();

        public void Save() => QueueSave(false, false, null);

        public void SaveWithUndo(string undoName) => QueueSave(false, true, undoName);

        void SaveAndPersist() => QueueSave(true, false, null);

        void QueueSave(bool persistAssets, bool recordUndo, string undoName)
        {
            if (!m_graphAsset || m_formula == null)
                return;

            if (recordUndo)
            {
                m_queuedSaveNeedsUndo = true;
                m_queuedSaveUndoName = string.IsNullOrEmpty(undoName) ? "Edit Formula Graph" : undoName;
            }

            if (persistAssets)
            {
                SaveNow(true);
                return;
            }

            if (m_saveQueued)
                return;

            m_saveQueued = true;
            EditorApplication.delayCall += DelayedSave;
        }

        void DelayedSave() => SaveNow(false);

        void FlushPendingSave()
        {
            if (m_saveQueued)
                SaveNow(false);
        }

        void SaveNow(bool persistAssets)
        {
            if (m_saveQueued)
                EditorApplication.delayCall -= DelayedSave;

            m_saveQueued = false;

            if (!m_graphAsset || m_formula == null || m_graphView == null)
                return;

            if (m_queuedSaveNeedsUndo)
                Undo.RecordObject(m_graphAsset, m_queuedSaveUndoName ?? "Edit Formula Graph");

            m_queuedSaveNeedsUndo = false;
            m_queuedSaveUndoName = null;

            m_graphView.WriteBack(m_formula);
            m_graphAsset.InvalidateRuntimeCache();
            EditorUtility.SetDirty(m_graphAsset);

            if (persistAssets)
                AssetDatabase.SaveAssets();

            UpdatePreview();
        }

        EntityStatsFormulaContext CreatePreviewContext(EntityStatsFormulaTarget target)
        {
            switch (m_previewMode)
            {
                case StatsFormulaPreviewMode.SelectedEntity:
                    return GetSelectedEntityStats()
                        ? CreateEntityPreviewContext(target, GetSelectedEntityStats())
                        : EntityStatsFormulaContext.Preview.WithTarget(target);
                case StatsFormulaPreviewMode.CustomValues:
                    return CreateCustomPreviewContext(target);
                default:
                    return EntityStatsFormulaContext.Preview.WithTarget(target);
            }
        }

        EntityStatsFormulaContext CreateEntityPreviewContext(
            EntityStatsFormulaTarget target,
            EntityStatsManager sourceStats
        ) => new EntityStatsFormulaContext(
            target,
            EntityStatsFormulaContext.Preview.Get(EntityStatsFormulaInput.BuiltInValue),
            input => TryGetSelectedEntityStatsValue(sourceStats, input, out var value)
                ? value
                : EntityStatsFormulaContext.Preview.Get(input),
            (EntityStatsFormulaTarget referenceTarget, HashSet<EntityStatsFormulaTarget> visitingTargets, out float value) =>
            {
                value = 0f;
                return false;
            }
        );

        EntityStatsFormulaContext CreateCustomPreviewContext(EntityStatsFormulaTarget target) =>
            new EntityStatsFormulaContext(
                target,
                EntityStatsFormulaContext.Preview.Get(EntityStatsFormulaInput.BuiltInValue),
                input =>
                {
                    switch (input)
                    {
                        case EntityStatsFormulaInput.Level:
                            return m_customLevel;
                        case EntityStatsFormulaInput.Strength:
                            return m_customStrength;
                        case EntityStatsFormulaInput.Dexterity:
                            return m_customDexterity;
                        case EntityStatsFormulaInput.Vitality:
                            return m_customVitality;
                        case EntityStatsFormulaInput.Energy:
                            return m_customEnergy;
                        default:
                            return EntityStatsFormulaContext.Preview.Get(input);
                    }
                },
                (EntityStatsFormulaTarget referenceTarget, HashSet<EntityStatsFormulaTarget> visitingTargets, out float value) =>
                {
                    value = 0f;
                    return false;
                }
            );

        public void UpdatePreview()
        {
            if (m_previewLabel == null)
                return;

            if (m_formula == null)
            {
                m_previewLabel.text = "Preview: —";
                return;
            }

            var validation = EntityStatsFormulaValidator.Validate(m_formula);

            if (validation.hasErrors)
            {
                m_previewLabel.text = $"Preview: invalid ({validation.Summary})";
                return;
            }

            if (m_previewMode == StatsFormulaPreviewMode.AllEntities && m_entitySourceOptions.Count > 0)
            {
                var previews = m_entitySourceOptions
                    .Take(4)
                    .Select(option => FormatPreview(option.displayName, CreateEntityPreviewContext(m_formula.target, option.stats)))
                    .ToList();

                var remaining = m_entitySourceOptions.Count - previews.Count;
                m_previewLabel.text = remaining > 0
                    ? $"Preview: {string.Join("  |  ", previews)} (+{remaining} more)"
                    : $"Preview: {string.Join("  |  ", previews)}";
                return;
            }

            m_previewLabel.text = $"Preview: {FormatPreview(GetPreviewLabel(), CreatePreviewContext(m_formula.target))}";
        }

        string GetPreviewLabel()
        {
            switch (m_previewMode)
            {
                case StatsFormulaPreviewMode.SelectedEntity:
                    return GetSelectedEntityDisplayName();
                case StatsFormulaPreviewMode.CustomValues:
                    return "Custom";
                default:
                    return "Sample";
            }
        }

        string FormatPreview(string label, EntityStatsFormulaContext context)
        {
            if (!EntityStatsFormulaEvaluator.TryEvaluate(m_formula, context, out var value))
                return $"{label}: not connected";

            var metadata = EntityStatsFormulaTargetMetadataProvider.Get(m_formula.target, context);
            var builtIn = context.hasBuiltInValue ? $" built-in {metadata.Format(context.builtInValue)}" : string.Empty;
            var delta = context.hasBuiltInValue ? $" Δ {metadata.Format(value - context.builtInValue)}" : string.Empty;
            return $"{label}: {metadata.Format(value)}{builtIn}{delta}";
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

    class FormulaNodeTemplate
    {
        public string path;
        public EntityStatsFormulaNodeType type;
        public EntityStatsFormulaOperator operation = EntityStatsFormulaOperator.Add;
        public EntityStatsFormulaInput input = EntityStatsFormulaInput.Level;
    }

    class FormulaNodeSearchProvider : ScriptableObject, ISearchWindowProvider
    {
        StatsFormulaGraphView m_graphView;
        Vector2 m_position;
        Port m_connectedPort;

        public void Init(StatsFormulaGraphView graphView, Vector2 position, Port connectedPort)
        {
            m_graphView = graphView;
            m_position = position;
            m_connectedPort = connectedPort;
        }

        public List<SearchTreeEntry> CreateSearchTree(SearchWindowContext context)
        {
            var entries = new List<SearchTreeEntry>
            {
                new SearchTreeGroupEntry(new GUIContent("Create Formula Node"), 0),
                new SearchTreeGroupEntry(new GUIContent("Inputs"), 1),
            };

            foreach (EntityStatsFormulaInput input in Enum.GetValues(typeof(EntityStatsFormulaInput)))
            {
                entries.Add(new SearchTreeEntry(new GUIContent(input.ToString()))
                {
                    level = 2,
                    userData = new FormulaNodeTemplate
                    {
                        path = input.ToString(),
                        type = input == EntityStatsFormulaInput.BuiltInValue
                            ? EntityStatsFormulaNodeType.BuiltInValue
                            : EntityStatsFormulaNodeType.Input,
                        input = input,
                    },
                });
            }

            entries.Add(new SearchTreeGroupEntry(new GUIContent("Constants"), 1));
            entries.Add(CreateEntry("Number", EntityStatsFormulaNodeType.Constant, 2));
            entries.Add(CreateEntry("Percent Constant (25 = 25%)", EntityStatsFormulaNodeType.PercentConstant, 2));
            entries.Add(CreateEntry("Built In Value", EntityStatsFormulaNodeType.BuiltInValue, 2));

            entries.Add(new SearchTreeGroupEntry(new GUIContent("Math"), 1));
            foreach (EntityStatsFormulaOperator operation in Enum.GetValues(typeof(EntityStatsFormulaOperator)))
            {
                entries.Add(new SearchTreeEntry(new GUIContent(GetOperatorSearchName(operation)))
                {
                    level = 2,
                    userData = new FormulaNodeTemplate
                    {
                        path = operation.ToString(),
                        type = EntityStatsFormulaNodeType.Operator,
                        operation = operation,
                    },
                });
            }

            entries.Add(new SearchTreeGroupEntry(new GUIContent("Reusable"), 1));
            entries.Add(CreateEntry("Formula Reference", EntityStatsFormulaNodeType.FormulaReference, 2));
            entries.Add(CreateEntry("Formula Function Asset", EntityStatsFormulaNodeType.FormulaFunction, 2));

            entries.Add(new SearchTreeGroupEntry(new GUIContent("Organization"), 1));
            entries.Add(CreateEntry("Reroute", EntityStatsFormulaNodeType.Reroute, 2));
            entries.Add(CreateEntry("Comment", EntityStatsFormulaNodeType.Comment, 2));

            return entries;
        }

        static SearchTreeEntry CreateEntry(string name, EntityStatsFormulaNodeType type, int level) =>
            new SearchTreeEntry(new GUIContent(name))
            {
                level = level,
                userData = new FormulaNodeTemplate { path = name, type = type },
            };

        static string GetOperatorSearchName(EntityStatsFormulaOperator operation) =>
            operation == EntityStatsFormulaOperator.NormalizePercent
                ? "To Percent 0-1 / Normalize Percent"
                : operation.ToString();

        public bool OnSelectEntry(SearchTreeEntry searchTreeEntry, SearchWindowContext context)
        {
            var template = searchTreeEntry.userData as FormulaNodeTemplate;

            if (template == null || m_graphView == null)
                return false;

            m_graphView.AddNodeAndConnect(template, m_position, m_connectedPort);
            return true;
        }
    }

    class StatsFormulaGraphView : GraphView
    {
        readonly EntityStatsFormulaEditorWindow m_window;
        readonly Dictionary<string, Node> m_nodesByGuid = new();
        EntityStatsFormulaData m_formula;
        FormulaEdgeConnectorListener m_edgeConnectorListener;
        FormulaNodeSearchProvider m_searchProvider;
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
            m_searchProvider = ScriptableObject.CreateInstance<FormulaNodeSearchProvider>();
            SetupContextMenu();

            graphViewChanged = OnGraphViewChanged;
        }

        void SetupContextMenu()
        {
            nodeCreationRequest = context =>
            {
                OpenSearch(
                    context.screenMousePosition,
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
            evt.menu.AppendAction("Search/Create Node...", _ => OpenSearchAtPosition(position, null));
            evt.menu.AppendAction("Organization/Auto Layout", _ => AutoLayout());
            evt.menu.AppendAction("Organization/Frame All", _ => FrameAllNodes());
            evt.menu.AppendAction("Edit/Duplicate Selection", _ => DuplicateSelection());
        }

        void ShowNodeCreationMenu(Vector2 position, Port connectedPort) =>
            OpenSearchAtPosition(position, connectedPort);

        public void OpenSearchAtCenter()
        {
            var center = contentViewContainer.WorldToLocal(worldBound.center);
            OpenSearchAtPosition(center, null);
        }

        void OpenSearchAtPosition(Vector2 position, Port connectedPort)
        {
            var screenPosition = Event.current != null
                ? GUIUtility.GUIToScreenPoint(Event.current.mousePosition)
                : m_window.position.center;
            OpenSearch(screenPosition, position, connectedPort);
        }

        void OpenSearch(Vector2 screenPosition, Vector2 contentPosition, Port connectedPort)
        {
            m_searchProvider.Init(this, contentPosition, connectedPort);
            SearchWindow.Open(new SearchWindowContext(screenPosition), m_searchProvider);
        }

        public void AddNodeAndConnect(
            FormulaNodeTemplate template,
            Vector2 position,
            Port connectedPort
        ) => AddNodeAndConnect(
            template.type,
            position,
            connectedPort,
            template.operation,
            template.input
        );

        void AddNodeAndConnect(
            EntityStatsFormulaNodeType type,
            Vector2 position,
            Port connectedPort,
            EntityStatsFormulaOperator operation = EntityStatsFormulaOperator.Add,
            EntityStatsFormulaInput input = EntityStatsFormulaInput.Level
        )
        {
            var data = m_window.AddNodeFromGraph(type, position, operation, input);

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


        public void AutoLayout()
        {
            var editableNodes = nodes
                .Where(node => node.userData is EntityStatsFormulaNodeData data && data.type != EntityStatsFormulaNodeType.Comment)
                .OrderBy(node => node.title)
                .ToList();

            const float startX = 120f;
            const float startY = 120f;
            const float columnWidth = 260f;
            const float rowHeight = 130f;

            for (var i = 0; i < editableNodes.Count; i++)
            {
                var column = i / 6;
                var row = i % 6;
                var rect = editableNodes[i].GetPosition();
                rect.position = new Vector2(startX + column * columnWidth, startY + row * rowHeight);
                editableNodes[i].SetPosition(rect);
            }

            m_window.SaveWithUndo("Auto Layout Formula Nodes");
            FrameAllNodes();
        }

        public void FrameAllNodes()
        {
            if (nodes.Any())
                FrameAll();
        }

        public void DuplicateSelection()
        {
            if (m_formula == null)
                return;

            WriteBack(m_formula);

            var selectedNodes = selection
                .OfType<Node>()
                .Where(node => node.userData is EntityStatsFormulaNodeData data && data.type != EntityStatsFormulaNodeType.Result)
                .ToList();

            if (selectedNodes.Count == 0)
                return;

            Undo.RecordObject(m_window.GraphAsset, "Duplicate Formula Nodes");
            var guidMap = new Dictionary<string, EntityStatsFormulaNodeData>();

            foreach (var selectedNode in selectedNodes)
            {
                var source = (EntityStatsFormulaNodeData)selectedNode.userData;
                var copy = new EntityStatsFormulaNodeData
                {
                    type = source.type,
                    input = source.input,
                    formulaTarget = source.formulaTarget,
                    function = source.function,
                    operation = source.operation,
                    constant = source.constant,
                    title = source.title,
                    note = source.note,
                    groupId = source.groupId,
                    position = new Rect(
                        source.position.x + 32f,
                        source.position.y + 32f,
                        source.position.width,
                        source.position.height
                    ),
                };

                m_formula.nodes.Add(copy);
                AddNode(copy);
                guidMap[source.guid] = copy;
            }

            foreach (var connection in m_formula.connections.ToList())
            {
                if (
                    guidMap.TryGetValue(connection.outputNodeGuid, out var output)
                    && guidMap.TryGetValue(connection.inputNodeGuid, out var input)
                )
                {
                    m_formula.connections.Add(new EntityStatsFormulaConnectionData
                    {
                        outputNodeGuid = output.guid,
                        inputNodeGuid = input.guid,
                        inputPortName = connection.inputPortName,
                    });
                }
            }

            Load(m_formula);
            m_window.Save();
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
                    AddOutput(node, EntityStatsFormulaEvaluator.ValuePort);
                    var inputField = new EnumField("Stat", data.input);
                    inputField.RegisterValueChangedCallback(evt =>
                    {
                        Undo.RecordObject(m_window.GraphAsset, "Change Formula Input");
                        data.input = (EntityStatsFormulaInput)evt.newValue;
                        node.title = GetTitle(data);
                        m_window.Save();
                    });
                    node.extensionContainer.Add(inputField);
                    break;
                case EntityStatsFormulaNodeType.BuiltInValue:
                    AddOutput(node, EntityStatsFormulaEvaluator.ValuePort);
                    node.extensionContainer.Add(new Label("Default result for this stat."));
                    break;
                case EntityStatsFormulaNodeType.FormulaReference:
                    AddOutput(node, EntityStatsFormulaEvaluator.ValuePort);
                    var targetField = new EnumField("Target", data.formulaTarget);
                    targetField.RegisterValueChangedCallback(evt =>
                    {
                        Undo.RecordObject(m_window.GraphAsset, "Change Formula Reference");
                        data.formulaTarget = (EntityStatsFormulaTarget)evt.newValue;
                        node.title = GetTitle(data);
                        m_window.Save();
                    });
                    node.extensionContainer.Add(targetField);
                    break;
                case EntityStatsFormulaNodeType.FormulaFunction:
                    AddOutput(node, EntityStatsFormulaEvaluator.ValuePort);
                    var functionField = new ObjectField("Function")
                    {
                        objectType = typeof(EntityStatsFormulaFunction),
                        allowSceneObjects = false,
                        value = data.function,
                    };
                    functionField.RegisterValueChangedCallback(evt =>
                    {
                        Undo.RecordObject(m_window.GraphAsset, "Change Formula Function");
                        data.function = evt.newValue as EntityStatsFormulaFunction;
                        node.title = GetTitle(data);
                        m_window.Save();
                    });
                    node.extensionContainer.Add(functionField);
                    break;
                case EntityStatsFormulaNodeType.Constant:
                    AddOutput(node, EntityStatsFormulaEvaluator.ValuePort);
                    var floatField = new FloatField("Value") { value = data.constant };
                    floatField.RegisterValueChangedCallback(evt =>
                    {
                        Undo.RecordObject(m_window.GraphAsset, "Change Formula Constant");
                        data.constant = evt.newValue;
                        m_window.Save();
                    });
                    node.extensionContainer.Add(floatField);
                    break;
                case EntityStatsFormulaNodeType.PercentConstant:
                    AddOutput(node, EntityStatsFormulaEvaluator.ValuePort);
                    var percentField = new FloatField("Percent") { value = data.constant };
                    var normalizedPercentLabel = new Label($"Outputs {data.constant / 100f:0.###}");
                    percentField.tooltip = "Designer-facing percent: 25 outputs 0.25 at runtime.";
                    percentField.RegisterValueChangedCallback(evt =>
                    {
                        Undo.RecordObject(m_window.GraphAsset, "Change Formula Percent Constant");
                        data.constant = evt.newValue;
                        normalizedPercentLabel.text = $"Outputs {data.constant / 100f:0.###}";
                        m_window.Save();
                    });
                    node.extensionContainer.Add(percentField);
                    node.extensionContainer.Add(normalizedPercentLabel);
                    break;
                case EntityStatsFormulaNodeType.Operator:
                    foreach (var portName in EntityStatsFormulaValidator.GetOperatorInputPorts(data.operation))
                        AddInput(node, portName);
                    AddOutput(node, EntityStatsFormulaEvaluator.ValuePort);
                    var opField = new EnumField(data.operation);
                    opField.RegisterValueChangedCallback(evt =>
                    {
                        Undo.RecordObject(m_window.GraphAsset, "Change Formula Operator");
                        data.operation = (EntityStatsFormulaOperator)evt.newValue;
                        m_window.Save();
                        m_window.ReloadFormula();
                    });
                    node.extensionContainer.Add(opField);
                    break;
                case EntityStatsFormulaNodeType.Reroute:
                    AddInput(node, EntityStatsFormulaEvaluator.ValuePort);
                    AddOutput(node, EntityStatsFormulaEvaluator.ValuePort);
                    break;
                case EntityStatsFormulaNodeType.Comment:
                    var titleField = new TextField("Title") { value = data.title };
                    titleField.RegisterValueChangedCallback(evt =>
                    {
                        Undo.RecordObject(m_window.GraphAsset, "Change Formula Comment");
                        data.title = evt.newValue;
                        node.title = GetTitle(data);
                        m_window.Save();
                    });
                    var noteField = new TextField("Note") { value = data.note, multiline = true };
                    noteField.RegisterValueChangedCallback(evt =>
                    {
                        Undo.RecordObject(m_window.GraphAsset, "Change Formula Comment");
                        data.note = evt.newValue;
                        m_window.Save();
                    });
                    node.extensionContainer.Add(titleField);
                    node.extensionContainer.Add(noteField);
                    break;
                case EntityStatsFormulaNodeType.Result:
                    AddInput(node, EntityStatsFormulaEvaluator.ValuePort);
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

            var undoName = GetGraphChangeUndoName(change);
            var changedSerializedData = false;

            if (change.elementsToRemove != null)
            {
                foreach (var element in change.elementsToRemove)
                {
                    if (element is Node node && node.userData is EntityStatsFormulaNodeData data)
                    {
                        if (!changedSerializedData)
                        {
                            Undo.RecordObject(m_window.GraphAsset, undoName);
                            changedSerializedData = true;
                        }

                        m_formula?.nodes.Remove(data);
                    }
                    else if (element is Edge)
                    {
                        changedSerializedData = true;
                    }
                }
            }

            if (change.edgesToCreate != null && change.edgesToCreate.Count > 0)
                changedSerializedData = true;

            if (change.movedElements != null && change.movedElements.Count > 0)
                changedSerializedData = true;

            if (changedSerializedData)
            {
                if (!undoName.Contains("Delete"))
                    m_window.SaveWithUndo(undoName);
                else
                    m_window.Save();
            }

            return change;
        }

        static string GetGraphChangeUndoName(GraphViewChange change)
        {
            if (change.elementsToRemove != null)
            {
                if (change.elementsToRemove.Any(element => element is Node))
                    return "Delete Formula Node";

                if (change.elementsToRemove.Any(element => element is Edge))
                    return "Disconnect Formula Nodes";
            }

            if (change.edgesToCreate != null && change.edgesToCreate.Count > 0)
                return "Connect Formula Nodes";

            if (change.movedElements != null && change.movedElements.Count > 0)
                return "Move Formula Nodes";

            return "Edit Formula Graph";
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
                        EntityStatsFormulaOperator.Clamp => "Clamp",
                        EntityStatsFormulaOperator.PercentOf => "Percent Of",
                        EntityStatsFormulaOperator.NormalizePercent => "To Percent 0-1",
                        _ => data.operation.ToString(),
                    };
                case EntityStatsFormulaNodeType.BuiltInValue:
                    return "Built In Value";
                case EntityStatsFormulaNodeType.PercentConstant:
                    return "% Constant";
                case EntityStatsFormulaNodeType.FormulaReference:
                    return $"Formula: {data.formulaTarget}";
                case EntityStatsFormulaNodeType.FormulaFunction:
                    return data.function ? $"Function: {data.function.name}" : "Formula Function";
                case EntityStatsFormulaNodeType.Reroute:
                    return "Reroute";
                case EntityStatsFormulaNodeType.Comment:
                    return string.IsNullOrEmpty(data.title) ? "Comment" : data.title;
                case EntityStatsFormulaNodeType.Result:
                    return "Result";
                default:
                    return data.type.ToString();
            }
        }
    }
}
#endif
