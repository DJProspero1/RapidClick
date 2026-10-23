# RapidClick 2.0 verification

Built and tested locally on Windows 11 x64, 23 October 2026, with Zig 0.15.2's C compiler. Compilation uses warnings as errors. The release executable is 46,592 bytes, has no .NET runtime, and uses native Windows libraries.

## Delivery and responsiveness

The default suite sends direct button messages to a hidden native receiver. It never generates global mouse or keyboard input, plays sounds, shows message boxes, or clicks inside a game. Receiver responsiveness is measured with **posted queue messages**, not just synchronous calls that Windows can prioritize over queued input.

| Requested rate | Received clicks | Received releases | Measured rate | Receiver p95 | Receiver maximum |
| ---: | ---: | ---: | ---: | ---: | ---: |
| 1,000/sec | 3,000 | 3,000 | 1,000.3/sec | 0.213 ms | 0.340 ms |
| 10,000/sec | 30,000 | 30,000 | 9,976.2/sec | 0.254 ms | 0.417 ms |

The 10,000/sec case consumed 656.250 ms of combined sender/receiver CPU over 3.007146 seconds. These are process CPU milliseconds, not a percentage of the whole machine. Explicit stop completed in 0.241 ms and no further clicks arrived afterwards.

Also passed: exact right/middle button counts and releases, delay cancellation with zero clicks, invalid speed rejection, duplicate-start rejection, stopping on an unresponsive receiver, and releasing a button after timeout.

The packaged UI passed input validation and rendering checks. It used 14,108 KiB working set in this sample, with 0.000 ms measured idle CPU during 500 ms. Its client area is 350 by 245 logical pixels. Screenshot: `rapidclick.png`. Full machine-readable evidence: `verification-v2.json`.

## Standard input is a different path

A separate native SendInput probe measured roughly 2,300-2,600 clicks/sec in small batches on this PC. Larger batches stalled. Rewriting in native code does not remove Windows/global-hook/target overhead. The probe is diagnostic only; it is not run by the default suite. Standard mode bounds batches rather than accumulating an input backlog.

Direct mode's result measures delivery of window messages, not physical mouse input or accepted gameplay actions. It does not imply that every application accepts direct messages, or that its internal processing remains fast at that rate.

## Cell to Singularity

Read-only inspection of the installed `Assembly-CSharp.dll` found that the background-tap handler calls `Calculator.AllowTapInput()`. That method accepts up to 50 taps, and the counter resets after the game-time accumulator exceeds one second. The installed assembly's SHA-256 is `72bf1b8b6830662faa4f2d6fdb9d47fdbf3bd7ae47c294034fb8ef8cf9b61a87`. No game binary, save, configuration, process memory, or network request was modified.

The game's developer also [publicly confirms that autoclickers are limited](https://steamcommunity.com/app/977400/discussions/0/6193093412366073347/). This is a game-side acceptance limit. Flooding it with 10,000 inputs does not produce 10,000 counted background taps. RapidClick detects the game's process and transparently selects standard input at no more than 50/sec; lower requested speeds are retained.

**Not live-verified:** gameplay acceptance, actual currency gains, and frame-rate changes in Cell to Singularity. The Computer Use helper returned `Computer Use native pipe is unavailable ... (os error 2)` on all recovery attempts. The game's input cap is code-inspection evidence, not a claim of a successful live input test. Other gameplay paths or future game versions may behave differently.

## Scope and API sources

Verified on this Windows 11 x64 PC: the native engine, its hidden receiver, posted-message latency, error/cancel behavior, and the packaged UI. Windows 10 and other machines are not tested. The executable is unsigned. No universal zero-lag guarantee is made for arbitrary target software.

- [SendMessageTimeout](https://learn.microsoft.com/windows/win32/api/winuser/nf-winuser-sendmessagetimeoutw): bounded synchronous message delivery.
- [SendMessage](https://learn.microsoft.com/windows/win32/api/winuser/nf-winuser-sendmessage): window message semantics and integrity boundaries.
- [SendInput](https://learn.microsoft.com/windows/win32/api/winuser/nf-winuser-sendinput): Windows mouse-input injection and integrity limits.
