using System.Security.Cryptography;
using System.Text;
using System.Threading;
using Microsoft.Data.Sqlite;

namespace SysGreen.Data;

/// <summary>
/// One-time relocation of the Data Store from the old in-install-root location to the new
/// sibling location (ADR-0016). Idempotent and safe to call from every process that opens the
/// store (App, Agent), since the Agent can autostart before the App on the first boot after the
/// relocation ships.
/// </summary>
public static class DataStoreMigration
{
    /// <summary>
    /// Moves <paramref name="oldDbPath"/> (and its <c>-wal</c>/<c>-shm</c> sidecars) to
    /// <paramref name="newDbPath"/> exactly once. No-op if the new location already exists
    /// (new always wins) or the old location is absent. Returns true iff a move was performed.
    /// A cross-process mutex (scoped to the target) serializes concurrent App/Agent callers so
    /// the first migrates and the rest see the new location and no-op, without racing on the move.
    /// </summary>
    /// <summary>
    /// Runs the default relocation: the old in-install-root DB (%LocalAppData%\SysGreen\sysgreen.db)
    /// to the current Data Store location. Called at the top of every entry point (App, Agent) before
    /// the store is opened. Composition glue over the unit-tested <see cref="EnsureMigrated"/>.
    /// </summary>
    public static bool EnsureDefaultMigrated()
    {
        var oldDb = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "SysGreen", "sysgreen.db");
        return EnsureMigrated(oldDb, SqliteConnectionFactory.DefaultDatabasePath());
    }

    public static bool EnsureMigrated(string oldDbPath, string newDbPath)
    {
        using var gate = new Mutex(initiallyOwned: false, MutexNameFor(newDbPath));
        var held = false;
        try { held = gate.WaitOne(TimeSpan.FromSeconds(30)); }
        catch (AbandonedMutexException) { held = true; } // prior caller crashed mid-migration; we hold it now

        try
        {
            if (File.Exists(newDbPath)) return false;  // new always wins — never overwrite live data
            if (!File.Exists(oldDbPath)) return false; // nothing to migrate

            Directory.CreateDirectory(Path.GetDirectoryName(newDbPath)!);

            // Checkpoint-then-move: fold any uncheckpointed WAL frames into the main .db so the moved
            // file is self-contained — moving the .db alone can otherwise lose the most recent commits
            // (ADR-0016). The now-redundant sidecars are discarded, not carried.
            try
            {
                Checkpoint(oldDbPath);
                File.Move(oldDbPath, newDbPath);
                DeleteIfExists(oldDbPath + "-wal");
                DeleteIfExists(oldDbPath + "-shm");
            }
            catch (SqliteException)
            {
                // Not a usable SQLite file (corrupt / partially written): the checkpoint can't run,
                // but we must not crash startup or drop the user's data — relocate the bytes as-is.
                MoveIfExists(oldDbPath, newDbPath);
                MoveIfExists(oldDbPath + "-wal", newDbPath + "-wal");
                MoveIfExists(oldDbPath + "-shm", newDbPath + "-shm");
            }
            return true;
        }
        finally
        {
            if (held) gate.ReleaseMutex();
        }
    }

    /// <summary>
    /// Folds the database's WAL frames into its main file and truncates the WAL, so the file is
    /// self-contained for the move. Opened without pooling so the handle is released before the move.
    /// </summary>
    private static void Checkpoint(string dbPath)
    {
        using var conn = new SqliteConnection(
            new SqliteConnectionStringBuilder { DataSource = dbPath, Pooling = false }.ToString());
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "PRAGMA wal_checkpoint(TRUNCATE);";
        cmd.ExecuteNonQuery();
    }

    private static void MoveIfExists(string from, string to)
    {
        if (File.Exists(from)) File.Move(from, to);
    }

    private static void DeleteIfExists(string path)
    {
        if (File.Exists(path)) File.Delete(path);
    }

    /// <summary>
    /// A mutex name scoped to the migration target. Mutex names treat '\' as a namespace
    /// separator, so the path is hashed into a safe token. Session-local (no "Global\" prefix)
    /// is sufficient — the App and Agent share the user's logon session.
    /// </summary>
    private static string MutexNameFor(string newDbPath)
    {
        var hash = Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes(newDbPath.ToLowerInvariant())));
        return "SysGreen.DataStoreMigration." + hash[..16];
    }
}
