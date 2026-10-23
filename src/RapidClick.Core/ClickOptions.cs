namespace RapidClick.Core;

public enum MouseButton { Left, Right, Middle }
public enum ActivationMode { Toggle, Hold }
public enum RunState { Ready, Waiting, Running, Stopped, Completed, Error }

public sealed record ClickOptions
{
    public int Rate { get; init; } = 100;
    public MouseButton Button { get; init; } = MouseButton.Left;
    public bool DoubleClick { get; init; }
    public long RepeatLimit { get; init; }
    public double DelaySeconds { get; init; } = 3;
    public bool FixedPosition { get; init; }
    public int X { get; init; }
    public int Y { get; init; }
    public ActivationMode Activation { get; init; }
    public int Hotkey { get; init; } = 0x75; // F6

    public void Validate()
    {
        if (Rate is < 1 or > 10_000) throw new ArgumentException("Speed must be between 1 and 10,000 repeats per second.");
        if (RepeatLimit is < 0 or > 1_000_000_000) throw new ArgumentException("Repeat count must be 0 to 1,000,000,000. Use 0 for unlimited.");
        if (!double.IsFinite(DelaySeconds) || DelaySeconds is < 0 or > 60) throw new ArgumentException("Start delay must be between 0 and 60 seconds.");
        if (!Enum.IsDefined(Button) || !Enum.IsDefined(Activation)) throw new ArgumentException("Invalid mouse button or activation mode.");
        if (Hotkey is < 0x70 or > 0x7A || Hotkey is 0x76 or 0x77) throw new ArgumentException("Choose F1–F11, except F7 (pick position) and F8 (stop).");
    }
}

public sealed record ClickSnapshot(RunState State, long Repeats, long Clicks, double ElapsedSeconds, string Message)
{
    public double AverageRate => ElapsedSeconds > 0 ? Repeats / ElapsedSeconds : 0;
}
