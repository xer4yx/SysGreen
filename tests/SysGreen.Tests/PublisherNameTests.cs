using SysGreen.Core.Knowledge;

namespace SysGreen.Tests;

public class PublisherNameTests
{
    [Theory]
    [InlineData("CN=Spotify AB, O=Spotify AB, L=Stockholm, C=SE", "Spotify AB")]
    [InlineData("CN=Microsoft Corporation, O=Microsoft Corporation, L=Redmond, S=Washington, C=US", "Microsoft Corporation")]
    [InlineData("Discord Inc.", "Discord Inc.")]              // already a plain name
    [InlineData("  Valve Corp.  ", "Valve Corp.")]            // trimmed
    public void Normalize_extracts_the_common_name(string raw, string expected) =>
        Assert.Equal(expected, PublisherName.Normalize(raw));

    [Fact]
    public void Normalize_handles_a_quoted_cn_containing_a_comma() =>
        Assert.Equal("Acme, Inc.", PublisherName.Normalize("CN=\"Acme, Inc.\", O=Acme"));

    [Fact]
    public void Normalize_returns_null_for_null_input() =>
        Assert.Null(PublisherName.Normalize(null));
}
