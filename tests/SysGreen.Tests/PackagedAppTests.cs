using SysGreen.Core.Knowledge;

namespace SysGreen.Tests;

public class PackagedAppTests
{
    [Theory]
    [InlineData(@"C:\Users\me\AppData\Local\Microsoft\WindowsApps\MSTeams_8wekyb3d8bbwe\ms-teams.exe", "MSTeams")]
    [InlineData(@"C:\Program Files\WindowsApps\Microsoft.WindowsTerminal_8wekyb3d8bbwe\wt.exe", "Microsoft.WindowsTerminal")]
    [InlineData(@"C:\Program Files\WindowsApps\SpotifyAB.SpotifyMusic_zpdnekdrzrea0\Spotify.exe", "SpotifyAB.SpotifyMusic")]
    public void Extracts_the_package_name_from_a_windowsapps_path(string path, string expected)
        => Assert.Equal(expected, PackagedApp.PackageName(path));

    [Theory]
    [InlineData(@"C:\Program Files\Acme\Acme.exe")]
    [InlineData(@"C:\Windows\System32\svchost.exe")]
    [InlineData(null)]
    [InlineData("")]
    public void Is_null_for_a_non_packaged_path(string? path)
        => Assert.Null(PackagedApp.PackageName(path));
}
