# 🚀 Aquafrisch Supervisor - Guía de Despliegue Manual

## 📋 Descripción General

Este documento describe el proceso de despliegue del sistema **Aquafrisch Supervisor** en ordenadores de producción mediante el script automatizado `Deploy-Manual-Remote.ps1`.

### Arquitectura del Sistema

```
┌─────────────────────────────────────────────────────────────────┐
│                    PC PRODUCCIÓN (IPC)                          │
│                    IP: 192.168.2.161                            │
├─────────────────────────────────────────────────────────────────┤
│                                                                 │
│   C:\Aquafrisch Supervisor\                                     │
│   ├── Backend\                                                  │
│   │   ├── SW.PC.API.Backend.exe    ← Servidor ASP.NET Core      │
│   │   ├── appsettings.json         ← Config Kestrel (puertos)   │
│   │   └── wwwroot\                 ← Frontend React compilado   │
│   │       ├── index.html                                        │
│   │       ├── static\js\                                        │
│   │       ├── static\css\                                       │
│   │       └── models\              ← Modelos 3D (.glb/.gltf)    │
│   ├── ExcelConfigs\                                             │
│   │   └── ProjectConfig.xlsm       ← Configuración instalación  │
│   └── Start-Supervisor.bat         ← Script de inicio           │
│                                                                 │
│   ┌─────────────────────────────────────────────────────────┐   │
│   │              TwinCAT Runtime (PLC)                      │   │
│   │         AMS NetId: 192.168.1.160.1.1                    │   │
│   │         Puerto ADS: 851                                 │   │
│   └─────────────────────────────────────────────────────────┘   │
│                                                                 │
└─────────────────────────────────────────────────────────────────┘
```

---

## 🔧 Requisitos Previos

### En el PC de Desarrollo (donde ejecutas el script)

| Requisito | Versión | Verificar |
|-----------|---------|-----------|
| Windows | 10/11 | - |
| PowerShell | 5.1+ | `$PSVersionTable.PSVersion` |
| .NET SDK | 8.0+ | `dotnet --version` |
| Node.js | 18+ | `node --version` |
| npm | 9+ | `npm --version` |

### En el PC de Producción (destino)

| Requisito | Versión | Notas |
|-----------|---------|-------|
| Windows | 10/11 | Con acceso Admin |
| TwinCAT Runtime | 3.x | Ya instalado |
| Acceso red | - | Compartir C$ habilitado |
| Puerto 5000 | - | Libre (se configura firewall) |

---

## 📁 Estructura del Proyecto

```
SW.PC.API.Backend_/
├── Deploy-Manual-Remote.ps1     ← Script de despliegue (MANUAL)
├── Deploy-Service-Remote.ps1    ← Script de despliegue (SERVICIO) [futuro]
├── DEPLOY_MANUAL.md             ← Esta documentación
├── Program.cs
├── appsettings.json
├── ExcelConfigs/
│   └── ProjectConfig.xlsm       ← Configuración de la instalación
└── ...

SW.PC.REACT.Frontend/my-3d-app/
├── src/                         ← Código fuente React
├── build/                       ← Frontend compilado (generado)
└── package.json
```

---

## 🚀 Proceso de Despliegue

### Paso 1: Preparar el Excel de Configuración

Antes de desplegar, asegúrate de que `ExcelConfigs/ProjectConfig.xlsm` tiene la configuración correcta para la instalación destino:

**Hoja "System Config":**

| Parámetro | Valor para Producción |
|-----------|----------------------|
| UseSimulatedPlc | `false` |
| PlcAmsNetId | `192.168.1.160.1.1` |
| PlcAdsPort | `851` |
| EnablePlcPolling | `true` |
| PlcPollingInterval | `1000` |
| EnableSignalR | `true` |
| EnableDatabase | `false` |

### Paso 2: Ejecutar el Script de Despliegue

