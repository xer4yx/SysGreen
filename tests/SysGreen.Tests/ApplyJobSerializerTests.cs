using SysGreen.Core.Apply;
using SysGreen.Core.ChangeLog;
using SysGreen.Core.Domain;

namespace SysGreen.Tests;

public class ApplyJobSerializerTests
{
    private static AutostartEntry Hklm(string name) =>
        new($"HKLM:{name}", name, ItemKind.StartupApp, AutostartLocation.RegistryRunLocalMachine,
            $@"C:\x\{name}.exe", "Acme Corp", AutostartState.Enabled)
        { TargetExecutable = "Real.exe" };

    [Fact]
    public void Job_round_trips_through_serialization()
    {
        var job = new ApplyJob(1, @"C:\db\sysgreen.db", @"C:\tmp\result.json",
            [new PendingChange(Hklm("Updater"), ChangeAction.Disable)]);

        var restored = ApplyJobSerializer.DeserializeJob(ApplyJobSerializer.SerializeJob(job));

        Assert.Equal(1, restored.Version);
        Assert.Equal(job.DatabasePath, restored.DatabasePath);
        Assert.Equal(job.ResultPath, restored.ResultPath);
        var change = Assert.Single(restored.Changes);
        Assert.Equal(ChangeAction.Disable, change.Action);
        Assert.Equal("HKLM:Updater", change.Entry.Id);
        Assert.Equal(AutostartLocation.RegistryRunLocalMachine, change.Entry.Location);
        Assert.Equal("Real.exe", change.Entry.TargetExecutable);
        Assert.True(change.Entry.RequiresElevation);
    }

    [Fact]
    public void Enums_serialize_as_strings_so_the_job_file_is_human_readable()
    {
        var job = new ApplyJob(1, "db", "result",
            [new PendingChange(Hklm("Updater"), ChangeAction.Disable)]);

        var json = ApplyJobSerializer.SerializeJob(job);

        Assert.Contains("\"Disable\"", json);
        Assert.Contains("\"RegistryRunLocalMachine\"", json);
    }

    [Fact]
    public void Result_round_trips_through_serialization()
    {
        var record = new ChangeRecord("id1", "HKLM:Updater", "Updater", ChangeAction.Disable,
            "Enabled", "Disabled", "StartupApproved",
            new DateTime(2026, 6, 22, 9, 0, 0, DateTimeKind.Utc), true, null);
        var result = new ApplyResult(true, true, [record]);

        var restored = ApplyJobSerializer.DeserializeResult(ApplyJobSerializer.SerializeResult(result));

        Assert.True(restored.RestorePointRequired);
        Assert.True(restored.RestorePointCreated);
        Assert.False(restored.Aborted);
        Assert.Equal(1, restored.SucceededCount);
        var r = Assert.Single(restored.Records);
        Assert.Equal("Updater", r.ItemName);
        Assert.Equal(ChangeAction.Disable, r.Action);
        Assert.True(r.Success);
    }
}
