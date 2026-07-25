# Configure la VM Ubuntu VMware en mode Bridged (Power Off requis).
# Usage (depuis la racine du dépôt) :
#   .\deploy\scripts\set-vm-bridged.ps1
#   .\deploy\scripts\set-vm-bridged.ps1 -VmPath "C:\...\Ubuntu 64-bit.vmx" -Restart
param(
    [string]$VmPath = "$env:USERPROFILE\Documents\Virtual Machines\Ubuntu 64-bit\Ubuntu 64-bit.vmx",
    [switch]$Restart,
    [string]$BridgedTo = ""  # ex. "Wi-Fi" — laisser vide = carte par défaut VMware
)

$ErrorActionPreference = "Stop"
$vmrun = "${env:ProgramFiles(x86)}\VMware\VMware Workstation\vmrun.exe"
if (-not (Test-Path $vmrun)) {
    $vmrun = "$env:ProgramFiles\VMware\VMware Workstation\vmrun.exe"
}
if (-not (Test-Path $vmrun)) { throw "vmrun.exe introuvable" }
if (-not (Test-Path $VmPath)) { throw "VM introuvable: $VmPath" }

$running = & $vmrun -T ws list | Select-String -SimpleMatch $VmPath
if ($running -and $Restart) {
    Write-Host "Arret VM..."
    & $vmrun -T ws stop $VmPath hard 2>$null
    Start-Sleep -Seconds 4
}

$vmx = Get-Content $VmPath -Raw
$lines = Get-Content $VmPath

$newLines = [System.Collections.Generic.List[string]]::new()
$skipKeys = @('ethernet0.connectionType', 'ethernet0.vnet', 'ethernet0.displayName', 'ethernet0.bridged')
foreach ($line in $lines) {
    $trim = $line.Trim()
    $skip = $false
    foreach ($key in $skipKeys) {
        if ($trim -like "$key*") { $skip = $true; break }
    }
    if (-not $skip) { [void]$newLines.Add($line) }
}

# Insérer après ethernet0.present
$insertAt = [Math]::Max(0, ($newLines | Select-String -SimpleMatch 'ethernet0.present = "TRUE"' | Select-Object -First 1).LineNumber)
if ($insertAt -gt 0) {
    $head = $newLines.GetRange(0, $insertAt)
    $tail = if ($insertAt -lt $newLines.Count) { $newLines.GetRange($insertAt, $newLines.Count - $insertAt) } else { @() }
    $bridgeLines = @(
        'ethernet0.connectionType = "bridged"'
        'ethernet0.displayName = "Bridged"'
    )
    if ($BridgedTo) {
        $bridgeLines += "ethernet0.bridged = `"$BridgedTo`""
    }
    $newLines = [System.Collections.Generic.List[string]]::new()
    foreach ($l in $head) { [void]$newLines.Add($l) }
    foreach ($l in $bridgeLines) { [void]$newLines.Add($l) }
    foreach ($l in $tail) { [void]$newLines.Add($l) }
}

Set-Content -Path $VmPath -Value $newLines -Encoding UTF8
Write-Host "VMX mis à jour : Bridged"

if ($Restart) {
    Write-Host "Démarrage VM..."
    & $vmrun -T ws start $VmPath nogui 2>&1 | Out-Host
    Write-Host "Attendre le boot Ubuntu (~60s), puis dans la VM :"
    Write-Host "  bash deploy/scripts/fix-vm-network.sh"
}
