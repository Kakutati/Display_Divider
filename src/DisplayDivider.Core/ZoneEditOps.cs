namespace DisplayDivider.Core;

/// <summary>
/// 존 목록에 대한 수동 편집 연산(분할, 삭제, 공유 경계 이동).
/// 모든 좌표는 0~1 비율. UI에 의존하지 않아 단위 테스트 대상.
/// </summary>
public static class ZoneEditOps
{
    /// <summary>존의 최소 크기(비율). 이보다 작아지는 연산은 거부된다.</summary>
    public const double MinSize = 0.03;

    const double Eps = 1e-6;

    public static FractionRect FullZone => new(0, 0, 1, 1);

    /// <summary>존을 좌/우 반으로 분할. 결과가 최소 크기보다 작아지면 false.</summary>
    public static bool SplitHorizontal(List<FractionRect> zones, int index)
    {
        var z = zones[index];
        double half = z.Width / 2;
        if (half < MinSize) return false;

        zones[index] = new FractionRect(z.X, z.Y, half, z.Height);
        zones.Insert(index + 1, new FractionRect(z.X + half, z.Y, z.Width - half, z.Height));
        return true;
    }

    /// <summary>존을 상/하 반으로 분할. 결과가 최소 크기보다 작아지면 false.</summary>
    public static bool SplitVertical(List<FractionRect> zones, int index)
    {
        var z = zones[index];
        double half = z.Height / 2;
        if (half < MinSize) return false;

        zones[index] = new FractionRect(z.X, z.Y, z.Width, half);
        zones.Insert(index + 1, new FractionRect(z.X, z.Y + half, z.Width, z.Height - half));
        return true;
    }

    /// <summary>
    /// 세로 경계선(x = edgeX)을 requestedX로 이동한다.
    /// 경계를 공유하는 양쪽 존이 모두 있어야 하며(타일 유지), 최소 크기를 보장하도록 클램프된다.
    /// crossPos(마우스의 세로 위치)를 주면 그 위치가 속한 구간만 이동한다 —
    /// 단, 한쪽 존이 여러 구간에 걸쳐 있으면 타일이 깨지지 않는 최소 범위까지 자동 확장된다.
    /// null이면 경계선 전체를 이동한다. 반환값은 실제 적용된 경계 위치(이동 불가 시 edgeX 그대로).
    /// </summary>
    public static double MoveVerticalEdge(List<FractionRect> zones, double edgeX, double requestedX, double? crossPos = null)
    {
        var leftSide = new List<int>();  // 오른쪽 변이 이 경계인 존들
        var rightSide = new List<int>(); // 왼쪽 변이 이 경계인 존들
        for (int i = 0; i < zones.Count; i++)
        {
            var z = zones[i];
            if (Math.Abs(z.X + z.Width - edgeX) < Eps) leftSide.Add(i);
            else if (Math.Abs(z.X - edgeX) < Eps) rightSide.Add(i);
        }
        // 모니터 바깥 경계이거나 한쪽에만 걸친 경계는 이동하면 틈/겹침이 생기므로 거부
        if (leftSide.Count == 0 || rightSide.Count == 0) return edgeX;

        if (crossPos is double pos)
        {
            if (SegmentInterval(zones, leftSide, rightSide, pos, vertical: true) is not { } seg)
                return edgeX;
            leftSide = leftSide.Where(i =>
                zones[i].Y >= seg.Lo - Eps && zones[i].Y + zones[i].Height <= seg.Hi + Eps).ToList();
            rightSide = rightSide.Where(i =>
                zones[i].Y >= seg.Lo - Eps && zones[i].Y + zones[i].Height <= seg.Hi + Eps).ToList();
            if (leftSide.Count == 0 || rightSide.Count == 0) return edgeX;
        }

        double lo = leftSide.Max(i => zones[i].X) + MinSize;
        double hi = rightSide.Min(i => zones[i].X + zones[i].Width) - MinSize;
        if (lo > hi) return edgeX;

        double x = Math.Clamp(requestedX, lo, hi);
        foreach (int i in leftSide)
        {
            var z = zones[i];
            z.Width = x - z.X;
            zones[i] = z;
        }
        foreach (int i in rightSide)
        {
            var z = zones[i];
            double right = z.X + z.Width;
            z.X = x;
            z.Width = right - x;
            zones[i] = z;
        }
        return x;
    }

