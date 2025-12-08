# Generador de Codigos de Recuperacion Aquafrisch
# HERRAMIENTA INTERNA - NO COMPARTIR CON CLIENTES
# Doble-click o click derecho - Ejecutar con PowerShell

# SECRETO - DEBE SER EL MISMO QUE EN RecoveryCodeService.cs
$AQUAFRISCH_SECRET = "AQF-2024-S3CR3T-K3Y-N0T-SH4R3"

# Caracteres sin ambiguedad visual (sin 0,1,I,O)
$CHARS = "23456789ABCDEFGHJKLMNPQRSTUVWXYZ"

function Generate-RecoveryCode {
    param(
        [string]$installationId,
        [string]$username,
        [string]$date
    )
    
    $normalizedInstallation = $installationId.ToUpperInvariant().Trim()
    $normalizedUsername = $username.ToLowerInvariant().Trim()
    $dataToHash = "$normalizedInstallation|$normalizedUsername|$date|$AQUAFRISCH_SECRET"
    
    $hmac = New-Object System.Security.Cryptography.HMACSHA256
    $hmac.Key = [System.Text.Encoding]::UTF8.GetBytes($AQUAFRISCH_SECRET)
    $hash = $hmac.ComputeHash([System.Text.Encoding]::UTF8.GetBytes($dataToHash))
    
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

Clear-Host

Write-Host ""
Write-Host "================================================================" -ForegroundColor Cyan
Write-Host "   GENERADOR DE CODIGOS DE RECUPERACION - AQUAFRISCH" -ForegroundColor Cyan
Write-Host "   HERRAMIENTA INTERNA - NO COMPARTIR CON CLIENTES" -ForegroundColor Yellow
Write-Host "================================================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "  Este script genera codigos de recuperacion de contrasena" -ForegroundColor White
Write-Host "  para dictar por telefono a usuarios que olvidaron su clave." -ForegroundColor Gray
Write-Host ""
Write-Host "  FUNCIONA SIN INTERNET - Calculo matematico local" -ForegroundColor Green
Write-Host ""
Write-Host "----------------------------------------------------------------" -ForegroundColor DarkGray
Write-Host ""

# Pedir Installation ID
Write-Host "  PASO 1: ID de Instalacion del cliente" -ForegroundColor Yellow
Write-Host "  (Ejemplo: AQFR-2024-001, DEMO-001, etc.)" -ForegroundColor Gray
Write-Host ""
$InstallationId = Read-Host "  Installation ID"

if ([string]::IsNullOrWhiteSpace($InstallationId)) {
    Write-Host ""
    Write-Host "  ERROR: Debe introducir un Installation ID" -ForegroundColor Red
    Write-Host ""
    Read-Host "  Presione Enter para cerrar"
    exit 1
}

Write-Host ""

# Pedir Username
Write-Host "  PASO 2: Nombre de usuario que olvido su contrasena" -ForegroundColor Yellow
Write-Host "  (Pregunte al usuario que nombre aparece en la lista de login)" -ForegroundColor Gray
Write-Host "  (Ejemplos: operador1, admin, supervisor, etc.)" -ForegroundColor Gray
Write-Host ""
$Username = Read-Host "  Username"

if ([string]::IsNullOrWhiteSpace($Username)) {
    Write-Host ""
    Write-Host "  ERROR: Debe introducir un nombre de usuario" -ForegroundColor Red
    Write-Host ""
    Read-Host "  Presione Enter para cerrar"
    exit 1
}

Write-Host ""

# Usar fecha de hoy
$Date = (Get-Date).ToString("yyyy-MM-dd")

Write-Host "----------------------------------------------------------------" -ForegroundColor DarkGray
Write-Host ""

# Generar codigo
$todayCode = Generate-RecoveryCode -installationId $InstallationId -username $Username -date $Date

# Calcular fecha de manana para mostrar validez
$tomorrow = (Get-Date $Date).AddDays(1).ToString("yyyy-MM-dd")

Write-Host "  Datos de la solicitud:" -ForegroundColor White
Write-Host "  Installation ID: $($InstallationId.ToUpperInvariant())" -ForegroundColor Gray
Write-Host "  Username:        $($Username.ToLowerInvariant())" -ForegroundColor Gray
Write-Host "  Fecha:           $Date" -ForegroundColor Gray
Write-Host ""
Write-Host "================================================================" -ForegroundColor Green
Write-Host ""
Write-Host "   CODIGO DE RECUPERACION:" -ForegroundColor Green
Write-Host ""
Write-Host "   $todayCode" -ForegroundColor White -BackgroundColor DarkGreen
Write-Host ""
Write-Host "================================================================" -ForegroundColor Green
Write-Host ""
Write-Host "  Valido hasta: $tomorrow 23:59" -ForegroundColor Yellow
Write-Host "  Solo para usuario: $($Username.ToLowerInvariant())" -ForegroundColor Yellow
Write-Host ""
Write-Host "----------------------------------------------------------------" -ForegroundColor DarkGray
Write-Host ""
Write-Host "  DICTE AL USUARIO:" -ForegroundColor Cyan
Write-Host ""
Write-Host "  Tu codigo de recuperacion es:" -ForegroundColor White
Write-Host "  $todayCode" -ForegroundColor White
Write-Host ""
Write-Host "  Con este codigo puedes cambiar tu contrasena" -ForegroundColor White
Write-Host "  desde la pantalla de login." -ForegroundColor White
Write-Host ""
Write-Host "----------------------------------------------------------------" -ForegroundColor DarkGray
Write-Host ""
Write-Host "  Instrucciones para el usuario:" -ForegroundColor White
Write-Host "  1. Acceder a la pantalla de login" -ForegroundColor Gray
Write-Host "  2. Click en 'Olvidaste tu contrasena?'" -ForegroundColor Gray
Write-Host "  3. Introducir su nombre de usuario" -ForegroundColor Gray
Write-Host "  4. Introducir el codigo: $todayCode" -ForegroundColor Gray
Write-Host "  5. Introducir la nueva contrasena" -ForegroundColor Gray
Write-Host ""

# Registrar en log interno
$logFile = Join-Path $PSScriptRoot "recovery_codes_log.txt"
$logEntry = "$(Get-Date -Format 'yyyy-MM-dd HH:mm:ss') | Installation: $InstallationId | User: $Username | Code: $todayCode"
Add-Content -Path $logFile -Value $logEntry -ErrorAction SilentlyContinue

Write-Host "  Registro guardado en: recovery_codes_log.txt" -ForegroundColor DarkGray
Write-Host ""
Write-Host "================================================================" -ForegroundColor DarkGray
Write-Host ""
Read-Host "  Presione Enter para cerrar"
