#!/usr/bin/env bash
# Build WebGL for super-inverters-game (Unity 6.3 LTS).
set -euo pipefail

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
VERSION_FILE="${ROOT}/ProjectSettings/ProjectVersion.txt"
PROJECT_VERSION="$(grep -E '^m_EditorVersion:' "${VERSION_FILE}" | awk '{print $2}')"
UNITY_DEFAULT="/Applications/Unity/Hub/Editor/${PROJECT_VERSION}/Unity.app/Contents/MacOS/Unity"
UNITY_64="/Applications/Unity/Hub/Editor/6000.4.4f1/Unity.app/Contents/MacOS/Unity"

if [[ -n "${UNITY_PATH:-}" ]]; then
  UNITY="${UNITY_PATH}"
elif [[ -x "${UNITY_DEFAULT}" ]]; then
  UNITY="${UNITY_DEFAULT}"
elif [[ -x "${UNITY_64}" ]]; then
  echo "Note: ${PROJECT_VERSION} not installed — falling back to 6000.4.4f1."
  UNITY="${UNITY_64}"
else
  echo "Error: no Unity editor found for project version ${PROJECT_VERSION}." >&2
  exit 1
fi

echo "Using Unity ${PROJECT_VERSION} → ${UNITY}"
OUT="${ROOT}/Web build"
LOG="${ROOT}/Logs/webgl-build.log"

mkdir -p "${ROOT}/Logs" "${OUT}"

echo "Building WebGL → ${OUT}"
echo "Log: ${LOG}"

"${UNITY}" \
  -batchmode -nographics -quit \
  -buildTarget WebGL \
  -projectPath "${ROOT}" \
  -executeMethod EditorTools.WebGLBuildPipeline.BuildWebGL \
  -logFile "${LOG}"

echo "Build finished. Serve locally with: ./Scripts/serve-webgl.sh"