    /// <summary>가로 경계선(y = edgeY)을 requestedY로 이동한다. MoveVerticalEdge와 동일한 규칙.</summary>
    public static double MoveHorizontalEdge(List<FractionRect> zones, double edgeY, double requestedY, double? crossPos = null)
    {
        var topSide = new List<int>();    // 아래 변이 이 경계인 존들
        var bottomSide = new List<int>(); // 위 변이 이 경계인 존들
        for (int i = 0; i < zones.Count; i++)
        {
            var z = zones[i];
            if (Math.Abs(z.Y + z.Height - edgeY) < Eps) topSide.Add(i);
            else if (Math.Abs(z.Y - edgeY) < Eps) bottomSide.Add(i);
        }
        if (topSide.Count == 0 || bottomSide.Count == 0) return edgeY;

        if (crossPos is double pos)
        {
            if (SegmentInterval(zones, topSide, bottomSide, pos, vertical: false) is not { } seg)
                return edgeY;
            topSide = topSide.Where(i =>
                zones[i].X >= seg.Lo - Eps && zones[i].X + zones[i].Width <= seg.Hi + Eps).ToList();
            bottomSide = bottomSide.Where(i =>
                zones[i].X >= seg.Lo - Eps && zones[i].X + zones[i].Width <= seg.Hi + Eps).ToList();
            if (topSide.Count == 0 || bottomSide.Count == 0) return edgeY;
        }

        double lo = topSide.Max(i => zones[i].Y) + MinSize;
        double hi = bottomSide.Min(i => zones[i].Y + zones[i].Height) - MinSize;
        if (lo > hi) return edgeY;

        double y = Math.Clamp(requestedY, lo, hi);
        foreach (int i in topSide)
        {
            var z = zones[i];
            z.Height = y - z.Y;
            zones[i] = z;
        }
        foreach (int i in bottomSide)
        {
            var z = zones[i];
            double bottom = z.Y + z.Height;
            z.Y = y;
            z.Height = bottom - y;
            zones[i] = z;
        }
        return y;
    }

    /// <summary>
    /// 경계에 접한 존들 중 crossPos가 속한 "구간"의 범위를 구한다.
    /// crossPos를 포함하는 존에서 시작해, 스팬이 겹치는 존이 있으면 그 존까지 범위를 넓힌다
    /// (경계 반대편에 걸쳐 있는 존을 반쪽만 움직일 수 없으므로). 어느 존에도 속하지 않으면 null.
    /// </summary>
    static (double Lo, double Hi)? SegmentInterval(
        List<FractionRect> zones, List<int> sideA, List<int> sideB, double crossPos, bool vertical)
    {
        var spans = new List<(double Start, double End)>();
        foreach (int i in sideA.Concat(sideB))
        {
            var z = zones[i];
            spans.Add(vertical ? (z.Y, z.Y + z.Height) : (z.X, z.X + z.Width));
        }

        double lo = double.NaN, hi = double.NaN;
        foreach (var (start, end) in spans)
        {
            if (crossPos < start - Eps || crossPos > end + Eps) continue;
            lo = double.IsNaN(lo) ? start : Math.Min(lo, start);
            hi = double.IsNaN(hi) ? end : Math.Max(hi, end);
        }
        if (double.IsNaN(lo)) return null;

        // 폐포(closure): 구간에 걸치는 존이 있으면 그 존 전체를 포함하도록 확장
        bool changed = true;
        while (changed)
        {
            changed = false;
            foreach (var (start, end) in spans)
            {
                if (end <= lo + Eps || start >= hi - Eps) continue; // 겹치지 않음
                if (start < lo - Eps) { lo = start; changed = true; }
                if (end > hi + Eps) { hi = end; changed = true; }
            }
        }
        return (lo, hi);
    }

