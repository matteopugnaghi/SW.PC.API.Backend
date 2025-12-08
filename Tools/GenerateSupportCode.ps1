# ============================================================================
# GenerateSupportCode.ps1 - Generador de Códigos de Soporte Aquafrisch
# ============================================================================
# HERRAMIENTA INTERNA - SOLO PARA USO DE AQUAFRISCH
# 
# Genera códigos de respuesta para desbloqueo temporal de herramientas
# cuando un cliente llama solicitando asistencia técnica.
#
# USO: Doble click o "Ejecutar con PowerShell"
#      El script pedirá los datos interactivamente
# ============================================================================

# 🔐 SECRETO - DEBE SER EL MISMO QUE EN SupportController.cs
# ⚠️ NUNCA COMPARTIR CON EL CLIENTE
$AQUAFRISCH_SUPPORT_SECRET = "AQF-2024-SUPP0RT-T00LS-K3Y"

function Get-HmacSha256 {
    param([string]$data, [string]$secret)
    $hmacsha = New-Object System.Security.Cryptography.HMACSHA256
    $hmacsha.Key = [System.Text.Encoding]::UTF8.GetBytes($secret)
    $hash = $hmacsha.ComputeHash([System.Text.Encoding]::UTF8.GetBytes($data))
    return [BitConverter]::ToString($hash).Replace("-", "")
}

function Generate-ChallengeCode {
    param([string]$installationId)
    $hourSlot = (Get-Date).ToUniversalTime().ToString("yyyyMMddHH")
    $data = "$($installationId.ToUpper())|$hourSlot|CHALLENGE|$AQUAFRISCH_SUPPORT_SECRET"
    $hash = Get-HmacSha256 -data $data -secret $AQUAFRISCH_SUPPORT_SECRET
    return $hash.Substring(0, 6).ToUpper()
}

function Generate-ResponseCode {
    param([string]$installationId)
    $hourSlot = (Get-Date).ToUniversalTime().ToString("yyyyMMddHH")
    $data = "$($installationId.ToUpper())|$hourSlot|RESPONSE|$AQUAFRISCH_SUPPORT_SECRET"
    $hash = Get-HmacSha256 -data $data -secret $AQUAFRISCH_SUPPORT_SECRET
    return $hash.Substring(0, 8).ToUpper()
}

# ============================================================================
# EJECUCIÓN PRINCIPAL - MODO INTERACTIVO
# ============================================================================

Clear-Host
Write-Host ""
Write-Host "======================================================================" -ForegroundColor Cyan
Write-Host "   GENERADOR DE CODIGOS DE SOPORTE - AQUAFRISCH" -ForegroundColor Cyan
Write-Host "                                                               " -ForegroundColor Cyan
Write-Host "   Herramienta interna para soporte tecnico                   " -ForegroundColor Cyan
Write-Host "======================================================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "  Introduzca los datos que el cliente le proporciona por telefono:" -ForegroundColor Yellow
Write-Host ""

# Pedir Installation ID
Write-Host "  ID de Instalacion (ej: AQF-ALSTOM-001): " -NoNewline -ForegroundColor White
$InstallationId = Read-Host

if ([string]::IsNullOrWhiteSpace($InstallationId)) {
    Write-Host ""
    Write-Host "  ERROR: Debe introducir el ID de instalacion" -ForegroundColor Red
    Write-Host ""
    Write-Host "  Presione cualquier tecla para salir..." -ForegroundColor DarkGray
    $null = $Host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown")
    exit 1
}

# Pedir Challenge Code (opcional)
Write-Host ""
Write-Host "  Codigo Challenge (opcional, para verificar): " -NoNewline -ForegroundColor White
$ChallengeCode = Read-Host

# Normalizar
$InstallationId = $InstallationId.ToUpper().Trim()

# Generar códigos
$expectedChallenge = Generate-ChallengeCode -installationId $InstallationId
$responseCode = Generate-ResponseCode -installationId $InstallationId

Write-Host ""
Write-Host "======================================================================" -ForegroundColor DarkGray
Write-Host ""
Write-Host "  Installation ID: " -NoNewline -ForegroundColor White
Write-Host $InstallationId -ForegroundColor Yellow
Write-Host ""

# Si se proporcionó challenge, verificar
if (-not [string]::IsNullOrWhiteSpace($ChallengeCode)) {
    $normalizedChallenge = $ChallengeCode.ToUpper().Replace("-", "").Replace(" ", "").Trim()
    
    Write-Host "  Challenge del cliente:  " -NoNewline -ForegroundColor White
    Write-Host $normalizedChallenge -ForegroundColor Cyan
    
    Write-Host "  Challenge esperado:     " -NoNewline -ForegroundColor White
    Write-Host $expectedChallenge -ForegroundColor Cyan
    
    if ($normalizedChallenge -eq $expectedChallenge) {
        Write-Host ""
        Write-Host "  [OK] Challenge VERIFICADO correctamente" -ForegroundColor Green
    } else {
        # Verificar hora anterior
        $previousHourSlot = (Get-Date).ToUniversalTime().AddHours(-1).ToString("yyyyMMddHH")
        $previousData = "$InstallationId|$previousHourSlot|CHALLENGE|$AQUAFRISCH_SUPPORT_SECRET"
        $previousHash = Get-HmacSha256 -data $previousData -secret $AQUAFRISCH_SUPPORT_SECRET
        $previousChallenge = $previousHash.Substring(0, 6).ToUpper()
        
        if ($normalizedChallenge -eq $previousChallenge) {
            Write-Host ""
            Write-Host "  [!] Challenge de hora anterior - Aun valido" -ForegroundColor Yellow
        } else {
            Write-Host ""
            Write-Host "  [!] ADVERTENCIA: Challenge NO coincide" -ForegroundColor Red
            Write-Host "      El cliente puede tener datos incorrectos o expirados." -ForegroundColor DarkGray
            Write-Host "      Se genera el codigo de todas formas..." -ForegroundColor DarkGray
        }
    }
}

Write-Host ""
Write-Host "======================================================================" -ForegroundColor Green
Write-Host ""
Write-Host "   CODIGO DE RESPUESTA PARA EL CLIENTE:" -ForegroundColor Green
Write-Host ""
Write-Host "                     $responseCode" -ForegroundColor White -BackgroundColor DarkGreen
Write-Host ""
Write-Host "======================================================================" -ForegroundColor Green
Write-Host ""
Write-Host "  Codigo valido durante: 1 hora" -ForegroundColor DarkGray
Write-Host "  Acceso temporal del cliente: 30 minutos" -ForegroundColor DarkGray
Write-Host ""

# Guardar log
try {
    $logFile = Join-Path $PSScriptRoot "support_codes_log.txt"
    $logEntry = "$(Get-Date -Format 'yyyy-MM-dd HH:mm:ss') | ID: $InstallationId | Challenge: $ChallengeCode | Response: $responseCode"
    Add-Content -Path $logFile -Value $logEntry -ErrorAction SilentlyContinue
    Write-Host "  Registro guardado en: support_codes_log.txt" -ForegroundColor DarkGray
} catch {
    # Ignorar errores de log
}

Write-Host ""
Write-Host "======================================================================" -ForegroundColor DarkGray
Write-Host ""
Write-Host "  Presione cualquier tecla para salir..." -ForegroundColor DarkGray
$null = $Host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown")
