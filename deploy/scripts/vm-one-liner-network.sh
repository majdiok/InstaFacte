#!/usr/bin/env bash
# One-liner friendly: IP Bridged + openssh. Run in VM console as sabiko.
set -euo pipefail
IFACE="${IFACE:-ens33}"
IP="${IP:-192.168.1.100}"
GW="${GATEWAY:-192.168.1.1}"
sudo ip addr flush dev "$IFACE"
sudo ip addr add "${IP}/24" dev "$IFACE"
sudo ip link set "$IFACE" up
sudo ip route replace default via "$GW" || sudo ip route add default via "$GW"
echo -e "nameserver 8.8.8.8\nnameserver 1.1.1.1" | sudo tee /etc/resolv.conf
ping -c 2 "$GW" || true
ping -c 2 8.8.8.8 || true
sudo apt-get update
sudo DEBIAN_FRONTEND=noninteractive apt-get install -y openssh-server
sudo systemctl enable --now ssh
ip -4 addr show "$IFACE"
echo "SSH ready: ssh $(whoami)@${IP}"