```powershell
# Abrir PowerShell como Administrador
# Navegar a la carpeta del Backend
cd "C:\Users\mpugnaghi.AQUAFRISCH\Documents\Work_In_Process\_Web\AI test\SW.PC.API.Backend_"

# Ejecutar el script
.\Deploy-Manual-Remote.ps1
```

### Paso 3: Qué Hace el Script

| Paso | Acción | Duración |
|------|--------|----------|
| 1 | Verifica rutas locales | ~1s |
| 2 | Compila Backend (`dotnet publish -c Release`) | ~30s |
| 3 | Compila Frontend (`npm run build`) | ~60s |
| 4 | Conecta al PC remoto | ~2s |
| 4.5 | **Para proceso existente** (si está corriendo) | ~3s |
| 5 | Crea estructura de carpetas | ~2s |
| 6 | Backup (opcional) | ~10s |
| 7 | Copia Backend | ~20s |
| 8 | Copia Frontend (wwwroot) | ~15s |
| 9 | Copia Excel Config | ~2s |
| 10 | Crea script de inicio (.bat) | ~1s |
| 10.5 | **Configura Firewall** (puerto 5000) | ~3s |
| 11 | Crea acceso directo en escritorio | ~2s |
| 12 | Limpieza conexión | ~1s |

**Tiempo total estimado: ~2-3 minutos**

### Paso 4: Iniciar el Supervisor

Después del despliegue, en el PC de producción:

**Opción A: Acceso directo**
- Doble-click en "Aquafrisch Supervisor" en el escritorio

**Opción B: Manual**
```batch
C:\Aquafrisch Supervisor\Start-Supervisor.bat
```

**Opción C: Directamente**
```batch
cd "C:\Aquafrisch Supervisor\Backend"
SW.PC.API.Backend.exe
```

---

## 🌐 Acceso Multi-Cliente

Una vez iniciado el supervisor, se puede acceder desde cualquier dispositivo en la red:

| Dispositivo | URL |
|-------------|-----|
| PC Servidor (local) | `http://localhost:5000` |
| Cualquier PC en red | `http://192.168.2.161:5000` |
| Tablet/Móvil en red | `http://192.168.2.161:5000` |

### Requisitos para Acceso Remoto

1. ✅ **Firewall configurado** (el script lo hace automáticamente)
2. ✅ **Binding a 0.0.0.0** (ya configurado en appsettings.json)
3. ✅ **CORS habilitado** (permite 192.168.x.x automáticamente)

---

## ⚙️ Parámetros del Script

```powershell
.\Deploy-Manual-Remote.ps1 [parámetros]
```

| Parámetro | Default | Descripción |
|-----------|---------|-------------|
| `-TargetIP` | `192.168.2.161` | IP del PC destino |
| `-TargetUser` | `Administrator` | Usuario con permisos Admin |
| `-TargetPassword` | `Aqua2014$$` | Contraseña del usuario |
| `-InstallPath` | `C:\Aquafrisch Supervisor` | Ruta de instalación |
| `-SkipBackendBuild` | `false` | Saltar compilación Backend |
| `-SkipFrontendBuild` | `false` | Saltar compilación Frontend |
| `-BackupExisting` | `false` | Crear backup antes de sobrescribir |

### Ejemplos de Uso

```powershell
# Despliegue estándar
.\Deploy-Manual-Remote.ps1

# Despliegue a otro PC
.\Deploy-Manual-Remote.ps1 -TargetIP "192.168.2.200"

# Despliegue rápido (sin recompilar)
.\Deploy-Manual-Remote.ps1 -SkipBackendBuild -SkipFrontendBuild

# Despliegue con backup
.\Deploy-Manual-Remote.ps1 -BackupExisting
```

---

## 🔥 Firewall

El script configura automáticamente la regla de firewall. Si falla, ejecutar manualmente en el PC destino:

