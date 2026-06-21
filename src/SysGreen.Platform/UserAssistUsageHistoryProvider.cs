using Microsoft.Win32;
using SysGreen.Core.Usage;

namespace SysGreen.Platform;

/// <summary>
/// Seeds the habit engine from existing Windows launch history (ADR-0008). Reads the HKCU
/// UserAssist Count subkeys, ROT13-decodes the value names, and parses run count + last-run via
/// the tested <see cref="UserAssistDecoder"/>. Humble object (registry I/O); the decode/parse is tested.
/// </summary>
public sealed class UserAssistUsageHistoryProvider : IUsageHistoryProvider
{
    private const string UserAssistKey =
        @"Software\Microsoft\Windows\CurrentVersion\Explorer\UserAssist";

    public IReadOnlyList<UsageRecord> ReadSeedHistory()
    {
        var records = new List<UsageRecord>();
        using var root = Registry.CurrentUser.OpenSubKey(UserAssistKey);
        if (root is null) return records;

        foreach (var guid in root.GetSubKeyNames())
        {
            using var count = root.OpenSubKey($@"{guid}\Count");
            if (count is null) continue;

            foreach (var encodedName in count.GetValueNames())
            {
                if (count.GetValue(encodedName) is not byte[] data) continue;

                var name = UserAssistDecoder.DecodeName(encodedName);
                if (!name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)) continue;

                var (runCount, lastRun) = UserAssistDecoder.ParseRunData(data);
                records.Add(new UsageRecord(name, runCount, lastRun));
            }
        }
        return records;
    }
}
