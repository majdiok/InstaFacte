#!/usr/bin/env bash
# Release : rebuild images, refresh static assets, restart API, smoke test.
# Usage (depuis la racine du dépôt) :
#   bash deploy/scripts/release.sh
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "${SCRIPT_DIR}/../.." && pwd)"
COMPOSE_FILE="${COMPOSE_FILE:-deploy/docker-compose.yml}"
SMOKE_URL="${SMOKE_URL:-https://localhost}"

cd "${REPO_ROOT}"

if [[ ! -f deploy/.env ]]; then
  echo "Missing deploy/.env — run: bash deploy/scripts/init-secrets.sh"
  exit 1
fi

echo "=== Pull latest (optional) ==="
git pull --ff-only 2>/dev/null || echo "Skip git pull (not a git repo or offline)"

echo "=== Rebuild static frontends ==="
docker compose -f "${COMPOSE_FILE}" build web-static-init admin-static-init
docker compose -f "${COMPOSE_FILE}" run --rm web-static-init
docker compose -f "${COMPOSE_FILE}" run --rm admin-static-init

echo "=== Rebuild & restart API ==="
docker compose -f "${COMPOSE_FILE}" build api
docker compose -f "${COMPOSE_FILE}" up -d sqlserver ollama api

echo "Waiting for API health..."
for i in $(seq 1 60); do
  if docker compose -f "${COMPOSE_FILE}" exec -T api curl -fsS http://localhost:8080/health/ready >/dev/null 2>&1; then
    echo "API ready."
    break
  fi
  sleep 5
  if [[ "${i}" -eq 60 ]]; then
    echo "API health timeout — check logs: docker compose -f ${COMPOSE_FILE} logs api"
    exit 1
  fi
done

echo "=== Restart nginx ==="
docker compose -f "${COMPOSE_FILE}" up -d nginx

echo "=== Smoke test ==="
bash deploy/scripts/smoke-test.sh "${SMOKE_URL}" || true

echo ""
echo "=== POST-RELEASE REMINDER ==="
echo "If this release includes EF tenant migrations, apply them via backoffice:"
echo "  POST /api/platform/migrations/tenants/apply-migrations"
echo "See docs/backend-tenant-migrations.md"
