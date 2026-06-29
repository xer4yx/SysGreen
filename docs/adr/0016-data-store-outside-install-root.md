# Data Store lives outside the Velopack install root

Velopack installs per-user into `%LocalAppData%\SysGreen\` (the `--packId`) and its uninstaller deletes that tree — which today also holds `sysgreen.db`, so an uninstall (and even a reinstall) silently destroys the user's history, Usage, and Overrides. We move the mutable **Data Store** (Usage, Change Records, Overrides, settings, Policy Acceptance) to a sibling directory Velopack does not manage (e.g. `%LocalAppData%\xer4yx\SysGreen\`), so its lifetime is the *user's*, not the *install's*. The shipped Knowledge Base stays with the install (it is read-only and versioned in source).

## Consequences

- **One-time migration is mandatory.** On startup, if the new-location DB is absent but an old-location one exists, move it (with `-wal`/`-shm` sidecars) before bootstrapping. Without this, existing users lose history on the *update* that ships this change, not just on uninstall. If both locations exist, the new one wins (never overwrite live data); if the move fails, fall back to copy and tolerate a leftover.
- This is genuinely hard to reverse once users have data at the new path, which is why it is recorded here.
