#!/usr/bin/env pwsh
<#
.SYNOPSIS
    🔐 Generador de Códigos de Recuperación Aquafrisch
    
.DESCRIPTION
    Herramienta INTERNA de Aquafrisch para generar códigos de recuperación
    de contraseñas por teléfono. El mismo algoritmo se usa en el backend.
    
    FUNCIONA SIN INTERNET - El código se calcula matemáticamente.
    
.PARAMETER InstallationId
    ID de instalación del cliente (ej: AQFR-2024-001)
    
.PARAMETER Username
    Nombre de usuario que necesita recuperar contraseña
    
.PARAMETER Date
    Fecha para la que generar el código (por defecto HOY)
    
.EXAMPLE
    .\GenerateRecoveryCode.ps1 -InstallationId "AQFR-2024-001" -Username "admin"
    
.EXAMPLE
    .\GenerateRecoveryCode.ps1 -InstallationId "AQFR-2024-001" -Username "operador1" -Date "2024-12-07"
    
.NOTES
    ⚠️ DOCUMENTO INTERNO - NO COMPARTIR CON CLIENTES
    El secreto AQUAFRISCH_SECRET debe ser el mismo que en RecoveryCodeService.cs
#>

param(
    [Parameter(Mandatory=$true)]
    [string]$InstallationId,
    
    [Parameter(Mandatory=$true)]
    [string]$Username,
    
    [Parameter(Mandatory=$false)]
    [string]$Date = (Get-Date).ToString("yyyy-MM-dd")
)

# 🔐 SECRETO - DEBE SER EL MISMO QUE EN RecoveryCodeService.cs
$AQUAFRISCH_SECRET = "AQF-2024-S3CR3T-K3Y-N0T-SH4R3"

# Caracteres sin ambigüedad visual (sin 0,1,I,O)
$CHARS = "23456789ABCDEFGHJKLMNPQRSTUVWXYZ"

function Generate-RecoveryCode {
    param(
        [string]$installationId,
        [string]$username,
        [string]$date
    )
    
    # Normalizar inputs
    $normalizedInstallation = $installationId.ToUpperInvariant().Trim()
    $normalizedUsername = $username.ToLowerInvariant().Trim()
    
    # Crear string a hashear
    $dataToHash = "$normalizedInstallation|$normalizedUsername|$date|$AQUAFRISCH_SECRET"
    
    # Generar HMAC-SHA256
    $hmac = New-Object System.Security.Cryptography.HMACSHA256
    $hmac.Key = [System.Text.Encoding]::UTF8.GetBytes($AQUAFRISCH_SECRET)
    $hash = $hmac.ComputeHash([System.Text.Encoding]::UTF8.GetBytes($dataToHash))
    
    # Convertir a código legible
    $code = "AQFR-"
    for ($i = 0; $i -lt 12; $i++) {
        if ($i -gt 0 -and $i % 4 -eq 0) {
            $code += "-"
        }
        $index = $hash[$i] % $CHARS.Length
        $code += $CHARS[$index]
    }
    
    return $code
}

# Banner
Write-Host ""
Write-Host "╔═══════════════════════════════════════════════════════════════╗" -ForegroundColor Cyan
Write-Host "║  🔐 GENERADOR DE CÓDIGOS DE RECUPERACIÓN - AQUAFRISCH        ║" -ForegroundColor Cyan
Write-Host "║     ⚠️  HERRAMIENTA INTERNA - NO COMPARTIR CON CLIENTES      ║" -ForegroundColor Yellow
Write-Host "╚═══════════════════════════════════════════════════════════════╝" -ForegroundColor Cyan
Write-Host ""

# Generar código para HOY
$todayCode = Generate-RecoveryCode -installationId $InstallationId -username $Username -date $Date

# Calcular fecha de mañana para mostrar validez
$tomorrow = (Get-Date $Date).AddDays(1).ToString("yyyy-MM-dd")

Write-Host "  📋 Datos de la solicitud:" -ForegroundColor White
Write-Host "     Installation ID: $($InstallationId.ToUpperInvariant())" -ForegroundColor Gray
Write-Host "     Username:        $($Username.ToLowerInvariant())" -ForegroundColor Gray
Write-Host "     Fecha:           $Date" -ForegroundColor Gray
Write-Host ""
Write-Host "  ┌─────────────────────────────────────────────────────────────┐" -ForegroundColor Green
Write-Host "  │                                                             │" -ForegroundColor Green
Write-Host "  │   CÓDIGO DE RECUPERACIÓN:  $todayCode   │" -ForegroundColor Green
Write-Host "  │                                                             │" -ForegroundColor Green
Write-Host "  └─────────────────────────────────────────────────────────────┘" -ForegroundColor Green
Write-Host ""
Write-Host "  ⏰ Válido hasta: $tomorrow 23:59" -ForegroundColor Yellow
Write-Host ""
Write-Host "  📞 Instrucciones para el usuario:" -ForegroundColor White
Write-Host "     1. Acceder a la pantalla de login" -ForegroundColor Gray
Write-Host "     2. Click en '¿Olvidaste tu contraseña?'" -ForegroundColor Gray
Write-Host "     3. Introducir el código: $todayCode" -ForegroundColor Gray
Write-Host "     4. Introducir la nueva contraseña" -ForegroundColor Gray
Write-Host ""

# Registrar en log interno (opcional)
$logFile = Join-Path $PSScriptRoot "recovery_codes_log.txt"
$logEntry = "$(Get-Date -Format 'yyyy-MM-dd HH:mm:ss') | Installation: $InstallationId | User: $Username | Code: $todayCode"
Add-Content -Path $logFile -Value $logEntry -ErrorAction SilentlyContinue

Write-Host "  📝 Registro guardado en: recovery_codes_log.txt" -ForegroundColor DarkGray
Write-Host ""
