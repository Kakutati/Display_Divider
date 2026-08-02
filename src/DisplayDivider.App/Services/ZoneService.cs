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
        MigrateLayoutKeys();
        Changed?.Invoke();
    }

    /// <summary>
    /// GDI 이름(\\.\DISPLAYn)은 재부팅 시 바뀔 수 있으므로 안정 ID를 기준으로 레이아웃을 유지한다.
    /// - 안정 ID로 매칭된 레이아웃의 GDI 이름이 바뀌었으면 갱신
    /// - 구버전 설정(안정 ID 없음)은 GDI 이름이 일치하는 연결된 모니터로 안정 ID를 백필
    /// </summary>
    void MigrateLayoutKeys()
    {
        bool dirty = false;
        foreach (var mon in Monitors)
        {
            var byStable = Config.Monitors.FirstOrDefault(l => l.StableId == mon.StableId);
            if (byStable is not null)
            {
                if (byStable.DeviceName != mon.DeviceName)
                {
                    byStable.DeviceName = mon.DeviceName;
                    dirty = true;
                }
                continue;
            }
            var byName = Config.Monitors.FirstOrDefault(l => l.StableId is null && l.DeviceName == mon.DeviceName);
            if (byName is not null)
            {
                byName.StableId = mon.StableId;
                dirty = true;
            }
        }
        if (dirty) Save();
    }

    public void Save() => LayoutStore.Save(Config);

    MonitorLayout? LayoutFor(MonitorInfo monitor) =>
        Config.Monitors.FirstOrDefault(l => l.StableId == monitor.StableId)
        ?? Config.Monitors.FirstOrDefault(l => l.StableId is null && l.DeviceName == monitor.DeviceName);

    public IReadOnlyList<FractionRect> FractionZonesFor(MonitorInfo monitor) =>
        LayoutFor(monitor)?.Zones ?? (IReadOnlyList<FractionRect>)Array.Empty<FractionRect>();

    /// <summary>존 계산의 기준 사각형. 기본은 작업 영역(작업표시줄 제외).</summary>
    public PixelRect BaseRect(MonitorInfo monitor) =>
        Config.UseWorkArea ? monitor.WorkArea : monitor.Bounds;

    public List<PixelRect> ZonesFor(MonitorInfo monitor) =>
        FractionZonesFor(monitor)
            .Select(z => LayoutEngine.ToPixels(z, BaseRect(monitor)))
            .ToList();

    public bool AnyZones => Monitors.Any(m => FractionZonesFor(m).Count > 0);

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

    public void SetZones(MonitorInfo monitor, List<FractionRect> zones)
    {
        var layout = LayoutFor(monitor);
        if (layout is null)
        {
            layout = new MonitorLayout();
            Config.Monitors.Add(layout);
        }
        layout.DeviceName = monitor.DeviceName;
        layout.StableId = monitor.StableId;
        layout.Zones = zones;
        Save();
        Changed?.Invoke();
    }
}