    /// <summary>
    /// 존을 삭제하고 인접 존을 확장해 빈 공간을 자동으로 채운다.
    /// 삭제된 변을 틈 없이 덮는 이웃 그룹이 있는 방향 중, 이웃 수가 가장 적은(깔끔한) 쪽이 흡수한다.
    /// 채울 수 없는 구조면 존만 삭제되고 false를 반환한다(빈 공간으로 남음).
    /// </summary>
    public static bool RemoveAndFill(List<FractionRect> zones, int index)
    {
        var removed = zones[index];
        zones.RemoveAt(index);

        List<int>? bestGroup = null;
        int bestSide = -1;
        for (int side = 0; side < 4; side++)
        {
            var group = AbsorbGroup(zones, removed, side);
            if (group is not null && (bestGroup is null || group.Count < bestGroup.Count))
            {
                bestGroup = group;
                bestSide = side;
            }
        }
        if (bestGroup is null) return false;

        foreach (int i in bestGroup)
        {
            var z = zones[i];
            switch (bestSide)
            {
                case 0: z.Width += removed.Width; break;                     // 왼쪽 이웃 → 오른쪽으로 확장
                case 1: z.X = removed.X; z.Width += removed.Width; break;    // 오른쪽 이웃 → 왼쪽으로 확장
                case 2: z.Height += removed.Height; break;                   // 위 이웃 → 아래로 확장
                case 3: z.Y = removed.Y; z.Height += removed.Height; break;  // 아래 이웃 → 위로 확장
            }
            zones[i] = z;
        }
        return true;
    }

    /// <summary>
    /// removed의 side(0=좌,1=우,2=상,3=하) 방향에서 흡수 가능한 이웃 인덱스 그룹.
    /// 이웃들의 스팬이 removed의 변을 벗어나지 않으면서 틈 없이 전부 덮어야 한다. 불가능하면 null.
    /// </summary>
    static List<int>? AbsorbGroup(List<FractionRect> zones, FractionRect removed, int side)
    {
        (double lo, double hi) = side < 2
            ? (removed.Y, removed.Y + removed.Height)
            : (removed.X, removed.X + removed.Width);

        var group = new List<int>();
        var intervals = new List<(double Start, double End)>();

        for (int i = 0; i < zones.Count; i++)
        {
            var z = zones[i];
            bool touches = side switch
            {
                0 => Math.Abs(z.X + z.Width - removed.X) < Eps,
                1 => Math.Abs(z.X - (removed.X + removed.Width)) < Eps,
                2 => Math.Abs(z.Y + z.Height - removed.Y) < Eps,
                _ => Math.Abs(z.Y - (removed.Y + removed.Height)) < Eps,
            };
            if (!touches) continue;

            (double start, double end) = side < 2 ? (z.Y, z.Y + z.Height) : (z.X, z.X + z.Width);
            if (end <= lo + Eps || start >= hi - Eps) continue; // 변에 걸치지 않는 이웃
            if (start < lo - Eps || end > hi + Eps) return null; // 부분 겹침 → 확장하면 다른 영역 침범

            group.Add(i);
            intervals.Add((start, end));
        }
        if (group.Count == 0) return null;

        // 커버리지 검사: 정렬 후 lo→hi를 틈 없이 덮는지
        intervals.Sort();
        if (Math.Abs(intervals[0].Start - lo) > Eps) return null;
        double cursor = intervals[0].End;
        for (int i = 1; i < intervals.Count; i++)
        {
            if (Math.Abs(intervals[i].Start - cursor) > Eps) return null;
            cursor = intervals[i].End;
        }
        return Math.Abs(cursor - hi) > Eps ? null : group;
    }

    /// <summary>
    /// 두 존의 합집합이 직사각형이면(같은 폭 상하 인접 또는 같은 높이 좌우 인접) 하나로 병합한다.
    /// 성공 시 병합된 존의 인덱스, 불가능하면 -1.
    /// </summary>
    public static int MergeZones(List<FractionRect> zones, int a, int b)
    {
        if (a == b || a < 0 || b < 0 || a >= zones.Count || b >= zones.Count) return -1;
        var za = zones[a];
        var zb = zones[b];

        bool sameColumn = Math.Abs(za.X - zb.X) < Eps && Math.Abs(za.Width - zb.Width) < Eps;
        bool sameRow = Math.Abs(za.Y - zb.Y) < Eps && Math.Abs(za.Height - zb.Height) < Eps;

        FractionRect merged;
        if (sameColumn &&
            (Math.Abs(za.Y + za.Height - zb.Y) < Eps || Math.Abs(zb.Y + zb.Height - za.Y) < Eps))
        {
            merged = new FractionRect(za.X, Math.Min(za.Y, zb.Y), za.Width, za.Height + zb.Height);
        }
        else if (sameRow &&
                 (Math.Abs(za.X + za.Width - zb.X) < Eps || Math.Abs(zb.X + zb.Width - za.X) < Eps))
        {
            merged = new FractionRect(Math.Min(za.X, zb.X), za.Y, za.Width + zb.Width, za.Height);
        }
        else
        {
            return -1;
        }

        int keep = Math.Min(a, b);
        int drop = Math.Max(a, b);
        zones[keep] = merged;
        zones.RemoveAt(drop);
        return keep;
    }

