using SysGreen.App.Services;

namespace SysGreen.Tests;

public class VelopackUninstallerTests
{
    [Fact]
    public void Update_exe_resolves_to_the_install_root_next_to_current()
    {
        // Velopack runs the app from <root>\current\ and keeps Update.exe in <root> (verified on a
        // real install). The uninstaller is launched as Update.exe --uninstall.
        var path = VelopackUninstaller.UpdateExePathFor(@"C:\X\SysGreen\current\");

        Assert.Equal(@"C:\X\SysGreen\Update.exe", path);
    }
}
