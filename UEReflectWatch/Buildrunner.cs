using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;

namespace UEReflectWatch
{
    public enum BuildResult { Succeeded, Failed }

    public static class BuildRunner
    {
        public static async Task<BuildResult> RunCycleAsync(
            UnrealProject project,
            IVsOutputWindowPane outputPane,
            UEReflectWatchOptions options)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            Log(outputPane, string.Empty);
            Log(outputPane, "=== UE Reflect Watch: Rebuild Cycle Started ===");
            Log(outputPane, $"Project : {project.ProjectName}");
            Log(outputPane, $"Engine  : {project.EnginePath}");
            Log(outputPane, string.Empty);

            // Step 1: Kill the editor.
            Log(outputPane, "[1/3] Closing Unreal Editor...");
            bool killed = KillEditor();
            if (killed)
            {
                Log(outputPane, $"      Editor closed. Waiting {options.KillEditorGracePeriodMs}ms...");
                await Task.Delay(options.KillEditorGracePeriodMs);
            }
            else
            {
                Log(outputPane, "      Editor was not running.");
            }

            // Step 2: Build.
            Log(outputPane, "[2/3] Building...");
            var result = await BuildAsync(project, outputPane);

            if (result == BuildResult.Failed)
            {
                Log(outputPane, string.Empty);
                Log(outputPane, "=== Build FAILED. Editor will not be relaunched. ===");
                return BuildResult.Failed;
            }

            Log(outputPane, string.Empty);
            Log(outputPane, "[3/3] Build succeeded.");

            // Step 3: Relaunch editor.
            if (options.AutoRelaunchEditor)
            {
                Log(outputPane, "      Launching Unreal Editor...");
                LaunchEditor(project);
                Log(outputPane, "      Editor launched.");
            }
            else
            {
                Log(outputPane, "      Auto-relaunch is disabled. Open the editor manually.");
            }

            Log(outputPane, string.Empty);
            Log(outputPane, "=== UE Reflect Watch: Rebuild Cycle Complete ===");

            return BuildResult.Succeeded;
        }

        private static bool KillEditor()
        {
            var processes = Process.GetProcessesByName("UnrealEditor");
            if (processes.Length == 0) return false;

            foreach (var proc in processes)
            {
                try { proc.Kill(); proc.WaitForExit(5000); }
                catch { }
                finally { proc.Dispose(); }
            }
            return true;
        }

        private static async Task<BuildResult> BuildAsync(UnrealProject project, IVsOutputWindowPane outputPane)
        {
            var args = $"\"{project.ProjectName}Editor\" Win64 Development " +
                       $"-Project=\"{project.UprojectPath}\" -WaitMutex";

            var startInfo = new ProcessStartInfo
            {
                FileName = project.BuildBatPath,
                Arguments = args,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
            Log(outputPane, $"      {project.BuildBatPath} {args}");
            Log(outputPane, string.Empty);

            using var proc = new Process { StartInfo = startInfo };

            proc.OutputDataReceived += (_, e) =>
            {
                if (e.Data is null) return;
                ThreadHelper.JoinableTaskFactory.Run(async () =>
                {
                    await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                    Log(outputPane, $"      {e.Data}");
                });
            };

            proc.ErrorDataReceived += (_, e) =>
            {
                if (e.Data is null) return;
                ThreadHelper.JoinableTaskFactory.Run(async () =>
                {
                    await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                    Log(outputPane, $"      ERR: {e.Data}");
                });
            };

            proc.Start();
            proc.BeginOutputReadLine();
            proc.BeginErrorReadLine();

            await Task.Run(() => proc.WaitForExit());

            return proc.ExitCode == 0 ? BuildResult.Succeeded : BuildResult.Failed;
        }

        private static void LaunchEditor(UnrealProject project)
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = project.EditorExePath,
                    Arguments = $"\"{project.UprojectPath}\"",
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[UEReflectWatch] Failed to launch editor: {ex.Message}");
            }
        }

        private static void Log(IVsOutputWindowPane pane, string message)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            pane.OutputStringThreadSafe(message + Environment.NewLine);
        }
    }
}