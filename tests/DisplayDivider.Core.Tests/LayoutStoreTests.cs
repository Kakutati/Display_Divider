using DisplayDivider.Core;
using Xunit;

namespace DisplayDivider.Core.Tests;

public class LayoutStoreTests : IDisposable
{
    readonly string _path = Path.Combine(Path.GetTempPath(), $"dd-test-{Guid.NewGuid():N}.json");

    [Fact]
    public void SaveAndLoad_RoundTripsConfig()
    {
        var config = new LayoutConfig
        {
            DragSnapEnabled = false,
            SnapRequiresCtrl = false,
            Monitors =
            {
                new MonitorLayout
                {
                    DeviceName = @"\\.\DISPLAY1",
                    Zones = LayoutEngine.Columns(70, 30),
                },
            },
        };

        LayoutStore.Save(config, _path);
        var loaded = LayoutStore.Load(_path);

        Assert.False(loaded.DragSnapEnabled);
        Assert.False(loaded.SnapRequiresCtrl);
        var monitor = Assert.Single(loaded.Monitors);
        Assert.Equal(@"\\.\DISPLAY1", monitor.DeviceName);
        Assert.Equal(2, monitor.Zones.Count);
        Assert.Equal(0.7, monitor.Zones[0].Width, 10);
    }

    [Fact]
    public void Load_MissingFile_ReturnsDefaults()
    {
        var loaded = LayoutStore.Load(_path);

        Assert.True(loaded.DragSnapEnabled);
        Assert.True(loaded.FakeMaximizeEnabled);
        Assert.Empty(loaded.Monitors);
    }

    [Fact]
    public void Load_CorruptFile_ReturnsDefaults()
    {
        File.WriteAllText(_path, "{ not valid json !!");

        var loaded = LayoutStore.Load(_path);

        Assert.Empty(loaded.Monitors);
    }

    public void Dispose()
    {
        if (File.Exists(_path)) File.Delete(_path);
    }
}
