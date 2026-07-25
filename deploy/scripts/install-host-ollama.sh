#!/bin/bash
# Install Ollama on the host (not Docker) for slow Docker Hub / limited disk labs.
set -euo pipefail

if command -v ollama >/dev/null 2>&1; then
  echo "Ollama already installed: $(ollama --version 2>/dev/null || true)"
else
  echo "Installing Ollama..."
  curl -fsSL https://ollama.com/install.sh | sh
fi

sudo systemctl enable ollama >/dev/null 2>&1 || true
sudo systemctl restart ollama
sleep 2
sudo systemctl --no-pager --full status ollama | head -20

# Listen on all interfaces so Docker bridge (host.docker.internal) can reach it
if ! grep -q 'OLLAMA_HOST' /etc/systemd/system/ollama.service.d/override.conf 2>/dev/null; then
  sudo mkdir -p /etc/systemd/system/ollama.service.d
  sudo tee /etc/systemd/system/ollama.service.d/override.conf >/dev/null <<'EOF'
[Service]
Environment="OLLAMA_HOST=0.0.0.0:11434"
Environment="OLLAMA_MAX_LOADED_MODELS=1"
Environment="OLLAMA_NUM_PARALLEL=1"
EOF
  sudo systemctl daemon-reload
  sudo systemctl restart ollama
fi

curl -fsS --max-time 5 http://127.0.0.1:11434/api/tags >/dev/null
echo "Host Ollama is ready on 0.0.0.0:11434"
