# Reversibility: native disable flags + change log + mandatory restore point

"Reversible" is a hard product constraint, implemented in three layers:

1. **Non-destructive disable.** SysGreen never deletes autostart configuration. It uses Windows' own disable mechanisms — the `...\Explorer\StartupApproved\` flags (the same ones Task Manager writes), disabling (not deleting) scheduled tasks, and setting service Start type to Disabled/Manual. This makes "disable" reversible by construction and keeps SysGreen consistent with what users see in Task Manager.
2. **Precise undo log.** Every change writes a **Change Record** (item + exact prior state + timestamp + mechanism) to the local store, enabling per-item Re-enable and per-batch Undo.
3. **Catastrophe lifeline.** A Windows **System Restore point is created automatically and mandatorily before any batch** that touches HKLM/services/boot-relevant items, because the in-app undo log is unreachable if a change ever prevents boot/login. If System Restore is disabled on the machine, the user is warned prominently before applying.

**Named profiles** ("Gaming mode" / "Work mode") are explicitly **out of MVP scope** — they multiply the state model. End Task (process kill) is not part of the undo model; it is inherently transient (the process returns at next launch/boot).
