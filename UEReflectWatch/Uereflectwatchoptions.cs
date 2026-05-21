using System.ComponentModel;
using Microsoft.VisualStudio.Shell;

namespace UEReflectWatch
{
    public sealed class UEReflectWatchOptions : DialogPage
    {
        [Category("Rebuild")]
        [DisplayName("Silent Mode")]
        [Description("When enabled, suppresses the automatic prompt when reflection macro changes are detected on save. Changes are still logged to the output pane. Use the Rebuild Now toolbar button to trigger a rebuild manually when ready.")]
        [DefaultValue(false)]
        public bool SilentMode { get; set; } = false;

        [Category("Rebuild")]
        [DisplayName("Auto Rebuild")]
        [Description("Rebuild automatically without prompting when reflection macro changes are detected. When false, a dialog appears asking whether to rebuild now or later.")]
        [DefaultValue(false)]
        public bool AutoRebuild { get; set; } = false;

        [Category("Rebuild")]
        [DisplayName("Confirm Before Rebuild")]
        [Description("Show a confirmation dialog asking whether you have saved your work in the Unreal Editor before the rebuild starts. Disable if you find it unnecessary.")]
        [DefaultValue(true)]
        public bool ConfirmBeforeRebuild { get; set; } = true;

        [Category("Rebuild")]
        [DisplayName("Auto Relaunch Editor")]
        [Description("Automatically relaunch the Unreal Editor after a successful build.")]
        [DefaultValue(true)]
        public bool AutoRelaunchEditor { get; set; } = true;

        [Category("Rebuild")]
        [DisplayName("Kill Editor Grace Period (ms)")]
        [Description("Milliseconds to wait after closing the Unreal Editor process before starting the build.")]
        [DefaultValue(2000)]
        public int KillEditorGracePeriodMs { get; set; } = 2000;

        [Category("Engine")]
        [DisplayName("Engine Path Override")]
        [Description("Override the Unreal Engine install path. Leave empty to use the default Epic Games Launcher install location (C:\\Program Files\\Epic Games\\UE_x.x).")]
        [DefaultValue("")]
        public string EnginePathOverride { get; set; } = string.Empty;
    }
}