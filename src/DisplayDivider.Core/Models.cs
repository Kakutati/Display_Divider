namespace DisplayDivider.Core;

/// <summary>모니터 크기에 대한 0~1 비율 좌표. 해상도가 바뀌어도 레이아웃이 유지된다.</summary>
public struct FractionRect
{
    public double X { get; set; }
    public double Y { get; set; }
    public double Width { get; set; }
    public double Height { get; set; }

    public FractionRect(double x, double y, double width, double height)
    {
        X = x; Y = y; Width = width; Height = height;
    }
}

/// <summary>물리 픽셀 좌표계 사각형 (Win32 스크린 좌표).</summary>
public readonly record struct PixelRect(int X, int Y, int Width, int Height)
{
    public int Right => X + Width;
    public int Bottom => Y + Height;
    public (int Cx, int Cy) Center => (X + Width / 2, Y + Height / 2);

    public bool Contains(int px, int py) => px >= X && px < Right && py >= Y && py < Bottom;
}

public sealed class MonitorLayout
{
    public string DeviceName { get; set; } = "";
    public List<FractionRect> Zones { get; set; } = new();
}

public sealed class LayoutConfig
{
    public int Version { get; set; } = 1;

    /// <summary>드래그 스냅 기능 전체 on/off.</summary>
    public bool DragSnapEnabled { get; set; } = true;

    /// <summary>true면 Ctrl을 누른 채 드롭할 때만 존에 스냅. false면 항상 스냅(Shift로 예외).</summary>
    public bool SnapRequiresCtrl { get; set; } = true;

    /// <summary>최대화 시 존 크기로 가두기(가짜 최대화). Shift+최대화는 항상 전체 화면.</summary>
    public bool FakeMaximizeEnabled { get; set; } = true;

    public List<MonitorLayout> Monitors { get; set; } = new();
}
