<# 
    Start-SftpServer.ps1 - Start SFTP server for testing (runs as current user)
    Run from an ADMIN PowerShell window.
    This avoids the Windows OpenSSH service mode bug.
    Press Ctrl+C to stop.
#>
param([int]$Port = 2222)

$ErrorActionPreference = "Stop"

# Stop the service if running (it conflicts)
$svc = Get-Service sshd -ErrorAction SilentlyContinue
if ($svc -and $svc.Status -eq "Running") {
    Write-Host "Stopping sshd service (will run standalone instead)..."
    Stop-Service sshd -Force
}

# Create a temporary config for standalone operation
$tempConfig = "C:\sftp-test\sshd_config_standalone"
$config = @"
Port $Port
ListenAddress 0.0.0.0
PubkeyAuthentication yes
PasswordAuthentication yes
AuthorizedKeysFile .ssh/authorized_keys
HostKey C:/ProgramData/ssh/ssh_host_ed25519_key
HostKey C:/ProgramData/ssh/ssh_host_rsa_key
HostKey C:/ProgramData/ssh/ssh_host_ecdsa_key
Subsystem sftp C:/Windows/System32/OpenSSH/sftp-server.exe
"@

$utf8NoBom = New-Object System.Text.UTF8Encoding($false)
[System.IO.File]::WriteAllText($tempConfig, $config, $utf8NoBom)

Write-Host ""
Write-Host "=== SFTP Test Server (Standalone) ===" -ForegroundColor Cyan
Write-Host "  Port: $Port"
Write-Host "  Config: $tempConfig"
Write-Host "  Auth: SSH key from ~/.ssh/authorized_keys"
Write-Host "  Press Ctrl+C to stop"
Write-Host ""

# Run sshd in foreground (non-detach) mode
# -D = don't detach, -e = log to stderr
& "C:\Windows\System32\OpenSSH\sshd.exe" -D -e -f $tempConfig
