#!/usr/bin/env bash
# Génère secrets, certificat DataProtection et certificats TLS bootstrap pour Nginx.
# Usage (depuis la racine du dépôt) :
#   bash deploy/scripts/init-secrets.sh
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
DEPLOY_DIR="$(cd "${SCRIPT_DIR}/.." && pwd)"
REPO_ROOT="$(cd "${DEPLOY_DIR}/.." && pwd)"
CERTS_DIR="${DEPLOY_DIR}/certs"
NGINX_CERTS_DIR="${DEPLOY_DIR}/nginx/certs"
ENV_FILE="${DEPLOY_DIR}/.env"
ENV_EXAMPLE="${DEPLOY_DIR}/.env.example"

mkdir -p "${CERTS_DIR}" "${NGINX_CERTS_DIR}"

if [[ ! -f "${ENV_FILE}" ]]; then
  cp "${ENV_EXAMPLE}" "${ENV_FILE}"
  echo "Created ${ENV_FILE} from example."
fi

# --- SQL SA password ---
if ! grep -q '^MSSQL_SA_PASSWORD=.\+' "${ENV_FILE}" 2>/dev/null || grep -q 'ChangeMe!Str0ng_Sql_Password' "${ENV_FILE}"; then
  SA_PASS="$(openssl rand -base64 24 | tr -d '/+=' | head -c 20)Aa1!"
  if grep -q '^MSSQL_SA_PASSWORD=' "${ENV_FILE}"; then
    sed -i.bak "s|^MSSQL_SA_PASSWORD=.*|MSSQL_SA_PASSWORD=${SA_PASS}|" "${ENV_FILE}"
    rm -f "${ENV_FILE}.bak"
  else
    echo "MSSQL_SA_PASSWORD=${SA_PASS}" >> "${ENV_FILE}"
  fi
  echo "Generated MSSQL_SA_PASSWORD."
fi

# --- JWT secret (>= 32 bytes) ---
if ! grep -q '^JwtSettings__SecretKey=.\{32,\}' "${ENV_FILE}" 2>/dev/null; then
  JWT_SECRET="$(openssl rand -base64 48)"
  if grep -q '^JwtSettings__SecretKey=' "${ENV_FILE}"; then
    sed -i.bak "s|^JwtSettings__SecretKey=.*|JwtSettings__SecretKey=${JWT_SECRET}|" "${ENV_FILE}"
    rm -f "${ENV_FILE}.bak"
  else
    echo "JwtSettings__SecretKey=${JWT_SECRET}" >> "${ENV_FILE}"
  fi
  echo "Generated JwtSettings__SecretKey."
fi

# --- DataProtection PFX ---
PFX_PATH="${CERTS_DIR}/dataprotection.pfx"

if [[ ! -f "${PFX_PATH}" ]]; then
  DP_PASS="$(openssl rand -base64 24 | tr -d '/+=' | head -c 24)"
  openssl req -x509 -newkey rsa:4096 -sha256 -days 3650 -nodes \
    -keyout "${CERTS_DIR}/dataprotection.key" \
    -out "${CERTS_DIR}/dataprotection.crt" \
    -subj "/CN=FactuTrust-DataProtection/O=FactuTrust"
  openssl pkcs12 -export \
    -out "${PFX_PATH}" \
    -inkey "${CERTS_DIR}/dataprotection.key" \
    -in "${CERTS_DIR}/dataprotection.crt" \
    -passout "pass:${DP_PASS}"
  chmod 600 "${PFX_PATH}" "${CERTS_DIR}/dataprotection.key"
  echo "Created DataProtection certificate: ${PFX_PATH}"

  if grep -q '^DataProtection__CertificatePassword=' "${ENV_FILE}"; then
    sed -i.bak "s|^DataProtection__CertificatePassword=.*|DataProtection__CertificatePassword=${DP_PASS}|" "${ENV_FILE}"
    rm -f "${ENV_FILE}.bak"
  else
    echo "DataProtection__CertificatePassword=${DP_PASS}" >> "${ENV_FILE}"
  fi
else
  echo "DataProtection PFX already exists — skipping (password unchanged)."
fi

# --- Platform admin bootstrap password (if empty; tolerate CRLF from Windows sync) ---
if grep -qE '^Bootstrap__PlatformAdmin__Password=[[:space:]]*$' "${ENV_FILE}" 2>/dev/null; then
  ADMIN_PASS="$(openssl rand -base64 18 | tr -d '/+=' | head -c 16)Aa1!"
  sed -i.bak "s|^Bootstrap__PlatformAdmin__Password=.*|Bootstrap__PlatformAdmin__Password=${ADMIN_PASS}|" "${ENV_FILE}"
  # Normalize CRLF so docker env_file / grep behave consistently
  sed -i.bak 's/\r$//' "${ENV_FILE}"
  rm -f "${ENV_FILE}.bak"
  echo "Generated Bootstrap__PlatformAdmin__Password (save it securely)."
fi

# --- Nginx self-signed TLS (bootstrap) ---
if [[ ! -f "${NGINX_CERTS_DIR}/fullchain.pem" ]]; then
  openssl req -x509 -nodes -newkey rsa:2048 -days 825 \
    -keyout "${NGINX_CERTS_DIR}/privkey.pem" \
    -out "${NGINX_CERTS_DIR}/fullchain.pem" \
    -subj "/CN=${FACTUTRUST_DOMAIN:-localhost}/O=FactuTrust"
  chmod 644 "${NGINX_CERTS_DIR}/fullchain.pem"
  chmod 600 "${NGINX_CERTS_DIR}/privkey.pem"
  echo "Created self-signed Nginx TLS certs in ${NGINX_CERTS_DIR}"
  echo "Replace with Let's Encrypt certs before public production traffic."
fi

echo ""
echo "=== init-secrets complete ==="
echo "Edit ${ENV_FILE} : FACTUTRUST_DOMAIN, AllowedOrigins, App__FrontendBaseUrl, SMTP, admin email."
echo "BACKUP ${PFX_PATH} — required to decrypt tenant connection strings after redeploy."
