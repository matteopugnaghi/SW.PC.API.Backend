# ============================================================================
# GenerateSupportCode.ps1 - Generador de Códigos de Soporte Aquafrisch
# ============================================================================
# HERRAMIENTA INTERNA - SOLO PARA USO DE AQUAFRISCH
# 
# Genera códigos de respuesta para desbloqueo temporal de herramientas
# cuando un cliente llama solicitando asistencia técnica.
#
# FLUJO:
# 1. Cliente llama y proporciona: InstallationId + ChallengeCode
# 2. Técnico Aquafrisch ejecuta este script
# 3. Script genera ResponseCode
# 4. Cliente introduce ResponseCode → acceso temporal a herramientas
#
# IMPORTANTE: El secreto AQUAFRISCH_SUPPORT_SECRET debe ser el MISMO
# que en SupportController.cs del backend
# ============================================================================

param(
    [Parameter(Mandatory=$true)]
    [string]$InstallationId,
    
    [Parameter(Mandatory=$false)]
    [string]$ChallengeCode = ""
)

<#
.SYNOPSIS
    Genera código de soporte para desbloqueo temporal de herramientas

.DESCRIPTION
    Genera el código de respuesta que permite acceso temporal a las 
    herramientas del sistema (TeamViewer, diagnóstico, etc.)

.PARAMETER InstallationId
    El ID de instalación del sistema del cliente (ej: "AQF-ALSTOM-001")

.PARAMETER ChallengeCode
    (Opcional) El código de desafío mostrado al cliente.
    Si se proporciona, se verifica que coincide antes de generar respuesta.

.EXAMPLE
    .\GenerateSupportCode.ps1 -InstallationId "AQF-ALSTOM-001"
    
.EXAMPLE
    .\GenerateSupportCode.ps1 -InstallationId "AQF-ALSTOM-001" -ChallengeCode "A1B2C3"

.NOTES
    ⚠️ CONFIDENCIAL - Solo para uso interno de Aquafrisch
    El secreto debe coincidir con SupportController.cs
#>

# 🔐 SECRETO - DEBE SER EL MISMO QUE EN SupportController.cs
# ⚠️ NUNCA COMPARTIR CON EL CLIENTE
$AQUAFRISCH_SUPPORT_SECRET = "AQF-2024-SUPP0RT-T00LS-K3Y"

function Get-HmacSha256 {
    param(
        [string]$data,
        [string]$secret
    )
    
    $hmacsha = New-Object System.Security.Cryptography.HMACSHA256
    $hmacsha.Key = [System.Text.Encoding]::UTF8.GetBytes($secret)
    $hash = $hmacsha.ComputeHash([System.Text.Encoding]::UTF8.GetBytes($data))
    return [BitConverter]::ToString($hash).Replace("-", "")
}

function Generate-ChallengeCode {
    param(
        [string]$installationId
    )
    
    $hourSlot = (Get-Date).ToUniversalTime().ToString("yyyyMMddHH")
    $data = "$($installationId.ToUpper())|$hourSlot|CHALLENGE|$AQUAFRISCH_SUPPORT_SECRET"
    $hash = Get-HmacSha256 -data $data -secret $AQUAFRISCH_SUPPORT_SECRET
    return $hash.Substring(0, 6).ToUpper()
}

function Generate-ResponseCode {
    param(
        [string]$installationId
    )
    
    $hourSlot = (Get-Date).ToUniversalTime().ToString("yyyyMMddHH")
    $data = "$($installationId.ToUpper())|$hourSlot|RESPONSE|$AQUAFRISCH_SUPPORT_SECRET"
    $hash = Get-HmacSha256 -data $data -secret $AQUAFRISCH_SUPPORT_SECRET
    return $hash.Substring(0, 8).ToUpper()
}

# ============================================================================
# EJECUCIÓN PRINCIPAL
# ============================================================================

Write-Host ""
Write-Host "═══════════════════════════════════════════════════════════════" -ForegroundColor Cyan
Write-Host "   🔧 GENERADOR DE CÓDIGOS DE SOPORTE - AQUAFRISCH" -ForegroundColor Cyan
Write-Host "═══════════════════════════════════════════════════════════════" -ForegroundColor Cyan
Write-Host ""

# Normalizar Installation ID
$InstallationId = $InstallationId.ToUpper().Trim()

# Generar códigos
$expectedChallenge = Generate-ChallengeCode -installationId $InstallationId
$responseCode = Generate-ResponseCode -installationId $InstallationId

Write-Host "  📋 Installation ID: " -NoNewline -ForegroundColor White
Write-Host $InstallationId -ForegroundColor Yellow
Write-Host ""

# Si se proporcionó challenge, verificar
if ($ChallengeCode) {
    $normalizedChallenge = $ChallengeCode.ToUpper().Replace("-", "").Replace(" ", "")
    Write-Host "  🔍 Challenge proporcionado: " -NoNewline -ForegroundColor White
    Write-Host $ChallengeCode -ForegroundColor Cyan
    
    Write-Host "  🔍 Challenge esperado:      " -NoNewline -ForegroundColor White
    Write-Host $expectedChallenge -ForegroundColor Cyan
    
    if ($normalizedChallenge -eq $expectedChallenge) {
        Write-Host ""
        Write-Host "  ✅ Challenge VERIFICADO - Generando código de respuesta..." -ForegroundColor Green
    } else {
        # Verificar hora anterior (tolerancia por cambio de hora)
        $previousHourSlot = (Get-Date).ToUniversalTime().AddHours(-1).ToString("yyyyMMddHH")
        $previousData = "$InstallationId|$previousHourSlot|CHALLENGE|$AQUAFRISCH_SUPPORT_SECRET"
        $previousHash = Get-HmacSha256 -data $previousData -secret $AQUAFRISCH_SUPPORT_SECRET
        $previousChallenge = $previousHash.Substring(0, 6).ToUpper()
        
        if ($normalizedChallenge -eq $previousChallenge) {
            Write-Host ""
            Write-Host "  ⚠️ Challenge de hora anterior - Aún válido" -ForegroundColor Yellow
        } else {
            Write-Host ""
            Write-Host "  ❌ Challenge NO COINCIDE - Verificar Installation ID" -ForegroundColor Red
            Write-Host "     El cliente puede estar proporcionando datos incorrectos." -ForegroundColor DarkGray
            Write-Host ""
            exit 1
        }
    }
}

Write-Host ""
Write-Host "═══════════════════════════════════════════════════════════════" -ForegroundColor Green
Write-Host ""
Write-Host "  🔑 CÓDIGO DE RESPUESTA: " -NoNewline -ForegroundColor White
Write-Host $responseCode -ForegroundColor Green -BackgroundColor DarkGreen
Write-Host ""
Write-Host "═══════════════════════════════════════════════════════════════" -ForegroundColor Green
Write-Host ""
Write-Host "  ⏰ Código válido durante: 1 hora (desde generación)" -ForegroundColor DarkGray
Write-Host "  🔓 Acceso temporal: 30 minutos (configurable en Excel)" -ForegroundColor DarkGray
Write-Host ""

# Guardar log
$logFile = Join-Path $PSScriptRoot "support_codes_log.txt"
$logEntry = "$(Get-Date -Format 'yyyy-MM-dd HH:mm:ss') | InstallationId: $InstallationId | Response: $responseCode"
Add-Content -Path $logFile -Value $logEntry
Write-Host "  📝 Registro guardado en: support_codes_log.txt" -ForegroundColor DarkGray
Write-Host ""
