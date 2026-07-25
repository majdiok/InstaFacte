#!/usr/bin/env bash
# Déploie la stack FactuTrust lab sur la VM (à exécuter dans /opt/factutrust/src).
# Usage :
#   cd /opt/factutrust/src
#   bash deploy/scripts/deploy-lab.sh
#
# Si l'image ollama/ollama:latest est absente (Docker Hub lent), active
# automatiquement docker-compose.lab-host-ollama.yml (Ollama installé sur l'hôte).
# Forcer : USE_HOST_OLLAMA=1|0
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "${SCRIPT_DIR}/../.." && pwd)"
cd "${REPO_ROOT}"

COMPOSE_ARGS=(-f deploy/docker-compose.yml -f deploy/docker-compose.lab.yml)
USE_HOST_OLLAMA="${USE_HOST_OLLAMA:-}"

if [[ -z "${USE_HOST_OLLAMA}" ]]; then
  if [[ -f deploy/docker-compose.lab-host-ollama.yml ]] \
    && ! docker image inspect ollama/ollama:latest >/dev/null 2>&1; then
    USE_HOST_OLLAMA=1
  else
    USE_HOST_OLLAMA=0
  fi
fi

if [[ "${USE_HOST_OLLAMA}" == "1" ]]; then
  COMPOSE_ARGS+=(-f deploy/docker-compose.lab-host-ollama.yml)
  echo "=== Using host Ollama (docker-compose.lab-host-ollama.yml) ==="
  if ! command -v ollama >/dev/null 2>&1; then
    echo "WARN: ollama CLI not on host — IA unavailable until:"
    echo "  bash deploy/scripts/install-host-ollama.sh"
  else
    systemctl start ollama >/dev/null 2>&1 || sudo systemctl start ollama >/dev/null 2>&1 || true
  fi
fi

compose() {
  docker compose "${COMPOSE_ARGS[@]}" "$@"
}

if [[ ! -f deploy/.env ]]; then
  if [[ -f deploy/.env.lab.example ]]; then
    cp deploy/.env.lab.example deploy/.env
    echo "Created deploy/.env from .env.lab.example — edit IP_VM in AllowedOrigins if needed."
  else
    echo "Missing deploy/.env — run init-secrets first."
    exit 1
  fi
fi

bash deploy/scripts/init-secrets.sh
bash deploy/scripts/use-http-nginx.sh

echo "=== Building images (20–45 min first time) ==="
compose build

echo "=== Starting stack ==="
compose up -d

# Nginx caches upstream IPs; recreate of api can leave stale DNS → 502
compose restart nginx >/dev/null 2>&1 || true

echo "Waiting for API..."
for i in $(seq 1 90); do
  if compose exec -T api curl -fsS http://localhost:8080/health/ready >/dev/null 2>&1; then
    echo "API ready."
    break
  fi
  sleep 5
  if [[ "${i}" -eq 90 ]]; then
    echo "API timeout — logs:"
    compose logs api --tail 80
    exit 1
  fi
done

if [[ "${USE_HOST_OLLAMA}" == "1" ]]; then
  if command -v ollama >/dev/null 2>&1; then
    echo "=== Pulling lab models via host ollama ==="
    bash deploy/scripts/pull-ollama-models-lab-host.sh || echo "WARN: host model pull failed"
  else
    echo "SKIP model pull — install host ollama first (deploy/scripts/install-host-ollama.sh)"
  fi
else
  bash deploy/scripts/pull-ollama-models-lab.sh
fi

BASE_URL="${SMOKE_BASE_URL:-http://localhost}"
bash deploy/scripts/smoke-test.sh "${BASE_URL}"

echo ""
echo "=== Lab deploy complete ==="
echo "Web:        ${BASE_URL}/"
echo "Backoffice: ${BASE_URL}/admin/"
grep '^Bootstrap__PlatformAdmin__Email=' deploy/.env || true
echo "(Bootstrap password in deploy/.env — change after first login)"
