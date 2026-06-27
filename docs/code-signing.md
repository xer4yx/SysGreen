# Code signing (SignPath Foundation)

SysGreen's installer is currently **unsigned**, which triggers SmartScreen "Unknown publisher"
warnings (ADR-0009). The plan is to sign releases for free via the **[SignPath Foundation](https://signpath.org)**
open-source program. This document is the enrolment checklist and the CI wiring to switch on once
approved.

> **Publisher note:** with SignPath Foundation, the certificate is **issued to SignPath Foundation**,
> so the signature's publisher reads *"SignPath Foundation"*, not "SysGreen". The private key lives on
> SignPath's HSM — we never hold it. Every release is **manually approved** before signing (which fits
> our draft-release / manual-publish flow).
>
> **SmartScreen reality:** an OV certificate (which this is) does **not** grant instant SmartScreen
> trust — reputation builds per file hash over time. Signing removes "Unknown publisher" and the
> tamper warning, but early downloads may still warn until reputation accrues.

## Eligibility checklist (SignPath Foundation terms)

| Requirement | Status |
| --- | --- |
| OSI-approved license, no commercial dual-licensing, no proprietary components | ✅ Apache-2.0 ([LICENSE](../LICENSE)) |
| Publicly accessible repository | ⛔ **TODO — repo is private; make it public** |
| Actively maintained | ✅ |
| Already released in the form to be signed | ⛔ **TODO — publish a GitHub Release (e.g. `v0.20.0`) first** |
| Functionality documented on the download/release page | ✅ [README](../README.md) |
| No malware / potentially-unwanted-program; includes uninstall | ✅ reversible by design; Inno uninstaller. ⚠️ *a footprint/telemetry tool can draw extra review — the transparency/consent/reversibility story (ADR-0004/0012/0014) is the answer* |
| Binary metadata (product, version) set | ✅ `Directory.Build.props` (`Product`, `Version`) |
| **Code signing policy** published on homepage + release pages, with the required wording | ✅ [README → Code signing policy](../README.md#code-signing-policy) |

### Required wording (already in the README)

> Free code signing provided by SignPath.io, certificate by SignPath Foundation.

Plus the privacy statement and the Committers/Reviewers/Approvers role lists — see the README section.

## What the maintainer must do (one-time, external)

1. **Make the repository public** (Settings → General → Danger Zone → Change visibility). Required by
   SignPath *and* a precondition for free CodeQL / dependency-review (see below).
2. **Publish a GitHub Release** (run the Installer workflow on a `v*` tag, then publish the draft).
3. **Apply** at <https://signpath.org/apply>. After approval, SignPath creates your *organization*,
   *project*, and a *signing policy* (e.g. `release-signing`), and issues an **API token**.
4. Add these to the GitHub repo:
   - **Secret** `SIGNPATH_API_TOKEN` — the SignPath API token.
   - **Variables** `SIGNPATH_ORGANIZATION_ID`, `SIGNPATH_PROJECT_SLUG`, `SIGNPATH_POLICY_SLUG`.
   - **Variable** `ENABLE_CODE_SCANNING = true` (turns on CodeQL + dependency-review once public).

## CI wiring to enable

Distribution is **Velopack** now (ADR-0009), so signing integrates with the **packaging** step rather
than a standalone installer-signing step. With SignPath Foundation there are two shapes; pick one when
enrolled (both no-op until the `SIGNPATH_*` variables are set):

1. **Sign during `vpk pack`** — Velopack signs each PE file as it packages, via a sign template that
   shells out to SignPath's signing CLI. In `installer/build.ps1`, add to the `vpk pack` args (guarded
   on the env vars):
   ```
   --signTemplate "signpath-cli sign --organization-id $env:SIGNPATH_ORGANIZATION_ID ... {{file}}"
   ```
   `{{file}}` is the placeholder Velopack substitutes per file.

2. **Submit the packed artifacts after pack** — keep `vpk pack` unsigned, then submit the produced
   `artifacts/releases/*` to SignPath with the
   [signpath/github-action-submit-signing-request](https://github.com/signpath/github-action-submit-signing-request)
   action and re-attach the signed outputs to the release.

> Confirm the exact flags against the current Velopack and SignPath docs before enabling — both revise
> their CLIs occasionally. The `SIGNPATH_*` secret/variables and the gate are the same either way.

## Alternatives (if SignPath Foundation doesn't fit)

- **Azure Artifact Signing / Trusted Signing** — ~$10/month, CI-friendly, signs the same unpackaged
  installer; reputation builds over time. (US/Canada/EU/UK only.)
- **OV certificate** — $150–300/yr, hardware token. Skip **EV**: since 2024 it no longer bypasses
  SmartScreen, so it isn't worth the premium. See [ADR-0009](adr/0009-distribution-and-signing.md).
