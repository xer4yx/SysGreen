# Solution structure and App↔Helper IPC

One solution, `net10.0-windows` throughout, decomposed into seven projects:

- **SysGreen.Core** (lib) — domain model, recommendation engine + scoring, KB loader, provider **interfaces**. Pure, fully unit-testable.
- **SysGreen.Platform** (lib) — concrete providers (registry, process/Private-Working-Set, ServiceController, TaskScheduler, UserAssist, Prefetch), restore-point creation, the disable/enable operations. All Win32-touching code, behind Core's interfaces.
- **SysGreen.Data** (lib) — SQLite + Dapper repositories for Usage + Change Records.
- **SysGreen.App** (WPF exe, non-elevated) — the three-view MVVM UI.
- **SysGreen.Agent** (exe, non-elevated, tray) — the Tray Agent, running at logon **independently of the UI** so habits are observed even when the main window is closed.
- **SysGreen.Helper** (console exe, `requireAdministrator` manifest) — the Elevated Helper.
- **SysGreen.Tests** (xUnit) — Core logic against mocked providers; Data tests.

The **Core/Platform split** (interfaces vs Win32 implementations) keeps the domain logic testable without touching the live system.

**App↔Helper IPC:** when an Apply includes admin-only actions, the App serializes the batch to a **temp job file**, launches the Helper elevated via ShellExecute `runas` (one UAC prompt); the Helper executes it, writes the **restore point + Change Records into the shared SQLite DB**, and exits with a status code. **No long-lived IPC** (named pipes/sockets) — simpler and a smaller attack surface, with nothing resident. Supporting libs: CommunityToolkit.Mvvm + Microsoft.Extensions.DependencyInjection/Hosting.
