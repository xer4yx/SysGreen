using System.Diagnostics;
using SysGreen.Core.Abstractions;
using SysGreen.Core.Domain;

namespace SysGreen.Platform;

/// <summary>Enumerates live processes. (TODO: switch WorkingSet64 to true Private Working Set
/// via the "Process \ Working Set - Private" performance counter — see Q12 / RamEstimate.)</summary>
public sealed class ProcessProvider : IProcessProvider
{
    public IReadOnlyList<ProcessInfo> Enumerate()
    {
        var results = new List<ProcessInfo>();
        foreach (var p in Process.GetProcesses())
        {
            try
            {
                string? path = null;
                try { path = p.MainModule?.FileName; }
                catch { /* protected/system process when non-elevated — path stays null */ }

                results.Add(new ProcessInfo(p.Id, p.ProcessName, path, p.WorkingSet64));
            }
            catch
            {
                // Process exited between enumeration and inspection — skip.
            }
            finally
            {
                p.Dispose();
            }
        }
        return results;
    }
}
