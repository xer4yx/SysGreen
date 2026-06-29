using SysGreen.Core.Domain;
using SysGreen.Core.Recommendations;
using SysGreen.Core.Usage;

namespace SysGreen.Tests;

public class RecommendationEngineThresholdTests
{
    private static readonly DateTime Now = new(2026, 6, 29, 0, 0, 0, DateTimeKind.Utc);

    // A safe, user-facing (non-overhead) startup app — so it's recommended only on the habit signal,
    // which is exactly what the abandoned threshold gates.
    private static ManageableItem HabitOnlyItem()
    {
        var entry = new AutostartEntry("id", "Game", ItemKind.StartupApp,
            AutostartLocation.RegistryRunCurrentUser, @"C:\x\game.exe", null, AutostartState.Enabled);
        return new ManageableItem("id", "Game", ItemKind.StartupApp, entry, null,
            Purpose.Media, SafetyRating.Safe, 100);
    }

    [Fact]
    public void Reads_the_abandoned_threshold_live_on_each_recommend()
    {
        var threshold = 30;
        var engine = new RecommendationEngine(() => threshold);
        var item = HabitOnlyItem();
        var usage = new[] { new UsageRecord(@"C:\x\game.exe", 5, Now.AddDays(-40)) }; // last used 40 days ago

        // 40 days >= 30 → abandoned → recommended.
        Assert.NotEmpty(engine.Recommend([item], usage, Now));

        threshold = 60; // user widens the window

        // 40 days < 60 → no longer abandoned, and it has no static evidence → not recommended.
        Assert.Empty(engine.Recommend([item], usage, Now));
    }
}
