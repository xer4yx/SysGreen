using SysGreen.Core.Domain;
using SysGreen.Core.Knowledge;

namespace SysGreen.Tests;

public class KnowledgeBaseMatchTests
{
    [Fact]
    public void Match_succeeds_when_the_publisher_is_a_full_x500_subject()
    {
        // The KB stores the plain publisher; the OS provides a full Authenticode subject.
        var entry = new KnowledgeEntry("Microsoft Corporation", "OneDrive.exe", null,
            "OneDrive", "Syncs files", Purpose.Productivity, SafetyRating.Caution, null, true);
        var kb = new JsonKnowledgeBase(new KnowledgeBaseDocument(1, "test", [entry]));

        var hit = kb.Match("OneDrive.exe",
            "CN=Microsoft Corporation, O=Microsoft Corporation, L=Redmond, S=Washington, C=US",
            @"C:\x\OneDrive.exe");

        Assert.NotNull(hit);
        Assert.Equal("OneDrive", hit!.DisplayName);
    }

    [Fact]
    public void Match_still_rejects_a_different_publisher()
    {
        var entry = new KnowledgeEntry("Microsoft Corporation", "OneDrive.exe", null,
            "OneDrive", "Syncs files", Purpose.Productivity, SafetyRating.Caution, null, true);
        var kb = new JsonKnowledgeBase(new KnowledgeBaseDocument(1, "test", [entry]));

        var hit = kb.Match("OneDrive.exe", "CN=Totally Different Co, O=Evil", @"C:\x\OneDrive.exe");

        Assert.Null(hit);
    }
}
