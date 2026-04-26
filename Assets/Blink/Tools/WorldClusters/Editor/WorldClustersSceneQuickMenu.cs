#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Experimental.SceneManagement;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

#if UNITY_2021_2_OR_NEWER
using UnityEditor.Overlays;
using UnityEditor.Toolbars;
using UnityEngine.UIElements;
#endif

namespace BLINK.WorldClusters
{
#if UNITY_2021_2_OR_NEWER

    [Overlay(typeof(SceneView),
        "BLINK.WorldClusters.SceneQuickMenu",
        "World Clusters",
        true,
        defaultDockZone = DockZone.TopToolbar,
        defaultDockPosition = DockPosition.Top)]
    internal sealed class WorldClustersSceneQuickMenuOverlay : Overlay, ICreateToolbar
    {
        private static readonly string[] kToolbarItems =
        {
            WorldClustersSceneQuickMenuToolbar.id
        };

        public IEnumerable<string> toolbarElements => kToolbarItems;

        public override VisualElement CreatePanelContent()
        {
            // When floating as a panel, show the same strip.
            return new WorldClustersSceneQuickMenuToolbar();
        }
    }

    [EditorToolbarElement(id, typeof(SceneView))]
    internal sealed class WorldClustersSceneQuickMenuToolbar : OverlayToolbar
    {
        public const string id = "BLINK.WorldClusters.SceneQuickMenuToolbar";

        private WorldClustersEditorData _data;

        private readonly EditorToolbarButton _btnManager;
        private readonly EditorToolbarButton _btnCluster;
        private readonly EditorToolbarButton _btnTrigger;
        private readonly EditorToolbarButton _btnCollider;

        private double _nextRefreshTime;

        public WorldClustersSceneQuickMenuToolbar()
        {
            _btnManager = MakeButton("Create World Clusters Manager in the active scene", CreateManager);
            _btnCluster = MakeButton("Add Cluster to selected object(s)", () => AddToSelection<Cluster>());
            _btnTrigger = MakeButton("Add Instant Trigger to selected object(s)", () => AddToSelection<ClusterInstantTrigger>(autoWireCluster: true));
            _btnCollider = MakeButton("Add Collider Trigger to selected object(s)", () => AddToSelection<ClusterCollider>(autoWireCluster: true));

            Add(_btnManager);
            Add(_btnCluster);
            Add(_btnTrigger);
            Add(_btnCollider);

            SetupChildrenAsButtonStrip();

            RegisterCallback<AttachToPanelEvent>(_ =>
            {
                ReloadEditorData();
                Refresh();

                Selection.selectionChanged += RefreshSoon;
                EditorApplication.projectChanged += OnProjectChanged;
                EditorApplication.hierarchyChanged += RefreshSoon;
                EditorApplication.update += Tick;
            });

            RegisterCallback<DetachFromPanelEvent>(_ =>
            {
                Selection.selectionChanged -= RefreshSoon;
                EditorApplication.projectChanged -= OnProjectChanged;
                EditorApplication.hierarchyChanged -= RefreshSoon;
                EditorApplication.update -= Tick;
            });
        }

        private static EditorToolbarButton MakeButton(string tooltip, Action onClick)
        {
            var b = new EditorToolbarButton();
            b.text = string.Empty;
            b.tooltip = tooltip;
            b.clicked += onClick;
            return b;
        }

        private void OnProjectChanged()
        {
            ReloadEditorData();
            RefreshSoon();
        }

        private void ReloadEditorData()
        {
            _data = Resources.Load<WorldClustersEditorData>("EditorData/WorldClustersEditorData");
            WorldClustersEditorDataDefaults.EnsureDefaults(_data);
        }

        private void Tick()
        {
            if (EditorApplication.timeSinceStartup < _nextRefreshTime)
                return;

            _nextRefreshTime = EditorApplication.timeSinceStartup + 0.25;
            Refresh();
        }

        private void RefreshSoon()
        {
            _nextRefreshTime = 0;
        }

        private void Refresh()
        {
            _btnManager.icon = GetIcon("manager");
            _btnCluster.icon = GetIcon("cluster");
            _btnTrigger.icon = GetIcon("trigger");
            _btnCollider.icon = GetIcon("collider");

            var hasSelection = GatherSelectionGameObjects().Count > 0;
            _btnCluster.SetEnabled(hasSelection);
            _btnTrigger.SetEnabled(hasSelection);
            _btnCollider.SetEnabled(hasSelection);

            _btnManager.SetEnabled(FindWorldClustersManagerInStage() == null);

            MarkDirtyRepaint();
        }

        private Texture2D GetIcon(string kind)
        {
            if (_data != null)
            {
                switch (kind)
                {
                    case "manager": return _data.iconManager != null ? _data.iconManager : WorldClustersEditorDataDefaults.FindDefaultIcon(kind);
                    case "cluster": return _data.iconCluster != null ? _data.iconCluster : WorldClustersEditorDataDefaults.FindDefaultIcon(kind);
                    case "trigger": return _data.iconTrigger != null ? _data.iconTrigger : WorldClustersEditorDataDefaults.FindDefaultIcon(kind);
                    case "collider": return _data.iconCollider != null ? _data.iconCollider : WorldClustersEditorDataDefaults.FindDefaultIcon(kind);
                }
            }

            return WorldClustersEditorDataDefaults.FindDefaultIcon(kind);
        }

