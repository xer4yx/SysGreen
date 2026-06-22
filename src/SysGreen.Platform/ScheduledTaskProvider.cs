using Microsoft.Win32.TaskScheduler;
using SysGreen.Core.Abstractions;
using SysGreen.Core.Domain;
using ScheduledTask = Microsoft.Win32.TaskScheduler.Task; // disambiguate from System.Threading.Tasks.Task

namespace SysGreen.Platform;

/// <summary>
/// Enumerates logon-triggered scheduled tasks as Autostart Entries (ADR-0005), reading each task's
/// exec target so the Knowledge Base can classify it and reflecting its real enabled state. Humble
/// object — Task Scheduler COM lives here; access-denied tasks are skipped best-effort. Implements
/// <see cref="IAutostartProvider"/> too, so it folds into the combined autostart list.
/// </summary>
public sealed class ScheduledTaskProvider : IScheduledTaskProvider, IAutostartProvider
{
    private readonly IExecutablePublisherReader _publisherReader;

    public ScheduledTaskProvider(IExecutablePublisherReader publisherReader) =>
        _publisherReader = publisherReader;

    public IReadOnlyList<AutostartEntry> Enumerate()
    {
        var entries = new List<AutostartEntry>();
        try
        {
            using var service = new TaskService();
            Collect(service.RootFolder, entries);
        }
        catch
        {
            // Task Scheduler unavailable — degrade to no task entries rather than failing enumeration.
        }
        return entries;
    }

    private void Collect(TaskFolder folder, List<AutostartEntry> sink)
    {
        foreach (var task in folder.Tasks)
        {
            try
            {
                if (!IsLogonTriggered(task)) continue;

                var exe = FirstExecutablePath(task);
                sink.Add(new AutostartEntry(
                    Id: $"{AutostartLocation.ScheduledTask}:{task.Path}",
                    DisplayName: task.Name,
                    Kind: ItemKind.ScheduledTask,
                    Location: AutostartLocation.ScheduledTask,
                    ExecutablePath: exe,
                    Publisher: exe is null ? null : _publisherReader.ReadPublisher(exe),
                    State: task.Enabled ? AutostartState.Enabled : AutostartState.Disabled)
                {
                    MechanismKey = task.Path, // the full task path addresses it for disable/reverse
                });
            }
            catch
            {
                // A protected task we can't read — skip it.
            }
        }

        foreach (var sub in folder.SubFolders)
            Collect(sub, sink);
    }

    private static bool IsLogonTriggered(ScheduledTask task)
    {
        foreach (var trigger in task.Definition.Triggers)
            if (trigger.TriggerType == TaskTriggerType.Logon)
                return true;
        return false;
    }

    private static string? FirstExecutablePath(ScheduledTask task)
    {
        foreach (var action in task.Definition.Actions)
            if (action is ExecAction exec && !string.IsNullOrWhiteSpace(exec.Path))
                return Environment.ExpandEnvironmentVariables(exec.Path.Trim('"'));
        return null;
    }
}
