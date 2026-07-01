using SysGreen.Core.Abstractions;
using SysGreen.Core.Apply;
using SysGreen.Core.ChangeLog;
using SysGreen.Core.Domain;

namespace SysGreen.Tests;

/// <summary>
/// Topic B (Phase 6): the Apply pipeline reports coarse phases to an injected progress sink so the
/// App can show a header progress strip. The sink is a no-op in-process and a file writer in the
/// elevated Helper (polled by the App). See the implementation plan.
/// </summary>
public class ApplyProgressTests : IDisposable
{
    private static readonly DateTime FixedNow = new(2026, 6, 20, 10, 0, 0, DateTimeKind.Utc);
    private readonly List<string> _temp = [];

    public void Dispose()
    {
        foreach (var f in _temp)
            try { if (File.Exists(f)) File.Delete(f); } catch { /* temp file, best effort */ }
    }

    private string TempPath()
    {
        var p = Path.Combine(Path.GetTempPath(), $"sysgreen_progress_{Guid.NewGuid():n}.json");
        _temp.Add(p);
        return p;
    }

    private static AutostartEntry Hkcu(string name) =>
        new($"HKCU:{name}", name, ItemKind.StartupApp, AutostartLocation.RegistryRunCurrentUser,
            $@"C:\x\{name}.exe", null, AutostartState.Enabled);

    private static AutostartEntry Hklm(string name) =>
        new($"HKLM:{name}", name, ItemKind.StartupApp, AutostartLocation.RegistryRunLocalMachine,
            $@"C:\x\{name}.exe", null, AutostartState.Enabled);

    private static ApplyService Build(IApplyProgressSink sink) =>
        new(new PassthroughController(), new NullChangeLog(),
            new AlwaysRestore(), new FixedClock(FixedNow), sink);

    [Fact]
    public void A_risky_batch_reports_restore_point_then_each_item_then_done()
    {
        var sink = new CapturingSink();
        Build(sink).Apply(
        [
            new PendingChange(Hklm("Updater"), ChangeAction.Disable),
            new PendingChange(Hklm("Sync"), ChangeAction.Disable),
        ]);

        Assert.Equal(
        [
            new ApplyProgress(ApplyStage.CreatingRestorePoint, 0, 2),
            new ApplyProgress(ApplyStage.Applying, 1, 2),
            new ApplyProgress(ApplyStage.Applying, 2, 2),
            new ApplyProgress(ApplyStage.Done, 2, 2),
        ], sink.Reports);
    }

    [Fact]
    public void A_per_user_batch_reports_items_and_done_but_no_restore_point()
    {
        var sink = new CapturingSink();
        Build(sink).Apply([new PendingChange(Hkcu("Spotify"), ChangeAction.Disable)]);

        Assert.Equal(
        [
            new ApplyProgress(ApplyStage.Applying, 1, 1),
            new ApplyProgress(ApplyStage.Done, 1, 1),
        ], sink.Reports);
    }

    [Fact]
    public void File_sink_writes_a_phase_that_the_reader_reads_back()
    {
        var path = TempPath();
        new FileApplyProgressSink(path).Report(new ApplyProgress(ApplyStage.Applying, 2, 5));

        Assert.Equal(new ApplyProgress(ApplyStage.Applying, 2, 5), ApplyProgressFile.TryRead(path));
    }

    [Fact]
    public void Reader_returns_null_when_the_progress_file_is_absent()
    {
        Assert.Null(ApplyProgressFile.TryRead(TempPath())); // never written
    }

    private sealed class CapturingSink : IApplyProgressSink
    {
        public List<ApplyProgress> Reports { get; } = [];
        public void Report(ApplyProgress progress) => Reports.Add(progress);
    }

    private sealed class PassthroughController : IItemController
    {
        public ChangeRecord Disable(AutostartEntry entry) =>
            new("r", entry.Id, entry.DisplayName, ChangeAction.Disable,
                "Enabled", "Disabled", "StartupApproved", FixedNow, true, null);
        public ChangeRecord Enable(AutostartEntry entry) =>
            new("r", entry.Id, entry.DisplayName, ChangeAction.Enable,
                "Disabled", "Enabled", "StartupApproved", FixedNow, true, null);
        public ChangeRecord EndTask(ProcessInfo process) => throw new NotSupportedException();
    }

    private sealed class NullChangeLog : IChangeLog
    {
        public void Record(ChangeRecord record) { }
    }

    private sealed class AlwaysRestore : IRestorePointService
    {
        public bool TryCreateRestorePoint(string description) => true;
    }

    private sealed class FixedClock(DateTime now) : IClock
    {
        public DateTime UtcNow { get; } = now;
    }
}
