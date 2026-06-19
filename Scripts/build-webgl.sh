#!/usr/bin/env bash
# Build WebGL for super-inverters-game (Unity 6.3 LTS).
set -euo pipefail

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
UNITY_63="/Applications/Unity/Hub/Editor/6000.3.16f1/Unity.app/Contents/MacOS/Unity"
UNITY_64="/Applications/Unity/Hub/Editor/6000.4.4f1/Unity.app/Contents/MacOS/Unity"
if [[ -n "${UNITY_PATH:-}" ]]; then
  UNITY="${UNITY_PATH}"
elif [[ -d "/Applications/Unity/Hub/Editor/6000.3.16f1/PlaybackEngines/WebGLSupport" ]]; then
  UNITY="${UNITY_63}"
elif [[ -x "${UNITY_64}" ]]; then
  echo "Note: WebGL module not found on 6000.3.16f1 — using 6000.4.4f1 for this build."
  UNITY="${UNITY_64}"
else
  UNITY="${UNITY_63}"
fi
OUT="${ROOT}/Web build"
LOG="${ROOT}/Logs/webgl-build.log"

mkdir -p "${ROOT}/Logs" "${OUT}"

echo "Building WebGL → ${OUT}"
echo "Log: ${LOG}"

"${UNITY}" \
  -batchmode -nographics -quit \
  -projectPath "${ROOT}" \
  -executeMethod EditorTools.WebGLBuildPipeline.BuildWebGL \
  -logFile "${LOG}"

echo "Build finished. Serve locally with: ./Scripts/serve-webgl.sh"
