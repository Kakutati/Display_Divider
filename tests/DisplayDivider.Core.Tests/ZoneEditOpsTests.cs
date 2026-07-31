using DisplayDivider.Core;
using Xunit;

namespace DisplayDivider.Core.Tests;

public class ZoneEditOpsTests
{
    [Fact]
    public void SplitHorizontal_ReplacesZoneWithTwoHalves()
    {
        var zones = new List<FractionRect> { ZoneEditOps.FullZone };

        bool ok = ZoneEditOps.SplitHorizontal(zones, 0);

        Assert.True(ok);
        Assert.Equal(2, zones.Count);
        Assert.Equal(0.0, zones[0].X, 10);
        Assert.Equal(0.5, zones[0].Width, 10);
        Assert.Equal(0.5, zones[1].X, 10);
        Assert.Equal(0.5, zones[1].Width, 10);
        Assert.Equal(1.0, zones[0].Height, 10);
    }

    [Fact]
    public void SplitVertical_ReplacesZoneWithTopAndBottom()
    {
        var zones = new List<FractionRect> { ZoneEditOps.FullZone };

        bool ok = ZoneEditOps.SplitVertical(zones, 0);

        Assert.True(ok);
        Assert.Equal(2, zones.Count);
        Assert.Equal(0.0, zones[0].Y, 10);
        Assert.Equal(0.5, zones[0].Height, 10);
        Assert.Equal(0.5, zones[1].Y, 10);
    }

    [Fact]
    public void Split_TooSmallZone_IsRejected()
    {
        var zones = new List<FractionRect> { new(0, 0, ZoneEditOps.MinSize * 1.5, 1) };

        bool ok = ZoneEditOps.SplitHorizontal(zones, 0);

        Assert.False(ok);
        Assert.Single(zones);
    }

    [Fact]
    public void MoveVerticalEdge_MovesBothSidesOfSharedEdge()
    {
        var zones = LayoutEngine.Columns(1, 1); // 경계 x=0.5

        double applied = ZoneEditOps.MoveVerticalEdge(zones, 0.5, 0.7);

        Assert.Equal(0.7, applied, 10);
        Assert.Equal(0.7, zones[0].Width, 10);
        Assert.Equal(0.7, zones[1].X, 10);
        Assert.Equal(0.3, zones[1].Width, 10);
    }

    [Fact]
    public void MoveVerticalEdge_SpanningMultipleRows_MovesAllAdjacentZones()
    {
        var zones = LayoutEngine.Grid(2, 2); // 세로 경계 x=0.5가 두 행에 걸침

        ZoneEditOps.MoveVerticalEdge(zones, 0.5, 0.6);

        // 4개 존 전부 새 경계를 공유해야 타일이 유지된다
        Assert.Equal(0.6, zones[0].Width, 10); // 좌상
        Assert.Equal(0.6, zones[1].X, 10);     // 우상
        Assert.Equal(0.6, zones[2].Width, 10); // 좌하
        Assert.Equal(0.6, zones[3].X, 10);     // 우하
    }

    [Fact]
    public void MoveVerticalEdge_WithCrossPos_MovesOnlyGrabbedSegment()
    {
        var zones = LayoutEngine.Grid(2, 2); // 세로 경계 x=0.5, 위/아래 행 독립 구간

        // 위쪽 행(y=0.25)을 잡고 드래그
        double applied = ZoneEditOps.MoveVerticalEdge(zones, 0.5, 0.6, crossPos: 0.25);

        Assert.Equal(0.6, applied, 10);
        Assert.Equal(0.6, zones[0].Width, 10); // 좌상: 이동
        Assert.Equal(0.6, zones[1].X, 10);     // 우상: 이동
        Assert.Equal(0.5, zones[2].Width, 10); // 좌하: 그대로
        Assert.Equal(0.5, zones[3].X, 10);     // 우하: 그대로
    }

