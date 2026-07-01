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
/// Topic C (Phase 7): completed-action feedback is delivered as closable in-app toasts — success vs
/// error differentiated — instead of inline status text. The header strip owns in-progress; the toast
/// owns the finished-action outcome.
/// </summary>
public class MainViewModelToastTests
{
    private static AutostartEntry Entry(string name, AutostartState state = AutostartState.Enabled) =>
        new($"HKCU:{name}", name, ItemKind.StartupApp, AutostartLocation.RegistryRunCurrentUser,
            $@"C:\x\{name}.exe", null, state);

    private static ChangeRecord Rec(string name, bool success) =>
        new("r", $"HKCU:{name}", name, ChangeAction.Disable, "Enabled", "Disabled", "StartupApproved",
            DateTime.UtcNow, success, success ? null : "denied");

    private sealed record Built(MainViewModel Vm, CapturingToasts Toasts);

    private static Built Build(
        ApplyResult applyResult, AutostartEntry[]? entries = null, ApplyResult? reverseResult = null)
    {
        entries ??= [Entry("Spotify")];
        var autostart = Substitute.For<IAutostartProvider>();
        autostart.Enumerate().Returns(entries);
        var processes = Substitute.For<IProcessProvider>();
        processes.Enumerate().Returns(Array.Empty<ProcessInfo>());
        var classifier = Substitute.For<IClassifier>();
        classifier.Classify(Arg.Any<AutostartEntry>()).Returns(
            new Classification(Purpose.Media, SafetyRating.Safe, ClassificationSource.KnowledgeBase, "Media", false));
        var engine = Substitute.For<IRecommendationEngine>();
        engine.Recommend(Arg.Any<IReadOnlyList<ManageableItem>>(),
                         Arg.Any<IReadOnlyList<UsageRecord>>(), Arg.Any<DateTime>())
            .Returns(ci => ((IReadOnlyList<ManageableItem>)ci[0])
                .Select(i => new Recommendation(i, RecommendationSource.Static, "unused", 1.0)).ToList());
        var usage = Substitute.For<IUsageRepository>();
        usage.GetAll().Returns(Array.Empty<UsageRecord>());
        var history = Substitute.For<IChangeRecordRepository>();
        history.GetRecent(Arg.Any<int>()).Returns(Array.Empty<ChangeRecord>());
        var apply = Substitute.For<IApplyService>();
        apply.Apply(Arg.Any<IReadOnlyList<PendingChange>>()).Returns(applyResult);
        var reverser = Substitute.For<IChangeReverser>();
        reverser.Reverse(Arg.Any<IReadOnlyList<ChangeRecord>>())
            .Returns(reverseResult ?? new ApplyResult(false, false, Array.Empty<ChangeRecord>()));
        var toasts = new CapturingToasts();
        var vm = new MainViewModel(autostart, processes, classifier, engine, usage, history, apply,
            reverser, Substitute.For<IOverrideStore>(), Substitute.For<IItemController>(),
            updateService: null, progress: null, toasts: toasts);
        return new Built(vm, toasts);
    }

    [Fact]
    public async Task A_successful_disable_is_announced_as_a_success_toast()
    {
        var b = Build(new ApplyResult(false, false, [Rec("Spotify", success: true)]));
        b.Vm.Recommendations[0].IsSelected = true;

        await b.Vm.ApplyCommand.ExecuteAsync(null);

        Assert.Equal("Disabled 1 of 1.", Assert.Single(b.Toasts.Successes));
        Assert.Empty(b.Toasts.Errors);
    }

    [Fact]
    public async Task A_declined_elevation_is_announced_as_an_error_toast()
    {
        var b = Build(new ApplyResult(false, false, Array.Empty<ChangeRecord>()) { ElevationDeclined = true });
        b.Vm.Recommendations[0].IsSelected = true;

        await b.Vm.ApplyCommand.ExecuteAsync(null);

        Assert.Contains("declined", Assert.Single(b.Toasts.Errors), StringComparison.OrdinalIgnoreCase);
        Assert.Empty(b.Toasts.Successes);
    }

    [Fact]
    public async Task A_partial_failure_is_announced_as_an_error_toast()
    {
        var b = Build(new ApplyResult(false, false, [Rec("Spotify", true), Rec("Deezer", false)]),
            entries: [Entry("Spotify"), Entry("Deezer")]);

        await b.Vm.AllItemGroups.Single().DisableGroupCommand.ExecuteAsync(null);

        Assert.Contains("failed", Assert.Single(b.Toasts.Errors), StringComparison.OrdinalIgnoreCase);
        Assert.Empty(b.Toasts.Successes);
    }

    [Fact]
    public async Task An_undo_is_announced_as_a_success_toast()
    {
        var b = Build(new ApplyResult(false, false, Array.Empty<ChangeRecord>()),
            reverseResult: new ApplyResult(false, false, [Rec("Spotify", true)]));

        await b.Vm.ReverseChangesAsync([Rec("Spotify", true)]);

        Assert.Equal("Reversed 1 change.", Assert.Single(b.Toasts.Successes));
        Assert.Empty(b.Toasts.Errors);
    }
}

/// <summary>A capturing <see cref="IToastService"/> test double, shared across the view-model tests.</summary>
internal sealed class CapturingToasts : IToastService
{
    public List<string> Successes { get; } = [];
    public List<string> Errors { get; } = [];
    public void ShowSuccess(string message) => Successes.Add(message);
    public void ShowError(string message) => Errors.Add(message);
}
