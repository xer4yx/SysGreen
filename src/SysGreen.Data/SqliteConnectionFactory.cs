using Microsoft.Data.Sqlite;

namespace SysGreen.Data;

/// <summary>Opens connections to the local SysGreen SQLite database (ADR-0006).</summary>
public interface IConnectionFactory
{
    SqliteConnection OpenConnection();
}

public sealed class SqliteConnectionFactory : IConnectionFactory
{
    private readonly string _connectionString;

    public SqliteConnectionFactory(string databasePath)
    {
        _connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = databasePath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Cache = SqliteCacheMode.Shared,
        }.ToString();
    }

    public SqliteConnection OpenConnection()
    {
        var connection = new SqliteConnection(_connectionString);
        connection.Open();
        return connection;
    }

    /// <summary>
    /// The default per-user database location: %LocalAppData%\xer4yx\SysGreen\sysgreen.db.
    /// Deliberately a sibling of — never inside — Velopack's %LocalAppData%\SysGreen install root,
    /// which the uninstaller deletes; keeping the Data Store outside it lets history survive an
    /// update or uninstall (ADR-0016).
    /// </summary>
    public static string DefaultDatabasePath()
    {
        var dir = DefaultDataDirectory();
        Directory.CreateDirectory(dir);
        return Path.Combine(dir, "sysgreen.db");
    }

    /// <summary>
    /// The Data Store directory (%LocalAppData%\xer4yx\SysGreen), outside the install root (ADR-0016).
    /// Exposed so the one-time relocation migration can resolve old and new locations.
    /// </summary>
    public static string DefaultDataDirectory() => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "xer4yx", "SysGreen");
}
