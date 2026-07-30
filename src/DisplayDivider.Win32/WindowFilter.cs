using System.Text;

namespace DisplayDivider.Win32;

/// <summary>스냅/최대화 가두기 대상이 되는 창인지 판정한다.</summary>
public static class WindowFilter
{
    static readonly HashSet<string> BlockedClasses = new(StringComparer.Ordinal)
    {
        "Shell_TrayWnd",            // 작업표시줄
        "Shell_SecondaryTrayWnd",   // 보조 모니터 작업표시줄
        "Progman",                  // 바탕화면
        "WorkerW",
        "Windows.UI.Core.CoreWindow",
        "XamlExplorerHostIslandWindow",
        "ForegroundStaging",
        "MultitaskingViewFrame",    // 작업 보기
    };

    public static bool IsManageable(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero || !Native.IsWindow(hwnd) || !Native.IsWindowVisible(hwnd))
            return false;
        if (Native.GetAncestor(hwnd, Native.GA_ROOT) != hwnd)
            return false;

        long style = Native.GetWindowLongPtr(hwnd, Native.GWL_STYLE);
        if ((style & Native.WS_CHILD) != 0)
            return false;

        long exStyle = Native.GetWindowLongPtr(hwnd, Native.GWL_EXSTYLE);
        if ((exStyle & Native.WS_EX_TOOLWINDOW) != 0)
            return false;

        // UWP 등 DWM에 의해 숨겨진(cloaked) 창 제외
        if (Native.DwmGetWindowAttribute(hwnd, Native.DWMWA_CLOAKED, out int cloaked, sizeof(int)) == 0 && cloaked != 0)
            return false;

        var sb = new StringBuilder(256);
        Native.GetClassName(hwnd, sb, sb.Capacity);
        return !BlockedClasses.Contains(sb.ToString());
    }
}
