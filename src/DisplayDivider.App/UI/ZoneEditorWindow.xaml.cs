using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using DisplayDivider.App.Services;
using DisplayDivider.Core;
using DisplayDivider.Win32;

namespace DisplayDivider.App.UI;

public partial class ZoneEditorWindow : Window
{
    const double EdgeTolerancePx = 7; // 경계 드래그 히트 판정 반경(캔버스 픽셀)

    readonly ZoneService _zones;
    List<FractionRect> _pending = new();
    int _selected = -1;

    // 미리보기 좌표 변환 (RedrawPreview에서 갱신)
    double _ox, _oy, _monW, _monH;

    // 경계 드래그 상태
    ZoneEditOps.EdgeOrientation _dragOrientation = ZoneEditOps.EdgeOrientation.None;
    double _dragEdge;
    double _dragCross; // 드래그 시작 시 경계와 수직 방향 위치 — 이 위치가 속한 구간만 이동

    // 수동 병합 모드: true면 다음 캔버스 클릭이 병합 대상 선택
    bool _mergeArmed;

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
        _selected = -1;
        StatusText.Text = "";
        RedrawPreview();
        UpdateSizeBoxes();
    }

    // ── 템플릿 / 직접 비율 ─────────────────────────────────────────────

    void Preset_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as Button)?.Tag is not string tag) return;
        var parts = tag.Split(':');
        try
        {
            switch (parts[0])
            {
                case "cols":
                    _pending = LayoutEngine.Columns(LayoutEngine.ParseWeights(parts[1]));
                    break;
                case "rows":
                    _pending = LayoutEngine.Rows(LayoutEngine.ParseWeights(parts[1]));
                    break;
                case "grid":
                    var dims = LayoutEngine.ParseWeights(parts[1]);
                    _pending = LayoutEngine.Grid((int)dims[0], (int)dims[1]);
                    break;
            }
            _selected = -1;
            SetPendingStatus("템플릿 적용됨");
            RedrawPreview();
            UpdateSizeBoxes();
        }
        catch (Exception ex) when (ex is FormatException or ArgumentException)
        {
            StatusText.Text = "템플릿 오류: " + ex.Message;
        }
    }

    // ── 수동 편집: 분할 / 삭제 ─────────────────────────────────────────

    void SplitH_Click(object sender, RoutedEventArgs e) => Split(horizontal: true);

    void SplitV_Click(object sender, RoutedEventArgs e) => Split(horizontal: false);

    void Split(bool horizontal)
    {
        // 존이 하나도 없으면 전체 화면 1존에서 시작
        if (_pending.Count == 0)
        {
            _pending.Add(ZoneEditOps.FullZone);
            _selected = 0;
        }
        if (_selected < 0)
        {
            StatusText.Text = "분할할 존을 미리보기에서 먼저 클릭하세요";
            return;
        }

        bool ok = horizontal
            ? ZoneEditOps.SplitHorizontal(_pending, _selected)
            : ZoneEditOps.SplitVertical(_pending, _selected);

        if (!ok)
        {
            StatusText.Text = "존이 너무 작아 더 분할할 수 없습니다";
            return;
        }
        SetPendingStatus($"존 {_selected + 1} 분할됨 — 경계를 드래그해 비율을 조절하세요");
        RedrawPreview();
        UpdateSizeBoxes();
    }

    void Delete_Click(object sender, RoutedEventArgs e)
    {
        if (_selected < 0 || _selected >= _pending.Count)
        {
            StatusText.Text = "삭제할 존을 미리보기에서 먼저 클릭하세요";
            return;
        }
        bool filled = ZoneEditOps.RemoveAndFill(_pending, _selected);
        _selected = -1;
        SetPendingStatus(filled
            ? "존 삭제됨 — 인접 존이 빈 공간을 채웠습니다"
            : "존 삭제됨 — 채울 수 있는 인접 존이 없어 빈 공간으로 남습니다");
        RedrawPreview();
        UpdateSizeBoxes();
    }

    void Merge_Click(object sender, RoutedEventArgs e)
    {
        if (_selected < 0 || _selected >= _pending.Count)
        {
            StatusText.Text = "병합할 기준 존을 미리보기에서 먼저 클릭하세요";
            return;
        }
        _mergeArmed = true;
        StatusText.Text = $"존 {_selected + 1}과(와) 합칠 인접 존을 클릭하세요 (Esc 취소)";
    }

    // ── 크기 직접 입력 ─────────────────────────────────────────────────

    bool IsPercentUnit => UnitCombo.SelectedIndex == 1;

    /// <summary>선택된 존의 현재 크기를 입력 필드에 채운다.</summary>
    void UpdateSizeBoxes()
    {
        if (ZoneWBox is null) return; // XAML 초기화 중 SelectionChanged 대비
        if (_selected < 0 || _selected >= _pending.Count || SelectedMonitor is not { } mon)
        {
            ZoneWBox.Text = "";
            ZoneHBox.Text = "";
            return;
        }
        var f = _pending[_selected];
        if (IsPercentUnit)
        {
            ZoneWBox.Text = (f.Width * 100).ToString("F1", CultureInfo.InvariantCulture);
            ZoneHBox.Text = (f.Height * 100).ToString("F1", CultureInfo.InvariantCulture);
        }
        else
        {
            var px = LayoutEngine.ToPixels(f, _zones.BaseRect(mon));
            ZoneWBox.Text = px.Width.ToString();
            ZoneHBox.Text = px.Height.ToString();
        }
    }

    double? ParseSize(string text, int monitorSpanPx)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        if (!double.TryParse(text.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out double value))
            return null;
        return IsPercentUnit ? value / 100.0 : value / monitorSpanPx;
    }

    void SizeApply_Click(object sender, RoutedEventArgs e)
    {
        if (_selected < 0 || _selected >= _pending.Count)
        {
            StatusText.Text = "크기를 바꿀 존을 미리보기에서 먼저 클릭하세요";
            return;
        }
        if (SelectedMonitor is not { } mon) return;

        var baseRect = _zones.BaseRect(mon);
        double? w = ParseSize(ZoneWBox.Text, baseRect.Width);
        double? h = ParseSize(ZoneHBox.Text, baseRect.Height);
        if (w is null && h is null)
        {
            StatusText.Text = "숫자 형식이 아닙니다 (예: 1280 또는 66.7)";
            return;
        }

        ZoneEditOps.ResizeZone(_pending, _selected, w, h);

        var applied = LayoutEngine.ToPixels(_pending[_selected], baseRect);
        UpdateSizeBoxes();
        RedrawPreview();
        SetPendingStatus($"적용됨 — 실제 {applied.Width}×{applied.Height}px (이웃 최소 크기·모니터 경계에 따라 조정될 수 있음)");
    }

    void SizeBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            SizeApply_Click(sender, e);
            e.Handled = true;
        }
    }

    void UnitCombo_SelectionChanged(object sender, SelectionChangedEventArgs e) => UpdateSizeBoxes();

    void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape && _mergeArmed)
        {
            _mergeArmed = false;
            StatusText.Text = "병합 취소됨";
            e.Handled = true;
        }
    }

    // ── 미리보기 마우스: 선택 + 경계 드래그 ────────────────────────────

    (double Fx, double Fy)? ToFraction(Point p)
    {
        if (_monW <= 0 || _monH <= 0) return null;
        return ((p.X - _ox) / _monW, (p.Y - _oy) / _monH);
    }

    void PreviewCanvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (ToFraction(e.GetPosition(PreviewCanvas)) is not { } f) return;

        if (_mergeArmed)
        {
            _mergeArmed = false;
            int target = ZoneEditOps.ZoneAt(_pending, f.Fx, f.Fy);
            if (target < 0 || target == _selected)
            {
                StatusText.Text = "병합 취소됨";
            }
            else
            {
                int merged = ZoneEditOps.MergeZones(_pending, _selected, target);
                if (merged >= 0)
                {
                    _selected = merged;
                    SetPendingStatus("존 병합됨");
                }
                else
                {
                    StatusText.Text = "합칠 수 없는 조합입니다 — 같은 폭(상하) 또는 같은 높이(좌우)로 맞닿은 존만 병합됩니다";
                }
            }
            RedrawPreview();
            UpdateSizeBoxes();
            e.Handled = true;
            return;
        }

        var edge = ZoneEditOps.FindEdgeNear(_pending, f.Fx, f.Fy,
            EdgeTolerancePx / _monW, EdgeTolerancePx / _monH);
        if (edge.Orientation != ZoneEditOps.EdgeOrientation.None)
        {
            _dragOrientation = edge.Orientation;
            _dragEdge = edge.Value;
            _dragCross = edge.Orientation == ZoneEditOps.EdgeOrientation.Vertical ? f.Fy : f.Fx;
            PreviewCanvas.CaptureMouse();
            e.Handled = true;
            return;
        }

        _selected = ZoneEditOps.ZoneAt(_pending, f.Fx, f.Fy);
        if (_selected >= 0 && SelectedMonitor is { } mon)
        {
            var baseRect = _zones.BaseRect(mon);
            var px = LayoutEngine.ToPixels(_pending[_selected], baseRect);
            StatusText.Text = $"존 {_selected + 1} 선택됨 — 위치 ({px.X - baseRect.X}, {px.Y - baseRect.Y}), 크기 {px.Width}×{px.Height}px";
        }
        RedrawPreview();
        UpdateSizeBoxes();
    }

    void PreviewCanvas_MouseMove(object sender, MouseEventArgs e)
    {
        if (ToFraction(e.GetPosition(PreviewCanvas)) is not { } f) return;

        if (_dragOrientation != ZoneEditOps.EdgeOrientation.None)
        {
            // 기본: 잡은 구간만 이동. Shift를 누르면 경계선 전체 이동
            double? cross = Keyboard.Modifiers.HasFlag(ModifierKeys.Shift) ? null : _dragCross;
            _dragEdge = _dragOrientation == ZoneEditOps.EdgeOrientation.Vertical
                ? ZoneEditOps.MoveVerticalEdge(_pending, _dragEdge, f.Fx, cross)
                : ZoneEditOps.MoveHorizontalEdge(_pending, _dragEdge, f.Fy, cross);

            if (SelectedMonitor is { } mon)
            {
                int px = _dragOrientation == ZoneEditOps.EdgeOrientation.Vertical
                    ? (int)Math.Round(_dragEdge * mon.Bounds.Width)
                    : (int)Math.Round(_dragEdge * mon.Bounds.Height);
                StatusText.Text = $"경계 {(_dragEdge * 100):F1}%  ({px}px)";
            }
            RedrawPreview();
            return;
        }

        // 드래그 중이 아니면 경계 근처에서 커서만 변경
        var edge = ZoneEditOps.FindEdgeNear(_pending, f.Fx, f.Fy,
            EdgeTolerancePx / _monW, EdgeTolerancePx / _monH);
        PreviewCanvas.Cursor = edge.Orientation switch
        {
            ZoneEditOps.EdgeOrientation.Vertical => Cursors.SizeWE,
            ZoneEditOps.EdgeOrientation.Horizontal => Cursors.SizeNS,
            _ => Cursors.Arrow,
        };
    }

    void PreviewCanvas_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (_dragOrientation == ZoneEditOps.EdgeOrientation.None) return;
        _dragOrientation = ZoneEditOps.EdgeOrientation.None;
        PreviewCanvas.ReleaseMouseCapture();
        SetPendingStatus("경계 조절됨");
        UpdateSizeBoxes();
    }

    void PreviewCanvas_MouseLeave(object sender, MouseEventArgs e)
    {
        if (_dragOrientation == ZoneEditOps.EdgeOrientation.None)
            PreviewCanvas.Cursor = Cursors.Arrow;
    }

    // ── 저장 / 해제 ────────────────────────────────────────────────────

    void Clear_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedMonitor is not { } mon) return;
        _pending = new List<FractionRect>();
        _selected = -1;
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

    void SetPendingStatus(string message) =>
        StatusText.Text = message + " — 저장을 눌러야 반영됩니다";

    // ── 미리보기 렌더링 ────────────────────────────────────────────────

    void PreviewCanvas_SizeChanged(object sender, SizeChangedEventArgs e) => RedrawPreview();

    void RedrawPreview()
    {
        PreviewCanvas.Children.Clear();
        if (SelectedMonitor is not { } mon) return;

        double cw = PreviewCanvas.ActualWidth;
        double ch = PreviewCanvas.ActualHeight;
        if (cw < 40 || ch < 40) return;

        var baseRect = _zones.BaseRect(mon);
        double scale = Math.Min((cw - 16) / mon.Bounds.Width, (ch - 16) / mon.Bounds.Height);
        double monW = mon.Bounds.Width * scale;
        double monH = mon.Bounds.Height * scale;
        double monX = (cw - monW) / 2;
        double monY = (ch - monH) / 2;

        // 존 좌표 변환은 기준 영역(작업 영역) 기준
        _monW = baseRect.Width * scale;
        _monH = baseRect.Height * scale;
        _ox = monX + (baseRect.X - mon.Bounds.X) * scale;
        _oy = monY + (baseRect.Y - mon.Bounds.Y) * scale;

        // 모니터 전체(작업표시줄 포함)는 회색, 작업 영역은 흰색으로 구분
        PreviewCanvas.Children.Add(Positioned(new Rectangle
        {
            Width = monW,
            Height = monH,
            Stroke = Brushes.DimGray,
            StrokeThickness = 2,
            Fill = new SolidColorBrush(Color.FromArgb(0xFF, 0xC8, 0xC8, 0xC8)),
        }, monX, monY));
        PreviewCanvas.Children.Add(Positioned(new Rectangle
        {
            Width = _monW,
            Height = _monH,
            Fill = Brushes.White,
        }, _ox, _oy));

        var zoneFill = new SolidColorBrush(Color.FromArgb(0x30, 0x3B, 0x82, 0xF6));
        var zoneStroke = new SolidColorBrush(Color.FromArgb(0xFF, 0x3B, 0x82, 0xF6));
        var selectedFill = new SolidColorBrush(Color.FromArgb(0x60, 0x3B, 0x82, 0xF6));
        var selectedStroke = new SolidColorBrush(Color.FromArgb(0xFF, 0x1D, 0x4E, 0xD8));

        var detailBrush = new SolidColorBrush(Color.FromArgb(0xFF, 0x50, 0x50, 0x50));

        for (int i = 0; i < _pending.Count; i++)
        {
            var f = _pending[i];
            bool isSelected = i == _selected;
            double zx = _ox + f.X * _monW, zy = _oy + f.Y * _monH;
            double zw = f.Width * _monW, zh = f.Height * _monH;

            PreviewCanvas.Children.Add(Positioned(new Rectangle
            {
                Width = Math.Max(0, zw - 2),
                Height = Math.Max(0, zh - 2),
                Stroke = isSelected ? selectedStroke : zoneStroke,
                StrokeThickness = isSelected ? 3 : 1.5,
                Fill = isSelected ? selectedFill : zoneFill,
            }, zx + 1, zy + 1));

            // 존 번호 + 수치 정보 (기준 영역 좌상단 기준 픽셀 좌표/크기와 백분율)
            var px = LayoutEngine.ToPixels(f, baseRect);
            int localX = px.X - baseRect.X;
            int localY = px.Y - baseRect.Y;

            var label = new StackPanel();
            label.Children.Add(new TextBlock
            {
                Text = (i + 1).ToString(),
                FontSize = 15,
                FontWeight = FontWeights.Bold,
                Foreground = isSelected ? selectedStroke : zoneStroke,
                TextAlignment = TextAlignment.Center,
            });
            // 존이 충분히 클 때만 상세 수치 표시
            if (zw >= 110 && zh >= 64)
            {
                label.Children.Add(new TextBlock
                {
                    Text = $"({localX}, {localY})",
                    FontSize = 10.5,
                    Foreground = detailBrush,
                    TextAlignment = TextAlignment.Center,
                });
                label.Children.Add(new TextBlock
                {
                    Text = $"{px.Width} × {px.Height} px",
                    FontSize = 10.5,
                    Foreground = detailBrush,
                    TextAlignment = TextAlignment.Center,
                });
                label.Children.Add(new TextBlock
                {
                    Text = $"{f.Width * 100:F1} × {f.Height * 100:F1} %",
                    FontSize = 10.5,
                    Foreground = detailBrush,
                    TextAlignment = TextAlignment.Center,
                });
            }
            label.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            PreviewCanvas.Children.Add(Positioned(label,
                zx + (zw - label.DesiredSize.Width) / 2,
                zy + (zh - label.DesiredSize.Height) / 2));
        }

        if (_pending.Count == 0)
        {
            PreviewCanvas.Children.Add(Positioned(new TextBlock
            {
                Text = "분할 없음 — 템플릿을 누르거나 분할 버튼으로 시작하세요",
                FontSize = 13,
                Foreground = Brushes.Gray,
            }, _ox + _monW / 2 - 140, _oy + _monH / 2 - 10));
        }
    }

    static UIElement Positioned(UIElement element, double x, double y)
    {
        Canvas.SetLeft(element, x);
        Canvas.SetTop(element, y);
        return element;
    }
}
