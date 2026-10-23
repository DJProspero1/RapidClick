# RapidClick

Native Windows utility: .NET 10, WPF, Win32 SendInput. Preserve the dark Material-inspired design, the independent worker, and responsive stop behavior.

- Build, test, and package locally using `scripts/build.ps1`; do not add cloud builds or GitHub Actions.
- Do not launch a development server. This application has none.
- Use `dotnet run --project tests/RapidClick.Tests -c Release` for behavioral tests. Native injection tests require `-- --native`; keep injection inside the dedicated test window and restore the pointer.
- Do not equate accepted SendInput events with delivery to arbitrary target applications.
- Keep button pairs complete, batches bounded, finite counts exact, and cancellation independent of the UI message pump.
- Never silently change elevation, run on startup, or introduce networking or telemetry.
- Source and release artifacts are public. Never commit build outputs, local settings, tokens, or machine-specific paths.
- Refer to PRODUCT.md and DESIGN.md for product and visual direction.
