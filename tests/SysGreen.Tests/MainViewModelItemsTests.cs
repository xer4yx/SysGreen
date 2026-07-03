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

public class MainViewModelItemsTests
{
    private static AutostartEntry Entry(string name, AutostartState state = AutostartState.Enabled) =>
        new($"HKCU:{name}", name, ItemKind.StartupApp, AutostartLocation.RegistryRunCurrentUser,
            $@"C:\x\{name}.exe", null, state);

    private static Classification Class(Purpose purpose) =>
        new(purpose, SafetyRating.Safe, ClassificationSource.KnowledgeBase, "d", false) { TypicalRamBytes = 398458880 };

    private sealed record Built(
        MainViewModel Vm, IApplyService Apply, IOverrideStore Overrides, IItemController Controller,
        IChangeRecordRepository History);

    private static Built Build(
        AutostartEntry[] entries,
        Func<AutostartEntry, Classification>? classify = null,
        ProcessInfo[]? running = null,
        IItemController? controller = null)
    {
        var autostart = Substitute.For<IAutostartProvider>();
        autostart.Enumerate().Returns(entries);
        var processes = Substitute.For<IProcessProvider>();
        processes.Enumerate().Returns(running ?? Array.Empty<ProcessInfo>());

        var classifier = Substitute.For<IClassifier>();
        classify ??= _ => Class(Purpose.Media);
        classifier.Classify(Arg.Any<AutostartEntry>()).Returns(ci => classify((AutostartEntry)ci[0]));

        var engine = Substitute.For<IRecommendationEngine>();
        engine.Recommend(Arg.Any<IReadOnlyList<ManageableItem>>(),
                         Arg.Any<IReadOnlyList<UsageRecord>>(), Arg.Any<DateTime>())
            .Returns(Array.Empty<Recommendation>());
        var usage = Substitute.For<IUsageRepository>();
        usage.GetAll().Returns(Array.Empty<UsageRecord>());
        var history = Substitute.For<IChangeRecordRepository>();
        history.GetRecent(Arg.Any<int>()).Returns(Array.Empty<ChangeRecord>());

        var apply = Substitute.For<IApplyService>();
        apply.Apply(Arg.Any<IReadOnlyList<PendingChange>>())
            .Returns(new ApplyResult(false, false, Array.Empty<ChangeRecord>()));
        var overrides = Substitute.For<IOverrideStore>();
        controller ??= Substitute.For<IItemController>();

        var vm = new MainViewModel(autostart, processes, classifier, engine, usage, history, apply,
            Substitute.For<IChangeReverser>(), overrides, controller);
        return new Built(vm, apply, overrides, controller, history);
    }

    [Fact]
    public void All_items_are_grouped_by_purpose()
    {
        var b = Build([Entry("Game"), Entry("Music"), Entry("Tunes")],
            classify: e => Class(e.DisplayName == "Game" ? Purpose.Gaming : Purpose.Media));

        Assert.Equal(2, b.Vm.AllItemGroups.Count);
        Assert.Equal(new[] { Purpose.Gaming, Purpose.Media }, b.Vm.AllItemGroups.Select(g => g.Purpose));
        Assert.Single(b.Vm.AllItemGroups.First(g => g.Purpose == Purpose.Gaming).Items);
        Assert.Equal(2, b.Vm.AllItemGroups.First(g => g.Purpose == Purpose.Media).Items.Count);
    }

    [Fact]
    public void A_row_shows_state_and_ram_and_can_be_disabled()
    {
        var row = Build([Entry("Spotify")]).Vm.AllItemGroups.Single().Items.Single();

        Assert.Contains("380 MB", row.DisplayText);
        Assert.Contains("Enabled", row.DisplayText);
        Assert.True(row.CanDisable);
    }

    [Fact]
    public void A_disabled_row_cannot_be_disabled_again()
    {
        var row = Build([Entry("Old", AutostartState.Disabled)]).Vm.AllItemGroups.Single().Items.Single();
        Assert.False(row.CanDisable);
    }

    [Fact]
    public async Task Disabling_a_row_applies_a_disable_for_that_item()
    {
        var b = Build([Entry("Spotify")]);

        await b.Vm.AllItemGroups.Single().Items.Single().DisableCommand.ExecuteAsync(null);

        b.Apply.Received(1).Apply(Arg.Is<IReadOnlyList<PendingChange>>(
            c => c.Count == 1 && c[0].Entry.DisplayName == "Spotify" && c[0].Action == ChangeAction.Disable));
    }

    [Fact]
    public void A_disabled_row_can_be_enabled()
    {
        var row = Build([Entry("Old", AutostartState.Disabled)]).Vm.AllItemGroups.Single().Items.Single();
        Assert.True(row.CanEnable);
    }

    [Fact]
    public void An_enabled_row_cannot_be_enabled()
    {
        var row = Build([Entry("Spotify")]).Vm.AllItemGroups.Single().Items.Single();
        Assert.False(row.CanEnable);
    }

    [Fact]
    public async Task Enabling_a_row_applies_an_enable_for_that_item()
    {
        var b = Build([Entry("Old", AutostartState.Disabled)]);

        await b.Vm.AllItemGroups.Single().Items.Single().EnableCommand.ExecuteAsync(null);

        b.Apply.Received(1).Apply(Arg.Is<IReadOnlyList<PendingChange>>(
            c => c.Count == 1 && c[0].Entry.DisplayName == "Old" && c[0].Action == ChangeAction.Enable));
    }

    [Fact]
    public async Task Disabling_a_group_disables_every_enabled_item_in_it()
    {
        var b = Build([Entry("Spotify"), Entry("Deezer")]);

        await b.Vm.AllItemGroups.Single().DisableGroupCommand.ExecuteAsync(null);

        b.Apply.Received(1).Apply(Arg.Is<IReadOnlyList<PendingChange>>(c => c.Count == 2));
    }

    [Fact]
    public void Relabelling_a_row_records_a_purpose_override()
    {
        var b = Build([Entry("Spotify")]); // classified Media
        var row = b.Vm.AllItemGroups.Single().Items.Single();

        row.SelectedPurpose = Purpose.Gaming;

        b.Overrides.Received(1).Set(Arg.Is<UserOverride>(o =>
            o.Purpose == Purpose.Gaming && o.ExecutableName.Contains("Spotify", StringComparison.OrdinalIgnoreCase)));
    }

    [Fact]
    public void Ending_a_task_kills_the_running_process_and_logs_it()
    {
        var process = new ProcessInfo(4242, "Spotify", @"C:\x\Spotify.exe", 100_000_000);
        var controller = Substitute.For<IItemController>();
        controller.EndTask(Arg.Any<ProcessInfo>()).Returns(new ChangeRecord(
            "r", "4242", "Spotify", ChangeAction.EndTask, "Running", "Ended", "ProcessKill", DateTime.UtcNow, true, null));
        var b = Build([Entry("Spotify")], running: [process], controller: controller);

        var row = b.Vm.AllItemGroups.Single().Items.Single();
        Assert.True(row.CanEndTask);
        row.EndTaskCommand.Execute(null);

        controller.Received(1).EndTask(Arg.Is<ProcessInfo>(p => p.Pid == 4242));
        b.History.Received(1).Add(Arg.Any<ChangeRecord>()); // logged so it shows in History
    }

    [Fact]
    public void A_stopped_item_cannot_be_ended()
    {
        var row = Build([Entry("Spotify")]).Vm.AllItemGroups.Single().Items.Single();
        Assert.False(row.CanEndTask);
    }
}
