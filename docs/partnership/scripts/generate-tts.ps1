#Requires -Version 5.1
<#
.SYNOPSIS
  Generates French TTS narration for the expert-comptable partnership video (Edge TTS).

.DESCRIPTION
  Requires Python with edge-tts: pip install edge-tts
  Output: docs/partnership/expert-comptable/audio/chapitre-XX.mp3

.EXAMPLE
  .\generate-tts.ps1
  .\generate-tts.ps1 -Voice fr-FR-HenriNeural
#>
param(
  [string]$Voice = "fr-FR-DeniseNeural"
)

$ErrorActionPreference = "Stop"
$PartnershipDir = Split-Path -Parent $PSScriptRoot
$BaseDir = Join-Path $PartnershipDir "expert-comptable"
$AudioDir = Join-Path $BaseDir "audio"
$TextDir = Join-Path $AudioDir "texts"

New-Item -ItemType Directory -Force -Path $AudioDir | Out-Null
New-Item -ItemType Directory -Force -Path $TextDir | Out-Null

function Test-EdgeTts {
  try {
    $null = & python -m edge_tts --version 2>&1
    return $true
  } catch {
    return $false
  }
}

if (-not (Test-EdgeTts)) {
  Write-Host "Installing edge-tts via pip..."
  & python -m pip install edge-tts --quiet
}

$Chapters = @{
  "01" = @"
Bienvenue dans FactuTrust, la plateforme tout-en-un pensée pour les PME tunisiennes.
Pour votre cabinet comptable, elle centralise facturation, trésorerie et comptabilité dans un seul environnement sécurisé.
Conformité TEJ pour les retenues à la source, exports comptables structurés, et journal d'audit inviolable : vos clients restent en règle, et vous gagnez du temps sur le contrôle et la reprise de données.
Découvrons ensemble les écrans qui comptent le plus pour un partenariat commercial durable.
"@
  "02" = @"
Après connexion, le tableau de bord offre une vision instantanée de l'activité : chiffre d'affaires, factures en attente, encaissements récents.
Votre client PME pilote son activité sans tableur ; vous, vous accédez aux mêmes indicateurs avec le rôle Comptable, en lecture ou en contrôle selon les droits accordés.
C'est le point d'entrée idéal pour accompagner plusieurs dossiers clients depuis un seul outil multi-tenant.
"@
  "03" = @"
Le cœur de FactuTrust : la facturation électronique conforme.
En quelques clics, votre client émet une facture validée, téléchargeable en PDF, avec signature électronique et traçabilité légale.
L'avantage décisif pour le cabinet : dès la validation, les écritures comptables sont générées automatiquement dans le journal des ventes.
Plus de ressaisie manuelle : vous contrôlez, vous ajustez si nécessaire, mais vous ne repartez jamais de zéro.
Ici, le journal JV affiche les écritures liées aux factures — débit client, crédit ventes et TVA collectée.
"@
  "04" = @"
La trésorerie est nativement connectée à la comptabilité.
Chaque encaissement client est enregistré avec son mode de paiement et alimente les écritures de caisse ou de banque.
Les comptes bancaires sont paramétrables par entrepôt ou par société.
Pour le cabinet, cela signifie des rapprochements plus simples et une piste d'audit claire entre facture, paiement et écriture comptable.
"@
  "05" = @"
FactuTrust intègre un module comptable complet, aligné sur le plan comptable tunisien.
Le plan comptable est pré-configuré et extensible. Le journal et le grand livre permettent le contrôle ligne à ligne.
La balance générale à huit colonnes, le lettrage des comptes clients et fournisseurs, et la déclaration de TVA préremplie accélèrent vos revues mensuelles.
Le bilan et le compte de résultat closent la boucle pour le reporting de fin d'exercice.
Enfin, l'export FEC — Fichier des Écritures Comptables — est disponible en un clic depuis l'écran de clôture, prêt à transmettre à l'administration ou à intégrer dans vos outils de révision.
"@
  "06" = @"
Côté fiscal tunisien, FactuTrust gère les retenues à la source et la déclaration TEJ.
Le tableau de bord fiscal synthétise les retenues par nature de paiement.
L'export TEJ génère le fichier XML conforme au format attendu par l'administration : prévisualisation, puis téléchargement pour dépôt sur le portail officiel.
Vos clients restent conformes ; votre cabinet sécurise les déclarations sans tableurs fragiles.
"@
  "07" = @"
La gouvernance est au centre de la confiance.
Le journal d'audit enregistre chaque action sensible : validation de facture, export FEC, clôture de période.
Les rôles prédéfinis — dont le rôle Comptable — limitent l'accès au strict nécessaire.
Vous déployez FactuTrust chez vos clients PME tout en conservant la visibilité et le contrôle adaptés à votre mission de conseil.
"@
  "08" = @"
FactuTrust est prêt pour un modèle partenariat gagnant-gagnant : vous recommandez une solution premium à vos clients PME, vous réduisez la charge de ressaisie, et vous valorisez votre expertise sur la conformité TEJ et la comptabilité.
POS, CRM et prévisions IA complètent l'offre pour les clients plus matures.
Contactez-nous pour co-construire votre offre cabinet : démo personnalisée, formation et conditions partenaires dédiées.
FactuTrust — la gestion commerciale intelligente, pensée pour la Tunisie.
"@
}

foreach ($id in $Chapters.Keys | Sort-Object) {
  $textFile = Join-Path $TextDir "chapitre-$id.txt"
  $mp3File = Join-Path $AudioDir "chapitre-$id.mp3"
  $Chapters[$id].Trim() | Set-Content -Path $textFile -Encoding UTF8

  Write-Host "Generating TTS: chapitre-$id.mp3 ($Voice)..."
  & python -m edge_tts `
    --voice $Voice `
    --file $textFile `
    --write-media $mp3File

  if (-not (Test-Path $mp3File)) {
    throw "Failed to generate $mp3File"
  }
}

Write-Host "`nDone. Audio files in: $AudioDir"
