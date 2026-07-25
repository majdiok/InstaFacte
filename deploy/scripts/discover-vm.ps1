# Cherche la VM Ubuntu (SSH port 22) sur le sous-réseau local.
param(
    [string]$SubnetPrefix = "192.168.179",
    [int]$Start = 2,
    [int]$End = 254,
    [string[]]$PreferHosts = @("192.168.179.100", "192.168.1.100", "192.168.1.101")
)

Write-Host "Scan SSH ${SubnetPrefix}.${Start}-${End} ..."

foreach ($ip in $PreferHosts) {
    $tcp = Test-NetConnection -ComputerName $ip -Port 22 -WarningAction SilentlyContinue -ErrorAction SilentlyContinue
    if ($tcp.TcpTestSucceeded) {
        Write-Host "FOUND (prefer): ${ip}:22"
        Write-Output $ip
        exit 0
    }
}

for ($i = $Start; $i -le $End; $i++) {
    $ip = "${SubnetPrefix}.${i}"
    if ($PreferHosts -contains $ip) { continue }
    $tcp = Test-NetConnection -ComputerName $ip -Port 22 -WarningAction SilentlyContinue -ErrorAction SilentlyContinue
    if ($tcp.TcpTestSucceeded) {
        Write-Host "FOUND: ${ip}:22"
        Write-Output $ip
        exit 0
    }
}

Write-Host "Aucune VM SSH trouvée sur ${SubnetPrefix}.x"
exit 1
