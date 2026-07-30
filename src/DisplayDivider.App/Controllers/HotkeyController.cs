using DisplayDivider.App.Services;
using DisplayDivider.Win32;

namespace DisplayDivider.App.Controllers;

/// <summary>
/// 전역 단축키:
///  Ctrl+Alt+←/→ : 활성 창을 이전/다음 존으로 이동
///  Ctrl+Alt+↑   : 활성 창을 현재 존 크기로 채우기
/// </summary>
public sealed class HotkeyController
{
    readonly ZoneService _zones;

    public HotkeyController(HotkeyService hotkeys, ZoneService zones)
    {
        _zones = zones;
        const uint mods = Native.MOD_CONTROL | Native.MOD_ALT;
        hotkeys.Register(mods, Native.VK_LEFT, () => MoveForeground(-1));
        hotkeys.Register(mods, Native.VK_RIGHT, () => MoveForeground(+1));
        hotkeys.Register(mods, Native.VK_UP, FillCurrentZone);
    }

    static IntPtr ForegroundRoot()
    {
        var hwnd = Native.GetForegroundWindow();
        return hwnd == IntPtr.Zero ? IntPtr.Zero : Native.GetAncestor(hwnd, Native.GA_ROOT);
    }

    void FillCurrentZone()
    {
        var hwnd = ForegroundRoot();
        if (!WindowFilter.IsManageable(hwnd)) return;

        var (cx, cy) = WindowMover.GetVisibleBounds(hwnd).Center;
        if (_zones.FindZoneAt(cx, cy) is { } hit)
            WindowMover.MoveTo(hwnd, hit.Zone);
    }

    void MoveForeground(int direction)
    {
        var hwnd = ForegroundRoot();
        if (!WindowFilter.IsManageable(hwnd)) return;

        var all = _zones.AllZonesOrdered();
        if (all.Count == 0) return;

        var (cx, cy) = WindowMover.GetVisibleBounds(hwnd).Center;
        int current = all.FindIndex(z => z.Contains(cx, cy));
        if (current < 0)
        {
            // 존 밖의 창은 중심이 가장 가까운 존 기준으로 이동
            double best = double.MaxValue;
            for (int i = 0; i < all.Count; i++)
            {
                var (zx, zy) = all[i].Center;
                double d = (double)(zx - cx) * (zx - cx) + (double)(zy - cy) * (zy - cy);
                if (d < best) { best = d; current = i; }
            }
        }

        int n = all.Count;
        int target = ((current + direction) % n + n) % n;
        WindowMover.MoveTo(hwnd, all[target]);
    }
}
