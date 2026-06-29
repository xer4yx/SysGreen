using System;
using SysGreen.Data;
using Velopack;

namespace SysGreen.App;

/// <summary>
/// Custom entry point (Velopack — ADR-0009). <see cref="VelopackApp"/> must run before WPF starts so
/// install/update/uninstall hooks are handled without spinning up the UI. On a normal launch it is a
/// no-op and we proceed to construct and run the WPF <see cref="App"/> as usual.
/// </summary>
public static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        // On uninstall, Velopack runs this hook then exits — so the lines below are reached only on a
        // normal launch. The hook honors the user's data-retention choice (ADR-0017).
        VelopackApp.Build()
            .OnBeforeUninstallFastCallback(_ => CleanUpDataStoreOnUninstall())
            .Run();

        // Relocate the Data Store out of the (uninstall-deleted) install root before anything opens it
        // (ADR-0016). One-time, idempotent, and shared with the Agent, which may run this first.
        DataStoreMigration.EnsureDefaultMigrated();

        var app = new App();
        app.InitializeComponent();
        app.Run();
    }

    /// <summary>
    /// Uninstall cleanup (ADR-0017): the Data Store lives outside the install root (ADR-0016), so
    /// Velopack never deletes it — "keep" (the default) is a no-op and only an explicit "delete" wipes
    /// it. Reading the choice is fail-safe: any error leaves the data in place.
    /// </summary>
    private static void CleanUpDataStoreOnUninstall()
    {
        var keep = true;
        try
        {
            var factory = new SqliteConnectionFactory(SqliteConnectionFactory.DefaultDatabasePath());
            keep = new SettingsRepository(factory).KeepDataOnUninstall;
        }
        catch { /* settings unreadable during uninstall — fail safe to keep */ }

        DataStoreUninstall.Apply(keep, SqliteConnectionFactory.DefaultDataDirectory());
    }
}