    /// <summary>
    /// 존의 폭/높이를 지정 값(비율)으로 변경한다. null인 축은 건드리지 않는다.
    /// 내부적으로 경계 이동을 사용하므로 타일이 유지되고, 이웃 존의 최소 크기에 걸리면 클램프된다.
    /// 폭 변경은 오른쪽 변을 움직이되 모니터 경계면 왼쪽 변을 움직인다(높이도 동일).
    /// 양쪽 모두 모니터 경계인 축(전체 폭/높이 존)은 변경할 수 없다.
    /// 반환값은 실제 적용된 (폭, 높이).
    /// </summary>
    public static (double Width, double Height) ResizeZone(
        List<FractionRect> zones, int index, double? newWidth, double? newHeight)
    {
        if (newWidth is double w)
        {
            var z = zones[index];
            w = Math.Clamp(w, MinSize, 1.0);
            double right = z.X + z.Width;
            double cross = z.Y + z.Height / 2;
            if (right < 1 - Eps)
                MoveVerticalEdge(zones, right, z.X + w, cross);
            else if (z.X > Eps)
                MoveVerticalEdge(zones, z.X, right - w, cross);
        }
        if (newHeight is double h)
        {
            var z = zones[index];
            h = Math.Clamp(h, MinSize, 1.0);
            double bottom = z.Y + z.Height;
            double cross = z.X + z.Width / 2;
            if (bottom < 1 - Eps)
                MoveHorizontalEdge(zones, bottom, z.Y + h, cross);
            else if (z.Y > Eps)
                MoveHorizontalEdge(zones, z.Y, bottom - h, cross);
        }
        var result = zones[index];
        return (result.Width, result.Height);
    }

    /// <summary>점을 포함하는 존의 인덱스. 없으면 -1.</summary>
    public static int ZoneAt(IReadOnlyList<FractionRect> zones, double fx, double fy)
    {
        for (int i = 0; i < zones.Count; i++)
        {
            var z = zones[i];
            if (fx >= z.X && fx < z.X + z.Width && fy >= z.Y && fy < z.Y + z.Height)
                return i;
        }
        return -1;
    }

    public enum EdgeOrientation { None, Vertical, Horizontal }

    /// <summary>
    /// (fx, fy) 근처의 이동 가능한 내부 경계를 찾는다. tolerance는 비율 단위.
    /// 세로/가로 경계가 모두 가까우면 더 가까운 쪽을 반환한다.
    /// </summary>
    public static (EdgeOrientation Orientation, double Value) FindEdgeNear(
        IReadOnlyList<FractionRect> zones, double fx, double fy, double tolX, double tolY)
    {
        var best = (Orientation: EdgeOrientation.None, Value: 0.0);
        double bestDist = double.MaxValue;

        void TryVertical(double x, double zy, double zh)
        {
            if (x < Eps || x > 1 - Eps) return;                 // 모니터 바깥 경계 제외
            if (fy < zy - tolY || fy > zy + zh + tolY) return;  // 해당 변의 범위 안에 있어야 함
            double d = Math.Abs(fx - x) / tolX;
            if (d <= 1 && d < bestDist)
            {
                best = (EdgeOrientation.Vertical, x);
                bestDist = d;
            }
        }

        void TryHorizontal(double y, double zx, double zw)
        {
            if (y < Eps || y > 1 - Eps) return;
            if (fx < zx - tolX || fx > zx + zw + tolX) return;
            double d = Math.Abs(fy - y) / tolY;
            if (d <= 1 && d < bestDist)
            {
                best = (EdgeOrientation.Horizontal, y);
                bestDist = d;
            }
        }

        foreach (var z in zones)
        {
            TryVertical(z.X, z.Y, z.Height);
            TryVertical(z.X + z.Width, z.Y, z.Height);
            TryHorizontal(z.Y, z.X, z.Width);
            TryHorizontal(z.Y + z.Height, z.X, z.Width);
        }
        return best;
    }
}
