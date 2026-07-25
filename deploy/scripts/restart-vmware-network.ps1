# Redémarre les services réseau VMware (nécessite PowerShell administrateur).
# Usage : clic droit PowerShell → Exécuter en tant qu'administrateur
#   .\deploy\scripts\restart-vmware-network.ps1

$ErrorActionPreference = "Stop"

$services = @(
    "VMnetDHCP",
    "VMware NAT Service",
    "VMware Authorization Service"
)

foreach ($name in $services) {
    $svc = Get-Service -Name $name -ErrorAction SilentlyContinue
    if (-not $svc) { continue }
    Write-Host "Starting $name ..."
    if ($svc.Status -ne "Running") {
        Start-Service $name
    }
    Get-Service $name | Format-Table Name, Status -AutoSize
}

Write-Host ""
Write-Host "Redémarrez la VM Ubuntu, puis dans la VM :"
Write-Host "  sudo dhclient ens33"
Write-Host "  ip -4 addr show ens33"
Write-Host ""
Write-Host "Attendu : 192.168.x.x (NAT) ou 192.168.1.x (Bridged), PAS 169.254.x.x"