    [Fact]
    public void MoveVerticalEdge_SegmentExpandsWhenZoneSpansAcross()
    {
        // 왼쪽은 전체 높이 1존, 오른쪽은 상/하 2존 → 왼쪽 존 때문에 구간이 전체로 확장
        var zones = new List<FractionRect>
        {
            new(0, 0, 0.5, 1.0),
            new(0.5, 0, 0.5, 0.5),
            new(0.5, 0.5, 0.5, 0.5),
        };

        ZoneEditOps.MoveVerticalEdge(zones, 0.5, 0.6, crossPos: 0.25);

        // 오른쪽 두 존 모두 함께 움직여야 타일이 유지된다
        Assert.Equal(0.6, zones[0].Width, 10);
        Assert.Equal(0.6, zones[1].X, 10);
        Assert.Equal(0.6, zones[2].X, 10);
    }

    [Fact]
    public void MoveHorizontalEdge_WithCrossPos_MovesOnlyGrabbedSegment()
    {
        var zones = LayoutEngine.Grid(2, 2); // 가로 경계 y=0.5, 좌/우 열 독립 구간

        // 왼쪽 열(x=0.25)을 잡고 드래그
        double applied = ZoneEditOps.MoveHorizontalEdge(zones, 0.5, 0.3, crossPos: 0.25);

        Assert.Equal(0.3, applied, 10);
        Assert.Equal(0.3, zones[0].Height, 10); // 좌상: 이동
        Assert.Equal(0.3, zones[2].Y, 10);      // 좌하: 이동
        Assert.Equal(0.5, zones[1].Height, 10); // 우상: 그대로
        Assert.Equal(0.5, zones[3].Y, 10);      // 우하: 그대로
    }

    [Fact]
    public void MoveVerticalEdge_NullCrossPos_MovesWholeLine()
    {
        var zones = LayoutEngine.Grid(2, 2);

        ZoneEditOps.MoveVerticalEdge(zones, 0.5, 0.6, crossPos: null);

        Assert.Equal(0.6, zones[0].Width, 10);
        Assert.Equal(0.6, zones[2].Width, 10); // 아래 행도 함께 이동
    }

    [Fact]
    public void MoveVerticalEdge_ClampsToMinimumZoneSize()
    {
        var zones = LayoutEngine.Columns(1, 1);

        double applied = ZoneEditOps.MoveVerticalEdge(zones, 0.5, 0.001);

        Assert.Equal(ZoneEditOps.MinSize, applied, 10);
        Assert.Equal(ZoneEditOps.MinSize, zones[0].Width, 10);
    }

    [Fact]
    public void MoveVerticalEdge_MonitorBoundary_IsNotMoved()
    {
        var zones = LayoutEngine.Columns(1, 1);

        double applied = ZoneEditOps.MoveVerticalEdge(zones, 0.0, 0.2);

        Assert.Equal(0.0, applied, 10);
        Assert.Equal(0.0, zones[0].X, 10); // 변화 없음
    }

    [Fact]
    public void MoveHorizontalEdge_MovesSharedEdge()
    {
        var zones = LayoutEngine.Rows(1, 1); // 경계 y=0.5

        double applied = ZoneEditOps.MoveHorizontalEdge(zones, 0.5, 0.25);

        Assert.Equal(0.25, applied, 10);
        Assert.Equal(0.25, zones[0].Height, 10);
        Assert.Equal(0.25, zones[1].Y, 10);
        Assert.Equal(0.75, zones[1].Height, 10);
    }

    [Fact]
    public void RemoveAndFill_SingleNeighborSharingFullEdge_Absorbs()
    {
        var zones = LayoutEngine.Columns(1, 1);

        bool filled = ZoneEditOps.RemoveAndFill(zones, 1); // 오른쪽 존 삭제

        Assert.True(filled);
        var only = Assert.Single(zones);
        Assert.Equal(0.0, only.X, 10);
        Assert.Equal(1.0, only.Width, 10); // 왼쪽 존이 전체를 차지
    }

