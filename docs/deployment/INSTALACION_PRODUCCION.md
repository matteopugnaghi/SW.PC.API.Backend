# 📦 GUÍA DE INSTALACIÓN - AQUAFRISCH SUPERVISOR

## 🖥️ Información del Sistema

| Componente | Valor |
|------------|-------|
| **PC Producción** | 192.168.2.161 |
| **Nombre PC** | A90-IPC-SERVER |
| **Usuario Admin** | Administrator |
| **Sistema Operativo** | Windows (x64) |

---

## 📁 Estructura de Carpetas en PC Producción

```
C:\Aquafrisch Supervisor\
├── Backend\                          # Aplicación ASP.NET Core (Self-contained)
│   ├── SW.PC.API.Backend.exe         # Ejecutable principal (incluye .NET Runtime)
│   ├── SW.PC.API.Backend.dll         # Librería principal
│   ├── appsettings.json              # Configuración del backend
│   ├── certificate.pfx               # 🔒 Certificado SSL (HTTPS)
│   ├── integrity-state.json          # Estado de integridad (EU CRA)
│   ├── Data\                         # 📊 Base de datos
│   │   ├── Aquafrisch.db             # SQLite (usuarios, sesiones, logs)
│   │   └── backups\                  # Backups automáticos
│   │       └── Aquafrisch_backup_*.db
│   ├── wwwroot\                      # Frontend React (archivos estáticos)
│   │   ├── index.html                # Página principal
│   │   ├── manifest.json
│   │   ├── robots.txt
│   │   ├── audit\                    # Logs de auditoría (EU CRA)
│   │   ├── locales\                  # Traducciones (i18n)
│   │   │   ├── en\translation.json
│   │   │   └── es\translation.json
│   │   ├── models\                   # 🎮 Modelos 3D (desde Backend)
│   │   │   ├── *.glb, *.obj, *.stl
│   │   │   └── Pumps\                # Subcarpetas de modelos
│   │   └── static\                   # Assets compilados (React)
│   │       ├── css\
│   │       └── js\
│   └── ... (DLLs y dependencias)
│
├── ExcelConfigs\                     # Configuración Excel
│   └── ProjectConfig.xlsm            # Configuración de colores/modelos
│
└── Start-Supervisor.bat              # Script de inicio manual
```

---

## 🔧 Requisitos del Sistema

### Software Requerido
| Software | Versión | Notas |
|----------|---------|-------|
| **Windows** | 10/11 o Server 2016+ | x64 |

> **Nota**: El deployment es **self-contained** - incluye el runtime .NET 8.0, no requiere instalación adicional.

### Puertos de Red
| Puerto | Protocolo | Uso | Seguridad |
|--------|-----------|-----|----------|
| **5000** | HTTP | API REST y Frontend | ⚠️ No cifrado |
| **5001** | HTTPS | API REST y Frontend | 🔒 **Recomendado** |
| **5000/5001** | WebSocket | SignalR (tiempo real) | Sigue protocolo HTTP/HTTPS |

---

## 🚀 Métodos de Instalación

### Método 1: Deploy Automático (Recomendado)

Desde el PC de desarrollo, ejecutar:

```powershell
& "c:\Users\mpugnaghi.AQUAFRISCH\Documents\Work_In_Process\_Web\AI test\SW.PC.API.Backend_\Deploy-Manual-Remote.ps1"
```

**Opciones disponibles:**
```powershell
# Deploy completo (compila todo)
.\Deploy-Manual-Remote.ps1

# Saltar compilación (usa builds existentes)
.\Deploy-Manual-Remote.ps1 -SkipBackendBuild -SkipFrontendBuild

# Crear backup antes de desplegar
.\Deploy-Manual-Remote.ps1 -BackupExisting

# Especificar IP diferente
.\Deploy-Manual-Remote.ps1 -TargetIP "192.168.2.100"
```

