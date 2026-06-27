<div align="center">

<img src="assets/logo.png" alt="SysGreen" width="112" height="112" />

# SysGreen

**A Windows desktop system-footprint manager — reclaim startup RAM safely and reversibly.**

[![CI](https://github.com/xer4yx/SysGreen/actions/workflows/ci.yml/badge.svg)](https://github.com/xer4yx/SysGreen/actions/workflows/ci.yml)
&nbsp;![version](https://img.shields.io/badge/version-0.20.0-2E7D32)
&nbsp;![platform](https://img.shields.io/badge/platform-Windows%2010%2F11-555)
&nbsp;![.NET](https://img.shields.io/badge/.NET-10-512BD4)
&nbsp;[![License](https://img.shields.io/badge/license-Apache--2.0-blue)](LICENSE)

</div>

## What it is

SysGreen helps you reduce what loads at startup and sits resident in memory after boot —
**without breaking your machine**. It enumerates everything that auto-starts, explains what each
item *is* and *how risky it is to disable*, recommends the safe wins, and lets you **disable and
undo every change**. It is a *system footprint manager*, **not** a memory profiler, RAM cleaner, or
"booster".

The long-term mission spans startup time, idle CPU/disk/network, and telemetry; the current **MVP is
scoped to RAM** — the memory consumed by things that auto-start. See [`CONTEXT.md`](CONTEXT.md) for
the full glossary and [`docs/adr/`](docs/adr) for the decisions behind the design.

## Highlights

- **Safe & reversible by construction.** Disabling never deletes — it flips Windows' own native
  disable flags (StartupApproved keys, scheduled-task state, background-app flags), so every change
  is undoable. A System Restore Point is created before any boot-relevant batch.
- **Knows what your startup items are.** A curated, offline **Knowledge Base** maps publishers and
  executables to a **Purpose** (Gaming, Updater, Telemetry, …) and a **Safety Rating** (Safe →
  Required-for-Boot). Unknowns fall back to a heuristic, and **your own overrides always win**.
- **Recommends the invisible wins** — telemetry, updaters, bloat you never open — plus
  **habit-based** suggestions for apps you haven't launched in a while. Nothing is ever auto-applied.
- **Acts on running processes too.** *End Task* frees RAM right now; *Disable* stops something
  loading next boot.
- **Full history & one-click undo.** Every applied batch is recorded and reversible.
- **Privacy-first.** Launch tracking is local-only, disclosed on first run, and switchable off from
  the tray. Nothing is uploaded.

## Install

SysGreen is distributed as a **self-contained** Windows installer (built with [Velopack](https://velopack.io)) —
you don't need .NET or anything else first, and after the first install it **updates itself**.

1. Download **`SysGreen-win-Setup.exe`** from the
   [**Releases**](https://github.com/xer4yx/SysGreen/releases) page.
2. Run it. It installs for the current user (no administrator prompt) and launches automatically.
3. It isn't code-signed yet, so SmartScreen may say *"Windows protected your PC / Unknown publisher."*
   Click **More info → Run anyway** (signing is a tracked launch blocker —
   [ADR-0009](docs/adr/0009-distribution-and-signing.md)).
4. From then on, SysGreen checks for new releases on launch and offers to **Install & restart** when
   one is available.

> Releases are cut from version tags. If the Releases page is empty, no public build has been
> published yet — you can [build the installer from source](CONTRIBUTING.md#building-the-installer) in
> the meantime.

## How it's built

A small WPF (.NET 10) app plus two tiny co-located executables:

| Project | Role |
| --- | --- |
| `SysGreen.App` | WPF UI; runs **non-elevated**; composes the object graph. |
| `SysGreen.Core` | Pure domain + logic (classification, recommendations, apply/undo). Unit-tested. |
| `SysGreen.Platform` | Thin Win32 / registry / WMI / Task Scheduler adapters. |
| `SysGreen.Data` | Local SQLite storage (usage, change log, settings, overrides). |
| `SysGreen.Helper` | Short-lived **elevated** worker, spawned only for admin-only changes. |
| `SysGreen.Agent` | Lightweight resident tray agent that samples app launches. |

Logic lives in `Core` behind interfaces; the OS-touching shells in `Platform` stay thin. See
[ADR-0011](docs/adr/0011-solution-structure-and-ipc.md) for the structure and IPC, and
[ADR-0004](docs/adr/0004-privilege-and-process-model.md) for the privilege model.

## Getting started

> Requires Windows 10/11 and the **.NET 10 SDK**. Full IDE setup, conventions, and troubleshooting
> are in [`CONTRIBUTING.md`](CONTRIBUTING.md).

```bash
# Build everything
dotnet build SysGreen.slnx -c Debug

# Run the test suite
dotnet test tests/SysGreen.Tests/SysGreen.Tests.csproj

# Launch the app (or press F5 in your IDE)
dotnet run --project src/SysGreen.App/SysGreen.App.csproj
```

The first launch shows a welcome + privacy screen. The app is **read-only until you click Apply** —
it changes nothing on your system just by browsing.

## Versioning

SysGreen follows [Semantic Versioning](https://semver.org/) in the **0.x** pre-release line; the
current version (shown in the app's main window) is sourced from a single `<Version>` in
`Directory.Build.props`. Bumps are manual and governed by Conventional Commits — see
[ADR-0015](docs/adr/0015-versioning-and-commit-conventions.md).

## Code signing

> **Status:** release builds are **not yet code-signed**, so Windows SmartScreen may warn *"Unknown
> publisher."* Until signing is enabled, click *More info → Run anyway* — see [Install](#install) and
> [ADR-0009](docs/adr/0009-distribution-and-signing.md). Enrolment in the SignPath Foundation OSS
> program is the planned path ([docs/code-signing.md](docs/code-signing.md)).

### Code signing policy

Free code signing provided by [SignPath.io](https://signpath.io), certificate by
[SignPath Foundation](https://signpath.org).

**Privacy:** This program will not transfer any information to other networked systems unless
specifically requested by the user or operator of the program. SysGreen's usage data is stored
**locally only** and is never uploaded (see [ADR-0012](docs/adr/0012-privacy-and-telemetry-stance.md)).

**Roles**

- **Committers & reviewers:** [@xer4yx](https://github.com/xer4yx)
- **Approvers:** [@xer4yx](https://github.com/xer4yx)

## Documentation

- [`CONTEXT.md`](CONTEXT.md) — the ubiquitous language / glossary (read this first).
- [`docs/adr/`](docs/adr) — Architecture Decision Records (0001–0015).
- [`CONTRIBUTING.md`](CONTRIBUTING.md) — how to build, test, run, and commit.
- [`SECURITY.md`](SECURITY.md) — how to report a vulnerability.
- [`docs/code-signing.md`](docs/code-signing.md) — SignPath Foundation enrolment + signing pipeline.

## License

Licensed under the **[Apache License 2.0](LICENSE)** — see [LICENSE](LICENSE) and [NOTICE](NOTICE).
Contributions are accepted under the same license (Apache-2.0 §5).
