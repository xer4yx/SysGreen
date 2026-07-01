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
/// Topic B (Phase 6): Apply runs off the UI thread behind a global busy gate — the shell stays
/// responsive, only one mutation runs at a time, and the header strip shows the current phase.
/// </summary>
public class MainViewModelBusyTests
{
    private static AutostartEntry Entry(string name, AutostartState state = AutostartState.Enabled) =>
        new($"HKCU:{name}", name, ItemKind.StartupApp, AutostartLocation.RegistryRunCurrentUser,
            $@"C:\x\{name}.exe", null, state);

    private static MainViewModel BuildVm(
        IApplyService apply, AutostartEntry[]? entries = null, ApplyProgressRelay? relay = null)
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
        return new MainViewModel(autostart, processes, classifier, engine, usage, history, apply,
            Substitute.For<IChangeReverser>(), Substitute.For<IOverrideStore>(),
            Substitute.For<IItemController>(), updateService: null, progress: relay);
    }

    [Fact]
    public async Task An_apply_runs_off_the_calling_thread_and_flags_busy_until_it_finishes()
    {
        var apply = new BlockingApply();
        var vm = BuildVm(apply);
        vm.Recommendations[0].IsSelected = true;

        var running = vm.ApplyCommand.ExecuteAsync(null); // offloaded to Task.Run — returns immediately

        // Busy is raised synchronously (before the Task.Run await), so it's already set here even
        // though the still-blocked apply may not yet have been scheduled onto a pool thread.
        Assert.True(vm.Busy.IsApplying);
        Assert.False(running.IsCompleted); // still applying — the call did not block

        apply.Release();
        await running;

        Assert.False(vm.Busy.IsApplying);
    }

    [Fact]
    public async Task While_applying_every_other_mutating_command_is_disabled()
    {
        var apply = new BlockingApply();
        var vm = BuildVm(apply, [Entry("Spotify"), Entry("Discord")]);
        var group = vm.AllItemGroups.Single();

        Assert.True(group.Items.First().DisableCommand.CanExecute(null)); // enabled at rest

        var running = group.Items.First().DisableCommand.ExecuteAsync(null);

        // Busy is raised synchronously inside ExecuteAsync (before the Task.Run await).
        Assert.True(vm.Busy.IsApplying);
        Assert.False(vm.ApplyCommand.CanExecute(null));                       // Recommendations Apply gated
        Assert.False(group.Items.Last().DisableCommand.CanExecute(null));     // a sibling row gated
        Assert.False(group.DisableGroupCommand.CanExecute(null));             // the group action gated

        apply.Release();
        await running;

        // Once the apply + refresh finish, mutations are live again.
        Assert.False(vm.Busy.IsApplying);
        Assert.True(vm.AllItemGroups.Single().Items.First().DisableCommand.CanExecute(null));
    }

    [Fact]
    public void Progress_reported_by_the_elevated_helper_updates_the_header_strip_text()
    {
        var relay = new ApplyProgressRelay();
        var vm = BuildVm(Substitute.For<IApplyService>(), relay: relay);

        relay.Report(new ApplyProgress(ApplyStage.Applying, 1, 2));

        Assert.Equal("Applying 1 of 2…", vm.Busy.ProgressPhase);
    }

    [Fact]
    public void The_header_strip_describes_each_apply_phase()
    {
        Assert.Equal("Creating a restore point…",
            ApplyProgressText.Describe(new ApplyProgress(ApplyStage.CreatingRestorePoint, 0, 3)));
        Assert.Equal("Applying 2 of 3…",
            ApplyProgressText.Describe(new ApplyProgress(ApplyStage.Applying, 2, 3)));
        Assert.Equal("Finishing…",
            ApplyProgressText.Describe(new ApplyProgress(ApplyStage.Done, 3, 3)));
    }

    /// <summary>An <see cref="IApplyService"/> that blocks inside Apply until released, so a test can
    /// observe the busy state while a "slow" apply is mid-flight. Releasing before the apply is even
    /// scheduled is safe — the gate stays signaled, so the later Wait returns at once.</summary>
    private sealed class BlockingApply : IApplyService
    {
        private readonly ManualResetEventSlim _gate = new(false);
        public void Release() => _gate.Set();
        public ApplyResult Apply(IReadOnlyList<PendingChange> changes)
        {
            _gate.Wait();
            return new ApplyResult(false, false, Array.Empty<ChangeRecord>());
        }
    }
}
