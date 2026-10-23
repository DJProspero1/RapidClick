using System.Diagnostics;

namespace RapidClick.Core;

public sealed class ClickEngine : IDisposable
{
    private readonly object gate = new();
    private readonly Func<ClickOptions, IClickSink> createSink;
    private readonly Func<int, bool> keyDown;
    private CancellationTokenSource? cancellation;
    private Thread? worker;
    private ClickSnapshot snapshot = new(RunState.Ready, 0, 0, 0, "Ready when you are.");
    private bool disposed;
    public ClickSnapshot Snapshot => Volatile.Read(ref snapshot);
    public bool IsActive { get { lock (gate) return worker?.IsAlive == true; } }

    public ClickEngine(Func<ClickOptions, IClickSink>? createSink = null, Func<int, bool>? keyDown = null)
    {
        this.createSink = createSink ?? (options => new NativeClickSink(options));
        this.keyDown = keyDown ?? Native.IsKeyDown;
    }

    public bool Start(ClickOptions options, bool requireHeldKey = false)
    {
        options.Validate();
        lock (gate)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            if (worker?.IsAlive == true) return false;
            cancellation?.Dispose();
            cancellation = new CancellationTokenSource();
            var token = cancellation.Token;
            snapshot = new(RunState.Waiting, 0, 0, 0, "Preparing…");
            worker = new Thread(() => Run(options, requireHeldKey, token))
            {
                IsBackground = true, Name = "RapidClick input worker", Priority = ThreadPriority.AboveNormal
            };
            worker.Start();
            return true;
        }
    }

    public void Stop() { lock (gate) cancellation?.Cancel(); }

    private void Run(ClickOptions options, bool requireHeldKey, CancellationToken token)
    {
        long repeats = 0;
        var clock = new Stopwatch();
        bool StopRequested() => keyDown(0x77) || (requireHeldKey && !keyDown(options.Hotkey));
        void Publish(RunState state, string message) => Volatile.Write(ref snapshot,
            new(state, repeats, repeats * (options.DoubleClick ? 2 : 1), clock.Elapsed.TotalSeconds, message));
        try
        {
            using var waiter = new PrecisionWaiter(token);
            using var sink = createSink(options);
            double countdownEnd = Stopwatch.GetTimestamp() + options.DelaySeconds * Stopwatch.Frequency;
            while (Stopwatch.GetTimestamp() < countdownEnd)
            {
                Publish(RunState.Waiting, $"Starting in {Math.Ceiling((countdownEnd - Stopwatch.GetTimestamp()) / Stopwatch.Frequency)}s…");
                waiter.Until(Math.Min(countdownEnd, Stopwatch.GetTimestamp() + Stopwatch.Frequency * 0.05), StopRequested);
            }
            clock.Start();
            double period = (double)Stopwatch.Frequency / options.Rate;
            double next = Stopwatch.GetTimestamp();
            double lastPublish = 0;
            while (true)
            {
                token.ThrowIfCancellationRequested();
                if (StopRequested()) throw new OperationCanceledException();
                double now = Stopwatch.GetTimestamp();
                long due = Math.Max(1, (long)((now - next) / period) + 1);
                int batch = (int)Math.Min(64, due);
                if (options.RepeatLimit > 0) batch = (int)Math.Min(batch, options.RepeatLimit - repeats);
                sink.Send(batch);
                repeats += batch;
                // Discard excessive timing debt after suspension instead of flooding the input queue.
                next += due * period;
                if (options.RepeatLimit > 0 && repeats >= options.RepeatLimit) break;
                if (clock.Elapsed.TotalSeconds - lastPublish >= 0.05)
                {
                    Publish(RunState.Running, "Clicking. F8 stops immediately.");
                    lastPublish = clock.Elapsed.TotalSeconds;
                }
                // Above 1,000/s, aggregate approximately 1 ms of work per native call.
                double deadline = next + Math.Max(0, Stopwatch.Frequency * 0.001 - period);
                waiter.Until(deadline, StopRequested);
            }
            clock.Stop();
            Publish(RunState.Completed, "Repeat count reached.");
        }
        catch (OperationCanceledException)
        {
            clock.Stop();
            Publish(RunState.Stopped, "Stopped.");
        }
        catch (Exception error)
        {
            clock.Stop();
            Publish(RunState.Error, error.Message);
        }
    }

    public void Dispose()
    {
        Thread? previous;
        lock (gate) { disposed = true; cancellation?.Cancel(); previous = worker; }
        previous?.Join();
        lock (gate) { cancellation?.Dispose(); cancellation = null; }
    }
}
