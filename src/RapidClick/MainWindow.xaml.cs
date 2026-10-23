using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using RapidClick.Core;

namespace RapidClick;

public partial class MainWindow : Window
{
    private readonly ClickEngine engine = new();
    private readonly DispatcherTimer timer = new() { Interval = TimeSpan.FromMilliseconds(30) };
    private readonly int[] hotkeys = [0x70, 0x71, 0x72, 0x73, 0x74, 0x75, 0x78, 0x79, 0x7A];
    private readonly string[] arguments;
    private readonly bool smoke;
    private nint handle;
    private int currentHotkey = 0x75;
    private int positionX, positionY;
    private bool initialized, hotkeysReady, heldPreviously;
    private ClickSnapshot? lastSnapshot;
    private HwndSource? source;

    public MainWindow(string[] arguments)
    {
        this.arguments = arguments;
        smoke = arguments.Contains("--smoke-test");
        InitializeComponent();
        var saved = smoke ? new ClickOptions() : SettingsStore.Load();
        RateInput.Text = saved.Rate.ToString(CultureInfo.InvariantCulture);
        ButtonInput.SelectedIndex = (int)saved.Button;
        TypeInput.SelectedIndex = saved.DoubleClick ? 1 : 0;
        LimitInput.Text = saved.RepeatLimit.ToString(CultureInfo.InvariantCulture);
        DelayInput.Text = saved.DelaySeconds.ToString(CultureInfo.CurrentCulture);
        ModeInput.SelectedIndex = (int)saved.Activation;
        foreach (int key in hotkeys) HotkeyInput.Items.Add($"F{key - 0x6F}");
        currentHotkey = saved.Hotkey;
        HotkeyInput.SelectedIndex = Array.IndexOf(hotkeys, saved.Hotkey);
        positionX = saved.X; positionY = saved.Y;
        FixedInput.IsChecked = saved.FixedPosition;
        initialized = true;
        RefreshPosition(); RefreshRate(); RefreshButton();
        SourceInitialized += (_, _) => InitializeNative();
        PreviewKeyDown += (_, e) => { if (e.Key == Key.Escape) { EmergencyStop(); e.Handled = true; } };
        timer.Tick += (_, _) => Tick();
        Loaded += async (_, _) =>
        {
            timer.Start();
            if (smoke) await SmokeTest();
        };
        Closed += (_, _) =>
        {
            timer.Stop(); engine.Dispose();
            for (int id = 1; id <= 3; id++) Native.UnregisterHotKey(handle, id);
            source?.RemoveHook(WindowMessage);
            if (!smoke)
            {
                try { SettingsStore.Save(ReadOptions()); }
                catch (Exception ex) when (ex is ArgumentException or IOException or UnauthorizedAccessException) { /* Invalid edits never overwrite the last valid settings. */ }
            }
        };
    }

    private void InitializeNative()
    {
        handle = new WindowInteropHelper(this).Handle;
        source = HwndSource.FromHwnd(handle);
        source.AddHook(WindowMessage);
        int dark = 1; DwmSetWindowAttribute(handle, 20, ref dark, sizeof(int));
        bool stopRegistered = Native.RegisterHotKey(handle, 2, 0x4000, 0x77);
        bool startRegistered = Native.RegisterHotKey(handle, 1, 0x4000, (uint)currentHotkey);
        Native.RegisterHotKey(handle, 3, 0x4000, 0x76);
        hotkeysReady = stopRegistered && startRegistered;
        if (!hotkeysReady) SetMessage("A hotkey is in use by another app. Choose a different start key, or close the app using F8, then reopen RapidClick.", true);
        RefreshButton();
    }

    private nint WindowMessage(nint hwnd, int message, nint wParam, nint lParam, ref bool handled)
    {
        if (message == 0x0312)
        {
            handled = true;
            switch ((int)wParam)
            {
                case 1 when ModeInput.SelectedIndex == 0: Toggle(); break;
                case 2: EmergencyStop(); break;
                case 3 when !engine.IsActive: CapturePosition(); break;
            }
        }
        return 0;
    }

    private void Tick()
    {
        bool held = Native.IsKeyDown(currentHotkey);
        if (hotkeysReady && ModeInput.SelectedIndex == 1 && held && !heldPreviously && !engine.IsActive) Start(true);
        heldPreviously = held;
        var snapshot = engine.Snapshot;
        if (!ReferenceEquals(snapshot, lastSnapshot))
        {
            lastSnapshot = snapshot;
            StateLabel.Text = snapshot.State.ToString();
            CountLabel.Text = snapshot.Clicks.ToString("N0");
            SpeedLabel.Text = snapshot.ElapsedSeconds > 0 ? snapshot.AverageRate.ToString("N0") : "—";
            TimeLabel.Text = $"{snapshot.ElapsedSeconds:0.0}s";
            if (snapshot.State != RunState.Ready) SetMessage(snapshot.Message, snapshot.State == RunState.Error);
        }
        bool active = engine.IsActive;
        SpeedSettings.IsEnabled = BehaviorSettings.IsEnabled = TestButton.IsEnabled = !active;
        RefreshButton();
    }

