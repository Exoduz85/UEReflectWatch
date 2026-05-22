using System;
using System.Collections.Generic;
using System.IO;

namespace UEReflectWatch
{
    public sealed class ProjectScanResult
    {
        public int FilesScanned { get; set; }
        public int FilesWithMacros { get; set; }
        public int TotalMacros { get; set; }
        public List<string> Errors { get; } = new List<string>();
    }

    public static class ProjectScanner
    {
        // Folders that are never part of the user's own source. Scanning them
        // would seed thousands of engine header entries into the state store
        // which wastes memory, slows every subsequent diff, and would generate
        // false positives if the engine itself is ever updated.
        private static readonly HashSet<string> ExcludedFolderNames = new HashSet<string>(
            StringComparer.OrdinalIgnoreCase)
        {
            "Binaries",
            "Intermediate",
            "DerivedDataCache",
            "Saved",
            "Plugins",   // exclude plugins to keep scope tight; can be made optional later
            ".vs",
            ".git"
        };

        public static ProjectScanResult ScanAndSeed(string projectRootDir, StateStore stateStore)
        {
            var result = new ProjectScanResult();

            var sourceDir = Path.Combine(projectRootDir, "Source");
            if (!Directory.Exists(sourceDir))
            {
                // Fall back to the project root if there is no Source subfolder.
                sourceDir = projectRootDir;
            }

            var headerFiles = FindHeaderFiles(sourceDir, result);

            foreach (var filePath in headerFiles)
            {
                try
                {
                    var content = File.ReadAllText(filePath);
                    var macros = MacroScanner.Scan(content);
                    stateStore.SetMacros(filePath, macros);

                    result.FilesScanned++;
                    if (macros.Count > 0)
                    {
                        result.FilesWithMacros++;
                        result.TotalMacros += macros.Count;
                    }
                }
                catch (Exception ex)
                {
                    result.Errors.Add($"{Path.GetFileName(filePath)}: {ex.Message}");
                }
            }

            return result;
        }

        private static List<string> FindHeaderFiles(string rootDir, ProjectScanResult result)
        {
            var files = new List<string>();

            try
            {
                // Non-recursive enumeration so we can filter excluded folders.
                foreach (var file in Directory.GetFiles(rootDir, "*.h", SearchOption.TopDirectoryOnly))
                {
                    files.Add(file);
                }

                foreach (var subDir in Directory.GetDirectories(rootDir))
                {
                    var folderName = Path.GetFileName(subDir);
                    if (ExcludedFolderNames.Contains(folderName)) continue;

                    files.AddRange(FindHeaderFiles(subDir, result));
                }
            }
            catch (Exception ex)
            {
                result.Errors.Add($"Could not read directory {rootDir}: {ex.Message}");
            }

            return files;
        }
    }
}