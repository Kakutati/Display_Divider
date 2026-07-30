using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using DisplayDivider.App.Services;
using DisplayDivider.Core;
using DisplayDivider.Win32;

namespace DisplayDivider.App.UI;

public partial class ZoneEditorWindow : Window
{
    readonly ZoneService _zones;
    List<FractionRect> _pending = new();

    public ZoneEditorWindow(ZoneService zones)
    {
        _zones = zones;
        InitializeComponent();
        LoadMonitors();
    }

    MonitorInfo? SelectedMonitor =>
        (MonitorCombo.SelectedItem as ComboBoxItem)?.Tag as MonitorInfo;

    void LoadMonitors()
    {
        _zones.RefreshMonitors();
        MonitorCombo.Items.Clear();
        for (int i = 0; i < _zones.Monitors.Count; i++)
        {
            var mon = _zones.Monitors[i];
            MonitorCombo.Items.Add(new ComboBoxItem
            {
                Content = $"{i + 1}. {mon.DeviceName}  ({mon.Bounds.Width}×{mon.Bounds.Height}){(mon.IsPrimary ? "  — 기본" : "")}",
                Tag = mon,
            });
        }
        if (MonitorCombo.Items.Count > 0)
            MonitorCombo.SelectedIndex = 0;
    }

    void MonitorCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (SelectedMonitor is not { } mon) return;
        _pending = _zones.FractionZonesFor(mon.DeviceName).ToList();
        StatusText.Text = "";
        RedrawPreview();
    }

    void Preset_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as Button)?.Tag is not string tag) return;
        var parts = tag.Split(':');
        try
        {
            switch (parts[0])
            {
                case "cols":
                    var colWeights = LayoutEngine.ParseWeights(parts[1]);
                    _pending = LayoutEngine.Columns(colWeights);
                    ColsBox.Text = parts[1];
                    RowsBox.Text = "100";
                    break;
                case "rows":
                    var rowWeights = LayoutEngine.ParseWeights(parts[1]);
                    _pending = LayoutEngine.Rows(rowWeights);
                    ColsBox.Text = "100";
                    RowsBox.Text = parts[1];
                    break;
                case "grid":
                    var dims = LayoutEngine.ParseWeights(parts[1]);
                    _pending = LayoutEngine.Grid((int)dims[0], (int)dims[1]);
                    ColsBox.Text = string.Join(",", Enumerable.Repeat("1", (int)dims[0]));
                    RowsBox.Text = string.Join(",", Enumerable.Repeat("1", (int)dims[1]));
                    break;
            }
            StatusText.Text = "미리보기 적용됨 — 저장을 눌러야 반영됩니다";
            RedrawPreview();
        }
        catch (Exception ex) when (ex is FormatException or ArgumentException)
        {
            StatusText.Text = "프리셋 오류: " + ex.Message;
        }
    }

    void Custom_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var cols = LayoutEngine.ParseWeights(ColsBox.Text);
            var rows = LayoutEngine.ParseWeights(RowsBox.Text);
            _pending = LayoutEngine.FromWeights(cols, rows);
            StatusText.Text = "미리보기 적용됨 — 저장을 눌러야 반영됩니다";
            RedrawPreview();
        }
        catch (Exception ex) when (ex is FormatException or ArgumentException)
        {
            StatusText.Text = "형식 오류 (예: 70,30): " + ex.Message;
        }
    }

    void Clear_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedMonitor is not { } mon) return;
        _pending = new List<FractionRect>();
        _zones.SetZones(mon.DeviceName, new List<FractionRect>());
        StatusText.Text = "분할 해제됨";
        RedrawPreview();
    }

    void Save_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedMonitor is not { } mon) return;
        _zones.SetZones(mon.DeviceName, _pending.ToList());
        StatusText.Text = $"저장됨 — {mon.DeviceName}, 존 {_pending.Count}개";
    }

    void CloseBtn_Click(object sender, RoutedEventArgs e) => Close();

    void PreviewCanvas_SizeChanged(object sender, SizeChangedEventArgs e) => RedrawPreview();

    void RedrawPreview()
    {
        PreviewCanvas.Children.Clear();
        if (SelectedMonitor is not { } mon) return;

        double cw = PreviewCanvas.ActualWidth;
        double ch = PreviewCanvas.ActualHeight;
        if (cw < 40 || ch < 40) return;

        double scale = Math.Min((cw - 16) / mon.Bounds.Width, (ch - 16) / mon.Bounds.Height);
        double monW = mon.Bounds.Width * scale;
        double monH = mon.Bounds.Height * scale;
        double ox = (cw - monW) / 2;
        double oy = (ch - monH) / 2;

        PreviewCanvas.Children.Add(Positioned(new Rectangle
        {
            Width = monW,
            Height = monH,
            Stroke = Brushes.DimGray,
            StrokeThickness = 2,
            Fill = Brushes.White,
        }, ox, oy));

        var zoneFill = new SolidColorBrush(Color.FromArgb(0x30, 0x3B, 0x82, 0xF6));
        var zoneStroke = new SolidColorBrush(Color.FromArgb(0xFF, 0x3B, 0x82, 0xF6));

        for (int i = 0; i < _pending.Count; i++)
        {
            var f = _pending[i];
            double zx = ox + f.X * monW, zy = oy + f.Y * monH;
            double zw = f.Width * monW, zh = f.Height * monH;

            PreviewCanvas.Children.Add(Positioned(new Rectangle
            {
                Width = Math.Max(0, zw - 2),
                Height = Math.Max(0, zh - 2),
                Stroke = zoneStroke,
                StrokeThickness = 1.5,
                Fill = zoneFill,
            }, zx + 1, zy + 1));

            PreviewCanvas.Children.Add(Positioned(new TextBlock
            {
                Text = (i + 1).ToString(),
                FontSize = 16,
                FontWeight = FontWeights.Bold,
                Foreground = zoneStroke,
            }, zx + zw / 2 - 5, zy + zh / 2 - 11));
        }

        if (_pending.Count == 0)
        {
            PreviewCanvas.Children.Add(Positioned(new TextBlock
            {
                Text = "분할 없음",
                FontSize = 14,
                Foreground = Brushes.Gray,
            }, ox + monW / 2 - 30, oy + monH / 2 - 10));
        }
    }

    static UIElement Positioned(UIElement element, double x, double y)
    {
        Canvas.SetLeft(element, x);
        Canvas.SetTop(element, y);
        return element;
    }
}
