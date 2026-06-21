using SysGreen.Core.Abstractions;

// SysGreen.Helper — the short-lived, admin-elevated process (ADR-0004).
// The non-elevated UI spawns it via ShellExecute "runas" with a temp job-file path when a
// batch includes admin-only actions (HKLM autostart, services, some scheduled tasks).
// The Helper creates the mandatory restore point (ADR-0005), executes the batch via
// IItemController, writes Change Records to the shared SQLite DB (ADR-0011), and exits.
//
// This scaffold validates the entry contract; batch execution is wired in a later milestone.

if (args.Length == 0)
{
    Console.Error.WriteLine("SysGreen.Helper expects a job-file path argument.");
    return ExitCodes.NoJobFile;
}

string jobFile = args[0];
if (!File.Exists(jobFile))
{
    Console.Error.WriteLine($"Job file not found: {jobFile}");
    return ExitCodes.JobFileNotFound;
}

Console.WriteLine($"SysGreen.Helper: would execute batch from '{jobFile}'.");
// TODO: deserialize the job, create a restore point, run IItemController actions,
// persist Change Records, and return a per-item result summary (ADR-0005, ADR-0013).
_ = typeof(IItemController); // anchor the Core reference until the batch runner lands.
return ExitCodes.Success;

static class ExitCodes
{
    public const int Success = 0;
    public const int NoJobFile = 1;
    public const int JobFileNotFound = 2;
}