    private ClickOptions ReadOptions()
    {
        if (!int.TryParse(RateInput.Text, NumberStyles.Integer, CultureInfo.CurrentCulture, out int rate)) throw new ArgumentException("Enter a whole-number speed from 1 to 10,000.");
        if (!long.TryParse(LimitInput.Text, NumberStyles.Integer, CultureInfo.CurrentCulture, out long limit)) throw new ArgumentException("Enter a whole-number repeat count, or 0 for unlimited.");
        if (!double.TryParse(DelayInput.Text, NumberStyles.Float, CultureInfo.CurrentCulture, out double delay)) throw new ArgumentException("Enter a start delay from 0 to 60 seconds.");
        var options = new ClickOptions { Rate = rate, Button = (Core.MouseButton)ButtonInput.SelectedIndex, DoubleClick = TypeInput.SelectedIndex == 1, RepeatLimit = limit, DelaySeconds = delay, FixedPosition = FixedInput.IsChecked == true, X = positionX, Y = positionY, Activation = (ActivationMode)ModeInput.SelectedIndex, Hotkey = currentHotkey };
        options.Validate(); return options;
    }

    private void Start(bool hold)
    {
        if (!hotkeysReady) return;
        try
        {
            var options = ReadOptions();
            if (!smoke)
            {
                try { SettingsStore.Save(options); }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { SetMessage("Settings could not be saved. This run will still work.", true); }
            }
            engine.Start(options, hold);
            Tick();
        }
        catch (ArgumentException ex) { SetMessage(ex.Message, true); }
    }

