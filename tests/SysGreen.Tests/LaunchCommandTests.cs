using SysGreen.Core.Domain;

namespace SysGreen.Tests;

public class LaunchCommandTests
{
    [Fact]
    public void Parses_squirrel_launcher_and_process_start_target()
    {
        var (launcher, target) = LaunchCommand.Parse(
            @"""C:\Users\me\AppData\Local\Discord\Update.exe"" --processStart Discord.exe");

        Assert.Equal(@"C:\Users\me\AppData\Local\Discord\Update.exe", launcher);
        Assert.Equal("Discord.exe", target);
    }

    [Fact]
    public void Parses_plain_command_with_args_and_no_target()
    {
        var (launcher, target) = LaunchCommand.Parse(@"C:\Program Files\OneDrive\OneDrive.exe /background");

        Assert.Equal(@"C:\Program Files\OneDrive\OneDrive.exe", launcher);
        Assert.Null(target);
    }

    [Fact]
    public void Parses_quoted_command_with_no_args()
    {
        var (launcher, target) = LaunchCommand.Parse(@"""C:\App\app.exe""");

        Assert.Equal(@"C:\App\app.exe", launcher);
        Assert.Null(target);
    }

    [Fact]
    public void Process_start_flag_is_case_insensitive_and_accepts_equals()
    {
        Assert.Equal("Foo.exe", LaunchCommand.Parse(@"Update.exe --processstart Foo.exe").TargetExecutable);
        Assert.Equal("Bar.exe", LaunchCommand.Parse(@"Update.exe --processStart=Bar.exe").TargetExecutable);
    }

    [Fact]
    public void Null_or_blank_command_yields_nulls()
    {
        Assert.Equal((null, null), LaunchCommand.Parse(null));
        Assert.Equal((null, null), LaunchCommand.Parse("   "));
    }
}
