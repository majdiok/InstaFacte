#!/usr/bin/env bash
# Télécharge les modèles Ollama requis par FactuTrust (appsettings Ollama section).
# Usage :
#   bash deploy/scripts/pull-ollama-models.sh
set -euo pipefail

COMPOSE_FILE="${COMPOSE_FILE:-deploy/docker-compose.yml}"
MODELS=(
  "qwen2.5:3b-instruct"
  "qwen2.5:7b-instruct"
  "llava"
)

echo "Pulling Ollama models into factutrust-ollama container..."
for model in "${MODELS[@]}"; do
  echo "--- ${model} ---"
  docker compose -f "${COMPOSE_FILE}" exec -T ollama ollama pull "${model}"
done

echo "Done. Verify: docker compose -f ${COMPOSE_FILE} exec ollama ollama list"
