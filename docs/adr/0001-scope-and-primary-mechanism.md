# SysGreen MVP scope and primary mechanism

SysGreen is a Windows system **footprint manager**, not a memory profiler. Its long-term mission is reducing unnecessary background activity (startup time, idle CPU/disk/network, telemetry) safely and reversibly. We deliberately **scope the MVP to RAM consumed at startup**, because users feel high memory use at boot and it gives a concrete, measurable win.

The **primary mechanism is disabling Autostart Entries** (registry Run keys, Startup folder, logon scheduled tasks) so RAM hogs (Spotify, Discord, Teams, etc.) don't load each boot — *not* killing live processes. **End Task on Processes** is a secondary "instant relief" action only.

We rejected the obvious "process killer / RAM booster" framing because killing processes gives no persistent benefit (they return next boot) and Windows reclaims/reuses free RAM anyway. Targeting *what auto-starts* is what actually moves the metric. Windows Services proper are deferred to a later, higher-risk tier (high breakage risk, low RAM payoff at startup).
