using System.Windows.Threading;
using DisplayDivider.App.Services;
using DisplayDivider.App.UI;
using DisplayDivider.Win32;

namespace DisplayDivider.App.Controllers;

/// <summary>
/// 창 드래그 시 존 오버레이를 표시하고, 드롭 위치의 존으로 스냅한다.
/// 기본 모드: Ctrl을 누른 채 드롭할 때만 스냅. 항상 스냅 모드에서는 Shift로 예외.
/// </summary>
public sealed class SnapController : IDisposable
{
    readonly WindowTracker _tracker;
    readonly ZoneService _zones;
    readonly OverlayManager _overlays;
    readonly DispatcherTimer _cursorTimer;

    IntPtr _dragHwnd;

    public SnapController(WindowTracker tracker, ZoneService zones, OverlayManager overlays)
    {
        _tracker = tracker;
        _zones = zones;
        _overlays = overlays;

        _cursorTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(33) };
        _cursorTimer.Tick += OnTick;

        _tracker.MoveSizeStarted += OnMoveSizeStarted;
        _tracker.MoveSizeEnded += OnMoveSizeEnded;
    }

    void OnMoveSizeStarted(IntPtr hwnd)
    {
        if (!_zones.Config.DragSnapEnabled || !_zones.AnyZones) return;
        if (!WindowFilter.IsManageable(hwnd)) return;

        _dragHwnd = hwnd;
        _overlays.ShowAll();
        _cursorTimer.Start();
    }

    void OnTick(object? sender, EventArgs e)
    {
        if (Native.GetCursorPos(out var pt))
            _overlays.UpdateHighlight(pt.X, pt.Y);
    }

    void OnMoveSizeEnded(IntPtr hwnd)
    {
        if (_dragHwnd == IntPtr.Zero || hwnd != _dragHwnd) return;

        _cursorTimer.Stop();
        _overlays.HideAll();
        _dragHwnd = IntPtr.Zero;

        if (Native.IsShiftDown()) return;
        if (_zones.Config.SnapRequiresCtrl && !Native.IsCtrlDown()) return;

        if (Native.GetCursorPos(out var pt) && _zones.FindZoneAt(pt.X, pt.Y) is { } hit)
            WindowMover.MoveTo(hwnd, hit.Zone);
    }

    public void Dispose()
    {
        _tracker.MoveSizeStarted -= OnMoveSizeStarted;
        _tracker.MoveSizeEnded -= OnMoveSizeEnded;
        _cursorTimer.Stop();
    }
}
