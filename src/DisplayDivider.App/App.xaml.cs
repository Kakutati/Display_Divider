using System.Windows;

namespace DisplayDivider.App;

public partial class App : Application
{
    AppCoordinator? _coordinator;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        DispatcherUnhandledException += (_, args) =>
        {
            MessageBox.Show(args.Exception.Message, "Display Divider 오류",
                MessageBoxButton.OK, MessageBoxImage.Error);
            args.Handled = true;
        };

        _coordinator = new AppCoordinator();
        _coordinator.Start();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _coordinator?.Dispose();
        base.OnExit(e);
    }
}
