#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

namespace BLINK.WorldClusters
{
    /// <summary>
    /// Ensures WorldClustersEditorData has default icon assignments.
    /// Defaults are loaded from: Assets/Blink/Tools/WorldClusters/EditorUI/HierarchyIcons
    /// </summary>
    public static class WorldClustersEditorDataDefaults
    {
        public const string DefaultIconsFolder = "Assets/Blink/Tools/WorldClusters/EditorUI/HierarchyIcons";

        public static Texture2D FindDefaultIcon(string kind)
        {
            if (string.IsNullOrWhiteSpace(kind)) return null;
            var k = kind.ToLowerInvariant();

            switch (k)
            {
                case "manager":
                    return FindIcon(new[] { "manager" }, exclude: null);
                case "cluster":
                    return FindIcon(new[] { "cluster" }, exclude: new[] { "manager", "collider", "trigger", "condition", "contains", "parent", "root" });
                case "collider":
                    return FindIcon(new[] { "collider" }, exclude: null);
                case "trigger":
                    return FindIcon(new[] { "trigger" }, exclude: null);
                case "conditions":
                case "condition":
                    return FindIcon(new[] { "condition" }, exclude: null);
                case "contains":
                case "parent":
                case "root":
                    return FindIconAnyOf(
                        new[]
                        {
                            new[] { "contains" },
                            new[] { "parent" },
                            new[] { "root" },
                            new[] { "worldcluster" },
                            new[] { "world", "cluster" }
                        },
                        exclude: null);
                default:
                    return FindIcon(new[] { k }, exclude: null);
            }
        }

        public static void EnsureDefaults(WorldClustersEditorData data)
        {
            if (data == null) return;

            bool changed = false;

            // Specific icons
            changed |= EnsureIcon(ref data.iconManager, new[] { "manager" }, new[] { "collider", "trigger", "condition", "contains", "parent", "root" });
            changed |= EnsureIcon(ref data.iconCluster, new[] { "cluster" }, new[] { "manager", "collider", "trigger", "condition", "contains", "parent", "root", "worldcluster", "world" });
            changed |= EnsureIcon(ref data.iconCollider, new[] { "collider" }, new[] { "manager", "cluster", "trigger", "condition", "contains", "parent", "root" });
            changed |= EnsureIcon(ref data.iconTrigger, new[] { "trigger" }, new[] { "manager", "cluster", "collider", "condition", "contains", "parent", "root" });
            changed |= EnsureIcon(ref data.iconConditions, new[] { "condition" }, new[] { "manager", "cluster", "collider", "trigger", "contains", "parent", "root" });

            // Parent / "contains" icon: try a few patterns in priority order
            if (data.iconContainsWorldClusters == null)
            {
                data.iconContainsWorldClusters = FindIconAnyOf(
                    new[]
                    {
                        new[] { "contains" },
                        new[] { "parent" },
                        new[] { "root" },
                        new[] { "worldcluster" },
                        new[] { "world", "cluster" }
                    },
                    exclude: null);

                changed |= data.iconContainsWorldClusters != null;
            }

            if (changed)
            {
                EditorUtility.SetDirty(data);
                AssetDatabase.SaveAssets();
            }
        }

        private static bool EnsureIcon(ref Texture2D field, string[] mustContain, string[] exclude)
        {
            if (field != null) return false;
            field = FindIcon(mustContain, exclude);
            return field != null;
        }

        private static Texture2D FindIconAnyOf(string[][] mustContainSets, string[] exclude)
        {
            foreach (var set in mustContainSets)
            {
                var icon = FindIcon(set, exclude);
                if (icon != null) return icon;
            }

            return null;
        }

        private static Texture2D FindIcon(string[] mustContain, string[] exclude)
        {
            if (!AssetDatabase.IsValidFolder(DefaultIconsFolder))
                return null;

            var guids = AssetDatabase.FindAssets("t:Texture2D", new[] { DefaultIconsFolder });
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var filename = Path.GetFileNameWithoutExtension(path);
                if (string.IsNullOrEmpty(filename)) continue;

                var name = filename.ToLowerInvariant();

                if (mustContain != null)
                {
                    bool ok = true;
                    foreach (var kw in mustContain)
                    {
                        if (string.IsNullOrEmpty(kw)) continue;
                        if (!name.Contains(kw.ToLowerInvariant()))
                        {
                            ok = false;
                            break;
                        }
                    }
                    if (!ok) continue;
                }

                if (exclude != null)
                {
                    bool bad = false;
                    foreach (var kw in exclude)
                    {
                        if (string.IsNullOrEmpty(kw)) continue;
                        if (name.Contains(kw.ToLowerInvariant()))
                        {
                            bad = true;
                            break;
                        }
                    }
                    if (bad) continue;
                }

                var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                if (tex != null) return tex;
            }

            return null;
        }
    }
}
#endif
