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
    private static MainViewModel BuildVm(params AutostartEntry[] entries)
    {
        var autostart = Substitute.For<IAutostartProvider>();
        autostart.Enumerate().Returns(entries);

        var processes = Substitute.For<IProcessProvider>();
        processes.Enumerate().Returns(Array.Empty<ProcessInfo>());

        var classifier = Substitute.For<IClassifier>();
        classifier.Classify(Arg.Any<AutostartEntry>()).Returns(
            new Classification(Purpose.Media, SafetyRating.Safe, ClassificationSource.KnowledgeBase, "Media", false)
            { TypicalRamBytes = 398458880 });

        var engine = Substitute.For<IRecommendationEngine>();
        engine.Recommend(Arg.Any<IReadOnlyList<ManageableItem>>(),
                         Arg.Any<IReadOnlyList<UsageRecord>>(), Arg.Any<DateTime>())
            .Returns(Array.Empty<Recommendation>());

        var usage = Substitute.For<IUsageRepository>();
        usage.GetAll().Returns(Array.Empty<UsageRecord>());

        var history = Substitute.For<IChangeRecordRepository>();
        history.GetRecent(Arg.Any<int>()).Returns(Array.Empty<ChangeRecord>());

        return new MainViewModel(autostart, processes, classifier, engine, usage, history,
            Substitute.For<IApplyService>(), Substitute.For<IChangeReverser>(), Substitute.For<IOverrideStore>());
    }

    [Fact]
    public void All_items_shows_a_disabled_entry_as_disabled()
    {
        var vm = BuildVm(new AutostartEntry("HKCU:Old", "Old", ItemKind.StartupApp,
            AutostartLocation.RegistryRunCurrentUser, @"C:\x\Old.exe", null, AutostartState.Disabled));

        var line = Assert.Single(vm.AllItems, l => l.Contains("Old"));
        Assert.Contains("Disabled", line);
    }

    [Fact]
    public void All_items_shows_the_ram_estimate_from_the_kb_when_not_running()
    {
        // Not running, so the live value is null; the chain falls back to the KB typical (≈380 MB).
        var vm = BuildVm(new AutostartEntry("HKCU:Big", "Big", ItemKind.StartupApp,
            AutostartLocation.RegistryRunCurrentUser, @"C:\x\Big.exe", null, AutostartState.Enabled));

        var line = Assert.Single(vm.AllItems, l => l.Contains("Big"));
        Assert.Contains("380 MB", line);
    }

    [Fact]
    public void All_items_shows_an_enabled_entry_as_enabled()
    {
        var vm = BuildVm(new AutostartEntry("HKCU:New", "New", ItemKind.StartupApp,
            AutostartLocation.RegistryRunCurrentUser, @"C:\x\New.exe", null, AutostartState.Enabled));

        var line = Assert.Single(vm.AllItems, l => l.Contains("New"));
        Assert.Contains("Enabled", line);
    }
}
