#!/usr/bin/env bash
# Driver for the FastCart API (Phase 0+).
# Builds the solution, launches the API on a fixed port, polls until it is
# listening, drives the real HTTP surface (/health, /swagger, swagger.json),
# asserts on the actual response bodies, then tears the server down.
#
# Works from any CWD. Cross-platform: needs only `dotnet`, `bash`, `curl`.
# On Windows run it under Git Bash. Override the port with PORT=xxxx.
#
# Exit 0 = every check passed. Exit 1 = a check failed (server log is dumped).

set -uo pipefail

# --- locate the unit root (three levels up from this script) ---------------
SKILL_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ROOT="$(cd "$SKILL_DIR/../../.." && pwd)"
cd "$ROOT"

PORT="${PORT:-5046}"
BASE="http://localhost:${PORT}"
DLL="src/FastCart.Api/bin/Debug/net10.0/FastCart.Api.dll"
WORK="$(mktemp -d)"
LOG="$WORK/server.log"
APP_PID=""

pass=0; fail=0
ok()   { echo "  PASS: $1"; pass=$((pass+1)); }
bad()  { echo "  FAIL: $1"; fail=$((fail+1)); }

cleanup() {
  if [ -n "$APP_PID" ]; then
    kill "$APP_PID" 2>/dev/null || true
    wait "$APP_PID" 2>/dev/null || true
  fi
  rm -rf "$WORK" 2>/dev/null || true
}
trap cleanup EXIT

# --- build -----------------------------------------------------------------
echo "==> building (dotnet build)"
if ! dotnet build src/FastCart.Api/FastCart.Api.csproj -c Debug --nologo; then
  echo "BUILD FAILED"; exit 1
fi

# --- launch ----------------------------------------------------------------
# Run the built DLL directly (single process — clean to kill). No launch
# profile is applied this way, so ASPNETCORE_URLS is required.
echo "==> launching $DLL on $BASE"
ASPNETCORE_ENVIRONMENT=Development ASPNETCORE_URLS="$BASE" dotnet "$DLL" >"$LOG" 2>&1 &
APP_PID=$!

# --- wait for ready --------------------------------------------------------
up=""
for _ in $(seq 1 40); do
  if curl -fsS -o /dev/null "$BASE/health" 2>/dev/null; then up=1; break; fi
  if ! kill -0 "$APP_PID" 2>/dev/null; then echo "SERVER EXITED EARLY"; cat "$LOG"; exit 1; fi
  sleep 0.5
done
[ -n "$up" ] || { echo "SERVER DID NOT COME UP"; cat "$LOG"; exit 1; }
echo "==> server is up"

# --- drive & assert --------------------------------------------------------
echo "==> GET /health"
code=$(curl -s -o "$WORK/health.json" -w '%{http_code}' "$BASE/health")
[ "$code" = "200" ] && ok "/health -> 200" || bad "/health -> $code (want 200)"
grep -Eq '"success":[[:space:]]*true' "$WORK/health.json" && ok '/health body: success=true' || bad '/health body: success=true'
grep -q 'Healthy' "$WORK/health.json" && ok '/health body: status Healthy' || bad '/health body: status Healthy'

echo "==> GET /swagger (UI)"
code=$(curl -s -o "$WORK/swagger.html" -w '%{http_code}' "$BASE/swagger/index.html")
[ "$code" = "200" ] && ok "/swagger/index.html -> 200" || bad "/swagger/index.html -> $code (want 200)"
grep -qi 'swagger' "$WORK/swagger.html" && ok '/swagger body: is Swagger UI' || bad '/swagger body: is Swagger UI'

echo "==> GET /swagger/v1/swagger.json (OpenAPI doc)"
code=$(curl -s -o "$WORK/swagger.json" -w '%{http_code}' "$BASE/swagger/v1/swagger.json")
[ "$code" = "200" ] && ok "swagger.json -> 200" || bad "swagger.json -> $code (want 200)"
grep -q '"securitySchemes"' "$WORK/swagger.json" && ok 'swagger.json: securitySchemes present' || bad 'swagger.json: securitySchemes present'
grep -Eq '"bearerFormat":[[:space:]]*"JWT"' "$WORK/swagger.json" && ok 'swagger.json: JWT bearer scheme' || bad 'swagger.json: JWT bearer scheme'
grep -q '"/health"' "$WORK/swagger.json" && ok 'swagger.json: /health documented' || bad 'swagger.json: /health documented'

# --- summary ---------------------------------------------------------------
echo
echo "==> $pass passed, $fail failed"
[ "$fail" -eq 0 ] || { echo "---- server log ----"; cat "$LOG"; exit 1; }
echo "ALL CHECKS PASSED"
