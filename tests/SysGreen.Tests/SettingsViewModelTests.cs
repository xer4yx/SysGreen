using NSubstitute;
using SysGreen.App.Services;
using SysGreen.App.ViewModels;
using SysGreen.Core.Usage;

namespace SysGreen.Tests;

public class SettingsViewModelTests
{
    private sealed record Built(
        SettingsViewModel Vm, ITrackingSettings Tracking, IDataRetentionSettings Retention,
        IDataStoreReset Reset, IAppUninstaller Uninstaller, IThresholdSettings Threshold, IPolicyProvider Policy);

    private static Built Build(bool tracking = true, bool keep = true, int threshold = 30, string policy = "policy")
    {
        var t = Substitute.For<ITrackingSettings>();
        t.LaunchTrackingEnabled.Returns(tracking);
        var r = Substitute.For<IDataRetentionSettings>();
        r.KeepDataOnUninstall.Returns(keep);
        var reset = Substitute.For<IDataStoreReset>();
        var uninstaller = Substitute.For<IAppUninstaller>();
        var th = Substitute.For<IThresholdSettings>();
        th.AbandonedThresholdDays.Returns(threshold);
        var p = Substitute.For<IPolicyProvider>();
        p.Text.Returns(policy);
        var vm = new SettingsViewModel(t, r, reset, uninstaller, th, p);
        return new Built(vm, t, r, reset, uninstaller, th, p);
    }

    [Fact]
    public void Loads_current_settings_on_construction()
    {
        // True is discriminating: ObservableProperty bools default to false, so reading true proves
        // the VM loaded from the settings rather than leaving the field at its default.
        var b = Build(tracking: true, keep: true, threshold: 45);

        Assert.True(b.Vm.LaunchTrackingEnabled);
        Assert.True(b.Vm.KeepDataOnUninstall);
        Assert.Equal(45, b.Vm.AbandonedThresholdDays);
    }

    [Fact]
    public void Toggling_launch_tracking_persists_the_choice()
    {
        var b = Build(tracking: true);

        b.Vm.LaunchTrackingEnabled = false;

        b.Tracking.Received(1).SetLaunchTrackingEnabled(false);
    }

    [Fact]
    public void Toggling_keep_data_on_uninstall_persists_the_choice()
    {
        var b = Build(keep: true);

        b.Vm.KeepDataOnUninstall = false;

        b.Retention.Received(1).SetKeepDataOnUninstall(false);
    }

    [Fact]
    public void Changing_the_abandoned_threshold_persists_it()
    {
        var b = Build(threshold: 30);

        b.Vm.AbandonedThresholdDays = 50;

        b.Threshold.Received(1).SetAbandonedThresholdDays(50);
    }

    [Fact]
    public void Exposes_the_policy_text_for_the_view_policy_action()
    {
        var b = Build(policy: "the terms");

        Assert.Equal("the terms", b.Vm.PolicyText);
    }

    [Fact]
    public void Resetting_data_clears_the_store()
    {
        var b = Build();

        b.Vm.ResetDataCommand.Execute(null);

        b.Reset.Received(1).Reset();
    }

    [Fact]
    public void Uninstalling_with_keep_persists_the_choice_then_launches_the_uninstaller()
    {
        var b = Build();

        b.Vm.Uninstall(keepData: true);

        b.Retention.Received(1).SetKeepDataOnUninstall(true);
        b.Uninstaller.Received(1).Uninstall();
    }

    [Fact]
    public void Uninstalling_with_delete_records_the_delete_choice_before_uninstalling()
    {
        var b = Build();

        b.Vm.Uninstall(keepData: false);

        b.Retention.Received(1).SetKeepDataOnUninstall(false);
        b.Uninstaller.Received(1).Uninstall();
    }
}
