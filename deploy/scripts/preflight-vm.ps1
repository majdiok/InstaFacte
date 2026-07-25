# Verifie la connectivite SSH vers la VM lab avant sync/deploy.
param(
    [string]$ConfigPath = "deploy\vm.lab.local.json",
    [switch]$Discover
)

$ErrorActionPreference = "Stop"
$RepoRoot = Resolve-Path (Join-Path $PSScriptRoot "..\..")
$configFile = Join-Path $RepoRoot $ConfigPath

if (-not (Test-Path $configFile)) {
    Write-Host "MISSING: $ConfigPath"
    exit 1
}

$cfg = Get-Content $configFile -Raw | ConvertFrom-Json
$VmHost = [string]$cfg.VmHost
$VmUser = [string]$cfg.VmUser
if ([string]::IsNullOrWhiteSpace($VmUser)) { $VmUser = "sabiko" }

if ($VmHost -match '^169\.254\.' -or [string]::IsNullOrWhiteSpace($VmHost)) {
    Write-Host "ERREUR: VmHost invalide ($VmHost)"
    exit 1
}

$port = if ($cfg.SshPort) { [int]$cfg.SshPort } else { 22 }
$sshTarget = $VmUser + "@" + $VmHost
$sshKey = Join-Path $env:USERPROFILE ".ssh\id_ed25519"

Write-Host ("Ping " + $VmHost + " ...")
if (-not (Test-Connection -ComputerName $VmHost -Count 1 -Quiet -ErrorAction SilentlyContinue)) {
    Write-Host ("ERREUR: ping echoue vers " + $VmHost)
    exit 1
}

Write-Host ("Testing SSH to " + $sshTarget + ":" + $port + " ...")
$sshBase = New-Object System.Collections.Generic.List[string]
[void]$sshBase.Add("-o"); [void]$sshBase.Add("ConnectTimeout=10")
[void]$sshBase.Add("-o"); [void]$sshBase.Add("BatchMode=yes")
[void]$sshBase.Add("-p"); [void]$sshBase.Add("$port")
if (Test-Path $sshKey) {
    [void]$sshBase.Add("-i"); [void]$sshBase.Add($sshKey)
}

& ssh $sshBase.ToArray() $sshTarget "uname -a"
if ($LASTEXITCODE -ne 0) {
    Write-Host "SSH failed."
    exit 1
}

& ssh $sshBase.ToArray() $sshTarget "docker --version"
if ($LASTEXITCODE -ne 0) {
    Write-Host "Docker not yet installed (OK before SetupVm)."
}

Write-Host "Preflight OK."
exit 0
