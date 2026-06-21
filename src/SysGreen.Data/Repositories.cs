using Dapper;
using SysGreen.Core.Abstractions;
using SysGreen.Core.ChangeLog;
using SysGreen.Core.Usage;

namespace SysGreen.Data;

/// <summary>Persists and queries the habit (Usage) signal.</summary>
public interface IUsageRepository
{
    IReadOnlyList<UsageRecord> GetAll();
    void RecordLaunch(string executablePath, DateTime whenUtc);
    void UpsertMany(IEnumerable<UsageRecord> records);
}

/// <summary>Persists the undo log (Change Records).</summary>
public interface IChangeRecordRepository
{
    void Add(ChangeRecord record);
    IReadOnlyList<ChangeRecord> GetRecent(int limit = 200);
}

public sealed class UsageRepository : IUsageRepository
{
    private readonly IConnectionFactory _factory;
    public UsageRepository(IConnectionFactory factory) => _factory = factory;

    public IReadOnlyList<UsageRecord> GetAll()
    {
        using var c = _factory.OpenConnection();
        return c.Query<UsageRecord>(
            "SELECT executable_path AS ExecutablePath, launch_count AS LaunchCount, " +
            "last_launch_utc AS LastLaunchUtc FROM usage;").AsList();
    }

    public void RecordLaunch(string executablePath, DateTime whenUtc)
    {
        using var c = _factory.OpenConnection();
        c.Execute(
            """
            INSERT INTO usage (executable_path, launch_count, last_launch_utc)
            VALUES (@Path, 1, @When)
            ON CONFLICT(executable_path) DO UPDATE SET
                launch_count = launch_count + 1,
                last_launch_utc = @When;
            """,
            new { Path = executablePath, When = whenUtc });
    }

    public void UpsertMany(IEnumerable<UsageRecord> records)
    {
        using var c = _factory.OpenConnection();
        using var tx = c.BeginTransaction();
        c.Execute(
            """
            INSERT INTO usage (executable_path, launch_count, last_launch_utc)
            VALUES (@ExecutablePath, @LaunchCount, @LastLaunchUtc)
            ON CONFLICT(executable_path) DO UPDATE SET
                launch_count = excluded.launch_count,
                last_launch_utc = excluded.last_launch_utc;
            """,
            records, tx);
        tx.Commit();
    }
}

public sealed class ChangeRecordRepository : IChangeRecordRepository, IChangeLog
{
    private readonly IConnectionFactory _factory;
    public ChangeRecordRepository(IConnectionFactory factory) => _factory = factory;

    /// <summary>The Core <see cref="IChangeLog"/> port — used by the Apply flow.</summary>
    public void Record(ChangeRecord record) => Add(record);

    public void Add(ChangeRecord r)
    {
        using var c = _factory.OpenConnection();
        c.Execute(
            """
            INSERT INTO change_record
                (id, item_id, item_name, action, prior_state, new_state,
                 mechanism, timestamp_utc, success, error)
            VALUES
                (@Id, @ItemId, @ItemName, @Action, @PriorState, @NewState,
                 @Mechanism, @TimestampUtc, @Success, @Error);
            """,
            new
            {
                r.Id, r.ItemId, r.ItemName, Action = r.Action.ToString(),
                r.PriorState, r.NewState, r.Mechanism, r.TimestampUtc,
                Success = r.Success ? 1 : 0, r.Error,
            });
    }

    public IReadOnlyList<ChangeRecord> GetRecent(int limit = 200)
    {
        using var c = _factory.OpenConnection();
        return c.Query<ChangeRecord>(
            """
            SELECT id AS Id, item_id AS ItemId, item_name AS ItemName, action AS Action,
                   prior_state AS PriorState, new_state AS NewState, mechanism AS Mechanism,
                   timestamp_utc AS TimestampUtc, success AS Success, error AS Error
            FROM change_record ORDER BY timestamp_utc DESC LIMIT @Limit;
            """,
            new { Limit = limit }).AsList();
    }
}
