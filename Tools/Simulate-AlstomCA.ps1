#!/usr/bin/env pwsh
# ================================================================
# Simulate-AlstomCA.ps1
# Simulates the Alstom CA certificate signing process
# 
# In production, Alstom's CA does this automatically.
# This script replicates the same flow for local testing.
# ================================================================

param(
    [string]$Action = "help"  # sign-client | sign-server | show-status
)

$ErrorActionPreference = "Stop"

# Paths
$configPath = Join-Path $PSScriptRoot "..\Projects\A72.TOUTWP\config"
$caCertDer = Join-Path $configPath "alstom_ca_root.der"
$opcuaCertsBase = Join-Path $env:LOCALAPPDATA "Aquafrisch\opcua-certs"

Write-Host ""
Write-Host "=== Alstom CA Simulator (Test Only) ===" -ForegroundColor Cyan
Write-Host ""

# Find CA cert in Windows cert store
$caCert = Get-ChildItem Cert:\CurrentUser\My | Where-Object { $_.Subject -like "*Test Alstom CA Root*" } | Select-Object -First 1
if (-not $caCert) {
    Write-Host "[ERROR] Test CA cert not found in Windows cert store!" -ForegroundColor Red
    exit 1
}
Write-Host "[OK] CA Root: $($caCert.Subject)" -ForegroundColor Green
Write-Host "     Thumbprint: $($caCert.Thumbprint)" -ForegroundColor Gray
Write-Host ""

