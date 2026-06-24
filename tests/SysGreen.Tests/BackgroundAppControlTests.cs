using SysGreen.Core.Abstractions;
using SysGreen.Core.ChangeLog;
using SysGreen.Core.Domain;
using SysGreen.Core.Startup;

namespace SysGreen.Tests;

public class BackgroundAppControlTests
{
    private static readonly DateTime When = new(2026, 6, 23, 9, 0, 0, DateTimeKind.Utc);

    private static AutostartEntry App(string familyName, string name) =>
        new($"BackgroundApp:{familyName}", name, ItemKind.BackgroundApp, AutostartLocation.BackgroundApp,
            null, null, AutostartState.Enabled) { MechanismKey = familyName };

    [Fact]
    public void Disable_blocks_background_access_and_records_it()
    {
        var store = new FakeStore();
        store.SetEnabled("Claude_pzs8sxrjxfjjc", true);

        var record = new BackgroundAppItemController(store, new FixedClock(When))
            .Disable(App("Claude_pzs8sxrjxfjjc", "Claude"));

        Assert.False(store.IsEnabled("Claude_pzs8sxrjxfjjc"));
        Assert.Equal(ChangeAction.Disable, record.Action);
        Assert.Equal("Enabled", record.PriorState);
        Assert.Equal("Disabled", record.NewState);
        Assert.Equal("BackgroundAccess", record.Mechanism);
        Assert.Equal(AutostartLocation.BackgroundApp, record.Location);
        Assert.Equal("Claude_pzs8sxrjxfjjc", record.MechanismKey);
    }

    [Fact]
    public void Enable_restores_background_access()
    {
        var store = new FakeStore();
        store.SetEnabled("Claude_pzs8sxrjxfjjc", false);

        var record = new BackgroundAppItemController(store, new FixedClock(When))
            .Enable(App("Claude_pzs8sxrjxfjjc", "Claude"));

        Assert.True(store.IsEnabled("Claude_pzs8sxrjxfjjc"));
        Assert.Equal(ChangeAction.Enable, record.Action);
        Assert.Equal("Disabled", record.PriorState);
    }

    [Fact]
    public void End_task_is_not_supported_for_a_background_app() =>
        Assert.Throws<NotSupportedException>(() =>
            new BackgroundAppItemController(new FakeStore(), new FixedClock(When))
                .EndTask(new ProcessInfo(1, "x", null, 0)));

    private sealed class FakeStore : IBackgroundAppStore
    {
        private readonly Dictionary<string, bool> _state = new();
        public bool IsEnabled(string packageFamilyName) =>
            _state.TryGetValue(packageFamilyName, out var v) && v;
        public void SetEnabled(string packageFamilyName, bool enabled) => _state[packageFamilyName] = enabled;
    }

    private sealed class FixedClock(DateTime now) : IClock
    {
        public DateTime UtcNow { get; } = now;
    }
}
