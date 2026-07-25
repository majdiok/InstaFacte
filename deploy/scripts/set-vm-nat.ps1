# Configure la VM Ubuntu en mode NAT VMware (VMnet8, DHCP fiable depuis Windows).
param(
    [string]$VmPath = "$env:USERPROFILE\Documents\Virtual Machines\Ubuntu 64-bit\Ubuntu 64-bit.vmx",
    [switch]$Restart
)

$ErrorActionPreference = "Stop"
$vmrun = "${env:ProgramFiles(x86)}\VMware\VMware Workstation\vmrun.exe"
if (-not (Test-Path $vmrun)) { throw "vmrun.exe introuvable" }
if (-not (Test-Path $VmPath)) { throw "VM introuvable: $VmPath" }

if ($Restart) {
    Write-Host "Arret VM (hard si soft echoue)..."
    & $vmrun -T ws stop $VmPath soft 2>$null
    Start-Sleep -Seconds 8
    $running = & $vmrun -T ws list | Select-String -SimpleMatch $VmPath
    if ($running) {
        & $vmrun -T ws stop $VmPath hard 2>$null
        Start-Sleep -Seconds 3
    }
}

$skipKeys = @('ethernet0.connectionType', 'ethernet0.vnet', 'ethernet0.displayName', 'ethernet0.bridged')
$newLines = [System.Collections.Generic.List[string]]::new()
foreach ($line in Get-Content $VmPath) {
    $trim = $line.Trim()
    $skip = $false
    foreach ($key in $skipKeys) {
        if ($trim -like "$key*") { $skip = $true; break }
    }
    if (-not $skip) { [void]$newLines.Add($line) }
}

$insertAt = ($newLines | Select-String -SimpleMatch 'ethernet0.present = "TRUE"' | Select-Object -First 1).LineNumber
if ($insertAt -gt 0) {
    $head = $newLines.GetRange(0, $insertAt)
    $tail = if ($insertAt -lt $newLines.Count) { $newLines.GetRange($insertAt, $newLines.Count - $insertAt) } else { @() }
    $natLines = @(
        'ethernet0.connectionType = "nat"'
        'ethernet0.displayName = "NAT"'
    )
    $newLines = [System.Collections.Generic.List[string]]::new()
    foreach ($l in $head) { [void]$newLines.Add($l) }
    foreach ($l in $natLines) { [void]$newLines.Add($l) }
    foreach ($l in $tail) { [void]$newLines.Add($l) }
}

Set-Content -Path $VmPath -Value $newLines -Encoding UTF8
Write-Host "VMX mis a jour : NAT"

if ($Restart) {
    & $vmrun -T ws start $VmPath nogui 2>&1 | Out-Host
    Write-Host "VM demarree en NAT. IP attendue 192.168.179.x (VMnet8)."
}
