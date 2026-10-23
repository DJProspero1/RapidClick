# RapidClick

A tiny native Windows auto-clicker. Speed, mouse button, start/stop. No .NET, web runtime, animation, accounts, telemetry, or sounds.

[Download the Windows executable](https://github.com/DJProspero1/RapidClick/releases/latest)

![RapidClick](docs/rapidclick.png)

## Use

1. Enter clicks per second, from **1 to 10,000**.
2. Choose **Direct to window (fast)** or **Standard input (compatibility)**.
3. Point at the target and press **F6**. Press **F6** again or **F8** to stop.

The Start button gives you 0.7 seconds to point at the target. Direct mode keeps the selected window/control and client position, so you can move the pointer away afterwards. Standard mode follows your pointer. Clicking stops if the target disappears or stops answering.

**Cell to Singularity:** when that game is the selected target, RapidClick switches to standard input and limits the rate to 50/sec. The installed game explicitly caps background taps at 50 per game-second; sending thousands adds work without increasing accepted taps. The change is shown in the speed field and status line. Rates below 50 are preserved. No game files or saves are changed. This profile is based on code inspection; live gameplay verification was unavailable in this session.

**Direct mode** sends button-down/up messages to the selected window. It bypasses the global mouse-input queue and reached approximately **10,000 click messages/sec** in the included native receiver test, with sub-millisecond receiver response times. Some games and apps ignore window messages; this is not a claim of 10,000 accepted actions/sec in every application.

**Standard mode** uses Windows `SendInput` for apps that require mouse-input events. It retains the operating system's input path and its limits. That path measured about 2,600 clicks/sec on the development PC. Selecting 10,000 does not remove that limit. There is no driver, elevation bypass, or guaranteed game compatibility.

The current version intentionally removes the large settings panel, presets, double-click mode, repeat limits, and built-in benchmark screen. It has one small 350×245 client area and an approximately 45 KB executable. It starts idle, runs as a normal user, and saves only speed and mouse button in `HKCU\Software\RapidClick`. The application is unsigned.

## Local build and silent tests

Install [Zig 0.15.2](https://ziglang.org/download/0.15.2/) or run the checksum-verified bootstrap:

```powershell
./scripts/bootstrap-native.ps1
./scripts/build.ps1
```

The build uses Zig's C compiler and Windows libraries; the application itself is C/Win32. Python plus Pillow is used only to verify and capture the small interface. All compilation, testing, and packaging happen locally. No GitHub Actions or cloud builds.

Default tests use a hidden message-only receiver, produce **no sounds**, and inject **no global mouse or keyboard input**. They verify 1,000 and 10,000/sec, exact counts, button releases, cancellation, slow-target handling, receiver responsiveness, and the packaged interface. Build outputs, the EXE/ZIP, and checksums are in `artifacts/release-v2/`.

`./scripts/build.ps1 -GlobalInputProbe` additionally measures the standard input path in a silent, guarded native window. That optional diagnostic temporarily confines the pointer to its own blank target, then restores it. It never targets a game or another application. F8 cancels it.

See [verification evidence](docs/VERIFICATION.md) for measured results and scope. The v1 source remains available under the `v1.0.0` tag; it is no longer built.

## Source

| File | Purpose |
| --- | --- |
| `native/app.c` | Small dark Win32 interface and hotkeys |
| `native/engine.c` | Direct and standard transports, timing, cancellation |
| `native/tests.c` | Silent receiver and performance checks |
| `native/probe.c` | Optional guarded global-input diagnostic |
| `scripts/build.ps1` | Local build, checks, packaging |

MIT licensed. Not affiliated with Google.
