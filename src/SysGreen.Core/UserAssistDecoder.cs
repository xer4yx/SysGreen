using System.Buffers.Binary;

namespace SysGreen.Core.Usage;

/// <summary>
/// Decodes Windows UserAssist registry entries: value names are ROT13-encoded, and the data blob
/// (Win7+) carries the run count at offset 4 and the last-run FILETIME at offset 60. See ADR-0008.
/// </summary>
public static class UserAssistDecoder
{
    private const int RunCountOffset = 4;
    private const int LastRunOffset = 60;

    /// <summary>ROT13 — letters rotated by 13, everything else unchanged (its own inverse).</summary>
    public static string DecodeName(string rot13Name) =>
        string.Create(rot13Name.Length, rot13Name, static (span, source) =>
        {
            for (var i = 0; i < source.Length; i++)
            {
                var c = source[i];
                span[i] = c switch
                {
                    >= 'a' and <= 'z' => (char)('a' + (c - 'a' + 13) % 26),
                    >= 'A' and <= 'Z' => (char)('A' + (c - 'A' + 13) % 26),
                    _ => c,
                };
            }
        });

    public static (int RunCount, DateTime? LastRunUtc) ParseRunData(ReadOnlySpan<byte> data)
    {
        if (data.Length < LastRunOffset + sizeof(long)) return (0, null);

        var runCount = BinaryPrimitives.ReadInt32LittleEndian(data.Slice(RunCountOffset, sizeof(int)));
        var fileTime = BinaryPrimitives.ReadInt64LittleEndian(data.Slice(LastRunOffset, sizeof(long)));
        var lastRun = fileTime > 0 ? DateTime.FromFileTimeUtc(fileTime) : (DateTime?)null;
        return (runCount, lastRun);
    }
}
