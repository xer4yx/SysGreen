using NSubstitute;
using SysGreen.App.Services;
using SysGreen.App.ViewModels;
using SysGreen.Core.Usage;

namespace SysGreen.Tests;

public class SettingsViewModelTests
{
    private static (SettingsViewModel Vm, ITrackingSettings Tracking, IDataRetentionSettings Retention,
        IDataStoreReset Reset, IAppUninstaller Uninstaller) Build(bool tracking = true, bool keep = true)
    {
        var t = Substitute.For<ITrackingSettings>();
        t.LaunchTrackingEnabled.Returns(tracking);
        var r = Substitute.For<IDataRetentionSettings>();
        r.KeepDataOnUninstall.Returns(keep);
        var reset = Substitute.For<IDataStoreReset>();
        var uninstaller = Substitute.For<IAppUninstaller>();
        return (new SettingsViewModel(t, r, reset, uninstaller), t, r, reset, uninstaller);
    }

    [Fact]
    public void Loads_current_settings_on_construction()
    {
        // True is discriminating: ObservableProperty bools default to false, so reading true proves
        // the VM loaded from the settings rather than leaving the field at its default.
        var (vm, _, _, _, _) = Build(tracking: true, keep: true);

        Assert.True(vm.LaunchTrackingEnabled);
        Assert.True(vm.KeepDataOnUninstall);
    }

    [Fact]
    public void Toggling_launch_tracking_persists_the_choice()
    {
        var (vm, tracking, _, _, _) = Build(tracking: true);

        vm.LaunchTrackingEnabled = false;

        tracking.Received(1).SetLaunchTrackingEnabled(false);
    }

    [Fact]
    public void Toggling_keep_data_on_uninstall_persists_the_choice()
    {
        var (vm, _, retention, _, _) = Build(keep: true);

        vm.KeepDataOnUninstall = false;

        retention.Received(1).SetKeepDataOnUninstall(false);
    }

    [Fact]
    public void Resetting_data_clears_the_store()
    {
        var (vm, _, _, reset, _) = Build();

        vm.ResetDataCommand.Execute(null);

        reset.Received(1).Reset();
    }

    [Fact]
    public void Uninstalling_with_keep_persists_the_choice_then_launches_the_uninstaller()
    {
        var (vm, _, retention, _, uninstaller) = Build();

        vm.Uninstall(keepData: true);

        retention.Received(1).SetKeepDataOnUninstall(true);
        uninstaller.Received(1).Uninstall();
    }

    [Fact]
    public void Uninstalling_with_delete_records_the_delete_choice_before_uninstalling()
    {
        var (vm, _, retention, _, uninstaller) = Build();

        vm.Uninstall(keepData: false);

        retention.Received(1).SetKeepDataOnUninstall(false);
        uninstaller.Received(1).Uninstall();
    }
}
