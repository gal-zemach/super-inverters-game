#!/usr/bin/env bash
# Serve the WebGL build locally for two-tab multiplayer testing.
# Decompresses Unity .gz build artifacts on the fly so script-tag loading works
# in all browsers (including embedded WebViews that ignore Content-Encoding).
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
export PORT

python3 - <<'PY'
import gzip
import http.server
import os
import socketserver

PORT = int(os.environ.get("PORT", "8080"))

class UnityWebGLHandler(http.server.SimpleHTTPRequestHandler):
    def guess_type(self, path):
        if path.endswith(".js.gz"):
            return "application/javascript"
        if path.endswith(".wasm.gz"):
            return "application/wasm"
        if path.endswith(".data.gz"):
            return "application/octet-stream"
        return super().guess_type(path)

    def do_GET(self):
        path = self.translate_path(self.path.split("?", 1)[0])
        if path.endswith(".gz") and os.path.isfile(path):
            with open(path, "rb") as handle:
                data = gzip.decompress(handle.read())
            self.send_response(200)
            content_type = self.guess_type(path)
            self.send_header("Content-Type", content_type)
            self.send_header("Content-Length", str(len(data)))
            self.send_header("Cache-Control", "no-store")
            self.end_headers()
            self.wfile.write(data)
            return
        return super().do_GET()

Handler = UnityWebGLHandler
socketserver.TCPServer.allow_reuse_address = True
with socketserver.TCPServer(("127.0.0.1", PORT), Handler) as httpd:
    print(f"Serving Unity WebGL build on http://127.0.0.1:{PORT}/")
    httpd.serve_forever()
PY