    private void Toggle() { if (engine.IsActive) EmergencyStop(); else Start(false); }
    public void EmergencyStop() { engine.Stop(); }
    private void StartClick(object sender, RoutedEventArgs e) { if (engine.IsActive || ModeInput.SelectedIndex == 0) Toggle(); }
    private void PresetClick(object sender, RoutedEventArgs e) => RateInput.Text = ((Button)sender).Tag.ToString();
    private void RateChanged(object sender, TextChangedEventArgs e) { if (initialized) RefreshRate(); }
    private void TypeChanged(object sender, SelectionChangedEventArgs e) { if (initialized) RefreshRate(); }
    private void ModeChanged(object sender, SelectionChangedEventArgs e) { if (initialized) RefreshButton(); }
    private void PositionChanged(object sender, RoutedEventArgs e) { if (initialized) RefreshPosition(); }
    private void RefreshRate()
    {
        if (int.TryParse(RateInput.Text, out int rate) && rate is >= 1 and <= 10000)
            IntervalLabel.Text = $"{1000.0 / rate:0.###} ms between repeats" + (TypeInput.SelectedIndex == 1 ? "\n2 clicks per repeat" : "");
        else IntervalLabel.Text = "Enter 1–10,000";
    }
    private void RefreshButton()
    {
        bool active = engine.IsActive;
        string key = $"F{currentHotkey - 0x6F}";
        StartButton.Content = active ? "Stop clicking · F8" : ModeInput.SelectedIndex == 1 ? $"Hold {key} to click" : $"Start clicking · {key}";
        StartButton.IsEnabled = hotkeysReady && (active || ModeInput.SelectedIndex == 0);
    }
    private void HotkeyChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!initialized || handle == 0 || HotkeyInput.SelectedIndex < 0) return;
        int previous = currentHotkey, requested = hotkeys[HotkeyInput.SelectedIndex];
        if (previous == requested) return;
        Native.UnregisterHotKey(handle, 1);
        if (Native.RegisterHotKey(handle, 1, 0x4000, (uint)requested))
        {
            currentHotkey = requested;
            Native.UnregisterHotKey(handle, 2);
            hotkeysReady = Native.RegisterHotKey(handle, 2, 0x4000, 0x77);
            SetMessage($"F{requested - 0x6F} starts or stops. F8 always stops.");
        }
        else
        {
            Native.RegisterHotKey(handle, 1, 0x4000, (uint)previous);
            HotkeyInput.SelectedIndex = Array.IndexOf(hotkeys, previous);
            SetMessage($"F{requested - 0x6F} is already used by another app. Your previous hotkey is unchanged.", true);
        }
        RefreshButton();
    }
    private void CapturePosition()
    {
        if (!Native.GetCursorPos(out var point)) { SetMessage("Windows could not read the pointer position.", true); return; }
        positionX = point.X; positionY = point.Y; FixedInput.IsChecked = true; RefreshPosition();
        SetMessage($"Position selected: {positionX}, {positionY}.");
    }
    private void PickClick(object sender, RoutedEventArgs e) => SetMessage("Move your pointer to the target, then press F7 to save that position.");
    private void RefreshPosition() => PositionLabel.Text = FixedInput.IsChecked == true ? $"X {positionX:N0}   Y {positionY:N0} · press F7 to replace" : "Following your cursor";
    private void SetMessage(string message, bool error = false)
    {
        MessageLabel.Text = message;
        MessageLabel.Foreground = (Brush)new BrushConverter().ConvertFromString(error ? "#FFB4AB" : "#CAC4D0")!;
    }
    private void TestClick(object sender, RoutedEventArgs e)
    {
        try { new SpeedTestWindow(ReadOptions().Rate) { Owner = this }.ShowDialog(); }
        catch (ArgumentException ex) { SetMessage(ex.Message, true); }
    }

    private async Task SmokeTest()
    {
        try
        {
            await Task.Delay(250);
            RateInput.Text = "invalid";
            StartButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            if (engine.IsActive || !MessageLabel.Text.Contains("whole-number")) throw new InvalidOperationException("Invalid speed did not block start.");
            RateInput.Text = "10000"; LimitInput.Text = "137"; DelayInput.Text = "0";
            var o = ReadOptions();
            if (o.Rate != 10000 || o.RepeatLimit != 137) throw new InvalidOperationException("Options did not round-trip.");
            RateInput.Text = "100"; LimitInput.Text = "0"; DelayInput.Text = "3";
            ModeInput.SelectedIndex = 1;
            if (StartButton.IsEnabled) throw new InvalidOperationException("Hold mode must be started by the held hotkey.");
            ModeInput.SelectedIndex = 0;
            DelayInput.Text = "3";
            bool handled = false;
            WindowMessage(handle, 0x0312, 1, 0, ref handled);
            if (!engine.IsActive || !handled) throw new InvalidOperationException("Start hotkey routing failed.");
            WindowMessage(handle, 0x0312, 2, 0, ref handled);
            for (int i = 0; i < 100 && engine.IsActive; i++) await Task.Delay(10);
            if (engine.IsActive || engine.Snapshot.Clicks != 0) throw new InvalidOperationException("Stop hotkey did not cancel the countdown.");
            using (var test = new AutomatedWindowScope(new SpeedTestWindow(1000) { Owner = this }))
            {
                test.Window.Show();
                await test.Window.VerifyAsync();
            }
            Activate();
            StateLabel.Text = "Ready"; CountLabel.Text = "0"; SpeedLabel.Text = "—"; TimeLabel.Text = "0.0s";
            SetMessage("F6 starts or stops. F8 always stops.");
            await Task.Delay(150);
            UpdateLayout();
            string output = arguments.SkipWhile(a => a != "--screenshot").Skip(1).FirstOrDefault() ?? "test-results/ui.png";
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(output))!);
            var visual = (FrameworkElement)Content;
            var bitmap = new RenderTargetBitmap((int)visual.ActualWidth, (int)visual.ActualHeight, 96, 96, PixelFormats.Pbgra32);
            bitmap.Render(visual);
            var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
            using (var file = File.Create(output)) encoder.Save(file);
            File.WriteAllText(Path.ChangeExtension(output, ".json"), System.Text.Json.JsonSerializer.Serialize(new { passed = true, hotkeysReady, width = ActualWidth, height = ActualHeight, tests = new[] { "invalid input prevents start", "options round-trip", "hold mode activation", "WM_HOTKEY toggle/stop routing", "built-in test receives 2000 clicks", "XAML render" } }));
            Application.Current.Shutdown(0);
        }
        catch (Exception ex)
        {
            Directory.CreateDirectory("test-results"); File.WriteAllText("test-results/ui-error.txt", ex.ToString());
            Application.Current.Shutdown(1);
        }
    }
    private sealed class AutomatedWindowScope(SpeedTestWindow window) : IDisposable
    {
        public SpeedTestWindow Window { get; } = window;
        public void Dispose() => Window.Close();
    }
    [DllImport("dwmapi.dll")] private static extern int DwmSetWindowAttribute(nint window, int attribute, ref int value, int size);
}
