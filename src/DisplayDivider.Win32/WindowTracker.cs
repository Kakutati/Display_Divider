namespace DisplayDivider.Win32;

/// <summary>
/// SetWinEventHook 기반 전역 창 이벤트 감시.
/// 메시지 루프가 있는 스레드(WPF UI 스레드)에서 생성해야 한다.
/// </summary>
public sealed class WindowTracker : IDisposable
{
    readonly Native.WinEventProc _proc; // GC 방지를 위해 필드로 유지
    readonly List<IntPtr> _hooks = new();
    bool _disposed;

    public event Action<IntPtr>? MoveSizeStarted;
    public event Action<IntPtr>? MoveSizeEnded;
    public event Action<IntPtr>? LocationChanged;

    public WindowTracker()
    {
        _proc = Callback;
        const uint flags = Native.WINEVENT_OUTOFCONTEXT | Native.WINEVENT_SKIPOWNPROCESS;
        _hooks.Add(Native.SetWinEventHook(
            Native.EVENT_SYSTEM_MOVESIZESTART, Native.EVENT_SYSTEM_MOVESIZEEND,
            IntPtr.Zero, _proc, 0, 0, flags));
        _hooks.Add(Native.SetWinEventHook(
            Native.EVENT_OBJECT_LOCATIONCHANGE, Native.EVENT_OBJECT_LOCATIONCHANGE,
            IntPtr.Zero, _proc, 0, 0, flags));
    }

    void Callback(IntPtr hook, uint ev, IntPtr hwnd, int idObject, int idChild, uint thread, uint time)
    {
        if (hwnd == IntPtr.Zero || idObject != Native.OBJID_WINDOW || idChild != 0)
            return;
        try
        {
            switch (ev)
            {
                case Native.EVENT_SYSTEM_MOVESIZESTART: MoveSizeStarted?.Invoke(hwnd); break;
                case Native.EVENT_SYSTEM_MOVESIZEEND: MoveSizeEnded?.Invoke(hwnd); break;
                case Native.EVENT_OBJECT_LOCATIONCHANGE: LocationChanged?.Invoke(hwnd); break;
            }
        }
        catch
        {
            // 훅 콜백에서 예외가 새어나가면 프로세스가 불안정해지므로 삼킨다.
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        foreach (var h in _hooks)
            if (h != IntPtr.Zero) Native.UnhookWinEvent(h);
        _hooks.Clear();
    }
}
