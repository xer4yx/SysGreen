using NSubstitute;
using SysGreen.App.Services;
using SysGreen.App.ViewModels;
using SysGreen.Core.Abstractions;
using SysGreen.Core.Apply;
using SysGreen.Core.ChangeLog;
using SysGreen.Core.Domain;
using SysGreen.Core.Knowledge;
using SysGreen.Core.Recommendations;
using SysGreen.Core.Usage;
using SysGreen.Data;

namespace SysGreen.Tests;

/// <summary>
/// The "update available" banner is driven by <see cref="IUpdateService"/> (Velopack — ADR-0009).
/// The service itself is a humble adapter; this verifies the view-model's banner logic.
/// </summary>
public class MainViewModelUpdateTests
{
    private static MainViewModel BuildVm(IUpdateService? updates)
    {
        var autostart = Substitute.For<IAutostartProvider>();
        autostart.Enumerate().Returns(Array.Empty<AutostartEntry>());
        var processes = Substitute.For<IProcessProvider>();
        processes.Enumerate().Returns(Array.Empty<ProcessInfo>());
        var engine = Substitute.For<IRecommendationEngine>();
        engine.Recommend(Arg.Any<IReadOnlyList<ManageableItem>>(),
                         Arg.Any<IReadOnlyList<UsageRecord>>(), Arg.Any<DateTime>())
            .Returns(Array.Empty<Recommendation>());
        var usage = Substitute.For<IUsageRepository>();
        usage.GetAll().Returns(Array.Empty<UsageRecord>());
        var history = Substitute.For<IChangeRecordRepository>();
        history.GetRecent(Arg.Any<int>()).Returns(Array.Empty<ChangeRecord>());

        return new MainViewModel(autostart, processes, Substitute.For<IClassifier>(), engine, usage,
            history, Substitute.For<IApplyService>(), Substitute.For<IChangeReverser>(),
            Substitute.For<IOverrideStore>(), Substitute.For<IItemController>(), updates);
    }

    [Fact]
    public async Task Surfaces_an_update_when_a_newer_version_is_available()
    {
        var updates = Substitute.For<IUpdateService>();
        updates.CheckForUpdateAsync().Returns(new UpdateCheckResult(true, "0.21.0"));
        var vm = BuildVm(updates);

        await vm.CheckForUpdatesAsync();

        Assert.True(vm.UpdateAvailable);
        Assert.Equal("0.21.0", vm.UpdateVersion);
    }

    [Fact]
    public async Task No_banner_when_already_current()
    {
        var updates = Substitute.For<IUpdateService>();
        updates.CheckForUpdateAsync().Returns(new UpdateCheckResult(false, null));
        var vm = BuildVm(updates);

        await vm.CheckForUpdatesAsync();

        Assert.False(vm.UpdateAvailable);
        Assert.Equal("", vm.UpdateVersion);
    }

    [Fact]
    public async Task Check_is_a_noop_without_an_update_service()
    {
        var vm = BuildVm(null);

        await vm.CheckForUpdatesAsync();

        Assert.False(vm.UpdateAvailable);
    }

    [Fact]
    public async Task Installing_the_update_applies_and_restarts_via_the_service()
    {
        var updates = Substitute.For<IUpdateService>();
        updates.CheckForUpdateAsync().Returns(new UpdateCheckResult(true, "0.21.0"));
        var vm = BuildVm(updates);
        await vm.CheckForUpdatesAsync();

        await vm.InstallUpdateCommand.ExecuteAsync(null);

        await updates.Received(1).ApplyAndRestartAsync();
    }
}
