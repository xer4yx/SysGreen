# Recommendation policy: never auto-apply; safety gate + weighted ranking

**SysGreen never changes anything on its own.** It only produces Recommendations the user explicitly confirms via Apply (which triggers the mandatory restore point + Change Records). A "Select all recommended" convenience keeps it one click while the human stays in the loop. Auto-apply may become a power-user opt-in post-MVP, but is never the default — the tool can break a machine, so a human must confirm.

Recommendations from the two sources (Static + Habit) combine as follows:

- **Safety is a hard gate:** only items rated **Safe** are ever recommended. Caution / Do-Not-Touch / Required-for-Boot are never in the recommended set (still visible and manually toggleable, just not recommended).
- **Purpose weight:** telemetry / updater / OEM-bloat rank highest (pure overhead); media / comms / gaming rank moderate (real apps).
- **Evidence weight:** static evidence ("known telemetry") gives a baseline; habit evidence ("Abandoned — N days") boosts it. Items that are both known-overhead and abandoned rank top.

Every recommendation shows a plain-language reason and an estimated RAM figure, e.g. *"Disable Spotify autostart — not opened in 47 days, ~380 MB at startup; safe, you can still launch it anytime."*
