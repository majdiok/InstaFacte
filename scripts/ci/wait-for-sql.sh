#!/usr/bin/env bash
# Attend qu'un SQL Server soit joignable (conteneur GitHub Actions / Azure Pipelines).
set -euo pipefail

HOST="${SQL_HOST:-localhost}"
PORT="${SQL_PORT:-1433}"
USER="${SQL_USER:-sa}"
PASSWORD="${CI_SQL_SA_PASSWORD:?CI_SQL_SA_PASSWORD is required}"
MAX_ATTEMPTS="${SQL_WAIT_MAX_ATTEMPTS:-30}"
SLEEP_SECONDS="${SQL_WAIT_SLEEP_SECONDS:-2}"

if ! command -v sqlcmd >/dev/null 2>&1 && [ ! -x /opt/mssql-tools18/bin/sqlcmd ]; then
  curl -fsSL https://packages.microsoft.com/keys/microsoft.asc | sudo apt-key add -
  curl -fsSL https://packages.microsoft.com/config/ubuntu/22.04/prod.list | sudo tee /etc/apt/sources.list.d/mssql-release.list >/dev/null
  sudo apt-get update -qq
  ACCEPT_EULA=Y sudo apt-get install -y -qq mssql-tools18 unixodbc-dev
fi

SQLCMD=$(command -v sqlcmd || echo /opt/mssql-tools18/bin/sqlcmd)

echo "Waiting for SQL Server at ${HOST},${PORT}..."
for attempt in $(seq 1 "$MAX_ATTEMPTS"); do
  if "$SQLCMD" -S "${HOST},${PORT}" -U "$USER" -P "$PASSWORD" -C -Q "SELECT 1" -b -l 2 >/dev/null; then
    echo "SQL Server is ready (attempt ${attempt}/${MAX_ATTEMPTS})."
    exit 0
  fi
  echo "  attempt ${attempt}/${MAX_ATTEMPTS} — not ready yet"
  sleep "$SLEEP_SECONDS"
done

echo "SQL Server unavailable after $((MAX_ATTEMPTS * SLEEP_SECONDS))s" >&2
exit 1
