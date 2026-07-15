# Validation rapide GET/PUT /api/platform/ai-settings (inferenceDevice).
# Usage :
#   $env:PLATFORM_ADMIN_EMAIL = "admin@example.com"
#   $env:PLATFORM_ADMIN_PASSWORD = "secret"
#   .\scripts\smoke-platform-ai-inference.ps1
# Ou sans variables : lit Bootstrap:PlatformAdmin depuis appsettings.json (API).

param(
    [string]$ApiBase = "https://localhost:7001",
    [switch]$SkipTlsCheck
)

$ErrorActionPreference = "Stop"
if ($SkipTlsCheck) {
    if (-not ([System.Management.Automation.PSTypeName]'TrustAllCertsPolicy').Type) {
        Add-Type @"
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
public class TrustAllCertsPolicy {
  public static bool Validator(object sender, X509Certificate cert, X509Chain chain, SslPolicyErrors errors) => true;
}
"@
    }
    [System.Net.ServicePointManager]::ServerCertificateValidationCallback = { $true }
}

function Get-BootstrapCredentials {
    if ($env:PLATFORM_ADMIN_EMAIL -and $env:PLATFORM_ADMIN_PASSWORD) {
        return @{ Email = $env:PLATFORM_ADMIN_EMAIL; Password = $env:PLATFORM_ADMIN_PASSWORD }
    }
    $appsettings = Join-Path $PSScriptRoot "..\FactuTrust.API\appsettings.json"
    if (-not (Test-Path $appsettings)) { throw "appsettings introuvable : $appsettings" }
    $json = Get-Content $appsettings -Raw | ConvertFrom-Json
    $email = $json.Bootstrap.PlatformAdmin.Email
    $password = $json.Bootstrap.PlatformAdmin.Password
    if (-not $email -or -not $password) {
        throw "Renseignez PLATFORM_ADMIN_EMAIL / PLATFORM_ADMIN_PASSWORD ou Bootstrap:PlatformAdmin dans appsettings."
    }
    return @{ Email = $email; Password = $password }
}

$creds = Get-BootstrapCredentials
$loginBody = @{ email = $creds.Email; password = $creds.Password } | ConvertTo-Json
$login = Invoke-RestMethod -Method Post -Uri "$ApiBase/api/platform/auth/login" -ContentType "application/json" -Body $loginBody
if (-not $login.success) { throw "Login échoué : $($login.message)" }
$token = $login.data.accessToken
$headers = @{ Authorization = "Bearer $token" }

Write-Host "GET /api/platform/ai-settings ..."
$get = Invoke-RestMethod -Method Get -Uri "$ApiBase/api/platform/ai-settings" -Headers $headers
$device = $get.data.inferenceDevice
Write-Host "  inferenceDevice actuel : $device"

Write-Host "PUT CpuOnly ..."
$putCpu = Invoke-RestMethod -Method Put -Uri "$ApiBase/api/platform/ai-settings" -Headers $headers `
    -ContentType "application/json" -Body '{"inferenceDevice":"CpuOnly"}'
if ($putCpu.data.inferenceDevice -ne "CpuOnly") {
    throw "PUT CpuOnly : attendu CpuOnly, reçu $($putCpu.data.inferenceDevice)"
}
Write-Host "  OK -> CpuOnly"

Write-Host "PUT Gpu (restauration) ..."
$putGpu = Invoke-RestMethod -Method Put -Uri "$ApiBase/api/platform/ai-settings" -Headers $headers `
    -ContentType "application/json" -Body '{"inferenceDevice":"Gpu"}'
if ($putGpu.data.inferenceDevice -ne "Gpu") {
    throw "PUT Gpu : attendu Gpu, reçu $($putGpu.data.inferenceDevice)"
}
Write-Host "  OK -> Gpu"

Write-Host "Smoke test inferenceDevice : SUCCÈS"