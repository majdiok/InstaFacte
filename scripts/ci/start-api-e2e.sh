#!/usr/bin/env bash
# Démarre FactuTrust.API en arrière-plan pour les smoke tests Playwright (CI Linux).
set -euo pipefail

ROOT="$(cd "$(dirname "$0")/../.." && pwd)"
API_DIR="${ROOT}/src/Backend/FactuTrust.API"
LOG_FILE="${ROOT}/api-e2e.log"
PID_FILE="${ROOT}/api-e2e.pid"

export ASPNETCORE_ENVIRONMENT="${ASPNETCORE_ENVIRONMENT:-Development}"
export ASPNETCORE_URLS="${ASPNETCORE_URLS:-http://localhost:7000}"
export JwtSettings__SecretKey="${JwtSettings__SecretKey:-ci-only-jwt-signing-key-not-a-secret-0123456789abcdef}"
export SignatureSettings__SecretKey="${SignatureSettings__SecretKey:-ci-only-signature-key-not-a-secret-0123456789ab}"
export Channels__Enabled="${Channels__Enabled:-false}"
export Channels__WhatsAppEnabled="${Channels__WhatsAppEnabled:-false}"
export Channels__AutoStart="${Channels__AutoStart:-false}"
export TenantProvisioning__Strategy="${TenantProvisioning__Strategy:-Migrate}"

if [ -z "${ConnectionStrings__MasterConnection:-}" ] || [ -z "${ConnectionStrings__DefaultTenantConnection:-}" ]; then
  echo "ConnectionStrings__MasterConnection and ConnectionStrings__DefaultTenantConnection must be set." >&2
  exit 1
fi

cd "$API_DIR"
nohup dotnet run --no-build --no-launch-profile --configuration Release >"$LOG_FILE" 2>&1 &
echo $! >"$PID_FILE"
echo "API started (pid $(cat "$PID_FILE")), log: ${LOG_FILE}"
