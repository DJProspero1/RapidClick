using System.ComponentModel;
using System.Runtime.InteropServices;

namespace RapidClick.Core;

public interface IClickSink : IDisposable { void Send(int repeats); }

public sealed class NativeClickSink : IClickSink
{
    public const nuint InputSignature = 0x52434C4B;
    private readonly Native.Input[] inputs = new Native.Input[256];
    private readonly Native.Input[] move = new Native.Input[1];
    private readonly Native.Input[] release = new Native.Input[1];
    private readonly int eventsPerRepeat;
    private readonly ClickOptions options;
    private static readonly int InputSize = Marshal.SizeOf<Native.Input>();

    public NativeClickSink(ClickOptions options)
    {
        options.Validate();
        this.options = options;
        uint down = options.Button switch { MouseButton.Left => 2u, MouseButton.Right => 8u, _ => 32u };
        uint up = down * 2;
        eventsPerRepeat = options.DoubleClick ? 2 : 1;
        if (options.FixedPosition)
        {
            int left = Native.GetSystemMetrics(76), top = Native.GetSystemMetrics(77);
            int width = Native.GetSystemMetrics(78), height = Native.GetSystemMetrics(79);
            if (options.X < left || options.X >= left + width || options.Y < top || options.Y >= top + height)
                throw new ArgumentException("The selected position is outside the connected displays. Pick a new position with F7.");
            move[0].Data.Mouse = new Native.MouseInput
            {
                X = (int)(((long)options.X - left) * 65536 / width + 65536L / (width * 2L)),
                Y = (int)(((long)options.Y - top) * 65536 / height + 65536L / (height * 2L)),
                Flags = 0xC001, ExtraInfo = InputSignature // move, absolute, virtual desktop
            };
        }
        for (int i = 0; i < inputs.Length; i++)
        {
            inputs[i].Data.Mouse.Flags = down | up;
            inputs[i].Data.Mouse.ExtraInfo = InputSignature;
        }
        release[0].Data.Mouse.Flags = up;
        release[0].Data.Mouse.ExtraInfo = InputSignature;
    }

    public void Send(int repeats)
    {
        if (repeats is < 1 or > 64) throw new ArgumentOutOfRangeException(nameof(repeats));
        if (options.FixedPosition && (!Native.GetCursorPos(out var cursor) || cursor.X != options.X || cursor.Y != options.Y))
        {
            if (Native.SendInput(1, move, InputSize) != 1) throw new Win32Exception(Marshal.GetLastWin32Error(), "Windows could not move the pointer to the selected position.");
        }
        // Small native submissions avoid Windows input-queue stalls caused by large bursts.
        // Keep a complete down/up pair in every call so stopping cannot leave a held button.
        for (int remaining = repeats; remaining > 0;)
        {
            int portion = Math.Min(remaining, 2);
            uint count = (uint)(portion * eventsPerRepeat);
            uint sent = Native.SendInput(count, inputs, InputSize);
            if (sent != count)
            {
                int error = Marshal.GetLastWin32Error();
                Native.SendInput(1, release, InputSize);
                throw new Win32Exception(error, $"Windows accepted {sent} of {count} input events. The target may have higher privileges or block simulated input.");
            }
            remaining -= portion;
        }
    }

    public void Dispose() { }
}
