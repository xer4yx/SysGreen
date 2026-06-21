using System.Management;
using SysGreen.Core.Abstractions;

namespace SysGreen.Platform;

/// <summary>
/// Real restore-point creation via WMI (<c>root\default : SystemRestore.CreateRestorePoint</c>).
/// Requires elevation and System Restore enabled; otherwise the call throws and the tested
/// <see cref="RestorePointService"/> swallows it as a safe failure. Humble object — no decision
/// logic beyond mapping the WMI ReturnValue.
/// </summary>
public sealed class WmiRestorePointApi : IRestorePointApi
{
    private const uint Success = 0;            // created — or throttled (Windows returns 0 and skips)
    private const uint ModifySettings = 12;    // RESTOREPOINTINFO.RestorePointType
    private const uint BeginSystemChange = 100; // RESTOREPOINTINFO.EventType

    public RestorePointStatus CreateRestorePoint(string description)
    {
        var scope = new ManagementScope(@"\\.\root\default");
        scope.Connect();

        using var systemRestore = new ManagementClass(scope, new ManagementPath("SystemRestore"), null);
        using var inParams = systemRestore.GetMethodParameters("CreateRestorePoint");
        inParams["Description"] = description;
        inParams["RestorePointType"] = ModifySettings;
        inParams["EventType"] = BeginSystemChange;

        using var outParams = systemRestore.InvokeMethod("CreateRestorePoint", inParams, null);
        var returnValue = Convert.ToUInt32(outParams?["ReturnValue"] ?? Success);

        // A throttled call (a restore point already exists within Windows' frequency window)
        // returns 0 and is correctly treated as success. Any non-zero code (e.g. 1058 =
        // System Restore disabled) is a genuine failure → no lifeline.
        return returnValue == Success ? RestorePointStatus.Created : RestorePointStatus.Failed;
    }
}
