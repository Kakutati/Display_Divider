using System.Runtime.InteropServices;
using DisplayDivider.Core;

namespace DisplayDivider.Win32;

/// <param name="DeviceName">GDI 장치 이름(\\.\DISPLAYn). 재부팅 시 바뀔 수 있어 표시용으로만 사용.</param>
/// <param name="StableId">모니터 하드웨어 기반 장치 인터페이스 경로. 레이아웃 매칭 키.</param>
public sealed record MonitorInfo(string DeviceName, string StableId, PixelRect Bounds, PixelRect WorkArea, bool IsPrimary);

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
                    GetStableId(mi.szDevice),
                    ToPixelRect(mi.rcMonitor),
                    ToPixelRect(mi.rcWork),
                    (mi.dwFlags & Native.MONITORINFOF_PRIMARY) != 0));
            return true;
        };
        Native.EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, callback, IntPtr.Zero);
        GC.KeepAlive(callback);
        return list.OrderBy(m => m.Bounds.X).ThenBy(m => m.Bounds.Y).ToList();
    }

    /// <summary>
    /// GDI 이름(\\.\DISPLAYn)은 재부팅 시 바뀔 수 있으므로, 모니터 하드웨어의
    /// 장치 인터페이스 경로(EDID 기반, 예: \\?\DISPLAY#GSM5B08#...)를 안정 키로 사용한다.
    /// 조회 실패 시 GDI 이름을 그대로 반환한다.
    /// </summary>
    static string GetStableId(string gdiDeviceName)
    {
        var dd = new DISPLAY_DEVICE { cb = Marshal.SizeOf<DISPLAY_DEVICE>() };
        if (Native.EnumDisplayDevicesW(gdiDeviceName, 0, ref dd, Native.EDD_GET_DEVICE_INTERFACE_NAME)
            && !string.IsNullOrEmpty(dd.DeviceID))
            return dd.DeviceID;
        return gdiDeviceName;
    }

    public static PixelRect ToPixelRect(RECT r) => new(r.Left, r.Top, r.Width, r.Height);
}
