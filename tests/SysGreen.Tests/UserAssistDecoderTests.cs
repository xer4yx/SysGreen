using System.Buffers.Binary;
using SysGreen.Core.Usage;

namespace SysGreen.Tests;

public class UserAssistDecoderTests
{
    [Fact]
    public void DecodeName_applies_rot13() =>
        Assert.Equal("Program.exe", UserAssistDecoder.DecodeName("Cebtenz.rkr"));

    [Fact]
    public void DecodeName_is_its_own_inverse()
    {
        const string original = @"{GUID}\Spotify.exe";
        Assert.Equal(original, UserAssistDecoder.DecodeName(UserAssistDecoder.DecodeName(original)));
    }

    [Fact]
    public void ParseRunData_extracts_run_count_and_last_run()
    {
        var when = new DateTime(2026, 6, 20, 8, 0, 0, DateTimeKind.Utc);
        var data = new byte[72];
        BinaryPrimitives.WriteInt32LittleEndian(data.AsSpan(4), 7);
        BinaryPrimitives.WriteInt64LittleEndian(data.AsSpan(60), when.ToFileTimeUtc());

        var (count, lastRun) = UserAssistDecoder.ParseRunData(data);

        Assert.Equal(7, count);
        Assert.Equal(when, lastRun);
    }

    [Fact]
    public void ParseRunData_returns_empty_for_short_data()
    {
        var (count, lastRun) = UserAssistDecoder.ParseRunData(new byte[8]);

        Assert.Equal(0, count);
        Assert.Null(lastRun);
    }

    [Fact]
    public void ParseRunData_treats_zero_filetime_as_no_last_run()
    {
        var data = new byte[72];
        BinaryPrimitives.WriteInt32LittleEndian(data.AsSpan(4), 3); // run count only, no timestamp

        var (count, lastRun) = UserAssistDecoder.ParseRunData(data);

        Assert.Equal(3, count);
        Assert.Null(lastRun);
    }
}
