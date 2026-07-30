using System.Runtime.InteropServices;
using DisplayDivider.App.Services;
using DisplayDivider.Core;
using DisplayDivider.Win32;

namespace DisplayDivider.App.Controllers;

/// <summary>
/// "가짜 최대화": 창이 최대화되는 순간을 감지해 존 크기의 일반 창으로 되돌린다.
/// - 존이 있는 모니터에서만 동작
/// - Shift+최대화는 모니터 전체 최대화 허용
/// - 존-최대화 상태에서 다시 최대화하면 원래 크기로 토글
/// </summary>
public sealed class MaximizeController : IDisposable
{
    readonly WindowTracker _tracker;
    readonly ZoneService _zones;

    /// <summary>존-최대화된 창 → 이전(복원용) 사각형.</summary>
    readonly Dictionary<IntPtr, PixelRect> _zoneMaximized = new();

    /// <summary>Shift로 전체 최대화를 허용한 창. 최대화가 풀리면 자동 해제.</summary>
    readonly HashSet<IntPtr> _fullMaxAllowed = new();

    public MaximizeController(WindowTracker tracker, ZoneService zones)
    {
        _tracker = tracker;
        _zones = zones;
        _tracker.LocationChanged += OnLocationChanged;
        _tracker.MoveSizeEnded += OnMoveSizeEnded;
    }

    void OnMoveSizeEnded(IntPtr hwnd)
    {
        // 사용자가 직접 이동/리사이즈하면 존-최대화 상태를 해제
        _zoneMaximized.Remove(hwnd);
    }

    void OnLocationChanged(IntPtr hwnd)
    {
        if (!_zones.Config.FakeMaximizeEnabled) return;

        if (!Native.IsZoomed(hwnd))
        {
            _fullMaxAllowed.Remove(hwnd);
            return;
        }
        if (_fullMaxAllowed.Contains(hwnd)) return;
        if (Native.IsIconic(hwnd) || !WindowFilter.IsManageable(hwnd)) return;

        var monitor = _zones.MonitorForWindow(hwnd);
        if (monitor is null) return;

        var zones = _zones.ZonesFor(monitor);
        if (zones.Count == 0) return; // 존 없는 모니터는 일반 최대화 그대로

        if (Native.IsShiftDown())
        {
            _fullMaxAllowed.Add(hwnd);
            _zoneMaximized.Remove(hwnd);
            return;
        }

        if (_zoneMaximized.TryGetValue(hwnd, out var savedRect))
        {
            // 존-최대화 상태에서 다시 최대화 → 원래 크기로 토글
            _zoneMaximized.Remove(hwnd);
            Native.ShowWindow(hwnd, Native.SW_RESTORE);
            WindowMover.MoveTo(hwnd, savedRect);
            return;
        }

        // 최대화 이전의 일반 상태 크기를 저장해 두고, 존 크기로 가둔다
        var placement = new WINDOWPLACEMENT { length = Marshal.SizeOf<WINDOWPLACEMENT>() };
        bool hasNormal = Native.GetWindowPlacement(hwnd, ref placement);
        var normal = hasNormal
            ? new PixelRect(
                placement.rcNormalPosition.Left,
                placement.rcNormalPosition.Top,
                placement.rcNormalPosition.Width,
                placement.rcNormalPosition.Height)
            : default;

        var zone = PickZone(zones, normal, hasNormal);

        Native.ShowWindow(hwnd, Native.SW_RESTORE);
        WindowMover.MoveTo(hwnd, zone);
        if (hasNormal)
            _zoneMaximized[hwnd] = normal;
    }

    static PixelRect PickZone(List<PixelRect> zones, PixelRect normalRect, bool hasNormal)
    {
        if (hasNormal)
        {
            var (cx, cy) = normalRect.Center;
            foreach (var z in zones)
                if (z.Contains(cx, cy))
                    return z;
        }
        if (Native.GetCursorPos(out var pt))
            foreach (var z in zones)
                if (z.Contains(pt.X, pt.Y))
                    return z;
        return zones[0];
    }

    public void Dispose()
    {
        _tracker.LocationChanged -= OnLocationChanged;
        _tracker.MoveSizeEnded -= OnMoveSizeEnded;
    }
}
