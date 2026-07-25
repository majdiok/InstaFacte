# Installe openssh-server dans la VM via vmrun (VMware Tools + utilisateur invité).
# Usage :
#   $env:VM_GUEST_PASSWORD = '...'
#   .\deploy\scripts\install-ssh-guest.ps1
param(
    [string]$VmPath = "$env:USERPROFILE\Documents\Virtual Machines\Ubuntu 64-bit\Ubuntu 64-bit.vmx",
    [string]$GuestUser = "sabiko",
    [string]$GuestPassword = $env:VM_GUEST_PASSWORD
)

$ErrorActionPreference = "Stop"
if (-not $GuestPassword) {
    Write-Host "Définir VM_GUEST_PASSWORD ou -GuestPassword (mot de passe utilisateur Ubuntu)."
    exit 1
}

$vmrun = "${env:ProgramFiles(x86)}\VMware\VMware Workstation\vmrun.exe"
$script = @'
#!/bin/bash
set -e
IFACE="${IFACE:-ens33}"
if ip -4 addr show "$IFACE" 2>/dev/null | grep -q '169.254.'; then
  sudo ip addr flush dev "$IFACE"
  sudo ip addr add 192.168.1.100/24 dev "$IFACE"
  sudo ip link set "$IFACE" up
  sudo ip route replace default via 192.168.1.1 dev "$IFACE" 2>/dev/null || sudo ip route add default via 192.168.1.1
  echo -e "nameserver 8.8.8.8\nnameserver 1.1.1.1" | sudo tee /etc/resolv.conf
fi
sudo apt-get update
sudo DEBIAN_FRONTEND=noninteractive apt-get install -y openssh-server
sudo systemctl enable --now ssh
ip -4 addr show "$IFACE" | grep inet
'@

$tmp = Join-Path $env:TEMP "factutrust-install-ssh.sh"
Set-Content -Path $tmp -Value $script -Encoding ASCII -NoNewline

Write-Host "Copie script vers /tmp/install-ssh.sh ..."
& $vmrun -T ws -gu $GuestUser -gp $GuestPassword copyFileFromHostToGuest $VmPath $tmp /tmp/install-ssh.sh
Write-Host "Exécution dans la VM..."
& $vmrun -T ws -gu $GuestUser -gp $GuestPassword runScriptInGuest $VmPath /bin/bash /tmp/install-ssh.sh
Remove-Item $tmp -Force -ErrorAction SilentlyContinue
Write-Host "Terminé. Tester : ssh ${GuestUser}@<IP_VM>"
