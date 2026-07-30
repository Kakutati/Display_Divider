using DisplayDivider.App.Services;

namespace DisplayDivider.App.UI;

/// <summary>존이 설정된 모든 모니터에 오버레이를 띄우고 하이라이트를 갱신한다.</summary>
public sealed class OverlayManager : IDisposable
{
    readonly ZoneService _zones;
    readonly List<OverlayWindow> _active = new();

    public OverlayManager(ZoneService zones)
    {
        _zones = zones;
    }

    public void ShowAll()
    {
        HideAll();
        string? hint = _zones.Config.SnapRequiresCtrl
            ? "Ctrl을 누른 채 놓으면 존에 맞춰집니다"
            : null;

        foreach (var mon in _zones.Monitors)
        {
            var zonesPx = _zones.ZonesFor(mon);
            if (zonesPx.Count == 0) continue;

            var overlay = new OverlayWindow();
            overlay.Setup(mon.Bounds, zonesPx, hint);
            _active.Add(overlay);
        }
    }

    public void UpdateHighlight(int cursorX, int cursorY)
    {
        foreach (var overlay in _active)
            overlay.SetHighlight(overlay.ZoneIndexAt(cursorX, cursorY));
    }

    public void HideAll()
    {
        foreach (var overlay in _active)
            overlay.Close();
        _active.Clear();
    }

    public void Dispose() => HideAll();
}
