using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace UEReflectWatch
{
    public sealed class UnrealProject
    {
        public string UprojectPath { get; }
        public string ProjectName { get; }
        public string EnginePath { get; }
        public string EngineVersion { get; }

        public UnrealProject(string uprojectPath, string projectName, string enginePath, string engineVersion)
        {
            UprojectPath = uprojectPath;
            ProjectName = projectName;
            EnginePath = enginePath;
            EngineVersion = engineVersion;
        }

        public string BuildBatPath =>
            Path.Combine(EnginePath, "Engine", "Build", "BatchFiles", "Build.bat");

        public string EditorExePath =>
            Path.Combine(EnginePath, "Engine", "Binaries", "Win64", "UnrealEditor.exe");
    }

    internal sealed class UprojectJson
    {
        [JsonPropertyName("EngineAssociation")]
        public string? EngineAssociation { get; set; }
    }

    public static class ProjectResolver
    {
        // Default install root used by the Epic Games Launcher on Windows.
        private const string EpicDefaultRoot = @"C:\Program Files\Epic Games";

        public static UnrealProject? Resolve(string solutionDirectory, string? enginePathOverride = null)
        {
            var uprojectPath = FindUproject(solutionDirectory);
            if (uprojectPath is null) return null;

            return ResolveFromUproject(uprojectPath, enginePathOverride);
        }

        private static string? FindUproject(string dir)
        {
            try
            {
                foreach (var file in Directory.GetFiles(dir, "*.uproject", SearchOption.TopDirectoryOnly))
                    return file;
            }
            catch { }
            return null;
        }

        private static UnrealProject? ResolveFromUproject(string uprojectPath, string? enginePathOverride)
        {
            try
            {
                var raw = File.ReadAllText(uprojectPath);
                var json = JsonSerializer.Deserialize<UprojectJson>(raw);

                var projectName = Path.GetFileNameWithoutExtension(uprojectPath);
                var engineVersion = json?.EngineAssociation ?? string.Empty;

                // User override takes priority.
                if (!string.IsNullOrWhiteSpace(enginePathOverride) && Directory.Exists(enginePathOverride))
                {
                    return new UnrealProject(uprojectPath, projectName, enginePathOverride, engineVersion);
                }

                // Construct the default Epic Games Launcher install path directly
                // from the EngineAssociation version string in the .uproject file.
                // e.g. "5.7" -> C:\Program Files\Epic Games\UE_5.7
                if (string.IsNullOrWhiteSpace(engineVersion))
                    return null;

                var defaultPath = Path.Combine(EpicDefaultRoot, $"UE_{engineVersion}");
                if (Directory.Exists(defaultPath))
                    return new UnrealProject(uprojectPath, projectName, defaultPath, engineVersion);

                return null;
            }
            catch
            {
                return null;
            }
        }
    }
}