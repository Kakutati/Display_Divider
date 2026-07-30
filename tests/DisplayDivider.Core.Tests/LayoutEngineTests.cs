using DisplayDivider.Core;
using Xunit;

namespace DisplayDivider.Core.Tests;

public class LayoutEngineTests
{
    [Fact]
    public void Columns_TwoEqual_ProducesHalves()
    {
        var zones = LayoutEngine.Columns(1, 1);

        Assert.Equal(2, zones.Count);
        Assert.Equal(0.0, zones[0].X, 10);
        Assert.Equal(0.5, zones[0].Width, 10);
        Assert.Equal(0.5, zones[1].X, 10);
        Assert.Equal(1.0, zones[0].Height, 10);
    }

    [Fact]
    public void Columns_SeventyThirty_NormalizesWeights()
    {
        var zones = LayoutEngine.Columns(70, 30);

        Assert.Equal(0.7, zones[0].Width, 10);
        Assert.Equal(0.3, zones[1].Width, 10);
        Assert.Equal(0.7, zones[1].X, 10);
    }

    [Fact]
    public void Grid_TwoByTwo_IsRowMajorOrder()
    {
        var zones = LayoutEngine.Grid(2, 2);

        Assert.Equal(4, zones.Count);
        // 행 우선: [0]=좌상, [1]=우상, [2]=좌하, [3]=우하
        Assert.Equal(0.0, zones[0].X, 10);
        Assert.Equal(0.0, zones[0].Y, 10);
        Assert.Equal(0.5, zones[1].X, 10);
        Assert.Equal(0.0, zones[1].Y, 10);
        Assert.Equal(0.0, zones[2].X, 10);
        Assert.Equal(0.5, zones[2].Y, 10);
    }

    [Fact]
    public void ToPixels_OddWidth_AdjacentZonesShareEdgeWithoutGap()
    {
        var monitor = new PixelRect(0, 0, 1919, 1079);
        var zones = LayoutEngine.Columns(1, 1);

        var left = LayoutEngine.ToPixels(zones[0], monitor);
        var right = LayoutEngine.ToPixels(zones[1], monitor);

        Assert.Equal(0, left.X);
        Assert.Equal(left.Right, right.X);       // 틈/겹침 없음
        Assert.Equal(monitor.Right, right.Right); // 전체 폭을 정확히 덮음
    }

    [Fact]
    public void ToPixels_MonitorWithNegativeOrigin_OffsetsCorrectly()
    {
        // 보조 모니터가 기본 모니터 왼쪽에 있으면 음수 좌표를 가진다
        var monitor = new PixelRect(-2560, 0, 2560, 1440);
        var zones = LayoutEngine.Columns(1, 1);

        var left = LayoutEngine.ToPixels(zones[0], monitor);

        Assert.Equal(-2560, left.X);
        Assert.Equal(1280, left.Width);
    }

    [Fact]
    public void FindZoneIndex_ReturnsContainingZone()
    {
        var monitor = new PixelRect(0, 0, 1920, 1080);
        var zones = LayoutEngine.Columns(1, 1);

        Assert.Equal(0, LayoutEngine.FindZoneIndex(zones, monitor, 100, 100));
        Assert.Equal(1, LayoutEngine.FindZoneIndex(zones, monitor, 1500, 100));
        Assert.Equal(-1, LayoutEngine.FindZoneIndex(zones, monitor, 5000, 100));
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(-1.0)]
    public void FromWeights_NonPositiveWeight_Throws(double bad)
    {
        Assert.Throws<ArgumentException>(() => LayoutEngine.Columns(1, bad));
    }

    [Fact]
    public void ParseWeights_ParsesCommaSeparatedValues()
    {
        var weights = LayoutEngine.ParseWeights(" 70, 30 ");

        Assert.Equal(new[] { 70.0, 30.0 }, weights);
    }

    [Fact]
    public void ParseWeights_InvalidText_Throws()
    {
        Assert.Throws<FormatException>(() => LayoutEngine.ParseWeights("abc"));
        Assert.Throws<FormatException>(() => LayoutEngine.ParseWeights(""));
    }
}
