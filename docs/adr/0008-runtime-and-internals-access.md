# Runtime and Windows-internals access strategy

**Runtime: .NET 10 (LTS)**, target `net10.0-windows`, x64. As of mid-2026, .NET 8 (the previous LTS) reaches end-of-support around Nov 2026 and .NET 9 (STS) is already out of support — neither is a sound base for a new project with long-run goals. .NET 10 is the current LTS (GA Nov 2025, supported to ~Nov 2028) and fully supports WPF.

**Access strategy:** prefer built-in/managed APIs wherever they exist — `Microsoft.Win32.Registry` (Run keys + StartupApproved flags), `System.ServiceProcess.ServiceController` (services), `System.Diagnostics.Process` (processes + Private Working Set), and the **TaskScheduler NuGet** (scheduled tasks) rather than hand-rolled COM. Each item kind sits behind a **provider interface** (`IAutostartProvider`, `IProcessProvider`, etc.) so the risky native bits are isolated, mockable, and individually testable.

**Habit seed:** **UserAssist** (registry; ROT13 + known binary layout) is the **primary** historical source. **Prefetch** parsing (`.pf` files — compressed, version-fragile binary) is **optional/best-effort** and must never block the MVP; the forward-looking Tray Agent fills any gap.
