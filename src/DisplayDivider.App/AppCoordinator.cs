using System.Windows;
using System.Windows.Threading;
using DisplayDivider.App.Controllers;
using DisplayDivider.App.Services;
using DisplayDivider.App.UI;
using DisplayDivider.Win32;
using Microsoft.Win32;

namespace DisplayDivider.App;

/// <summary>서비스/컨트롤러/UI를 조립하고 수명을 관리한다.</summary>
public sealed class AppCoordinator : IDisposable
{
    ZoneService? _zones;
    WindowTracker? _tracker;
    OverlayManager? _overlays;
    SnapController? _snap;
    MaximizeController? _maximize;
    FullscreenController? _fullscreen;
    HotkeyService? _hotkeys;
    TrayIcon? _tray;
    ZoneEditorWindow? _editor;
    RulesWindow? _rules;
    DispatcherTimer? _workAreaSettleTimer;

    public void Start()
    {
        _zones = new ZoneService();
        _zones.Reload();

        _tracker = new WindowTracker();
        _overlays = new OverlayManager(_zones);
        _snap = new SnapController(_tracker, _zones, _overlays);
        _maximize = new MaximizeController(_tracker, _zones);
        _fullscreen = new FullscreenController(_tracker, _zones);

        _hotkeys = new HotkeyService();
        _ = new HotkeyController(_hotkeys, _zones);

        _tray = new TrayIcon(_zones, OpenEditor, OpenRules, () => _zones.Reload(), () => Application.Current.Shutdown());

        SystemEvents.DisplaySettingsChanged += OnDisplaySettingsChanged;
        SystemEvents.UserPreferenceChanged += OnUserPreferenceChanged;

        // 처음 실행(존이 하나도 없음)이면 편집기를 바로 열어준다
        if (!_zones.AnyZones)
            OpenEditor();
    }

    void OnDisplaySettingsChanged(object? sender, EventArgs e)
    {
        _zones?.RefreshMonitors();

        // 모니터 전원 Off/On 등으로 재검색된 직후에는 작업표시줄이 아직 재배치되지 않아
        // 작업 영역이 일시적으로 전체 화면과 같게 조회될 수 있다. 안정화 후 한 번 더 갱신.
        _workAreaSettleTimer?.Stop();
        _workAreaSettleTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
        _workAreaSettleTimer.Tick += (_, _) =>
        {
            _workAreaSettleTimer?.Stop();
            _zones?.RefreshMonitors();
        };
        _workAreaSettleTimer.Start();
    }

    // 작업표시줄 위치/크기 변경(작업 영역 변경)은 DisplaySettingsChanged가 아니라
    // WM_SETTINGCHANGE → UserPreferenceChanged로 통지된다.
    void OnUserPreferenceChanged(object? sender, UserPreferenceChangedEventArgs e)
    {
        if (e.Category is UserPreferenceCategory.Desktop or UserPreferenceCategory.General)
            _zones?.RefreshMonitors();
    }

    void OpenEditor()
    {
        if (_zones is null) return;
        if (_editor is { IsLoaded: true })
        {
            _editor.Activate();
            return;
        }
        _editor = new ZoneEditorWindow(_zones);
        _editor.Closed += (_, _) => _editor = null;
        _editor.Show();
    }

    void OpenRules()
    {
        if (_zones is null) return;
        if (_rules is { IsLoaded: true })
        {
            _rules.Activate();
            return;
        }
        _rules = new RulesWindow(_zones);
        _rules.Closed += (_, _) => _rules = null;
        _rules.Show();
    }

    public void Dispose()
    {
        SystemEvents.DisplaySettingsChanged -= OnDisplaySettingsChanged;
        SystemEvents.UserPreferenceChanged -= OnUserPreferenceChanged;
        _workAreaSettleTimer?.Stop();
        _tray?.Dispose();
        _hotkeys?.Dispose();
        _snap?.Dispose();
        _maximize?.Dispose();
        _fullscreen?.Dispose();
        _overlays?.Dispose();
        _tracker?.Dispose();
    }
}
