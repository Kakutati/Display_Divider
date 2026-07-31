using DisplayDivider.Core;
using Xunit;

namespace DisplayDivider.Core.Tests;

public class AppRulesTests
{
    [Theory]
    [InlineData("MyGame.exe", "mygame")]
    [InlineData("  Notepad  ", "notepad")]
    [InlineData("game.EXE", "game")]
    [InlineData("already-normal", "already-normal")]
    public void Normalize_StripsExeAndLowercases(string input, string expected)
    {
        Assert.Equal(expected, AppRules.Normalize(input));
    }

    [Fact]
    public void Find_MatchesCaseInsensitiveAndIgnoresExe()
    {
        var rules = new List<AppRule>
        {
            new() { ProcessName = "MyGame.exe", Enabled = true, MonitorDevice = "D1", ZoneIndex = 1 },
        };

        var found = AppRules.Find(rules, "mygame");

        Assert.NotNull(found);
        Assert.Equal(1, found.ZoneIndex);
    }

    [Fact]
    public void Find_DisabledRule_IsIgnored()
    {
        var rules = new List<AppRule>
        {
            new() { ProcessName = "mygame", Enabled = false },
        };

        Assert.Null(AppRules.Find(rules, "mygame"));
    }

    [Fact]
    public void Find_NoMatch_ReturnsNull()
    {
        var rules = new List<AppRule>
        {
            new() { ProcessName = "other", Enabled = true },
        };

        Assert.Null(AppRules.Find(rules, "mygame"));
    }
}
