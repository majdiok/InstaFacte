#!/usr/bin/env bash
set -euo pipefail
cd /opt/factutrust/src
find deploy/scripts -name '*.sh' -exec dos2unix {} \;
if [[ ! -f deploy/.env ]]; then
  cp deploy/.env.lab.example deploy/.env
fi
sed -i 's#http://IP_VM#http://192.168.179.100#g' deploy/.env
grep -q '192.168.179.100' deploy/.env || true
# Prefer sg docker; fallback sudo
if sg docker -c 'docker info' >/dev/null 2>&1; then
  sg docker -c 'bash deploy/scripts/deploy-lab.sh'
else
  sudo bash deploy/scripts/deploy-lab.sh
fi
