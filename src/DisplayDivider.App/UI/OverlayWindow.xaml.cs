using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Shapes;
using DisplayDivider.Core;
using DisplayDivider.Win32;

namespace DisplayDivider.App.UI;

/// <summary>
/// 드래그 중 존 위치를 표시하는 클릭 통과(click-through) 오버레이. 모니터당 1개.
/// 위치/크기는 WPF 좌표계 대신 SetWindowPos로 물리 픽셀 기준 배치한다.
/// </summary>
public partial class OverlayWindow : Window
{
    static readonly Brush IdleFill = new SolidColorBrush(Color.FromArgb(0x16, 0x3B, 0x82, 0xF6));
    static readonly Brush HighlightFill = new SolidColorBrush(Color.FromArgb(0x50, 0x3B, 0x82, 0xF6));
    static readonly Brush ZoneStroke = new SolidColorBrush(Color.FromArgb(0xA0, 0x3B, 0x82, 0xF6));

    PixelRect _monitorBounds;
    List<PixelRect> _zones = new();
    int _highlight = -1;
    string? _hint;

    public OverlayWindow()
    {
        InitializeComponent();
    }

    public void Setup(PixelRect monitorBounds, List<PixelRect> zones, string? hint)
    {
        _monitorBounds = monitorBounds;
        _zones = zones;
        _hint = hint;

        Show();
        var hwnd = new WindowInteropHelper(this).Handle;

        // 클릭 통과 + 비활성 유지: 드래그 중인 창의 입력을 방해하지 않는다.
        long exStyle = Native.GetWindowLongPtr(hwnd, Native.GWL_EXSTYLE);
        Native.SetWindowLongPtr(hwnd, Native.GWL_EXSTYLE,
            exStyle | Native.WS_EX_TRANSPARENT | Native.WS_EX_LAYERED |
            Native.WS_EX_NOACTIVATE | Native.WS_EX_TOOLWINDOW);

        Native.SetWindowPos(hwnd, Native.HWND_TOPMOST,
            monitorBounds.X, monitorBounds.Y, monitorBounds.Width, monitorBounds.Height,
            Native.SWP_NOACTIVATE | Native.SWP_SHOWWINDOW);

        Redraw();
    }

    /// <summary>커서가 올라간 존 인덱스(-1이면 없음)를 반영한다.</summary>
    public void SetHighlight(int index)
    {
        if (_highlight == index) return;
        _highlight = index;
        Redraw();
    }

    public int ZoneIndexAt(int px, int py)
    {
        for (int i = 0; i < _zones.Count; i++)
            if (_zones[i].Contains(px, py))
                return i;
        return -1;
    }

    void Redraw()
    {
        ZoneCanvas.Children.Clear();

        var hwnd = new WindowInteropHelper(this).Handle;
        double scale = hwnd == IntPtr.Zero ? 1.0 : Native.GetDpiForWindow(hwnd) / 96.0;
        if (scale <= 0) scale = 1.0;

        for (int i = 0; i < _zones.Count; i++)
        {
            var z = _zones[i];
            var rect = new Rectangle
            {
                Width = Math.Max(0, z.Width / scale - 4),
                Height = Math.Max(0, z.Height / scale - 4),
                Stroke = ZoneStroke,
                StrokeThickness = 2,
                RadiusX = 6,
                RadiusY = 6,
                Fill = i == _highlight ? HighlightFill : IdleFill,
            };
            Canvas.SetLeft(rect, (z.X - _monitorBounds.X) / scale + 2);
            Canvas.SetTop(rect, (z.Y - _monitorBounds.Y) / scale + 2);
            ZoneCanvas.Children.Add(rect);

            // 존 번호 (중앙) — 드래그 중 목표 존을 알아보기 쉽게
            var number = new TextBlock
            {
                Text = (i + 1).ToString(),
                FontSize = 28,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(Color.FromArgb(
                    i == _highlight ? (byte)0xE0 : (byte)0x70, 0x3B, 0x82, 0xF6)),
            };
            number.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            Canvas.SetLeft(number, (z.X - _monitorBounds.X + z.Width / 2.0) / scale - number.DesiredSize.Width / 2);
            Canvas.SetTop(number, (z.Y - _monitorBounds.Y + z.Height / 2.0) / scale - number.DesiredSize.Height / 2);
            ZoneCanvas.Children.Add(number);
        }

        if (_hint is not null && _zones.Count > 0)
        {
            var hintText = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(0xC0, 0x20, 0x20, 0x20)),
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(12, 6, 12, 6),
                Child = new TextBlock
                {
                    Text = _hint,
                    Foreground = Brushes.White,
                    FontSize = 13,
                },
            };
            hintText.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            Canvas.SetLeft(hintText, (_monitorBounds.Width / scale - hintText.DesiredSize.Width) / 2);
            Canvas.SetTop(hintText, 24);
            ZoneCanvas.Children.Add(hintText);
        }
    }
}
