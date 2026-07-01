using SysGreen.Core.Abstractions;

namespace SysGreen.Core.Apply;

/// <summary>Process exit codes the elevated Helper returns to the App (ADR-0011).</summary>
public static class HelperExitCodes
{
    public const int Success = 0;
    public const int NoJobFile = 1;
    public const int JobFileNotFound = 2;
    public const int BadJobFile = 3;
    public const int RestorePointAborted = 4;
    public const int PartialFailure = 5;
}

/// <summary>
/// The Helper's testable core: reads a job file, applies its batch through an injected
/// <see cref="IApplyService"/> (built for the job's database), writes the result file, and maps the
/// outcome to an exit code. Keeps process/Win32 concerns out of the unit-tested path (ADR-0011).
/// </summary>
public sealed class HelperRunner
{
    private readonly Func<ApplyJob, IApplyService> _applyServiceFor;

    // Takes the whole job (not just the database path) so the built ApplyService can also wire a
    // progress sink at the job's ProgressPath (Topic B / Phase 6).
    public HelperRunner(Func<ApplyJob, IApplyService> applyServiceFor) =>
        _applyServiceFor = applyServiceFor;

    public int Run(string[] args)
    {
        if (args.Length == 0) return HelperExitCodes.NoJobFile;

        var jobPath = args[0];
        if (!File.Exists(jobPath)) return HelperExitCodes.JobFileNotFound;

        ApplyJob job;
        try
        {
            job = ApplyJobSerializer.DeserializeJob(File.ReadAllText(jobPath));
        }
        catch
        {
            return HelperExitCodes.BadJobFile;
        }

        var result = _applyServiceFor(job).Apply(job.Changes);
        File.WriteAllText(job.ResultPath, ApplyJobSerializer.SerializeResult(result));

        if (result.Aborted) return HelperExitCodes.RestorePointAborted;
        if (result.FailedCount > 0) return HelperExitCodes.PartialFailure;
        return HelperExitCodes.Success;
    }
}
