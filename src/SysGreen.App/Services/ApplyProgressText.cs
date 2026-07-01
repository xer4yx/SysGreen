using SysGreen.Core.Apply;

namespace SysGreen.App.Services;

/// <summary>Maps an <see cref="ApplyProgress"/> phase to the text shown in the header progress strip
/// (Topic B / Phase 6). The restore point is indeterminate, so there is no percentage — just words.</summary>
public static class ApplyProgressText
{
    public static string Describe(ApplyProgress progress) => progress.Stage switch
    {
        ApplyStage.CreatingRestorePoint => "Creating a restore point…",
        ApplyStage.Applying => $"Applying {progress.Current} of {progress.Total}…",
        ApplyStage.Done => "Finishing…",
        _ => "",
    };
}
