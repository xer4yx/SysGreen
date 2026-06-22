using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using SysGreen.Core.Abstractions;
using SysGreen.Core.Apply;
using SysGreen.Core.ChangeLog;

namespace SysGreen.Platform;

/// <summary>
/// Launches the short-lived elevated Helper to apply an admin-only batch (ADR-0004/0011).
/// Writes the batch to a temp job file, runs <c>SysGreen.Helper.exe</c> via ShellExecute "runas"
/// (one UAC prompt), waits for it to finish, then reads back the result the Helper wrote. Humble
/// object: process + file I/O only — the apply + restore-point logic lives in the Helper, where the
/// shared <see cref="ApplyService"/> (already unit-tested) does the work.
/// </summary>
public sealed class HelperElevatedApplyClient : IElevatedApplyClient
{
    private const int ErrorCancelled = 1223; // ERROR_CANCELLED: the user dismissed the UAC prompt.

    private readonly string _helperExecutablePath;
    private readonly string _databasePath;
    private readonly IClock _clock;

    public HelperElevatedApplyClient(string helperExecutablePath, string databasePath, IClock clock)
    {
        _helperExecutablePath = helperExecutablePath;
        _databasePath = databasePath;
        _clock = clock;
    }

    public ApplyResult Apply(IReadOnlyList<PendingChange> elevatedChanges)
    {
        var jobPath = Path.Combine(Path.GetTempPath(), $"sysgreen-job-{Guid.NewGuid():n}.json");
        var resultPath = jobPath + ".result.json";

        try
        {
            var job = new ApplyJob(
                ApplyJobSerializer.CurrentVersion, _databasePath, resultPath, elevatedChanges);
            File.WriteAllText(jobPath, ApplyJobSerializer.SerializeJob(job));

            switch (TryRunElevated(jobPath))
            {
                case ElevationOutcome.Declined:
                    // The user dismissed the UAC prompt: nothing was attempted (ADR-0004).
                    return new ApplyResult(
                        RestorePointRequired: false, RestorePointCreated: false, []) { ElevationDeclined = true };
                case ElevationOutcome.CouldNotStart:
                    return Failed(elevatedChanges, "The elevated helper could not be started.");
            }

            if (!File.Exists(resultPath))
                return Failed(elevatedChanges, "The elevated helper did not report a result.");

            return ApplyJobSerializer.DeserializeResult(File.ReadAllText(resultPath));
        }
        catch (Exception ex)
        {
            return Failed(elevatedChanges, ex.Message);
        }
        finally
        {
            TryDelete(jobPath);
            TryDelete(resultPath);
        }
    }

    private enum ElevationOutcome { Ran, Declined, CouldNotStart }

    private ElevationOutcome TryRunElevated(string jobPath)
    {
        var psi = new ProcessStartInfo
        {
            FileName = _helperExecutablePath,
            UseShellExecute = true, // required for the "runas" verb that raises the UAC prompt
            Verb = "runas",
            WindowStyle = ProcessWindowStyle.Hidden,
            CreateNoWindow = true,
        };
        psi.ArgumentList.Add(jobPath);

        try
        {
            using var process = Process.Start(psi);
            if (process is null) return ElevationOutcome.CouldNotStart;
            process.WaitForExit();
            return ElevationOutcome.Ran;
        }
        catch (Win32Exception ex) when (ex.NativeErrorCode == ErrorCancelled)
        {
            return ElevationOutcome.Declined;
        }
    }

    /// <summary>
    /// A best-effort "nothing applied" result when the Helper never ran or never reported. The
    /// records are for the on-screen status only — nothing reached the system, so nothing is
    /// persisted to the undo log here (the Helper is the sole writer of real Change Records).
    /// </summary>
    private ApplyResult Failed(IReadOnlyList<PendingChange> changes, string reason)
    {
        var records = changes
            .Select(c => new ChangeRecord(
                Guid.NewGuid().ToString("n"), c.Entry.Id, c.Entry.DisplayName, c.Action,
                "Unknown", "Unknown", "ElevatedHelper", _clock.UtcNow, false, reason))
            .ToList();
        return new ApplyResult(RestorePointRequired: false, RestorePointCreated: false, records);
    }

    private static void TryDelete(string path)
    {
        try { if (File.Exists(path)) File.Delete(path); } catch { /* temp file, best effort */ }
    }
}
