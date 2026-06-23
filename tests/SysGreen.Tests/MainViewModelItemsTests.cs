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

    private static (MainViewModel vm, IApplyService apply, IOverrideStore overrides) Build(
        Func<AutostartEntry, Classification>? classify, params AutostartEntry[] entries)
    {
        var autostart = Substitute.For<IAutostartProvider>();
        autostart.Enumerate().Returns(entries);
        var processes = Substitute.For<IProcessProvider>();
        processes.Enumerate().Returns(Array.Empty<ProcessInfo>());

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

        var vm = new MainViewModel(autostart, processes, classifier, engine, usage, history, apply,
            Substitute.For<IChangeReverser>(), overrides);
        return (vm, apply, overrides);
    }

    [Fact]
    public void All_items_are_grouped_by_purpose()
    {
        var (vm, _, _) = Build(
            e => Class(e.DisplayName == "Game" ? Purpose.Gaming : Purpose.Media),
            Entry("Game"), Entry("Music"), Entry("Tunes"));

        Assert.Equal(2, vm.AllItemGroups.Count);
        Assert.Equal(new[] { Purpose.Gaming, Purpose.Media }, vm.AllItemGroups.Select(g => g.Purpose));
        Assert.Single(vm.AllItemGroups.First(g => g.Purpose == Purpose.Gaming).Items);
        Assert.Equal(2, vm.AllItemGroups.First(g => g.Purpose == Purpose.Media).Items.Count);
    }

    [Fact]
    public void A_row_shows_state_and_ram_and_can_be_disabled()
    {
        var (vm, _, _) = Build(null, Entry("Spotify"));
        var row = vm.AllItemGroups.Single().Items.Single();

        Assert.Contains("380 MB", row.DisplayText);
        Assert.Contains("Enabled", row.DisplayText);
        Assert.True(row.CanDisable);
    }

    [Fact]
    public void A_disabled_row_cannot_be_disabled_again()
    {
        var (vm, _, _) = Build(null, Entry("Old", AutostartState.Disabled));
        Assert.False(vm.AllItemGroups.Single().Items.Single().CanDisable);
    }

    [Fact]
    public void Disabling_a_row_applies_a_disable_for_that_item()
    {
        var (vm, apply, _) = Build(null, Entry("Spotify"));

        vm.AllItemGroups.Single().Items.Single().DisableCommand.Execute(null);

        apply.Received(1).Apply(Arg.Is<IReadOnlyList<PendingChange>>(
            c => c.Count == 1 && c[0].Entry.DisplayName == "Spotify" && c[0].Action == ChangeAction.Disable));
    }

    [Fact]
    public void Disabling_a_group_disables_every_enabled_item_in_it()
    {
        var (vm, apply, _) = Build(null, Entry("Spotify"), Entry("Deezer"));

        vm.AllItemGroups.Single().DisableGroupCommand.Execute(null);

        apply.Received(1).Apply(Arg.Is<IReadOnlyList<PendingChange>>(c => c.Count == 2));
    }

    [Fact]
    public void Relabelling_a_row_records_a_purpose_override()
    {
        var (vm, _, overrides) = Build(null, Entry("Spotify")); // classified Media
        var row = vm.AllItemGroups.Single().Items.Single();

        row.SelectedPurpose = Purpose.Gaming;

        overrides.Received(1).Set(Arg.Is<UserOverride>(o =>
            o.Purpose == Purpose.Gaming && o.ExecutableName.Contains("Spotify", StringComparison.OrdinalIgnoreCase)));
    }
}
