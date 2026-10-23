using System.ComponentModel;
using System.Diagnostics;

namespace RapidClick.Core;

internal sealed class PrecisionWaiter : IDisposable
{
    private readonly nint timer;
    private readonly nint[] handles;
    private readonly CancellationToken token;

    public PrecisionWaiter(CancellationToken token)
    {
        this.token = token;
        timer = Native.CreateWaitableTimerEx(0, null, 2, 0x1F0003);
        if (timer == 0) timer = Native.CreateWaitableTimerEx(0, null, 0, 0x1F0003);
        if (timer == 0) throw new Win32Exception();
        handles = [token.WaitHandle.SafeWaitHandle.DangerousGetHandle(), timer];
    }

    public void Until(double deadline, Func<bool> shouldStop)
    {
        while (true)
        {
            token.ThrowIfCancellationRequested();
            if (shouldStop()) throw new OperationCanceledException();
            double remaining = (deadline - Stopwatch.GetTimestamp()) / Stopwatch.Frequency;
            if (remaining <= 0) return;
            // Check the physical stop/hold keys at least every 5 ms, even at 1 repeat/s.
            long due = -(long)Math.Max(1, Math.Min(remaining, 0.005) * 10_000_000);
            if (!Native.SetWaitableTimer(timer, ref due, 0, 0, 0, false)) throw new Win32Exception();
            uint result = Native.WaitForMultipleObjects(2, handles, false, uint.MaxValue);
            if (result == 0) token.ThrowIfCancellationRequested();
            if (result == uint.MaxValue) throw new Win32Exception();
        }
    }

    public void Dispose() => Native.CloseHandle(timer);
}
