# Bootstrap complet à exécuter DANS la VM (copier le dossier deploy/scripts ou tout le repo via partage VMware).
# Usage depuis la racine du dépôt copié sur la VM :
#   bash deploy/scripts/vm-console-bootstrap.sh
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "${SCRIPT_DIR}/../.." && pwd)"
cd "${REPO_ROOT}"

echo "=== FactuTrust VM console bootstrap ==="

bash deploy/scripts/fix-vm-network.sh

IFACE="${IFACE:-ens33}"
IP="$(ip -4 addr show "${IFACE}" 2>/dev/null | grep -oP '(?<=inet\s)\d+(\.\d+){3}' | grep -v '^127\.' | head -1 || true)"
if [[ -z "${IP}" ]]; then
  echo "Pas d'IP routable — corriger le réseau VMware avant de continuer."
  exit 1
fi

# Setup Docker (utilisateur courant, ex. sabiko)
export DEPLOY_USER="${DEPLOY_USER:-$(whoami)}"
sudo bash deploy/scripts/setup-vmware-lab.sh

# Préparer .env lab avec la vraie IP
cp deploy/.env.lab.example deploy/.env
sed -i "s|http://IP_VM|http://${IP}|g" deploy/.env
sed -i "s|http://factutrust.local|http://${IP}|g" deploy/.env || true

grep -q "^AllowedOrigins=.*${IP}" deploy/.env || \
  sed -i "s|^AllowedOrigins=.*|AllowedOrigins=http://${IP},http://factutrust.local|" deploy/.env

echo ""
echo "=== Bootstrap VM OK — IP: ${IP} ==="
echo "Depuis Windows, éditez deploy/vm.lab.local.json :"
echo "  VmHost: ${IP}"
echo "  VmUser: ${DEPLOY_USER}"
echo ""
echo "Puis sur Windows :"
echo "  .\\deploy\\scripts\\lab-from-windows.ps1 -Deploy -UpdateHosts"
