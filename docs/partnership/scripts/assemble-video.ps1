#Requires -Version 5.1
<#
.SYNOPSIS
  Assembles chapter videos + TTS audio into the final partnership MP4.

.DESCRIPTION
  Requires ffmpeg in PATH.
  Reads docs/partnership/expert-comptable/scenes.json

.EXAMPLE
  .\assemble-video.ps1
#>
param(
  [string]$ScenesFile = ""
)

$ErrorActionPreference = "Stop"

function Test-Ffmpeg {
  try {
    $null = & ffmpeg -version 2>&1 | Select-Object -First 1
    return $true
  } catch {
    return $false
  }
}

if (-not (Test-Ffmpeg)) {
  throw "ffmpeg is not installed or not in PATH. Install from https://ffmpeg.org/"
}

$ScriptDir = $PSScriptRoot
$BaseDir = Join-Path (Split-Path -Parent $ScriptDir) "expert-comptable"
if (-not $ScenesFile) {
  $ScenesFile = Join-Path $BaseDir "scenes.json"
}

$Scenes = Get-Content $ScenesFile -Raw -Encoding UTF8 | ConvertFrom-Json
$WorkDir = Join-Path $BaseDir "build"
$SegmentsDir = Join-Path $WorkDir "segments"
New-Item -ItemType Directory -Force -Path $WorkDir | Out-Null
New-Item -ItemType Directory -Force -Path $SegmentsDir | Out-Null

function Get-MediaDurationSeconds {
  param([string]$Path)
  $probe = & ffprobe -v error -show_entries format=duration -of default=noprint_wrappers=1:nokey=1 $Path 2>$null
  return [double]$probe
}

$segmentFiles = @()

foreach ($chapter in $Scenes.chapters) {
  $videoPath = Join-Path $BaseDir $chapter.video
  $audioPath = Join-Path $BaseDir $chapter.audio
  $segmentOut = Join-Path $SegmentsDir ("segment-{0}.mp4" -f $chapter.id)

  if (-not (Test-Path $videoPath)) {
    Write-Warning "Missing video: $videoPath - skipping chapter $($chapter.id)"
    continue
  }
  if (-not (Test-Path $audioPath)) {
    Write-Warning "Missing audio: $audioPath - skipping chapter $($chapter.id)"
    continue
  }

  $audioDur = Get-MediaDurationSeconds $audioPath
  Write-Host "Building segment $($chapter.id) - $($chapter.title) (audio ${audioDur}s)..."

  $filterComplex = '[0:v]setpts=PTS-STARTPTS,scale=1920:1080:force_original_aspect_ratio=decrease,pad=1920:1080:(ow-iw)/2:(oh-ih)/2[v]'

  $prevEap = $ErrorActionPreference
  $ErrorActionPreference = 'Continue'
  & ffmpeg -y `
    -i $videoPath `
    -i $audioPath `
    -filter_complex $filterComplex `
    -map "[v]" `
    -map "1:a" `
    -c:v libx264 `
    -preset medium `
    -crf 23 `
    -c:a aac `
    -b:a 192k `
    -r 30 `
    -shortest `
    -t $audioDur `
    $segmentOut 2>&1 | Out-Null
  $ErrorActionPreference = $prevEap

  if ($LASTEXITCODE -ne 0 -or -not (Test-Path $segmentOut)) {
    throw "ffmpeg failed for segment $($chapter.id)"
  }

  $segmentFiles += $segmentOut
}

if ($segmentFiles.Count -eq 0) {
  throw "No segments were created. Run docs:partnership-video and generate-tts.ps1 first."
}

$concatList = Join-Path $WorkDir "concat.txt"
$lines = $segmentFiles | ForEach-Object { "file '$($_.Replace('\', '/'))'" }
$lines -join "`n" | Set-Content -Path $concatList -Encoding ASCII

$finalOut = Join-Path $BaseDir $Scenes.output
Write-Host "Concatenating $($segmentFiles.Count) segments -> $finalOut"

$prevEap = $ErrorActionPreference
$ErrorActionPreference = 'Continue'
& ffmpeg -y -f concat -safe 0 -i $concatList -c copy $finalOut 2>&1 | Out-Null
$ErrorActionPreference = $prevEap

if ($LASTEXITCODE -ne 0 -or -not (Test-Path $finalOut)) {
  throw "Final video was not created: $finalOut"
}

$totalDur = Get-MediaDurationSeconds $finalOut
Write-Host ""
Write-Host "Done: $finalOut (${totalDur}s)"
