# v1.0.0 verification

Verified locally on Windows 11 x64 (build 26200) on 23 October 2026. Built using .NET SDK 10.0.402 and runtime 10.0.13. The release is a self-contained, compressed, single-file Windows x64 executable. Source builds finished with zero warnings and errors.

## Observed input delivery

`verification-native.json` contains the complete passing native run. A foreground Win32 receiver counted only RapidClick-tagged input and checked both presses and releases. All requested finite counts arrived exactly. The receiver was pumped continuously during the test; its CPU consumption is included in the report and is not the idle application's CPU use.

| Requested repeats/sec | Clicks sent | Clicks received | Observed repeats/sec |
| ---: | ---: | ---: | ---: |
| 100 | 200 | 200 | 100.5 |
| 1,000 | 2,000 | 2,000 | 1,000.3 |
| 2,500 | 3,000 | 3,000 | 2,097.3 |
| 5,000 | 3,000 | 3,000 | 2,079.4 |
| 10,000 | 3,000 | 3,000 | 2,113.2 |

**The 10,000 setting is a requested rate, not a claim that this machine delivered 10,000 clicks/sec.** In this run, the tested path topped out around 2,100/sec. Different target applications, desktop load, hooks, security boundaries, and hardware can change throughput. Games that sample mouse state per frame may process far fewer clicks.

Elapsed time runs from the beginning of emission through the final submission. Short finite tests at low rates can therefore show a slightly higher average than the configured rate.

The native stop test completed in **9.95 ms** at the highest requested rate. No further clicks arrived after the input queue drained. This is one measured run, not a real-time guarantee. The tests also verified exact left/right/middle double-click counts, their corresponding releases, and fixed-position delivery.

Early large-batch experiments stalled on this desktop. The released implementation uses small submissions and combines a click's down/up transitions in one native input record. The test suite verifies that Windows still delivers both transitions. Closing and reopening OP Auto Clicker did not remove the original large-batch behavior; it was restored afterwards. No performance claim depends on closing other software.

## Behavior and packaged application

`verification-behavior.json` records passing validation, exact finite counts, repeatable starts, countdown cancellation without clicks, hold-key release, emergency-stop polling, overlapping-start prevention, and input-failure reporting.

`verification-packaged-ui.json` records checks performed by the actual self-contained release executable:

- Invalid speed prevents a run.
- UI settings round-trip to validated engine options.
- Hold mode cannot accidentally start through the toggle button.
- The window's hotkey-message routing starts and stops a countdown without clicks.
- The built-in speed-test window receives all 2,000 clicks at the 1,000/sec setting.
- The complete dark interface renders successfully; `rapidclick.png` is a capture from this run.

Hotkey-message routing was exercised directly; physical key presses were not synthesized across other running apps. Global registrations succeeded in the packaged run. Cross-app delivery was verified with native mouse input, not `PostMessage` clicks.

## Scope

Verified: this Windows 11 x64 machine, native receiver, built-in WPF receiver, and the packaged executable. Windows 10, other machines, ARM64, protected/elevated targets, and game-specific acceptance were not tested. The binary is unsigned. The app has no network or update component.

Re-run `scripts/build.ps1 -NativeTests` on the target machine to reproduce the checks. The native fixture must keep focus; a competing foreground window aborts input safely. Leave the mouse and keyboard idle during the run.

## Windows API references

- [SendInput](https://learn.microsoft.com/windows/win32/api/winuser/nf-winuser-sendinput): insertion result, serial input, and integrity-level limitations.
- [MOUSEINPUT](https://learn.microsoft.com/windows/win32/api/winuser/ns-winuser-mouseinput): transition flags and virtual-desktop coordinates.
- [Waitable timer objects](https://learn.microsoft.com/windows/win32/sync/waitable-timer-objects): scheduler waiting.
- [RegisterHotKey](https://learn.microsoft.com/windows/win32/api/winuser/nf-winuser-registerhotkey): global key registration and no-repeat behavior.