```powershell
# Ejecutar como Administrador en el PC de producción
New-NetFirewallRule -DisplayName "Aquafrisch Supervisor" `
    -Direction Inbound `
    -Port 5000 `
    -Protocol TCP `
    -Action Allow `
    -Description "Permite acceso al servidor Aquafrisch Supervisor"
```

Para verificar:
```powershell
Get-NetFirewallRule -DisplayName "Aquafrisch Supervisor"
```

---

## 🛑 Detener el Supervisor

### Método 1: Desde la consola
Presiona `Ctrl+C` en la ventana del servidor

### Método 2: Task Manager
1. Abrir Task Manager (`Ctrl+Shift+Esc`)
2. Buscar `SW.PC.API.Backend`
3. Click derecho → End Task

### Método 3: PowerShell
```powershell
Stop-Process -Name "SW.PC.API.Backend" -Force
```

---

## 🔄 Actualizar una Instalación

Para actualizar a una nueva versión:

1. **El script para automáticamente** el proceso existente
2. Ejecutar el script normalmente:
   ```powershell
   .\Deploy-Manual-Remote.ps1
   ```
3. Iniciar el supervisor de nuevo

**Con backup:**
```powershell
.\Deploy-Manual-Remote.ps1 -BackupExisting
```

---

## ❓ Troubleshooting

### Error: "No se puede conectar al PC remoto"

**Causa:** El PC destino no permite conexiones de red

**Solución:**
1. Verificar que el PC está encendido y en la red
2. Verificar que el servicio "Server" está corriendo
3. Habilitar compartir archivos:
   ```
   Panel de Control → Centro de redes → Configuración avanzada
   → Activar uso compartido de archivos
   ```

### Error: "Access Denied"

**Causa:** Credenciales incorrectas o permisos insuficientes

**Solución:**
1. Verificar usuario/contraseña
2. Verificar que el usuario tiene permisos de Admin
3. Probar conectar manualmente: `\\192.168.2.161\C$`

### Error: "El proceso ya está corriendo"

**Causa:** El supervisor anterior no se detuvo

**Solución:** El script intenta pararlo automáticamente. Si falla:
1. Conectar por RDP al PC destino
2. Cerrar la ventana del servidor o usar Task Manager

### Error: "Puerto 5000 ya en uso"

**Causa:** Otro proceso usa el puerto 5000

**Solución:**
```powershell
# Ver qué proceso usa el puerto
netstat -ano | findstr :5000

# Matar el proceso (reemplazar PID)
taskkill /PID <PID> /F
```

### El Frontend no se ve correctamente

**Causa:** El frontend no se copió bien a wwwroot

**Solución:**
1. Verificar que existe `C:\Aquafrisch Supervisor\Backend\wwwroot\index.html`
2. Re-ejecutar el script sin `-SkipFrontendBuild`

### No puedo acceder desde otros PCs

**Causa:** Firewall bloqueando puerto 5000

**Solución:**
1. Verificar regla de firewall (ver sección Firewall)
2. Probar desde el PC servidor: `http://localhost:5000`
3. Si local funciona pero remoto no → es firewall

---

## 📊 Logs y Diagnóstico

### Ver logs del servidor

Los logs aparecen en la consola donde se ejecuta el servidor.

### Verificar estado de servicios

Desde el navegador, acceder a:
- `http://192.168.2.161:5000` → Frontend con InfoPanel
- `http://192.168.2.161:5000/swagger` → API Swagger

---

## 🔮 Próximos Pasos

- [ ] **Deploy-Service-Remote.ps1** - Despliegue como Servicio Windows (inicio automático)
- [ ] **Monitor de salud** - Verificar que el servidor sigue corriendo
- [ ] **Actualizaciones automáticas** - CI/CD pipeline

---

## 📞 Contacto

**Autor:** Aquafrisch  
**Fecha:** Diciembre 2024  
**Versión:** 1.0

---

*Este documento forma parte del proyecto Aquafrisch Supervisor - Sistema de supervisión industrial con visualización 3D y comunicación TwinCAT.*
