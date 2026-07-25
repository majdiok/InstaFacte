#!/usr/bin/env bash
# Active la configuration Nginx HTTP-only pour les tests lab VMware (sans TLS).
# Usage (depuis la racine du dépôt) :
#   bash deploy/scripts/use-http-nginx.sh
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
CONF_DIR="$(cd "${SCRIPT_DIR}/../nginx/conf.d" && pwd)"

SSL_CONF="${CONF_DIR}/factutrust.conf"
HTTP_EXAMPLE="${CONF_DIR}/factutrust-http-only.conf.example"
HTTP_ACTIVE="${CONF_DIR}/factutrust-http-only.conf"
SSL_BACKUP="${CONF_DIR}/factutrust-ssl.conf.disabled"

if [[ ! -f "${HTTP_EXAMPLE}" ]]; then
  echo "Missing ${HTTP_EXAMPLE}"
  exit 1
fi

if [[ -f "${SSL_CONF}" ]] && ! grep -q "listen 443 ssl" "${SSL_CONF}" 2>/dev/null; then
  echo "Nginx already in HTTP-only mode (${SSL_CONF})."
  exit 0
fi

if [[ -f "${SSL_CONF}" ]]; then
  mv "${SSL_CONF}" "${SSL_BACKUP}"
  echo "Disabled SSL config → ${SSL_BACKUP}"
fi

rm -f "${HTTP_ACTIVE}"
cp "${HTTP_EXAMPLE}" "${SSL_CONF}"
echo "HTTP-only Nginx active : ${SSL_CONF}"
echo "Restart nginx if stack is running:"
echo "  docker compose -f deploy/docker-compose.yml -f deploy/docker-compose.lab.yml restart nginx"
