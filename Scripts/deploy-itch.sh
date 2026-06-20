#!/usr/bin/env bash
# Upload WebGL build to itch.io (requires itch butler CLI + BUTLER_API_KEY or login).
set -euo pipefail

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
BUILD="${ROOT}/Web build"
USER="${ITCH_USER:-nmeidan}"
GAME="${ITCH_GAME:-superinverters}"
CHANNEL="${ITCH_CHANNEL:-web}"

if [[ ! -f "${BUILD}/index.html" ]]; then
  echo "Missing ${BUILD}/index.html — run ./Scripts/build-webgl.sh first."
  exit 1
fi

if ! command -v butler >/dev/null 2>&1; then
  BUTLER="${ROOT}/.tools/butler"
  if [[ ! -x "${BUTLER}" ]]; then
    echo "Install itch butler: https://itch.io/docs/butler/"
    exit 1
  fi
else
  BUTLER="butler"
fi

echo "Pushing ${BUILD} → ${USER}/${GAME}:${CHANNEL}"
"${BUTLER}" push "${BUILD}" "${USER}/${GAME}:${CHANNEL}"
echo "Live at: https://${USER}.itch.io/${GAME}"
