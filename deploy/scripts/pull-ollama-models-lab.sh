#!/usr/bin/env bash
# Modèles Ollama pour lab VMware 16 Go — phase 1 : 3B uniquement.
# Phase 2 optionnelle : décommenter MODELS_PHASE2 si free -h > 4 Go disponibles.
# Usage :
#   bash deploy/scripts/pull-ollama-models-lab.sh
set -euo pipefail

COMPOSE_FILE="${COMPOSE_FILE:-deploy/docker-compose.yml}"
LAB_FILE="${LAB_FILE:-deploy/docker-compose.lab.yml}"

MODELS_PHASE1=(
  "qwen2.5:3b-instruct"
)

MODELS_PHASE2=(
  "qwen2.5:7b-instruct"
  "llava"
)

compose() {
  docker compose -f "${COMPOSE_FILE}" -f "${LAB_FILE}" "$@"
}

echo "=== Lab Ollama pull (phase 1 — 3B) ==="
for model in "${MODELS_PHASE1[@]}"; do
  echo "--- ${model} ---"
  compose exec -T ollama ollama pull "${model}"
done

AVAIL_KB="$(grep MemAvailable /proc/meminfo | awk '{print $2}')"
AVAIL_MB=$((AVAIL_KB / 1024))
echo "RAM available: ~${AVAIL_MB} MB"

if [[ "${AVAIL_MB}" -ge 4096 ]]; then
  echo "=== Phase 2 (RAM suffisante) — modèles additionnels ==="
  for model in "${MODELS_PHASE2[@]}"; do
    echo "--- ${model} ---"
    compose exec -T ollama ollama pull "${model}" || echo "WARN: pull ${model} failed (optional)"
  done
else
  echo "Skip phase 2 (7B/llava) — RAM < 4 Go libres. Relancer après libération mémoire."
fi

compose exec ollama ollama list
