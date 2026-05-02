using UnityEditor;
using UnityEngine;

namespace PLAYERTWO.ARPGProject
{
    public partial class EnemyModelReplacerWindow
    {
        private static GUIStyle s_headerTitleStyle;
        private static GUIStyle s_headerSubtitleStyle;
        private static GUIStyle s_cardStyle;
        private static GUIStyle s_cardTitleStyle;
        private static GUIStyle s_dropZoneStyle;
        private static GUIStyle s_mutedMiniStyle;
        private static GUIStyle s_primaryButtonStyle;
        private static GUIStyle s_actionButtonStyle;
        private static GUIStyle s_commandRailStyle;
        private static GUIStyle s_commandRailTitleStyle;
        private static GUIStyle s_contentPaneStyle;
        private static GUIStyle s_tabButtonStyle;
        private static GUIStyle s_tabButtonSelectedStyle;
        private const string k_headerIconAssetPath = "Assets/ANSTUDIO/EnemyModelReplacer/Images/ModelReplacementHeader.png";

        private static GUIContent[] s_tabContents;
        private static Texture s_headerIconTexture;
        private static bool s_headerIconLoadAttempted;
        private static Texture2D s_cardTexture;
        private static Texture2D s_dropZoneTexture;
        private static Texture2D s_dropZoneHoverTexture;
        private static Texture2D s_footerTexture;
        private static Texture2D s_commandRailTexture;
        private static Texture2D s_contentPaneTexture;

        private void EnsureModernStyles()
        {
            if (s_cardStyle != null)
                return;

            s_cardTexture = MakeColorTexture(new Color(0.18f, 0.18f, 0.18f, 0.48f));
            s_dropZoneTexture = MakeColorTexture(new Color(0.12f, 0.16f, 0.22f, 0.82f));
            s_dropZoneHoverTexture = MakeColorTexture(new Color(0.16f, 0.30f, 0.45f, 0.95f));
            s_footerTexture = MakeColorTexture(new Color(0.13f, 0.13f, 0.13f, 0.98f));
            s_commandRailTexture = MakeColorTexture(new Color(0.10f, 0.115f, 0.14f, 0.98f));
            s_contentPaneTexture = MakeColorTexture(new Color(0.155f, 0.155f, 0.155f, 0.46f));

            s_headerTitleStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 18,
                alignment = TextAnchor.MiddleLeft,
                richText = true
            };

            s_headerSubtitleStyle = new GUIStyle(EditorStyles.miniLabel)
            {
                fontSize = 11,
                wordWrap = true,
                richText = true
            };

            s_cardStyle = new GUIStyle(EditorStyles.helpBox)
            {
                padding = new RectOffset(12, 12, 10, 12),
                margin = new RectOffset(10, 10, 6, 8),
                normal = { background = s_cardTexture }
            };

            s_cardTitleStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 13,
                richText = true
            };

