using System.Globalization;

namespace DisplayDivider.Core;

/// <summary>존 레이아웃 계산. UI/Win32에 의존하지 않는 순수 로직.</summary>
public static class LayoutEngine
{
    /// <summary>열/행 가중치로 그리드 존을 생성한다. 행 우선(왼쪽→오른쪽, 위→아래) 순서.</summary>
    public static List<FractionRect> FromWeights(IReadOnlyList<double> colWeights, IReadOnlyList<double> rowWeights)
    {
        if (colWeights.Count == 0 || rowWeights.Count == 0)
            throw new ArgumentException("가중치는 최소 1개 이상이어야 합니다.");
        if (colWeights.Concat(rowWeights).Any(w => w <= 0 || double.IsNaN(w) || double.IsInfinity(w)))
            throw new ArgumentException("가중치는 양수여야 합니다.");

        double colSum = colWeights.Sum();
        double rowSum = rowWeights.Sum();
        var zones = new List<FractionRect>(colWeights.Count * rowWeights.Count);

        double y = 0;
        foreach (double rw in rowWeights)
        {
            double h = rw / rowSum;
            double x = 0;
            foreach (double cw in colWeights)
            {
                double w = cw / colSum;
                zones.Add(new FractionRect(x, y, w, h));
                x += w;
            }
            y += h;
        }
        return zones;
    }

    public static List<FractionRect> Columns(params double[] weights) => FromWeights(weights, new[] { 1.0 });

    public static List<FractionRect> Rows(params double[] weights) => FromWeights(new[] { 1.0 }, weights);

    public static List<FractionRect> Grid(int cols, int rows)
    {
        if (cols < 1 || rows < 1) throw new ArgumentException("행/열은 1 이상이어야 합니다.");
        return FromWeights(
            Enumerable.Repeat(1.0, cols).ToArray(),
            Enumerable.Repeat(1.0, rows).ToArray());
    }

    /// <summary>
    /// 비율 존을 모니터 물리 픽셀 사각형으로 변환한다.
    /// 각 변을 독립적으로 반올림해 인접 존 사이에 틈/겹침이 생기지 않는다.
    /// </summary>
    public static PixelRect ToPixels(FractionRect f, PixelRect monitor)
    {
        int x1 = monitor.X + (int)Math.Round(f.X * monitor.Width);
        int y1 = monitor.Y + (int)Math.Round(f.Y * monitor.Height);
        int x2 = monitor.X + (int)Math.Round((f.X + f.Width) * monitor.Width);
        int y2 = monitor.Y + (int)Math.Round((f.Y + f.Height) * monitor.Height);
        return new PixelRect(x1, y1, x2 - x1, y2 - y1);
    }

    /// <summary>점이 속한 존의 인덱스. 없으면 -1.</summary>
    public static int FindZoneIndex(IReadOnlyList<FractionRect> zones, PixelRect monitor, int px, int py)
    {
        for (int i = 0; i < zones.Count; i++)
            if (ToPixels(zones[i], monitor).Contains(px, py))
                return i;
        return -1;
    }

    /// <summary>"70,30" 같은 쉼표 구분 가중치 문자열을 파싱한다. 잘못된 형식이면 FormatException.</summary>
    public static double[] ParseWeights(string text)
    {
        var parts = text.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length == 0) throw new FormatException("가중치가 비어 있습니다.");
        return parts.Select(p => double.Parse(p, CultureInfo.InvariantCulture)).ToArray();
    }
}
