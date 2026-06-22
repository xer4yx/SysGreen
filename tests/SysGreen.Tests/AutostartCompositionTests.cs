using SysGreen.Core.Abstractions;
using SysGreen.Core.Domain;
using SysGreen.Core.Startup;

namespace SysGreen.Tests;

public class AutostartCompositionTests
{
    private static AutostartEntry Run(string name) =>
        new($"HKCU:{name}", name, ItemKind.StartupApp, AutostartLocation.RegistryRunCurrentUser,
            $@"C:\x\{name}.exe", null, AutostartState.Enabled);

    [Fact]
    public void Composite_combines_entries_from_every_provider_in_order()
    {
        var composite = new CompositeAutostartProvider(
            new FakeProvider(Run("A")),
            new FakeProvider(Run("B"), Run("C")));

        var names = composite.Enumerate().Select(e => e.DisplayName).ToList();

        Assert.Equal(new[] { "A", "B", "C" }, names);
    }

    [Fact]
    public void Folder_entry_is_keyed_by_shortcut_file_name_with_a_friendly_display_name()
    {
        var entry = StartupFolderEntry.Create(
            @"C:\Users\me\AppData\Roaming\Microsoft\Windows\Start Menu\Programs\Startup\Spotify.lnk",
            AutostartLocation.StartupFolderCurrentUser, @"C:\x\Spotify.exe", "Spotify AB");

        Assert.Equal("Spotify", entry.DisplayName);          // friendly: no ".lnk"
        Assert.Equal("Spotify.lnk", entry.StartupApprovedValueName); // flag is keyed by the link file name
        Assert.Equal(AutostartLocation.StartupFolderCurrentUser, entry.Location);
        Assert.Equal(ItemKind.StartupApp, entry.Kind);
        Assert.Equal(@"C:\x\Spotify.exe", entry.ExecutablePath);
        Assert.Equal("Spotify AB", entry.Publisher);
    }

    [Fact]
    public void Folder_entry_id_is_distinct_per_location_so_per_user_and_common_do_not_collide()
    {
        var user = StartupFolderEntry.Create(
            @"C:\u\Startup\OneDrive.lnk", AutostartLocation.StartupFolderCurrentUser, null, null);
        var common = StartupFolderEntry.Create(
            @"C:\pd\Startup\OneDrive.lnk", AutostartLocation.StartupFolderCommon, null, null);

        Assert.NotEqual(user.Id, common.Id);
    }

    private sealed class FakeProvider(params AutostartEntry[] entries) : IAutostartProvider
    {
        public IReadOnlyList<AutostartEntry> Enumerate() => entries;
    }
}
