#!/usr/bin/env bash
# Serve the WebGL build locally for two-tab multiplayer testing.
set -euo pipefail

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
BUILD="${ROOT}/Web build"
PORT="${PORT:-8080}"

if [[ ! -f "${BUILD}/index.html" ]]; then
  echo "Missing ${BUILD}/index.html — run ./Scripts/build-webgl.sh first."
  exit 1
fi

echo "Serving ${BUILD} at http://127.0.0.1:${PORT}/"
echo "Open two browser tabs to test multiplayer."
cd "${BUILD}"
python3 -m http.server "${PORT}"
