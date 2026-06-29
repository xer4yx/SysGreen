using SysGreen.App.Services;

namespace SysGreen.Tests;

public class FilePolicyProviderTests
{
    [Fact]
    public void Parses_the_policy_version_from_the_header()
    {
        var text = "# SysGreen Privacy Policy & Terms of Use\n\n**Policy version:** 3\n\nBody text.";

        Assert.Equal(3, FilePolicyProvider.ParseVersion(text));
    }

    [Fact]
    public void Defaults_to_zero_when_no_version_marker_is_present()
    {
        Assert.Equal(0, FilePolicyProvider.ParseVersion("Some policy text without a version line."));
    }

    [Fact]
    public void The_shipped_policy_is_version_one()
    {
        // Guards the policy.md ↔ code version contract: bump both together.
        Assert.Equal(1, FilePolicyProvider.ParseVersion(File.ReadAllText(
            Path.Combine(AppContext.BaseDirectory, "policy.md"))));
    }
}