function Sign-Certificate {
    param(
        [string]$InputDerPath,
        [string]$OutputDerPath,
        [string]$Label
    )
    
    Write-Host "[SIGNING] $Label" -ForegroundColor Yellow
    Write-Host "  Input:  $InputDerPath" -ForegroundColor Gray
    
    $origCert = New-Object System.Security.Cryptography.X509Certificates.X509Certificate2($InputDerPath)
    Write-Host "  Subject: $($origCert.Subject)" -ForegroundColor Gray
    Write-Host "  Original Issuer: $($origCert.Issuer)" -ForegroundColor Gray
    
    $subjectName = $origCert.Subject
    
    # Create new cert signed by our test CA
    $signedCert = New-SelfSignedCertificate `
        -Subject $subjectName `
        -KeyAlgorithm RSA `
        -KeyLength 2048 `
        -HashAlgorithm SHA256 `
        -CertStoreLocation "Cert:\CurrentUser\My" `
        -Signer $caCert `
        -NotAfter (Get-Date).AddYears(2) `
        -KeyUsage DigitalSignature, KeyEncipherment, DataEncipherment `
        -TextExtension @(
            "2.5.29.37={text}1.3.6.1.5.5.7.3.1,1.3.6.1.5.5.7.3.2"
        )
    
    # Export as DER
    $derBytes = $signedCert.Export([System.Security.Cryptography.X509Certificates.X509ContentType]::Cert)
    [System.IO.File]::WriteAllBytes($OutputDerPath, $derBytes)
    
    Write-Host "  [OK] Signed certificate created!" -ForegroundColor Green
    Write-Host "  Output: $OutputDerPath" -ForegroundColor Gray
    Write-Host "  New Issuer: $($signedCert.Issuer)" -ForegroundColor Green
    Write-Host "  Thumbprint: $($signedCert.Thumbprint)" -ForegroundColor Gray
    Write-Host "  Valid until: $($signedCert.NotAfter)" -ForegroundColor Gray
    Write-Host ""
    
    # Cleanup from Windows store
    Remove-Item "Cert:\CurrentUser\My\$($signedCert.Thumbprint)" -ErrorAction SilentlyContinue
    
    return $OutputDerPath
}

switch ($Action) {
    "sign-client" {
        Write-Host "[ACTION] Signing CLIENT certificate (UaExpert) with CA..." -ForegroundColor Cyan
        Write-Host "  (Simulates: Alstom signs their client cert with same CA)" -ForegroundColor Gray
        Write-Host ""
        
        $rejectedPath = Join-Path $opcuaCertsBase "rejected\certs"
        $clientCerts = Get-ChildItem "$rejectedPath\*.der" -ErrorAction SilentlyContinue | Where-Object { $_.Name -notlike "Aquafrisch*" }
        
        if (-not $clientCerts -or $clientCerts.Count -eq 0) {
            Write-Host "[ERROR] No client certificates found in rejected store!" -ForegroundColor Red
            Write-Host "  Connect UaExpert first so its cert appears in rejected." -ForegroundColor Yellow
            exit 1
        }
        
        foreach ($clientCert in $clientCerts) {
            $outputPath = Join-Path $configPath "client_signed_by_ca.der"
            Sign-Certificate -InputDerPath $clientCert.FullName -OutputDerPath $outputPath -Label "Client: $($clientCert.Name)"
        }
        
        Write-Host "=== NEXT STEPS ===" -ForegroundColor Green
        Write-Host "  1. Import client_signed_by_ca.der in frontend (Certificates > Import .DER)" -ForegroundColor Green
        Write-Host "  2. Reconnect UaExpert --> should be ACCEPTED" -ForegroundColor Green
    }
    
    "sign-server" {
        Write-Host "[ACTION] Signing OUR server certificate with CA..." -ForegroundColor Cyan
        Write-Host "  (Simulates: we upload .DER to Alstom, they sign and return)" -ForegroundColor Gray
        Write-Host ""
        
        $ownCertPath = Get-ChildItem (Join-Path $opcuaCertsBase "own\certs\*.der") | Select-Object -First 1
        if (-not $ownCertPath) {
            Write-Host "[ERROR] No server certificate found in own store!" -ForegroundColor Red
            exit 1
        }
        
        $outputPath = Join-Path $configPath "server_signed_by_ca.der"
        Sign-Certificate -InputDerPath $ownCertPath.FullName -OutputDerPath $outputPath -Label "Our OPC UA Server cert"
        
        Write-Host "=== NEXT STEPS ===" -ForegroundColor Green
        Write-Host "  Import server_signed_by_ca.der in frontend (Certificates > Import .DER)" -ForegroundColor Green
    }
    
    "show-status" {
        Write-Host "[STATUS] Certificate Stores" -ForegroundColor Cyan
        Write-Host ""
        
        Write-Host "-- Own (server cert) --" -ForegroundColor Yellow
        Get-ChildItem (Join-Path $opcuaCertsBase "own\certs\*.der") -ErrorAction SilentlyContinue | ForEach-Object {
            $c = New-Object System.Security.Cryptography.X509Certificates.X509Certificate2($_.FullName)
            $type = if ($c.Subject -eq $c.Issuer) { "Self-Signed" } else { "CA-Signed by: $($c.Issuer)" }
            Write-Host "  $($c.Subject) --> $type" -ForegroundColor Gray
        }
        
        Write-Host ""
        Write-Host "-- Trusted --" -ForegroundColor Green
        Get-ChildItem (Join-Path $opcuaCertsBase "trusted\certs\*.der") -ErrorAction SilentlyContinue | ForEach-Object {
            $c = New-Object System.Security.Cryptography.X509Certificates.X509Certificate2($_.FullName)
            $type = if ($c.Subject -eq $c.Issuer) { "Self-Signed" } else { "CA-Signed" }
            Write-Host "  $($c.Subject) --> $type" -ForegroundColor Gray
        }
        
        Write-Host ""
        Write-Host "-- Rejected --" -ForegroundColor Red
        Get-ChildItem (Join-Path $opcuaCertsBase "rejected\certs\*.der") -ErrorAction SilentlyContinue | ForEach-Object {
            $c = New-Object System.Security.Cryptography.X509Certificates.X509Certificate2($_.FullName)
            $type = if ($c.Subject -eq $c.Issuer) { "Self-Signed" } else { "CA-Signed" }
            Write-Host "  $($c.Subject) --> $type" -ForegroundColor Gray
        }
        
        Write-Host ""
        Write-Host "-- Issuers (CA roots) --" -ForegroundColor Cyan
        Get-ChildItem (Join-Path $opcuaCertsBase "issuers\certs\*.der") -ErrorAction SilentlyContinue | ForEach-Object {
            $c = New-Object System.Security.Cryptography.X509Certificates.X509Certificate2($_.FullName)
            Write-Host "  $($c.Subject)" -ForegroundColor Gray
        }
        
        Write-Host ""
        Write-Host "-- Config files --" -ForegroundColor Yellow
        Get-ChildItem $configPath -ErrorAction SilentlyContinue | Where-Object {
            $_.Extension -eq '.der' -or $_.Name -like 'sftp_key*'
        } | ForEach-Object {
            Write-Host "  $($_.Name) ($($_.Length) bytes)" -ForegroundColor Gray
        }
    }
    
    default {
        Write-Host "Usage:" -ForegroundColor Yellow
        Write-Host "  .\Simulate-AlstomCA.ps1 -Action sign-client   # Sign UaExpert cert with CA" -ForegroundColor Gray
        Write-Host "  .\Simulate-AlstomCA.ps1 -Action sign-server   # Sign our server cert with CA" -ForegroundColor Gray
        Write-Host "  .\Simulate-AlstomCA.ps1 -Action show-status   # Show all cert stores" -ForegroundColor Gray
        Write-Host ""
        Write-Host "Full test flow:" -ForegroundColor Cyan
        Write-Host "  1. Backend in CertificateMode=ca" -ForegroundColor Gray
        Write-Host "  2. UaExpert connects --> rejected (self-signed)" -ForegroundColor Gray
        Write-Host "  3. Run: .\Simulate-AlstomCA.ps1 -Action sign-client" -ForegroundColor Gray
        Write-Host "  4. Import client_signed_by_ca.der in frontend" -ForegroundColor Gray
        Write-Host "  5. UaExpert reconnects --> ACCEPTED (CA-signed)" -ForegroundColor Gray
    }
}
