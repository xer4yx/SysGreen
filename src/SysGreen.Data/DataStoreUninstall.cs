using Microsoft.Data.Sqlite;

namespace SysGreen.Data;

/// <summary>
/// Honors the user's uninstall data-retention choice (ADR-0017). The Data Store lives outside the
/// install root (ADR-0016), so Velopack's uninstaller never touches it — keeping it is the default
/// and costs nothing. Only an explicit "delete" wipes it, which this performs.
/// </summary>
public static class DataStoreUninstall
{
    /// <summary>Deletes the Data Store directory iff the user opted out of keeping it. Best-effort.</summary>
    public static void Apply(bool keepData, string dataDirectory)
    {
        if (keepData) return;
        if (!Directory.Exists(dataDirectory)) return;
        SqliteConnection.ClearAllPools(); // release any open handles before deleting the store
        Directory.Delete(dataDirectory, recursive: true);
    }
}
