using SysGreen.Core;
using SysGreen.Core.Apply;
using SysGreen.Core.Startup;
using SysGreen.Data;
using SysGreen.Platform;

// SysGreen.Helper — the short-lived, admin-elevated process (ADR-0004).
// The non-elevated App launches it via ShellExecute "runas" with a temp job-file path when an
// Apply batch includes admin-only actions (HKLM autostart, common Startup folder, services). It
// applies the batch through the same ApplyService the App uses, which creates the mandatory
// restore point (ADR-0005) and persists Change Records to the shared SQLite DB; it then writes a
// result file and exits with a status code (ADR-0011). No long-lived IPC; nothing stays resident.

return new HelperRunner(BuildApplyService).Run(args);

// Composed per job so the Helper writes to the *invoking user's* database (passed in the job),
// not whichever account approved the elevation.
static IApplyService BuildApplyService(string databasePath)
{
    var factory = new SqliteConnectionFactory(databasePath);
    new DatabaseBootstrapper(factory).EnsureCreated(); // idempotent; the App normally created it first
    var clock = new SystemClock();
    var controller = new StartupApprovedItemController(
        new StartupApprovedRegistryStore(), new ProcessTerminator(), clock);
    var restorePoints = new RestorePointService(new WmiRestorePointApi());
    var changeLog = new ChangeRecordRepository(factory);
    return new ApplyService(controller, changeLog, restorePoints, clock);
}
