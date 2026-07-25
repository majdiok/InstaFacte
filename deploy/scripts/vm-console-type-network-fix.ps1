# Envoie fix reseau + openssh dans la console VMware (clic + frappe clavier).
# Usage :
#   $env:VM_GUEST_PASSWORD = 'mot-de-passe-sabiko'
#   .\deploy\scripts\vm-console-type-network-fix.ps1
param(
    [string]$StaticIp = "192.168.1.100",
    [string]$Gateway = "192.168.1.1",
    [string]$GuestPassword = $env:VM_GUEST_PASSWORD,
    [string]$WindowTitle = "Ubuntu 64-bit"
)

Add-Type -AssemblyName System.Windows.Forms
Add-Type @"
using System;
using System.Drawing;
using System.Runtime.InteropServices;
public class Win32 {
  [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr hWnd);
  [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);
  [DllImport("user32.dll")] public static extern bool SetCursorPos(int X, int Y);
  [DllImport("user32.dll")] public static extern void mouse_event(int dwFlags, int dx, int dy, int dwData, int dwExtraInfo);
  public struct RECT { public int Left; public int Top; public int Right; public int Bottom; }
  public const int MOUSEEVENTF_LEFTDOWN = 0x02;
  public const int MOUSEEVENTF_LEFTUP = 0x04;
  public static void ClickCenter(IntPtr hWnd) {
    RECT r; GetWindowRect(hWnd, out r);
    int x = r.Left + (r.Right - r.Left) / 2;
    int y = r.Top + (r.Bottom - r.Top) / 2 + 40;
    SetCursorPos(x, y);
    mouse_event(MOUSEEVENTF_LEFTDOWN, 0, 0, 0, 0);
    mouse_event(MOUSEEVENTF_LEFTUP, 0, 0, 0, 0);
  }
}
"@

$proc = Get-Process -Name "vmware" -ErrorAction SilentlyContinue | Where-Object { $_.MainWindowTitle -like "*$WindowTitle*" } | Select-Object -First 1
if (-not $proc) {
    Write-Host "Fenêtre VMware introuvable."
    exit 1
}

[Win32]::SetForegroundWindow($proc.MainWindowHandle) | Out-Null
Start-Sleep -Milliseconds 500
[Win32]::ClickCenter($proc.MainWindowHandle)
Start-Sleep -Milliseconds 500

function Send-Cmd([string]$text) {
    [System.Windows.Forms.SendKeys]::SendWait($text)
    Start-Sleep -Milliseconds 900
}

function Send-Sudo([string]$cmd) {
    if ($GuestPassword) {
        Send-Cmd ("echo " + $GuestPassword + " | sudo -S " + $cmd + "{ENTER}")
    } else {
        Send-Cmd ("sudo " + $cmd + "{ENTER}")
        Write-Host "WARN: VM_GUEST_PASSWORD absent - saisir sudo manuellement si demande"
        Start-Sleep -Seconds 8
    }
}

Write-Host "Ouverture terminal Ubuntu (Ctrl+Alt+T)..."
Send-Cmd "^%+t"
Start-Sleep -Seconds 2

Send-Sudo "ip addr flush dev ens33"
Send-Sudo ("ip addr add " + $StaticIp + "/24 dev ens33")
Send-Sudo "ip link set ens33 up"
Send-Sudo ("ip route replace default via " + $Gateway + " dev ens33")
Send-Sudo "bash -c ""echo -e 'nameserver 8.8.8.8\nnameserver 1.1.1.1' > /etc/resolv.conf"""
Send-Cmd ("ping -c 2 " + $Gateway + "{ENTER}")
Send-Cmd "ping -c 2 8.8.8.8{ENTER}"
Send-Sudo "apt update"
Send-Sudo "DEBIAN_FRONTEND=noninteractive apt install -y openssh-server"
Send-Sudo "systemctl enable --now ssh"
Send-Cmd "ip -4 addr show ens33{ENTER}"

Write-Host "Commandes envoyees. Attendre ~3 min puis tester :"
Write-Host ('  ssh sabiko@' + $StaticIp)
