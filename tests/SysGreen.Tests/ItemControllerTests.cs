using SysGreen.Core.Abstractions;
using SysGreen.Core.ChangeLog;
using SysGreen.Core.Domain;
using SysGreen.Core.Startup;

namespace SysGreen.Tests;

public class ItemControllerTests
{
    private static readonly DateTime FixedNow = new(2026, 6, 20, 9, 0, 0, DateTimeKind.Utc);
    private const AutostartLocation HkcuRun = AutostartLocation.RegistryRunCurrentUser;

    private static AutostartEntry Entry(AutostartState state = AutostartState.Enabled) =>
        new("HKCU:Spotify", "Spotify", ItemKind.StartupApp, HkcuRun, @"C:\x\Spotify.exe", "Spotify AB", state);

    private static StartupApprovedItemController Controller(
        FakeStore store, FakeTerminator? terminator = null) =>
        new(store, terminator ?? new FakeTerminator(), new FixedClock(FixedNow));

    [Fact]
    public void Disable_writes_a_disabled_startup_approved_flag()
    {
        var store = new FakeStore();

        Controller(store).Disable(Entry());

        var flag = store.ReadFlag(HkcuRun, "Spotify")!;
        Assert.False(StartupApprovedFlag.IsEnabled(flag));
    }

    [Fact]
    public void Disable_records_prior_enabled_state_mechanism_and_timestamp()
    {
        var store = new FakeStore();
        store.WriteFlag(HkcuRun, "Spotify", StartupApprovedFlag.EncodeEnabled());

        var record = Controller(store).Disable(Entry());

        Assert.Equal(ChangeAction.Disable, record.Action);
        Assert.Equal("Enabled", record.PriorState);
        Assert.Equal("Disabled", record.NewState);
        Assert.Equal("StartupApproved", record.Mechanism);
        Assert.Equal("Spotify", record.ItemName);
        Assert.Equal(FixedNow, record.TimestampUtc);
        Assert.True(record.Success);
    }

    [Fact]
    public void Disable_records_the_entrys_location_so_the_change_can_be_reversed_precisely()
    {
        var record = Controller(new FakeStore()).Disable(Entry());

        Assert.Equal(HkcuRun, record.Location);
        Assert.True(record.IsReversible);
    }

    [Fact]
    public void Disable_on_already_disabled_item_records_prior_disabled()
    {
        var store = new FakeStore();
        store.WriteFlag(HkcuRun, "Spotify", StartupApprovedFlag.EncodeDisabled(FixedNow));

        var record = Controller(store).Disable(Entry(AutostartState.Disabled));

        Assert.Equal("Disabled", record.PriorState);
    }

    [Fact]
    public void Enable_writes_enabled_flag_and_records_enable()
    {
        var store = new FakeStore();
        store.WriteFlag(HkcuRun, "Spotify", StartupApprovedFlag.EncodeDisabled(FixedNow));

        var record = Controller(store).Enable(Entry(AutostartState.Disabled));

        Assert.True(StartupApprovedFlag.IsEnabled(store.ReadFlag(HkcuRun, "Spotify")!));
        Assert.Equal(ChangeAction.Enable, record.Action);
        Assert.Equal("Disabled", record.PriorState);
        Assert.Equal("Enabled", record.NewState);
    }

    [Fact]
    public void EndTask_terminates_the_process_and_records_kill()
    {
        var terminator = new FakeTerminator();
        var process = new ProcessInfo(4242, "Spotify", @"C:\x\Spotify.exe", 100);

        var record = Controller(new FakeStore(), terminator).EndTask(process);

        Assert.Contains(4242, terminator.Terminated);
        Assert.Equal(ChangeAction.EndTask, record.Action);
        Assert.Equal("ProcessKill", record.Mechanism);
        Assert.Equal("Spotify", record.ItemName);
    }

    private sealed class FakeStore : IStartupApprovedStore
    {
        private readonly Dictionary<string, byte[]> _flags = new();
        private static string Key(AutostartLocation l, string n) => $"{l}|{n}";

        public byte[]? ReadFlag(AutostartLocation location, string valueName) =>
            _flags.TryGetValue(Key(location, valueName), out var v) ? v : null;

        public void WriteFlag(AutostartLocation location, string valueName, byte[] data) =>
            _flags[Key(location, valueName)] = data;
    }

    private sealed class FakeTerminator : IProcessTerminator
    {
        public List<int> Terminated { get; } = [];
        public void Terminate(int pid) => Terminated.Add(pid);
    }

    private sealed class FixedClock(DateTime now) : IClock
    {
        public DateTime UtcNow { get; } = now;
    }
}
