using SysGreen.Core.Abstractions;
using SysGreen.Core.ChangeLog;
using SysGreen.Core.Domain;
using SysGreen.Core.Startup;

namespace SysGreen.Tests;

public class ScheduledTaskControllerTests
{
    private static readonly DateTime When = new(2026, 6, 22, 9, 0, 0, DateTimeKind.Utc);

    private static AutostartEntry Task(string path, string name) =>
        new($"ScheduledTask:{path}", name, ItemKind.ScheduledTask, AutostartLocation.ScheduledTask,
            null, null, AutostartState.Enabled) { MechanismKey = path };

    private static AutostartEntry Run(string name) =>
        new($"HKCU:{name}", name, ItemKind.StartupApp, AutostartLocation.RegistryRunCurrentUser,
            $@"C:\x\{name}.exe", null, AutostartState.Enabled);

    [Fact]
    public void Disable_turns_the_task_off_and_records_prior_state_mechanism_and_key()
    {
        var store = new FakeTaskStore();
        store.SetEnabled(@"\Updater", true);

        var record = new ScheduledTaskItemController(store, new FixedClock(When))
            .Disable(Task(@"\Updater", "Updater"));

        Assert.False(store.IsEnabled(@"\Updater"));
        Assert.Equal(ChangeAction.Disable, record.Action);
        Assert.Equal("Enabled", record.PriorState);
        Assert.Equal("Disabled", record.NewState);
        Assert.Equal("ScheduledTask", record.Mechanism);
        Assert.Equal(AutostartLocation.ScheduledTask, record.Location);
        Assert.Equal(@"\Updater", record.MechanismKey);
    }

    [Fact]
    public void Enable_turns_the_task_back_on()
    {
        var store = new FakeTaskStore();
        store.SetEnabled(@"\Updater", false);

        var record = new ScheduledTaskItemController(store, new FixedClock(When))
            .Enable(Task(@"\Updater", "Updater"));

        Assert.True(store.IsEnabled(@"\Updater"));
        Assert.Equal(ChangeAction.Enable, record.Action);
        Assert.Equal("Disabled", record.PriorState);
    }

    [Fact]
    public void EndTask_is_not_supported_for_a_scheduled_task()
    {
        Assert.Throws<NotSupportedException>(() =>
            new ScheduledTaskItemController(new FakeTaskStore(), new FixedClock(When))
                .EndTask(new ProcessInfo(1, "x", null, 0)));
    }

    [Fact]
    public void A_scheduled_task_change_requires_elevation_so_it_runs_through_the_helper()
    {
        // Disabling a logon task commonly needs admin; routing through the Helper makes it reliable.
        Assert.True(Task(@"\Updater", "Updater").RequiresElevation);
    }

    [Fact]
    public void Dispatcher_routes_a_scheduled_task_to_the_task_controller()
    {
        var startup = new RecordingController();
        var task = new RecordingController();

        new DispatchingItemController(startup, task, new RecordingController()).Disable(Task(@"\Updater", "Updater"));

        Assert.Equal(1, task.DisableCount);
        Assert.Equal(0, startup.DisableCount);
    }

    [Fact]
    public void Dispatcher_routes_a_background_app_to_the_background_controller()
    {
        var startup = new RecordingController();
        var background = new RecordingController();
        var entry = new AutostartEntry("BackgroundApp:Claude_x", "Claude", ItemKind.BackgroundApp,
            AutostartLocation.BackgroundApp, null, null, AutostartState.Enabled) { MechanismKey = "Claude_x" };

        new DispatchingItemController(startup, new RecordingController(), background).Disable(entry);

        Assert.Equal(1, background.DisableCount);
        Assert.Equal(0, startup.DisableCount);
    }

    [Fact]
    public void Dispatcher_routes_a_run_key_to_the_startup_approved_controller()
    {
        var startup = new RecordingController();
        var task = new RecordingController();

        new DispatchingItemController(startup, task, new RecordingController()).Disable(Run("Spotify"));

        Assert.Equal(1, startup.DisableCount);
        Assert.Equal(0, task.DisableCount);
    }

    [Fact]
    public void Dispatcher_sends_end_task_to_the_startup_approved_controller()
    {
        var startup = new RecordingController();
        var task = new RecordingController();

        new DispatchingItemController(startup, task, new RecordingController()).EndTask(new ProcessInfo(1, "x", null, 0));

        Assert.Equal(1, startup.EndTaskCount);
        Assert.Equal(0, task.EndTaskCount);
    }

    private sealed class FakeTaskStore : IScheduledTaskStore
    {
        private readonly Dictionary<string, bool> _state = new();
        public bool IsEnabled(string taskPath) => _state.TryGetValue(taskPath, out var v) && v;
        public void SetEnabled(string taskPath, bool enabled) => _state[taskPath] = enabled;
    }

    private sealed class RecordingController : IItemController
    {
        public int DisableCount { get; private set; }
        public int EndTaskCount { get; private set; }
        private static ChangeRecord Dummy =>
            new("r", "i", "n", ChangeAction.Disable, "", "", "", default, true, null);

        public ChangeRecord Disable(AutostartEntry entry) { DisableCount++; return Dummy; }
        public ChangeRecord Enable(AutostartEntry entry) => Dummy;
        public ChangeRecord EndTask(ProcessInfo process) { EndTaskCount++; return Dummy; }
    }

    private sealed class FixedClock(DateTime now) : IClock
    {
        public DateTime UtcNow { get; } = now;
    }
}
