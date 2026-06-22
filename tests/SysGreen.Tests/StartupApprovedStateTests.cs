using SysGreen.Core.Abstractions;
using SysGreen.Core.Domain;
using SysGreen.Core.Startup;

namespace SysGreen.Tests;

/// <summary>
/// The decorator that turns the raw StartupApproved flags into each entry's enable/disable state,
/// so a disabled item reads back as disabled (and drops out of recommendations) — ADR-0005.
/// </summary>
public class StartupApprovedStateTests
{
    private static readonly DateTime When = new(2026, 6, 22, 0, 0, 0, DateTimeKind.Utc);

    private static AutostartEntry Run(string name) =>
        new($"HKCU:{name}", name, ItemKind.StartupApp, AutostartLocation.RegistryRunCurrentUser,
            $@"C:\x\{name}.exe", null, AutostartState.Enabled);

    [Fact]
    public void A_disabled_flag_is_reflected_as_disabled_state()
    {
        var store = new FakeStore();
        store.WriteFlag(AutostartLocation.RegistryRunCurrentUser, "Spotify",
            StartupApprovedFlag.EncodeDisabled(When));
        var provider = new StartupApprovedAutostartProvider(new FakeInner(Run("Spotify")), store);

        Assert.Equal(AutostartState.Disabled, Assert.Single(provider.Enumerate()).State);
    }

    [Fact]
    public void A_missing_flag_means_enabled()
    {
        var provider = new StartupApprovedAutostartProvider(new FakeInner(Run("Spotify")), new FakeStore());

        Assert.Equal(AutostartState.Enabled, Assert.Single(provider.Enumerate()).State);
    }

    [Fact]
    public void Folder_items_resolve_state_under_their_shortcut_file_name()
    {
        // A Startup-folder item displays "Spotify" but its flag is keyed by the shortcut file name.
        var folder = new AutostartEntry("Folder:Spotify", "Spotify", ItemKind.StartupApp,
            AutostartLocation.StartupFolderCurrentUser, @"C:\x\Spotify.exe", null, AutostartState.Enabled)
            { MechanismKey = "Spotify.lnk" };
        var store = new FakeStore();
        store.WriteFlag(AutostartLocation.StartupFolderCurrentUser, "Spotify.lnk",
            StartupApprovedFlag.EncodeDisabled(When));
        var provider = new StartupApprovedAutostartProvider(new FakeInner(folder), store);

        Assert.Equal(AutostartState.Disabled, Assert.Single(provider.Enumerate()).State);
    }

    [Fact]
    public void Locations_without_startup_approved_flags_are_left_untouched()
    {
        var task = new AutostartEntry("Task:Foo", "Foo", ItemKind.ScheduledTask,
            AutostartLocation.ScheduledTask, null, null, AutostartState.Enabled);
        var provider = new StartupApprovedAutostartProvider(new FakeInner(task), new ThrowingStore());

        Assert.Equal(AutostartState.Enabled, Assert.Single(provider.Enumerate()).State);
    }

    private sealed class FakeInner(params AutostartEntry[] entries) : IAutostartProvider
    {
        public IReadOnlyList<AutostartEntry> Enumerate() => entries;
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

    private sealed class ThrowingStore : IStartupApprovedStore
    {
        public byte[]? ReadFlag(AutostartLocation location, string valueName) =>
            throw new InvalidOperationException("must not consult the store for a non-flag location");
        public void WriteFlag(AutostartLocation location, string valueName, byte[] data) =>
            throw new InvalidOperationException();
    }
}
