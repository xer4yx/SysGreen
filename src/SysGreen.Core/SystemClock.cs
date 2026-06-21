using SysGreen.Core.Abstractions;

namespace SysGreen.Core;

/// <summary>The real system clock used in production.</summary>
public sealed class SystemClock : IClock
{
    public DateTime UtcNow => DateTime.UtcNow;
}
