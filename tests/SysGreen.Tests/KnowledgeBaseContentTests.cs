using System.IO;
using SysGreen.Core.Domain;
using SysGreen.Core.Knowledge;

namespace SysGreen.Tests;

/// <summary>
/// Guards the shipped Knowledge Base data (ADR-0010): that it has grown to a curated set, stays
/// well-formed and conservative on Safety, and recognises a few representative real-world items.
/// </summary>
public class KnowledgeBaseContentTests
{
    private static readonly IKnowledgeBase Kb = JsonKnowledgeBase.LoadFromFile(
        Path.Combine(AppContext.BaseDirectory, "knowledge-base.json"));

    [Fact]
    public void The_shipped_kb_has_grown_to_a_curated_set()
    {
        Assert.True(Kb.Document.Entries.Count >= 60,
            $"expected a curated KB (>=60 entries), found {Kb.Document.Entries.Count}");
    }

    [Fact]
    public void Has_no_duplicate_publisher_plus_executable_pairs()
    {
        var keys = Kb.Document.Entries.Select(e =>
            $"{PublisherName.Normalize(e.MatchPublisher)?.ToLowerInvariant()}|" +
            $"{e.MatchExecutable.ToLowerInvariant().Replace(".exe", "")}").ToList();

        Assert.Equal(keys.Count, keys.Distinct().Count());
    }

    [Fact]
    public void Every_entry_is_well_formed()
    {
        Assert.All(Kb.Document.Entries, e =>
        {
            Assert.False(string.IsNullOrWhiteSpace(e.MatchExecutable));
            Assert.False(string.IsNullOrWhiteSpace(e.DisplayName));
            Assert.False(string.IsNullOrWhiteSpace(e.Description));
            Assert.True(e.TypicalRamBytes is null or > 0);
        });
    }

    [Fact]
    public void Security_software_is_never_marked_safe()
    {
        // A wrong "Safe" on protective software is the dangerous error (ADR-0010 conservative bias).
        Assert.All(Kb.Document.Entries.Where(e => e.Purpose == Purpose.Security),
            e => Assert.NotEqual(SafetyRating.Safe, e.Safety));
    }

    [Theory]
    [InlineData("OfficeC2RClient.exe", "Microsoft Corporation", Purpose.Updater, SafetyRating.Safe)]
    [InlineData("MicrosoftEdgeUpdate.exe", "Microsoft Corporation", Purpose.Updater, SafetyRating.Safe)]
    [InlineData("Slack.exe", "Slack Technologies, Inc.", Purpose.Communication, SafetyRating.Safe)]
    [InlineData("EpicGamesLauncher.exe", "Epic Games, Inc.", Purpose.Gaming, SafetyRating.Safe)]
    [InlineData("vgtray.exe", "Riot Games, Inc.", Purpose.Security, SafetyRating.DoNotTouch)]
    public void Classifies_representative_real_world_items(
        string exe, string publisher, Purpose purpose, SafetyRating safety)
    {
        var hit = Kb.Match(exe, publisher, null);

        Assert.NotNull(hit);
        Assert.Equal(purpose, hit!.Purpose);
        Assert.Equal(safety, hit.Safety);
    }

    [Fact]
    public void Recognises_a_packaged_store_app_by_its_package_name()
    {
        // The classifier passes the package name for a WindowsApps app whose exe stub can't be read.
        var hit = Kb.Match("MSTeams", publisher: null,
            fullPath: @"C:\Users\me\AppData\Local\Microsoft\WindowsApps\MSTeams_8wekyb3d8bbwe\ms-teams.exe");

        Assert.NotNull(hit);
        Assert.Equal(Purpose.Communication, hit!.Purpose);
        Assert.Equal(SafetyRating.Safe, hit.Safety);
    }

    [Fact]
    public void Cloud_sync_apps_are_flagged_as_providing_passive_value()
    {
        // So the habit engine never recommends disabling something that works while its window is closed.
        var dropbox = Kb.Match("Dropbox.exe", "Dropbox, Inc.", null);

        Assert.NotNull(dropbox);
        Assert.True(dropbox!.ProvidesPassiveValue);
    }
}
