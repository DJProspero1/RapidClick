# RapidClick design

Dark Material-inspired desktop utility. Users move between a game or work application and RapidClick on the same Windows display; the requested dark surfaces avoid a bright interruption.

## Color

Restrained Material 3 dark palette, represented as sRGB because WPF brushes do not accept OKLCH: background #141218, surface #211F26, surface container #2B2930, outline #49454F, primary #D0BCFF, on-primary #381E72, text #E6E0E9, secondary text #CAC4D0, error #FFB4AB. Accent is reserved for the primary action, selected state, and focus.

## Type and layout

Segoe UI for dependable Windows rendering. 14px body, 12px helper text, 20px section headings, 28px app title. Generous 24px outer spacing; related settings use 12px gaps. One resizable single-window flow: speed, behavior, start/stop, run statistics. Scroll at small heights.

## Controls

Material-style filled text fields, outlined choice controls, rounded chips, and a pill-shaped primary action. Dark popup menus and visible focus rings. Disabled fields remain readable. State labels always include words. No animation, external fonts, imagery, or web runtime.
