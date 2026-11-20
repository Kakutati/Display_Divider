using DisplayDivider.App.Services;
using WF = System.Windows.Forms;

namespace DisplayDivider.App.UI;

/// <summary>시스템 트레이 아이콘과 컨텍스트 메뉴.</summary>
public sealed class TrayIcon : IDisposable
{
    readonly WF.NotifyIcon _icon;

    public TrayIcon(ZoneService zones, Action openEditor, Action openRules, Action reload, Action exit)
    {
        var menu = new WF.ContextMenuStrip();

        var editItem = new WF.ToolStripMenuItem("존 편집…");
        editItem.Click += (_, _) => openEditor();

        var rulesItem = new WF.ToolStripMenuItem("전체화면 규칙…");
        rulesItem.Click += (_, _) => openRules();

        var dragSnapItem = new WF.ToolStripMenuItem("드래그 스냅 사용")
        {
            Checked = zones.Config.DragSnapEnabled,
            CheckOnClick = true,
        };
        dragSnapItem.CheckedChanged += (_, _) =>
        {
            zones.Config.DragSnapEnabled = dragSnapItem.Checked;
            zones.Save();
        };

        var ctrlSnapItem = new WF.ToolStripMenuItem("스냅 시 Ctrl 필요 (해제하면 항상 스냅)")
        {
            Checked = zones.Config.SnapRequiresCtrl,
            CheckOnClick = true,
        };
        ctrlSnapItem.CheckedChanged += (_, _) =>
        {
            zones.Config.SnapRequiresCtrl = ctrlSnapItem.Checked;
            zones.Save();
        };

        var fakeMaxItem = new WF.ToolStripMenuItem("최대화를 존에 가두기 (Shift로 예외)")
        {
            Checked = zones.Config.FakeMaximizeEnabled,
            CheckOnClick = true,
        };
        fakeMaxItem.CheckedChanged += (_, _) =>
        {
            zones.Config.FakeMaximizeEnabled = fakeMaxItem.Checked;
            zones.Save();
        };

        var fullscreenItem = new WF.ToolStripMenuItem("전체화면 창 존 가두기 (규칙 기반)")
        {
            Checked = zones.Config.FullscreenConfineEnabled,
            CheckOnClick = true,
        };
        fullscreenItem.CheckedChanged += (_, _) =>
        {
            zones.Config.FullscreenConfineEnabled = fullscreenItem.Checked;
            zones.Save();
        };

        var workAreaItem = new WF.ToolStripMenuItem("작업표시줄 겹침 방지 (작업 영역 기준)")
        {
            Checked = zones.Config.UseWorkArea,
            CheckOnClick = true,
        };
        workAreaItem.CheckedChanged += (_, _) =>
        {
            zones.Config.UseWorkArea = workAreaItem.Checked;
            zones.Save();
        };

        var startupItem = new WF.ToolStripMenuItem(StartupService.IsElevated
            ? "Windows 시작 시 자동 실행 (관리자 권한)"
            : "Windows 시작 시 자동 실행")
        {
            Checked = StartupService.IsEnabled(),
        };
        startupItem.Click += (_, _) =>
        {
            if (!StartupService.SetEnabled(!startupItem.Checked))
                WF.MessageBox.Show(StartupService.IsElevated
                        ? "자동 실행 설정을 바꾸지 못했습니다."
                        : "자동 실행 설정을 바꾸지 못했습니다.\n관리자 권한으로 등록된 항목은 관리자 권한으로 재시작한 뒤 해제하세요.",
                    "Display Divider");
            startupItem.Checked = StartupService.IsEnabled(); // 실제 상태 반영
        };

        // 관리자 권한 창은 UIPI 때문에 일반 권한 프로세스가 이동/크기 변경할 수 없다.
        var elevateItem = new WF.ToolStripMenuItem("관리자 권한으로 재시작")
        {
            Visible = !StartupService.IsElevated,
        };
        elevateItem.Click += (_, _) =>
        {
            if (StartupService.StartElevated()) exit();
        };

        var reloadItem = new WF.ToolStripMenuItem("레이아웃 다시 읽기");
        reloadItem.Click += (_, _) => reload();

        var exitItem = new WF.ToolStripMenuItem("종료");
        exitItem.Click += (_, _) => exit();

        menu.Items.Add(editItem);
        menu.Items.Add(rulesItem);
        menu.Items.Add(new WF.ToolStripSeparator());
        menu.Items.Add(dragSnapItem);
        menu.Items.Add(ctrlSnapItem);
        menu.Items.Add(fakeMaxItem);
        menu.Items.Add(fullscreenItem);
        menu.Items.Add(workAreaItem);
        menu.Items.Add(new WF.ToolStripSeparator());
        menu.Items.Add(startupItem);
        menu.Items.Add(elevateItem);
        menu.Items.Add(reloadItem);
        menu.Items.Add(exitItem);

        _icon = new WF.NotifyIcon
        {
            Icon = LoadIcon(),
            Text = StartupService.IsElevated ? "Display Divider (관리자)" : "Display Divider",
            ContextMenuStrip = menu,
            Visible = true,
        };
        _icon.DoubleClick += (_, _) => openEditor();
    }

    /// <summary>트레이 크기(DPI 반영)에 맞는 프레임을 골라 로드 — 32px 축소보다 선명.</summary>
    static System.Drawing.Icon LoadIcon()
    {
        var res = System.Windows.Application.GetResourceStream(new Uri("pack://application:,,,/Assets/app.ico"));
        using var stream = res.Stream;
        return new System.Drawing.Icon(stream, WF.SystemInformation.SmallIconSize);
    }

    public void Dispose()
    {
        _icon.Visible = false;
        _icon.Dispose();
    }
}
