using System;
using System.ComponentModel.Design;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Task = System.Threading.Tasks.Task;

namespace UEReflectWatch
{
    [PackageRegistration(UseManagedResourcesOnly = true, AllowsBackgroundLoading = true)]
    [Guid(PackageGuidString)]
    [ProvideOptionPage(typeof(UEReflectWatchOptions), "UE Reflect Watch", "General", 0, 0, true)]
    [ProvideAutoLoad(UIContextGuids80.SolutionExists, PackageAutoLoadFlags.BackgroundLoad)]
    [ProvideMenuResource("Menus.ctmenu", 1)]
    public sealed class UEReflectWatchPackage : AsyncPackage, IVsRunningDocTableEvents
    {
        public const string PackageGuidString = "a1b2c3d4-e5f6-7890-abcd-ef1234567890";

        // Command IDs - must match UEReflectWatch.vsct
        public static readonly Guid CommandSetGuid = new Guid("c2d3e4f5-a6b7-8901-cdef-012345678902");
        public const int CmdIdRebuildNow = 0x0100;
        public const int CmdIdToggleSilentMode = 0x0101;

        private IVsRunningDocumentTable? _rdt;
        private uint _rdtCookie;
        private IVsOutputWindowPane? _outputPane;
        private StateStore? _stateStore;
        private UnrealProject? _project;

        private static readonly Guid OutputPaneGuid = new Guid("b1c2d3e4-f5a6-7890-bcde-f01234567891");

        protected override async Task InitializeAsync(
            CancellationToken cancellationToken,
            IProgress<ServiceProgressData> progress)
        {
            await JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);

            _stateStore = new StateStore();

            // Set up the output window pane.
            var outputWindow = await GetServiceAsync(typeof(SVsOutputWindow)) as IVsOutputWindow;
            if (outputWindow != null)
            {
                var guid = OutputPaneGuid;
                outputWindow.CreatePane(ref guid, "UE Reflect Watch", 1, 1);
                outputWindow.GetPane(ref guid, out _outputPane);
            }

            // Subscribe to the Running Document Table for file save events.
            _rdt = await GetServiceAsync(typeof(SVsRunningDocumentTable)) as IVsRunningDocumentTable;
            if (_rdt != null)
            {
                _rdt.AdviseRunningDocTableEvents(this, out _rdtCookie);
            }

            // Register toolbar commands.
            var commandService = await GetServiceAsync(typeof(IMenuCommandService)) as IMenuCommandService;
            if (commandService != null)
            {
                // Rebuild Now button.
                var rebuildId = new CommandID(CommandSetGuid, CmdIdRebuildNow);
                var rebuildCmd = new MenuCommand(OnRebuildNowClicked, rebuildId);
                commandService.AddCommand(rebuildCmd);

                // Toggle Silent Mode button.
                var toggleId = new CommandID(CommandSetGuid, CmdIdToggleSilentMode);
                var toggleCmd = new OleMenuCommand(OnToggleSilentModeClicked, toggleId);
                toggleCmd.BeforeQueryStatus += OnToggleQueryStatus;
                commandService.AddCommand(toggleCmd);
            }

            // Resolve the Unreal project from the solution directory.
            await TryResolveProjectAsync();

            Log("UE Reflect Watch activated. Monitoring .h files for UCLASS, UPROPERTY, UFUNCTION, USTRUCT, UENUM changes.");

            if (_project != null)
            {
                Log($"Project : {_project.ProjectName}");
                Log($"Engine  : {_project.EnginePath}");
            }
            else
            {
                Log("Warning: No .uproject found or engine path could not be resolved.");
                Log("Set the Engine Path Override in Tools > Options > UE Reflect Watch if your engine is not in the default location.");
            }
        }

        private async Task TryResolveProjectAsync()
        {
            await JoinableTaskFactory.SwitchToMainThreadAsync();

            var dte = await GetServiceAsync(typeof(EnvDTE.DTE)) as EnvDTE.DTE;
            if (dte?.Solution?.FullName is null) return;

            var solutionDir = Path.GetDirectoryName(dte.Solution.FullName);
            if (solutionDir is null) return;

            var options = GetDialogPage(typeof(UEReflectWatchOptions)) as UEReflectWatchOptions;
            _project = ProjectResolver.Resolve(solutionDir, options?.EnginePathOverride);
        }

