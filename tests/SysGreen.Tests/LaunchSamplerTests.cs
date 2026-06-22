using SysGreen.Core.Abstractions;
using SysGreen.Core.Usage;

namespace SysGreen.Tests;

public class LaunchSamplerTests
{
    private static readonly DateTime When = new(2026, 6, 23, 8, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Records_a_user_app_launch_when_tracking_is_on()
    {
        var recorder = new FakeRecorder();
        new LaunchSampler(recorder, new FakeSettings(true), new FixedClock(When))
            .OnProcessStarted(@"C:\Users\me\AppData\Local\Discord\Discord.exe");

        var (path, when) = Assert.Single(recorder.Launches);
        Assert.Equal(@"C:\Users\me\AppData\Local\Discord\Discord.exe", path);
        Assert.Equal(When, when);
    }

    [Fact]
    public void Records_nothing_when_tracking_is_off()
    {
        var recorder = new FakeRecorder();
        new LaunchSampler(recorder, new FakeSettings(false), new FixedClock(When))
            .OnProcessStarted(@"C:\Apps\Thing.exe");

        Assert.Empty(recorder.Launches);
    }

    [Fact]
    public void Ignores_windows_system_processes()
    {
        var recorder = new FakeRecorder();
        new LaunchSampler(recorder, new FakeSettings(true), new FixedClock(When))
            .OnProcessStarted(@"C:\Windows\System32\svchost.exe");

        Assert.Empty(recorder.Launches);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(@"C:\Apps\thing.dll")]
    public void Ignores_paths_that_are_not_a_user_executable(string? path)
    {
        var recorder = new FakeRecorder();
        new LaunchSampler(recorder, new FakeSettings(true), new FixedClock(When)).OnProcessStarted(path);

        Assert.Empty(recorder.Launches);
    }

    private sealed class FakeRecorder : ILaunchRecorder
    {
        public List<(string Path, DateTime When)> Launches { get; } = [];
        public void RecordLaunch(string executablePath, DateTime whenUtc) => Launches.Add((executablePath, whenUtc));
    }

    private sealed class FakeSettings(bool enabled) : ITrackingSettings
    {
        public bool LaunchTrackingEnabled { get; } = enabled;
        public void SetLaunchTrackingEnabled(bool e) { }
    }

    private sealed class FixedClock(DateTime now) : IClock
    {
        public DateTime UtcNow { get; } = now;
    }
}
