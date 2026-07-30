using System.Windows.Interop;
using DisplayDivider.Win32;

namespace DisplayDivider.App.Services;

/// <summary>메시지 전용 창(HWND_MESSAGE)으로 전역 단축키를 등록/수신한다.</summary>
public sealed class HotkeyService : IDisposable
{
    static readonly IntPtr HWND_MESSAGE = new(-3);

    readonly HwndSource _source;
    readonly Dictionary<int, Action> _actions = new();
    int _nextId = 1;
    bool _disposed;

    public HotkeyService()
    {
        var parameters = new HwndSourceParameters("DisplayDivider.Hotkeys", 0, 0)
        {
            WindowStyle = 0,
            ParentWindow = HWND_MESSAGE,
        };
        _source = new HwndSource(parameters);
        _source.AddHook(WndProc);
    }

    public bool Register(uint modifiers, uint vk, Action action)
    {
        int id = _nextId++;
        if (!Native.RegisterHotKey(_source.Handle, id, modifiers | Native.MOD_NOREPEAT, vk))
            return false;
        _actions[id] = action;
        return true;
    }

    IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == Native.WM_HOTKEY && _actions.TryGetValue(wParam.ToInt32(), out var action))
        {
            action();
            handled = true;
        }
        return IntPtr.Zero;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        foreach (int id in _actions.Keys)
            Native.UnregisterHotKey(_source.Handle, id);
        _actions.Clear();
        _source.RemoveHook(WndProc);
        _source.Dispose();
    }
}
