#!/usr/bin/env bash
# Pull lab Ollama models via host-installed ollama (not the Docker container).
# Usage:
#   bash deploy/scripts/pull-ollama-models-lab-host.sh
set -euo pipefail

if ! command -v ollama >/dev/null 2>&1; then
  echo "ollama not installed. Run: bash deploy/scripts/install-host-ollama.sh"
  exit 1
fi

MODELS_PHASE1=(
  "qwen2.5:3b-instruct"
)

MODELS_PHASE2=(
  "qwen2.5:7b-instruct"
  "llava"
)

echo "=== Lab Ollama pull via host (phase 1 — 3B) ==="
for model in "${MODELS_PHASE1[@]}"; do
  echo "--- ${model} ---"
  ollama pull "${model}"
done

AVAIL_KB="$(grep MemAvailable /proc/meminfo | awk '{print $2}')"
AVAIL_MB=$((AVAIL_KB / 1024))
echo "RAM available: ~${AVAIL_MB} MB"

if [[ "${AVAIL_MB}" -ge 4096 ]]; then
  echo "=== Phase 2 (RAM suffisante) — modèles additionnels ==="
  for model in "${MODELS_PHASE2[@]}"; do
    echo "--- ${model} ---"
    ollama pull "${model}" || echo "WARN: pull ${model} failed (optional)"
  done
else
  echo "Skip phase 2 (7B/llava) — RAM < 4 Go libres."
fi

ollama list
