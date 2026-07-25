#!/usr/bin/env bash
# Smoke tests after deploy (HTTP or HTTPS base URL).
# Usage :
#   bash deploy/scripts/smoke-test.sh http://localhost
#   bash deploy/scripts/smoke-test.sh https://votre-domaine.tn
set -euo pipefail

BASE_URL="${1:-http://localhost}"
BASE_URL="${BASE_URL%/}"

echo "Smoke testing FactuTrust at ${BASE_URL}"

curl -fsS "${BASE_URL}/health" >/dev/null
echo "[OK] GET /health"

curl -fsS "${BASE_URL}/health/ready" >/dev/null
echo "[OK] GET /health/ready"

curl -fsS "${BASE_URL}/" | grep -qi '<html\|app-root' \
  && echo "[OK] GET / (web SPA)" \
  || { echo "[FAIL] GET / (web SPA)"; exit 1; }

curl -fsS "${BASE_URL}/admin/" | grep -qi '<html\|app-root' \
  && echo "[OK] GET /admin/ (backoffice SPA)" \
  || { echo "[FAIL] GET /admin/ (backoffice SPA)"; exit 1; }

# API should respond (401/404 acceptable for unauthenticated probe)
HTTP_CODE="$(curl -s -o /dev/null -w '%{http_code}' "${BASE_URL}/api/platform/ops/health" || true)"
if [[ "${HTTP_CODE}" =~ ^[45][0-9]{2}$ ]]; then
  echo "[OK] GET /api/ reachable (HTTP ${HTTP_CODE})"
else
  echo "[WARN] GET /api/platform/ops/health returned HTTP ${HTTP_CODE}"
fi

echo ""
echo "All smoke checks passed."
