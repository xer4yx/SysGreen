using Microsoft.Data.Sqlite;
using SysGreen.Core.ChangeLog;
using SysGreen.Data;

namespace SysGreen.Tests;

/// <summary>
/// The Data Store must live outside Velopack's per-user install root (%LocalAppData%\SysGreen),
/// which the uninstaller deletes, and existing users' data must migrate to the new location on
/// first run after the relocation. See ADR-0016.
/// </summary>
public sealed class DataStoreMigrationTests : IDisposable
{
    private readonly string _root =
        Path.Combine(Path.GetTempPath(), $"sysgreen_migration_{Guid.NewGuid():n}");

    public void Dispose()
    {
        try { if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true); }
        catch { /* temp dir, best effort */ }
    }

    /// <summary>A path under a fresh, not-yet-created subdirectory of the test root.</summary>
    private string DbPathIn(string subdir) => Path.Combine(_root, subdir, "sysgreen.db");

    private static void WriteDb(string dbPath, string contents)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(dbPath)!);
        File.WriteAllText(dbPath, contents);
        File.WriteAllText(dbPath + "-wal", contents + "-wal");
        File.WriteAllText(dbPath + "-shm", contents + "-shm");
    }

    private static void Exec(SqliteConnection conn, string sql)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        cmd.ExecuteNonQuery();
    }

    /// <summary>Seeds a real Data Store at <paramref name="dbPath"/> with one Change Record.</summary>
    private static void SeedDatabase(string dbPath, string itemName)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(dbPath)!);
        var factory = new SqliteConnectionFactory(dbPath);
        new DatabaseBootstrapper(factory).EnsureCreated();
        new ChangeRecordRepository(factory).Add(new ChangeRecord(
            "id-" + itemName, "x", itemName, ChangeAction.Disable, "Enabled", "Disabled",
            "m", new DateTime(2026, 6, 20, 9, 0, 0, DateTimeKind.Utc), true, null));
        SqliteConnection.ClearAllPools(); // release the file handle for the move
    }

    private static string? FirstItemName(string dbPath) =>
        new ChangeRecordRepository(new SqliteConnectionFactory(dbPath)).GetRecent() is [var first, ..]
            ? first.ItemName : null;

    /// <summary>
    /// Seeds a Data Store whose one Change Record is committed in WAL mode with auto-checkpoint off,
    /// so the row lives only in the <c>-wal</c> sidecar — not yet in the main <c>.db</c>. The live
    /// files (including that uncheckpointed WAL) are copied to <paramref name="dbPath"/> while the
    /// connection is still open, reproducing a store left behind by a session that never checkpointed.
    /// </summary>
    private void SeedDatabaseWithUncheckpointedWal(string dbPath, string itemName)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(dbPath)!);
        var src = Path.Combine(_root, "wal_src_" + Guid.NewGuid().ToString("n"), "sysgreen.db");
        Directory.CreateDirectory(Path.GetDirectoryName(src)!);

        new DatabaseBootstrapper(new SqliteConnectionFactory(src)).EnsureCreated();
        SqliteConnection.ClearAllPools();

        using var conn = new SqliteConnection(
            new SqliteConnectionStringBuilder { DataSource = src, Pooling = false }.ToString());
        conn.Open();
        Exec(conn, "PRAGMA journal_mode=WAL;");
        Exec(conn, "PRAGMA wal_autocheckpoint=0;");
        Exec(conn,
            "INSERT INTO change_record " +
            "(id,item_id,item_name,action,prior_state,new_state,mechanism,timestamp_utc,success) " +
            $"VALUES ('id1','x','{itemName}','Disable','Enabled','Disabled','m','2026-06-20T09:00:00Z',1);");

        File.Copy(src, dbPath, overwrite: true);
        if (File.Exists(src + "-wal")) File.Copy(src + "-wal", dbPath + "-wal", overwrite: true);
        if (File.Exists(src + "-shm")) File.Copy(src + "-shm", dbPath + "-shm", overwrite: true);
    }

    [Fact]
    public void Default_database_path_lives_outside_the_velopack_install_root()
    {
        var path = SqliteConnectionFactory.DefaultDatabasePath();

        // Velopack installs to %LocalAppData%\SysGreen and deletes that tree on uninstall.
        // The Data Store must not sit inside it (ADR-0016).
        var installRoot = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SysGreen");

        Assert.False(
            path.StartsWith(installRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase),
            $"Data Store must live outside the install root, but was: {path}");
    }

    [Fact]
    public void Migrates_the_database_and_clears_the_old_location_when_only_old_exists()
    {
        var oldDb = DbPathIn("old");
        var newDb = DbPathIn("new");
        SeedDatabase(oldDb, "Spotify");

        var migrated = DataStoreMigration.EnsureMigrated(oldDb, newDb);

        Assert.True(migrated);
        Assert.Equal("Spotify", FirstItemName(newDb));
        // A move, not a copy: the old location is left clean so a later uninstall can't resurrect it.
        Assert.False(File.Exists(oldDb));
        Assert.False(File.Exists(oldDb + "-wal"));
        Assert.False(File.Exists(oldDb + "-shm"));
    }

    [Fact]
    public void Does_not_touch_the_new_location_when_both_exist_so_new_always_wins()
    {
        var oldDb = DbPathIn("old");
        var newDb = DbPathIn("new");
        WriteDb(oldDb, "OLD");
        WriteDb(newDb, "NEW"); // live data already present at the new location

        var migrated = DataStoreMigration.EnsureMigrated(oldDb, newDb);

        Assert.False(migrated);
        Assert.Equal("NEW", File.ReadAllText(newDb)); // untouched — never overwritten by OLD
    }

    [Fact]
    public void Uninstall_keeps_the_data_store_when_the_user_chose_keep()
    {
        var dataDir = Path.Combine(_root, "data");
        SeedDatabase(Path.Combine(dataDir, "sysgreen.db"), "Spotify");

        DataStoreUninstall.Apply(keepData: true, dataDir);

        Assert.True(Directory.Exists(dataDir)); // kept by default (ADR-0017)
    }

    [Fact]
    public void Uninstall_deletes_the_data_store_when_the_user_chose_delete()
    {
        var dataDir = Path.Combine(_root, "data");
        SeedDatabase(Path.Combine(dataDir, "sysgreen.db"), "Spotify");

        DataStoreUninstall.Apply(keepData: false, dataDir);

        Assert.False(Directory.Exists(dataDir));
    }

    [Fact]
    public void Uninstall_delete_is_a_no_op_when_the_data_store_is_already_gone()
    {
        var dataDir = Path.Combine(_root, "missing");

        DataStoreUninstall.Apply(keepData: false, dataDir); // must not throw

        Assert.False(Directory.Exists(dataDir));
    }

    [Fact]
    public void Relocates_a_non_sqlite_store_as_is_without_throwing()
    {
        // Defensive: a corrupt / partially written old file must not throw out of startup. The
        // checkpoint can't run, so fall back to relocating the files as-is rather than losing them.
        var oldDb = DbPathIn("old");
        var newDb = DbPathIn("new");
        WriteDb(oldDb, "not-a-sqlite-database");

        var migrated = DataStoreMigration.EnsureMigrated(oldDb, newDb);

        Assert.True(migrated);
        Assert.Equal("not-a-sqlite-database", File.ReadAllText(newDb));
        Assert.False(File.Exists(oldDb));
    }

    [Fact]
    public void Is_a_no_op_when_there_is_nothing_to_migrate()
    {
        var oldDb = DbPathIn("old");
        var newDb = DbPathIn("new");

        var migrated = DataStoreMigration.EnsureMigrated(oldDb, newDb);

        Assert.False(migrated);
        Assert.False(File.Exists(newDb));
    }

    [Fact]
    public void Concurrent_callers_migrate_exactly_once_without_throwing()
    {
        // On the first boot after the relocation ships, the Agent (autostart) and the App can call
        // the migration at nearly the same time. Exactly one must perform the move; none may throw.
        var oldDb = DbPathIn("old");
        var newDb = DbPathIn("new");
        SeedDatabase(oldDb, "Spotify");

        var results = new System.Collections.Concurrent.ConcurrentBag<bool>();
        Parallel.For(0, 8, _ => results.Add(DataStoreMigration.EnsureMigrated(oldDb, newDb)));

        Assert.Equal(1, results.Count(performed => performed)); // exactly one did the move
        Assert.Equal("Spotify", FirstItemName(newDb));
    }

    [Fact]
    public void Folds_uncheckpointed_wal_commits_into_the_db_so_the_new_location_is_self_contained()
    {
        var oldDb = DbPathIn("old");
        var newDb = DbPathIn("new");
        SeedDatabaseWithUncheckpointedWal(oldDb, "Spotify");
        Assert.True(File.Exists(oldDb + "-wal")); // precondition: the row really is WAL-only

        Assert.True(DataStoreMigration.EnsureMigrated(oldDb, newDb));

        // The moved .db is self-contained: no sidecar is carried alongside it (checkpoint-then-move).
        // Asserted before any read opens a fresh WAL connection on the new file.
        Assert.False(File.Exists(newDb + "-wal"));
        Assert.False(File.Exists(newDb + "-shm"));
        // ...and the WAL-only row was folded in, so the history is intact.
        Assert.Equal("Spotify", FirstItemName(newDb));
    }

    [Fact]
    public void A_real_sqlite_database_keeps_its_change_record_history_across_the_move()
    {
        var oldDb = DbPathIn("old");
        var newDb = DbPathIn("new");

        // Seed a real SQLite Data Store at the old location with one Change Record, then release
        // the file handles — standing in for the process that wrote it having exited before the move.
        Directory.CreateDirectory(Path.GetDirectoryName(oldDb)!);
        var oldFactory = new SqliteConnectionFactory(oldDb);
        new DatabaseBootstrapper(oldFactory).EnsureCreated();
        new ChangeRecordRepository(oldFactory).Add(new ChangeRecord(
            "id1", "HKCU:Spotify", "Spotify", ChangeAction.Disable, "Enabled", "Disabled",
            "StartupApproved", new DateTime(2026, 6, 20, 9, 0, 0, DateTimeKind.Utc), true, null));
        SqliteConnection.ClearAllPools();

        Assert.True(DataStoreMigration.EnsureMigrated(oldDb, newDb));

        // A fresh factory on the NEW path sees the pre-migration history.
        var record = Assert.Single(
            new ChangeRecordRepository(new SqliteConnectionFactory(newDb)).GetRecent());
        Assert.Equal("Spotify", record.ItemName);
    }
}
