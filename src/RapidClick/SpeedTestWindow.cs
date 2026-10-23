using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using RapidClick.Core;

namespace RapidClick;

public sealed class SpeedTestWindow : Window
{
    private readonly ClickEngine engine;
    private readonly int rate;
    private readonly TextBlock result;
    private readonly Border pad;
    private readonly Button start;
    private readonly DispatcherTimer timer = new() { Interval = TimeSpan.FromMilliseconds(50) };
    private HwndSource? source;
    private nint targetHandle;
    private long received;
    private bool running;
    private DateTime stoppedAt;

    public SpeedTestWindow(int rate)
    {
        this.rate = rate;
        Style = (Style)Application.Current.FindResource(typeof(Window));
        Title = "RapidClick · speed test"; Width = 540; Height = 420; ResizeMode = ResizeMode.NoResize; WindowStartupLocation = WindowStartupLocation.CenterOwner; Topmost = true;
        engine = new ClickEngine(options => new GuardedSink(options, targetHandle));
        var panel = new StackPanel { Margin = new Thickness(24) };
        panel.Children.Add(new TextBlock { Text = "See what actually arrives", FontSize = 24, FontWeight = FontWeights.SemiBold });
        panel.Children.Add(new TextBlock { Text = $"A two-second test at {rate:N0} repeats/sec.\nKeep this window in front. F8 stops the test.", Margin = new Thickness(0, 10, 0, 16), TextWrapping = TextWrapping.Wrap, Foreground = (Brush)Application.Current.FindResource("MutedBrush") });
        pad = new Border { Height = 130, Background = (Brush)Application.Current.FindResource("FieldBrush"), CornerRadius = new CornerRadius(16), Child = new TextBlock { Text = "Clicks land here", HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, Foreground = (Brush)Application.Current.FindResource("AccentBrush") } };
        panel.Children.Add(pad);
        result = new TextBlock { Text = "Ready to measure.", Margin = new Thickness(0, 14, 0, 14), TextWrapping = TextWrapping.Wrap };
        panel.Children.Add(result);
        start = new Button { Content = "Run speed test", Style = (Style)Application.Current.FindResource("PrimaryButton") };
        start.Click += (_, _) => Begin(); panel.Children.Add(start); Content = panel;
        SourceInitialized += (_, _) => { targetHandle = new WindowInteropHelper(this).Handle; source = HwndSource.FromHwnd(targetHandle); source.AddHook(CountInput); };
        Deactivated += (_, _) => engine.Stop();
        Closed += (_, _) => { timer.Stop(); engine.Dispose(); source?.RemoveHook(CountInput); };
        timer.Tick += (_, _) => Refresh(); timer.Start();
    }

    private nint CountInput(nint hwnd, int message, nint wParam, nint lParam, ref bool handled)
    {
        if (running && message is 0x201 or 0x203 && (nuint)GetMessageExtraInfo() == NativeClickSink.InputSignature) received++;
        return 0;
    }
    [System.Runtime.InteropServices.DllImport("user32.dll")] private static extern nint GetMessageExtraInfo();
    private void Begin()
    {
        received = 0; running = true; stoppedAt = default; start.IsEnabled = false;
        Point p = pad.PointToScreen(new Point(pad.ActualWidth / 2, pad.ActualHeight / 2));
        engine.Start(new() { Rate = rate, DelaySeconds = 0.4, RepeatLimit = rate * 2, FixedPosition = true, X = (int)p.X, Y = (int)p.Y });
    }
    public async Task VerifyAsync()
    {
        await Task.Delay(150);
        for (int i = 0; i < 20 && GetForegroundWindow() != targetHandle; i++) { Activate(); await Task.Delay(100); }
        Begin();
        var deadline = Stopwatch.StartNew();
        while (engine.IsActive && deadline.Elapsed.TotalSeconds < 15) await Task.Delay(15);
        await Task.Delay(250);
        if (engine.IsActive || engine.Snapshot.State != RunState.Completed || received != rate * 2)
            throw new InvalidOperationException($"Built-in speed test failed: {engine.Snapshot.State}, {received} received, {engine.Snapshot.Message}");
    }
    [System.Runtime.InteropServices.DllImport("user32.dll")] private static extern nint GetForegroundWindow();
    private void Refresh()
    {
        if (!running) return;
        var snap = engine.Snapshot;
        if (!engine.IsActive)
        {
            if (stoppedAt == default) stoppedAt = DateTime.UtcNow;
            if ((DateTime.UtcNow - stoppedAt).TotalMilliseconds < 150) return;
            running = false; start.IsEnabled = true;
            result.Text = snap.State == RunState.Completed
                ? $"{received:N0} received / {snap.Clicks:N0} sent · {snap.AverageRate:N0} repeats/sec\nThis result applies to this test window; other apps may differ."
                : $"Test stopped. {received:N0} clicks received. {snap.Message}";
        }
        else result.Text = snap.State == RunState.Waiting ? "Preparing test…" : $"{received:N0} clicks received…";
    }
    private sealed class GuardedSink(ClickOptions options, nint target) : IClickSink
    {
        private readonly NativeClickSink sink = new(options);
        public void Send(int repeats)
        {
            if (GetForegroundWindow() != target) throw new OperationCanceledException();
            sink.Send(repeats);
        }
        public void Dispose() => sink.Dispose();
        [System.Runtime.InteropServices.DllImport("user32.dll")] private static extern nint GetForegroundWindow();
    }
}
