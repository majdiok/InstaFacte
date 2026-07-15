# Télécharge les modèles tessdata_fast (fra, eng, ara) pour l'OCR import facture.
$ErrorActionPreference = "Stop"
$base = "https://github.com/tesseract-ocr/tessdata_fast/raw/main"
$dest = Join-Path $PSScriptRoot "..\FactuTrust.Infrastructure\Resources\Tessdata"
New-Item -ItemType Directory -Force -Path $dest | Out-Null

foreach ($lang in @("fra", "eng", "ara")) {
    $file = Join-Path $dest "$lang.traineddata"
    if (Test-Path $file) {
        Write-Host "OK $lang (déjà présent)"
        continue
    }
    Write-Host "Téléchargement $lang.traineddata..."
    Invoke-WebRequest -Uri "$base/$lang.traineddata" -OutFile $file -UseBasicParsing
}

Write-Host "Terminé. Recompilez FactuTrust.API pour copier les fichiers vers bin/Resources/Tessdata."
