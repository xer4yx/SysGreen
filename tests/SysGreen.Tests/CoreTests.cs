using SysGreen.Core.Domain;
using SysGreen.Core.Knowledge;
using SysGreen.Core.Recommendations;
using SysGreen.Core.Usage;

namespace SysGreen.Tests;

public class RecommendationEngineTests
{
    private static readonly DateTime Now = new(2026, 6, 20, 0, 0, 0, DateTimeKind.Utc);
    private readonly RecommendationEngine _engine = new();

    private static ManageableItem Item(Purpose purpose, SafetyRating safety, string exe = @"C:\app\app.exe")
    {
        var entry = new AutostartEntry(
            "id", "Thing", ItemKind.StartupApp, AutostartLocation.RegistryRunCurrentUser,
            exe, null, AutostartState.Enabled);
        return new ManageableItem("id", "Thing", ItemKind.StartupApp, entry, null, purpose, safety, 20_000_000);
    }

    [Fact]
    public void Recommends_safe_overhead_even_with_no_usage()
    {
        var recs = _engine.Recommend([Item(Purpose.Updater, SafetyRating.Safe)], [], Now);

        Assert.Single(recs);
        Assert.Equal(RecommendationSource.Static, recs[0].Source);
    }

    [Fact]
    public void Safety_is_a_hard_gate()
    {
        // Overhead, but Caution — must never be recommended.
        var recs = _engine.Recommend([Item(Purpose.Updater, SafetyRating.Caution)], [], Now);

        Assert.Empty(recs);
    }

    [Fact]
    public void Recommends_abandoned_safe_app_via_habit()
    {
        var item = Item(Purpose.Media, SafetyRating.Safe);
        var usage = new[] { new UsageRecord(@"C:\app\app.exe", 5, Now.AddDays(-60)) };

        var recs = _engine.Recommend([item], usage, Now);

        Assert.Single(recs);
        Assert.Equal(RecommendationSource.Habit, recs[0].Source);
    }

    [Fact]
    public void Habit_matches_usage_by_executable_filename_not_full_path()
    {
        // Autostart records a full path; UserAssist records the same exe under a known-folder
        // GUID prefix. They must still correlate by filename (ADR-0008).
        var entry = new AutostartEntry("id", "Spotify", ItemKind.StartupApp,
            AutostartLocation.RegistryRunCurrentUser,
            @"C:\Users\me\AppData\Roaming\Spotify\Spotify.exe", null, AutostartState.Enabled);
        var item = new ManageableItem("id", "Spotify", ItemKind.StartupApp, entry, null,
            Purpose.Media, SafetyRating.Safe, null);
        var usage = new[]
        {
            new UsageRecord(@"{1AC14E77-02E7-4E5D-B744-2EB1AE5198B7}\Spotify.exe", 9, Now.AddDays(-60)),
        };

        var recs = _engine.Recommend([item], usage, Now);

        Assert.Single(recs);
        Assert.Equal(RecommendationSource.Habit, recs[0].Source);
    }

    [Fact]
    public void Does_not_recommend_recently_used_non_overhead_app()
    {
        var item = Item(Purpose.Media, SafetyRating.Safe);
        var usage = new[] { new UsageRecord(@"C:\app\app.exe", 5, Now.AddDays(-1)) };

        var recs = _engine.Recommend([item], usage, Now);

        Assert.Empty(recs);
    }
}

public class ClassifierTests
{
    private static AutostartEntry Entry(string exePath, string? publisher = null) =>
        new("id", "Thing", ItemKind.StartupApp, AutostartLocation.RegistryRunCurrentUser,
            exePath, publisher, AutostartState.Enabled);

    [Fact]
    public void Unknown_executable_defaults_to_caution()
    {
        var kb = new JsonKnowledgeBase(new KnowledgeBaseDocument(1, "test", []));
        var classification = new Classifier(kb).Classify(Entry(@"C:\x\mystery.exe"));

        Assert.Equal(Purpose.Unknown, classification.Purpose);
        Assert.Equal(SafetyRating.Caution, classification.Safety);
        Assert.Equal(ClassificationSource.Unknown, classification.Source);
    }

    [Fact]
    public void Falls_back_to_heuristics_when_the_knowledge_base_misses()
    {
        var kb = new JsonKnowledgeBase(new KnowledgeBaseDocument(1, "test", []));

        var c = new Classifier(kb).Classify(Entry(@"C:\Program Files (x86)\Google\Update\GoogleUpdate.exe"));

        Assert.Equal(Purpose.Updater, c.Purpose);
        Assert.Equal(SafetyRating.Safe, c.Safety);
        Assert.Equal(ClassificationSource.Heuristic, c.Source);
    }

