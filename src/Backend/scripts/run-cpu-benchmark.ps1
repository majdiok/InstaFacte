# Login tenant demo + benchmark assistant IA en mode CPU.
# Prérequis : API démarrée, Ollama disponible, modèle chat installé.
#
# Usage :
#   .\run-cpu-benchmark.ps1
#   .\run-cpu-benchmark.ps1 -Scenario S2 -Runs 1
#   .\run-cpu-benchmark.ps1 -SkipPlatformCpuCheck

param(
    [ValidateSet("S1", "S2", "S3", "all")]
    [string] $Scenario = "all",
    [int] $Runs = 3,
    [string] $ApiBase = "https://localhost:7001",
    [string] $DemoCredentialsPath = "",
    [switch] $SkipPlatformCpuCheck
)

$ErrorActionPreference = "Stop"

if ([string]::IsNullOrWhiteSpace($DemoCredentialsPath)) {
    $DemoCredentialsPath = Join-Path $PSScriptRoot "..\..\..\docs\partnership\expert-comptable\.demo-credentials.json"
}

if (-not (Test-Path $DemoCredentialsPath)) {
    throw "Fichier credentials introuvable : $DemoCredentialsPath"
}

$creds = Get-Content $DemoCredentialsPath -Raw | ConvertFrom-Json
$loginBody = @{ email = $creds.email; password = $creds.password } | ConvertTo-Json
$login = Invoke-RestMethod -Method Post -Uri "$ApiBase/api/auth/login" -ContentType "application/json" -Body $loginBody -SkipCertificateCheck
if (-not $login.success) { throw "Login tenant échoué : $($login.message)" }
$tenantToken = $login.data.accessToken
Write-Host "Tenant connecté : $($creds.email)"

if (-not $SkipPlatformCpuCheck) {
    $appsettings = Join-Path $PSScriptRoot "..\FactuTrust.API\appsettings.json"
    if (Test-Path $appsettings) {
        $bootstrap = (Get-Content $appsettings -Raw | ConvertFrom-Json).Bootstrap.PlatformAdmin
        if ($bootstrap.Email -and $bootstrap.Password) {
            $pLoginBody = @{ email = $bootstrap.Email; password = $bootstrap.Password } | ConvertTo-Json
            try {
                $pLogin = Invoke-RestMethod -Method Post -Uri "$ApiBase/api/platform/auth/login" -ContentType "application/json" -Body $pLoginBody -SkipCertificateCheck
                if ($pLogin.success) {
                    $pHeaders = @{ Authorization = "Bearer $($pLogin.data.accessToken)" }
                    $ai = Invoke-RestMethod -Method Get -Uri "$ApiBase/api/platform/ai-settings" -Headers $pHeaders -SkipCertificateCheck
                    Write-Host "Plateforme : inferenceDevice=$($ai.data.inferenceDevice) model=$($ai.data.configuredModelRef)"
                    if ($ai.data.inferenceDevice -ne "CpuOnly") {
                        Write-Warning "Le back-office n'est pas en CpuOnly. Passez en CPU uniquement pour un benchmark représentatif."
                    }
                }
            } catch {
                Write-Warning "Vérification plateforme ignorée : $_"
            }
        }
    }
}

& (Join-Path $PSScriptRoot "ai-chat-benchmark.ps1") `
    -BearerToken $tenantToken `
    -ApiUrl "$ApiBase/api/ai/chat" `
    -Scenario $Scenario `
    -Runs $Runs