# AquafrischCodes — Generadores de códigos (uso interno)

⚠️ **CONFIDENCIAL — NO compartir con clientes.** Contiene secretos compartidos con el backend.

## Contenido

| Archivo | Para qué sirve |
|---|---|
| `RecoveryCode.bat` | Genera código para **recuperar contraseña** de un usuario. Doble clic. |
| `SupportCode.bat` | Genera código para **desbloquear herramientas** (modo soporte). Doble clic. |
| `GenerateRecoveryCode.ps1` | Script PowerShell invocado por `RecoveryCode.bat`. |
| `GenerateSupportCode.ps1` | Script PowerShell invocado por `SupportCode.bat`. |

## Uso (PC del técnico Aquafrisch)

1. Copiar la carpeta `AquafrischCodes` completa al PC del técnico.
2. Doble clic en el `.bat` correspondiente.
3. Introducir los datos que pida (los lee del modal *"Llamar a Aquafrisch"* del cliente).
4. Dictar el código resultante al cliente.

Los `.bat` ejecutan PowerShell con `-ExecutionPolicy Bypass`, así que **no hace falta cambiar la política global** del PC del técnico ni permisos de admin.

## Recuperación de contraseña

El cliente debe leer del modal: **Installation ID**, **Username** y la **Fecha del sistema** (YYYY-MM-DD).
Si no dictan la fecha, el script usa la HOY del PC del técnico (válida solo si ambos PCs están en el mismo día local).

## Soporte / desbloqueo herramientas

El cliente debe leer del modal: **Installation ID** (y opcionalmente el **Challenge** mostrado en pantalla).
El código es válido durante **1 hora UTC**. Si el reloj del PC del cliente difiere mucho del del técnico, pedir al cliente la fecha/hora UTC exacta y pasarla como `DateTimeUtc` (formato ISO: `2026-05-24T14:30:00Z`).

## Logs locales

Cada script genera un log en la misma carpeta:
- `recovery_codes_log.txt`
- `support_codes_log.txt`

No commitear estos logs al repo.
