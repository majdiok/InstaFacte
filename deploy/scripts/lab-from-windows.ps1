# Orchestration: sync Windows -> VM + setup + deploy lab
# Usage:
#   .\deploy\scripts\lab-from-windows.ps1 -SetupVm -Deploy -UpdateHosts
param(
    [switch]$SetupVm,
    [switch]$Deploy,
    [switch]$UpdateHosts,
    [string]$ConfigPath = "deploy\vm.lab.local.json"
)

$ErrorActionPreference = "Continue"
$RepoRoot = Resolve-Path (Join-Path $PSScriptRoot "..\..")
$configFile = Join-Path $RepoRoot $ConfigPath

if (-not (Test-Path $configFile)) {
    Copy-Item (Join-Path $RepoRoot "deploy\vm.lab.local.json.example") $configFile
    Write-Host "Created config - edit VmHost then re-run."
    exit 1
}

$cfg = Get-Content $configFile -Raw | ConvertFrom-Json
$VmHost = [string]$cfg.VmHost
$VmUser = [string]$cfg.VmUser
$SshPort = if ($cfg.SshPort) { [int]$cfg.SshPort } else { 22 }
$LabDomain = if ($cfg.LabDomain) { [string]$cfg.LabDomain } else { "factutrust.local" }
$remotePath = if ($cfg.RemotePath) { [string]$cfg.RemotePath } else { "/opt/factutrust/src" }
$sshKey = Join-Path $env:USERPROFILE ".ssh\id_ed25519"
$sshTarget = $VmUser + "@" + $VmHost

function Invoke-VmSsh {
    param([Parameter(Mandatory)][string]$RemoteCommand)
    $arr = @("-p", "$SshPort", "-o", "BatchMode=yes")
    if (Test-Path $sshKey) { $arr += @("-i", $sshKey) }
    $arr += @($sshTarget, $RemoteCommand)
    $output = & ssh.exe @arr 2>&1
    $code = $LASTEXITCODE
    foreach ($line in $output) { Write-Host $line }
    return $code
}

function Invoke-VmScp {
    param([Parameter(Mandatory)][string]$LocalPath, [Parameter(Mandatory)][string]$RemoteDest)
    $arr = @("-P", "$SshPort", "-o", "BatchMode=yes")
    if (Test-Path $sshKey) { $arr += @("-i", $sshKey) }
    $arr += @($LocalPath, $RemoteDest)
    $output = & scp.exe @arr 2>&1
    $code = $LASTEXITCODE
    foreach ($line in $output) { Write-Host $line }
    return $code
}

if ([string]::IsNullOrWhiteSpace($VmHost) -or $VmHost -match "192\.168\.1\.50") {
    Write-Warning "Edit deploy/vm.lab.local.json with your real VM IP before continuing."
}

& (Join-Path $PSScriptRoot "sync-to-vm.ps1") -ConfigPath $ConfigPath

if ($SetupVm) {
    Write-Host "=== Running VM setup (requires sudo on VM) ==="
    $setupLocal = Join-Path $RepoRoot "deploy\scripts\setup-vmware-lab.sh"
    $rc = Invoke-VmScp -LocalPath $setupLocal -RemoteDest ($sshTarget + ":/tmp/setup-vmware-lab.sh")
    if ($rc -ne 0) { throw "scp setup-vmware-lab.sh failed" }
    $rc = Invoke-VmSsh -RemoteCommand ("sudo DEPLOY_USER=" + $VmUser + " bash /tmp/setup-vmware-lab.sh")
    if ($rc -ne 0) { throw "setup-vmware-lab.sh failed" }
}

$patchCmd = "cd " + $remotePath + "; sed -i s#http://IP_VM#http://" + $VmHost + "#g deploy/.env.lab.example"
Invoke-VmSsh -RemoteCommand $patchCmd | Out-Null

if ($Deploy) {
    Write-Host "=== Deploy lab stack on VM (long running) ==="
    $rc = Invoke-VmSsh -RemoteCommand ("cd " + $remotePath + "; bash deploy/scripts/deploy-lab.sh")
    if ($rc -ne 0) { throw "deploy-lab.sh failed" }
}

if ($UpdateHosts) {
    $hostsLine = $VmHost + "`t" + $LabDomain
    $hostsPath = Join-Path $env:SystemRoot "System32\drivers\etc\hosts"
    $content = Get-Content $hostsPath -Raw -ErrorAction SilentlyContinue
    if ($content -notmatch [regex]::Escape($LabDomain)) {
        Write-Host ("Adding to hosts: " + $hostsLine)
        try {
            Add-Content -Path $hostsPath -Value ("`n" + $hostsLine) -ErrorAction Stop
        }
        catch {
            Write-Warning ("Could not update hosts file. Run as Admin: " + $hostsLine)
        }
    }
    else {
        Write-Host ("hosts already contains " + $LabDomain)
    }
}

Write-Host ""
Write-Host "=== Done ==="
Write-Host ("Web:        http://" + $LabDomain + "/  (or http://" + $VmHost + "/)")
Write-Host ("Backoffice: http://" + $LabDomain + "/admin/")
