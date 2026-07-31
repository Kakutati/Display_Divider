using DisplayDivider.Core;
using DisplayDivider.Win32;

namespace DisplayDivider.App.Services;

/// <summary>현재 레이아웃 설정 + 모니터 정보를 합쳐 픽셀 존을 제공하는 중앙 서비스.</summary>
public sealed class ZoneService
{
    public LayoutConfig Config { get; private set; } = new();
    public List<MonitorInfo> Monitors { get; private set; } = new();

    public event Action? Changed;

    public void Reload()
    {
        Config = LayoutStore.Load();
        RefreshMonitors();
    }

    public void RefreshMonitors()
    {
        Monitors = MonitorService.GetMonitors();
        Changed?.Invoke();
    }

    public void Save() => LayoutStore.Save(Config);

    public IReadOnlyList<FractionRect> FractionZonesFor(string deviceName) =>
        Config.Monitors.FirstOrDefault(m => m.DeviceName == deviceName)?.Zones
        ?? (IReadOnlyList<FractionRect>)Array.Empty<FractionRect>();

    /// <summary>존 계산의 기준 사각형. 기본은 작업 영역(작업표시줄 제외).</summary>
    public PixelRect BaseRect(MonitorInfo monitor) =>
        Config.UseWorkArea ? monitor.WorkArea : monitor.Bounds;

    public List<PixelRect> ZonesFor(MonitorInfo monitor) =>
        FractionZonesFor(monitor.DeviceName)
            .Select(z => LayoutEngine.ToPixels(z, BaseRect(monitor)))
            .ToList();

    public bool AnyZones => Monitors.Any(m => FractionZonesFor(m.DeviceName).Count > 0);

    public (MonitorInfo Monitor, PixelRect Zone)? FindZoneAt(int x, int y)
    {
        foreach (var mon in Monitors)
        {
            if (!mon.Bounds.Contains(x, y)) continue;
            foreach (var zone in ZonesFor(mon))
                if (zone.Contains(x, y))
                    return (mon, zone);
        }
        return null;
    }

    /// <summary>모든 모니터의 존을 좌→우 순서로 평탄화한 목록.</summary>
    public List<PixelRect> AllZonesOrdered() =>
        Monitors.SelectMany(ZonesFor).ToList();

    public MonitorInfo? MonitorForWindow(IntPtr hwnd)
    {
        var (cx, cy) = WindowMover.GetVisibleBounds(hwnd).Center;
        return Monitors.FirstOrDefault(m => m.Bounds.Contains(cx, cy))
               ?? Monitors.FirstOrDefault();
    }

    public void SetZones(string deviceName, List<FractionRect> zones)
    {
        var layout = Config.Monitors.FirstOrDefault(m => m.DeviceName == deviceName);
        if (layout is null)
        {
            layout = new MonitorLayout { DeviceName = deviceName };
            Config.Monitors.Add(layout);
        }
        layout.Zones = zones;
        Save();
        Changed?.Invoke();
    }
}
