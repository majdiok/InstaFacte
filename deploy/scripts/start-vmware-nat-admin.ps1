Start-Service VMnetDHCP
Start-Service "VMware NAT Service"
Get-Service VMnetDHCP,"VMware NAT Service" | Format-Table Name, Status -AutoSize
Get-NetIPAddress -AddressFamily IPv4 | Where-Object { $_.InterfaceAlias -match "VMnet8" } | Format-Table InterfaceAlias, IPAddress -AutoSize
pause
