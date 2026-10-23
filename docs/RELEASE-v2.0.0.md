RapidClick rebuilt as a small native Windows utility.

- Approximately 45 KB, with no .NET or other runtime installation.
- One 350×245 window: speed, button, transport, start/stop.
- Direct-to-window mode for high throughput; standard input for compatibility.
- Silent tests, no benchmark sounds, no animated UI or continuously updating dashboard.
- F6 starts/stops; F8 stops. The Start button gives 0.7 seconds to point at the target.
- Cell to Singularity profile: automatically uses standard input at up to 50/sec, matching the installed game's background-tap limit and avoiding excess input work. The selected rate and status show this adjustment.

Direct mode delivered approximately **10,000 click messages/sec** to the native test receiver with sub-millisecond response times. It sends window messages, so some games and applications will ignore it. Standard mode uses `SendInput`; its measured limit on the build PC remains around 2,600/sec. The transport names in the UI make this distinction explicit.

The 10,000 result is not a claim that Cell to Singularity accepts 10,000 taps/sec. Its cap was confirmed by inspecting the installed game's background-tap code and is consistent with its developer's public explanation. Gameplay effects were not verified live because the Windows inspection helper was unavailable.

This replaces the heavy v1 interface. Repeat limits, double-click settings, presets, and the built-in benchmark screen have been removed.

Download **RapidClick-2.0.0-win-x64.exe**. Windows 10/11 x64, unsigned. Built, tested, and packaged locally. See [verification](https://github.com/DJProspero1/RapidClick/blob/main/docs/VERIFICATION.md) for exact measurements and limitations. SHA-256 hashes are attached.
