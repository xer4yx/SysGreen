#!/usr/bin/env bash
#
# Rejects compiled binaries / executables added or changed in a pull request.
# Two checks per file: (1) a denylist of binary/executable extensions, and (2) executable "magic
# bytes" regardless of extension (catches a .exe renamed to .png). Allowlisted known-good binary
# assets (icons/images under assets/) are permitted. Usage: binary-guard.sh <base-ref>
set -euo pipefail

BASE="${1:?usage: binary-guard.sh <base-ref>}"

# Known-good binary files allowed to be added/changed (the app icon and image assets).
ALLOW_REGEX='^assets/.*\.(ico|png|jpg|jpeg|gif|svg|webp)$'

# Denied by extension: compiled executables, libraries, installers, disk/archive payloads.
DENY_EXT_REGEX='\.(exe|dll|so|dylib|msi|msix|appx|appxbundle|com|scr|sys|drv|cpl|ocx|jar|apk|node|a|lib|o|obj|bin|msp|msu|cab|iso|img|vhd|vhdx|pdb)$'

mapfile -t files < <(git diff --name-only --diff-filter=AM "${BASE}...HEAD")

violations=()
for f in "${files[@]}"; do
  [[ -z "$f" || ! -f "$f" ]] && continue
  if [[ "$f" =~ $ALLOW_REGEX ]]; then continue; fi

  # 1) denied extension
  lower="${f,,}"
  if [[ "$lower" =~ $DENY_EXT_REGEX ]]; then
    violations+=("$f — disallowed binary/executable extension")
    continue
  fi

  # 2) executable magic bytes regardless of extension
  magic="$(head -c 4 "$f" | xxd -p 2>/dev/null || true)"
  case "$magic" in
    4d5a*)                                   violations+=("$f — Windows PE/EXE signature (MZ)") ;;
    7f454c46)                                violations+=("$f — ELF executable signature") ;;
    feedface|feedfacf|cefaedfe|cffaedfe|cafebabe|cafebabf)
                                             violations+=("$f — Mach-O executable signature") ;;
  esac
done

if (( ${#violations[@]} > 0 )); then
  echo "::error::Binary guard blocked this PR — compiled binaries/executables are not permitted in source."
  printf '  • %s\n' "${violations[@]}"
  echo ""
  echo "If a binary is genuinely required, add it to the allowlist in .github/scripts/binary-guard.sh"
  echo "and justify it in the PR description so a maintainer can review."
  exit 1
fi

echo "Binary guard: no new binaries or executables detected. OK."
