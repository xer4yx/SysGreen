using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
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

    [ObservableProperty]
    private bool _launchTrackingEnabled;

    [ObservableProperty]
    private bool _keepDataOnUninstall;

    /// <summary>The app's display version (vX.Y.Z), shown read-only in Settings — ADR-0015.</summary>
    public string AppVersion => AppInfo.DisplayVersion;

    public SettingsViewModel(
        ITrackingSettings tracking, IDataRetentionSettings retention, IDataStoreReset reset)
    {
        _tracking = tracking;
        _retention = retention;
        _reset = reset;
        _launchTrackingEnabled = tracking.LaunchTrackingEnabled; // field assignment: don't persist on load
        _keepDataOnUninstall = retention.KeepDataOnUninstall;
    }

    partial void OnLaunchTrackingEnabledChanged(bool value) => _tracking.SetLaunchTrackingEnabled(value);

    partial void OnKeepDataOnUninstallChanged(bool value) => _retention.SetKeepDataOnUninstall(value);

    /// <summary>
    /// Clears the Data Store in place (ADR-0017). The window confirms with the user before invoking
    /// this — the command itself unconditionally wipes, so it can stay simple and testable.
    /// </summary>
    [RelayCommand]
    private void ResetData() => _reset.Reset();
}
