using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace PLAYERTWO.ARPGProject
{
    public partial class PluginInspectorWindow
    {
        [Serializable]
        private class PluginMetadata
        {
            public string description = string.Empty;
            public string previewImageRelativePath = string.Empty;
            public string iconRelativePath = string.Empty;
        }

        private sealed class PluginPreviewWindow : EditorWindow
        {
            private Texture2D m_texture;

            public static void Show(Texture2D texture, string pluginName)
            {
                var wnd = CreateInstance<PluginPreviewWindow>();
                wnd.titleContent = new GUIContent($"{pluginName} Preview");
                wnd.m_texture = texture;
                wnd.minSize = new Vector2(320f, 240f);
                wnd.ShowUtility();
            }

            private void OnGUI()
            {
                if (m_texture == null)
                {
                    EditorGUILayout.LabelField("No image loaded.", EditorStyles.centeredGreyMiniLabel, GUILayout.ExpandHeight(true));
                    return;
                }

                Rect rect = GUILayoutUtility.GetRect(position.width - 16f, position.height - 16f, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
                UnityEngine.GUI.DrawTexture(rect, m_texture, ScaleMode.ScaleToFit);
            }
        }

        private void EnsureMetadataLoaded(string pluginRoot)
        {
            if (!string.Equals(m_metadataRoot, pluginRoot, StringComparison.OrdinalIgnoreCase))
                LoadPluginMetadata(pluginRoot);
        }

        private void LoadPluginMetadata(string pluginRoot)
        {
            m_pluginDescription = string.Empty;
            m_previewImageRelativePath = string.Empty;
            m_previewTexture = null;
            m_iconRelativePath = string.Empty;
            m_iconTexture = null;

            if (string.IsNullOrEmpty(pluginRoot) || !Directory.Exists(pluginRoot))
            {
                m_metadataRoot = pluginRoot;
                return;
            }

            string metaFolder = EnsurePluginDetailsFolder(pluginRoot);
            if (string.IsNullOrEmpty(metaFolder))
                return;
            string metaPath = Path.Combine(metaFolder, PluginMetadataFileName);
            string metaAbsPath = Path.GetFullPath(metaPath);
            string legacyMetaPath = Path.GetFullPath(Path.Combine(pluginRoot, PluginMetadataFileName));

            if (!File.Exists(metaAbsPath) && File.Exists(legacyMetaPath))
            {
                Directory.CreateDirectory(metaFolder);
                File.Move(legacyMetaPath, metaAbsPath);
            }

            if (File.Exists(metaAbsPath))
            {
                try
                {
                    var metadata = JsonUtility.FromJson<PluginMetadata>(File.ReadAllText(metaAbsPath)) ?? new PluginMetadata();
                    m_pluginDescription = metadata.description ?? string.Empty;
                    if (m_pluginDescription.Length > DescriptionCharacterLimit)
                        m_pluginDescription = m_pluginDescription.Substring(0, DescriptionCharacterLimit);
                    m_previewImageRelativePath = metadata.previewImageRelativePath ?? string.Empty;
                    m_iconRelativePath = metadata.iconRelativePath ?? string.Empty;
                    MovePreviewIntoDetailsFolder(pluginRoot, metaFolder);
                    MoveIconIntoDetailsFolder(pluginRoot, metaFolder);
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[PluginInspector] Failed to read plugin metadata: {ex.Message}");
                }
            }

            LoadPreviewTexture(pluginRoot);
            LoadIconTexture(pluginRoot);
            m_metadataRoot = pluginRoot;
        }

        private void SavePluginMetadata(string pluginRoot)
        {
            if (string.IsNullOrEmpty(pluginRoot) || !Directory.Exists(pluginRoot)) return;

            var metadata = new PluginMetadata
            {
                description = m_pluginDescription ?? string.Empty,
                previewImageRelativePath = m_previewImageRelativePath ?? string.Empty,
                iconRelativePath = m_iconRelativePath ?? string.Empty
            };

            try
            {
                string metaFolder = EnsurePluginDetailsFolder(pluginRoot);
                if (string.IsNullOrEmpty(metaFolder))
                    return;
                string metaPath = Path.Combine(metaFolder, PluginMetadataFileName);
                string metaAbsPath = Path.GetFullPath(metaPath);

                File.WriteAllText(metaAbsPath, JsonUtility.ToJson(metadata, true), Encoding.UTF8);
                m_metadataRoot = pluginRoot;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[PluginInspector] Failed to save plugin metadata: {ex.Message}");
            }
        }

        private void LoadPreviewTexture(string pluginRoot)
        {
            m_previewTexture = null;

            if (string.IsNullOrEmpty(pluginRoot) || string.IsNullOrEmpty(m_previewImageRelativePath))
                return;

            string assetPath = Path.Combine(pluginRoot, m_previewImageRelativePath).Replace('\\', '/');
            m_previewTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);

            if (m_previewTexture == null && File.Exists(assetPath))
            {
                AssetDatabase.ImportAsset(assetPath);
                m_previewTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
            }
        }

        private void LoadIconTexture(string pluginRoot)
        {
            m_iconTexture = null;

            if (string.IsNullOrEmpty(pluginRoot) || string.IsNullOrEmpty(m_iconRelativePath))
                return;

            string assetPath = Path.Combine(pluginRoot, m_iconRelativePath).Replace('\\', '/');
            m_iconTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);

            if (m_iconTexture == null && File.Exists(assetPath))
            {
                AssetDatabase.ImportAsset(assetPath);
                m_iconTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
            }
        }

        private void PickPreviewImage(string pluginRoot)
        {
            if (string.IsNullOrEmpty(pluginRoot) || !Directory.Exists(pluginRoot))
                return;

            string picked = EditorUtility.OpenFilePanel("Select preview image", Application.dataPath, "png,jpg,jpeg");
            if (string.IsNullOrEmpty(picked)) return;

            string ext = Path.GetExtension(picked);
            if (string.IsNullOrEmpty(ext)) ext = ".png";

            string detailsFolder = EnsurePluginDetailsFolder(pluginRoot);
            if (string.IsNullOrEmpty(detailsFolder))
                return;
            string dest = Path.Combine(detailsFolder, $"plugin_preview{ext}");

            string existingMatchingImage = FindMatchingPreview(detailsFolder, picked);
            if (!string.IsNullOrEmpty(existingMatchingImage))
            {
                m_previewImageRelativePath = MakeRelativeToPluginRoot(pluginRoot, existingMatchingImage);
                LoadPreviewTexture(pluginRoot);
                SavePluginMetadata(pluginRoot);
                return;
            }

            ReplaceExistingPreview(detailsFolder, dest);

            File.Copy(picked, dest, true);
            AssetDatabase.Refresh();

            m_previewImageRelativePath = MakeRelativeToPluginRoot(pluginRoot, dest);
            LoadPreviewTexture(pluginRoot);
            SavePluginMetadata(pluginRoot);
        }

        private void PickIconImage(string pluginRoot)
        {
            if (string.IsNullOrEmpty(pluginRoot) || !Directory.Exists(pluginRoot))
                return;

            string picked = EditorUtility.OpenFilePanel("Select icon image", Application.dataPath, "png,ico");
            if (string.IsNullOrEmpty(picked)) return;

            string ext = Path.GetExtension(picked);
            if (string.IsNullOrEmpty(ext)) ext = ".png";

            string detailsFolder = EnsurePluginDetailsFolder(pluginRoot);
            if (string.IsNullOrEmpty(detailsFolder))
                return;

            string dest = Path.Combine(detailsFolder, $"plugin_icon{ext}");
            string existingMatchingImage = FindMatchingIcon(detailsFolder, picked);
            if (!string.IsNullOrEmpty(existingMatchingImage))
            {
                m_iconRelativePath = MakeRelativeToPluginRoot(pluginRoot, existingMatchingImage);
                LoadIconTexture(pluginRoot);
                SavePluginMetadata(pluginRoot);
                CachePluginIcon(pluginRoot, m_iconTexture);
                return;
            }

            ReplaceExistingIcon(detailsFolder, dest);

            File.Copy(picked, dest, true);
            AssetDatabase.Refresh();

            m_iconRelativePath = MakeRelativeToPluginRoot(pluginRoot, dest);
            LoadIconTexture(pluginRoot);
            SavePluginMetadata(pluginRoot);
            CachePluginIcon(pluginRoot, m_iconTexture);
        }

        private string EnsurePluginDetailsFolder(string pluginRoot)
        {
            if (string.IsNullOrEmpty(pluginRoot) || !Directory.Exists(pluginRoot))
                return string.Empty;

            string path = Path.Combine(pluginRoot, PluginMetadataFolderName);
            if (!Directory.Exists(path))
                Directory.CreateDirectory(path);

            return path;
        }

        private static bool IsPreviewImage(string path)
        {
            string ext = Path.GetExtension(path)?.ToLowerInvariant();
            return ext == ".png" || ext == ".jpg" || ext == ".jpeg";
        }

        private static bool IsIconImage(string path)
        {
            string ext = Path.GetExtension(path)?.ToLowerInvariant();
            return ext == ".png" || ext == ".ico";
        }

        private static string FindMatchingPreview(string detailsFolder, string picked)
        {
            if (!Directory.Exists(detailsFolder) || string.IsNullOrEmpty(picked))
                return string.Empty;

            foreach (var existing in Directory.GetFiles(detailsFolder).Where(IsPreviewImage))
            {
                if (FilesMatch(existing, picked))
                    return existing;
            }

            return string.Empty;
        }

        private static string FindMatchingIcon(string detailsFolder, string picked)
        {
            if (!Directory.Exists(detailsFolder) || string.IsNullOrEmpty(picked))
                return string.Empty;

            foreach (var existing in Directory.GetFiles(detailsFolder).Where(IsIconImage))
            {
                if (FilesMatch(existing, picked))
                    return existing;
            }

            return string.Empty;
        }

        private static void ReplaceExistingPreview(string detailsFolder, string destinationPath)
        {
            if (string.IsNullOrEmpty(detailsFolder))
                return;

            if (!Directory.Exists(detailsFolder))
                Directory.CreateDirectory(detailsFolder);

            foreach (var existing in Directory.GetFiles(detailsFolder).Where(IsPreviewImage))
            {
                if (!string.Equals(existing, destinationPath, StringComparison.OrdinalIgnoreCase))
                    File.Delete(existing);
            }
        }

        private static void ReplaceExistingIcon(string detailsFolder, string destinationPath)
        {
            if (string.IsNullOrEmpty(detailsFolder))
                return;

            if (!Directory.Exists(detailsFolder))
                Directory.CreateDirectory(detailsFolder);

            foreach (var existing in Directory.GetFiles(detailsFolder).Where(IsIconImage))
            {
                if (!string.Equals(existing, destinationPath, StringComparison.OrdinalIgnoreCase))
                    File.Delete(existing);
            }
        }

        private void MovePreviewIntoDetailsFolder(string pluginRoot, string detailsFolder)
        {
            if (string.IsNullOrEmpty(m_previewImageRelativePath)) return;

            if (string.IsNullOrEmpty(pluginRoot) || !Directory.Exists(pluginRoot))
                return;

            if (string.IsNullOrEmpty(detailsFolder))
                return;

            string currentPreviewPath = Path.Combine(pluginRoot, m_previewImageRelativePath);
            string previewFullPath = Path.GetFullPath(currentPreviewPath);
            string detailsFullPath = Path.GetFullPath(detailsFolder);

            if (previewFullPath.StartsWith(detailsFullPath, StringComparison.OrdinalIgnoreCase))
                return;

            if (!File.Exists(currentPreviewPath))
                return;

            Directory.CreateDirectory(detailsFolder);
            string fileName = Path.GetFileName(currentPreviewPath);
            string destination = Path.Combine(detailsFolder, fileName);
            File.Copy(currentPreviewPath, destination, true);
            m_previewImageRelativePath = MakeRelativeToPluginRoot(pluginRoot, destination);
            SavePluginMetadata(pluginRoot);
        }

        private void MoveIconIntoDetailsFolder(string pluginRoot, string detailsFolder)
        {
            if (string.IsNullOrEmpty(m_iconRelativePath)) return;

            if (string.IsNullOrEmpty(pluginRoot) || !Directory.Exists(pluginRoot))
                return;

            if (string.IsNullOrEmpty(detailsFolder))
                return;

            string currentIconPath = Path.Combine(pluginRoot, m_iconRelativePath);
            string iconFullPath = Path.GetFullPath(currentIconPath);
            string detailsFullPath = Path.GetFullPath(detailsFolder);

            if (iconFullPath.StartsWith(detailsFullPath, StringComparison.OrdinalIgnoreCase))
                return;

            if (!File.Exists(currentIconPath))
                return;

            Directory.CreateDirectory(detailsFolder);
            string fileName = Path.GetFileName(currentIconPath);
            string destination = Path.Combine(detailsFolder, fileName);
            File.Copy(currentIconPath, destination, true);
            m_iconRelativePath = MakeRelativeToPluginRoot(pluginRoot, destination);
            SavePluginMetadata(pluginRoot);
        }

        private static bool FilesMatch(string firstPath, string secondPath)
        {
            if (!File.Exists(firstPath) || !File.Exists(secondPath)) return false;

            var firstInfo = new FileInfo(firstPath);
            var secondInfo = new FileInfo(secondPath);

            if (firstInfo.Length != secondInfo.Length)
                return false;

            using var firstStream = File.OpenRead(firstPath);
            using var secondStream = File.OpenRead(secondPath);

            int firstByte;
            while ((firstByte = firstStream.ReadByte()) != -1)
            {
                if (firstByte != secondStream.ReadByte())
                    return false;
            }

            return secondStream.ReadByte() == -1;
        }

        private static string MakeRelativeToPluginRoot(string pluginRoot, string path)
        {
            if (string.IsNullOrEmpty(pluginRoot) || string.IsNullOrEmpty(path))
                return string.Empty;

            string rootFull = Path.GetFullPath(pluginRoot);
            string pathFull = Path.GetFullPath(path);

            if (pathFull.StartsWith(rootFull, StringComparison.OrdinalIgnoreCase))
            {
                string relative = pathFull.Substring(rootFull.Length)
                    .TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                return relative.Replace('\\', '/');
            }

            return path.Replace('\\', '/');
        }

        private void UnloadPreviewImage(string pluginRoot)
        {
            m_previewImageRelativePath = string.Empty;
            m_previewTexture = null;
            SavePluginMetadata(pluginRoot);
        }

        private void UnloadIconImage(string pluginRoot)
        {
            m_iconRelativePath = string.Empty;
            m_iconTexture = null;
            SavePluginMetadata(pluginRoot);
            CachePluginIcon(pluginRoot, null);
        }

        private Texture2D GetPluginIconForList(string pluginRoot)
        {
            if (string.IsNullOrEmpty(pluginRoot))
                return null;

            if (m_pluginIconCache.TryGetValue(pluginRoot, out var cached))
                return cached;

            string metaFolder = Path.Combine(pluginRoot, PluginMetadataFolderName);
            string metaPath = Path.Combine(metaFolder, PluginMetadataFileName);
            string iconRelativePath = string.Empty;

            if (File.Exists(metaPath))
            {
                try
                {
                    var metadata = JsonUtility.FromJson<PluginMetadata>(File.ReadAllText(metaPath));
                    iconRelativePath = metadata?.iconRelativePath ?? string.Empty;
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[PluginInspector] Failed to read plugin metadata for icon: {ex.Message}");
                }
            }

            Texture2D iconTexture = null;
            if (!string.IsNullOrEmpty(iconRelativePath))
            {
                string assetPath = Path.Combine(pluginRoot, iconRelativePath).Replace('\\', '/');
                iconTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
                if (iconTexture == null && File.Exists(assetPath))
                {
                    AssetDatabase.ImportAsset(assetPath);
                    iconTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
                }
            }

            CachePluginIcon(pluginRoot, iconTexture);
            return iconTexture;
        }

        private void CachePluginIcon(string pluginRoot, Texture2D iconTexture)
        {
            if (string.IsNullOrEmpty(pluginRoot))
                return;

            m_pluginIconCache[pluginRoot] = iconTexture;
        }
    }
}
