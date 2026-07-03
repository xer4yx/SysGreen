using SysGreen.Core.Apply;
using SysGreen.Core.ChangeLog;
using SysGreen.Core.Domain;

namespace SysGreen.Tests;

public sealed class HelperRunnerTests : IDisposable
{
    private readonly List<string> _temp = [];

    public void Dispose()
    {
        foreach (var f in _temp)
            try { if (File.Exists(f)) File.Delete(f); } catch { /* temp file, best effort */ }
    }

    private string TempPath(string suffix)
    {
        var p = Path.Combine(Path.GetTempPath(), $"sysgreen_helpertest_{Guid.NewGuid():n}{suffix}");
        _temp.Add(p);
        return p;
    }

    private static AutostartEntry Hklm(string name) =>
        new($"HKLM:{name}", name, ItemKind.StartupApp, AutostartLocation.RegistryRunLocalMachine,
            $@"C:\x\{name}.exe", null, AutostartState.Enabled);

    [Fact]
    public void Returns_NoJobFile_when_no_arguments_are_passed()
    {
        var runner = new HelperRunner(_ => new FakeApply(new ApplyResult(false, false, [])));
        Assert.Equal(HelperExitCodes.NoJobFile, runner.Run([]));
    }

    [Fact]
    public void Returns_JobFileNotFound_when_the_job_file_is_missing()
    {
        var runner = new HelperRunner(_ => new FakeApply(new ApplyResult(false, false, [])));
        Assert.Equal(HelperExitCodes.JobFileNotFound, runner.Run([TempPath(".json")]));
    }

    [Fact]
    public void Applies_the_job_changes_and_writes_the_result_file()
    {
        var jobPath = TempPath(".json");
        var resultPath = TempPath(".result.json");
        var record = new ChangeRecord("r", "HKLM:Updater", "Updater", ChangeAction.Disable,
            "Enabled", "Disabled", "StartupApproved",
            new DateTime(2026, 6, 22, 9, 0, 0, DateTimeKind.Utc), true, null);
        var apply = new FakeApply(new ApplyResult(true, true, [record]));
        var job = new ApplyJob(1, "ignored-db-path", resultPath,
            [new PendingChange(Hklm("Updater"), ChangeAction.Disable)]);
        File.WriteAllText(jobPath, ApplyJobSerializer.SerializeJob(job));

        var exit = new HelperRunner(_ => apply).Run([jobPath]);

        Assert.Equal(HelperExitCodes.Success, exit);
        Assert.Equal(1, apply.CallCount);
        Assert.True(File.Exists(resultPath));
        var written = ApplyJobSerializer.DeserializeResult(File.ReadAllText(resultPath));
        Assert.Equal(1, written.SucceededCount);
        Assert.Equal("Updater", Assert.Single(written.Records).ItemName);
    }

    [Fact]
    public void Builds_the_apply_service_for_the_job_including_its_database_and_progress_paths()
    {
        var jobPath = TempPath(".json");
        var resultPath = TempPath(".result.json");
        ApplyJob? seenJob = null;
        var job = new ApplyJob(1, @"C:\db\sysgreen.db", resultPath, [])
        { ProgressPath = @"C:\tmp\progress.json" };
        File.WriteAllText(jobPath, ApplyJobSerializer.SerializeJob(job));

        new HelperRunner(j =>
        {
            seenJob = j;
            return new FakeApply(new ApplyResult(false, false, []));
        }).Run([jobPath]);

        Assert.Equal(@"C:\db\sysgreen.db", seenJob!.DatabasePath);
        Assert.Equal(@"C:\tmp\progress.json", seenJob.ProgressPath);
    }

    [Fact]
    public void Returns_RestorePointAborted_when_the_apply_aborts()
    {
        var jobPath = TempPath(".json");
        var resultPath = TempPath(".result.json");
        var job = new ApplyJob(1, "db", resultPath,
            [new PendingChange(Hklm("Updater"), ChangeAction.Disable)]);
        File.WriteAllText(jobPath, ApplyJobSerializer.SerializeJob(job));
        var apply = new FakeApply(new ApplyResult(true, false, [])); // restore point failed → aborted

        var exit = new HelperRunner(_ => apply).Run([jobPath]);

        Assert.Equal(HelperExitCodes.RestorePointAborted, exit);
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
}
