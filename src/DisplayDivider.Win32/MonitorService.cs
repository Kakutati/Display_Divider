using System.Runtime.InteropServices;
using DisplayDivider.Core;

namespace DisplayDivider.Win32;

public sealed record MonitorInfo(string DeviceName, PixelRect Bounds, PixelRect WorkArea, bool IsPrimary);

public static class MonitorService
{
    /// <summary>현재 연결된 모니터 목록. 좌→우, 상→하 순으로 정렬된다.</summary>
    public static List<MonitorInfo> GetMonitors()
    {
        var list = new List<MonitorInfo>();
        Native.MonitorEnumProc callback = (IntPtr hMonitor, IntPtr hdc, ref RECT rc, IntPtr data) =>
        {
            var mi = new MONITORINFOEX { cbSize = Marshal.SizeOf<MONITORINFOEX>() };
            if (Native.GetMonitorInfoW(hMonitor, ref mi))
                list.Add(new MonitorInfo(
                    mi.szDevice,
                    ToPixelRect(mi.rcMonitor),
                    ToPixelRect(mi.rcWork),
                    (mi.dwFlags & Native.MONITORINFOF_PRIMARY) != 0));
            return true;
        };
        Native.EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, callback, IntPtr.Zero);
        GC.KeepAlive(callback);
        return list.OrderBy(m => m.Bounds.X).ThenBy(m => m.Bounds.Y).ToList();
    }

    public static PixelRect ToPixelRect(RECT r) => new(r.Left, r.Top, r.Width, r.Height);
}
