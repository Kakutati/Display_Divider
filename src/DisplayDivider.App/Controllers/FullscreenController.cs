using DisplayDivider.App.Services;
using DisplayDivider.Core;
using DisplayDivider.Win32;

namespace DisplayDivider.App.Controllers;

/// <summary>
/// 테두리 없는 전체화면(borderless) 창을 앱 규칙에 따라 지정 존에 가둔다.
/// 게임이 스스로 전체화면으로 되돌리면 LOCATIONCHANGE 이벤트로 다시 감지되어 재적용된다.
/// 외부 SetWindowPos만 사용 — 게임 프로세스에 코드를 주입하지 않는다.
/// </summary>
public sealed class FullscreenController : IDisposable
{
    /// <summary>같은 창에 대한 재적용 최소 간격. 무한 루프로 게임과 싸우는 것을 방지.</summary>
    static readonly TimeSpan ApplyCooldown = TimeSpan.FromMilliseconds(400);

    readonly WindowTracker _tracker;
    readonly ZoneService _zones;
    readonly Dictionary<IntPtr, DateTime> _lastApplied = new();

    public FullscreenController(WindowTracker tracker, ZoneService zones)
    {
        _tracker = tracker;
        _zones = zones;
        _tracker.LocationChanged += OnLocationChanged;
    }

    void OnLocationChanged(IntPtr hwnd)
    {
        if (!_zones.Config.FullscreenConfineEnabled || _zones.Config.Rules.Count == 0)
            return;

        // 창 중심이 속한 모니터의 전체 해상도를 기준으로 전체화면 판정
        var monitor = _zones.MonitorForWindow(hwnd);
        if (monitor is null) return;
        if (!WindowProbe.IsBorderlessFullscreen(hwnd, monitor.Bounds)) return;
        if (!WindowFilter.IsManageable(hwnd)) return;

        var processName = WindowProbe.GetProcessName(hwnd);
        if (processName is null) return;

        var rule = AppRules.Find(_zones.Config.Rules, processName);
        if (rule is null) return;

        // 규칙이 지정한 모니터/존 (모니터가 사라졌으면 현재 모니터로 대체)
        var targetMonitor = _zones.Monitors.FirstOrDefault(m => m.DeviceName == rule.MonitorDevice) ?? monitor;
        var zones = _zones.ZonesFor(targetMonitor);
        if (zones.Count == 0) return;
        var zone = zones[Math.Clamp(rule.ZoneIndex, 0, zones.Count - 1)];

        var now = DateTime.UtcNow;
        if (_lastApplied.TryGetValue(hwnd, out var last) && now - last < ApplyCooldown)
            return;
        if (_lastApplied.Count > 64) _lastApplied.Clear();
        _lastApplied[hwnd] = now;

        WindowMover.MoveTo(hwnd, zone);
    }

    public void Dispose() => _tracker.LocationChanged -= OnLocationChanged;
}
