using SysGreen.Core.Abstractions;
using SysGreen.Core.Apply;
using SysGreen.Core.ChangeLog;
using SysGreen.Core.Domain;

namespace SysGreen.Tests;

public class ApplyServiceTests
{
    private static readonly DateTime FixedNow = new(2026, 6, 20, 10, 0, 0, DateTimeKind.Utc);

    private static AutostartEntry Hkcu(string name) =>
        new($"HKCU:{name}", name, ItemKind.StartupApp, AutostartLocation.RegistryRunCurrentUser,
            $@"C:\x\{name}.exe", null, AutostartState.Enabled);

    private static AutostartEntry Hklm(string name) =>
        new($"HKLM:{name}", name, ItemKind.StartupApp, AutostartLocation.RegistryRunLocalMachine,
            $@"C:\x\{name}.exe", null, AutostartState.Enabled);

    private static ApplyService Build(
        FakeChangeLog log, FakeRestore restore, FakeController? controller = null) =>
        new(controller ?? new FakeController(), log, restore, new FixedClock(FixedNow));

    [Fact]
    public void Per_user_only_batch_does_not_create_a_restore_point()
    {
        var restore = new FakeRestore(succeeds: true);
        var result = Build(new FakeChangeLog(), restore)
            .Apply([new PendingChange(Hkcu("Spotify"), ChangeAction.Disable)]);

        Assert.Equal(0, restore.CallCount);
        Assert.False(result.RestorePointRequired);
        Assert.Equal(1, result.SucceededCount);
    }

    [Fact]
    public void Risky_batch_creates_a_restore_point_first()
    {
        var restore = new FakeRestore(succeeds: true);
        var result = Build(new FakeChangeLog(), restore)
            .Apply([new PendingChange(Hklm("Updater"), ChangeAction.Disable)]);

        Assert.Equal(1, restore.CallCount);
        Assert.True(result.RestorePointCreated);
        Assert.Equal(1, result.SucceededCount);
    }

    [Fact]
    public void Risky_batch_aborts_when_restore_point_cannot_be_created()
    {
        var log = new FakeChangeLog();
        var controller = new FakeController();
        var result = Build(log, new FakeRestore(succeeds: false), controller)
            .Apply([new PendingChange(Hklm("Updater"), ChangeAction.Disable)]);

        Assert.True(result.Aborted);
        Assert.Empty(result.Records);
        Assert.Equal(0, controller.DisableCount); // nothing was applied
        Assert.Empty(log.Records);
    }

    [Fact]
    public void Persists_a_change_record_for_each_applied_change()
    {
        var log = new FakeChangeLog();
        Build(log, new FakeRestore(succeeds: true)).Apply(
        [
            new PendingChange(Hkcu("Spotify"), ChangeAction.Disable),
            new PendingChange(Hkcu("Discord"), ChangeAction.Disable),
        ]);

        Assert.Equal(2, log.Records.Count);
    }

    [Fact]
    public void Continues_on_error_and_records_the_failure()
    {
        var log = new FakeChangeLog();
        var controller = new FakeController { FailOnItemId = "HKCU:Discord" };
        var result = Build(log, new FakeRestore(succeeds: true), controller).Apply(
        [
            new PendingChange(Hkcu("Spotify"), ChangeAction.Disable),
            new PendingChange(Hkcu("Discord"), ChangeAction.Disable),
        ]);

        Assert.Equal(1, result.SucceededCount);
        Assert.Equal(1, result.FailedCount);
        Assert.Equal(2, log.Records.Count); // both outcomes are persisted
        Assert.Contains(log.Records, r => !r.Success && r.ItemName == "Discord");
    }

    private sealed class FakeController : IItemController
    {
        public string? FailOnItemId { get; set; }
        public int DisableCount { get; private set; }

        public ChangeRecord Disable(AutostartEntry entry)
        {
            DisableCount++;
            if (entry.Id == FailOnItemId) throw new InvalidOperationException("access denied");
            return new ChangeRecord("r", entry.Id, entry.DisplayName, ChangeAction.Disable,
                "Enabled", "Disabled", "StartupApproved", FixedNow, true, null);
        }

        public ChangeRecord Enable(AutostartEntry entry) =>
            new("r", entry.Id, entry.DisplayName, ChangeAction.Enable,
                "Disabled", "Enabled", "StartupApproved", FixedNow, true, null);

        public ChangeRecord EndTask(ProcessInfo process) => throw new NotSupportedException();
    }

    private sealed class FakeChangeLog : IChangeLog
    {
        public List<ChangeRecord> Records { get; } = [];
        public void Record(ChangeRecord record) => Records.Add(record);
    }

    private sealed class FakeRestore(bool succeeds) : IRestorePointService
    {
        public int CallCount { get; private set; }
        public bool TryCreateRestorePoint(string description)
        {
            CallCount++;
            return succeeds;
        }
    }

    private sealed class FixedClock(DateTime now) : IClock
    {
        public DateTime UtcNow { get; } = now;
    }
}
