using SysGreen.Core.Startup;

namespace SysGreen.Tests;

public class StartupApprovedFlagTests
{
    [Fact]
    public void Enabled_byte_is_reported_enabled() =>
        Assert.True(StartupApprovedFlag.IsEnabled(new byte[] { 0x02, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 }));

    [Fact]
    public void Disabled_byte_is_reported_disabled() =>
        Assert.False(StartupApprovedFlag.IsEnabled(new byte[] { 0x03, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 }));

    [Fact]
    public void Missing_flag_defaults_to_enabled() =>
        Assert.True(StartupApprovedFlag.IsEnabled(Array.Empty<byte>()));

    [Fact]
    public void EncodeEnabled_is_twelve_bytes_and_reads_as_enabled()
    {
        var data = StartupApprovedFlag.EncodeEnabled();

        Assert.Equal(12, data.Length);
        Assert.True(StartupApprovedFlag.IsEnabled(data));
    }

    [Fact]
    public void EncodeDisabled_is_twelve_bytes_reads_disabled_and_embeds_timestamp()
    {
        var when = new DateTime(2026, 6, 20, 8, 30, 0, DateTimeKind.Utc);

        var data = StartupApprovedFlag.EncodeDisabled(when);

        Assert.Equal(12, data.Length);
        Assert.False(StartupApprovedFlag.IsEnabled(data));
        // The disable timestamp is stored as a FILETIME at offset 4 (matches Task Manager).
        Assert.Equal(when, DateTime.FromFileTimeUtc(BitConverter.ToInt64(data, 4)));
    }
}
