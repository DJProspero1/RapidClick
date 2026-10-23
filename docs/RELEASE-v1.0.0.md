RapidClick is a portable Windows auto-clicker with a dark Material-inspired interface.

- Left, right, and middle clicks; single or double click.
- 1–10,000 requested repeats/sec, quick presets, exact repeat limits, and a start delay.
- Configurable global start/stop key, hold-to-click, F7 position picking, and F8 emergency stop.
- Live run statistics and a built-in test that compares clicks received with clicks sent.
- Offline settings; no account, ads, telemetry, or runtime installation.

Download **RapidClick-1.0.0-win-x64.exe** and run it. The ZIP contains the same executable, README, and license. Windows 10/11 x64; unsigned binary.

Built and packaged locally on Windows, with no cloud build. The actual packaged executable passed the UI and built-in click-delivery checks.

**Measured performance:** the native test window received all clicks, reaching approximately **2,100 clicks/sec on the build PC**; 1,000/sec ran at the requested rate. The 10,000 setting is a requested ceiling, not a guarantee. Native stop took 9.95 ms in the recorded run. Target applications and games can process fewer clicks. See [full verification evidence](https://github.com/DJProspero1/RapidClick/blob/main/docs/VERIFICATION.md).

Use **F6** to toggle, **F8** to stop, and **F7** to pick a fixed position. The default three-second delay leaves time to move to the target. Use **Test speed** to measure delivery on your machine.

SHA-256 hashes are attached in `SHA256SUMS.txt`.
