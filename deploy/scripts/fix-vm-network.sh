#!/usr/bin/env bash
# Corrige une interface sans DHCP (adresse 169.254.x.x) sur Ubuntu VMware Bridged.
# Exécuter DANS la VM :
#   bash deploy/scripts/fix-vm-network.sh
# IP statique Bridged (sans apt) :
#   BRIDGED_STATIC=1 GATEWAY=192.168.1.1 IP=192.168.1.100 bash deploy/scripts/fix-vm-network.sh
set -euo pipefail

IFACE="${IFACE:-ens33}"
GATEWAY="${GATEWAY:-192.168.1.1}"
STATIC_IP="${IP:-192.168.1.100}"
HOST_IP="${HOST_IP:-192.168.1.17}"

if [[ "$(id -u)" -ne 0 ]]; then
  SUDO=sudo
else
  SUDO=""
fi

current_ip="$($SUDO ip -4 addr show "${IFACE}" 2>/dev/null | grep -oP '(?<=inet\s)\d+(\.\d+){3}' | head -1 || true)"

echo "=== FactuTrust — fix réseau VM (${IFACE}) ==="
echo "IP actuelle: ${current_ip:-aucune}"

apply_static_bridged() {
  echo "Application IP statique Bridged ${STATIC_IP}/24 via ${GATEWAY} ..."
  $SUDO ip addr flush dev "${IFACE}"
  $SUDO ip addr add "${STATIC_IP}/24" dev "${IFACE}"
  $SUDO ip link set "${IFACE}" up
  $SUDO ip route replace default via "${GATEWAY}" dev "${IFACE}" 2>/dev/null || \
    $SUDO ip route add default via "${GATEWAY}" dev "${IFACE}"
  echo -e "nameserver 8.8.8.8\nnameserver 1.1.1.1" | $SUDO tee /etc/resolv.conf >/dev/null
}

try_dhcp() {
  if command -v nmcli &>/dev/null; then
    $SUDO nmcli networking on || true
    $SUDO nmcli dev set "${IFACE}" managed yes 2>/dev/null || true
    $SUDO nmcli dev disconnect "${IFACE}" 2>/dev/null || true
    $SUDO nmcli dev connect "${IFACE}" 2>/dev/null || true
  fi
  if command -v dhclient &>/dev/null; then
    $SUDO dhclient -r "${IFACE}" 2>/dev/null || true
    $SUDO dhclient -v "${IFACE}" 2>/dev/null || true
  fi
}

# Option B en premier si APIPA ou BRIDGED_STATIC=1 (sans apt requis)
if [[ "${BRIDGED_STATIC:-0}" == "1" ]] || [[ "${current_ip:-}" == 169.254.* ]] || [[ -z "${current_ip:-}" ]]; then
  apply_static_bridged
  current_ip="${STATIC_IP}"
  if ping -c 1 -W 2 "${GATEWAY}" >/dev/null 2>&1; then
    echo "Passerelle ${GATEWAY} OK."
  else
    echo "WARN: passerelle ${GATEWAY} injoignable — vérifiez GATEWAY= (box/routeur)."
  fi
  if ping -c 1 -W 2 8.8.8.8 >/dev/null 2>&1; then
    echo "Internet OK (8.8.8.8)."
  else
    echo "WARN: pas d'accès Internet — vérifiez Bridged VMware + GATEWAY."
  fi
fi

IP="$($SUDO ip -4 addr show "${IFACE}" | grep -oP '(?<=inet\s)\d+(\.\d+){3}' | head -1 || true)"
echo "Interface ${IFACE} IPv4: ${IP:-NONE}"

if [[ "${IP:-}" == 169.254.* ]] || [[ -z "${IP:-}" ]]; then
  echo ""
  echo "Tentative DHCP..."
  $SUDO ip addr flush dev "${IFACE}" 2>/dev/null || true
  $SUDO ip link set "${IFACE}" up
  try_dhcp
  IP="$($SUDO ip -4 addr show "${IFACE}" | grep -oP '(?<=inet\s)\d+(\.\d+){3}' | head -1 || true)"
  echo "IP après DHCP: ${IP:-NONE}"
fi

if [[ "${IP:-}" == 169.254.* ]] || [[ -z "${IP:-}" ]]; then
  echo ""
  echo "ERREUR: pas d'IP routable."
  echo "  1. VM éteinte → VMware Settings → Network Adapter → Bridged (Wi-Fi/Ethernet)"
  echo "  2. Relancer: BRIDGED_STATIC=1 IP=192.168.1.100 GATEWAY=192.168.1.1 bash $0"
  exit 1
fi

# apt uniquement si Internet/DNS OK
if ping -c 1 -W 3 archive.ubuntu.com >/dev/null 2>&1; then
  $SUDO apt update
  $SUDO apt install -y openssh-server net-tools isc-dhcp-client 2>/dev/null || \
    $SUDO apt install -y openssh-server net-tools
  $SUDO systemctl enable --now ssh
else
  echo "DNS/apt ignorés (archive.ubuntu.com injoignable). SSH peut être installé plus tard."
  $SUDO systemctl enable --now ssh 2>/dev/null || true
fi

echo ""
echo "=== Réseau OK ==="
echo "  ssh $(whoami)@${IP}"
echo "Mettre à jour deploy/vm.lab.local.json : VmHost=${IP}, VmUser=$(whoami)"