**El script automáticamente:**
1. ✅ Compila el Backend (`dotnet publish --self-contained -r win-x64`)
2. ✅ Compila el Frontend (`npm run build`)
3. ✅ Verifica/detiene procesos existentes en PC remoto
4. ✅ Conecta al PC remoto via SMB
5. ✅ Crea estructura de carpetas
6. ✅ Copia el Backend a `C:\Aquafrisch Supervisor\Backend\`
7. ✅ Copia el Frontend (React) a `Backend\wwwroot\`
8. ✅ **Copia Modelos 3D del Backend** a `Backend\wwwroot\models\`
9. ✅ Copia Excel Config a `ExcelConfigs\`
10. ✅ **Gestiona Base de Datos** (backup si existe, copia si es nueva)
11. ✅ Copia archivos de estado (integrity-state.json)
12. ✅ Genera certificado SSL auto-firmado (10 años validez)
13. ✅ Configura Firewall (puertos 5000 HTTP y 5001 HTTPS)
14. ✅ Crea acceso directo en escritorio
15. ✅ Muestra resumen con estado de todos los archivos

> **Nota**: El deployment es **self-contained** - incluye .NET Runtime, NO requiere instalación adicional.

---

### Método 2: Instalación Manual

#### Paso 1: Copiar archivos

1. En el PC de desarrollo, compilar:
```powershell
# Backend
cd "c:\...\SW.PC.API.Backend_"
dotnet publish -c Release -o .\publish

