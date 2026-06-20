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
        VisualElement m_previewCard;
        Label m_previewTargetLabel;
        Label m_previewResultLabel;
        Label m_previewBuiltInLabel;
        Label m_previewChangeLabel;
        Label m_previewStatusLabel;
        Label m_healthBadge;
        VisualElement m_diagnosticsContainer;
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
            LoadStyleSheet();

            RefreshEntitySourceOptions();

            var toolbar = new VisualElement { name = "StatsFormulaToolbar" };
            toolbar.AddToClassList("stats-formula-toolbar");

            var graphSection = new VisualElement();
            graphSection.AddToClassList("toolbar-section");
            graphSection.AddToClassList("graph-toolbar-section");
            graphSection.Add(new Button(CreateAsset) { text = "New Graph Asset" });

            graphSection.Add(CreateInlineToolbarLabel("Graph"));
            var assetField = new ObjectField
            {
                objectType = typeof(EntityStatsFormulaGraph),
                allowSceneObjects = false,
                value = m_graphAsset,
            };
            assetField.AddToClassList("toolbar-graph-field");
            assetField.RegisterValueChangedCallback(evt =>
            {
                m_graphAsset = evt.newValue as EntityStatsFormulaGraph;
                LoadFormula();
            });
            graphSection.Add(assetField);

            graphSection.Add(CreateInlineToolbarLabel("Stat"));
            var targetField = new EnumField(m_target);
            targetField.AddToClassList("toolbar-stat-field");
            targetField.RegisterValueChangedCallback(evt =>
            {
                m_target = (EntityStatsFormulaTarget)evt.newValue;
                LoadFormula();
            });
            graphSection.Add(targetField);

            graphSection.Add(CreateInlineToolbarLabel("Graph / Stat / Enabled"));
            m_enabledToggle = new Toggle { value = m_formula?.enabled ?? true };
            m_enabledToggle.AddToClassList("toolbar-enabled-toggle");
            m_enabledToggle.RegisterValueChangedCallback(evt =>
            {
                if (m_formula == null)
                    return;

                Undo.RecordObject(m_graphAsset, "Toggle Formula Enabled");
                m_formula.enabled = evt.newValue;
                Save();
            });
            graphSection.Add(m_enabledToggle);

            graphSection.Add(CreateInlineToolbarLabel("Status"));
            m_healthBadge = new Label("—");
            m_healthBadge.AddToClassList("formula-health-badge");
            graphSection.Add(m_healthBadge);
            toolbar.Add(graphSection);

            var exampleSection = CreateToolbarSection("Examples");
            var exampleField = new EnumField(m_exampleTarget);
            exampleField.RegisterValueChangedCallback(evt =>
            {
                m_exampleTarget = (EntityStatsFormulaTarget)evt.newValue;
            });
            exampleSection.Add(exampleField);
            exampleSection.Add(new Button(PopulateSelectedBuiltInExample) { text = "Load Example" });
            exampleSection.Add(new Button(PopulateAllBuiltInExamples) { text = "Load All Examples" });
            toolbar.Add(exampleSection);

            var saveSection = CreateToolbarSection("Save / JSON");
            saveSection.Add(new Button(SaveAndPersist) { text = "Save" });
            saveSection.Add(new Button(ExportJson) { text = "Export JSON" });
            saveSection.Add(new Button(ImportJson) { text = "Import JSON" });
            toolbar.Add(saveSection);

            var toolsSection = CreateToolbarSection("Edit Tools");
            toolsSection.Add(new Button(() => Undo.PerformUndo()) { text = "Undo" });
            toolsSection.Add(new Button(() => Undo.PerformRedo()) { text = "Redo" });
            toolsSection.Add(new Button(() => m_graphView.CreateGroupFromSelection()) { text = "Group" });
            toolsSection.Add(new Button(() => m_graphView.AutoLayout()) { text = "Auto Layout" });
            toolsSection.Add(new Button(() => m_graphView.FrameAllNodes()) { text = "Frame All" });
            toolbar.Add(toolsSection);

            rootVisualElement.Add(toolbar);

            var content = new VisualElement { name = "StatsFormulaContent" };
            content.AddToClassList("stats-formula-content");

            var graphColumn = new VisualElement();
            graphColumn.AddToClassList("stats-formula-graph-column");

            m_graphView = new StatsFormulaGraphView(this);
            graphColumn.Add(m_graphView);
            content.Add(graphColumn);

            m_previewCard = CreatePreviewCard();
            content.Add(m_previewCard);
            rootVisualElement.Add(content);

            m_diagnosticsContainer = new VisualElement();
            m_diagnosticsContainer.AddToClassList("diagnostics-panel");
            rootVisualElement.Add(m_diagnosticsContainer);

            LoadFormula();
        }

        void LoadStyleSheet()
        {
            var styleSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(
                "Assets/PLAYER TWO/ARPG Project/Core/Entity/Stats Formula/Editor/StatsFormulaEditor.uss"
            );

            if (styleSheet != null)
                rootVisualElement.styleSheets.Add(styleSheet);
        }

        VisualElement CreateToolbarSection(string title)
        {
            var section = new VisualElement();
            section.AddToClassList("toolbar-section");
            section.Add(new Label(title) { name = "ToolbarSectionLabel" });
            return section;
        }

        static Label CreateInlineToolbarLabel(string text)
        {
            var label = new Label(text);
            label.AddToClassList("toolbar-inline-label");
            return label;
        }

        VisualElement CreatePreviewControls()
        {
            var controls = new VisualElement();
            controls.AddToClassList("preview-controls");

            var entityField = new PopupField<string>(
                "Entity",
                m_entitySourceNames,
                Mathf.Clamp(m_selectedEntityIndex, 0, Mathf.Max(0, m_entitySourceNames.Count - 1))
            );
            entityField.RegisterValueChangedCallback(evt =>
            {
                m_selectedEntityIndex = Mathf.Max(0, m_entitySourceNames.IndexOf(evt.newValue));
                UpdatePreview();
            });
            controls.Add(entityField);
            controls.Add(new Button(BuildWindow) { text = "Refresh Entities" });

            var previewModeField = new EnumField("Preview With", m_previewMode);
            previewModeField.RegisterValueChangedCallback(evt =>
            {
                m_previewMode = (StatsFormulaPreviewMode)evt.newValue;
                UpdatePreview();
            });
            controls.Add(previewModeField);
            AddCustomPreviewField(controls, "Lvl", () => m_customLevel, value => m_customLevel = value);
            AddCustomPreviewField(controls, "Str", () => m_customStrength, value => m_customStrength = value);
            AddCustomPreviewField(controls, "Dex", () => m_customDexterity, value => m_customDexterity = value);
            AddCustomPreviewField(controls, "Vit", () => m_customVitality, value => m_customVitality = value);
            AddCustomPreviewField(controls, "Ene", () => m_customEnergy, value => m_customEnergy = value);
            return controls;
        }

        VisualElement CreatePreviewCard()
        {
            var card = new VisualElement();
            card.AddToClassList("preview-card");
            card.Add(new Label("Preview") { name = "PreviewCardTitle" });
            card.Add(CreatePreviewControls());

            var previewScroll = new ScrollView(ScrollViewMode.Vertical);
            previewScroll.AddToClassList("preview-scroll");
            m_previewTargetLabel = AddPreviewRow(previewScroll, "Target", "—");
            m_previewResultLabel = AddPreviewRow(previewScroll, "Result", "—");
            m_previewResultLabel.AddToClassList("preview-result-value");
            m_previewBuiltInLabel = AddPreviewRow(previewScroll, "Built-in", "—");
            m_previewChangeLabel = AddPreviewRow(previewScroll, "Change", "—");
            m_previewStatusLabel = AddPreviewRow(previewScroll, "Status", "—");
            card.Add(previewScroll);
            return card;
        }

        Label AddPreviewRow(VisualElement card, string name, string value)
        {
            var row = new VisualElement();
            row.AddToClassList("preview-row");
            row.Add(new Label(name + ":") { name = "PreviewRowName" });
            var label = new Label(value);
            label.AddToClassList("preview-row-value");
            label.style.whiteSpace = WhiteSpace.Normal;
            row.Add(label);
            card.Add(row);
            return label;
        }

        void AddCustomPreviewField(VisualElement toolbar, string label, Func<int> getter, Action<int> setter)
        {
            var field = new IntegerField(label) { value = getter() };
            field.style.width = 72f;
            field.RegisterValueChangedCallback(evt =>
            {
                var clampedValue = Mathf.Max(0, evt.newValue);
                if (clampedValue != evt.newValue)
                    field.SetValueWithoutNotify(clampedValue);

                setter(clampedValue);
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

        public void FlushPendingSave()
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
            if (m_previewCard == null)
                return;

            if (m_formula == null)
            {
                SetPreviewCard("—", "—", "—", "—", "No formula");
                SetHealthBadge(null, null);
                return;
            }

            var validation = EntityStatsFormulaValidator.Validate(m_formula);
            var graphValidation = m_graphAsset ? m_graphAsset.ValidateGraph() : null;

            UpdateDiagnostics(validation, graphValidation);
            m_graphView?.ApplyDiagnostics(validation, graphValidation);

            SetHealthBadge(validation, graphValidation);

            if (validation.hasErrors)
            {
                SetPreviewCard(m_formula.target.ToString(), "—", "—", "—", $"Invalid ({validation.Summary})");
                return;
            }

            if (m_previewMode == StatsFormulaPreviewMode.AllEntities && m_entitySourceOptions.Count > 0)
            {
                var previews = m_entitySourceOptions
                    .Take(4)
                    .Select(option => FormatPreview(option.displayName, CreateEntityPreviewContext(m_formula.target, option.stats)))
                    .ToList();

                var remaining = m_entitySourceOptions.Count - previews.Count;
                SetPreviewCard(m_formula.target.ToString(), string.Join("\n", previews), "—", "—", remaining > 0 ? $"Valid (+{remaining} more)" : "Valid");
                return;
            }

            SetPreviewCard(GetPreviewLabel(), CreatePreviewContext(m_formula.target), validation);
        }

        void UpdateDiagnostics(
            EntityStatsFormulaValidationResult validation,
            EntityStatsFormulaValidationResult graphValidation
        )
        {
            if (m_diagnosticsContainer == null)
                return;

            m_diagnosticsContainer.Clear();

            var diagnostics = new List<EntityStatsFormulaDiagnostic>();

            if (validation != null)
                diagnostics.AddRange(validation.diagnostics);

            if (graphValidation != null)
                diagnostics.AddRange(graphValidation.diagnostics);

            if (diagnostics.Count == 0)
            {
                var valid = new Label("Diagnostics: Valid");
                valid.AddToClassList("diagnostic-valid");
                m_diagnosticsContainer.Add(valid);
                return;
            }

            var errors = diagnostics.Count(diagnostic => diagnostic.severity == EntityStatsFormulaDiagnosticSeverity.Error);
            var warnings = diagnostics.Count(diagnostic => diagnostic.severity == EntityStatsFormulaDiagnosticSeverity.Warning);
            var header = new Label($"Diagnostics: {errors} error(s), {warnings} warning(s)");
            header.AddToClassList("diagnostics-header");
            m_diagnosticsContainer.Add(header);

            foreach (var diagnostic in diagnostics)
                m_diagnosticsContainer.Add(CreateDiagnosticElement(diagnostic));
        }

        VisualElement CreateDiagnosticElement(EntityStatsFormulaDiagnostic diagnostic)
        {
            var node = FindNodeData(diagnostic.nodeGuid);
            var button = new Button(() => m_graphView?.FrameNode(diagnostic.nodeGuid));
            button.AddToClassList("diagnostic-item");
            button.AddToClassList("diagnostic-" + diagnostic.severity.ToString().ToLowerInvariant());
            button.text = string.Empty;
            button.Add(new Label($"{diagnostic.severity}: {diagnostic.message}"));
            if (node != null)
                button.Add(new Label($"Node: {StatsFormulaGraphView.GetTitle(node)}"));
            if (!string.IsNullOrEmpty(diagnostic.portName))
                button.Add(new Label($"Port: {diagnostic.portName}"));
            if (!string.IsNullOrEmpty(diagnostic.nodeGuid))
                button.Add(new Label("Frame Node"));
            return button;
        }

        EntityStatsFormulaNodeData FindNodeData(string guid) =>
            string.IsNullOrEmpty(guid) || m_formula?.nodes == null
                ? null
                : m_formula.nodes.FirstOrDefault(node => node.guid == guid);

        void SetHealthBadge(EntityStatsFormulaValidationResult validation, EntityStatsFormulaValidationResult graphValidation)
        {
            if (m_healthBadge == null)
                return;

            var diagnostics = new List<EntityStatsFormulaDiagnostic>();
            if (validation != null)
                diagnostics.AddRange(validation.diagnostics);
            if (graphValidation != null)
                diagnostics.AddRange(graphValidation.diagnostics);

            m_healthBadge.RemoveFromClassList("health-valid");
            m_healthBadge.RemoveFromClassList("health-warning");
            m_healthBadge.RemoveFromClassList("health-error");
            m_healthBadge.RemoveFromClassList("health-disabled");

            if (m_formula != null && !m_formula.enabled)
            {
                m_healthBadge.text = "Disabled";
                m_healthBadge.AddToClassList("health-disabled");
                return;
            }

            var errors = diagnostics.Count(diagnostic => diagnostic.severity == EntityStatsFormulaDiagnosticSeverity.Error);
            var warnings = diagnostics.Count(diagnostic => diagnostic.severity == EntityStatsFormulaDiagnosticSeverity.Warning);
            if (errors > 0)
            {
                m_healthBadge.text = $"✖ {errors} error" + (errors == 1 ? string.Empty : "s");
                m_healthBadge.AddToClassList("health-error");
            }
            else if (warnings > 0)
            {
                m_healthBadge.text = $"⚠ {warnings} warning" + (warnings == 1 ? string.Empty : "s");
                m_healthBadge.AddToClassList("health-warning");
            }
            else
            {
                m_healthBadge.text = "✓ Valid";
                m_healthBadge.AddToClassList("health-valid");
            }
        }

        void SetPreviewCard(string target, string result, string builtIn, string change, string status)
        {
            if (m_previewTargetLabel == null)
                return;

            m_previewTargetLabel.text = target;
            m_previewResultLabel.text = result;
            m_previewBuiltInLabel.text = builtIn;
            m_previewChangeLabel.text = change;
            m_previewStatusLabel.text = status;
        }

        void SetPreviewCard(string label, EntityStatsFormulaContext context, EntityStatsFormulaValidationResult validation)
        {
            if (!EntityStatsFormulaEvaluator.TryEvaluateRaw(m_formula, context, out var value, out var rawValue))
            {
                SetPreviewCard(label, "Not connected", "—", "—", "Missing result input");
                return;
            }

            var metadata = EntityStatsFormulaTargetMetadataProvider.Get(m_formula.target, context);
            var result = metadata.Format(value);
            if (!Mathf.Approximately(rawValue, value))
                result += $" (raw {metadata.Format(rawValue)}, clamped)";

            var builtIn = context.hasBuiltInValue ? metadata.Format(context.builtInValue) : "—";
            var change = "—";
            if (context.hasBuiltInValue)
            {
                var difference = value - context.builtInValue;
                change = (difference > 0f ? "+" : string.Empty) + metadata.Format(difference);
            }

            SetPreviewCard(label, result, builtIn, change, validation?.Summary ?? "Valid");
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
            if (!EntityStatsFormulaEvaluator.TryEvaluateRaw(m_formula, context, out var value, out var rawValue))
                return $"{label}: not connected";

            var metadata = EntityStatsFormulaTargetMetadataProvider.Get(m_formula.target, context);
            var result = metadata.Format(value);
            var rawSuffix = !Mathf.Approximately(rawValue, value)
                ? $" (raw {metadata.Format(rawValue)}, clamped)"
                : string.Empty;

            if (!context.hasBuiltInValue)
                return $"{label} → Result {result}{rawSuffix}";

            var builtIn = metadata.Format(context.builtInValue);
            var difference = value - context.builtInValue;
            var change = metadata.Format(difference);
            var sign = difference > 0f ? "+" : string.Empty;
            return $"{label} → Result {result}{rawSuffix} (Built-in {builtIn}, Change {sign}{change})";
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

    class FormulaCommentResizeManipulator : MouseManipulator
    {
        readonly Node m_node;
        readonly EntityStatsFormulaNodeData m_data;
        readonly EntityStatsFormulaEditorWindow m_window;
        Vector2 m_startMouse;
        Rect m_startRect;
        bool m_active;

        public FormulaCommentResizeManipulator(
            Node node,
            EntityStatsFormulaNodeData data,
            EntityStatsFormulaEditorWindow window
        )
        {
            m_node = node;
            m_data = data;
            m_window = window;
            activators.Add(new ManipulatorActivationFilter { button = MouseButton.LeftMouse });
        }

        protected override void RegisterCallbacksOnTarget()
        {
            target.RegisterCallback<MouseDownEvent>(OnMouseDown);
            target.RegisterCallback<MouseMoveEvent>(OnMouseMove);
            target.RegisterCallback<MouseUpEvent>(OnMouseUp);
        }

        protected override void UnregisterCallbacksFromTarget()
        {
            target.UnregisterCallback<MouseDownEvent>(OnMouseDown);
            target.UnregisterCallback<MouseMoveEvent>(OnMouseMove);
            target.UnregisterCallback<MouseUpEvent>(OnMouseUp);
        }

        void OnMouseDown(MouseDownEvent evt)
        {
            if (!CanStartManipulation(evt))
                return;

            Undo.RecordObject(m_window.GraphAsset, "Resize Formula Comment");
            m_active = true;
            m_startMouse = evt.mousePosition;
            m_startRect = m_node.GetPosition();
            target.CaptureMouse();
            evt.StopPropagation();
        }

        void OnMouseMove(MouseMoveEvent evt)
        {
            if (!m_active || !target.HasMouseCapture())
                return;

            var delta = evt.mousePosition - m_startMouse;
            var rect = m_startRect;
            rect.width = Mathf.Max(180f, m_startRect.width + delta.x);
            rect.height = Mathf.Max(120f, m_startRect.height + delta.y);
            m_node.SetPosition(rect);
            m_node.style.width = rect.width;
            m_node.style.height = rect.height;
            m_data.position = rect;
            evt.StopPropagation();
        }

        void OnMouseUp(MouseUpEvent evt)
        {
            if (!m_active || !CanStopManipulation(evt))
                return;

            m_active = false;
            target.ReleaseMouse();
            m_window.Save();
            evt.StopPropagation();
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

            entries.Add(new SearchTreeGroupEntry(new GUIContent("Flow"), 1));
            entries.Add(CreateEntry("Reroute", EntityStatsFormulaNodeType.Reroute, 2));
            entries.Add(new SearchTreeGroupEntry(new GUIContent("Annotations"), 1));
            entries.Add(CreateEntry("Resizable Comment Box", EntityStatsFormulaNodeType.Comment, 2));

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

    class FormulaClipboardData
    {
        public List<EntityStatsFormulaNodeData> nodes = new();
        public List<EntityStatsFormulaConnectionData> connections = new();
    }

    class StatsFormulaGraphView : GraphView
    {
        readonly EntityStatsFormulaEditorWindow m_window;
        static FormulaClipboardData s_clipboard;
        readonly Dictionary<string, Node> m_nodesByGuid = new();
        readonly Dictionary<string, Group> m_groupsByGuid = new();
        EntityStatsFormulaData m_formula;
        static readonly Vector2 ContextSearchMenuOffset = new Vector2(177f, 30f);
        FormulaEdgeConnectorListener m_edgeConnectorListener;
        FormulaNodeSearchProvider m_searchProvider;
        bool m_isLoading;

        public StatsFormulaGraphView(EntityStatsFormulaEditorWindow window)
        {
            m_window = window;
            focusable = true;
            style.flexGrow = 1;
            style.minHeight = 0f;

            Insert(0, new GridBackground());
            this.AddManipulator(new ContentZoomer());
            this.AddManipulator(new ContentDragger());
            this.AddManipulator(new SelectionDragger());
            this.AddManipulator(new RectangleSelector());
            AddMiniMap();
            RegisterCallback<KeyDownEvent>(OnKeyDown);

            m_edgeConnectorListener = new FormulaEdgeConnectorListener(this);
            m_searchProvider = ScriptableObject.CreateInstance<FormulaNodeSearchProvider>();
            SetupContextMenu();

            graphViewChanged = OnGraphViewChanged;
        }

        void AddMiniMap()
        {
            var miniMap = new MiniMap { anchored = false };
            miniMap.SetPosition(new Rect(12, 32, 220, 150));
            Add(miniMap);
        }

        void OnKeyDown(KeyDownEvent evt)
        {
            if (evt.keyCode == KeyCode.Delete || evt.keyCode == KeyCode.Backspace)
            {
                DeleteSelectedFormulaElements();
                evt.StopPropagation();
                return;
            }

            if (!evt.ctrlKey && !evt.commandKey)
                return;

            switch (evt.keyCode)
            {
                case KeyCode.C:
                    CopySelectionToClipboard();
                    evt.StopPropagation();
                    break;
                case KeyCode.X:
                    CutSelectionToClipboard();
                    evt.StopPropagation();
                    break;
                case KeyCode.V:
                    PasteClipboard();
                    evt.StopPropagation();
                    break;
                case KeyCode.D:
                    DuplicateSelection();
                    evt.StopPropagation();
                    break;
                case KeyCode.Z:
                    Undo.PerformUndo();
                    evt.StopPropagation();
                    break;
                case KeyCode.Y:
                    Undo.PerformRedo();
                    evt.StopPropagation();
                    break;
            }
        }

        void SetupContextMenu()
        {
            nodeCreationRequest = context =>
            {
                OpenSearch(
                    context.screenMousePosition + ContextSearchMenuOffset,
                    GetContentPositionFromScreen(context.screenMousePosition),
                    context.target as Port
                );
            };
        }

        public override void BuildContextualMenu(ContextualMenuPopulateEvent evt)
        {
            var position = GetContentPosition(evt.localMousePosition);
            var screenPosition = GetScreenPosition(evt.localMousePosition);
            evt.menu.AppendAction("Create Node...", _ => OpenSearch(screenPosition + ContextSearchMenuOffset, position, null));
            evt.menu.AppendSeparator();
            evt.menu.AppendAction("Duplicate", _ => DuplicateSelection(), CanDuplicateSelection());
            evt.menu.AppendAction("Copy", _ => CopySelectionToClipboard(), CanDuplicateSelection());
            evt.menu.AppendAction("Cut", _ => CutSelectionToClipboard(), CanDuplicateSelection());
            evt.menu.AppendAction("Paste", _ => PasteClipboard(), CanPasteClipboard());
            evt.menu.AppendAction("Delete", _ => DeleteSelectedFormulaElements(), CanDuplicateSelection());
            evt.menu.AppendSeparator();
            evt.menu.AppendAction("Group Selection", _ => CreateGroupFromSelection(), CanDuplicateSelection());
            evt.menu.AppendAction("Auto Layout", _ => AutoLayout());
            evt.menu.AppendAction("Frame All", _ => FrameAllNodes());
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
            var panelPosition = contentViewContainer.LocalToWorld(position);
            OpenSearch(GUIUtility.GUIToScreenPoint(panelPosition), position, connectedPort);
        }

        Vector2 GetScreenPosition(Vector2 localPosition)
        {
            var panelPosition = this.LocalToWorld(localPosition);
            return GUIUtility.GUIToScreenPoint(panelPosition);
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
            ApplyEdgeStyle(edge, output.node?.userData as EntityStatsFormulaNodeData);
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


        public void OpenNodeCreationMenuForDroppedEdge(Edge edge, Vector2 localMousePosition)
        {
            if (edge == null)
                return;

            var connectedPort = edge.output ?? edge.input;

            if (connectedPort == null)
                return;

            OpenSearch(
                GetScreenPosition(localMousePosition) + new Vector2(120f, -96f),
                GetContentPosition(localMousePosition),
                connectedPort
            );
        }


        public void FrameNode(string guid)
        {
            if (string.IsNullOrEmpty(guid) || !m_nodesByGuid.TryGetValue(guid, out var node))
                return;

            ClearSelection();
            AddToSelection(node);
            FrameSelection();
        }

        public void ApplyDiagnostics(
            EntityStatsFormulaValidationResult validation,
            EntityStatsFormulaValidationResult graphValidation
        )
        {
            var severitiesByNodeGuid = new Dictionary<string, EntityStatsFormulaDiagnosticSeverity>();
            CollectNodeDiagnosticSeverities(validation, severitiesByNodeGuid);
            CollectNodeDiagnosticSeverities(graphValidation, severitiesByNodeGuid);

            foreach (var pair in m_nodesByGuid)
            {
                if (severitiesByNodeGuid.TryGetValue(pair.Key, out var severity))
                    ApplyDiagnosticStyle(pair.Value, severity);
                else
                    ClearDiagnosticStyle(pair.Value);
            }
        }

        static void CollectNodeDiagnosticSeverities(
            EntityStatsFormulaValidationResult validation,
            Dictionary<string, EntityStatsFormulaDiagnosticSeverity> severitiesByNodeGuid
        )
        {
            if (validation == null)
                return;

            foreach (var diagnostic in validation.diagnostics)
            {
                if (diagnostic == null || string.IsNullOrEmpty(diagnostic.nodeGuid))
                    continue;

                if (
                    !severitiesByNodeGuid.TryGetValue(diagnostic.nodeGuid, out var currentSeverity)
                    || GetDiagnosticSeverityRank(diagnostic.severity) > GetDiagnosticSeverityRank(currentSeverity)
                )
                    severitiesByNodeGuid[diagnostic.nodeGuid] = diagnostic.severity;
            }
        }

        static int GetDiagnosticSeverityRank(EntityStatsFormulaDiagnosticSeverity severity)
        {
            switch (severity)
            {
                case EntityStatsFormulaDiagnosticSeverity.Error:
                    return 3;
                case EntityStatsFormulaDiagnosticSeverity.Warning:
                    return 2;
                case EntityStatsFormulaDiagnosticSeverity.Info:
                    return 1;
                default:
                    return 0;
            }
        }

        static void ApplyDiagnosticStyle(Node node, EntityStatsFormulaDiagnosticSeverity severity)
        {
            var color = GetDiagnosticColor(severity);
            node.style.borderTopColor = color;
            node.style.borderRightColor = color;
            node.style.borderBottomColor = color;
            node.style.borderLeftColor = color;
            node.style.borderTopWidth = 3f;
            node.style.borderRightWidth = 3f;
            node.style.borderBottomWidth = 3f;
            node.style.borderLeftWidth = 3f;
        }

        static void ClearDiagnosticStyle(Node node)
        {
            node.style.borderTopWidth = 0f;
            node.style.borderRightWidth = 0f;
            node.style.borderBottomWidth = 0f;
            node.style.borderLeftWidth = 0f;
        }

        static Color GetDiagnosticColor(EntityStatsFormulaDiagnosticSeverity severity)
        {
            switch (severity)
            {
                case EntityStatsFormulaDiagnosticSeverity.Error:
                    return new Color(1f, 0.22f, 0.18f, 1f);
                case EntityStatsFormulaDiagnosticSeverity.Warning:
                    return new Color(1f, 0.68f, 0.15f, 1f);
                default:
                    return new Color(0.25f, 0.55f, 1f, 1f);
            }
        }

        public void ClearGraph()
        {
            m_isLoading = true;

            foreach (var element in graphElements.ToList())
                RemoveElement(element);

            m_nodesByGuid.Clear();
            m_groupsByGuid.Clear();
            m_formula = null;
            m_isLoading = false;
        }

        public void Load(EntityStatsFormulaData formula)
        {
            ClearGraph();
            m_formula = formula;
            m_formula.nodes ??= new List<EntityStatsFormulaNodeData>();
            m_formula.connections ??= new List<EntityStatsFormulaConnectionData>();
            m_formula.groups ??= new List<EntityStatsFormulaGroupData>();

            foreach (var node in m_formula.nodes)
                AddNode(node);

            foreach (var group in m_formula.groups)
                AddGroup(group);

            foreach (var connection in m_formula.connections)
                AddConnection(connection);
        }

        public void AddNode(EntityStatsFormulaNodeData data)
        {
            var node = CreateNode(data);
            m_nodesByGuid[data.guid] = node;
            AddElement(node);
        }

        void AddGroup(EntityStatsFormulaGroupData data)
        {
            var group = new Group { title = $"▦ {data.title}", userData = data };
            group.AddToClassList("formula-group");
            group.style.backgroundColor = data.color;
            group.SetPosition(data.position);
            var descriptionField = new TextField
            {
                value = data.description,
                tooltip = "Short subtitle or note for this formula group.",
            };
            descriptionField.AddToClassList("group-description-field");
            descriptionField.RegisterValueChangedCallback(evt =>
            {
                Undo.RecordObject(m_window.GraphAsset, "Change Formula Group Description");
                data.description = evt.newValue;
                m_window.Save();
            });
            group.headerContainer.Add(descriptionField);
            var colorField = new ColorField("Color") { value = data.color, showAlpha = true };
            colorField.AddToClassList("group-color-field");
            colorField.style.width = 220f;
            colorField.style.height = 22f;
            colorField.RegisterValueChangedCallback(evt =>
            {
                Undo.RecordObject(m_window.GraphAsset, "Change Formula Group Color");
                data.color = evt.newValue;
                group.style.backgroundColor = data.color;
                m_window.Save();
            });
            group.headerContainer.Add(colorField);
            var collapseButton = new Button(() => ToggleGroupCollapsed(group, data)) { text = data.collapsed ? "Expand" : "Collapse" };
            collapseButton.AddToClassList("group-collapse-button");
            group.headerContainer.Add(collapseButton);
            m_groupsByGuid[data.guid] = group;
            AddElement(group);

            foreach (var nodeData in m_formula.nodes.Where(node => node.groupId == data.guid))
            {
                if (m_nodesByGuid.TryGetValue(nodeData.guid, out var node))
                    group.AddElement(node);
            }

            SetGroupCollapsed(group, data.collapsed);
        }

        void ToggleGroupCollapsed(Group group, EntityStatsFormulaGroupData data)
        {
            data.collapsed = !data.collapsed;
            SetGroupCollapsed(group, data.collapsed);
            var button = group.headerContainer.Query<Button>(className: "group-collapse-button").First();
            if (button != null)
                button.text = data.collapsed ? "Expand" : "Collapse";

            m_window.Save();
        }

        void SetGroupCollapsed(Group group, bool collapsed)
        {
            var containedNodes = group.containedElements.OfType<Node>().ToList();
            var containedNodeSet = new HashSet<Node>(containedNodes);

            foreach (var node in containedNodes)
                node.style.display = collapsed ? DisplayStyle.None : DisplayStyle.Flex;

            foreach (var edge in edges.ToList())
            {
                if (edge.input?.node != null && containedNodeSet.Contains(edge.input.node)
                    || edge.output?.node != null && containedNodeSet.Contains(edge.output.node))
                    edge.style.display = collapsed ? DisplayStyle.None : DisplayStyle.Flex;
            }

            group.AddToClassList(collapsed ? "formula-group-collapsed" : "formula-group-expanded");
            group.RemoveFromClassList(collapsed ? "formula-group-expanded" : "formula-group-collapsed");
        }

        DropdownMenuAction.Status CanDuplicateSelection() =>
            selection.OfType<Node>().Any(node => node.userData is EntityStatsFormulaNodeData data && data.type != EntityStatsFormulaNodeType.Result)
                ? DropdownMenuAction.Status.Normal
                : DropdownMenuAction.Status.Disabled;

        DropdownMenuAction.Status CanPasteClipboard() =>
            s_clipboard != null && s_clipboard.nodes.Count > 0
                ? DropdownMenuAction.Status.Normal
                : DropdownMenuAction.Status.Disabled;

        public void CopySelectionToClipboard()
        {
            if (m_formula == null)
                return;

            m_window.FlushPendingSave();
            WriteBack(m_formula);
            var selectedNodes = selection
                .OfType<Node>()
                .Select(node => node.userData as EntityStatsFormulaNodeData)
                .Where(data => data != null && data.type != EntityStatsFormulaNodeType.Result)
                .ToList();

            if (selectedNodes.Count == 0)
                return;

            var selectedGuids = new HashSet<string>(selectedNodes.Select(node => node.guid));
            s_clipboard = new FormulaClipboardData
            {
                nodes = selectedNodes.Select(CloneNodeForClipboard).ToList(),
                connections = m_formula.connections
                    .Where(connection => selectedGuids.Contains(connection.outputNodeGuid) && selectedGuids.Contains(connection.inputNodeGuid))
                    .Select(CloneConnection)
                    .ToList(),
            };
        }

        public void CutSelectionToClipboard()
        {
            CopySelectionToClipboard();
            DeleteSelectedFormulaElements();
        }

        public void PasteClipboard()
        {
            if (m_formula == null || s_clipboard == null || s_clipboard.nodes.Count == 0)
                return;

            Undo.RecordObject(m_window.GraphAsset, "Paste Formula Nodes");
            var guidMap = new Dictionary<string, EntityStatsFormulaNodeData>();

            foreach (var source in s_clipboard.nodes)
            {
                var copy = CloneNodeForPaste(source, new Vector2(36f, 36f));
                m_formula.nodes.Add(copy);
                AddNode(copy);
                guidMap[source.guid] = copy;
            }

            foreach (var connection in s_clipboard.connections)
            {
                if (
                    guidMap.TryGetValue(connection.outputNodeGuid, out var output)
                    && guidMap.TryGetValue(connection.inputNodeGuid, out var input)
                )
                {
                    var copy = new EntityStatsFormulaConnectionData
                    {
                        outputNodeGuid = output.guid,
                        inputNodeGuid = input.guid,
                        inputPortName = connection.inputPortName,
                    };
                    m_formula.connections.Add(copy);
                    AddConnection(copy);
                }
            }

            ClearSelection();
            foreach (var pasted in guidMap.Values)
            {
                if (m_nodesByGuid.TryGetValue(pasted.guid, out var node))
                    AddToSelection(node);
            }

            m_window.Save();
        }

        public void DuplicateSelection()
        {
            CopySelectionToClipboard();
            PasteClipboard();
        }

        public void DeleteSelectedFormulaElements()
        {
            if (m_formula == null)
                return;

            var selectedNodes = selection
                .OfType<Node>()
                .Where(node => node.userData is EntityStatsFormulaNodeData data && data.type != EntityStatsFormulaNodeType.Result)
                .ToList();
            var selectedEdges = selection.OfType<Edge>().ToList();
            var selectedGroups = selection.OfType<Group>().ToList();

            if (selectedNodes.Count == 0 && selectedEdges.Count == 0 && selectedGroups.Count == 0)
                return;

            Undo.RecordObject(m_window.GraphAsset, "Delete Formula Selection");
            m_isLoading = true;

            foreach (var group in selectedGroups)
            {
                if (group.userData is EntityStatsFormulaGroupData groupData)
                {
                    foreach (var node in m_formula.nodes.Where(node => node.groupId == groupData.guid))
                        node.groupId = null;

                    m_formula.groups.Remove(groupData);
                    m_groupsByGuid.Remove(groupData.guid);
                }

                RemoveElement(group);
            }

            foreach (var edge in selectedEdges)
            {
                if (
                    edge.output?.node?.userData is EntityStatsFormulaNodeData output
                    && edge.input?.node?.userData is EntityStatsFormulaNodeData input
                )
                    m_formula.connections.RemoveAll(connection =>
                        connection.outputNodeGuid == output.guid
                        && connection.inputNodeGuid == input.guid
                        && connection.inputPortName == edge.input.portName
                    );

                RemoveElement(edge);
            }

            foreach (var node in selectedNodes)
            {
                if (!(node.userData is EntityStatsFormulaNodeData data))
                    continue;

                RemoveConnectedEdges(node, data);
                m_formula.nodes.Remove(data);
                m_formula.connections.RemoveAll(connection => connection.outputNodeGuid == data.guid || connection.inputNodeGuid == data.guid);
                m_nodesByGuid.Remove(data.guid);
                RemoveElement(node);
            }

            m_isLoading = false;
            m_window.Save();
        }

        void RemoveConnectedEdges(Node node, EntityStatsFormulaNodeData data)
        {
            foreach (var edge in edges.ToList())
            {
                if (edge.input?.node != node && edge.output?.node != node)
                    continue;

                if (
                    edge.output?.node?.userData is EntityStatsFormulaNodeData output
                    && edge.input?.node?.userData is EntityStatsFormulaNodeData input
                )
                {
                    m_formula.connections.RemoveAll(connection =>
                        connection.outputNodeGuid == output.guid
                        && connection.inputNodeGuid == input.guid
                        && connection.inputPortName == edge.input.portName
                    );
                }

                RemoveElement(edge);
            }

            m_formula.connections.RemoveAll(connection => connection.outputNodeGuid == data.guid || connection.inputNodeGuid == data.guid);
        }

        static EntityStatsFormulaNodeData CloneNodeForClipboard(EntityStatsFormulaNodeData source) =>
            CloneNode(source, source.guid, Vector2.zero);

        static EntityStatsFormulaNodeData CloneNodeForPaste(EntityStatsFormulaNodeData source, Vector2 offset) =>
            CloneNode(source, Guid.NewGuid().ToString(), offset);

        static EntityStatsFormulaNodeData CloneNode(EntityStatsFormulaNodeData source, string guid, Vector2 offset) =>
            new EntityStatsFormulaNodeData
            {
                guid = guid,
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
                    source.position.x + offset.x,
                    source.position.y + offset.y,
                    source.position.width,
                    source.position.height
                ),
            };

        static EntityStatsFormulaConnectionData CloneConnection(EntityStatsFormulaConnectionData source) =>
            new EntityStatsFormulaConnectionData
            {
                outputNodeGuid = source.outputNodeGuid,
                inputNodeGuid = source.inputNodeGuid,
                inputPortName = source.inputPortName,
            };

        public void CreateGroupFromSelection()
        {
            if (m_formula == null)
                return;

            var selectedNodes = selection
                .OfType<Node>()
                .Where(node => node.userData is EntityStatsFormulaNodeData data && data.type != EntityStatsFormulaNodeType.Result)
                .ToList();

            if (selectedNodes.Count == 0)
                return;

            Undo.RecordObject(m_window.GraphAsset, "Create Formula Group");
            var bounds = selectedNodes[0].GetPosition();
            foreach (var node in selectedNodes.Skip(1))
                bounds = Encompass(bounds, node.GetPosition());

            bounds.xMin -= 32f;
            bounds.yMin -= 56f;
            bounds.xMax += 32f;
            bounds.yMax += 32f;

            m_formula.groups ??= new List<EntityStatsFormulaGroupData>();
            var groupData = new EntityStatsFormulaGroupData
            {
                title = "Group",
                position = bounds,
            };
            m_formula.groups.Add(groupData);
            AddGroup(groupData);

            foreach (var selectedNode in selectedNodes)
            {
                if (selectedNode.userData is EntityStatsFormulaNodeData nodeData)
                {
                    nodeData.groupId = groupData.guid;
                    m_groupsByGuid[groupData.guid].AddElement(selectedNode);
                }
            }

            m_window.Save();
        }

        static Rect Encompass(Rect a, Rect b)
        {
            var xMin = Mathf.Min(a.xMin, b.xMin);
            var yMin = Mathf.Min(a.yMin, b.yMin);
            var xMax = Mathf.Max(a.xMax, b.xMax);
            var yMax = Mathf.Max(a.yMax, b.yMax);
            return Rect.MinMaxRect(xMin, yMin, xMax, yMax);
        }


        public void AutoLayout()
        {
            if (m_formula == null)
                return;

            WriteBack(m_formula);

            var layoutNodes = nodes
                .Where(node => node.userData is EntityStatsFormulaNodeData data && data.type != EntityStatsFormulaNodeType.Comment)
                .ToList();

            if (layoutNodes.Count == 0)
                return;

            var nodesByGuid = layoutNodes
                .Where(node => node.userData is EntityStatsFormulaNodeData)
                .ToDictionary(node => ((EntityStatsFormulaNodeData)node.userData).guid, node => node);
            var incoming = m_formula.connections
                .Where(connection => nodesByGuid.ContainsKey(connection.inputNodeGuid) && nodesByGuid.ContainsKey(connection.outputNodeGuid))
                .GroupBy(connection => connection.inputNodeGuid)
                .ToDictionary(group => group.Key, group => group.Select(connection => connection.outputNodeGuid).ToList());
            var depthCache = new Dictionary<string, int>();
            var visiting = new HashSet<string>();

            int GetDepth(string guid)
            {
                if (depthCache.TryGetValue(guid, out var cachedDepth))
                    return cachedDepth;

                if (!visiting.Add(guid))
                    return 0;

                var depth = 0;
                if (incoming.TryGetValue(guid, out var dependencies) && dependencies.Count > 0)
                    depth = dependencies.Max(dependency => GetDepth(dependency) + 1);

                visiting.Remove(guid);
                depthCache[guid] = depth;
                return depth;
            }

            foreach (var guid in nodesByGuid.Keys)
                GetDepth(guid);

            var columns = layoutNodes
                .GroupBy(node => depthCache[((EntityStatsFormulaNodeData)node.userData).guid])
                .OrderBy(group => group.Key)
                .ToList();
            const float startX = 280f;
            const float startY = 300f;
            const float columnWidth = 300f;
            const float rowHeight = 135f;

            foreach (var column in columns)
            {
                var orderedNodes = column
                    .OrderBy(node => GetLayoutSortOrder((EntityStatsFormulaNodeData)node.userData))
                    .ThenBy(node => node.title)
                    .ToList();
                var columnHeight = (orderedNodes.Count - 1) * rowHeight;
                var yOffset = -columnHeight * 0.5f;

                for (var row = 0; row < orderedNodes.Count; row++)
                {
                    var rect = orderedNodes[row].GetPosition();
                    rect.position = new Vector2(
                        startX + column.Key * columnWidth,
                        startY + yOffset + row * rowHeight
                    );
                    orderedNodes[row].SetPosition(rect);
                }
            }

            foreach (var group in graphElements.ToList().OfType<Group>())
                group.SendToBack();

            m_window.SaveWithUndo("Auto Layout Formula Nodes");
            FrameAllNodes();
        }

        static int GetLayoutSortOrder(EntityStatsFormulaNodeData data)
        {
            switch (data.type)
            {
                case EntityStatsFormulaNodeType.Input:
                case EntityStatsFormulaNodeType.BuiltInValue:
                case EntityStatsFormulaNodeType.Constant:
                case EntityStatsFormulaNodeType.PercentConstant:
                    return 0;
                case EntityStatsFormulaNodeType.Operator:
                case EntityStatsFormulaNodeType.FormulaFunction:
                case EntityStatsFormulaNodeType.FormulaReference:
                    return 1;
                case EntityStatsFormulaNodeType.Result:
                    return 2;
                default:
                    return 3;
            }
        }

        public void FrameAllNodes()
        {
            if (nodes.Any())
                FrameAll();
        }

        public void WriteBack(EntityStatsFormulaData formula)
        {
            foreach (var node in nodes.ToList())
            {
                if (node.userData is EntityStatsFormulaNodeData data)
                    data.position = node.GetPosition();
            }

            formula.groups ??= new List<EntityStatsFormulaGroupData>();
            formula.groups.Clear();

            foreach (var group in graphElements.ToList().OfType<Group>())
            {
                if (group.userData is EntityStatsFormulaGroupData groupData)
                {
                    groupData.title = group.title.Replace("▦ ", string.Empty);
                    groupData.position = group.GetPosition();
                    formula.groups.Add(groupData);
                }
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
            var node = new Node { title = GetTitle(data), userData = data, tooltip = GetTooltip(data) };
            node.SetPosition(data.position);
            ApplyCategoryStyle(node, data);

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
                    AddBadge(node, data.formulaTarget.ToString(), "target-badge");
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
                    AddBadge(node, data.function ? data.function.name : "Function", "function-badge");
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
                    AddBadge(node, data.constant.ToString("0.###"), "value-badge");
                    var constantNameField = CreateNameField(data, node, "Constant");
                    node.extensionContainer.Add(constantNameField);
                    var floatField = new FloatField("Value") { value = data.constant, tooltip = "Numeric value emitted by this named constant." };
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
                    AddBadge(node, data.constant.ToString("0.###") + "%", "value-badge");
                    var percentNameField = CreateNameField(data, node, "% Constant");
                    node.extensionContainer.Add(percentNameField);
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
                    AddBadge(node, GetOperatorSymbol(data.operation), "operator-badge");
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
                    node.tooltip = "Resizable note box. Use this to document nearby formula nodes.";
                    node.AddToClassList("comment-note-card");
                    node.style.minWidth = 220f;
                    node.style.minHeight = 140f;
                    var titleField = new TextField("Title") { value = data.title, tooltip = "Comment title shown in the top-left of the box." };
                    titleField.RegisterValueChangedCallback(evt =>
                    {
                        Undo.RecordObject(m_window.GraphAsset, "Change Formula Comment");
                        data.title = evt.newValue;
                        node.title = GetTitle(data);
                        m_window.Save();
                    });
                    var noteField = new TextField("Note") { value = data.note, multiline = true, tooltip = "Free-form formula notes." };
                    noteField.AddToClassList("comment-note-text");
                    noteField.style.flexGrow = 1f;
                    noteField.style.minHeight = 80f;
                    noteField.RegisterValueChangedCallback(evt =>
                    {
                        Undo.RecordObject(m_window.GraphAsset, "Change Formula Comment");
                        data.note = evt.newValue;
                        m_window.Save();
                    });
                    var resizeHandle = new VisualElement { tooltip = "Drag to resize this comment box." };
                    resizeHandle.AddToClassList("comment-resize-handle");
                    resizeHandle.AddManipulator(new FormulaCommentResizeManipulator(node, data, m_window));
                    node.extensionContainer.Add(titleField);
                    node.extensionContainer.Add(noteField);
                    node.extensionContainer.Add(resizeHandle);
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

        static void AddBadge(Node node, string text, string className)
        {
            if (string.IsNullOrEmpty(text))
                return;

            var badge = new Label(text);
            badge.AddToClassList("node-badge");
            badge.AddToClassList(className);
            node.titleContainer.Add(badge);
        }

        TextField CreateNameField(EntityStatsFormulaNodeData data, Node node, string fallbackName)
        {
            var nameField = new TextField("Name")
            {
                value = data.title,
                tooltip = "Optional display name for this constant. Leave empty to use the default node title.",
            };
            nameField.RegisterValueChangedCallback(evt =>
            {
                Undo.RecordObject(m_window.GraphAsset, "Rename Formula Constant");
                data.title = evt.newValue;
                node.title = GetTitle(data);
                m_window.Save();
            });
            return nameField;
        }

        static void ApplyCategoryStyle(Node node, EntityStatsFormulaNodeData data)
        {
            node.AddToClassList("formula-node");
            node.AddToClassList("formula-node-" + data.type.ToString().ToLowerInvariant());
            node.titleContainer.AddToClassList("formula-node-header");

            // Keep the original category colors visible even when GraphView's built-in
            // node header styles win over USS class selectors in older Unity versions.
            node.titleContainer.style.backgroundColor = GetCategoryColor(data);
            node.tooltip = GetTooltip(data);
        }

        static Color GetCategoryColor(EntityStatsFormulaNodeData data)
        {
            switch (data.type)
            {
                case EntityStatsFormulaNodeType.Input:
                case EntityStatsFormulaNodeType.BuiltInValue:
                case EntityStatsFormulaNodeType.FormulaReference:
                case EntityStatsFormulaNodeType.FormulaFunction:
                    return new Color(0.16f, 0.28f, 0.48f, 0.95f);
                case EntityStatsFormulaNodeType.Constant:
                case EntityStatsFormulaNodeType.PercentConstant:
                    return new Color(0.18f, 0.42f, 0.24f, 0.95f);
                case EntityStatsFormulaNodeType.Operator:
                    return new Color(0.42f, 0.28f, 0.12f, 0.95f);
                case EntityStatsFormulaNodeType.Result:
                    return new Color(0.42f, 0.16f, 0.18f, 0.95f);
                case EntityStatsFormulaNodeType.Comment:
                    return new Color(0.38f, 0.34f, 0.14f, 0.95f);
                default:
                    return new Color(0.22f, 0.22f, 0.22f, 0.95f);
            }
        }

        static string GetTooltip(EntityStatsFormulaNodeData data)
        {
            switch (data.type)
            {
                case EntityStatsFormulaNodeType.Input:
                    return $"Reads {data.input} from the preview/runtime formula context.";
                case EntityStatsFormulaNodeType.BuiltInValue:
                    return "Outputs the original built-in stat result before graph overrides.";
                case EntityStatsFormulaNodeType.FormulaReference:
                    return "Reads another enabled formula target from this graph with cycle protection.";
                case EntityStatsFormulaNodeType.FormulaFunction:
                    return "Evaluates a reusable formula function asset in the current context.";
                case EntityStatsFormulaNodeType.Constant:
                    return "Named numeric constant.";
                case EntityStatsFormulaNodeType.PercentConstant:
                    return "Named percent constant. Enter 25 to output 0.25.";
                case EntityStatsFormulaNodeType.Operator:
                    return GetOperatorTooltip(data.operation);
                case EntityStatsFormulaNodeType.Reroute:
                    return "Passes a value through to keep wires readable.";
                case EntityStatsFormulaNodeType.Comment:
                    return "Resizable comment box for formula notes.";
                case EntityStatsFormulaNodeType.Result:
                    return "Final value returned by this formula.";
                default:
                    return data.type.ToString();
            }
        }

        static string GetOperatorTooltip(EntityStatsFormulaOperator operation)
        {
            switch (operation)
            {
                case EntityStatsFormulaOperator.Add:
                    return "Adds A and B.";
                case EntityStatsFormulaOperator.Subtract:
                    return "Subtracts B from A.";
                case EntityStatsFormulaOperator.Multiply:
                    return "Multiplies A by B.";
                case EntityStatsFormulaOperator.Divide:
                    return "Divides A by B. Returns zero if B is zero.";
                case EntityStatsFormulaOperator.Min:
                    return "Returns the smaller input.";
                case EntityStatsFormulaOperator.Max:
                    return "Returns the larger input.";
                case EntityStatsFormulaOperator.Clamp:
                    return "Clamps Value between Min and Max.";
                case EntityStatsFormulaOperator.Abs:
                    return "Returns the absolute value.";
                case EntityStatsFormulaOperator.Negate:
                    return "Multiplies Value by -1.";
                case EntityStatsFormulaOperator.Floor:
                    return "Rounds Value down.";
                case EntityStatsFormulaOperator.Ceil:
                    return "Rounds Value up.";
                case EntityStatsFormulaOperator.Round:
                    return "Rounds Value to the nearest integer.";
                case EntityStatsFormulaOperator.Power:
                    return "Raises A to the power of B.";
                case EntityStatsFormulaOperator.Sqrt:
                    return "Returns the square root of Value.";
                case EntityStatsFormulaOperator.Log:
                    return "Returns the natural logarithm of Value.";
                case EntityStatsFormulaOperator.PercentOf:
                    return "Returns Percent percent of Value.";
                case EntityStatsFormulaOperator.Lerp:
                    return "Interpolates from A to B using T.";
                case EntityStatsFormulaOperator.IfGreater:
                    return "Returns True when A is greater than B; otherwise False.";
                case EntityStatsFormulaOperator.IfLess:
                    return "Returns True when A is less than B; otherwise False.";
                case EntityStatsFormulaOperator.Select:
                    return "Returns True when Value is positive; otherwise False.";
                case EntityStatsFormulaOperator.NormalizePercent:
                    return "Converts 25 into 0.25 for normalized chance values.";
                default:
                    return operation.ToString();
            }
        }

        void AddInput(Node node, string name)
        {
            var port = node.InstantiatePort(Orientation.Horizontal, Direction.Input, Port.Capacity.Single, typeof(float));
            port.portName = name;
            ApplyPortClass(port, name);
            port.AddManipulator(new EdgeConnector<Edge>(m_edgeConnectorListener));
            node.inputContainer.Add(port);
        }

        void AddOutput(Node node, string name)
        {
            var port = node.InstantiatePort(Orientation.Horizontal, Direction.Output, Port.Capacity.Multi, typeof(float));
            port.portName = name;
            ApplyPortClass(port, name);
            port.AddManipulator(new EdgeConnector<Edge>(m_edgeConnectorListener));
            node.outputContainer.Add(port);
        }

        static void ApplyPortClass(Port port, string name)
        {
            port.AddToClassList("formula-port");
            var normalized = (name ?? string.Empty).ToLowerInvariant();
            var color = new Color(0.74f, 0.74f, 0.74f, 1f);
            if (normalized.Contains("percent"))
            {
                port.AddToClassList("port-percent");
                color = new Color(0.58f, 0.34f, 0.88f, 1f);
            }
            else if (normalized == "true")
            {
                port.AddToClassList("port-true");
                color = new Color(0.25f, 0.78f, 0.38f, 1f);
            }
            else if (normalized == "false")
            {
                port.AddToClassList("port-false");
                color = new Color(0.86f, 0.26f, 0.24f, 1f);
            }
            else if (normalized == "min")
            {
                port.AddToClassList("port-min");
                color = new Color(0.30f, 0.55f, 0.92f, 1f);
            }
            else if (normalized == "max")
            {
                port.AddToClassList("port-max");
                color = new Color(0.92f, 0.55f, 0.20f, 1f);
            }
            else
            {
                port.AddToClassList("port-value");
            }

            port.portColor = color;
            var label = port.Q<Label>();
            if (label != null)
            {
                label.style.color = Color.white;
                label.style.unityFontStyleAndWeight = FontStyle.Bold;
            }
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
            ApplyEdgeStyle(edge, outputNode.userData as EntityStatsFormulaNodeData);
            AddElement(edge);
        }

        static void ApplyEdgeStyle(Edge edge, EntityStatsFormulaNodeData outputData)
        {
            edge.AddToClassList("formula-edge");
            if (outputData == null)
                return;

            if (outputData.type == EntityStatsFormulaNodeType.FormulaReference)
                edge.AddToClassList("edge-formula-reference");
            else if (outputData.type == EntityStatsFormulaNodeType.BuiltInValue)
                edge.AddToClassList("edge-built-in-value");
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
                        m_formula?.connections.RemoveAll(connection => connection.outputNodeGuid == data.guid || connection.inputNodeGuid == data.guid);
                    }
                    else if (element is Edge)
                    {
                        changedSerializedData = true;
                    }
                }
            }

            if (change.edgesToCreate != null && change.edgesToCreate.Count > 0)
            {
                foreach (var edge in change.edgesToCreate)
                    ApplyEdgeStyle(edge, edge.output?.node?.userData as EntityStatsFormulaNodeData);

                changedSerializedData = true;
            }

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

        static string GetOperatorDisplayName(EntityStatsFormulaOperator operation) =>
            operation switch
            {
                EntityStatsFormulaOperator.Add => "Add",
                EntityStatsFormulaOperator.Subtract => "Subtract",
                EntityStatsFormulaOperator.Multiply => "Multiply",
                EntityStatsFormulaOperator.Divide => "Divide",
                EntityStatsFormulaOperator.PercentOf => "Percent Of",
                EntityStatsFormulaOperator.NormalizePercent => "To Percent 0-1",
                _ => operation.ToString(),
            };

        static string GetOperatorSymbol(EntityStatsFormulaOperator operation) =>
            operation switch
            {
                EntityStatsFormulaOperator.Add => "+",
                EntityStatsFormulaOperator.Subtract => "−",
                EntityStatsFormulaOperator.Multiply => "×",
                EntityStatsFormulaOperator.Divide => "÷",
                EntityStatsFormulaOperator.Clamp => "Clamp",
                EntityStatsFormulaOperator.PercentOf => "%",
                EntityStatsFormulaOperator.NormalizePercent => "%",
                _ => string.Empty,
            };

        public static string GetTitle(EntityStatsFormulaNodeData data)
        {
            switch (data.type)
            {
                case EntityStatsFormulaNodeType.Input:
                    return "Get Stat";
                case EntityStatsFormulaNodeType.Constant:
                    return string.IsNullOrEmpty(data.title) ? "Constant" : data.title;
                case EntityStatsFormulaNodeType.Operator:
                    return GetOperatorDisplayName(data.operation);
                case EntityStatsFormulaNodeType.BuiltInValue:
                    return "Built In Value";
                case EntityStatsFormulaNodeType.PercentConstant:
                    return string.IsNullOrEmpty(data.title) ? "% Constant" : data.title;
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
