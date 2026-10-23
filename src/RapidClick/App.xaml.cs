using System.IO;
using System.Windows;

namespace RapidClick;

public partial class App : Application
{
    private Mutex? instance;
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        instance = new Mutex(true, "Local\\RapidClick.Desktop", out bool first);
        if (!first) { MessageBox.Show("RapidClick is already running. Look for its window in the taskbar.", "RapidClick"); Shutdown(); return; }
        DispatcherUnhandledException += (_, error) =>
        {
            if (MainWindow is MainWindow window) window.EmergencyStop();
            if (e.Args.Contains("--smoke-test"))
            {
                Directory.CreateDirectory("test-results");
                File.WriteAllText("test-results/ui-error.txt", error.Exception.ToString());
                error.Handled = true; Shutdown(1); return;
            }
            MessageBox.Show(error.Exception.Message, "RapidClick stopped", MessageBoxButton.OK, MessageBoxImage.Error);
            error.Handled = true;
            Shutdown(1);
        };
        MainWindow = new MainWindow(e.Args);
        MainWindow.Show();
    }
    protected override void OnExit(ExitEventArgs e) { instance?.Dispose(); base.OnExit(e); }
}
