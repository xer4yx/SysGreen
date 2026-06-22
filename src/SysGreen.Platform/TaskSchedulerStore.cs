using Microsoft.Win32.TaskScheduler;
using SysGreen.Core.Abstractions;

namespace SysGreen.Platform;

/// <summary>
/// Real Task Scheduler adapter for the scheduled-task disable mechanism (ADR-0005). Humble object:
/// it only flips a task's <c>Enabled</c> flag, addressed by full path — non-destructive and reversible.
/// Disabling most logon tasks needs admin, so this runs inside the elevated Helper.
/// </summary>
public sealed class TaskSchedulerStore : IScheduledTaskStore
{
    public bool IsEnabled(string taskPath)
    {
        using var service = new TaskService();
        using var task = service.GetTask(taskPath);
        return task?.Enabled ?? false;
    }

    public void SetEnabled(string taskPath, bool enabled)
    {
        using var service = new TaskService();
        using var task = service.GetTask(taskPath)
            ?? throw new InvalidOperationException($"Scheduled task not found: {taskPath}");
        task.Enabled = enabled;
    }
}
