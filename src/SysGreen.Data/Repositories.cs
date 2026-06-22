using Dapper;
using SysGreen.Core.Abstractions;
using SysGreen.Core.ChangeLog;
using SysGreen.Core.Domain;
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
        // Mapped explicitly: SQLite returns INTEGER as Int64 and TEXT as String, which don't
        // match the record's (string, int, DateTime?) constructor for Dapper auto-materialization.
        using var c = _factory.OpenConnection();
        using var cmd = c.CreateCommand();
        cmd.CommandText = "SELECT executable_path, launch_count, last_launch_utc FROM usage;";
        using var reader = cmd.ExecuteReader();

        var records = new List<UsageRecord>();
        while (reader.Read())
        {
            records.Add(new UsageRecord(
                reader.GetString(0),
                (int)reader.GetInt64(1),
                reader.IsDBNull(2) ? null : reader.GetDateTime(2)));
        }
        return records;
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
                 mechanism, timestamp_utc, success, error, batch_id, location, mechanism_key)
            VALUES
                (@Id, @ItemId, @ItemName, @Action, @PriorState, @NewState,
                 @Mechanism, @TimestampUtc, @Success, @Error, @BatchId, @Location, @MechanismKey);
            """,
            new
            {
                r.Id, r.ItemId, r.ItemName, Action = r.Action.ToString(),
                r.PriorState, r.NewState, r.Mechanism, r.TimestampUtc,
                Success = r.Success ? 1 : 0, r.Error,
                r.BatchId, Location = r.Location.ToString(), r.MechanismKey,
            });
    }

    public IReadOnlyList<ChangeRecord> GetRecent(int limit = 200)
    {
        using var c = _factory.OpenConnection();
        using var cmd = c.CreateCommand();
        cmd.CommandText =
            """
            SELECT id, item_id, item_name, action, prior_state, new_state, mechanism,
                   timestamp_utc, success, error, batch_id, location, mechanism_key
            FROM change_record ORDER BY timestamp_utc DESC LIMIT $limit;
            """;
        var limitParam = cmd.CreateParameter();
        limitParam.ParameterName = "$limit";
        limitParam.Value = limit;
        cmd.Parameters.Add(limitParam);
        using var reader = cmd.ExecuteReader();

        var records = new List<ChangeRecord>();
        while (reader.Read())
        {
            records.Add(new ChangeRecord(
                reader.GetString(0), reader.GetString(1), reader.GetString(2),
                Enum.Parse<ChangeAction>(reader.GetString(3)),
                reader.GetString(4), reader.GetString(5), reader.GetString(6),
                reader.GetDateTime(7),
                reader.GetInt64(8) != 0,
                reader.IsDBNull(9) ? null : reader.GetString(9))
            {
                BatchId = reader.IsDBNull(10) ? "" : reader.GetString(10),
                Location = reader.IsDBNull(11)
                    ? AutostartLocation.Unknown
                    : Enum.Parse<AutostartLocation>(reader.GetString(11)),
                MechanismKey = reader.IsDBNull(12) ? "" : reader.GetString(12),
            });
        }
        return records;
    }
}
