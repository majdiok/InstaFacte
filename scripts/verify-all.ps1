# Porte qualité locale FactuTrust — mêmes étapes que .github/workflows/ci.yml.
# Usage : powershell -File scripts\verify-all.ps1
# S'arrête à la première étape en échec et affiche un récapitulatif.

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$steps = @()

function Invoke-Step {
    param([string]$Name, [scriptblock]$Action)
    Write-Host "`n=== $Name ===" -ForegroundColor Cyan
    $sw = [System.Diagnostics.Stopwatch]::StartNew()
    & $Action
    if ($LASTEXITCODE -ne 0) {
        Write-Host "ECHEC : $Name (exit $LASTEXITCODE)" -ForegroundColor Red
        exit $LASTEXITCODE
    }
    $sw.Stop()
    $script:steps += "[OK] $Name ($([int]$sw.Elapsed.TotalSeconds)s)"
}

Invoke-Step "Backend : build" {
    dotnet build (Join-Path $root 'src\Backend\FactuTrust.sln') --nologo -v q
}
Invoke-Step "Backend : tests Infrastructure" {
    dotnet test (Join-Path $root 'src\Backend\tests\FactuTrust.Infrastructure.Tests\FactuTrust.Infrastructure.Tests.csproj') --nologo --no-build -v minimal
}
Invoke-Step "Backend : tests API" {
    dotnet test (Join-Path $root 'src\Backend\tests\FactuTrust.API.Tests\FactuTrust.API.Tests.csproj') --nologo --no-build -v minimal
}
Invoke-Step "Web : npm audit (critical)" {
    Push-Location (Join-Path $root 'src\Frontend\factutrust-web')
    try { npm audit --audit-level=critical } finally { Pop-Location }
}
Invoke-Step "Web : specs Jasmine (headless)" {
    Push-Location (Join-Path $root 'src\Frontend\factutrust-web')
    try { npx ng test --watch=false --browsers=ChromeHeadless } finally { Pop-Location }
}
Invoke-Step "Web : build production" {
    Push-Location (Join-Path $root 'src\Frontend\factutrust-web')
    try { npm run build:prod } finally { Pop-Location }
}
Invoke-Step "Backoffice : npm audit (critical)" {
    Push-Location (Join-Path $root 'src\Frontend\factutrust-backoffice')
    try { npm audit --audit-level=critical } finally { Pop-Location }
}
Invoke-Step "Backoffice : build" {
    Push-Location (Join-Path $root 'src\Frontend\factutrust-backoffice')
    try { npx ng build --configuration production } finally { Pop-Location }
}

Write-Host "`n=== RECAPITULATIF ===" -ForegroundColor Green
$steps | ForEach-Object { Write-Host $_ }
Write-Host "Toutes les verifications sont passees." -ForegroundColor Green