            s_dropZoneStyle = new GUIStyle(EditorStyles.centeredGreyMiniLabel)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 12,
                fontStyle = FontStyle.Bold,
                wordWrap = true,
                richText = true,
                padding = new RectOffset(8, 8, 8, 8)
            };

            s_mutedMiniStyle = new GUIStyle(EditorStyles.miniLabel)
            {
                wordWrap = true,
                richText = true
            };

            s_primaryButtonStyle = new GUIStyle(UnityEngine.GUI.skin.button)
            {
                fontSize = 13,
                fontStyle = FontStyle.Bold,
                fixedHeight = 34f
            };

            s_actionButtonStyle = new GUIStyle(UnityEngine.GUI.skin.button)
            {
                alignment = TextAnchor.MiddleLeft,
                fixedHeight = 32f,
                imagePosition = ImagePosition.ImageLeft,
                padding = new RectOffset(10, 8, 4, 4),
                margin = new RectOffset(0, 0, 3, 3),
                clipping = TextClipping.Clip
            };

            s_commandRailStyle = new GUIStyle(EditorStyles.helpBox)
            {
                padding = new RectOffset(8, 8, 8, 8),
                margin = new RectOffset(0, 8, 0, 0),
                normal = { background = s_commandRailTexture }
            };

            s_commandRailTitleStyle = new GUIStyle(EditorStyles.miniBoldLabel)
            {
                alignment = TextAnchor.MiddleLeft,
                richText = true
            };

            s_contentPaneStyle = new GUIStyle(EditorStyles.helpBox)
            {
                padding = new RectOffset(10, 10, 8, 10),
                margin = new RectOffset(0, 0, 0, 0),
                normal = { background = s_contentPaneTexture }
            };

            s_tabButtonStyle = new GUIStyle(EditorStyles.toolbarButton)
            {
                alignment = TextAnchor.MiddleCenter,
                fixedHeight = 30f,
                imagePosition = ImagePosition.ImageLeft,
                richText = false,
                clipping = TextClipping.Clip
            };

            s_tabButtonSelectedStyle = new GUIStyle(s_tabButtonStyle)
            {
                fontStyle = FontStyle.Bold
            };
        }

        private void DrawModernHeader()
        {
            const float headerImageWidth = 242f;
            const float headerImageHeight = 98f;
            const float headerPadding = 12f;

            var rect = GUILayoutUtility.GetRect(1f, headerImageHeight + headerPadding * 2f, GUILayout.ExpandWidth(true));
            EditorGUI.DrawRect(rect, new Color(0.08f, 0.09f, 0.11f, 1f));

            var icon = GetHeaderIcon();
            var iconRect = new Rect(rect.x + headerPadding, rect.y + headerPadding, headerImageWidth, headerImageHeight);
            if (icon != null)
                UnityEngine.GUI.DrawTexture(iconRect, icon, ScaleMode.ScaleToFit, true);

            var textX = iconRect.xMax + 14f;
            var textWidth = rect.xMax - textX - headerPadding;
            if (textWidth < 120f)
                return;

            var titleRect = new Rect(textX, rect.y + 28f, textWidth, 24f);
            UnityEngine.GUI.Label(titleRect, "Enemy Model Replacer", s_headerTitleStyle);

            var subtitleRect = new Rect(textX, rect.y + 56f, textWidth, 36f);
            UnityEngine.GUI.Label(subtitleRect, "Drag a prefab, preview it, configure replacement, then rebind the enemy safely.", s_headerSubtitleStyle);
        }

        private void DrawTabBar()
        {
            EnsureTabContents(false);

            var availableWidth = Mathf.Max(1f, position.width - 12f);
            var tabCount = s_tabContents.Length;
            var fullOneRowWidth = tabCount * 118f;
            var compactOneRowWidth = tabCount * 86f;

            if (availableWidth >= fullOneRowWidth)
            {
                EnsureTabContents(false);
                DrawTabButtonRow(0, tabCount, availableWidth);
                return;
            }

            if (availableWidth >= compactOneRowWidth)
            {
                EnsureTabContents(true);
                DrawTabButtonRow(0, tabCount, availableWidth);
                return;
            }

            EnsureTabContents(true);
            var tabsPerRow = Mathf.Clamp(Mathf.FloorToInt(availableWidth / 92f), 2, tabCount);
            var index = 0;
            while (index < tabCount)
            {
                var count = Mathf.Min(tabsPerRow, tabCount - index);
                DrawTabButtonRow(index, count, availableWidth);
                index += count;
            }
        }

        private void EnsureTabContents(bool compactLabels)
        {
            if (s_tabContents != null && s_tabContents.Length == 5)
            {
                var expectedSetupLabel = "Setup";
                var expectedAnimationsLabel = compactLabels ? "Anim" : "Animations";
                if (s_tabContents[0].text == expectedSetupLabel && s_tabContents[2].text == expectedAnimationsLabel)
                    return;
            }

            s_tabContents = new[]
            {
                TabContent("GameObject Icon", "Setup"),
                TabContent("ViewToolOrbit", compactLabels ? "View" : "Preview"),
                TabContent("AnimationClip Icon", compactLabels ? "Anim" : "Animations"),
                TabContent("TreeEditor.Duplicate", compactLabels ? "Copies" : "Duplicates"),
                TabContent("ScriptableObject Icon", "Profiles")
            };
        }

        private void DrawTabButtonRow(int startIndex, int count, float availableWidth)
        {
            var horizontalMargin = 6f;
            var buttonWidth = Mathf.Max(64f, (availableWidth - horizontalMargin * 2f) / Mathf.Max(1, count));

            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                GUILayout.Space(horizontalMargin);

                for (var i = 0; i < count; i++)
                {
                    var tabIndex = startIndex + i;
                    var isSelected = (int)selectedTab == tabIndex;
                    var style = isSelected ? s_tabButtonSelectedStyle : s_tabButtonStyle;

                    if (GUILayout.Toggle(isSelected, s_tabContents[tabIndex], style, GUILayout.Width(buttonWidth), GUILayout.Height(30f)))
                        selectedTab = (EnemyModelReplacerTab)tabIndex;
                }

                GUILayout.Space(horizontalMargin);
            }
        }

        private static GUIContent TabContent(string iconName, string label)
        {
            return new GUIContent(label, FindEditorIcon(iconName), label);
        }

        private GameObject DrawSceneGameObjectField(string label, GameObject current)
        {
            var result = current;

            using (new EditorGUILayout.HorizontalScope())
            {
                var fieldRect = EditorGUILayout.GetControlRect(
                    true,
                    EditorGUIUtility.singleLineHeight,
                    GUILayout.ExpandWidth(true));

                EditorGUI.BeginChangeCheck();
                var picked = (GameObject)EditorGUI.ObjectField(fieldRect, label, current, typeof(GameObject), true);
                if (EditorGUI.EndChangeCheck())
                    result = ValidateSceneGameObjectSelection(picked, current);

                HandleSceneGameObjectFieldDrop(fieldRect, current, ref result);

                var selectedSceneObject = Selection.activeGameObject;
                using (new EditorGUI.DisabledScope(!IsSceneGameObject(selectedSceneObject)))
                {
                    if (GUILayout.Button("Use Selected", GUILayout.Width(92f)))
                        result = selectedSceneObject;
                }
            }

            return result;
        }

        private static GameObject ValidateSceneGameObjectSelection(GameObject picked, GameObject previous)
        {
            if (picked == null)
                return null;

            if (IsSceneGameObject(picked))
                return picked;

            Debug.LogWarning("[EnemyModelReplacer] Enemy Root must be a scene object from the Hierarchy. Prefab assets from the Project window are not accepted for this field.", picked);
            return previous;
        }

        private static bool IsSceneGameObject(GameObject gameObject)
        {
            if (gameObject == null)
                return false;

            if (EditorUtility.IsPersistent(gameObject))
                return false;

            return gameObject.scene.IsValid() && gameObject.scene.isLoaded;
        }

        private void HandleSceneGameObjectFieldDrop(Rect fieldRect, GameObject previous, ref GameObject result)
        {
            var currentEvent = Event.current;
            if (!fieldRect.Contains(currentEvent.mousePosition))
                return;

            if (currentEvent.type != EventType.DragUpdated && currentEvent.type != EventType.DragPerform)
                return;

            var draggedSceneObject = GetDraggedSceneGameObject();
            DragAndDrop.visualMode = draggedSceneObject != null ? DragAndDropVisualMode.Link : DragAndDropVisualMode.Rejected;

            if (currentEvent.type == EventType.DragPerform && draggedSceneObject != null)
            {
                DragAndDrop.AcceptDrag();
                result = ValidateSceneGameObjectSelection(draggedSceneObject, previous);
                UnityEngine.GUI.changed = true;
            }

            currentEvent.Use();
        }

        private static GameObject GetDraggedSceneGameObject()
        {
            foreach (var draggedObject in DragAndDrop.objectReferences)
            {
                var gameObject = draggedObject as GameObject;
                var component = draggedObject as Component;

                if (gameObject == null && component != null)
                    gameObject = component.gameObject;

                if (IsSceneGameObject(gameObject))
                    return gameObject;
            }

            return null;
        }

        private void DrawSelectedTab()
        {
            switch (selectedTab)
            {
                case EnemyModelReplacerTab.Setup:
                    DrawSetupTab();
                    break;
                case EnemyModelReplacerTab.Preview:
                    DrawPreviewTab();
                    break;
                case EnemyModelReplacerTab.Animations:
                    DrawAnimationsTab();
                    break;
                case EnemyModelReplacerTab.Duplicates:
                    DrawDuplicatesTab();
                    break;
                case EnemyModelReplacerTab.Profiles:
                    DrawProfilesTab();
                    break;
            }
        }

        private void DrawSetupTab()
        {
            BeginModernCard("Enemy + New Prefab", "Assign the scene enemy root and drag the replacement prefab into the drop zone.");
            DrawEnemyRootDropZone();
            enemyRoot = DrawSceneGameObjectField("Enemy Root", enemyRoot);
            DrawNewPrefabDropZone();
            EditorGUILayout.Space(5f);
            modelContainerName = EditorGUILayout.TextField("Model Container Name", modelContainerName);
            autoApplyToChildren = EditorGUILayout.Toggle("Auto Rebind References", autoApplyToChildren);
            removePreviousModel = EditorGUILayout.Toggle("Remove Previous Model", removePreviousModel);
            EndModernCard();

            BeginModernCard("Placement", "Optional local Y correction for prefabs that import with a different origin.");
            forceNewModelY = EditorGUILayout.Toggle("Force New Model Local Y", forceNewModelY);
            using (new EditorGUI.DisabledScope(!forceNewModelY))
            {
                forcedNewModelLocalY = EditorGUILayout.FloatField("Forced Local Y", forcedNewModelLocalY);
            }
            EndModernCard();
        }

        private void DrawPreviewTab()
        {
            BeginModernCard("Prefab Preview", "Assign the prefab in Setup, then hold right mouse button inside the preview and move left/right to rotate it.");
            DrawPrefabPreviewPanel(468f);
            EndModernCard();
        }

        private void DrawAnimationsTab()
        {
            BeginModernCard("Animator Override Controller", "Create a fresh override controller, or reuse and assign an existing one with the same name.");

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                var detectedSource = PrepareAnimatorOverrideSourceController();

                BeginTwoColumnPanel("Action Buttons");
                DrawAnimatorOverrideActionButtons(detectedSource);
                SwitchToTwoColumnContent("Animator Settings");

                createAnimatorOverrideController = EditorGUILayout.Toggle("Create / Reuse Override Controller", createAnimatorOverrideController);

                using (new EditorGUI.DisabledScope(!createAnimatorOverrideController))
                {
                    animatorOverrideControllerName = EditorGUILayout.TextField("Override Controller Name", animatorOverrideControllerName);
                    animatorOverrideControllerFolder = EditorGUILayout.TextField("Create In Folder", animatorOverrideControllerFolder);
                    DrawAnimatorOverrideClipFields();
                    EditorGUILayout.HelpBox("The Animator Override Controller is assigned to the Entity Animator component's Default Animations field. If one with this name already exists, it is reused and updated with the replacement clips listed above.", MessageType.None);
                }

                EndTwoColumnPanel();
            }

            EndModernCard();
        }


        private void DrawDuplicatesTab()
        {
            BeginModernCard("Standalone Enemy Duplicates", "Create full converted enemy copies around the Enemy Root using spacing and collision checks.");
            createStandaloneDuplicates = EditorGUILayout.Toggle("Create Duplicates", createStandaloneDuplicates);

            using (new EditorGUI.DisabledScope(!createStandaloneDuplicates))
            {
                duplicateCount = Mathf.Max(0, EditorGUILayout.IntField("Duplicate Count", duplicateCount));
                duplicateNamePrefix = EditorGUILayout.TextField("Duplicate Enemy Name Prefix", duplicateNamePrefix);
                duplicateMinRadius = Mathf.Max(0f, EditorGUILayout.FloatField("Min Range From Enemy Root", duplicateMinRadius));
                duplicateMaxRadius = Mathf.Max(duplicateMinRadius, EditorGUILayout.FloatField("Max Range From Enemy Root", duplicateMaxRadius));
                duplicateMinSpacing = Mathf.Max(0f, EditorGUILayout.FloatField("Space Between Duplicates", duplicateMinSpacing));
                duplicatePlacementAttempts = Mathf.Max(1, EditorGUILayout.IntField("Placement Attempts", duplicatePlacementAttempts));

                EditorGUILayout.Space(8f);
                EditorGUILayout.LabelField("Spawn Collision", s_cardTitleStyle);
                avoidSpawnCollisionLayers = EditorGUILayout.Toggle("Avoid Layers", avoidSpawnCollisionLayers);

                using (new EditorGUI.DisabledScope(!avoidSpawnCollisionLayers))
                {
                    blockedSpawnLayers = LayerMaskField("Blocked Spawn Layers", blockedSpawnLayers);
                    spawnCollisionCheckRadius = Mathf.Max(0.01f, EditorGUILayout.FloatField("Collision Check Radius", spawnCollisionCheckRadius));
                    spawnCollisionCheckYOffset = EditorGUILayout.FloatField("Collision Check Y Offset", spawnCollisionCheckYOffset);
                    spawnCollisionTriggerInteraction = (QueryTriggerInteraction)EditorGUILayout.EnumPopup("Trigger Colliders", spawnCollisionTriggerInteraction);
                }
            }
            EndModernCard();

            BeginModernCard("Scene Gizmo Preview", "The range gizmo is visible only when Enemy Root is assigned and Create Duplicates is enabled.");
            duplicateRangeGizmoYOffset = EditorGUILayout.FloatField("Gizmo Y Offset", duplicateRangeGizmoYOffset);
            using (new EditorGUI.DisabledScope(enemyRoot == null))
            {
                if (GUILayout.Button("Reset Gizmo Y Offset"))
                    duplicateRangeGizmoYOffset = 0f;
            }
            EndModernCard();
        }

        private void DrawProfilesTab()
        {
            BeginModernCard("Profiles", "Save and load all tool settings without leaving the window.");
            DrawProfileControls();
            EndModernCard();
        }

        private void DrawActionFooter()
        {
            var rect = GUILayoutUtility.GetRect(1f, 52f, GUILayout.ExpandWidth(true));
            UnityEngine.GUI.DrawTexture(rect, s_footerTexture, ScaleMode.StretchToFill);

            var buttonRect = new Rect(rect.x + 12f, rect.y + 9f, rect.width - 24f, 34f);
            using (new EditorGUI.DisabledScope(enemyRoot == null || newModelPrefab == null))
            {
                if (UnityEngine.GUI.Button(buttonRect, "Replace Model & Rebind", s_primaryButtonStyle))
                    ReplaceModel();
            }
        }

        private void DrawEnemyRootDropZone()
        {
            var rect = GUILayoutUtility.GetRect(1f, 92f, GUILayout.ExpandWidth(true));
            var draggedSceneObject = GetDraggedSceneGameObject();
            var hovering = rect.Contains(Event.current.mousePosition);
            var canDrop = hovering && draggedSceneObject != null;
            UnityEngine.GUI.DrawTexture(rect, canDrop ? s_dropZoneHoverTexture : s_dropZoneTexture, ScaleMode.StretchToFill);

            var icon = GetSceneObjectDropZoneIcon();
            if (icon != null)
                UnityEngine.GUI.DrawTexture(new Rect(rect.x + 14f, rect.y + 24f, 44f, 44f), icon, ScaleMode.ScaleToFit, true);

            var currentName = enemyRoot != null ? enemyRoot.name : "Drop Enemy Root Scene Object Here";
            var textRect = new Rect(rect.x + 64f, rect.y + 10f, rect.width - 78f, rect.height - 20f);
            UnityEngine.GUI.Label(textRect, $"<b>{currentName}</b>\nDrag the enemy GameObject from the Hierarchy here. Project prefab assets are rejected.", s_dropZoneStyle);

            HandleEnemyRootDrop(rect);
        }


        private static Texture GetSceneObjectDropZoneIcon()
        {
            var icon = FindEditorIcon(
                "GameObject Icon",
                "d_GameObject Icon",
                "UnityEditor.GameObject Icon",
                "d_UnityEditor.GameObject Icon",
                "Transform Icon",
                "d_Transform Icon");

            return icon != null
                ? icon
                : EditorGUIUtility.ObjectContent(null, typeof(GameObject)).image;
        }

        private void HandleEnemyRootDrop(Rect rect)
        {
            var currentEvent = Event.current;
            if (!rect.Contains(currentEvent.mousePosition))
                return;

            if (currentEvent.type != EventType.DragUpdated && currentEvent.type != EventType.DragPerform)
                return;

            var draggedSceneObject = GetDraggedSceneGameObject();
            DragAndDrop.visualMode = draggedSceneObject != null ? DragAndDropVisualMode.Link : DragAndDropVisualMode.Rejected;

            if (currentEvent.type == EventType.DragPerform && draggedSceneObject != null)
            {
                DragAndDrop.AcceptDrag();
                AssignEnemyRoot(draggedSceneObject);
            }

            currentEvent.Use();
        }

        private void AssignEnemyRoot(GameObject sceneObject)
        {
            var validated = ValidateSceneGameObjectSelection(sceneObject, enemyRoot);
            if (validated == enemyRoot)
                return;

            enemyRoot = validated;
            SceneView.RepaintAll();
            Repaint();
        }

        private void DrawNewPrefabDropZone()
        {
            var rect = GUILayoutUtility.GetRect(1f, 92f, GUILayout.ExpandWidth(true));
            var hovering = rect.Contains(Event.current.mousePosition);
            UnityEngine.GUI.DrawTexture(rect, hovering ? s_dropZoneHoverTexture : s_dropZoneTexture, ScaleMode.StretchToFill);

            var icon = FindEditorIcon("Prefab Icon");
            if (icon != null)
                UnityEngine.GUI.DrawTexture(new Rect(rect.x + 14f, rect.y + 24f, 44f, 44f), icon, ScaleMode.ScaleToFit, true);

            var currentName = newModelPrefab != null ? newModelPrefab.name : "Drop New Model Prefab Here";
            var textRect = new Rect(rect.x + 64f, rect.y + 10f, rect.width - 78f, rect.height - 20f);
            UnityEngine.GUI.Label(textRect, $"<b>{currentName}</b>\nDrag a prefab asset here. A preview unlocks automatically after assignment.", s_dropZoneStyle);

            HandlePrefabDrop(rect);
        }

        private void HandlePrefabDrop(Rect rect)
        {
            var currentEvent = Event.current;
            if (!rect.Contains(currentEvent.mousePosition))
                return;

            if (currentEvent.type != EventType.DragUpdated && currentEvent.type != EventType.DragPerform)
                return;

            var prefab = GetDraggedPrefab();
            DragAndDrop.visualMode = prefab != null ? DragAndDropVisualMode.Copy : DragAndDropVisualMode.Rejected;

            if (currentEvent.type == EventType.DragPerform && prefab != null)
            {
                DragAndDrop.AcceptDrag();
                AssignNewModelPrefab(prefab);
            }

            currentEvent.Use();
        }

        private GameObject GetDraggedPrefab()
        {
            foreach (var draggedObject in DragAndDrop.objectReferences)
            {
                var go = draggedObject as GameObject;
                if (go == null)
                    continue;

                if (!AssetDatabase.Contains(go))
                    continue;

                var prefabType = PrefabUtility.GetPrefabAssetType(go);
                if (prefabType != PrefabAssetType.NotAPrefab)
                    return go;
            }

            return null;
        }

        private void AssignNewModelPrefab(GameObject prefab)
        {
            if (prefab == null)
                return;

            if (newModelPrefab == prefab)
                return;

            newModelPrefab = prefab;
            previewYaw = 0f;
            InvalidatePrefabPreview();
            Repaint();
        }

        private void BeginTwoColumnPanel(string commandTitle, float commandWidth = 220f)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.BeginVertical(s_commandRailStyle, GUILayout.Width(commandWidth));
        }

        private void SwitchToTwoColumnContent(string contentTitle)
        {
            EditorGUILayout.EndVertical();
            EditorGUILayout.BeginVertical(s_contentPaneStyle, GUILayout.ExpandWidth(true));
            EditorGUILayout.LabelField(contentTitle, s_cardTitleStyle);
            EditorGUILayout.Space(4f);
        }

        private static void EndTwoColumnPanel()
        {
            EditorGUILayout.EndVertical();
            EditorGUILayout.EndHorizontal();
        }

        private static bool DrawLeftIconButton(string label, string iconName, bool enabled = true, float width = 204f)
        {
            using (new EditorGUI.DisabledScope(!enabled))
            {
                return GUILayout.Button(IconContent(label, iconName), s_actionButtonStyle, GUILayout.Width(width));
            }
        }

        private static GUIContent IconContent(string label, params string[] iconNames)
        {
            return new GUIContent(label, FindEditorIcon(iconNames), label);
        }

        private static Texture GetHeaderIcon()
        {
            if (!s_headerIconLoadAttempted)
            {
                s_headerIconLoadAttempted = true;
                s_headerIconTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(k_headerIconAssetPath);
            }

            return s_headerIconTexture != null
                ? s_headerIconTexture
                : FindEditorIcon("Prefab Icon");
        }

        private static Texture FindEditorIcon(params string[] iconNames)
        {
            if (iconNames == null)
                return null;

            foreach (var iconName in iconNames)
            {
                if (string.IsNullOrWhiteSpace(iconName))
                    continue;

                var icon = EditorGUIUtility.FindTexture(iconName);
                if (icon != null)
                    return icon;

                if (iconName.StartsWith("d_", System.StringComparison.Ordinal))
                {
                    icon = EditorGUIUtility.FindTexture(iconName.Substring(2));
                    if (icon != null)
                        return icon;
                }
            }

            return null;
        }

        private void BeginModernCard(string title, string subtitle = null)
        {
            EditorGUILayout.BeginVertical(s_cardStyle);
            EditorGUILayout.LabelField(title, s_cardTitleStyle);
            if (!string.IsNullOrEmpty(subtitle))
                EditorGUILayout.LabelField(subtitle, s_mutedMiniStyle);
            EditorGUILayout.Space(6f);
        }

        private static void EndModernCard()
        {
            EditorGUILayout.EndVertical();
        }

        private static Texture2D MakeColorTexture(Color color)
        {
            var texture = new Texture2D(1, 1, TextureFormat.RGBA32, false)
            {
                hideFlags = HideFlags.HideAndDontSave
            };
            texture.SetPixel(0, 0, color);
            texture.Apply();
            return texture;
        }
    }
}
