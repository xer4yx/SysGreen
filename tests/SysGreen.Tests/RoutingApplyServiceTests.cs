using SysGreen.Core.Apply;
using SysGreen.Core.ChangeLog;
using SysGreen.Core.Domain;

namespace SysGreen.Tests;

public class RoutingApplyServiceTests
{
    private static AutostartEntry Hkcu(string name) =>
        new($"HKCU:{name}", name, ItemKind.StartupApp, AutostartLocation.RegistryRunCurrentUser,
            $@"C:\x\{name}.exe", null, AutostartState.Enabled);

    private static AutostartEntry Hklm(string name) =>
        new($"HKLM:{name}", name, ItemKind.StartupApp, AutostartLocation.RegistryRunLocalMachine,
            $@"C:\x\{name}.exe", null, AutostartState.Enabled);

    [Fact]
    public void Per_user_only_batch_is_applied_in_process_and_never_touches_the_helper()
    {
        var inProcess = new FakeApply(new ApplyResult(false, false, []));
        var elevated = new FakeElevated();
        var router = new RoutingApplyService(inProcess, elevated);

        router.Apply([new PendingChange(Hkcu("Spotify"), ChangeAction.Disable)]);

        Assert.Equal(1, inProcess.CallCount);
        Assert.Equal(0, elevated.CallCount);
    }

    [Fact]
    public void Batch_with_an_admin_item_is_delegated_whole_to_the_helper_not_applied_in_process()
    {
        var inProcess = new FakeApply(new ApplyResult(false, false, []));
        var elevated = new FakeElevated();
        var router = new RoutingApplyService(inProcess, elevated);

        router.Apply(
        [
            new PendingChange(Hkcu("Spotify"), ChangeAction.Disable),
            new PendingChange(Hklm("Updater"), ChangeAction.Disable),
        ]);

        Assert.Equal(0, inProcess.CallCount);
        Assert.Equal(1, elevated.CallCount);
        // The whole batch (both items) crosses to the Helper under one elevation.
        Assert.Equal(2, elevated.LastBatch!.Count);
    }

    [Fact]
    public void Returns_the_helper_result_unchanged_for_an_elevated_batch()
    {
        var helperResult = new ApplyResult(true, true,
            [new ChangeRecord("r", "HKLM:Updater", "Updater", ChangeAction.Disable,
                "Enabled", "Disabled", "StartupApproved", DateTime.UtcNow, true, null)]);
        var router = new RoutingApplyService(
            new FakeApply(new ApplyResult(false, false, [])),
            new FakeElevated(helperResult));

        var result = router.Apply([new PendingChange(Hklm("Updater"), ChangeAction.Disable)]);

        Assert.Same(helperResult, result);
    }

    private sealed class FakeApply(ApplyResult result) : IApplyService
    {
        public int CallCount { get; private set; }
        public ApplyResult Apply(IReadOnlyList<PendingChange> changes)
        {
            CallCount++;
            return result;
        }
    }

    private sealed class FakeElevated(ApplyResult? result = null) : IElevatedApplyClient
    {
        public int CallCount { get; private set; }
        public IReadOnlyList<PendingChange>? LastBatch { get; private set; }
        public ApplyResult Apply(IReadOnlyList<PendingChange> changes)
        {
            CallCount++;
            LastBatch = changes;
            return result ?? new ApplyResult(true, true, []);
        }
    }
}
