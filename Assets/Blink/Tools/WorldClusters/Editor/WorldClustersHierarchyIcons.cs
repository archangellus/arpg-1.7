#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace BLINK.WorldClusters
{
    [InitializeOnLoad]
    public static class WorldClustersHierarchyIcons
    {
        private static WorldClustersEditorData _data;

        private static Texture2D _iconManager, _iconCluster, _iconCollider, _iconTrigger;
        private static Texture2D _iconContains; // NEW
        private static bool _enabled;

        // Cache: instanceID -> icon to draw (null means none)
        private static readonly Dictionary<int, Texture2D> _iconCache = new();

        static WorldClustersHierarchyIcons()
        {
            EditorApplication.hierarchyWindowItemOnGUI += OnHierarchyItemGUI;

            // Invalidate cache whenever hierarchy/scene changes
            EditorApplication.hierarchyChanged += ClearCacheAndRepaint;
            EditorSceneManager.sceneOpened += (_, __) => ClearCacheAndRepaint();

            Reload();
        }

        public static void Reload()
        {
            _data = Resources.Load<WorldClustersEditorData>("EditorData/WorldClustersEditorData");

            _enabled = _data != null && _data.hierarchyIconsEnabled;
            _iconManager = _data != null ? _data.iconManager : null;
            _iconCluster = _data != null ? _data.iconCluster : null;
            _iconCollider = _data != null ? _data.iconCollider : null;
            _iconTrigger = _data != null ? _data.iconTrigger : null;

            _iconContains = _data != null ? _data.iconContainsWorldClusters : null; // NEW

            ClearCacheAndRepaint();
        }

        private static void ClearCacheAndRepaint()
        {
            _iconCache.Clear();
            EditorApplication.RepaintHierarchyWindow();
        }

        private static void OnHierarchyItemGUI(int instanceID, Rect selectionRect)
        {
            if (!_enabled) return;
            if (Event.current.type != EventType.Repaint) return;

            if (_iconCache.TryGetValue(instanceID, out var cached))
            {
                if (cached) DrawIcon(selectionRect, cached);
                return;
            }

            Object obj;
            #if UNITY_6000_3_OR_NEWER
            obj = EditorUtility.EntityIdToObject((EntityId)instanceID);
            #else
            obj = EditorUtility.InstanceIDToObject(instanceID);
            #endif

            var go = obj as GameObject;

            if (!go)
            {
                _iconCache[instanceID] = null;
                return;
            }

            // 1) direct component icon (your current behavior)
            var icon = PickDirectIcon(go);

            // 2) NEW: if no direct icon, but children contain WC components -> show default "contains" icon
            if (!icon && _iconContains && HasWorldClustersInChildren(go))
                icon = _iconContains;

            _iconCache[instanceID] = icon;
            if (icon) DrawIcon(selectionRect, icon);
        }

        private static void DrawIcon(Rect selectionRect, Texture2D icon)
        {
            var r = new Rect(selectionRect.x - 16f, selectionRect.y, 16f, 16f);
            GUI.DrawTexture(r, icon, ScaleMode.ScaleToFit, true);
        }

        private static Texture2D PickDirectIcon(GameObject go)
        {
            // Keep your priority order
            if (_iconManager && go.GetComponent<WorldClustersManager>()) return _iconManager;
            if (_iconCluster && go.GetComponent<Cluster>()) return _iconCluster;
            if (_iconCollider && go.GetComponent<ClusterCollider>()) return _iconCollider;
            if (_iconTrigger && go.GetComponent<ClusterInstantTrigger>()) return _iconTrigger;
            return null;
        }

        private static bool HasWorldClustersInChildren(GameObject go)
        {
            // includes self, but we only call this if PickDirectIcon returned null, so it's fine.
            return go.GetComponentInChildren<WorldClustersManager>(true) ||
                   go.GetComponentInChildren<Cluster>(true) ||
                   go.GetComponentInChildren<ClusterCollider>(true) ||
                   go.GetComponentInChildren<ClusterInstantTrigger>(true);
        }
    }
}
#endif
