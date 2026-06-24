using SysGreen.Core;

namespace SysGreen.Tests;

/// <summary>
/// The display version comes from the assembly's <c>AssemblyInformationalVersion</c> (sourced from
/// &lt;Version&gt; in Directory.Build.props — ADR-0015). This formats it for the UI: prefix <c>v</c>,
/// drop the <c>+buildmetadata</c> the .NET SDK appends in a git repo (and that future automation
/// would use), and degrade gracefully when nothing is stamped.
/// </summary>
public class AppVersionTests
{
    [Theory]
    [InlineData("0.20.0", "v0.20.0")]
    [InlineData("1.2.3", "v1.2.3")]
    public void Prefixes_a_clean_semantic_version_with_v(string raw, string expected) =>
        Assert.Equal(expected, AppVersion.Format(raw));

    [Fact]
    public void Strips_build_metadata_after_the_plus() =>
        Assert.Equal("v0.20.0", AppVersion.Format("0.20.0+a1b2c3d4e5"));

    [Fact]
    public void Keeps_prerelease_label_but_drops_metadata() =>
        Assert.Equal("v0.20.0-beta.1", AppVersion.Format("0.20.0-beta.1+abc123"));

    [Fact]
    public void Trims_surrounding_whitespace() =>
        Assert.Equal("v0.20.0", AppVersion.Format("  0.20.0  "));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Returns_empty_when_no_version_is_available(string? raw) =>
        Assert.Equal("", AppVersion.Format(raw));
}
