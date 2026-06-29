using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SysGreen.App.Services;
using SysGreen.Core.Usage;

namespace SysGreen.App.ViewModels;

/// <summary>
/// Drives the Settings window. Each toggle reads its current value on construction and writes the
/// change straight back to the persisted settings (ADR-0012 launch tracking, ADR-0017 retention).
/// </summary>
public sealed partial class SettingsViewModel : ObservableObject
{
    private readonly ITrackingSettings _tracking;
    private readonly IDataRetentionSettings _retention;
    private readonly IDataStoreReset _reset;
    private readonly IAppUninstaller _uninstaller;
    private readonly IThresholdSettings _threshold;
    private readonly IPolicyProvider _policy;

    [ObservableProperty]
    private bool _launchTrackingEnabled;

    [ObservableProperty]
    private bool _keepDataOnUninstall;

    [ObservableProperty]
    private int _abandonedThresholdDays;

    /// <summary>The app's display version (vX.Y.Z), shown read-only in Settings — ADR-0015.</summary>
    public string AppVersion => AppInfo.DisplayVersion;

    /// <summary>The Privacy Policy &amp; Terms text, shown by the "View policy" action (ADR-0018).</summary>
    public string PolicyText => _policy.Text;

    public SettingsViewModel(
        ITrackingSettings tracking, IDataRetentionSettings retention, IDataStoreReset reset,
        IAppUninstaller uninstaller, IThresholdSettings threshold, IPolicyProvider policy)
    {
        _tracking = tracking;
        _retention = retention;
        _reset = reset;
        _uninstaller = uninstaller;
        _threshold = threshold;
        _policy = policy;
        _launchTrackingEnabled = tracking.LaunchTrackingEnabled; // field assignment: don't persist on load
        _keepDataOnUninstall = retention.KeepDataOnUninstall;
        _abandonedThresholdDays = threshold.AbandonedThresholdDays;
    }

    /// <summary>
    /// Records the user's keep/delete choice (ADR-0017), then launches the uninstaller. The window
    /// presents the choice and confirms before calling this — the method itself just commits and goes.
    /// </summary>
    public void Uninstall(bool keepData)
    {
        _retention.SetKeepDataOnUninstall(keepData);
        _uninstaller.Uninstall();
    }

    partial void OnLaunchTrackingEnabledChanged(bool value) => _tracking.SetLaunchTrackingEnabled(value);

    partial void OnKeepDataOnUninstallChanged(bool value) => _retention.SetKeepDataOnUninstall(value);

    partial void OnAbandonedThresholdDaysChanged(int value) => _threshold.SetAbandonedThresholdDays(value);

    /// <summary>
    /// Clears the Data Store in place (ADR-0017). The window confirms with the user before invoking
    /// this — the command itself unconditionally wipes, so it can stay simple and testable.
    /// </summary>
    [RelayCommand]
    private void ResetData() => _reset.Reset();
}
