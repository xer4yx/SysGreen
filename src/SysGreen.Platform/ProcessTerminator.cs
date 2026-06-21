using System.Diagnostics;
using SysGreen.Core.Abstractions;

namespace SysGreen.Platform;

/// <summary>Real process termination (the End Task mechanism). Humble object — no logic.</summary>
public sealed class ProcessTerminator : IProcessTerminator
{
    public void Terminate(int pid)
    {
        using var process = Process.GetProcessById(pid);
        process.Kill();
    }
}
