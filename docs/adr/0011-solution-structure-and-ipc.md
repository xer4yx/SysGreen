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

**Implementation notes (as built):**
- **Routing is all-or-nothing per Apply.** If *any* item in the batch requires elevation, the **whole** batch is handed to the Helper (which then also applies the per-user items under the same restore point); a fully per-user batch never spawns the Helper. This keeps the "one UAC prompt per Apply" promise and means the in-process `ApplyService` and the Helper run the *same* tested apply logic — the App-side `RoutingApplyService` only chooses between them.
- **Job file** (App→Helper) is JSON and self-contained: schema `Version`, the invoking user's `DatabasePath` (so an over-the-shoulder admin elevation still writes the *user's* DB), a `ResultPath`, and the `Changes`. Enums serialize as strings for readability.
- **Result channel** (Helper→App) is twofold: the **status code** (`HelperExitCodes`: success / job-file errors / restore-point-aborted / partial-failure) *and* a JSON **result file** at `ResultPath` carrying the per-item `ApplyResult`, so the App shows an accurate outcome without re-querying the DB. The Helper remains the sole writer of real Change Records; the App never re-persists them.
- The Helper's `SysGreen.Helper.exe` is co-located beside `SysGreen.App.exe` (build-time copy; install-time co-location in production) so the launcher resolves it from `AppContext.BaseDirectory`.
- **Disable is dispatched per mechanism.** A Core `DispatchingItemController` routes each Disable/Enable to the controller for the item's `AutostartLocation` — `StartupApprovedItemController` for Run keys and Startup folders (StartupApproved flags), `ScheduledTaskItemController` for logon scheduled tasks (the task's `Enabled` flag) — keeping each mechanism a small humble seam. Scheduled tasks are marked `RequiresElevation`, so task batches always route to the Helper; the Helper therefore composes the dispatching controller, while the non-elevated App only needs the StartupApproved path. Each record carries a `MechanismKey` (StartupApproved value name / shortcut file name / task path) so a reversal re-targets the exact item.
