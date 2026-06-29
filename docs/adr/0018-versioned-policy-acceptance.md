# Policy acceptance is gated on policy version, not install events

SysGreen requires the user to accept its terms/privacy policy, and we re-prompt only when the **policy version** the user accepted is older than the current one — rather than on every install, reinstall, or update. The accepted version is stored in the (now surviving — ADR-0016) Data Store. This single rule covers every case without per-event special-casing: a fresh or wiped-data install has no accepted version and is prompted; an existing user's first update after this ships is prompted (the field is new); a routine update with an unchanged policy is not; and a *keep-data* reinstall at the same version is not — re-prompting there would contradict the data the user deliberately chose to keep.

## Consequences

- A keep-data reinstall at the same policy version does **not** re-show the policy. This is deliberate: gating on version (not events) keeps the prompt meaningful and avoids training users to click "Accept" blindly. If a legal requirement ever demands re-acceptance regardless of version, bump the policy version.
- The first-run onboarding screen evolves into the acceptance gate (must accept to proceed) alongside the existing launch-tracking consent toggle; a later version-bump re-prompt shows only the policy and does not reset the tracking preference.
