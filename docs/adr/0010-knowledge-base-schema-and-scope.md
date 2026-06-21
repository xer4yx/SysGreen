# Knowledge Base: publisher-based matching, entry schema, curated scope

**Matching.** A KB entry recognizes a real item by **Authenticode publisher (signing cert subject) + executable name**, with optional path / registry-value hints. Filename-only matching was rejected — it is spoofable and collision-prone (`updater.exe`). Publisher-based matching resists spoofing and groups a vendor's many binaries.

**Entry schema:** match criteria; display name; friendly description; Purpose (fixed taxonomy); Safety Rating; typical RAM (coarse, for the estimate fallback); a **Provides-passive-value flag** (so the habit engine doesn't recommend disabling background-useful apps like OneDrive); and KB schema-version + data-version.

**Trustworthiness.** Safety ratings use a **conservative bias — when unsure, rate Caution, never Safe.** The MVP is also structurally low-risk: it acts only on autostart entries + processes, where "disable" means "won't preload" (the app still launches on demand); the genuinely dangerous ratings live in the deferred Services tier.

**Scope.** MVP coverage is a **curated ~100–300 high-frequency entries** (common startup apps, updaters, Office/Windows telemetry, common OEM helpers) — quality over breadth. The long tail falls through to heuristics → Unknown/Caution. Exhaustive coverage is explicitly a non-goal for the MVP.
