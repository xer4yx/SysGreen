using SysGreen.Core.ChangeLog;
using SysGreen.Core.Domain;
using SysGreen.Data;

namespace SysGreen.Tests;

/// <summary>
/// Exercises the repositories against a real SQLite file — the path the unit tests (which use
/// fakes) never touch. Reproduces the startup crash where Dapper could not materialize the
/// domain records from SQLite's column types.
/// </summary>
public sealed class RepositoryIntegrationTests : IDisposable
{
    private readonly string _dbPath =
        Path.Combine(Path.GetTempPath(), $"sysgreen_test_{Guid.NewGuid():n}.db");
    private readonly SqliteConnectionFactory _factory;

    public RepositoryIntegrationTests()
    {
        _factory = new SqliteConnectionFactory(_dbPath);
        new DatabaseBootstrapper(_factory).EnsureCreated();
    }

    public void Dispose()
    {
        try { if (File.Exists(_dbPath)) File.Delete(_dbPath); } catch { /* temp file, best effort */ }
    }

    [Fact]
    public void GetAll_on_empty_usage_table_returns_empty_without_throwing()
    {
        // This is exactly what crashed the app on first run.
        Assert.Empty(new UsageRepository(_factory).GetAll());
    }

    [Fact]
    public void Usage_round_trips_through_the_repository()
    {
        var repo = new UsageRepository(_factory);
        repo.RecordLaunch(@"C:\x\app.exe", new DateTime(2026, 6, 20, 8, 0, 0, DateTimeKind.Utc));

        var record = Assert.Single(repo.GetAll());
        Assert.Equal(@"C:\x\app.exe", record.ExecutablePath);
        Assert.Equal(1, record.LaunchCount);
        Assert.NotNull(record.LastLaunchUtc);
    }

    [Fact]
    public void GetRecent_on_empty_change_log_returns_empty_without_throwing()
    {
        Assert.Empty(new ChangeRecordRepository(_factory).GetRecent());
    }

    [Fact]
    public void Change_records_round_trip_through_the_repository()
    {
        var repo = new ChangeRecordRepository(_factory);
        repo.Add(new ChangeRecord("id1", "HKCU:Spotify", "Spotify", ChangeAction.Disable,
            "Enabled", "Disabled", "StartupApproved",
            new DateTime(2026, 6, 20, 9, 0, 0, DateTimeKind.Utc), true, null));

        var record = Assert.Single(repo.GetRecent());
        Assert.Equal("Spotify", record.ItemName);
        Assert.Equal(ChangeAction.Disable, record.Action);
        Assert.True(record.Success);
        Assert.Null(record.Error);
    }

    [Fact]
    public void Change_records_round_trip_their_batch_id_and_location_for_undo()
    {
        var repo = new ChangeRecordRepository(_factory);
        repo.Add(new ChangeRecord("id2", "HKLM:Updater", "Updater", ChangeAction.Disable,
            "Enabled", "Disabled", "StartupApproved",
            new DateTime(2026, 6, 22, 9, 0, 0, DateTimeKind.Utc), true, null)
            { BatchId = "batch-1", Location = AutostartLocation.RegistryRunLocalMachine });

        var record = Assert.Single(repo.GetRecent());
        Assert.Equal("batch-1", record.BatchId);
        Assert.Equal(AutostartLocation.RegistryRunLocalMachine, record.Location);
    }
}
