# Cell to Singularity input investigation

Decision: keep RapidClick external. No injected module, memory patch, modified game assembly, save edit, server request, or limiter bypass is part of this project.

## What was checked

Read-only inspection of the installed game's `Assembly-CSharp.dll` (SHA-256 `72bf1b8b6830662faa4f2d6fdb9d47fdbf3bd7ae47c294034fb8ef8cf9b61a87`) identified a shared background-tap counter with a limit of 50. It resets after the game-time accumulator exceeds one second.

The assembly was searched for every reference to `AllowTapInput`. The mouse background-tap handler, keyboard/background-tap utility, and event input path use that gate. The controller path was also traced: its ordinary background-tap paths reach the common helper; a fallback path has its own cooldown. The controller defaults inspected range from a 0.05-second initial interval to a 0.4-second held interval. This does not provide a route to 10,000 accepted taps/sec.

The [game developer publicly confirms that autoclickers are limited](https://steamcommunity.com/app/977400/discussions/0/6193093412366073347/). The installed code inspection narrows the observed background-tap limit to 50 per game-second for this build. Other actions or future versions may behave differently.

## Result

No external-input bypass was established. Sending faster mouse events does not remove a check inside the game. A client modification would change game behavior and still would not prove 10,000 counted taps/sec: Unity's input/update processing and per-tap effects also matter. The user chose to keep RapidClick external, so no such modification was prepared or installed.

RapidClick's Cell to Singularity profile selects standard input and no more than 50 clicks/sec. It displays the adjustment instead of presenting the requested 10,000 as accepted gameplay taps. Lower requested rates are preserved.

The approximately 10,000/sec result elsewhere in the project is explicitly a native **window-message receiver** benchmark, not a Cell to Singularity result.

## Verification boundary

Live gameplay testing was unavailable: the Windows Computer Use native pipe failed on initial attempts and after reconnection. No click burst was sent into the game during this investigation. No extracted game source or game binaries are included in the repository; temporary inspection output stayed in the ignored local artifacts directory.
