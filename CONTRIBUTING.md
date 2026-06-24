# Contributing to SysGreen

Thanks for helping out! This guide covers setting up a local environment, building/testing/running
the app, and the conventions the project follows. For the *why* behind the design, read
[`CONTEXT.md`](CONTEXT.md) and the ADRs in [`docs/adr/`](docs/adr) first.

## Prerequisites

| Requirement | Notes |
| --- | --- |
| **Windows 10 or 11** | The app targets `net10.0-windows` (WPF), so it builds and runs on Windows only. |
| **.NET 10 SDK** (≥ 10.0.300) | [Download here](https://dotnet.microsoft.com/download/dotnet/10.0). Verify with `dotnet --version`. |
| **An IDE** (one of the below) | |

There is no `global.json`, so the latest installed 10.0.x SDK is used.

### IDE setup

The solution uses the new XML solution format (**`SysGreen.slnx`**). Use a recent IDE that
understands it:

- **Visual Studio 2022** (17.12 or newer) with the **".NET desktop development"** workload. Open
  `SysGreen.slnx` directly.
- **JetBrains Rider** (2024.3 or newer). Open the folder or `SysGreen.slnx`.
- **VS Code** with the **C# Dev Kit** extension.

If your IDE doesn't yet support `.slnx`, the `dotnet` CLI always does — see below.

## Build, test, run

From the repository root:

```bash
# Build the whole solution (Debug)
dotnet build SysGreen.slnx -c Debug

# Build Release (what the installer will ship — CI guards both)
dotnet build SysGreen.slnx -c Release

# Run the test suite
dotnet test tests/SysGreen.Tests/SysGreen.Tests.csproj

# Launch the app (or press F5 / Run in your IDE)
dotnet run --project src/SysGreen.App/SysGreen.App.csproj
```

Notes:

- **First run** shows a welcome + privacy/consent screen before anything else.
- The app is **read-only until you click *Apply***. Browsing changes nothing on your system.
- `SysGreen.Helper.exe` and `SysGreen.Agent.exe` are built and co-located next to `SysGreen.App.exe`
  automatically; the app launches them at runtime.

CI runs `dotnet build` + `dotnet test` on `windows-latest` across **Debug and Release** for every PR
into `main` — make sure both pass locally before opening one.

## How we work

- **Test-Driven Development.** Write the test first, run it, watch it fail *for the right reason*,
  then write the minimal code to make it pass. One logical commit per red→green slice.
- **Humble-object boundary.** Pure logic lives in `SysGreen.Core` behind interfaces and is
  unit-tested; the thin Win32/registry/WMI/COM adapters in `SysGreen.Platform` are not. Extract the
  testable shape into `Core`.
- **Zero-warning bar.** Builds are expected to be **0 warnings / 0 errors** (nullable reference types
  and .NET analyzers are on).
- **One slice per branch off `main`.** Open a pull request into `main`; CI must be green. Don't
  rewrite published history without agreement, and stage specific paths rather than `git add -A`.

## Commit messages — Conventional Commits

Commits follow [**Conventional Commits**](https://www.conventionalcommits.org/) (Angular flavour).
This is what drives versioning (see [ADR-0015](docs/adr/0015-versioning-and-commit-conventions.md)).

```
<type>(<optional scope>): <short summary>

<optional body>

<optional footer(s)>
```

**Types**

| Type | Use for | Version effect (0.x) |
| --- | --- | --- |
| `feat` | A new feature | **minor** bump |
| `fix` | A bug fix | **patch** bump |
| `perf` | A performance improvement | **patch** bump |
| `docs` | Documentation only | none |
| `test` | Adding/Fixing tests | none |
| `refactor` | Code change that neither fixes a bug nor adds a feature | none |
| `style` | Formatting only (no logic) | none |
| `build` | Build system / dependencies | none |
| `ci` | CI configuration | none |
| `chore` | Maintenance (e.g. the version bump itself) | none |
| `revert` | Reverts a previous commit | depends |

**Scope** is optional and usually the project area: `app`, `core`, `data`, `platform`, `helper`,
`agent`.

**Breaking changes** are marked with a `!` after the type/scope (`feat(core)!: …`) or a
`BREAKING CHANGE:` footer. While the project is `< 1.0`, a breaking change bumps the **minor**, not
the major (per ADR-0015).

**Examples**

```
feat(app): show app version in the main window
fix(core): keep the prerelease label when stripping build metadata
docs: add ADR-0016 for the installer layout
chore: set version baseline to 0.20.0 (ADR-0015)
```

**Trailer.** AI-assisted commits end with a co-author trailer, e.g.:

```
Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>
```

## Versioning

The version is a single `<Version>` line in `Directory.Build.props` (every assembly inherits it; the
app displays it). Bumps are **manual for now**: edit that line in a `chore:` commit according to the
Conventional Commits above. The scheme is intentionally open to future automation (e.g. MinVer /
Nerdbank.GitVersioning) — see [ADR-0015](docs/adr/0015-versioning-and-commit-conventions.md).

## Pull requests

- Keep a PR to a single slice; describe *what* and *why*, and link any relevant ADR.
- If a change introduces new domain terms or a hard-to-reverse decision, update
  [`CONTEXT.md`](CONTEXT.md) and/or add an ADR in the same PR.
- Ensure CI (build + test, Debug & Release) is green.

## Troubleshooting

- **`error … file is locked by SysGreen.Agent`** — the app self-spawns the resident Agent, which can
  outlive a smoke run and lock the next build. Kill the stray processes:
  ```powershell
  Get-Process SysGreen.Agent, SysGreen.App -ErrorAction SilentlyContinue | Stop-Process -Force
  ```
- **Transient `MainWindow.g.cs not found` in a `_wpftmp` project** — a known WPF build glitch. Delete
  `src/SysGreen.App/obj` (and `bin`) and rebuild.