        protected override void Dispose(bool disposing)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            if (_rdt != null && _rdtCookie != 0)
            {
                _rdt.UnadviseRunningDocTableEvents(_rdtCookie);
                _rdtCookie = 0;
            }
            base.Dispose(disposing);
        }

        // Toolbar command: Rebuild Now.
        private void OnRebuildNowClicked(object sender, EventArgs e)
        {
            JoinableTaskFactory.RunAsync(async () => await TriggerRebuildAsync("Manual rebuild requested.", forcePrompt: true));
        }

        // Toolbar command: Toggle Silent Mode (suppresses the auto-prompt on save).
        private void OnToggleSilentModeClicked(object sender, EventArgs e)
        {
            var options = GetDialogPage(typeof(UEReflectWatchOptions)) as UEReflectWatchOptions;
            if (options is null) return;

            options.SilentMode = !options.SilentMode;
            options.SaveSettingsToStorage();

            var state = options.SilentMode ? "ON (prompts suppressed)" : "OFF (prompts active)";
            Log($"Silent mode: {state}");
        }

        // Updates the toggle button checked state to reflect current SilentMode.
        private void OnToggleQueryStatus(object sender, EventArgs e)
        {
            if (sender is OleMenuCommand cmd)
            {
                var options = GetDialogPage(typeof(UEReflectWatchOptions)) as UEReflectWatchOptions;
                cmd.Checked = options?.SilentMode ?? false;
                cmd.Text = cmd.Checked ? "UE: Silent ON" : "UE: Silent OFF";
            }
        }

        // IVsRunningDocTableEvents: fires after every document save.
        public int OnAfterSave(uint docCookie)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            if (_rdt == null || _stateStore == null) return VSConstants.S_OK;

            _rdt.GetDocumentInfo(
                docCookie,
                out _,
                out _,
                out _,
                out var filePath,
                out _,
                out _,
                out _);

            if (!filePath.EndsWith(".h", StringComparison.OrdinalIgnoreCase))
                return VSConstants.S_OK;

            string content;
            try { content = File.ReadAllText(filePath); }
            catch { return VSConstants.S_OK; }

            var currentMacros = MacroScanner.Scan(content);
            var previousMacros = _stateStore.GetMacros(filePath);
            var diff = MacroScanner.Diff(previousMacros, currentMacros);

            // Always update the stored state to reflect the latest save.
            _stateStore.SetMacros(filePath, currentMacros);

            if (!diff.HasChanges) return VSConstants.S_OK;

            var fileName = Path.GetFileName(filePath);
            var summary = MacroScanner.SummariseDiff(diff, fileName);
            Log("Reflection macro change detected:");
            Log($"  {summary}");

            foreach (var m in diff.Added)
                Log($"  + Line {m.Line}: {m.Raw}");
            foreach (var m in diff.Removed)
                Log($"  - Line {m.Line}: {m.Raw}");

            // Check silent mode before prompting.
            var options = GetDialogPage(typeof(UEReflectWatchOptions)) as UEReflectWatchOptions;
            if (options?.SilentMode == true)
            {
                Log("Silent mode is ON. Skipping prompt. Use the Rebuild Now button in the toolbar when ready.");
                return VSConstants.S_OK;
            }

            // Fire and forget: cannot await inside a COM callback.
            JoinableTaskFactory.RunAsync(async () => await TriggerRebuildAsync(summary, forcePrompt: false));

            return VSConstants.S_OK;
        }

        private async Task TriggerRebuildAsync(string reason, bool forcePrompt)
        {
            await JoinableTaskFactory.SwitchToMainThreadAsync();

            if (_project is null)
                await TryResolveProjectAsync();

            if (_project is null)
            {
                VsShellUtilities.ShowMessageBox(
                    this,
                    "No .uproject found or engine path not resolved.\n\nSet the Engine Path Override in Tools > Options > UE Reflect Watch.",
                    "UE Reflect Watch: Cannot Rebuild",
                    OLEMSGICON.OLEMSGICON_WARNING,
                    OLEMSGBUTTON.OLEMSGBUTTON_OK,
                    OLEMSGDEFBUTTON.OLEMSGDEFBUTTON_FIRST);
                return;
            }

            var options = GetDialogPage(typeof(UEReflectWatchOptions)) as UEReflectWatchOptions
                          ?? new UEReflectWatchOptions();

            // Confirmation: ask whether work has been saved in the editor.
            // Always shown when triggered manually (forcePrompt). When triggered
            // by a save event, only shown if ConfirmBeforeRebuild is enabled.
            if (forcePrompt || options.ConfirmBeforeRebuild)
            {
                var confirmResult = VsShellUtilities.ShowMessageBox(
                    this,
                    "The Unreal Editor will be closed and the project will be rebuilt.\n\n" +
                    $"{reason}\n\n" +
                    "Have you saved your work in the Unreal Editor?",
                    "UE Reflect Watch: Confirm Rebuild",
                    OLEMSGICON.OLEMSGICON_WARNING,
                    OLEMSGBUTTON.OLEMSGBUTTON_YESNO,
                    OLEMSGDEFBUTTON.OLEMSGDEFBUTTON_SECOND);

                // IDYES = 6
                if (confirmResult != 6)
                {
                    Log("Rebuild cancelled by user.");
                    Log("Remember to close the editor and rebuild manually before testing your macro changes.");
                    return;
                }
            }

            // If autoRebuild is off, ask whether to rebuild now or later.
            if (!options.AutoRebuild)
            {
                var rebuildResult = VsShellUtilities.ShowMessageBox(
                    this,
                    $"{reason}\n\nRebuild now?",
                    "UE Reflect Watch",
                    OLEMSGICON.OLEMSGICON_QUERY,
                    OLEMSGBUTTON.OLEMSGBUTTON_YESNO,
                    OLEMSGDEFBUTTON.OLEMSGDEFBUTTON_FIRST);

                // IDYES = 6
                if (rebuildResult != 6)
                {
                    Log("Rebuild deferred by user. Remember to close the editor and rebuild before testing.");
                    return;
                }
            }

            _outputPane?.Activate();

            var result = await BuildRunner.RunCycleAsync(_project, _outputPane!, options);

            if (result == BuildResult.Succeeded)
            {
                VsShellUtilities.ShowMessageBox(
                    this,
                    $"{_project.ProjectName} built successfully." +
                    (options.AutoRelaunchEditor ? "\n\nThe Unreal Editor is relaunching." : string.Empty),
                    "UE Reflect Watch: Build Succeeded",
                    OLEMSGICON.OLEMSGICON_INFO,
                    OLEMSGBUTTON.OLEMSGBUTTON_OK,
                    OLEMSGDEFBUTTON.OLEMSGDEFBUTTON_FIRST);
            }
            else
            {
                VsShellUtilities.ShowMessageBox(
                    this,
                    $"Build failed for {_project.ProjectName}.\n\nSee the UE Reflect Watch output pane for details.",
                    "UE Reflect Watch: Build Failed",
                    OLEMSGICON.OLEMSGICON_CRITICAL,
                    OLEMSGBUTTON.OLEMSGBUTTON_OK,
                    OLEMSGDEFBUTTON.OLEMSGDEFBUTTON_FIRST);
            }
        }

        private void Log(string message)
        {
            _outputPane?.OutputStringThreadSafe($"[UE Reflect Watch] {message}{Environment.NewLine}");
        }

        // Unused IVsRunningDocTableEvents members.
        public int OnAfterFirstDocumentLock(uint cookie, uint lockType, uint readLocks, uint editLocks) => VSConstants.S_OK;
        public int OnBeforeLastDocumentUnlock(uint cookie, uint lockType, uint readLocks, uint editLocks) => VSConstants.S_OK;
        public int OnAfterAttributeChange(uint cookie, uint grfAttribs) => VSConstants.S_OK;
        public int OnBeforeDocumentWindowShow(uint cookie, int fFirstShow, IVsWindowFrame pFrame) => VSConstants.S_OK;
        public int OnAfterDocumentWindowHide(uint cookie, IVsWindowFrame pFrame) => VSConstants.S_OK;
    }
}
