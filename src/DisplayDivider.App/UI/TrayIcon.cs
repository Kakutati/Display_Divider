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
        menu.Items.Add(reloadItem);
        menu.Items.Add(exitItem);

        _icon = new WF.NotifyIcon
        {
            Icon = System.Drawing.SystemIcons.Application,
            Text = "Display Divider",
            ContextMenuStrip = menu,
            Visible = true,
        };
        _icon.DoubleClick += (_, _) => openEditor();
    }

    public void Dispose()
    {
        _icon.Visible = false;
        _icon.Dispose();
    }
}
