using SysGreen.Core.ChangeLog;
using SysGreen.Core.Domain;
using SysGreen.Core.Knowledge;
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
            { BatchId = "batch-1", Location = AutostartLocation.RegistryRunLocalMachine, MechanismKey = @"\Updater" });

        var record = Assert.Single(repo.GetRecent());
        Assert.Equal("batch-1", record.BatchId);
        Assert.Equal(AutostartLocation.RegistryRunLocalMachine, record.Location);
        Assert.Equal(@"\Updater", record.MechanismKey);
    }

    [Fact]
    public void Overrides_round_trip_and_persist_across_instances()
    {
        new OverrideRepository(_factory).Set(new UserOverride("Spotify.exe", Purpose.Media, NeverRecommend: true));

        // A fresh repo reloads from the DB; the key matches case-insensitively without the extension.
        var ov = new OverrideRepository(_factory).Get("spotify");
        Assert.NotNull(ov);
        Assert.Equal(Purpose.Media, ov!.Purpose);
        Assert.True(ov.NeverRecommend);

        new OverrideRepository(_factory).Remove("SPOTIFY.exe");
        Assert.Null(new OverrideRepository(_factory).Get("Spotify.exe"));
    }

    [Fact]
    public void First_run_starts_incomplete_and_can_be_marked_complete()
    {
        var settings = new SettingsRepository(_factory);
        Assert.False(settings.FirstRunComplete); // a fresh install hasn't onboarded yet

        settings.MarkFirstRunComplete();

        Assert.True(new SettingsRepository(_factory).FirstRunComplete); // persists across instances
    }

    [Fact]
    public void Launch_tracking_defaults_on_and_the_off_switch_persists()
    {
        var settings = new SettingsRepository(_factory);
        Assert.True(settings.LaunchTrackingEnabled); // on by default (ADR-0012)

        settings.SetLaunchTrackingEnabled(false);

        Assert.False(new SettingsRepository(_factory).LaunchTrackingEnabled); // persists across instances
    }

    [Fact]
    public void Keep_data_on_uninstall_defaults_on_and_the_choice_persists()
    {
        var settings = new SettingsRepository(_factory);
        Assert.True(settings.KeepDataOnUninstall); // keep by default (ADR-0017)

        settings.SetKeepDataOnUninstall(false);

        Assert.False(new SettingsRepository(_factory).KeepDataOnUninstall); // persists across instances
    }

    [Fact]
    public void Accepted_policy_version_defaults_to_zero_and_persists()
    {
        var settings = new SettingsRepository(_factory);
        Assert.Equal(0, settings.AcceptedPolicyVersion); // nothing accepted yet (ADR-0018)

        settings.SetAcceptedPolicyVersion(2);

        Assert.Equal(2, new SettingsRepository(_factory).AcceptedPolicyVersion); // persists across instances
    }

    [Fact]
    public void Abandoned_threshold_defaults_to_30_days_and_persists()
    {
        var settings = new SettingsRepository(_factory);
        Assert.Equal(30, settings.AbandonedThresholdDays); // CONTEXT.md default

        settings.SetAbandonedThresholdDays(45);

        Assert.Equal(45, new SettingsRepository(_factory).AbandonedThresholdDays); // persists across instances
    }

    [Fact]
    public void Reset_clears_every_user_data_table()
    {
        new UsageRepository(_factory).RecordLaunch(@"C:\x\app.exe", DateTime.UtcNow);
        new ChangeRecordRepository(_factory).Add(new ChangeRecord(
            "id", "i", "App", ChangeAction.Disable, "Enabled", "Disabled", "m", DateTime.UtcNow, true, null));
        new OverrideRepository(_factory).Set(new UserOverride("app.exe", Purpose.Media, NeverRecommend: true));
        new SettingsRepository(_factory).MarkFirstRunComplete();

        new DataStoreReset(_factory).Reset();

        Assert.Empty(new UsageRepository(_factory).GetAll());
        Assert.Empty(new ChangeRecordRepository(_factory).GetRecent());
        Assert.Null(new OverrideRepository(_factory).Get("app.exe"));
        Assert.False(new SettingsRepository(_factory).FirstRunComplete); // settings wiped → onboarding again
    }

    [Fact]
    public void An_override_with_no_purpose_round_trips_as_null()
    {
        new OverrideRepository(_factory).Set(new UserOverride("foo.exe", Purpose: null, NeverRecommend: true));

        var ov = new OverrideRepository(_factory).Get("foo.exe");
        Assert.NotNull(ov);
        Assert.Null(ov!.Purpose);
        Assert.True(ov.NeverRecommend);
    }
}
