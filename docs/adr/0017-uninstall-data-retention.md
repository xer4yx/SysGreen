# Uninstall keeps the Data Store by default, with an explicit user choice

Because the Data Store now lives outside Velopack's install root (ADR-0016), uninstall no longer deletes it — so the default is **keep**, which is exactly what makes "turn it back on after reinstall" work. To respect privacy we still let the user choose to wipe: an **in-app "Uninstall SysGreen" action** presents a keep/delete choice in a surface we control, then launches the Velopack uninstaller. For OS-driven uninstalls (Windows "Apps & features"), which we cannot reliably interrupt with a dialog, a stored **uninstall-retention preference** (default keep) is honored by the Velopack uninstall hook. An in-app **"Reset my data"** action gives a clean-slate path at any time.

## Considered options

- *Interactive dialog from the Velopack uninstall hook* — rejected as the primary path: the hook is a fast, non-interactive callback and a blocking dialog there is fragile across uninstall surfaces. It survives only as the stored-preference fallback.
- *Always wipe on uninstall* — rejected: it is the original bug.
