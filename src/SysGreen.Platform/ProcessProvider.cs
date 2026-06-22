using System.Diagnostics;
using System.Management;
using SysGreen.Core.Abstractions;
using SysGreen.Core.Domain;

namespace SysGreen.Platform;

/// <summary>
/// Enumerates live processes with their true Private Working Set — memory unique to the process,
/// matching Task Manager (CONTEXT.md / Q12). The private figure comes from one WMI perf snapshot
/// (<c>Win32_PerfFormattedData_PerfProc_Process.WorkingSetPrivate</c>, in bytes); full
/// <see cref="Process.WorkingSet64"/> is the fallback when a process isn't in the snapshot. Humble
/// object — Win32/WMI only; the RAM-estimate chain logic is the tested <see cref="Core.Usage.RamEstimate"/>.
/// </summary>
public sealed class ProcessProvider : IProcessProvider
{
    public IReadOnlyList<ProcessInfo> Enumerate()
    {
        var privateWorkingSet = ReadPrivateWorkingSets();

        var results = new List<ProcessInfo>();
        foreach (var p in Process.GetProcesses())
        {
            try
            {
                string? path = null;
                try { path = p.MainModule?.FileName; }
                catch { /* protected/system process when non-elevated — path stays null */ }

                var bytes = privateWorkingSet.TryGetValue(p.Id, out var pws) ? pws : p.WorkingSet64;
                results.Add(new ProcessInfo(p.Id, p.ProcessName, path, bytes));
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

    /// <summary>PID → Private Working Set (bytes) from one WMI perf snapshot. Empty on any failure.</summary>
    private static Dictionary<int, long> ReadPrivateWorkingSets()
    {
        var map = new Dictionary<int, long>();
        try
        {
            using var searcher = new ManagementObjectSearcher(
                "SELECT IDProcess, WorkingSetPrivate FROM Win32_PerfFormattedData_PerfProc_Process");
            foreach (var result in searcher.Get())
            {
                using var mo = (ManagementObject)result;
                var pid = Convert.ToInt32(mo["IDProcess"]);
                if (pid == 0) continue; // the Idle process / "_Total" aggregate
                map[pid] = Convert.ToInt64(mo["WorkingSetPrivate"]);
            }
        }
        catch
        {
            // WMI / perf counters unavailable — callers fall back to WorkingSet64.
        }
        return map;
    }
}