        private static void CreateManager()
        {
            if (FindWorldClustersManagerInStage() != null)
            {
                EditorUtility.DisplayDialog("World Clusters", "A World Clusters Manager already exists in this stage.", "OK");
                return;
            }

            var stage = PrefabStageUtility.GetCurrentPrefabStage();
            var root = stage != null ? stage.prefabContentsRoot : null;

            var go = new GameObject("WorldCluster_MANAGER");
            Undo.RegisterCreatedObjectUndo(go, "Create WorldClustersManager");
            go.AddComponent<WorldClustersManager>();

            if (root != null)
                go.transform.SetParent(root.transform, false);

            Selection.activeGameObject = go;

            if (stage == null)
                EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            else
                EditorSceneManager.MarkSceneDirty(stage.scene);
        }

        private static void AddToSelection<T>(bool autoWireCluster = false) where T : Component
        {
            var targets = GatherSelectionGameObjects();
            if (targets.Count == 0)
            {
                EditorUtility.DisplayDialog("World Clusters", "Select one or more GameObjects in the Scene or Project.", "OK");
                return;
            }

            int added = 0;

            foreach (var go in targets)
            {
                if (go == null) continue;

                if (EditorUtility.IsPersistent(go) && PrefabUtility.IsPartOfPrefabAsset(go))
                {
                    if (TryAddToPrefabAsset<T>(go, autoWireCluster))
                        added++;
                    continue;
                }

                if (go.GetComponent<T>() != null)
                    continue;

                var comp = Undo.AddComponent<T>(go);
                added++;

                if (autoWireCluster)
                    TryAutoWireClusterReference(comp, go);

                EditorSceneManager.MarkSceneDirty(go.scene);
            }

            if (added > 0)
                EditorApplication.RepaintHierarchyWindow();
        }

        private static bool TryAddToPrefabAsset<T>(GameObject prefabAssetRoot, bool autoWireCluster) where T : Component
        {
            var path = AssetDatabase.GetAssetPath(prefabAssetRoot);
            if (string.IsNullOrEmpty(path)) return false;

            GameObject contentsRoot = null;
            try
            {
                contentsRoot = PrefabUtility.LoadPrefabContents(path);

                if (contentsRoot.GetComponent<T>() != null)
                    return false;

                var comp = contentsRoot.AddComponent<T>();

                if (autoWireCluster)
                    TryAutoWireClusterReference(comp, contentsRoot);

                PrefabUtility.SaveAsPrefabAsset(contentsRoot, path);
                AssetDatabase.ImportAsset(path);

                return true;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"WORLD CLUSTERS: Failed to add {typeof(T).Name} to prefab '{path}'. {e.Message}");
                return false;
            }
            finally
            {
                if (contentsRoot != null)
                    PrefabUtility.UnloadPrefabContents(contentsRoot);
            }
        }

        private static void TryAutoWireClusterReference(Component comp, GameObject host)
        {
            if (comp == null || host == null) return;

            var cluster = host.GetComponent<Cluster>();
            if (cluster == null)
                cluster = host.GetComponentInParent<Cluster>();

            if (cluster == null) return;

            switch (comp)
            {
                case ClusterCollider cc:
                    cc.cluster = cluster;
                    EditorUtility.SetDirty(cc);
                    break;
                case ClusterInstantTrigger it:
                    it.cluster = cluster;
                    EditorUtility.SetDirty(it);
                    break;
            }
        }

        private static WorldClustersManager FindWorldClustersManagerInStage()
        {
            var stage = PrefabStageUtility.GetCurrentPrefabStage();
            if (stage != null && stage.prefabContentsRoot != null)
                return stage.prefabContentsRoot.GetComponentInChildren<WorldClustersManager>(true);

#if UNITY_2023_1_OR_NEWER
            return UnityEngine.Object.FindFirstObjectByType<WorldClustersManager>();
#else
            return UnityEngine.Object.FindObjectOfType<WorldClustersManager>();
#endif
        }

        private static List<GameObject> GatherSelectionGameObjects()
        {
            var results = new List<GameObject>(Selection.objects.Length);

            foreach (var obj in Selection.objects)
            {
                if (obj == null) continue;

                if (obj is GameObject go)
                {
                    results.Add(go);
                    continue;
                }

                var assetPath = AssetDatabase.GetAssetPath(obj);
                if (string.IsNullOrEmpty(assetPath)) continue;

                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
                if (prefab != null)
                    results.Add(prefab);
            }

            return results;
        }
    }

#else

    // Unity < 2021.2: Overlays not available.
    [InitializeOnLoad]
    public static class WorldClustersSceneQuickMenuOverlay
    {
        static WorldClustersSceneQuickMenuOverlay() { }
    }

#endif
}
#endif
