namespace SysGreen.Core.Startup;

/// <summary>
/// Encodes/decodes the Windows "StartupApproved" flag — the 12-byte REG_BINARY value Task Manager
/// writes to non-destructively enable/disable a startup item. Using this (instead of deleting the
/// Run value) is what makes SysGreen's disable reversible by construction (ADR-0005).
/// </summary>
public static class StartupApprovedFlag
{
    public const int Length = 12;

    private const byte EnabledMarker = 0x02;
    private const byte DisabledMarker = 0x03;

    /// <summary>
    /// A flag is "disabled" when the low bit of the first byte is set. A missing/empty value
    /// means the item has never been toggled, which Windows treats as enabled.
    /// </summary>
    public static bool IsEnabled(ReadOnlySpan<byte> data) =>
        data.IsEmpty || (data[0] & 0x01) == 0;

    public static byte[] EncodeEnabled()
    {
        var data = new byte[Length];
        data[0] = EnabledMarker;
        return data;
    }

    public static byte[] EncodeDisabled(DateTime disabledAtUtc)
    {
        var data = new byte[Length];
        data[0] = DisabledMarker;
        // Bytes 4..11 hold the disable time as a little-endian FILETIME (as Task Manager records).
        System.Buffers.Binary.BinaryPrimitives.WriteInt64LittleEndian(
            data.AsSpan(4), disabledAtUtc.ToFileTimeUtc());
        return data;
    }
}
