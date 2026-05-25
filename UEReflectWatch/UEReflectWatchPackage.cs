using System;
using System.ComponentModel.Design;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
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
        public const string PackageGuidString = "05F701D1-C367-448A-AFD6-91FCE6A76680";

        // Command IDs - must match UEReflectWatch.vsct
        public static readonly Guid CommandSetGuid = new Guid("0C43793C-CB2C-4D3B-8962-E9AC9DE0E25D");
        public const int CmdIdRebuildNow = 0x0100;
        public const int CmdIdToggleSilentMode = 0x0101;
        public const int CmdIdRebuildNowMenu = 0x0102;
        public const int CmdIdToggleSilentModeMenu = 0x0103;
        public const int CmdIdInitialScan = 0x0104;

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

            // Register toolbar and menu commands.
            var commandService = await GetServiceAsync(typeof(IMenuCommandService)) as IMenuCommandService;
            if (commandService != null)
            {
                // Toolbar: Rebuild Now.
                var rebuildId = new CommandID(CommandSetGuid, CmdIdRebuildNow);
                commandService.AddCommand(new MenuCommand(OnRebuildNowClicked, rebuildId));

                // Toolbar: Toggle Silent Mode.
                var toggleId = new CommandID(CommandSetGuid, CmdIdToggleSilentMode);
                var toggleCmd = new OleMenuCommand(OnToggleSilentModeClicked, toggleId);
                toggleCmd.BeforeQueryStatus += OnToggleQueryStatus;
                commandService.AddCommand(toggleCmd);

                // Menu: Rebuild Now.
                var rebuildMenuId = new CommandID(CommandSetGuid, CmdIdRebuildNowMenu);
                commandService.AddCommand(new MenuCommand(OnRebuildNowClicked, rebuildMenuId));

                // Menu: Toggle Silent Mode.
                var toggleMenuId = new CommandID(CommandSetGuid, CmdIdToggleSilentModeMenu);
                var toggleMenuCmd = new OleMenuCommand(OnToggleSilentModeClicked, toggleMenuId);
                toggleMenuCmd.BeforeQueryStatus += OnToggleSilentModeMenuQueryStatus;
                commandService.AddCommand(toggleMenuCmd);

                // Menu: Initial Project Scan.
                var scanId = new CommandID(CommandSetGuid, CmdIdInitialScan);
                commandService.AddCommand(new OleMenuCommand(OnInitialScanClicked, scanId));
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

        // Updates the toolbar toggle button text and checked state.
        private void OnToggleQueryStatus(object sender, EventArgs e)
        {
            if (sender is OleMenuCommand cmd)
            {
                var options = GetDialogPage(typeof(UEReflectWatchOptions)) as UEReflectWatchOptions;
                var silentOn = options?.SilentMode ?? false;
                cmd.Checked = silentOn;
                cmd.Text = silentOn ? "UE: Silent ON" : "UE: Silent OFF";
            }
        }

        // Updates the menu toggle item text and checked state.
        private void OnToggleSilentModeMenuQueryStatus(object sender, EventArgs e)
        {
            if (sender is OleMenuCommand cmd)
            {
                var options = GetDialogPage(typeof(UEReflectWatchOptions)) as UEReflectWatchOptions;
                var silentOn = options?.SilentMode ?? false;
                cmd.Checked = silentOn;
                cmd.Text = silentOn ? "Silent Mode: ON" : "Silent Mode: OFF";
            }
        }

        // Tools menu command: Initial Project Scan.
        private void OnInitialScanClicked(object sender, EventArgs e)
        {
            JoinableTaskFactory.RunAsync(async () => await RunInitialScanAsync());
        }

        private async Task RunInitialScanAsync()
        {
            await JoinableTaskFactory.SwitchToMainThreadAsync();

            if (_stateStore is null) return;

            if (_project is null)
                await TryResolveProjectAsync();

            if (_project is null)
            {
                VsShellUtilities.ShowMessageBox(
                    this,
                    "No .uproject found or engine path not resolved.\n\nSet the Engine Path Override in Tools > Options > UE Reflect Watch.",
                    "UE Reflect Watch: Cannot Scan",
                    OLEMSGICON.OLEMSGICON_WARNING,
                    OLEMSGBUTTON.OLEMSGBUTTON_OK,
                    OLEMSGDEFBUTTON.OLEMSGDEFBUTTON_FIRST);
                return;
            }

            // Warn if state already exists so the user does not accidentally
            // overwrite a working baseline with a fresh one.
            var existingFiles = _stateStore.GetAllFiles();
            if (existingFiles.Count > 0)
            {
                var overwriteResult = VsShellUtilities.ShowMessageBox(
                    this,
                    $"UE Reflect Watch already has a baseline for {existingFiles.Count} file(s).\n\n" +
                    "Running an initial scan will replace the existing baseline with the current state of all header files.\n\n" +
                    "This will not trigger a rebuild. Any macro changes made after the scan " +
                    "will be detected normally on the next save.\n\n" +
                    "Continue?",
                    "UE Reflect Watch: Initial Project Scan",
                    OLEMSGICON.OLEMSGICON_QUERY,
                    OLEMSGBUTTON.OLEMSGBUTTON_YESNO,
                    OLEMSGDEFBUTTON.OLEMSGDEFBUTTON_SECOND);

                // IDYES = 6
                if (overwriteResult != 6) return;
            }

            _outputPane?.Activate();
            Log("Initial project scan started...");

            var projectRootDir = Path.GetDirectoryName(_project.UprojectPath)!;

            // Run the file system scan on a background thread to avoid
            // blocking the UI thread while walking potentially thousands of files.
            var scanResult = await Task.Run(() =>
                ProjectScanner.ScanAndSeed(projectRootDir, _stateStore));

            await JoinableTaskFactory.SwitchToMainThreadAsync();

            Log($"Initial scan complete.");
            Log($"  Files scanned  : {scanResult.FilesScanned}");
            Log($"  Files with macros : {scanResult.FilesWithMacros}");
            Log($"  Total macros found: {scanResult.TotalMacros}");

            if (scanResult.Errors.Count > 0)
            {
                Log($"  Errors ({scanResult.Errors.Count}):");
                foreach (var error in scanResult.Errors)
                    Log($"    {error}");
            }

            var errorNote = scanResult.Errors.Count > 0
                ? $"\n\n{scanResult.Errors.Count} file(s) could not be read. See the output pane for details."
                : string.Empty;

            VsShellUtilities.ShowMessageBox(
                this,
                $"Initial scan complete.\n\n" +
                $"Files scanned: {scanResult.FilesScanned}\n" +
                $"Files with macros: {scanResult.FilesWithMacros}\n" +
                $"Total macros found: {scanResult.TotalMacros}\n\n" +
                $"The extension now has a baseline for your project. " +
                $"Macro changes will be detected correctly from the next save onwards." +
                errorNote,
                "UE Reflect Watch: Initial Scan Complete",
                scanResult.Errors.Count > 0 ? OLEMSGICON.OLEMSGICON_WARNING : OLEMSGICON.OLEMSGICON_INFO,
                OLEMSGBUTTON.OLEMSGBUTTON_OK,
                OLEMSGDEFBUTTON.OLEMSGDEFBUTTON_FIRST);
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