using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;
using RapidClick.Core;
using MouseButton = RapidClick.Core.MouseButton;

internal static class Program
{
    private static readonly List<object> Results = [];
    private static int failures;

    [STAThread]
    private static int Main(string[] args)
    {
        Run("reject invalid settings", () =>
        {
            foreach (var option in new[] { new ClickOptions { Rate = 0 }, new ClickOptions { Rate = 10001 }, new ClickOptions { DelaySeconds = double.NaN }, new ClickOptions { RepeatLimit = -1 }, new ClickOptions { Hotkey = 0x77 } })
            {
                bool rejected = false;
                try { option.Validate(); } catch (ArgumentException) { rejected = true; }
                Check(rejected, "Invalid settings were accepted.");
            }
        });
        Run("exact finite count and restart", () =>
        {
            var sink = new CountingSink();
            using var engine = new ClickEngine(_ => sink, _ => false);
            Check(engine.Start(new() { Rate = 10000, RepeatLimit = 137, DelaySeconds = 0 }), "Did not start.");
            Wait(engine);
            Check(sink.Count == 137 && engine.Snapshot.Clicks == 137 && engine.Snapshot.State == RunState.Completed, "Incorrect finite count.");
            Check(engine.Start(new() { Rate = 10000, RepeatLimit = 11, DoubleClick = true, DelaySeconds = 0 }), "Did not restart.");
            Wait(engine);
            Check(engine.Snapshot.Clicks == 22 && sink.Count == 148, "Incorrect double-click count.");
        });
        Run("cancel countdown without clicks", () =>
        {
            var sink = new CountingSink();
            using var engine = new ClickEngine(_ => sink, _ => false);
            engine.Start(new() { DelaySeconds = 3 });
            Thread.Sleep(40);
            var elapsed = Stopwatch.StartNew();
            engine.Stop(); Wait(engine);
            Check(sink.Count == 0 && elapsed.ElapsedMilliseconds < 250, "Cancellation was slow or emitted clicks.");
        });
        Run("physical emergency stop and hold release", () =>
        {
            int stop = 0;
            using var engine = new ClickEngine(_ => new CountingSink(), key => key == 0x77 && Volatile.Read(ref stop) != 0);
            engine.Start(new() { Rate = 1, DelaySeconds = 0 });
            Thread.Sleep(30);
            var elapsed = Stopwatch.StartNew();
            Volatile.Write(ref stop, 1); Wait(engine);
            Check(elapsed.ElapsedMilliseconds < 250, "Emergency stop waited for the 1-second interval.");
            using var hold = new ClickEngine(_ => new CountingSink(), _ => false);
            hold.Start(new() { DelaySeconds = 0 }, true); Wait(hold);
            Check(hold.Snapshot.Clicks == 0, "Hold mode clicked with the key released.");
        });
        Run("overlapping starts and injection failure", () =>
        {
            using var engine = new ClickEngine(_ => new CountingSink(), _ => false);
            engine.Start(new() { DelaySeconds = 3 });
            Check(!engine.Start(new()), "Allowed two workers.");
            engine.Stop(); Wait(engine);
            using var broken = new ClickEngine(_ => new BrokenSink(), _ => false);
            broken.Start(new() { DelaySeconds = 0 }); Wait(broken);
            Check(broken.Snapshot.State == RunState.Error && broken.Snapshot.Clicks == 0, "Failed injection was counted as success.");
        });
        if (args.Contains("--native")) NativeTests();
        var report = new { timestampUtc = DateTime.UtcNow, os = Environment.OSVersion.ToString(), runtime = Environment.Version.ToString(), failures, results = Results };
        string json = JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true });
        Directory.CreateDirectory("test-results");
        File.WriteAllText(args.Contains("--native") ? "test-results/verification-native.json" : "test-results/verification.json", json);
        Console.WriteLine(json);
        return failures == 0 ? 0 : 1;
    }

    private static void NativeTests()
    {
        Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
        Application.EnableVisualStyles();
        Native.GetCursorPos(out var oldCursor);
        using var window = new TargetWindow();
        try
        {
            window.Show(); window.Activate();
            Pump(300);
            foreach (int rate in new[] { 100, 1000, 2500, 5000, 10000 })
            {
                Run($"native delivery at {rate}/s", () =>
                {
                    window.Count = 0;
                    window.UpCount = 0;
                    var point = window.PointToScreen(new Point(window.ClientSize.Width / 2, window.ClientSize.Height / 2));
                    Console.WriteLine($"Target {window.Bounds}, DPI {window.DeviceDpi}, point {point}, virtual desktop {SystemInformation.VirtualScreen}");
                    FocusTarget(window, point);
                    Native.GetCursorPos(out var actualCursor);
                    Console.WriteLine($"Actual pointer {actualCursor.X},{actualCursor.Y}; point window {WindowFromPoint(actualCursor):X}; expected {window.Handle:X}");
                    using var engine = new ClickEngine(options => new GuardedSink(options, window.Handle));
                    int expected = Math.Min(rate * 2, 3000);
                    var process = Process.GetCurrentProcess();
                    TimeSpan cpu = process.TotalProcessorTime;
                    engine.Start(new() { Rate = rate, RepeatLimit = expected, DelaySeconds = 0.1, FixedPosition = false, X = point.X, Y = point.Y });
                    Wait(engine, true);
                    Pump(250);
                    var snapshot = engine.Snapshot;
                    double cpuMs = (process.TotalProcessorTime - cpu).TotalMilliseconds;
                    Results.Add(new { benchmark = true, requestedRate = rate, emitted = snapshot.Clicks, received = window.Count, elapsedSeconds = snapshot.ElapsedSeconds, achievedRate = Math.Round(snapshot.AverageRate, 1), cpuMilliseconds = Math.Round(cpuMs, 1) });
                    Check(snapshot.State == RunState.Completed, snapshot.Message);
                    Check(window.Count == expected && snapshot.Clicks == expected, $"Expected {expected}, received {window.Count}.");
                    Check(window.UpCount == expected, $"Expected {expected} releases, received {window.UpCount}.");
                    if (rate <= 1000) Check(snapshot.AverageRate >= rate * 0.85, "Throughput below 85% of baseline speed.");
                });
            }
            foreach (var button in new[] { MouseButton.Right, MouseButton.Middle, MouseButton.Left })
            {
                Run($"native {button} double-click pairs", () =>
                {
                    window.Count = 0;
                    window.UpCount = 0;
                    var p = window.PointToScreen(new Point(180, 100));
                    FocusTarget(window, p);
                    using var engine = new ClickEngine(options => new GuardedSink(options, window.Handle));
                    engine.Start(new() { Rate = 1000, RepeatLimit = 37, DoubleClick = true, Button = button, DelaySeconds = 0, FixedPosition = true, X = p.X, Y = p.Y });
                    Wait(engine, true); Pump(100);
                    Check(window.Count == 74 && engine.Snapshot.Clicks == 74, $"Received {window.Count} of 74 clicks.");
                    Check(window.UpCount == 74, $"Received {window.UpCount} of 74 releases.");
                });
            }
            Run("native stop while running at maximum requested rate", () =>
            {
                var p = window.PointToScreen(new Point(180, 100));
                FocusTarget(window, p);
                using var engine = new ClickEngine(options => new GuardedSink(options, window.Handle));
                engine.Start(new() { Rate = 10000, DelaySeconds = 0, FixedPosition = true, X = p.X, Y = p.Y });
                Pump(150);
                var stop = Stopwatch.StartNew(); engine.Stop(); Wait(engine, true);
                double stopMs = stop.Elapsed.TotalMilliseconds;
                Pump(250); int settled = window.Count; Pump(100);
                Results.Add(new { nativeStopMilliseconds = Math.Round(stopMs, 2), noClicksAfterDrain = window.Count == settled });
                Check(stopMs < 250 && window.Count == settled, "Stop was delayed or clicks continued after the queue drained.");
            });
        }
        finally { window.Close(); SetCursorPos(oldCursor.X, oldCursor.Y); }
    }

    private static void Wait(ClickEngine engine, bool pump = false)
    {
        var timeout = Stopwatch.StartNew();
        while (engine.IsActive && timeout.Elapsed.TotalSeconds < 30)
        {
            if (pump) { Application.DoEvents(); Thread.Yield(); }
            else Thread.Sleep(1);
        }
        Check(!engine.IsActive, $"Worker timed out. Snapshot: {JsonSerializer.Serialize(engine.Snapshot)}");
    }
    private static void Pump(int milliseconds)
    {
        var timer = Stopwatch.StartNew();
        while (timer.ElapsedMilliseconds < milliseconds) { Application.DoEvents(); Thread.Sleep(1); }
    }
    private static void FocusTarget(Form window, Point point)
    {
        var wait = Stopwatch.StartNew();
        do
        {
            window.Activate(); SetCursorPos(point.X, point.Y); Pump(150);
            Native.GetCursorPos(out var cursor);
            if (GetForegroundWindow() == window.Handle && WindowFromPoint(cursor) == window.Handle) return;
        } while (wait.Elapsed.TotalSeconds < 5);
        throw new InvalidOperationException("Could not safely focus the native test target. Leave the mouse and keyboard idle during native verification.");
    }
    private static void Run(string name, Action test)
    {
        try { test(); Results.Add(new { test = name, passed = true }); Console.WriteLine($"PASS {name}"); }
        catch (Exception error) { failures++; Results.Add(new { test = name, passed = false, error = error.Message }); Console.WriteLine($"FAIL {name}: {error.Message}"); }
    }
    private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    private sealed class CountingSink : IClickSink { public long Count; public void Send(int repeats) => Count += repeats; public void Dispose() { } }
    private sealed class BrokenSink : IClickSink { public void Send(int repeats) => throw new InvalidOperationException("Test injection failure"); public void Dispose() { } }
    private sealed class GuardedSink(ClickOptions options, nint window) : IClickSink
    {
        private readonly NativeClickSink sink = new(options);
        public void Send(int repeats)
        {
            if (GetForegroundWindow() != window) throw new InvalidOperationException($"Test target lost focus ({GetForegroundWindow():X} instead of {window:X}); refusing to inject outside the test window.");
            sink.Send(repeats);
        }
        public void Dispose() => sink.Dispose();
    }
    private sealed class TargetWindow : Form
    {
        public int Count;
        public int UpCount;
        public TargetWindow()
        {
            Text = "RapidClick delivery verification"; Width = 540; Height = 280;
            StartPosition = FormStartPosition.CenterScreen; BackColor = Color.FromArgb(244, 240, 252); TopMost = true;
            Controls.Add(new Label { Text = "Testing real Windows mouse input\nThis window closes automatically.", Dock = DockStyle.Top, Height = 64, TextAlign = ContentAlignment.MiddleCenter });
        }
        protected override void WndProc(ref Message message)
        {
            if (message.Msg is >= 0x200 and <= 0x209)
            {
                if (message.Msg is 0x201 or 0x203 or 0x204 or 0x206 or 0x207 or 0x209 && (nuint)GetMessageExtraInfo() == NativeClickSink.InputSignature) Count++;
                if (message.Msg is 0x202 or 0x205 or 0x208 && (nuint)GetMessageExtraInfo() == NativeClickSink.InputSignature) UpCount++;
                message.Result = 0;
                return; // raw receiver: exclude framework mouse capture and double-click rewriting
            }
            base.WndProc(ref message);
        }
    }
    [DllImport("user32.dll")] private static extern bool SetCursorPos(int x, int y);
    [DllImport("user32.dll")] private static extern nint GetForegroundWindow();
    [DllImport("user32.dll")] private static extern nint GetMessageExtraInfo();
    [DllImport("user32.dll")] private static extern nint WindowFromPoint(Native.Point point);
}
