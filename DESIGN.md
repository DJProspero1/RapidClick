# RapidClick native design

The user rejected the large v1 settings interface and explicitly prioritized a bare-bones, extremely responsive utility. Dark mode remains; decoration and configurability give way to immediate use.

- A fixed 350×245 logical-pixel client area, scaled for Windows DPI.
- Speed, mouse button, transport, start/stop, one status line. No cards, dashboard, presets, diagrams, or live graphs.
- Dark background #141218, native filled fields #2B2930, readable text #E6E0E9, one pale-purple primary button #D0BCFF.
- Segoe UI, ordinary Win32 controls, no animation, no continuous rendering loop.
- Idle UI has no timer. While clicking, status updates twice per second. Stop feedback is immediate, with a short completion check.
- Every failure appears inline. No alerts, beeps, sound playback, or modal message boxes.
- The transport choice is visible because direct window messages and standard mouse input have materially different compatibility and performance.
