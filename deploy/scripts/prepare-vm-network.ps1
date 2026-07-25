# Orchestration reseau VM : Bridged + console fix + preflight.
# Usage :
#   $env:VM_GUEST_PASSWORD = 'mot-de-passe-ubuntu'
#   .\deploy\scripts\prepare-vm-network.ps1
param(
    [string]$StaticIp = "192.168.1.100",
    [string]$GuestPassword = $env:VM_GUEST_PASSWORD,
    [switch]$SkipBridged,
    [switch]$SkipConsole
)

$ErrorActionPreference = "Stop"
$RepoRoot = Resolve-Path (Join-Path $PSScriptRoot "..\..")
Set-Location $RepoRoot

if (-not $SkipBridged) {
    & (Join-Path $PSScriptRoot "set-vm-bridged.ps1") -Restart
    Write-Host "Attente boot VM (90s)..."
    Start-Sleep -Seconds 90
}

if (-not $GuestPassword) {
    $pwFile = Join-Path $RepoRoot "deploy\vm.guest-password.local"
    if (Test-Path $pwFile) {
        $GuestPassword = (Get-Content $pwFile -Raw).Trim()
        $env:VM_GUEST_PASSWORD = $GuestPassword
    }
}

if (-not $SkipConsole) {
    & (Join-Path $PSScriptRoot "vm-console-type-network-fix.ps1") -StaticIp $StaticIp
    Write-Host "Attente apt/openssh (120s)..."
    Start-Sleep -Seconds 120
}

$discovered = & (Join-Path $PSScriptRoot "discover-vm.ps1") -PreferHosts @($StaticIp) -ErrorAction SilentlyContinue
$ip = if ($discovered) { $discovered } else { $StaticIp }

$cfgPath = Join-Path $RepoRoot "deploy\vm.lab.local.json"
$cfg = Get-Content $cfgPath | ConvertFrom-Json
$cfg.VmHost = $ip
$cfg | ConvertTo-Json -Depth 3 | Set-Content $cfgPath
Write-Host "vm.lab.local.json -> VmHost=$ip"

& (Join-Path $PSScriptRoot "preflight-vm.ps1")
if ($LASTEXITCODE -ne 0) {
    Write-Host ""
    Write-Host "Preflight echoue. Dans la VM (console), executer :"
    Write-Host "  BRIDGED_STATIC=1 bash deploy/scripts/fix-vm-network.sh"
    exit 1
}

Write-Host "Reseau VM OK. Lancer :"
Write-Host "  .\deploy\scripts\lab-from-windows.ps1 -SetupVm -Deploy -UpdateHosts"
