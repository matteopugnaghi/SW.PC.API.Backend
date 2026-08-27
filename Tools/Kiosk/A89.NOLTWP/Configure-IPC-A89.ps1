<#
.SYNOPSIS
    Configuracion A89-IPC-SERVER - Kiosk Completo (Arquitectura IPC Unico).

.DESCRIPTION
    Script de configuracion del IPC para el proyecto A89.NOLTWP.
    Arquitectura Single IPC:
      - CP2221-0040 (A89-IPC-SERVER) → Aquafrisch Supervisor + TwinCAT XAR

    Fases:
      0.  Hostname (A89-IPC-SERVER)
      1.  Cuentas de usuario (aqf, aqf-admin, aqf-advanced)
      2.  Politicas de contrasena
      3.  Auto-logon del usuario kiosk
      4.  Custom Shell (LaunchKiosk.bat → KioskWatchdog.ps1)
      5.  Keyboard Filter (bloqueo de atajos)
      6.  Firewall (puertos 5000/5001, ADS local)
      7.  Servicio Windows AquafrischSupervisor
      8.  Deshabilitacion de servicios innecesarios
      9.  Auditoria de eventos
      10. Herramientas Admin (escritorio aqf-admin)
      11. Copiar Tools/Kiosk al IPC
      12. Resumen

    Red:
      IP: 192.168.2.161/24 (acceso clientes, Supervisor HTTPS)

    Proyecto: A89.NOLTWP — Train Washing Plant, Nola (Napoli, Italia)
    Cliente:  Alstom
    Hardware: Beckhoff CP2221-0040 / S/N 000ur244 / Var 000685050
    Ref: 04.2-01 · 04.2-03 · IEC 62443

.PARAMETER Phase
    Fase(s) a ejecutar. Valores posibles:
      All, Hostname, Accounts, Passwords, AutoLogon, Shell, KeyboardFilter,
      Firewall, Service, DisableServices, Audit, AdminTools, CopyTools, Summary

.PARAMETER SupervisorPath
    Ruta de instalacion del Supervisor en el IPC. Default: C:\Aquafrisch Supervisor

.PARAMETER KioskUser
    Nombre del usuario kiosk. Default: aqf

.PARAMETER AdminUser
    Nombre del usuario administrador. Default: aqf-admin

.PARAMETER AdvancedUser
    Nombre del usuario avanzado. Default: aqf-advanced

.PARAMETER SupervisorUrl
    URL del backend para el navegador kiosk. Default: https://192.168.2.161:5001

.PARAMETER NewComputerName
    Nuevo hostname. Si se especifica, renombra el equipo.

.PARAMETER ComputerName
    IP o hostname del IPC remoto para conectar via WinRM.

.PARAMETER Credential
    Credenciales para conectar al IPC remoto.

.PARAMETER IdleTimeoutMinutes
    Timeout de inactividad. Default: 30

.PARAMETER DryRun
    Muestra los cambios sin aplicarlos.

.PARAMETER Rollback
    Ruta al archivo de rollback JSON para revertir cambios.

.EXAMPLE
    # REMOTO - desde el PC de desarrollo:
    .\Configure-IPC-A89.ps1 -ComputerName 192.168.2.161 -Credential (Get-Credential) -Phase All -DryRun

    # LOCAL - en el propio IPC:
    .\Configure-IPC-A89.ps1 -Phase All

    # ROLLBACK:
    .\Configure-IPC-A89.ps1 -Rollback ".\rollback_20260827_143000.json" -ComputerName 192.168.2.161 -Credential (Get-Credential)

.NOTES
    Requiere: Ejecutar como Administrador (local) o WinRM habilitado (remoto)
    Proyecto: A89.NOLTWP
    Script base: Configure-Kiosk.ps1 (estandar IPC Unico)
#>

[CmdletBinding()]
param(
    [ValidateSet('All','Hostname','Accounts','Passwords','AutoLogon','Shell',
                 'KeyboardFilter','Firewall','Service','DisableServices',
                 'Audit','AdminTools','CopyTools','Summary')]
    [string[]]$Phase = @('All'),

    [string]$SupervisorPath = 'C:\Aquafrisch Supervisor',

    [string]$KioskUser = 'aqf',

    [string]$AdminUser = 'aqf-admin',

    [string]$AdvancedUser = 'aqf-advanced',

    [string]$SupervisorUrl = 'https://192.168.2.161:5001',

    [string]$NewComputerName = 'A89-IPC-SERVER',

    [string]$ComputerName,

    [PSCredential]$Credential,

    [int]$IdleTimeoutMinutes = 30,

    [switch]$DryRun,

    [string]$Rollback
)

# Delegar toda la logica al script estandar con los parametros de este proyecto
$sharedScript = Join-Path (Split-Path -Parent $PSScriptRoot) 'Configure-Kiosk.ps1'

if (-not (Test-Path $sharedScript)) {
    Write-Host "`n  ERROR: No se encontro el script base: $sharedScript" -ForegroundColor Red
    Write-Host "  Asegurate de que Configure-Kiosk.ps1 existe en Tools\Kiosk\" -ForegroundColor Yellow
    exit 1
}

$params = @{
    Phase             = $Phase
    SupervisorPath    = $SupervisorPath
    KioskUser         = $KioskUser
    AdminUser         = $AdminUser
    AdvancedUser      = $AdvancedUser
    SupervisorUrl     = $SupervisorUrl
    NewComputerName   = $NewComputerName
    IdleTimeoutMinutes = $IdleTimeoutMinutes
}

if ($ComputerName)  { $params['ComputerName'] = $ComputerName }
if ($Credential)    { $params['Credential']   = $Credential }
if ($DryRun)        { $params['DryRun']       = $true }
if ($Rollback)      { $params['Rollback']     = $Rollback }

Write-Host ""
Write-Host "  +==============================================================+" -ForegroundColor Cyan
Write-Host "  |  AQUAFRISCH - A89-IPC-SERVER - Configuracion Kiosk         |" -ForegroundColor Cyan
Write-Host "  |  Proyecto: A89.NOLTWP - Train Washing Plant                |" -ForegroundColor Cyan
Write-Host "  |  Cliente:  Alstom - Nola, Napoli, Italia                   |" -ForegroundColor Cyan
Write-Host "  |  Hardware: CP2221-0040 (S/N 000ur244)                      |" -ForegroundColor Cyan
Write-Host "  +==============================================================+" -ForegroundColor Cyan
Write-Host ""

& $sharedScript @params