    [Fact]
    public void Knowledge_base_wins_over_a_heuristic_match()
    {
        // GoogleUpdate would heuristically read as Updater/Safe; a curated entry must take precedence.
        var entry = new KnowledgeEntry("Google LLC", "GoogleUpdate.exe", null, "Google Updater",
            "Keeps Google software current", Purpose.Updater, SafetyRating.Caution, null, true);
        var kb = new JsonKnowledgeBase(new KnowledgeBaseDocument(1, "test", [entry]));

        var c = new Classifier(kb).Classify(
            Entry(@"C:\Program Files (x86)\Google\Update\GoogleUpdate.exe", "Google LLC"));

        Assert.Equal(ClassificationSource.KnowledgeBase, c.Source);
        Assert.Equal(SafetyRating.Caution, c.Safety); // the curated value, not the heuristic's Safe
    }

    [Fact]
    public void Matches_knowledge_base_entry_by_executable_name()
    {
        var entry = new KnowledgeEntry(
            "Spotify AB", "Spotify.exe", null, "Spotify", "Media auto-launcher",
            Purpose.Media, SafetyRating.Safe, 398458880, false);
        var kb = new JsonKnowledgeBase(new KnowledgeBaseDocument(1, "test", [entry]));

        var classification = new Classifier(kb).Classify(Entry(@"C:\Spotify\Spotify.exe", "Spotify AB"));

        Assert.Equal(Purpose.Media, classification.Purpose);
        Assert.Equal(SafetyRating.Safe, classification.Safety);
        Assert.Equal(ClassificationSource.KnowledgeBase, classification.Source);
    }

    [Fact]
    public void Surfaces_the_kb_typical_ram_for_the_estimate_chain()
    {
        var kbEntry = new KnowledgeEntry("Spotify AB", "Spotify.exe", null, "Spotify", "Media auto-launcher",
            Purpose.Media, SafetyRating.Safe, 398458880, false);
        var kb = new JsonKnowledgeBase(new KnowledgeBaseDocument(1, "test", [kbEntry]));

        var c = new Classifier(kb).Classify(Entry(@"C:\Spotify\Spotify.exe", "Spotify AB"));

        Assert.Equal(398458880L, c.TypicalRamBytes);
    }

    [Fact]
    public void Has_no_typical_ram_when_not_matched_in_the_kb()
    {
        var kb = new JsonKnowledgeBase(new KnowledgeBaseDocument(1, "test", []));

        var c = new Classifier(kb).Classify(Entry(@"C:\x\mystery.exe"));

        Assert.Null(c.TypicalRamBytes);
    }

    [Fact]
    public void Classifies_a_store_app_by_package_name_when_the_publisher_is_unreadable()
    {
        // The WindowsApps stub can't be read for a publisher, but the package name (MSTeams) identifies it.
        var teams = new KnowledgeEntry(null, "MSTeams", null, "Microsoft Teams (Store)",
            "Teams, packaged", Purpose.Communication, SafetyRating.Safe, null, false);
        var kb = new JsonKnowledgeBase(new KnowledgeBaseDocument(1, "test", [teams]));
        var entry = Entry(
            @"C:\Users\me\AppData\Local\Microsoft\WindowsApps\MSTeams_8wekyb3d8bbwe\ms-teams.exe");

        var c = new Classifier(kb).Classify(entry);

        Assert.Equal(Purpose.Communication, c.Purpose);
        Assert.Equal(ClassificationSource.KnowledgeBase, c.Source);
    }

    [Fact]
    public void Matches_via_process_start_target_when_launcher_is_an_updater()
    {
        // Discord's Run entry launches Update.exe (signed by Discord Inc.) which starts Discord.exe.
        var discord = new KnowledgeEntry("Discord Inc.", "Discord.exe", null,
            "Discord", "Chat client", Purpose.Communication, SafetyRating.Safe, null, false);
        var kb = new JsonKnowledgeBase(new KnowledgeBaseDocument(1, "test", [discord]));
        var entry = new AutostartEntry("id", "Discord", ItemKind.StartupApp,
            AutostartLocation.RegistryRunCurrentUser,
            @"C:\Users\me\AppData\Local\Discord\Update.exe", "Discord Inc.", AutostartState.Enabled)
        {
            TargetExecutable = "Discord.exe",
        };

        var classification = new Classifier(kb).Classify(entry);

        Assert.Equal(Purpose.Communication, classification.Purpose);
        Assert.Equal(ClassificationSource.KnowledgeBase, classification.Source);
    }
}
