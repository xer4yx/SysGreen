# SysGreen

A Windows desktop tool that reduces unnecessary background activity — startup time, idle CPU/disk/network, and telemetry — **safely and reversibly**. The long-term mission spans all of those; the **MVP is scoped to RAM**, specifically the memory consumed by things that auto-start and sit resident after boot.

## Language

**SysGreen**:
The product. A system footprint manager, *not* a memory profiler in the developer sense (it does not inspect a single program's heap).
_Avoid_: memory profiler, RAM cleaner, booster

**Background activity**:
Resource consumption (RAM in the MVP; later CPU, disk, network, telemetry) by software the user is not actively using. The thing SysGreen exists to reduce.

**Reversible**:
Every change SysGreen makes can be undone to restore the prior system state. A hard product constraint, not a feature.

**Manageable Item**:
The single abstraction for anything the user can act on. Each has a `kind`: **StartupApp**, **BackgroundApp** (UWP/Store), **ScheduledTask**, **Service** (strict Windows Service), or **Process** (live runtime instance). The MVP supports **StartupApp + Process** first; Service is a later, higher-risk tier.
_Avoid_: "service" as a catch-all for all of these — reserve "service" for the strict Windows meaning.

**Service host**:
A `svchost.exe` process that bundles several real Windows Services. What Task Manager lists under "Windows processes." You disable the individual Services inside it, not the host itself.

**Process**:
A live runtime instance consuming RAM *right now*. Acted on with **End Task** (instant RAM relief, but it returns next boot). Transient.

**Autostart Entry**:
The configuration (Run key, Startup folder, logon scheduled task, etc.) that *causes* something to launch at boot/logon. Acted on with **Disable/Enable** (changes future boots; frees nothing immediately). Persistent. A Process and an Autostart Entry are linked by app identity (path/publisher).

**Disable / Enable**:
The **primary** MVP verb. Acts on an Autostart Entry to stop/allow it loading at future boots. This is what delivers the persistent RAM win.

**End Task**:
The **secondary** verb. Acts on a Process for instant RAM relief; no lasting effect.

**Knowledge Base**:
The curated, shipped-with-the-app source of truth that maps known publishers/executables to a Purpose and a Safety Rating. Offline-first; no backend in the MVP. When an item isn't in the KB, **heuristic fallback** infers a rough guess from file metadata (signature/publisher, description, install path, updater patterns).
_Avoid_: database (ambiguous), online lookup

**Purpose** (a.k.a. Category):
*What an item is for.* A fixed taxonomy: Gaming, Media, Communication, Productivity, Updater, OEM/Driver, Windows Telemetry, Windows Core, Security/AV, Unknown. Distinct from Safety Rating.

**Safety Rating**:
*How risky it is to disable* an item: **Safe**, **Caution**, **Do-Not-Touch**, **Required-for-Boot**. The axis the recommendation engine relies on. A wrong value here can break a user's machine.
_Avoid_: conflating with Purpose.

**Unknown / Caution default**:
Anything not confidently classified is labeled Purpose=Unknown, Safety=Caution, and is **never auto-recommended** for disabling.

**Recommendation**:
A suggested action (almost always "disable this Autostart Entry") paired with a human-readable reason and the Safety Rating behind it. Produced by combining two sources.

**Static Recommendation**:
Derived from the Knowledge Base alone (Purpose + Safety) — e.g., "telemetry, rated Safe → disable." Covers the invisible wins (telemetry, updaters, bloat) the user never opens. The **primary** source.

**Habit-based Recommendation**:
Derived from observed usage — e.g., "you haven't used this app in 30 days but it auto-starts → disable." Covers user-facing apps only. The **secondary** source.

**Usage**:
The habit signal: **launch recency + frequency** of user-initiated launches. (Foreground-focus time is deliberately excluded from the MVP — it would force always-on monitoring.)

**Tray Agent**:
A deliberately lightweight, resident component that records launch events going forward. Seeded on first run from existing Windows history (UserAssist, Prefetch) to avoid a cold start. All Usage data is stored **local-only**.

**Abandoned**:
An app not launched within the threshold (default **30 days**, user-adjustable). The trigger for a Habit-based Recommendation.

**Elevated Helper**:
A short-lived, admin-elevated process the UI spawns *only* when applying changes that require admin (HKLM autostart, Services, some scheduled tasks). It performs the batch, writes the undo record, and exits. There is **no always-on elevated process**; the UI and Tray Agent run non-elevated.

**Apply**:
The act of committing a batch of pending Disable/Enable changes. Before a batch that touches boot-relevant items, a System Restore Point is created (mandatory). Each change in the batch produces a Change Record.

**Change Record**:
The undo unit. Captures the item, its **exact prior state**, timestamp, and the mechanism used, so the change can be restored precisely. Disabling never deletes — it uses Windows' native disable flags (StartupApproved keys, task Disabled state, service Start type), so it is reversible by construction.

**RAM Estimate**:
The memory figure shown per item, always framed as *"≈ uses at startup,"* never *"saved"* (Windows shares/reclaims memory, so a precise saving can't be promised). Measured as **Private Working Set** (memory unique to the process). Resolved by a degradation chain: live value if running → historical median (Tray Agent) → coarse KB typical → "Unknown."

**Override**:
A user-asserted classification that takes precedence over the Knowledge Base — correcting a wrong Purpose, or flagging "I rely on this, never recommend it." Stored locally and always respected. A clean future signal for improving the KB.

**Passive Background Value**:
A property of some items (e.g., OneDrive sync, backup agents) that do useful work *while running even if the user never opens their window*. Flagged in the KB so the habit engine knows "Abandoned ≠ useless here" and won't wrongly recommend disabling them.
