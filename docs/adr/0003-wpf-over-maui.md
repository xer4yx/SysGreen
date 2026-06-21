# UI framework: WPF, not MAUI

SysGreen's desktop UI is built with **WPF** on current .NET (8/9), C#.

MAUI was the initial preference but was rejected. MAUI's sole advantage is sharing one UI across iOS/Android/Mac/Windows, and SysGreen is a **Windows-only system utility** whose every operation is Windows-specific (registry, WMI, P/Invoke, SCM, Prefetch/UserAssist, a system-tray agent, UAC elevation). MAUI provides no benefit here while actively obstructing the design: no built-in Windows tray support, and a default MSIX/sandboxed packaging model that makes the required elevation and unrestricted registry/process access awkward.

WPF gives unrestricted Win32/registry/WMI access, a trivial admin manifest, **unpackaged** deployment (no container), mature data-grid/tree controls, and solid tray support via libraries (e.g., H.NotifyIcon). WinUI 3 was considered (more modern Fluent aesthetic) but has a rougher tray + elevation + unpackaged story; not worth the friction for the MVP.
