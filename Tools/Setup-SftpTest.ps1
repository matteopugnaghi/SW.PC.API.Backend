<#
.SYNOPSIS
    Setup local OpenSSH/SFTP server for testing OPC UA certificate exchange.
    MUST RUN AS ADMINISTRATOR.
.DESCRIPTION
    1. Installs OpenSSH Server if not present
    2. Configures SSH key authentication
    3. Creates test cert exchange folder
    4. Starts sshd service
#>

param(
    [string]$TestUser = $env:USERNAME,
    [int]$SshPort = 2222,
    [string]$CertsFolder = "C:\sftp-test\certs"
)

$ErrorActionPreference = "Stop"

# --- Check admin ---
$isAdmin = ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
if (-not $isAdmin) {
    Write-Host "[ERROR] This script must be run as Administrator!" -ForegroundColor Red
    Write-Host "Right-click PowerShell -> Run as Administrator, then run this script again." -ForegroundColor Yellow
    exit 1
}

Write-Host "=== SFTP Test Server Setup ===" -ForegroundColor Cyan
Write-Host "User: $TestUser"
Write-Host "Port: $SshPort"  
Write-Host "Certs folder: $CertsFolder"
Write-Host ""

# --- Step 1: Install OpenSSH Server ---
Write-Host "[1/5] Checking OpenSSH Server..." -ForegroundColor Green
$sshServer = Get-WindowsCapability -Online -Name "OpenSSH.Server*"
if ($sshServer.State -ne "Installed") {
    Write-Host "  Installing OpenSSH Server..."
    Add-WindowsCapability -Online -Name "OpenSSH.Server~~~~0.0.1.0"
    Write-Host "  OpenSSH Server installed." -ForegroundColor Green
} else {
    Write-Host "  OpenSSH Server already installed." -ForegroundColor Green
}

# --- Step 2: Configure sshd on custom port ---
Write-Host "[2/5] Configuring sshd on port $SshPort..." -ForegroundColor Green
$sshdConfig = "C:\ProgramData\ssh\sshd_config"

if (Test-Path $sshdConfig) {
    $config = Get-Content $sshdConfig -Raw
    
    # Set port
    if ($config -match "(?m)^#?\s*Port\s+\d+") {
        $config = $config -replace "(?m)^#?\s*Port\s+\d+", "Port $SshPort"
    } else {
        $config = "Port $SshPort`n" + $config
    }
    
    # Enable PubkeyAuthentication
    $config = $config -replace "(?m)^#?\s*PubkeyAuthentication\s+\w+", "PubkeyAuthentication yes"
    
    # Disable password authentication for security
    $config = $config -replace "(?m)^#?\s*PasswordAuthentication\s+\w+", "PasswordAuthentication no"
    
    # IMPORTANT: Comment out the admin authorized_keys override (last 2 lines in default config)
    # Otherwise admin users' keys are read from a different file
    $config = $config -replace "(?m)^(Match Group administrators)", "#`$1"
    $config = $config -replace "(?m)^(\s+AuthorizedKeysFile\s+__PROGRAMDATA__)", "#`$1"
    
    Set-Content $sshdConfig $config -Encoding UTF8
    Write-Host "  sshd_config updated (port=$SshPort, pubkey=yes, password=no)" -ForegroundColor Green
} else {
    Write-Host "  [WARNING] sshd_config not found at $sshdConfig" -ForegroundColor Yellow
}

# --- Step 3: Setup SSH key auth ---
Write-Host "[3/5] Setting up SSH key authentication..." -ForegroundColor Green
$sshDir = "C:\Users\$TestUser\.ssh"
$authKeysFile = Join-Path $sshDir "authorized_keys"

if (-not (Test-Path $sshDir)) {
    New-Item -ItemType Directory -Path $sshDir -Force | Out-Null
}

# Read our test public key
$projectRoot = Split-Path -Parent $PSScriptRoot
$pubKeyFile = Join-Path $projectRoot "Projects\A72.TOUTWP\config\sftp_key.pub"

if (Test-Path $pubKeyFile) {
    $pubKey = (Get-Content $pubKeyFile -Raw).Trim()
    
    # Add to authorized_keys if not already there
    $existingKeys = ""
    if (Test-Path $authKeysFile) {
        $existingKeys = Get-Content $authKeysFile -Raw
    }
    
    if ($existingKeys -notlike "*$pubKey*") {
        Add-Content $authKeysFile "`n$pubKey"
        Write-Host "  Public key added to authorized_keys" -ForegroundColor Green
    } else {
        Write-Host "  Public key already in authorized_keys" -ForegroundColor Green
    }
    
    # Fix permissions (Windows OpenSSH is strict about this)
    icacls $authKeysFile /inheritance:r /grant "${TestUser}:F" /grant "SYSTEM:F" | Out-Null
    Write-Host "  Permissions set on authorized_keys" -ForegroundColor Green
} else {
    Write-Host "  [ERROR] Public key not found at: $pubKeyFile" -ForegroundColor Red
    Write-Host "  Generate it first with: ssh-keygen -t ed25519" -ForegroundColor Yellow
}

# --- Step 4: Create test certs folder ---
Write-Host "[4/5] Creating test certs folder: $CertsFolder ..." -ForegroundColor Green
if (-not (Test-Path $CertsFolder)) {
    New-Item -ItemType Directory -Path $CertsFolder -Force | Out-Null
}

# Put a test file in it
$testCertInfo = "Test cert exchange folder - created $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')"
Set-Content (Join-Path $CertsFolder "README.txt") $testCertInfo
Write-Host "  Folder ready with README.txt" -ForegroundColor Green

# --- Step 5: Start/restart sshd ---
Write-Host "[5/5] Starting sshd service..." -ForegroundColor Green

# Add firewall rule
$fwRule = Get-NetFirewallRule -Name "SFTP-Test-$SshPort" -ErrorAction SilentlyContinue
if (-not $fwRule) {
    New-NetFirewallRule -Name "SFTP-Test-$SshPort" -DisplayName "SFTP Test (Port $SshPort)" -Direction Inbound -Protocol TCP -LocalPort $SshPort -Action Allow | Out-Null
    Write-Host "  Firewall rule added for port $SshPort" -ForegroundColor Green
}

# Set service to manual start and restart it
Set-Service sshd -StartupType Manual
Restart-Service sshd
Write-Host "  sshd service running on port $SshPort" -ForegroundColor Green

# --- Summary ---
Write-Host ""
Write-Host "=== SFTP Test Server Ready ===" -ForegroundColor Cyan
Write-Host "  Host:        localhost"
Write-Host "  Port:        $SshPort"
Write-Host "  User:        $TestUser"
Write-Host "  Auth:        SSH key (ed25519)"
Write-Host "  Remote path: /certs/ -> $CertsFolder (via SFTP chroot or absolute)"
Write-Host ""
Write-Host "  Test with:"
Write-Host "    sftp -P $SshPort -i `"Projects\A72.TOUTWP\config\sftp_key`" $TestUser@localhost"
Write-Host ""
Write-Host "  Update Excel config:"
Write-Host "    OpcUa_Sftp_Host = localhost"
Write-Host "    OpcUa_Sftp_Port = $SshPort"
Write-Host "    OpcUa_Sftp_User = $TestUser"
Write-Host "    OpcUa_Sftp_RemotePath = $CertsFolder"
Write-Host ""
Write-Host "  To stop:  Stop-Service sshd"
Write-Host "  To remove: Remove-WindowsCapability -Online -Name OpenSSH.Server~~~~0.0.1.0"
