# RapidClick

Native C/Win32 Windows utility. The user requires bare-bones UI, high performance, and silent testing.

- Build locally with `scripts/build.ps1`; use `scripts/bootstrap-native.ps1` for the pinned, checksum-verified compiler. No .NET, web runtime, server, cloud builds, or GitHub Actions.
- Keep the interface small: speed, mouse button, direct/standard mode, start/stop, status. Avoid restoring the old settings dashboard.
- Tests must be silent. Never use MessageBox, MessageBeep, Beep, PlaySound, or a noisy click target.
- Default tests use a hidden message-only receiver and must not generate global mouse/keyboard input. The optional raw-input diagnostic must constrain and verify its own target, allow F8 cancellation, and restore the pointer.
- Distinguish direct window messages from SendInput. Never imply 10,000 real mouse events/sec or universal game compatibility from direct-mode results.
- Preserve the transparent Cell to Singularity profile (standard input, max 50/sec) unless new evidence shows its background-tap cap changed. Never modify the game, saves, or anti-cheat behavior.
- The user explicitly chose to keep RapidClick external after discussing a local game mod. Preserve that boundary.
- Measure delivered counts, release counts, receiver latency, cancellation, and CPU, not just scheduling speed or accepted API calls.
- Bound synchronous calls and stop on nonresponsive or destroyed targets. Release a timed-out button asynchronously once; never build an unbounded input queue.
- No network, telemetry, startup task, driver, or silent elevation.
- See PRODUCT.md, DESIGN.md, and docs/VERIFICATION.md. The old managed implementation is retained in the v1.0.0 git tag only.
