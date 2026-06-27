# Distribution: Velopack (superseded Inno Setup); code signing deferred (with risk noted)

SysGreen ships as a traditional **unpackaged** installer built with **Inno Setup** (per-machine, `Program Files`), with **Velopack** for app self-update. The Knowledge Base ships **inside each release** — no separate KB download channel in the MVP (keeps it offline-first per ADR-0002). MSIX was rejected because it re-introduces the sandbox/container deliberately avoided in ADR-0003 and ADR-0004.

**Code signing is deferred** for the MVP/beta phase. Accepted consequence: early users will see SmartScreen "Unknown publisher" warnings and face a higher chance of antivirus/PUA flags (the app modifies the registry, services, and disables telemetry — heuristically malware-like), and each unsigned Velopack update restarts SmartScreen reputation. This is tolerable for internal/beta distribution but is a **launch blocker that must be revisited before any public release** (EV certificate preferred for immediate SmartScreen reputation). Signing is orthogonal to the architecture, so it can be added later with no structural change.

## As built (Tier 4 #15) — Velopack replaced Inno Setup

The original "Inno installer + Velopack updates" pairing **does not work**: Velopack's `UpdateManager` can only update a Velopack-managed install (it requires `Update.exe` + `sq.version` and throws `NotInstalledException` otherwise), so an Inno-installed app can never self-update. Rather than keep a self-update mechanism that couldn't run, **Velopack now owns both install and update** and the Inno Setup script was retired (`installer/SysGreen.iss` deleted). Decisions:

- **Velopack packaging.** `installer/build.ps1` publishes App + Helper + Agent **self-contained (win-x64)** into one payload (no .NET runtime needed on the box) and runs `vpk pack` → `SysGreen-win-Setup.exe` + full/delta `.nupkg` + the `releases.win.json` feed. CI (`installer.yml`) builds it and, on a `v*` tag, attaches everything to a **draft GitHub Release** — which is both the download and the self-update feed.
- **Per-user install** (`%LocalAppData%`), the Velopack default — the trade for seamless, no-UAC delta self-update. This **changes the earlier per-machine/Program Files choice**: acceptable because the UI runs non-elevated anyway (ADR-0004) and the Helper still elevates on demand regardless of install location. Per-machine remains possible later via Velopack's WiX-5 MSI if needed.
- **In-app self-update.** A custom `Program.Main` runs `VelopackApp.Build().Run()` before WPF; `VelopackUpdateService` (behind `IUpdateService`) reads GitHub Releases via `GithubSource`, and the main window shows an "update available → Install & restart" banner. It fails safe to "no update" when not a Velopack install (dev runs).
- **Version** is the single `<Version>` in `Directory.Build.props` (ADR-0015), read by `build.ps1` into `vpk pack --packVersion`.

**Signing (deferred) integrates with `vpk pack`** (`--signTemplate`/`--signParams`) rather than an installer-specific step — see `docs/code-signing.md`. Until then, unsigned Velopack updates restart SmartScreen reputation each release, as noted above.
