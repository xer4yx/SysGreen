# Distribution: Inno Setup + Velopack; code signing deferred (with risk noted)

SysGreen ships as a traditional **unpackaged** installer built with **Inno Setup** (per-machine, `Program Files`), with **Velopack** for app self-update. The Knowledge Base ships **inside each release** — no separate KB download channel in the MVP (keeps it offline-first per ADR-0002). MSIX was rejected because it re-introduces the sandbox/container deliberately avoided in ADR-0003 and ADR-0004.

**Code signing is deferred** for the MVP/beta phase. Accepted consequence: early users will see SmartScreen "Unknown publisher" warnings and face a higher chance of antivirus/PUA flags (the app modifies the registry, services, and disables telemetry — heuristically malware-like), and each unsigned Velopack update restarts SmartScreen reputation. This is tolerable for internal/beta distribution but is a **launch blocker that must be revisited before any public release** (EV certificate preferred for immediate SmartScreen reputation). Signing is orthogonal to the architecture, so it can be added later with no structural change.

## As built (installer — Tier 4 #15)

The **Inno Setup** half is implemented: `installer/SysGreen.iss` + `installer/build.ps1` produce a per-machine `Setup.exe` (Program Files, `PrivilegesRequired=admin`), built in CI by `.github/workflows/installer.yml` and uploaded as an artifact. Decisions made here:

- **Self-contained (win-x64).** The App, Helper, and Agent are each published self-contained into one payload folder, so the target machine needs **no .NET runtime pre-installed** (~177 MB uncompressed; LZMA2-compressed in the Setup.exe). Chosen over framework-dependent because the audience is non-technical and the three co-located exes would otherwise each need the shared runtime resolved on the box.
- **Version** is the single `<Version>` in `Directory.Build.props` (ADR-0015), extracted by `build.ps1` and injected into the script (`/DMyAppVersion`) — no drift.
- **Non-elevated launch.** The post-install "run now" uses Inno's `runasoriginaluser`, so the UI starts non-elevated (ADR-0004) and elevates the Helper on demand. Uninstall stops the Agent/App first so their exes aren't locked.
- A stable `AppId` GUID lets future versions upgrade in place.

**Velopack self-update is deferred** to its own slice. It needs a release feed (hosting) and is only worthwhile once **code signing** lands — unsigned self-updates restart SmartScreen reputation each time. The choice is orthogonal: the self-contained per-machine payload can be wrapped by Velopack later without reworking the app.
