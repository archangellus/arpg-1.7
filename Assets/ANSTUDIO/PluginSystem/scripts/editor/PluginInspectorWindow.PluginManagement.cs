using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace PLAYERTWO.ARPGProject
{
    public partial class PluginInspectorWindow
    {
        private static bool IsWithinDirectory(string baseDir, string path)
        {
            var fullBase = Path.GetFullPath(baseDir)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
            var fullPath = Path.GetFullPath(path);
            return fullPath.StartsWith(fullBase, StringComparison.Ordinal);
        }

        private void ImportPluginZip()
        {
            string zipPath = EditorUtility.OpenFilePanel("Import Plug-in (.zip)", "", "zip");
            if (string.IsNullOrEmpty(zipPath)) return;

            // Ensure destination exists
            Directory.CreateDirectory(PluginsPath);

            string tempDir = Path.Combine(Path.GetTempPath(), "ARPGPluginImport_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);

            try
            {
                // Extract all entries to a temp folder (robust zip-slip protection)
                using (var fs = new FileStream(zipPath, FileMode.Open, FileAccess.Read))
                using (var archive = new ZipArchive(fs, ZipArchiveMode.Read))
                {
                    foreach (var entry in archive.Entries)
                    {
                        // Skip macOS junk & empty entries
                        if (string.IsNullOrEmpty(entry.FullName) || entry.FullName.EndsWith("/") || entry.FullName.StartsWith("__MACOSX/"))
                            continue;

                        // Normalize and compute the safe destination path
                        var cleanPath = entry.FullName.Replace('\\', '/'); // keep original filename as-is (including ".." if present)
                        string outPath = Path.GetFullPath(Path.Combine(tempDir, cleanPath));

                        // Guard: the resolved path must remain inside tempDir
                        if (!IsWithinDirectory(tempDir, outPath))
                        {
                            Debug.LogWarning($"[Plugin Import] Skipped potential zip-slip entry: {entry.FullName}");
                            continue;
                        }

                        // Ensure parent folder exists and extract
                        Directory.CreateDirectory(Path.GetDirectoryName(outPath) ?? tempDir);
                        entry.ExtractToFile(outPath, overwrite: true);
                    }
                }

                // Decide final plug-in folder name
                string[] roots = Directory
                    .GetFileSystemEntries(tempDir)
                    .Where(p => !p.EndsWith(".meta", StringComparison.OrdinalIgnoreCase))
                    .ToArray();
                string pluginName;
                string pluginDest;

                if (roots.Length == 1 && Directory.Exists(roots[0]))
                {
                    // Zip has a single top-level folder → use that as plug-in name
                    pluginName = Path.GetFileName(roots[0].TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
                    pluginDest = Path.Combine(PluginsPath, pluginName);

                    if (Directory.Exists(pluginDest) &&
                        !EditorUtility.DisplayDialog("Overwrite plug-in?",
                            $"A plug-in named \"{pluginName}\" already exists. Overwrite its contents?", "Overwrite", "Cancel"))
                        return;

                    if (Directory.Exists(pluginDest)) FileUtil.DeleteFileOrDirectory(pluginDest);
                    FileUtil.CopyFileOrDirectory(roots[0], pluginDest);
                }
                else
                {
                    // Multiple top-level entries → use the zip filename as the folder
                    pluginName = Path.GetFileNameWithoutExtension(zipPath);
                    pluginDest = Path.Combine(PluginsPath, pluginName);

                    if (Directory.Exists(pluginDest) &&
                        !EditorUtility.DisplayDialog("Overwrite plug-in?",
                            $"A plug-in named \"{pluginName}\" already exists. Overwrite its contents?", "Overwrite", "Cancel"))
                        return;

                    if (Directory.Exists(pluginDest)) FileUtil.DeleteFileOrDirectory(pluginDest);
                    Directory.CreateDirectory(pluginDest);

                    foreach (var entry in Directory.GetFileSystemEntries(tempDir))
                    {
                        string name = Path.GetFileName(entry);
                        string target = Path.Combine(pluginDest, name);
                        FileUtil.CopyFileOrDirectory(entry, target);
                    }
                }

                AssetDatabase.Refresh();
                HandlePrefabConflicts(pluginDest);
                // Refresh metadata/preview cache so newly imported plug-ins immediately
                // display their description and preview image, even when overwriting an
                // existing plug-in that was already selected in the inspector.
                if (string.Equals(m_activePluginRoot, pluginDest, StringComparison.OrdinalIgnoreCase))
                {
                    LoadPluginMetadata(pluginDest);
                }
                else
                {
                    // Force reload on next selection since cached root may point to the
                    // previous version of this plug-in.
                    m_metadataRoot = string.Empty;
                }
                Refresh();
                EditorUtility.DisplayDialog("Plug-in Imported", $"Imported \"{pluginName}\" into {PluginsPath}.", "OK");
                PluginDependencyReminderWindow.Show(pluginName, pluginDest);
            }
            catch (Exception ex)
            {
                EditorUtility.DisplayDialog("Import failed", "The plug-in could not be imported.\n\n" + ex.Message, "OK");
            }
            finally
            {
                try { if (Directory.Exists(tempDir)) FileUtil.DeleteFileOrDirectory(tempDir); } catch { /* ignore */ }
            }
        }

        private void HandlePrefabConflicts(string pluginRoot)
        {
            var pluginPrefabs = AssetDatabase.FindAssets("t:Prefab", new[] { pluginRoot })
                .Select(AssetDatabase.GUIDToAssetPath)
                .ToList();

            if (pluginPrefabs.Count == 0)
                return;

            var conflicts = new List<(string src, string dest)>();

            foreach (var pluginPath in pluginPrefabs)
            {
                var name = Path.GetFileName(pluginPath);
                var nameNoExt = Path.GetFileNameWithoutExtension(name);

                foreach (var guid in AssetDatabase.FindAssets($"t:Prefab {nameNoExt}"))
                {
                    var existingPath = AssetDatabase.GUIDToAssetPath(guid);
                    if (existingPath == pluginPath)
                        continue;
                    if (!Path.GetFileName(existingPath).Equals(name, StringComparison.OrdinalIgnoreCase))
                        continue;
                    if (existingPath.StartsWith(pluginRoot, StringComparison.OrdinalIgnoreCase))
                        continue;

                    conflicts.Add((pluginPath, existingPath));
                }
            }

            if (conflicts.Count == 0)
                return;

            var lines = conflicts.Select(c => c.dest).Distinct();
            var message = "The following prefabs already exist in the project:\n" +
                          string.Join("\n", lines) +
                          "\n\nOverwrite with plug-in versions?";

            if (!EditorUtility.DisplayDialog("Overwrite Prefabs?", message, "Overwrite", "Keep Existing"))
                return;

            var replacementSummaries = new List<string>();
            var processedTargets = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var pair in conflicts)
            {
                if (processedTargets.Contains(pair.dest))
                    continue;

                processedTargets.Add(pair.dest);

                try
                {
                    string backupPath;
                    if (TryReplacePrefabWithBackup(pair.src, pair.dest, out backupPath))
                    {
                        replacementSummaries.Add($"Replaced prefab at {pair.dest}\nBackup saved as {backupPath}");
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogError($"Failed to replace prefab {pair.dest}: {ex.Message}\n{ex.StackTrace}");
                }
            }

            AssetDatabase.Refresh();

            if (replacementSummaries.Count > 0)
            {
                var summary = string.Join("\n\n", replacementSummaries);
                EditorUtility.DisplayDialog("Prefabs Replaced", summary, "OK");
            }
        }

        private static bool TryReplacePrefabWithBackup(string pluginPrefabPath, string targetPrefabPath, out string backupPath)
        {
            backupPath = null;

            string originalGuid = AssetDatabase.AssetPathToGUID(targetPrefabPath);
            if (string.IsNullOrEmpty(originalGuid))
            {
                Debug.LogWarning($"Could not determine GUID for {targetPrefabPath}. Skipping replacement.");
                return false;
            }

            string directory = Path.GetDirectoryName(targetPrefabPath)?.Replace('\\', '/') ?? "";
            string fileName = Path.GetFileNameWithoutExtension(targetPrefabPath);
            string extension = Path.GetExtension(targetPrefabPath);

            string desiredBackupPath = Path.Combine(directory, fileName + "BCK" + extension).Replace('\\', '/');
            backupPath = AssetDatabase.GenerateUniqueAssetPath(desiredBackupPath);

            var moveError = AssetDatabase.MoveAsset(targetPrefabPath, backupPath);
            if (!string.IsNullOrEmpty(moveError))
            {
                Debug.LogError($"Could not move existing prefab to backup: {moveError}");
                return false;
            }

            AssignNewGuidToAsset(backupPath);

            CopyPrefabWithGuid(pluginPrefabPath, targetPrefabPath, originalGuid);

            return true;
        }

        private static void AssignNewGuidToAsset(string assetPath)
        {
            string metaPath = assetPath + ".meta";
            if (!File.Exists(metaPath))
            {
                AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
                return;
            }

            string metaText = File.ReadAllText(metaPath);
            string newGuid = Guid.NewGuid().ToString("N");

            if (Regex.IsMatch(metaText, @"guid:\s*[0-9a-fA-F]{32}"))
                metaText = Regex.Replace(metaText, @"guid:\s*[0-9a-fA-F]{32}", $"guid: {newGuid}");
            else
                metaText = $"guid: {newGuid}\n" + metaText;

            File.WriteAllText(metaPath, metaText);
            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
        }

        private static void CopyPrefabWithGuid(string sourcePrefabPath, string destinationPrefabPath, string desiredGuid)
        {
            string destinationDir = Path.GetDirectoryName(destinationPrefabPath);
            if (!string.IsNullOrEmpty(destinationDir) && !Directory.Exists(destinationDir))
                Directory.CreateDirectory(destinationDir);

            FileUtil.CopyFileOrDirectory(sourcePrefabPath, destinationPrefabPath);

            string sourceMetaPath = sourcePrefabPath + ".meta";
            string destinationMetaPath = destinationPrefabPath + ".meta";

            if (File.Exists(sourceMetaPath))
            {
                FileUtil.CopyFileOrDirectory(sourceMetaPath, destinationMetaPath);
            }
            else if (File.Exists(destinationMetaPath))
            {
                File.Delete(destinationMetaPath);
            }

            if (File.Exists(destinationMetaPath))
            {
                string metaText = File.ReadAllText(destinationMetaPath);

                if (Regex.IsMatch(metaText, @"guid:\s*[0-9a-fA-F]{32}"))
                    metaText = Regex.Replace(metaText, @"guid:\s*[0-9a-fA-F]{32}", $"guid: {desiredGuid}");
                else
                    metaText = $"guid: {desiredGuid}\n" + metaText;

                File.WriteAllText(destinationMetaPath, metaText);
            }

            AssetDatabase.ImportAsset(destinationPrefabPath, ImportAssetOptions.ForceUpdate);
        }

        private void ExportSelectedPluginZip()
        {
            if (s_isExporting) return;
            s_isExporting = true;

            try
            {
                // Resolve current selection to a plug-in root
                string selFolder = null;
                if (!string.IsNullOrEmpty(m_selectedPath))
                    selFolder = Directory.Exists(m_selectedPath) ? m_selectedPath : Path.GetDirectoryName(m_selectedPath);

                string pluginRoot = GetPluginRoot(selFolder);
                if (string.IsNullOrEmpty(pluginRoot) || !Directory.Exists(pluginRoot))
                {
                    EditorUtility.DisplayDialog("Nothing to export", "Select a plug-in in the list first.", "OK");
                    return;
                }

                // Persist any pending metadata (description/preview) so the archive ships with the latest info.
                SavePluginMetadata(pluginRoot);

                string pluginName = Path.GetFileName(pluginRoot.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));

                // One dialog only — where to save the ZIP
                string defaultDir = Directory.GetParent(pluginRoot)?.FullName ?? Application.dataPath;
                string zipPath = EditorUtility.SaveFilePanel("Export Plug-in", defaultDir, pluginName + ".zip", "zip");
                if (string.IsNullOrEmpty(zipPath)) return;

                // Create in temp, then move — avoids “sharing violation” even if target is inside source
                string tempZip = Path.Combine(Path.GetTempPath(), $"ARPG_{pluginName}_{Guid.NewGuid():N}.zip");
                if (File.Exists(tempZip)) File.Delete(tempZip);

                // Include the base folder so the archive has a single <pluginName>/ root
                ZipFile.CreateFromDirectory(pluginRoot, tempZip, System.IO.Compression.CompressionLevel.Optimal, includeBaseDirectory: true);

                if (File.Exists(zipPath)) File.Delete(zipPath);
                File.Move(tempZip, zipPath);

                EditorUtility.RevealInFinder(zipPath);
                EditorUtility.DisplayDialog("Plug-in Exported", $"Exported \"{pluginName}\" to:\n{zipPath}", "OK");
            }
            catch (Exception ex)
            {
                EditorUtility.DisplayDialog("Export failed", "The plug-in could not be exported.\n\n" + ex.Message, "OK");
            }
            finally
            {
                s_isExporting = false;
            }
        }

        private void CreatePlugin(string name)
        {
            string plugin = name.Trim().Replace(" ", "");
            if (string.IsNullOrEmpty(plugin)) return;

            string folder = Path.Combine(PluginsPath, plugin);
            string script = Path.Combine(folder, plugin + ".cs");

            if (Directory.Exists(folder))
            {
                EditorUtility.DisplayDialog("Error", "A plug-in with that name already exists.", "OK");
                return;
            }

            Directory.CreateDirectory(folder);

            string template =
        $@"using UnityEngine;

namespace PLAYERTWO.ARPGProject.Plugins.{plugin}
{{
    public class {plugin} : IPlugin
    {{
        public void Initialize() 
        {{
        }}
        public void Shutdown()  
        {{ 
        }}
    }}
}}";
            File.WriteAllText(script, template);

            AssetDatabase.Refresh();
            Refresh();
        }

        private void CreatePluginFile(string targetFolder, string className)
        {
            className = className.Trim().Replace(" ", "");
            if (string.IsNullOrEmpty(className)) return;

            /* 1) full path for the new file */
            string filePath = Path.Combine(targetFolder, className + ".cs");
            if (File.Exists(filePath))
            {
                EditorUtility.DisplayDialog("Error", "File already exists.", "OK");
                return;
            }

            /* 2) top-level plug-in folder name (for the namespace) */
            string root = GetPluginRoot(targetFolder);               // e.g. “…/plugins/NewPlugin”
            string ns = $"PLAYERTWO.ARPGProject.Plugins.{Path.GetFileName(root)}";

            /* 3) template — SINGLE braces so C# sees them */
            string template =
        $@"using UnityEngine;

namespace {ns}
{{
    public class {className} : MonoBehaviour
    {{
    }}
}}";

            File.WriteAllText(filePath, template);
            AssetDatabase.Refresh();
            Refresh();
        }

        private void CreateSubFolder(string parentFolder, string newFolder)
        {
            newFolder = newFolder.Trim().Replace(" ", "");
            if (string.IsNullOrEmpty(newFolder)) return;

            string path = Path.Combine(parentFolder, newFolder);
            if (Directory.Exists(path))
            {
                EditorUtility.DisplayDialog("Error", "Folder already exists.", "OK");
                return;
            }

            Directory.CreateDirectory(path);
            AssetDatabase.Refresh();
            Refresh();
        }

        private void RenamePlugin(string path, string oldName, string newName)
        {
            var className = newName.Trim().Replace(" ", "");
            if (string.IsNullOrEmpty(className))
                return;

            var directory = Path.GetDirectoryName(path);
            var newPath = Path.Combine(directory, className + ".cs");
            if (File.Exists(newPath))
            {
                EditorUtility.DisplayDialog("Error", "A plugin with that name already exists.", "OK");
                return;
            }

            var content = File.ReadAllText(path).Replace(oldName, className);
            File.WriteAllText(path, content);
            AssetDatabase.RenameAsset(path, className);
            AssetDatabase.Refresh();
            Refresh();
        }
    }
}
