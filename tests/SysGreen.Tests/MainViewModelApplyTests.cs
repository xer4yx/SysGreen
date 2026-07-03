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

public class MainViewModelApplyTests
{
    private static AutostartEntry SpotifyEntry() =>
        new("HKCU:Spotify", "Spotify", ItemKind.StartupApp, AutostartLocation.RegistryRunCurrentUser,
            @"C:\x\Spotify.exe", null, AutostartState.Enabled);

    /// <summary>A view-model wired to fakes that surface one recommendation for the Spotify entry.</summary>
    private static MainViewModel BuildVm(IApplyService apply, IOverrideStore? overrides = null)
    {
        var autostart = Substitute.For<IAutostartProvider>();
        autostart.Enumerate().Returns(new[] { SpotifyEntry() });

        var processes = Substitute.For<IProcessProvider>();
        processes.Enumerate().Returns(Array.Empty<ProcessInfo>());

        var classifier = Substitute.For<IClassifier>();
        classifier.Classify(Arg.Any<AutostartEntry>()).Returns(
            new Classification(Purpose.Media, SafetyRating.Safe, ClassificationSource.KnowledgeBase, "Media", false));

        var engine = Substitute.For<IRecommendationEngine>();
        engine.Recommend(Arg.Any<IReadOnlyList<ManageableItem>>(),
                         Arg.Any<IReadOnlyList<UsageRecord>>(), Arg.Any<DateTime>())
            .Returns(ci =>
            {
                var items = (IReadOnlyList<ManageableItem>)ci[0];
                return items.Select(i =>
                    new Recommendation(i, RecommendationSource.Static, "not used in 47 days", 1.0)).ToList();
            });

        var usage = Substitute.For<IUsageRepository>();
        usage.GetAll().Returns(Array.Empty<UsageRecord>());

        var history = Substitute.For<IChangeRecordRepository>();
        history.GetRecent(Arg.Any<int>()).Returns(Array.Empty<ChangeRecord>());

        return new MainViewModel(autostart, processes, classifier, engine, usage, history, apply,
            Substitute.For<IChangeReverser>(), overrides ?? Substitute.For<IOverrideStore>(),
            Substitute.For<IItemController>());
    }

    [Fact]
    public async Task Applying_a_selected_recommendation_disables_it_via_the_apply_service()
    {
        var apply = Substitute.For<IApplyService>();
        apply.Apply(Arg.Any<IReadOnlyList<PendingChange>>())
            .Returns(new ApplyResult(false, false, Array.Empty<ChangeRecord>()));
        var vm = BuildVm(apply);

        Assert.Single(vm.Recommendations);
        vm.Recommendations[0].IsSelected = true;
        await vm.ApplyCommand.ExecuteAsync(null);

        apply.Received(1).Apply(Arg.Is<IReadOnlyList<PendingChange>>(
            c => c.Count == 1 && c[0].Action == ChangeAction.Disable && c[0].Entry.DisplayName == "Spotify"));
    }

    [Fact]
    public void Never_recommend_records_a_never_recommend_override_for_the_item()
    {
        var apply = Substitute.For<IApplyService>();
        var overrides = Substitute.For<IOverrideStore>();
        var vm = BuildVm(apply, overrides);

        vm.Recommendations[0].NeverRecommendCommand.Execute(null);

        overrides.Received(1).Set(Arg.Is<UserOverride>(o =>
            o.NeverRecommend && o.ExecutableName.Contains("Spotify", StringComparison.OrdinalIgnoreCase)));
    }

    [Fact]
    public async Task Applying_with_nothing_selected_does_not_touch_the_apply_service()
    {
        var apply = Substitute.For<IApplyService>();
        apply.Apply(Arg.Any<IReadOnlyList<PendingChange>>())
            .Returns(new ApplyResult(false, false, Array.Empty<ChangeRecord>()));
        var vm = BuildVm(apply);

        vm.Recommendations[0].IsSelected = false;
        await vm.ApplyCommand.ExecuteAsync(null);

        apply.DidNotReceive().Apply(Arg.Any<IReadOnlyList<PendingChange>>());
    }
}
