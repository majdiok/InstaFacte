#!/usr/bin/env bash
# Sauvegarde SQL Server + volumes applicatifs + certificat DataProtection.
# Usage :
#   bash deploy/scripts/backup.sh
# Planifier via cron quotidien sur le VPS.
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
DEPLOY_DIR="$(cd "${SCRIPT_DIR}/.." && pwd)"
COMPOSE_FILE="${COMPOSE_FILE:-${DEPLOY_DIR}/docker-compose.yml}"
BACKUP_ROOT="${BACKUP_ROOT:-/opt/factutrust/backups}"
STAMP="$(date +%Y%m%d_%H%M%S)"
DEST="${BACKUP_ROOT}/${STAMP}"

# shellcheck source=/dev/null
source "${DEPLOY_DIR}/.env"

mkdir -p "${DEST}"

echo "Backup to ${DEST}"

# SQL — master database
docker compose -f "${COMPOSE_FILE}" exec -T sqlserver \
  /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P "${MSSQL_SA_PASSWORD}" -C \
  -Q "BACKUP DATABASE [FactuTrust_Master] TO DISK='/var/opt/mssql/data/FactuTrust_Master_${STAMP}.bak' WITH INIT, COMPRESSION"

# List tenant databases and backup each
DATABASES="$(docker compose -f "${COMPOSE_FILE}" exec -T sqlserver \
  /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P "${MSSQL_SA_PASSWORD}" -C -h -1 -W \
  -Q "SET NOCOUNT ON; SELECT name FROM sys.databases WHERE name LIKE 'FactuTrust_%' AND name <> 'FactuTrust_Master'")"

while IFS= read -r db; do
  [[ -z "${db}" ]] && continue
  db="$(echo "${db}" | tr -d '[:space:]')"
  echo "Backing up ${db}..."
  docker compose -f "${COMPOSE_FILE}" exec -T sqlserver \
    /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P "${MSSQL_SA_PASSWORD}" -C \
    -Q "BACKUP DATABASE [${db}] TO DISK='/var/opt/mssql/data/${db}_${STAMP}.bak' WITH INIT, COMPRESSION"
done <<< "${DATABASES}"

# Copy .bak files from container
docker compose -f "${COMPOSE_FILE}" cp "sqlserver:/var/opt/mssql/data/." "${DEST}/sql/" 2>/dev/null \
  || docker cp "factutrust-sqlserver:/var/opt/mssql/data/." "${DEST}/sql/"

# DataProtection cert (critical)
if [[ -f "${DEPLOY_DIR}/certs/dataprotection.pfx" ]]; then
  cp "${DEPLOY_DIR}/certs/dataprotection.pfx" "${DEST}/"
fi

# Archive named volumes (wwwroot + App_Data) via temporary container
for vol in factutrust_api-wwwroot factutrust_api-appdata; do
  if docker volume inspect "${vol}" >/dev/null 2>&1; then
    docker run --rm -v "${vol}:/data:ro" -v "${DEST}:/backup" alpine \
      tar czf "/backup/${vol}.tar.gz" -C /data .
  fi
done

echo "Backup complete: ${DEST}"
echo "Copy ${DEST} off-site. NEVER lose dataprotection.pfx with the SQL backups."
