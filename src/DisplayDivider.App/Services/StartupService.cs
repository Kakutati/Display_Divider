using Microsoft.Win32;

namespace DisplayDivider.App.Services;

/// <summary>Windows 부팅 시 자동 실행 (HKCU Run 키). 관리자 권한 불필요.</summary>
public static class StartupService
{
    const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    const string ValueName = "DisplayDivider";

    public static bool IsEnabled()
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

    /// <summary>등록/해제. 성공하면 true.</summary>
    public static bool SetEnabled(bool enabled)
    {
        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(RunKeyPath);
            if (key is null) return false;

            if (enabled)
            {
                var exePath = Environment.ProcessPath;
                if (string.IsNullOrEmpty(exePath)) return false;
                key.SetValue(ValueName, $"\"{exePath}\"");
            }
            else
            {
                key.DeleteValue(ValueName, throwOnMissingValue: false);
            }
            return true;
        }
        catch
        {
            return false;
        }
    }
}
