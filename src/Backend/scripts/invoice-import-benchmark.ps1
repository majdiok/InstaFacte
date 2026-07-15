# Mesure P50/P95 sur 5 imports consécutifs du même PDF (hors CI).
#
# Checklist déploiement on-premise (Ollama local) :
# 1. ollama pull qwen2.5:7b-instruct
# 2. Ollama:InvoiceImportModel != DefaultModel si le chat utilise un gros modèle
# 3. RAM libre >= 1.5x taille du modèle d'import
# 4. Ollama:KeepAliveEnabled=true en production
# 5. Tessdata présent pour PDF scannés (Resources/Tessdata/)
# 6. Import golden PDF < 60 s sur poste de référence
# Exemple :
#   $token = "<jwt>"
#   $pdf = "Facture_FAC-2026-000093.pdf"
#   1..5 | ForEach-Object { Measure-Command {
#     curl.exe -sk -F "file=@$pdf" "https://localhost:7001/api/ai/invoice-import" -H "Authorization: Bearer $token"
#   } }

param(
    [Parameter(Mandatory = $true)]
    [string] $PdfPath,
    [Parameter(Mandatory = $true)]
    [string] $BearerToken,
    [string] $ApiUrl = "https://localhost:7001/api/ai/invoice-import",
    [int] $Runs = 5
)

$times = @()
1..$Runs | ForEach-Object {
    $sw = [System.Diagnostics.Stopwatch]::StartNew()
    curl.exe -sk -F "file=@$PdfPath" $ApiUrl -H "Authorization: Bearer $BearerToken" | Out-Null
    $sw.Stop()
    $sec = [math]::Round($sw.Elapsed.TotalSeconds, 1)
    $times += $sec
    Write-Host "Run $_ : ${sec}s"
}

$sorted = $times | Sort-Object
$p50 = $sorted[[int][math]::Floor(($sorted.Count - 1) * 0.5)]
$p95 = $sorted[[int][math]::Floor(($sorted.Count - 1) * 0.95)]
Write-Host "P50=${p50}s P95=${p95}s (n=$($sorted.Count))"
