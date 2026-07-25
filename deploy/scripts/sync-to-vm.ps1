# Copie le depot FactuTrust vers une VM Ubuntu VMware (tar + ssh).
param(
    [string]$VmHost = "",
    [string]$VmUser = "deploy",
    [string]$RemotePath = "/opt/factutrust/src",
    [int]$SshPort = 22,
    [string]$ConfigPath = "deploy\vm.lab.local.json"
)

$ErrorActionPreference = "Continue"
$RepoRoot = Resolve-Path (Join-Path $PSScriptRoot "..\..")

if (-not $VmHost -and (Test-Path (Join-Path $RepoRoot $ConfigPath))) {
    $cfg = Get-Content (Join-Path $RepoRoot $ConfigPath) -Raw | ConvertFrom-Json
    $VmHost = [string]$cfg.VmHost
    if ($cfg.VmUser) { $VmUser = [string]$cfg.VmUser }
    if ($cfg.RemotePath) { $RemotePath = [string]$cfg.RemotePath }
    if ($cfg.SshPort) { $SshPort = [int]$cfg.SshPort }
}

if (-not $VmHost) {
    throw "VmHost required. Use -VmHost IP or create deploy/vm.lab.local.json"
}

$sshTarget = $VmUser + "@" + $VmHost
$sshKey = Join-Path $env:USERPROFILE ".ssh\id_ed25519"

function Invoke-Ssh {
    param([string]$Cmd)
    $arr = @("-p", "$SshPort", "-o", "BatchMode=yes")
    if (Test-Path $sshKey) { $arr += @("-i", $sshKey) }
    $arr += @($sshTarget, $Cmd)
    $output = & ssh.exe @arr 2>&1
    $code = $LASTEXITCODE
    foreach ($line in $output) { Write-Host $line }
    return $code
}

Write-Host ("=== Sync FactuTrust -> " + $sshTarget + ":" + $RemotePath + " ===")

$rc = Invoke-Ssh -Cmd "echo SSH_OK"
if ($rc -ne 0) { throw ("SSH failed to " + $sshTarget) }

# Ensure remote path exists and is writable by the SSH user
$parent = (Split-Path $RemotePath -Parent).Replace("\", "/")
$prep = "sudo mkdir -p '" + $RemotePath + "'; sudo chown -R " + $VmUser + ":" + $VmUser + " '" + $parent + "'"
$rc = Invoke-Ssh -Cmd $prep
if ($rc -ne 0) { throw "remote mkdir/chown failed" }

$excludes = @(
    "node_modules", "bin", "obj", "dist", ".git", ".vs", ".cursor", ".angular",
    "logs", "terminals", "agent-transcripts", "coverage", "TestResults",
    "artifacts", ".dotnet", ".agents", ".claude", ".codex", ".qoder", ".qodo",
    "deploy/.env", "deploy/.cache", "deploy/certs/*.pfx", "deploy/nginx/certs/*.pem",
    "*.png", "*.log", "*.mp4", "*.zip"
)

Push-Location $RepoRoot
try {
    Write-Host "Using tar stream..."
    $tarArgs = @("-czf", "-")
    foreach ($ex in $excludes) { $tarArgs += ("--exclude=" + $ex) }
    $tarArgs += "."
    $remoteCmd = "tar -xzf - -C '" + $RemotePath + "'"
    $pipeArr = @("-p", "$SshPort", "-o", "BatchMode=yes")
    if (Test-Path $sshKey) { $pipeArr += @("-i", $sshKey) }
    $pipeArr += @($sshTarget, $remoteCmd)
    & tar.exe @tarArgs | & ssh.exe @pipeArr
    if ($LASTEXITCODE -ne 0) { throw ("tar+ssh sync failed (exit " + $LASTEXITCODE + ")") }
}
finally {
    Pop-Location
}

$verify = "test -f '" + $RemotePath + "/deploy/docker-compose.yml'; test -f '" + $RemotePath + "/src/Backend/FactuTrust.sln'; echo SYNC_OK"
$rc = Invoke-Ssh -Cmd $verify
if ($rc -ne 0) { throw "Post-sync verification failed" }

Write-Host "Sync complete."
