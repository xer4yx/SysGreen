# Security Policy

## Reporting a vulnerability

**Please do not open a public issue for security vulnerabilities.**

Report privately via GitHub's **[Report a vulnerability](https://github.com/xer4yx/SysGreen/security/advisories/new)**
(Security → Advisories → Report a vulnerability). We aim to acknowledge reports within a few days and
will coordinate a fix and disclosure with you.

When reporting, please include: affected version, steps to reproduce, and the impact you observed.

## Supported versions

SysGreen is pre-1.0; only the latest released version is supported for security fixes.

## Scope notes

SysGreen intentionally changes system state (disabling autostart entries, ending processes, creating
restore points) — **always reversibly**, and only when the user clicks *Apply*. It runs non-elevated
and spawns a short-lived elevated helper only for admin-only changes (see
[ADR-0004](docs/adr/0004-privilege-and-process-model.md)). Reports about these *intended* behaviors
aren't vulnerabilities, but reports about **bypasses** of the consent/reversibility/least-privilege
model very much are.
