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

| Requirement | Status                                                                                                                                                                                |
| --- |---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|
| OSI-approved license, no commercial dual-licensing, no proprietary components | ✅ Apache-2.0 ([LICENSE](../LICENSE))                                                                                                                                                  |
| Publicly accessible repository | ✅                                                                                                                                                                                     |
| Actively maintained | ✅                                                                                                                                                                                     |
| Already released in the form to be signed | ✅ [Releases](https://github.com/xer4yx/SysGreen/releases)                                                                                                                             | 
| Functionality documented on the download/release page | ✅ [README](../README.md)                                                                                                                                                              |
| No malware / potentially-unwanted-program; includes uninstall | ✅ reversible by design; Velopack uninstaller. ⚠️ *a footprint/telemetry tool can draw extra review — the transparency/consent/reversibility story (ADR-0004/0012/0014) is the answer* |
| Binary metadata (product, version) set | ✅ `Directory.Build.props` (`Product`, `Version`)                                                                                                                                      |
| **Code signing policy** published on homepage + release pages, with the required wording | ✅ [README → Code signing policy](../README.md#code-signing-policy)                                                                                                                    |

### Required wording (already in the README)

> Free code signing provided by SignPath.io, certificate by SignPath Foundation.

Plus the privacy statement and the Committers/Reviewers/Approvers role lists — see the README section.

## Alternatives (if SignPath Foundation doesn't fit)

- **Azure Artifact Signing / Trusted Signing** — ~$10/month, CI-friendly, signs the same unpackaged
  installer; reputation builds over time. (US/Canada/EU/UK only.)
- **OV certificate** — $150–300/yr, hardware token. Skip **EV**: since 2024 it no longer bypasses
  SmartScreen, so it isn't worth the premium. See [ADR-0009](adr/0009-distribution-and-signing.md).
