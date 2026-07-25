#!/usr/bin/env bash
# Monte le VMDK Ubuntu (VM éteinte) et injecte IP + openssh + oneshot systemd.
# Bridged (défaut) :
#   bash deploy/scripts/offline-inject-vm-network.sh
# NAT VMware :
#   STATIC_IP=192.168.179.100 GATEWAY=192.168.179.2 bash deploy/scripts/offline-inject-vm-network.sh
set -euo pipefail

VMDK="${VMDK:-/mnt/c/Users/sabriko/Documents/Virtual Machines/Ubuntu 64-bit/Ubuntu 64-bit.vmdk}"
MNT="${MNT:-/mnt/factutrust-vm}"
STATIC_IP="${STATIC_IP:-192.168.1.100}"
GATEWAY="${GATEWAY:-192.168.1.1}"
IFACE="${IFACE:-ens33}"
USER_NAME="${USER_NAME:-sabiko}"

if [[ ! -f "$VMDK" ]]; then
  echo "VMDK introuvable: $VMDK"
  exit 1
fi

sudo mkdir -p "$MNT"
sudo modprobe nbd max_part=16 || true
sudo qemu-nbd -d /dev/nbd0 2>/dev/null || true
sudo umount "$MNT" 2>/dev/null || true

echo "Attaching $VMDK ..."
sudo qemu-nbd -c /dev/nbd0 -f vmdk "$VMDK"
sleep 2
lsblk /dev/nbd0

ROOT_PART="/dev/nbd0p2"
for cand in /dev/nbd0p2 /dev/nbd0p3 /dev/nbd0p1 ; do
  if [[ -b "$cand" ]] && sudo blkid "$cand" | grep -qiE 'ext4|btrfs|xfs'; then
    ROOT_PART="$cand"
    break
  fi
done

echo "Mounting $ROOT_PART -> $MNT"
sudo mount "$ROOT_PART" "$MNT"

echo "Writing netplan ${STATIC_IP}/24 via ${GATEWAY} ..."
sudo tee "$MNT/etc/netplan/99-factutrust-static.yaml" >/dev/null <<EOF
network:
  version: 2
  renderer: networkd
  ethernets:
    ${IFACE}:
      dhcp4: false
      addresses:
        - ${STATIC_IP}/24
      routes:
        - to: default
          via: ${GATEWAY}
      nameservers:
        addresses: [8.8.8.8, 1.1.1.1, ${GATEWAY}]
EOF
sudo chmod 600 "$MNT/etc/netplan/99-factutrust-static.yaml"

sudo mkdir -p "$MNT/etc/netplan/disabled"
sudo bash -c "shopt -s nullglob; for f in $MNT/etc/netplan/*.yaml; do bn=\$(basename \"\$f\"); [[ \"\$bn\" == 99-factutrust-static.yaml ]] && continue; mv \"\$f\" $MNT/etc/netplan/disabled/ 2>/dev/null || true; done"

sudo tee "$MNT/usr/local/sbin/factutrust-force-ip.sh" >/dev/null <<EOF
#!/bin/bash
IFACE=${IFACE}
IP=${STATIC_IP}
GW=${GATEWAY}
ip link set "\$IFACE" up || true
ip addr flush dev "\$IFACE" || true
ip addr add "\${IP}/24" dev "\$IFACE" || true
ip route replace default via "\$GW" || ip route add default via "\$GW" || true
echo -e "nameserver 8.8.8.8\\nnameserver 1.1.1.1" > /etc/resolv.conf || true
EOF
sudo chmod 755 "$MNT/usr/local/sbin/factutrust-force-ip.sh"

sudo tee "$MNT/etc/systemd/system/factutrust-force-ip.service" >/dev/null <<'EOF'
[Unit]
Description=FactuTrust force static IP
DefaultDependencies=no
After=sys-subsystem-net-devices-ens33.device
Wants=network-pre.target
Before=network-pre.target ssh.service

[Service]
Type=oneshot
ExecStart=/usr/local/sbin/factutrust-force-ip.sh
RemainAfterExit=yes

[Install]
WantedBy=multi-user.target
EOF
sudo ln -sf /etc/systemd/system/factutrust-force-ip.service \
  "$MNT/etc/systemd/system/multi-user.target.wants/factutrust-force-ip.service"

echo -e "nameserver 8.8.8.8\nnameserver 1.1.1.1" | sudo tee "$MNT/etc/resolv.conf" >/dev/null

HOST_PUB=""
for f in /mnt/c/Users/sabriko/.ssh/id_ed25519.pub /mnt/c/Users/sabriko/.ssh/id_rsa.pub; do
  if [[ -f "$f" ]]; then HOST_PUB="$f"; break; fi
done
HOME_DIR="$MNT/home/$USER_NAME"
if [[ -n "$HOST_PUB" && -d "$HOME_DIR" ]]; then
  sudo mkdir -p "$HOME_DIR/.ssh"
  sudo cp "$HOST_PUB" "$HOME_DIR/.ssh/authorized_keys"
  sudo chmod 700 "$HOME_DIR/.ssh"
  sudo chmod 600 "$HOME_DIR/.ssh/authorized_keys"
  sudo chown -R --reference="$HOME_DIR" "$HOME_DIR/.ssh" || true
  echo "Injected SSH key"
fi

sudo tee "$MNT/etc/ssh/sshd_config.d/99-factutrust.conf" >/dev/null <<'EOF'
PubkeyAuthentication yes
PasswordAuthentication yes
PermitRootLogin no
EOF

if [[ -x "$MNT/usr/sbin/sshd" ]]; then
  echo "sshd present — enabling"
  sudo ln -sf /lib/systemd/system/ssh.service "$MNT/etc/systemd/system/multi-user.target.wants/ssh.service" 2>/dev/null || true
else
  echo "Installing openssh-server via chroot..."
  sudo mount --bind /dev "$MNT/dev"
  sudo mount --bind /proc "$MNT/proc"
  sudo mount --bind /sys "$MNT/sys"
  sudo mount --bind /dev/pts "$MNT/dev/pts" 2>/dev/null || true
  sudo cp /etc/resolv.conf "$MNT/etc/resolv.conf"
  sudo chroot "$MNT" bash -lc 'export DEBIAN_FRONTEND=noninteractive; apt-get update && apt-get install -y openssh-server && systemctl enable ssh'
  sudo umount "$MNT/dev/pts" 2>/dev/null || true
  sudo umount "$MNT/dev" 2>/dev/null || true
  sudo umount "$MNT/proc" 2>/dev/null || true
  sudo umount "$MNT/sys" 2>/dev/null || true
fi

echo "factutrust offline inject $(date -Iseconds) ip=${STATIC_IP} gw=${GATEWAY}" | sudo tee "$MNT/etc/factutrust-offline-inject.txt" >/dev/null

sync
sudo umount "$MNT"
sudo qemu-nbd -d /dev/nbd0
echo "=== DONE === ssh ${USER_NAME}@${STATIC_IP}"