    [Fact]
    public void RemoveAndFill_MultipleNeighborsCoveringEdge_AllExpand()
    {
        // 왼쪽 큰 존 1개 + 오른쪽 상/하 2개
        var zones = new List<FractionRect>
        {
            new(0, 0, 0.5, 1.0),
            new(0.5, 0, 0.5, 0.5),
            new(0.5, 0.5, 0.5, 0.5),
        };

        bool filled = ZoneEditOps.RemoveAndFill(zones, 0); // 왼쪽 존 삭제

        Assert.True(filled);
        Assert.Equal(2, zones.Count);
        // 오른쪽 두 존이 왼쪽으로 확장되어 전체 폭을 차지
        Assert.All(zones, z =>
        {
            Assert.Equal(0.0, z.X, 10);
            Assert.Equal(1.0, z.Width, 10);
        });
    }

    [Fact]
    public void RemoveAndFill_PrefersSingleNeighborOverGroup()
    {
        // 좌상/좌하/우(전체 높이) — 우측 존 삭제 시 좌측 2개 그룹보다... 대신
        // 좌상 삭제: 왼쪽엔 이웃 없음, 아래(좌하)가 정확히 한 개로 흡수
        var zones = new List<FractionRect>
        {
            new(0, 0, 0.5, 0.5),   // 좌상
            new(0, 0.5, 0.5, 0.5), // 좌하
            new(0.5, 0, 0.5, 1.0), // 우 전체
        };

        bool filled = ZoneEditOps.RemoveAndFill(zones, 0);

        Assert.True(filled);
        Assert.Equal(2, zones.Count);
        // 좌하 존이 위로 확장되어 왼쪽 전체가 됨
        Assert.Contains(zones, z =>
            Math.Abs(z.X) < 1e-9 && Math.Abs(z.Y) < 1e-9 &&
            Math.Abs(z.Width - 0.5) < 1e-9 && Math.Abs(z.Height - 1.0) < 1e-9);
    }

    [Fact]
    public void RemoveAndFill_NoAbsorbableNeighbor_LeavesHole()
    {
        var zones = new List<FractionRect> { ZoneEditOps.FullZone };

        bool filled = ZoneEditOps.RemoveAndFill(zones, 0);

        Assert.False(filled);
        Assert.Empty(zones);
    }

    [Fact]
    public void MergeZones_SideBySideSameHeight_MergesIntoOne()
    {
        var zones = LayoutEngine.Columns(70, 30);

        int merged = ZoneEditOps.MergeZones(zones, 0, 1);

        Assert.Equal(0, merged);
        var only = Assert.Single(zones);
        Assert.Equal(1.0, only.Width, 10);
    }

    [Fact]
    public void MergeZones_StackedSameWidth_MergesIntoOne()
    {
        var zones = LayoutEngine.Rows(1, 1);

        int merged = ZoneEditOps.MergeZones(zones, 1, 0);

        Assert.Equal(0, merged);
        var only = Assert.Single(zones);
        Assert.Equal(0.0, only.Y, 10);
        Assert.Equal(1.0, only.Height, 10);
    }

    [Fact]
    public void MergeZones_UnionNotRectangle_Fails()
    {
        // 2×2에서 대각선 존은 병합 불가
        var zones = LayoutEngine.Grid(2, 2);

        int merged = ZoneEditOps.MergeZones(zones, 0, 3);

        Assert.Equal(-1, merged);
        Assert.Equal(4, zones.Count);
    }

    [Fact]
    public void MergeZones_DifferentHeightSideBySide_Fails()
    {
        var zones = new List<FractionRect>
        {
            new(0, 0, 0.5, 1.0),
            new(0.5, 0, 0.5, 0.5),
        };

        int merged = ZoneEditOps.MergeZones(zones, 0, 1);

        Assert.Equal(-1, merged);
    }

