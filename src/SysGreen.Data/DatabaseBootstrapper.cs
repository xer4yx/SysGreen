using Dapper;

namespace SysGreen.Data;

/// <summary>Creates the SQLite schema on first run. Idempotent.</summary>
public sealed class DatabaseBootstrapper
{
    private readonly IConnectionFactory _factory;

    public DatabaseBootstrapper(IConnectionFactory factory) => _factory = factory;

    public void EnsureCreated()
    {
        using var connection = _factory.OpenConnection();
        connection.Execute(Schema);
    }

    private const string Schema = """
        CREATE TABLE IF NOT EXISTS usage (
            executable_path TEXT PRIMARY KEY,
            launch_count    INTEGER NOT NULL DEFAULT 0,
            last_launch_utc TEXT
        );

        CREATE TABLE IF NOT EXISTS change_record (
            id            TEXT PRIMARY KEY,
            item_id       TEXT NOT NULL,
            item_name     TEXT NOT NULL,
            action        TEXT NOT NULL,
            prior_state   TEXT NOT NULL,
            new_state     TEXT NOT NULL,
            mechanism     TEXT NOT NULL,
            timestamp_utc TEXT NOT NULL,
            success       INTEGER NOT NULL,
            error         TEXT
        );

        CREATE INDEX IF NOT EXISTS ix_change_record_time
            ON change_record (timestamp_utc DESC);

        -- User overrides take precedence over the Knowledge Base (CONTEXT.md "Override").
        CREATE TABLE IF NOT EXISTS override (
            executable_path        TEXT PRIMARY KEY,
            purpose                TEXT,
            never_recommend        INTEGER NOT NULL DEFAULT 0
        );
        """;
}
