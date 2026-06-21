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

    /// <summary>The default per-user database location: %LocalAppData%\SysGreen\sysgreen.db.</summary>
    public static string DefaultDatabasePath()
    {
        var dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "SysGreen");
        Directory.CreateDirectory(dir);
        return Path.Combine(dir, "sysgreen.db");
    }
}
