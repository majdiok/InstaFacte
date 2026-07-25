#!/usr/bin/env bash
# Prépare une VM Ubuntu VMware pour FactuTrust Docker lab.
# Exécuter SUR LA VM (console ou SSH) :
#   curl -fsSL ... | bash   OU   bash deploy/scripts/setup-vmware-lab.sh
set -euo pipefail

DEPLOY_USER="${DEPLOY_USER:-deploy}"

echo "=== FactuTrust VMware lab — VM setup ==="

if [[ "$(id -u)" -ne 0 ]]; then
  echo "Re-run with sudo: sudo bash $0"
  exit 1
fi

apt update
apt install -y ca-certificates curl git openssl ufw rsync openssh-server dos2unix

systemctl enable --now ssh
echo "SSH enabled."

if ! id "${DEPLOY_USER}" &>/dev/null; then
  adduser --disabled-password --gecos "" "${DEPLOY_USER}" || true
  usermod -aG sudo "${DEPLOY_USER}"
  echo "${DEPLOY_USER} ALL=(ALL) NOPASSWD:ALL" > "/etc/sudoers.d/${DEPLOY_USER}"
  chmod 440 "/etc/sudoers.d/${DEPLOY_USER}"
  echo "User ${DEPLOY_USER} created (set password: passwd ${DEPLOY_USER})"
fi

if ! command -v docker &>/dev/null; then
  curl -fsSL https://get.docker.com | sh
fi
usermod -aG docker "${DEPLOY_USER}" || true
apt install -y docker-compose-plugin

if ! swapon --show | grep -q swapfile; then
  fallocate -l 4G /swapfile || dd if=/dev/zero of=/swapfile bs=1M count=4096
  chmod 600 /swapfile
  mkswap /swapfile
  swapon /swapfile
  grep -q '/swapfile' /etc/fstab || echo '/swapfile none swap sw 0 0' >> /etc/fstab
  echo "4G swap enabled."
fi

ufw allow OpenSSH
ufw allow 80/tcp
ufw allow 443/tcp
ufw --force enable

mkdir -p /opt/factutrust
chown -R "${DEPLOY_USER}:${DEPLOY_USER}" /opt/factutrust

echo ""
echo "=== Setup complete ==="
echo "VM IP addresses:"
ip -4 addr show | grep -oP '(?<=inet\s)\d+(\.\d+){3}' | grep -v '^127\.' || true
echo ""
echo "Next (from Windows host): deploy/scripts/sync-to-vm.ps1 -VmHost IP -VmUser ${DEPLOY_USER}"
