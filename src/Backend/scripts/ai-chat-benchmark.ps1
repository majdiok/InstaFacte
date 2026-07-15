# Mesure P50/P95 latence assistant IA (POST /api/ai/chat, SSE).
# Corréler avec les logs serveur via X-Trace-Id (en-tête de réponse).
#
# Prérequis :
#   - API FactuTrust démarrée, Ollama disponible, modèle chat installé
#   - JWT tenant valide
#
# Exemple :
#   .\ai-chat-benchmark.ps1 -BearerToken "<jwt>" -ApiUrl "https://localhost:7001/api/ai/chat"
#
# Scénarios (paramètre -Scenario) :
#   S1 = CA du mois (2 tours LLM typiques)
#   S2 = salutation (0 outil)
#   S3 = analyse écran invoice-list (nécessite -AnalysisSummaryPath)

param(
    [Parameter(Mandatory = $true)]
    [string] $BearerToken,
    [string] $ApiUrl = "https://localhost:7001/api/ai/chat",
    [ValidateSet("S1", "S2", "S3", "all")]
    [string] $Scenario = "all",
    [int] $Runs = 3,
    [string] $AnalysisSummaryPath = ""
)

$scenarios = @{
    S1 = @{
        Label = "CA du mois"
        Body = @{
            message = "Quel est mon chiffre d'affaires ce mois-ci ?"
        }
    }
    S2 = @{
        Label = "Salutation"
        Body = @{
            message = "Bonjour"
        }
    }
    S3 = @{
        Label = "Analyse ecran invoice-list"
        Body = @{
            message = "Analyse cette liste de factures."
            options = @{ assistantMode = 2 }
            uiContext = @{
                screenId = "invoice-list"
                analysisSummary = ""
            }
        }
    }
}

function Invoke-ChatBenchmark {
    param($Label, $BodyJson)
    $times = @()
    $firstTokens = @()

    1..$Runs | ForEach-Object {
        $sw = [System.Diagnostics.Stopwatch]::StartNew()
        $firstTokenMs = $null
        $traceId = $null

        $bodyFile = [System.IO.Path]::GetTempFileName()
        try {
            [System.IO.File]::WriteAllText($bodyFile, $BodyJson, [System.Text.UTF8Encoding]::new($false))
            $response = curl.exe -sk -N -X POST $ApiUrl `
                -H "Authorization: Bearer $BearerToken" `
                -H "Content-Type: application/json" `
                -H "Accept: text/event-stream" `
                --data-binary "@$bodyFile" `
                -D - 2>$null

            foreach ($line in ($response -split "`n")) {
                if ($line -match '^[Xx]-[Tt]race-[Ii]d:\s*(.+)$') {
                    $traceId = $Matches[1].Trim()
                }
                if ($line -match '^data:\s*(.+)$') {
                    $json = $Matches[1].Trim()
                    try {
                        $evt = $json | ConvertFrom-Json
                        if ($null -eq $firstTokenMs -and $evt.type -eq 'content') {
                            $firstTokenMs = $sw.ElapsedMilliseconds
                        }
                    } catch { }
                }
            }
        } catch {
            Write-Warning "Run $_ failed: $_"
        } finally {
            if (Test-Path $bodyFile) { Remove-Item -LiteralPath $bodyFile -Force -ErrorAction SilentlyContinue }
        }

        $sw.Stop()
        $totalMs = $sw.ElapsedMilliseconds
        $times += $totalMs
        if ($null -ne $firstTokenMs) { $firstTokens += $firstTokenMs }
        $ft = if ($null -ne $firstTokenMs) { "${firstTokenMs}ms" } else { "n/a" }
        Write-Host "  Run $_ : total=${totalMs}ms first_token=$ft trace=$traceId"
    }

    $sorted = $times | Sort-Object
    $p50 = $sorted[[int][math]::Floor(($sorted.Count - 1) * 0.5)]
    $p95 = $sorted[[int][math]::Floor(($sorted.Count - 1) * 0.95)]
    Write-Host "[$Label] total P50=${p50}ms P95=${p95}ms (n=$($sorted.Count))"

    if ($firstTokens.Count -gt 0) {
        $ftSorted = $firstTokens | Sort-Object
        $ftP50 = $ftSorted[[int][math]::Floor(($ftSorted.Count - 1) * 0.5)]
        $ftP95 = $ftSorted[[int][math]::Floor(($ftSorted.Count - 1) * 0.95)]
        Write-Host "[$Label] first_token P50=${ftP50}ms P95=${ftP95}ms"
    }
    Write-Host ""
}

if ($Scenario -eq "S3" -or $Scenario -eq "all") {
    if ([string]::IsNullOrWhiteSpace($AnalysisSummaryPath)) {
        $golden = Join-Path $PSScriptRoot "..\..\..\docs\ai-screen-analysis\golden\invoice-list.json"
        if (Test-Path $golden) {
            $AnalysisSummaryPath = $golden
        }
    }
    if (-not [string]::IsNullOrWhiteSpace($AnalysisSummaryPath) -and (Test-Path $AnalysisSummaryPath)) {
        $summary = Get-Content -Raw -Path $AnalysisSummaryPath
        $scenarios.S3.Body.uiContext.analysisSummary = $summary
    } else {
        Write-Warning "S3 ignoré : fichier golden invoice-list.json introuvable."
        if ($Scenario -eq "S3") { exit 1 }
    }
}

$toRun = if ($Scenario -eq "all") { @("S1", "S2", "S3") } else { @($Scenario) }

foreach ($key in $toRun) {
    if ($null -eq $scenarios[$key]) { continue }
    $def = $scenarios[$key]
    $json = $def.Body | ConvertTo-Json -Depth 10 -Compress
    Write-Host "=== $($def.Label) ($key) ==="
    Invoke-ChatBenchmark -Label $def.Label -BodyJson $json
}

Write-Host "Consulter les logs API : AI chat phase=... correlationId=<X-Trace-Id>"