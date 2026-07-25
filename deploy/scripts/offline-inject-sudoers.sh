#!/usr/bin/env bash
set -euo pipefail
VMDK="/mnt/c/Users/sabriko/Documents/Virtual Machines/Ubuntu 64-bit/Ubuntu 64-bit.vmdk"
MNT=/mnt/factutrust-vm
modprobe nbd max_part=16 || true
qemu-nbd -d /dev/nbd0 2>/dev/null || true
umount "$MNT" 2>/dev/null || true
mkdir -p "$MNT"
qemu-nbd -c /dev/nbd0 -f vmdk "$VMDK"
sleep 2
mount /dev/nbd0p2 "$MNT"
mkdir -p "$MNT/etc/sudoers.d"
printf 'sabiko ALL=(ALL) NOPASSWD:ALL\n' > "$MNT/etc/sudoers.d/99-factutrust-sabiko"
chmod 440 "$MNT/etc/sudoers.d/99-factutrust-sabiko"
ls -la "$MNT/etc/sudoers.d/99-factutrust-sabiko"
cat "$MNT/etc/sudoers.d/99-factutrust-sabiko"
sync
umount "$MNT"
qemu-nbd -d /dev/nbd0
echo SUDOERS_OK
