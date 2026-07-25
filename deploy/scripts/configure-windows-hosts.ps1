# Ajoute factutrust.local -> IP VM dans le fichier hosts Windows (admin requis).
param(
    [Parameter(Mandatory = $true)]
    [string]$VmHost,
    [string]$LabDomain = "factutrust.local"
)

$hostsPath = "$env:SystemRoot\System32\drivers\etc\hosts"
$line = "${VmHost}`t${LabDomain}"

$content = Get-Content $hostsPath -ErrorAction Stop
if ($content -match [regex]::Escape($LabDomain)) {
    Write-Host "Entry for $LabDomain already exists in hosts."
    exit 0
}

Add-Content -Path $hostsPath -Value "`n# FactuTrust VMware lab`n$line"
Write-Host "Added: $line"
