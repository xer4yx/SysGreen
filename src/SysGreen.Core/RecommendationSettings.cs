namespace SysGreen.Core.Usage;

/// <summary>
/// User-adjustable inputs to the recommendation engine. Currently the <b>Abandoned</b> threshold
/// (CONTEXT.md): the number of days without a launch after which an app is a habit-based candidate
/// for disabling. Defaults to 30 days.
/// </summary>
public interface IThresholdSettings
{
    int AbandonedThresholdDays { get; }
    void SetAbandonedThresholdDays(int days);
}
