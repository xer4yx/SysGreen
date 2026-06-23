using NSubstitute;
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

public class MainViewModelHistoryTests
{
    private static readonly DateTime When = new(2026, 6, 22, 9, 0, 0, DateTimeKind.Utc);

    private static ChangeRecord Disabled(string name, string batchId) =>
        new(Guid.NewGuid().ToString("n"), $"HKCU:{name}", name, ChangeAction.Disable,
            "Enabled", "Disabled", "StartupApproved", When, true, null)
        { BatchId = batchId, Location = AutostartLocation.RegistryRunCurrentUser };

    private static MainViewModel BuildVm(IChangeRecordRepository history, IChangeReverser reverser)
    {
        var autostart = Substitute.For<IAutostartProvider>();
        autostart.Enumerate().Returns(Array.Empty<AutostartEntry>());

        var processes = Substitute.For<IProcessProvider>();
        processes.Enumerate().Returns(Array.Empty<ProcessInfo>());

        var classifier = Substitute.For<IClassifier>();
        var engine = Substitute.For<IRecommendationEngine>();
        engine.Recommend(Arg.Any<IReadOnlyList<ManageableItem>>(),
                         Arg.Any<IReadOnlyList<UsageRecord>>(), Arg.Any<DateTime>())
            .Returns(Array.Empty<Recommendation>());

        var usage = Substitute.For<IUsageRepository>();
        usage.GetAll().Returns(Array.Empty<UsageRecord>());

        return new MainViewModel(autostart, processes, classifier, engine, usage,
            history, Substitute.For<IApplyService>(), reverser, Substitute.For<IOverrideStore>(),
            Substitute.For<IItemController>());
    }

    [Fact]
    public void History_groups_consecutive_records_by_their_batch()
    {
        var history = Substitute.For<IChangeRecordRepository>();
        history.GetRecent(Arg.Any<int>()).Returns(new[]
        {
            Disabled("Spotify", "batch-A"),
            Disabled("Discord", "batch-A"),
            Disabled("Steam", "batch-B"),
        });

        var vm = BuildVm(history, Substitute.For<IChangeReverser>());

        Assert.Equal(2, vm.History.Count);
        Assert.Equal(2, vm.History[0].Items.Count); // batch-A: Spotify + Discord
        Assert.Single(vm.History[1].Items);         // batch-B: Steam
    }

    [Fact]
    public void Re_enabling_a_history_row_reverses_just_that_one_record()
    {
        var history = Substitute.For<IChangeRecordRepository>();
        history.GetRecent(Arg.Any<int>()).Returns(new[] { Disabled("Spotify", "batch-A") });
        var reverser = Substitute.For<IChangeReverser>();
        reverser.Reverse(Arg.Any<IReadOnlyList<ChangeRecord>>())
            .Returns(new ApplyResult(false, false, Array.Empty<ChangeRecord>()));
        var vm = BuildVm(history, reverser);

        vm.History[0].Items[0].ReEnableCommand.Execute(null);

        reverser.Received(1).Reverse(Arg.Is<IReadOnlyList<ChangeRecord>>(
            r => r.Count == 1 && r[0].ItemName == "Spotify"));
    }

    [Fact]
    public void Undoing_a_batch_reverses_every_record_in_it_at_once()
    {
        var history = Substitute.For<IChangeRecordRepository>();
        history.GetRecent(Arg.Any<int>()).Returns(new[]
        {
            Disabled("Spotify", "batch-A"),
            Disabled("Discord", "batch-A"),
        });
        var reverser = Substitute.For<IChangeReverser>();
        reverser.Reverse(Arg.Any<IReadOnlyList<ChangeRecord>>())
            .Returns(new ApplyResult(false, false, Array.Empty<ChangeRecord>()));
        var vm = BuildVm(history, reverser);

        vm.History[0].UndoCommand.Execute(null);

        reverser.Received(1).Reverse(Arg.Is<IReadOnlyList<ChangeRecord>>(r => r.Count == 2));
    }

    [Fact]
    public void A_transient_end_task_row_cannot_be_re_enabled()
    {
        var endTask = new ChangeRecord("id", "4242", "Spotify", ChangeAction.EndTask,
            "Running", "Ended", "ProcessKill", When, true, null);
        var history = Substitute.For<IChangeRecordRepository>();
        history.GetRecent(Arg.Any<int>()).Returns(new[] { endTask });
        var vm = BuildVm(history, Substitute.For<IChangeReverser>());

        Assert.False(vm.History[0].Items[0].ReEnableCommand.CanExecute(null));
        Assert.False(vm.History[0].UndoCommand.CanExecute(null)); // nothing reversible in the batch
    }
}
