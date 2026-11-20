using System.Windows;

namespace DisplayDivider.App;

public partial class App : Application
{
    AppCoordinator? _coordinator;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // "관리자 권한으로 재시작": 이전 인스턴스가 핫키/훅을 놓을 때까지 대기
        if (e.Args is ["--after", var pidArg] && int.TryParse(pidArg, out var pid))
        {
            try { System.Diagnostics.Process.GetProcessById(pid).WaitForExit(5000); }
            catch (ArgumentException) { } // 이미 종료됨
        }

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
