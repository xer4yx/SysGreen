using NSubstitute;
using SysGreen.App.ViewModels;
using SysGreen.Core.Usage;

namespace SysGreen.Tests;

public class SettingsViewModelTests
{
    private static (SettingsViewModel Vm, ITrackingSettings Tracking, IDataRetentionSettings Retention,
        IDataStoreReset Reset) Build(bool tracking = true, bool keep = true)
    {
        var t = Substitute.For<ITrackingSettings>();
        t.LaunchTrackingEnabled.Returns(tracking);
        var r = Substitute.For<IDataRetentionSettings>();
        r.KeepDataOnUninstall.Returns(keep);
        var reset = Substitute.For<IDataStoreReset>();
        return (new SettingsViewModel(t, r, reset), t, r, reset);
    }

    [Fact]
    public void Loads_current_settings_on_construction()
    {
        // True is discriminating: ObservableProperty bools default to false, so reading true proves
        // the VM loaded from the settings rather than leaving the field at its default.
        var (vm, _, _, _) = Build(tracking: true, keep: true);

        Assert.True(vm.LaunchTrackingEnabled);
        Assert.True(vm.KeepDataOnUninstall);
    }

    [Fact]
    public void Toggling_launch_tracking_persists_the_choice()
    {
        var (vm, tracking, _, _) = Build(tracking: true);

        vm.LaunchTrackingEnabled = false;

        tracking.Received(1).SetLaunchTrackingEnabled(false);
    }

    [Fact]
    public void Toggling_keep_data_on_uninstall_persists_the_choice()
    {
        var (vm, _, retention, _) = Build(keep: true);

        vm.KeepDataOnUninstall = false;

        retention.Received(1).SetKeepDataOnUninstall(false);
    }

    [Fact]
    public void Resetting_data_clears_the_store()
    {
        var (vm, _, _, reset) = Build();

        vm.ResetDataCommand.Execute(null);

        reset.Received(1).Reset();
    }
}
