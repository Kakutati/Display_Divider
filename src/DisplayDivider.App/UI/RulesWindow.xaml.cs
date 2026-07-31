using System.Windows;
using System.Windows.Controls;
using DisplayDivider.App.Services;
using DisplayDivider.Core;
using DisplayDivider.Win32;

namespace DisplayDivider.App.UI;

public partial class RulesWindow : Window
{
    readonly ZoneService _zones;

    public RulesWindow(ZoneService zones)
    {
        _zones = zones;
        InitializeComponent();
        LoadProcesses();
        LoadMonitors();
        RefreshRulesList();
    }

    MonitorInfo? SelectedMonitor =>
        (MonitorCombo.SelectedItem as ComboBoxItem)?.Tag as MonitorInfo;

    void LoadProcesses()
    {
        var current = ProcessCombo.Text;
        ProcessCombo.Items.Clear();
        foreach (var name in WindowProbe.GetTopLevelProcessNames())
            ProcessCombo.Items.Add(name);
        ProcessCombo.Text = current;
    }

    void LoadMonitors()
    {
        _zones.RefreshMonitors();
        MonitorCombo.Items.Clear();
        for (int i = 0; i < _zones.Monitors.Count; i++)
        {
            var mon = _zones.Monitors[i];
            MonitorCombo.Items.Add(new ComboBoxItem
            {
                Content = $"{i + 1}. {mon.DeviceName}",
                Tag = mon,
            });
        }
        if (MonitorCombo.Items.Count > 0)
            MonitorCombo.SelectedIndex = 0;
    }

    void MonitorCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ZoneCombo is null) return;
        ZoneCombo.Items.Clear();
        if (SelectedMonitor is not { } mon) return;

        int count = _zones.FractionZonesFor(mon.DeviceName).Count;
        for (int i = 0; i < count; i++)
            ZoneCombo.Items.Add($"존 {i + 1}");
        if (count > 0)
            ZoneCombo.SelectedIndex = 0;
        else
            StatusText.Text = "이 모니터에는 존이 없습니다 — 존 편집에서 먼저 분할하세요";
    }

    void RefreshProcesses_Click(object sender, RoutedEventArgs e) => LoadProcesses();

    void Add_Click(object sender, RoutedEventArgs e)
    {
        var name = AppRules.Normalize(ProcessCombo.Text);
        if (string.IsNullOrEmpty(name))
        {
            StatusText.Text = "프로세스 이름을 입력하거나 목록에서 선택하세요";
            return;
        }
        if (SelectedMonitor is not { } mon || ZoneCombo.SelectedIndex < 0)
        {
            StatusText.Text = "대상 모니터와 존을 선택하세요";
            return;
        }
        if (_zones.Config.Rules.Any(r => AppRules.Normalize(r.ProcessName) == name))
        {
            StatusText.Text = $"'{name}' 규칙이 이미 있습니다";
            return;
        }

        _zones.Config.Rules.Add(new AppRule
        {
            ProcessName = name,
            Enabled = true,
            MonitorDevice = mon.DeviceName,
            ZoneIndex = ZoneCombo.SelectedIndex,
        });
        _zones.Save();
        RefreshRulesList();
        StatusText.Text = $"'{name}' 규칙 추가됨";
    }

    void Toggle_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedRule() is not { } rule) return;
        rule.Enabled = !rule.Enabled;
        _zones.Save();
        RefreshRulesList();
    }

    void Delete_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedRule() is not { } rule) return;
        _zones.Config.Rules.Remove(rule);
        _zones.Save();
        RefreshRulesList();
        StatusText.Text = "규칙 삭제됨";
    }

    AppRule? SelectedRule()
    {
        int index = RulesList.SelectedIndex;
        if (index < 0 || index >= _zones.Config.Rules.Count)
        {
            StatusText.Text = "목록에서 규칙을 먼저 선택하세요";
            return null;
        }
        return _zones.Config.Rules[index];
    }

    void RefreshRulesList()
    {
        int selected = RulesList.SelectedIndex;
        RulesList.Items.Clear();
        foreach (var rule in _zones.Config.Rules)
            RulesList.Items.Add(
                $"{(rule.Enabled ? "[사용]" : "[해제]")} {rule.ProcessName}  →  {rule.MonitorDevice} 존 {rule.ZoneIndex + 1}");
        if (selected >= 0 && selected < RulesList.Items.Count)
            RulesList.SelectedIndex = selected;
    }

    void CloseBtn_Click(object sender, RoutedEventArgs e) => Close();
}