# Frontend
cd "c:\...\SW.PC.REACT.Frontend\my-3d-app"
npm run build
```

2. Copiar al PC de producción:
   - `.\publish\*` → `C:\Aquafrisch Supervisor\Backend\`
   - `.\build\*` → `C:\Aquafrisch Supervisor\Backend\wwwroot\`

#### Paso 2: Instalar .NET Runtime

Si no está instalado, ejecutar en el PC de producción:
```cmd
C:\Aquafrisch Supervisor\Installers\aspnetcore-runtime-8.0.22-win-x64.exe /install /quiet
```

#### Paso 3: Configurar Firewall

En PowerShell (como Administrador):
```powershell
New-NetFirewallRule -DisplayName "Aquafrisch Supervisor" -Direction Inbound -Port 5000 -Protocol TCP -Action Allow
```

---

## ▶️ Iniciar la Aplicación

### Opción 1: Acceso directo
Doble clic en el acceso directo **"Aquafrisch Supervisor"** en el escritorio.

### Opción 2: Script batch
```cmd
C:\Aquafrisch Supervisor\Start-Supervisor.bat
```

### Opción 3: Línea de comandos
```cmd
cd "C:\Aquafrisch Supervisor\Backend"
SW.PC.API.Backend.exe
```

---

## 🌐 URLs de Acceso

### HTTP (No cifrado)
| Ubicación | URL |
|-----------|-----|
| **Desde el PC local** | http://localhost:5000 |
| **Desde la red** | http://192.168.2.161:5000 |
| **Por nombre** | http://A90-IPC-SERVER:5000 |

### 🔒 HTTPS (Recomendado - Cifrado)
| Ubicación | URL |
|-----------|-----|
| **Desde el PC local** | https://localhost:5001 |
| **Desde la red** | https://192.168.2.161:5001 |
| **Por nombre** | https://A90-IPC-SERVER:5001 |

> **Nota**: El certificado SSL es auto-firmado. La primera vez que accedas por HTTPS, el navegador mostrará una advertencia de seguridad. Acepta el certificado para continuar.

---

## 📂 Rutas en PC de Desarrollo

```
c:\Users\mpugnaghi.AQUAFRISCH\Documents\Work_In_Process\_Web\AI test\
├── SW.PC.API.Backend_\                    # Código fuente Backend
│   ├── Deploy-Manual-Remote.ps1           # Script de despliegue
│   ├── Installers\                        # Instaladores
│   │   └── aspnetcore-runtime-8.0.22-win-x64.exe
│   ├── ExcelConfigs\
│   │   └── ProjectConfig.xlsm
│   └── ... (código fuente C#)
│
└── SW.PC.REACT.Frontend\my-3d-app\        # Código fuente Frontend
    ├── src\                               # Código React
    │   ├── BabylonScene.js                # Escena 3D principal
    │   ├── components\                    # Componentes React
    │   ├── services\                      # API y SignalR
    │   └── views\                         # Vistas
    ├── public\                            # Archivos estáticos
    │   ├── models\                        # Modelos 3D
    │   └── locales\                       # Traducciones
    ├── build\                             # Build de producción
    ├── package.json
    └── .env                               # Variables de entorno
```

---

## ⚙️ Configuración

### Variables de Entorno Frontend (.env)
```env
# Backend API Configuration
REACT_APP_ENABLE_BACKEND=true
REACT_APP_BACKEND_URL=http://192.168.2.161:5000

# Development Settings
REACT_APP_DISABLE_CACHE=true

# Network access (para desarrollo)
HOST=0.0.0.0
```

### Configuración Backend (appsettings.json)
```json
{
  "AllowedHosts": "*",
  "Kestrel": {
    "Endpoints": {
      "Http": {
        "Url": "http://0.0.0.0:5000"
      },
      "Https": {
        "Url": "https://0.0.0.0:5001",
        "Certificate": {
          "Path": "certificate.pfx",
          "Password": "Aquafrisch2024!"
        }
      }
    }
  },
  "Cors": {
    "AllowedOrigins": ["http://localhost:3000", "http://localhost:3001", "https://localhost:3001"]
  }
}
```

---

## 🔄 Actualización

Para actualizar la aplicación en producción:

```powershell
# Opción 1: Deploy completo
.\Deploy-Manual-Remote.ps1

# Opción 2: Solo actualizar frontend (más rápido)
.\Deploy-Manual-Remote.ps1 -SkipBackendBuild

# Opción 3: Con backup previo
.\Deploy-Manual-Remote.ps1 -BackupExisting
```

**Nota:** Asegúrate de detener la aplicación antes de actualizar si está corriendo.

---

## 🛠️ Solución de Problemas

### Error: "Certificado SSL no encontrado"
El script de deploy genera automáticamente el certificado. Si falta, ejecutar en el PC de producción:
```powershell
cd "C:\Aquafrisch\Backend"
$cert = New-SelfSignedCertificate -DnsName "localhost","192.168.2.161" -CertStoreLocation "Cert:\LocalMachine\My" -NotAfter (Get-Date).AddYears(10)
$pwd = ConvertTo-SecureString -String "Aquafrisch2024!" -Force -AsPlainText
Export-PfxCertificate -Cert $cert -FilePath ".\certificate.pfx" -Password $pwd
```

### Error: "Puerto 5000 o 5001 en uso"
```powershell
# Ver qué proceso usa el puerto
netstat -ano | findstr :5000

# Matar el proceso
taskkill /PID <numero_pid> /F
```

### Error: "No se puede conectar desde otro PC"
1. Verificar firewall: `Get-NetFirewallRule -DisplayName "Aquafrisch*"`
2. Si faltan reglas, crearlas:
```powershell
New-NetFirewallRule -DisplayName "Aquafrisch HTTP" -Direction Inbound -Port 5000 -Protocol TCP -Action Allow
New-NetFirewallRule -DisplayName "Aquafrisch HTTPS" -Direction Inbound -Port 5001 -Protocol TCP -Action Allow
```
3. Verificar que el servidor está corriendo
4. Hacer ping al PC: `ping 192.168.2.161`

### Ver logs de la aplicación
Los logs se muestran en la consola donde se ejecuta `SW.PC.API.Backend.exe`

---

## 📋 Resumen de Comandos

| Acción | Comando |
|--------|---------|
| **Deploy completo** | `.\Deploy-Manual-Remote.ps1` |
| **Deploy sin compilar** | `.\Deploy-Manual-Remote.ps1 -SkipBackendBuild -SkipFrontendBuild` |
| **Iniciar servidor** | `C:\Aquafrisch Supervisor\Start-Supervisor.bat` |
| **Detener servidor** | `Ctrl+C` en la consola |
| **Abrir firewall HTTP** | `New-NetFirewallRule -DisplayName "Aquafrisch HTTP" -Direction Inbound -Port 5000 -Protocol TCP -Action Allow` |
| **Abrir firewall HTTPS** | `New-NetFirewallRule -DisplayName "Aquafrisch HTTPS" -Direction Inbound -Port 5001 -Protocol TCP -Action Allow` |

---

## 📁 Mapa de Archivos: Desarrollo → Producción

Esta tabla muestra exactamente qué archivos se copian y de dónde vienen:

### Archivos copiados automáticamente por `Deploy-Manual-Remote.ps1`:

| Origen (PC Desarrollo) | Destino (PC Producción) | Acción |
|------------------------|-------------------------|--------|
| `SW.PC.API.Backend_\publish\*` | `Backend\*.exe, *.dll` | Sobrescribe siempre |
| `my-3d-app\build\index.html, static\*` | `Backend\wwwroot\` | Sobrescribe siempre |
| `my-3d-app\build\locales\*` | `Backend\wwwroot\locales\` | Sobrescribe siempre |
| **`SW.PC.API.Backend_\wwwroot\models\*`** | **`Backend\wwwroot\models\`** | **Sobrescribe siempre** |
| `SW.PC.API.Backend_\ExcelConfigs\*` | `ExcelConfigs\` | Sobrescribe siempre |
| `SW.PC.API.Backend_\Data\Aquafrisch.db` | `Backend\Data\Aquafrisch.db` | Solo si NO existe (primera instalación) |
| `SW.PC.API.Backend_\integrity-state.json` | `Backend\integrity-state.json` | Solo si NO existe |
| (generado) | `Backend\certificate.pfx` | Solo si NO existe |

### Gestión de Base de Datos:

| Escenario | Comportamiento |
|-----------|----------------|
| **Primera instalación** | Copia `Aquafrisch.db` desde desarrollo |
| **Actualización** | Crea backup automático en `Data\backups\` y **preserva** la DB existente |
| **Backups** | Se mantienen los últimos 5 backups automáticamente |

### Diagrama de flujo:

```
PC DESARROLLO                              PC PRODUCCIÓN (192.168.2.161)
═════════════                              ═══════════════════════════════

SW.PC.API.Backend_\
├── publish\                    ────────►  C:\Aquafrisch Supervisor\Backend\
│   └── *.exe, *.dll                       ├── SW.PC.API.Backend.exe
│                                          ├── *.dll
├── wwwroot\models\             ────────►  ├── wwwroot\models\
│   ├── Box.glb                            │   ├── Box.glb
│   ├── Pumps\*.glb                        │   ├── Pumps\*.glb
│   └── ...                                │   └── ...
│
├── Data\Aquafrisch.db          ────────►  ├── Data\Aquafrisch.db (solo 1ª vez)
│                                          │   └── backups\ (automático)
│
├── ExcelConfigs\               ────────►  ExcelConfigs\
│   └── ProjectConfig.xlsm                 └── ProjectConfig.xlsm

my-3d-app\
└── build\                      ────────►  Backend\wwwroot\
    ├── index.html                         ├── index.html
    ├── static\                            ├── static\
    └── locales\                           └── locales\
```

> **Importante**: Los modelos 3D vienen del **Backend** (`wwwroot\models\`), NO del Frontend.
> El Backend gestiona todo según la configuración Excel.

---

## 🔒 Seguridad HTTPS

- El deployment incluye certificado SSL auto-firmado con 10 años de validez
- Contraseña del certificado: `Aquafrisch2024!`
- **Siempre usar HTTPS (puerto 5001) en producción** para comunicaciones cifradas
- El certificado cubre: `localhost`, `192.168.2.161`, nombre del PC

---

*Documento actualizado: Diciembre 2025 - Versión Self-Contained con HTTPS*
