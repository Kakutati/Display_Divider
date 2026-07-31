using System.Diagnostics;
using DisplayDivider.Core;

namespace DisplayDivider.Win32;

/// <summary>창의 프로세스/전체화면 상태 조회. 게임 프로세스에 코드를 주입하지 않는다(안티치트 안전).</summary>
public static class WindowProbe
{
    // pid → 프로세스 이름 캐시 (Process.GetProcessById가 비싸므로)
    static readonly Dictionary<uint, string> ProcessNameCache = new();

    public static string? GetProcessName(IntPtr hwnd)
    {
        Native.GetWindowThreadProcessId(hwnd, out uint pid);
        if (pid == 0) return null;

        if (ProcessNameCache.TryGetValue(pid, out var cached))
            return cached;

        try
        {
            using var process = Process.GetProcessById((int)pid);
            var name = process.ProcessName;
            if (ProcessNameCache.Count > 256) ProcessNameCache.Clear();
            ProcessNameCache[pid] = name;
            return name;
        }
        catch (ArgumentException)
        {
            return null; // 프로세스가 이미 종료됨
        }
    }

    /// <summary>
    /// 테두리 없는 전체화면(borderless fullscreen) 판정:
    /// 캡션이 없는 창이 모니터 전체(작업표시줄 포함)를 덮고 있으면 true.
    /// </summary>
    public static bool IsBorderlessFullscreen(IntPtr hwnd, PixelRect monitorBounds)
    {
        long style = Native.GetWindowLongPtr(hwnd, Native.GWL_STYLE);
        if ((style & Native.WS_CAPTION) == Native.WS_CAPTION)
            return false; // 일반 캡션 창

        if (!Native.GetWindowRect(hwnd, out var rc))
            return false;

        const int tolerance = 2;
        return rc.Left <= monitorBounds.X + tolerance
            && rc.Top <= monitorBounds.Y + tolerance
            && rc.Right >= monitorBounds.Right - tolerance
            && rc.Bottom >= monitorBounds.Bottom - tolerance;
    }

    /// <summary>제목이 있는 최상위 창들의 프로세스 이름 목록(중복 제거, 정렬). 자기 자신은 제외.</summary>
    public static List<string> GetTopLevelProcessNames()
    {
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        uint ownPid = (uint)Environment.ProcessId;

        Native.EnumWindowsProc callback = (hwnd, _) =>
        {
            if (!Native.IsWindowVisible(hwnd)) return true;
            if (Native.GetWindowTextLengthW(hwnd) == 0) return true;
            Native.GetWindowThreadProcessId(hwnd, out uint pid);
            if (pid == 0 || pid == ownPid) return true;
            if (!WindowFilter.IsManageable(hwnd)) return true;

            var name = GetProcessName(hwnd);
            if (!string.IsNullOrEmpty(name))
                names.Add(name);
            return true;
        };
        Native.EnumWindows(callback, IntPtr.Zero);
        GC.KeepAlive(callback);

        return names.OrderBy(n => n, StringComparer.OrdinalIgnoreCase).ToList();
    }
}