    [Fact]
    public void ResizeZone_InteriorRightEdge_SetsExactWidth()
    {
        var zones = LayoutEngine.Columns(1, 1);

        var (w, _) = ZoneEditOps.ResizeZone(zones, 0, 0.7, null);

        Assert.Equal(0.7, w, 10);
        Assert.Equal(0.7, zones[0].Width, 10);
        Assert.Equal(0.7, zones[1].X, 10);
        Assert.Equal(0.3, zones[1].Width, 10);
    }

    [Fact]
    public void ResizeZone_ZoneAtMonitorRightBoundary_MovesLeftEdge()
    {
        var zones = LayoutEngine.Columns(1, 1);

        // 오른쪽 존은 오른쪽 변이 모니터 경계 → 왼쪽 변이 움직여야 한다
        var (w, _) = ZoneEditOps.ResizeZone(zones, 1, 0.4, null);

        Assert.Equal(0.4, w, 10);
        Assert.Equal(0.6, zones[0].Width, 10);
        Assert.Equal(0.6, zones[1].X, 10);
    }

    [Fact]
    public void ResizeZone_FullWidthZone_WidthUnchanged()
    {
        var zones = LayoutEngine.Rows(1, 1); // 두 존 모두 전체 폭

        var (w, h) = ZoneEditOps.ResizeZone(zones, 0, 0.5, 0.3);

        Assert.Equal(1.0, w, 10);  // 폭은 변경 불가
        Assert.Equal(0.3, h, 10);  // 높이는 적용
        Assert.Equal(0.3, zones[1].Y, 10);
    }

    [Fact]
    public void ResizeZone_RequestBeyondNeighborMin_IsClamped()
    {
        var zones = LayoutEngine.Columns(1, 1);

        var (w, _) = ZoneEditOps.ResizeZone(zones, 0, 0.999, null);

        Assert.Equal(1.0 - ZoneEditOps.MinSize, w, 10); // 이웃 최소 크기만큼 남김
    }

    [Fact]
    public void ResizeZone_BothAxes_AppliesWidthAndHeight()
    {
        var zones = LayoutEngine.Grid(2, 2);

        var (w, h) = ZoneEditOps.ResizeZone(zones, 0, 0.6, 0.4);

        Assert.Equal(0.6, w, 10);
        Assert.Equal(0.4, h, 10);
        Assert.Equal(0.6, zones[1].X, 10); // 우상 존이 밀림
        Assert.Equal(0.4, zones[2].Y, 10); // 좌하 존이 밀림
    }

    [Fact]
    public void ZoneAt_FindsContainingZone()
    {
        var zones = LayoutEngine.Columns(1, 1);

        Assert.Equal(0, ZoneEditOps.ZoneAt(zones, 0.25, 0.5));
        Assert.Equal(1, ZoneEditOps.ZoneAt(zones, 0.75, 0.5));
        Assert.Equal(-1, ZoneEditOps.ZoneAt(zones, 1.5, 0.5));
    }

    [Fact]
    public void FindEdgeNear_FindsInteriorVerticalEdge()
    {
        var zones = LayoutEngine.Columns(1, 1);

        var edge = ZoneEditOps.FindEdgeNear(zones, 0.505, 0.5, 0.01, 0.01);

        Assert.Equal(ZoneEditOps.EdgeOrientation.Vertical, edge.Orientation);
        Assert.Equal(0.5, edge.Value, 10);
    }

    [Fact]
    public void FindEdgeNear_IgnoresMonitorBoundary()
    {
        var zones = LayoutEngine.Columns(1, 1);

        var edge = ZoneEditOps.FindEdgeNear(zones, 0.002, 0.5, 0.01, 0.01);

        Assert.Equal(ZoneEditOps.EdgeOrientation.None, edge.Orientation);
    }

    [Fact]
    public void FindEdgeNear_FarFromAnyEdge_ReturnsNone()
    {
        var zones = LayoutEngine.Columns(1, 1);

        var edge = ZoneEditOps.FindEdgeNear(zones, 0.25, 0.5, 0.01, 0.01);

        Assert.Equal(ZoneEditOps.EdgeOrientation.None, edge.Orientation);
    }
}
