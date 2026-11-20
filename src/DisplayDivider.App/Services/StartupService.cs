using System.Diagnostics;
using System.IO;
using System.Security;
using System.Security.Principal;
using Microsoft.Win32;

namespace DisplayDivider.App.Services;

/// <summary>
/// Windows 로그온 시 자동 실행.
/// 일반 권한: HKCU Run 키. 관리자 권한: 작업 스케줄러(가장 높은 권한) — Run 키는 권한 상승이 안 되므로
/// 관리자 권한 창(UIPI 차단 대상)까지 다루려면 작업 스케줄러가 필요하다.
/// </summary>
public static class StartupService
{
    const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    const string ValueName = "DisplayDivider";
    const string TaskName = "DisplayDivider";

    public static bool IsElevated => Environment.IsPrivilegedProcess;

    public static bool IsEnabled() => IsRunKeySet() || TaskExists();

    /// <summary>등록/해제. 성공하면 true.</summary>
    public static bool SetEnabled(bool enabled)
    {
        try
        {
            if (!enabled)
            {
                SetRunKey(null);
                return !TaskExists() || Schtasks($"/Delete /TN {TaskName} /F");
            }

            var exePath = Environment.ProcessPath;
            if (string.IsNullOrEmpty(exePath)) return false;

            if (!IsElevated)
            {
                SetRunKey($"\"{exePath}\"");
                return true;
            }

            if (!CreateTask(exePath)) return false;
            SetRunKey(null); // 이중 실행 방지
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>관리자 권한으로 새 인스턴스를 띄운다. UAC 거부 시 false.</summary>
    public static bool StartElevated()
    {
        try
        {
            Process.Start(new ProcessStartInfo(Environment.ProcessPath!)
            {
                UseShellExecute = true,
                Verb = "runas",
                Arguments = $"--after {Environment.ProcessId}",
            });
            return true;
        }
        catch
        {
            return false; // UAC 취소 (Win32Exception 1223) 등
        }
    }

    static bool IsRunKeySet()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath);
            return key?.GetValue(ValueName) is string;
        }
        catch
        {
            return false;
        }
    }

    static void SetRunKey(string? value)
    {
        using var key = Registry.CurrentUser.CreateSubKey(RunKeyPath);
        if (value is null) key.DeleteValue(ValueName, throwOnMissingValue: false);
        else key.SetValue(ValueName, value);
    }

    static bool TaskExists() => Schtasks($"/Query /TN {TaskName}");

    static bool CreateTask(string exePath)
    {
        var user = SecurityElement.Escape(WindowsIdentity.GetCurrent().Name);
        // schtasks /Create 기본값은 72시간 실행 제한·배터리 시 중지·낮은 우선순위(7)라 XML로 직접 지정.
        var xml = $"""
            <?xml version="1.0" encoding="UTF-16"?>
            <Task version="1.2" xmlns="http://schemas.microsoft.com/windows/2004/02/mit/task">
              <Triggers><LogonTrigger><Enabled>true</Enabled><UserId>{user}</UserId></LogonTrigger></Triggers>
              <Principals><Principal id="Author"><UserId>{user}</UserId><LogonType>InteractiveToken</LogonType><RunLevel>HighestAvailable</RunLevel></Principal></Principals>
              <Settings>
                <MultipleInstancesPolicy>IgnoreNew</MultipleInstancesPolicy>
                <DisallowStartIfOnBatteries>false</DisallowStartIfOnBatteries>
                <StopIfGoingOnBatteries>false</StopIfGoingOnBatteries>
                <ExecutionTimeLimit>PT0S</ExecutionTimeLimit>
                <Priority>4</Priority>
              </Settings>
              <Actions Context="Author"><Exec><Command>{SecurityElement.Escape(exePath)}</Command></Exec></Actions>
            </Task>
            """;
        var file = Path.Combine(Path.GetTempPath(), "DisplayDivider.task.xml");
        File.WriteAllText(file, xml, System.Text.Encoding.Unicode);
        try { return Schtasks($"/Create /TN {TaskName} /XML \"{file}\" /F"); }
        finally { File.Delete(file); }
    }

    static bool Schtasks(string args)
    {
        using var p = Process.Start(new ProcessStartInfo("schtasks.exe", args)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        })!;
        p.StandardOutput.ReadToEnd();
        p.StandardError.ReadToEnd();
        p.WaitForExit();
        return p.ExitCode == 0;
    }
}
