using SysGreen.Core.Usage;

namespace SysGreen.Tests;

public class RamEstimateTests
{
    [Fact]
    public void Prefers_the_live_working_set_when_running()
    {
        var e = RamEstimate.Resolve(live: 100, historicalMedian: 200, knowledgeBaseTypical: 300);
        Assert.Equal(100L, e.Bytes);
        Assert.Equal(RamEstimateSource.Live, e.Source);
    }

    [Fact]
    public void Falls_back_to_the_historical_median_when_not_running()
    {
        var e = RamEstimate.Resolve(live: null, historicalMedian: 200, knowledgeBaseTypical: 300);
        Assert.Equal(200L, e.Bytes);
        Assert.Equal(RamEstimateSource.Historical, e.Source);
    }

    [Fact]
    public void Falls_back_to_the_kb_typical_when_no_live_or_median()
    {
        var e = RamEstimate.Resolve(live: null, historicalMedian: null, knowledgeBaseTypical: 300);
        Assert.Equal(300L, e.Bytes);
        Assert.Equal(RamEstimateSource.KnowledgeBase, e.Source);
    }

    [Fact]
    public void Is_unknown_when_no_source_has_a_value()
    {
        var e = RamEstimate.Resolve(live: null, historicalMedian: null, knowledgeBaseTypical: null);
        Assert.Null(e.Bytes);
        Assert.Equal(RamEstimateSource.Unknown, e.Source);
        Assert.False(e.IsKnown);
    }

    [Fact]
    public void Treats_a_zero_or_negative_value_as_no_value_and_falls_through()
    {
        var e = RamEstimate.Resolve(live: 0, historicalMedian: null, knowledgeBaseTypical: 300);
        Assert.Equal(300L, e.Bytes);
        Assert.Equal(RamEstimateSource.KnowledgeBase, e.Source);
    }

    [Theory]
    [InlineData(20971520L, "20 MB")]
    [InlineData(398458880L, "380 MB")]
    [InlineData(1610612736L, "1.5 GB")]
    public void Formats_coarsely_in_mb_or_gb(long bytes, string expected)
        => Assert.Equal(expected, RamEstimate.Format(bytes));
}
