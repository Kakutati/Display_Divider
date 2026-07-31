namespace DisplayDivider.Core;

/// <summary>앱 규칙 이름 정규화와 매칭.</summary>
public static class AppRules
{
    /// <summary>프로세스 이름 정규화: 공백 제거, ".exe" 제거, 소문자.</summary>
    public static string Normalize(string processName)
    {
        var name = processName.Trim();
        if (name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
            name = name[..^4];
        return name.ToLowerInvariant();
    }

    /// <summary>프로세스 이름과 일치하는 활성 규칙. 없으면 null.</summary>
    public static AppRule? Find(IEnumerable<AppRule> rules, string processName)
    {
        var normalized = Normalize(processName);
        return rules.FirstOrDefault(r => r.Enabled && Normalize(r.ProcessName) == normalized);
    }
}
