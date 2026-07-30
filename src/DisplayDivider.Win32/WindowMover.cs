using System.Runtime.InteropServices;
using DisplayDivider.Core;

namespace DisplayDivider.Win32;

public static class WindowMover
{
    /// <summary>
    /// DWM 확장 프레임(그림자 제외 실제 보이는 영역) 기준 창 사각형.
    /// </summary>
    public static PixelRect GetVisibleBounds(IntPtr hwnd)
    {
        Native.GetWindowRect(hwnd, out var wr);
        if (Native.DwmGetWindowAttribute(hwnd, Native.DWMWA_EXTENDED_FRAME_BOUNDS, out RECT efb, Marshal.SizeOf<RECT>()) == 0
            && efb.Width > 0 && efb.Height > 0)
            return new PixelRect(efb.Left, efb.Top, efb.Width, efb.Height);
        return new PixelRect(wr.Left, wr.Top, wr.Width, wr.Height);
    }

    /// <summary>
    /// 창의 "보이는 영역"이 target과 일치하도록 이동/리사이즈한다.
    /// GetWindowRect와 확장 프레임의 차이(투명 테두리)만큼 보정해 픽셀이 정확히 맞는다.
    /// </summary>
    public static void MoveTo(IntPtr hwnd, PixelRect target)
    {
        if (Native.IsZoomed(hwnd))
            Native.ShowWindow(hwnd, Native.SW_RESTORE);

        Native.GetWindowRect(hwnd, out var wr);
        var efb = wr;
        if (Native.DwmGetWindowAttribute(hwnd, Native.DWMWA_EXTENDED_FRAME_BOUNDS, out RECT e, Marshal.SizeOf<RECT>()) == 0
            && e.Width > 0 && e.Height > 0)
            efb = e;

        int padLeft = efb.Left - wr.Left;
        int padTop = efb.Top - wr.Top;
        int padRight = wr.Right - efb.Right;
        int padBottom = wr.Bottom - efb.Bottom;

        Native.SetWindowPos(hwnd, IntPtr.Zero,
            target.X - padLeft,
            target.Y - padTop,
            target.Width + padLeft + padRight,
            target.Height + padTop + padBottom,
            Native.SWP_NOZORDER | Native.SWP_NOACTIVATE);
    }
}
