using SysGreen.Core.Abstractions;

namespace SysGreen.Core;

/// <summary>
/// Interprets a raw restore-point attempt into the mandatory-lifeline contract (ADR-0005):
/// a newly created point OR a recent existing one both satisfy "we have a restore point";
/// any failure (System Restore disabled, not elevated, WMI unavailable) is a safe false.
/// </summary>
public sealed class RestorePointService : IRestorePointService
{
    private readonly IRestorePointApi _api;

    public RestorePointService(IRestorePointApi api) => _api = api;

    public bool TryCreateRestorePoint(string description)
    {
        try
        {
            return _api.CreateRestorePoint(description)
                is RestorePointStatus.Created or RestorePointStatus.AlreadyExistsRecently;
        }
        catch
        {
            // System Restore disabled, not elevated, or WMI unavailable — no lifeline available.
            return false;
        }
    }
}
