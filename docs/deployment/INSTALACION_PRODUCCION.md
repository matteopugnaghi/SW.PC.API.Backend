# 📦 GUÍA DE INSTALACIÓN - AQUAFRISCH SUPERVISOR

## 🖥️ Información del Sistema

| Componente | Valor |
|------------|-------|
| **PC Producción** | 192.168.2.161 |
| **Nombre PC** | A90-IPC-SERVER |
| **Usuario Admin** | Administrator |
| **Sistema Operativo** | Windows (x64) |

---

## 📁 Estructura de Carpetas en PC Producción (Multi-Proyecto)

```
C:\Aquafrisch Supervisor\
├── Backend\                              # Aplicación ASP.NET Core (Self-contained)
│   ├── SW.PC.API.Backend.exe             # Ejecutable principal (incluye .NET Runtime)
│   ├── SW.PC.API.Backend.dll             # Librería principal
│   ├── appsettings.json                  # Configuración del backend
│   ├── active-project.json               # 🎯 Selector de proyecto activo
│   ├── certificate.pfx                   # 🔒 Certificado SSL (HTTPS)
│   ├── integrity-state.json              # Estado de integridad (EU CRA)
│   │
│   ├── Projects/                         # 📁 MULTI-PROYECTO
│   │   └── {ProjectId}/                  # Carpeta por proyecto (ej: A70.AMITWP)
│   │       ├── config/                   # Configuración Excel
│   │       │   └── ProjectConfig.xlsm
│   │       ├── models/                   # 🎮 Modelos 3D del proyecto
│   │       │   └── *.glb, *.gltf
│   │       ├── data/                     # Base de datos del proyecto
│   │       │   └── project.db
│   │       ├── backups/                  # 💾 Backups del proyecto
│   │       │   └── backup_*.zip
│   │       ├── audit/                    # 📋 Logs de auditoría (EU CRA)
│   │       │   └── audit_*.json
│   │       └── sbom/                     # 📦 SBOM (EU CRA)
│   │           └── sbom-combined.json
│   │
│   ├── wwwroot/                          # Frontend React (archivos estáticos)
│   │   ├── index.html                    # Página principal
│   │   ├── manifest.json
│   │   ├── locales/                      # Traducciones (i18n)
│   │   │   ├── en/translation.json
│   │   │   └── es/translation.json
│   │   └── static/                       # Assets compilados (React)
│   │       ├── css/
│   │       └── js/
│   │
│   └── ... (DLLs y dependencias)
│
└── Backups/                              # Backups del deploy (automáticos)
    └── Backup_YYYYMMDD_HHmmss/
```

### ⚠️ Carpetas que NO deben existir en producción:
- `Backend\publish\` - Carpeta de compilación local
- `Backend\ExcelConfigs\` - Solo para desarrollo (legacy)
- `Backend\Projects\_template\` - Solo para desarrollo
- `Backend\wwwroot\audit\` - Legacy (ahora en Projects/{id}/audit)
- `Backend\wwwroot\sbom\` - Legacy (ahora en Projects/{id}/sbom)
- `Backend\wwwroot\models\` - Legacy (ahora en Projects/{id}/models)
- `Backend\backups\` - Legacy (ahora en Projects/{id}/backups)

---

## 🎯 Sistema Multi-Proyecto

### Selector de Proyecto (`active-project.json`)
```json
{
  "activeProject": "A70.AMITWP"
}
```

| Valor | Comportamiento |
|-------|----------------|
| `"default"` | Modo legacy (usa ExcelConfigs/, wwwroot/models/, Data/) |
| `"A70.AMITWP"` | Multi-proyecto (usa Projects/A70.AMITWP/) |

### Rutas según modo:
| Recurso | Legacy (default) | Multi-Proyecto |
|---------|------------------|----------------|
| Config Excel | `ExcelConfigs/` | `Projects/{id}/config/` |
| Modelos 3D | `wwwroot/models/` | `Projects/{id}/models/` |
| Base de datos | `Data/Aquafrisch.db` | `Projects/{id}/data/project.db` |
| Backups | `backups/` | `Projects/{id}/backups/` |
| Audit logs | `wwwroot/audit/` | `Projects/{id}/audit/` |
| SBOM | `wwwroot/sbom/` | `Projects/{id}/sbom/` |

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
5. ✅ **Limpia carpetas residuales** (publish, ExcelConfigs legacy, _template, etc.)
6. ✅ Crea estructura multi-proyecto (`Projects/{id}/`)
7. ✅ Copia el Backend a `C:\Aquafrisch Supervisor\Backend\`
8. ✅ Copia el Frontend (React) a `Backend\wwwroot\`
9. ✅ Copia Modelos 3D a `Projects/{id}/models/` (multi-proyecto)
10. ✅ Copia Excel Config a `Projects/{id}/config/`
11. ✅ **Gestiona Base de Datos** (backup si existe, preserva DB existente)
12. ✅ Copia archivo `active-project.json` (selector de proyecto)
13. ✅ Genera `deploy-version.json` con metadatos del deploy
14. ✅ Genera certificado SSL auto-firmado (10 años validez)
15. ✅ Configura Firewall (puertos 5000 HTTP y 5001 HTTPS)
16. ✅ Crea acceso directo en escritorio
17. ✅ Muestra resumen con estado de todos los archivos

> **Nota**: El deployment es **self-contained** - incluye .NET Runtime, NO requiere instalación adicional.

---

### 📋 Archivos copiados por el Deploy Script

| Origen (Desarrollo) | Destino (Producción) | Descripción |
|---------------------|---------------------|-------------|
| `publish\*` | `Backend\` | Ejecutables y DLLs (.NET self-contained) |
| `my-3d-app\build\*` | `Backend\wwwroot\` | Frontend React compilado |
| `Projects\{id}\config\*` | `Backend\Projects\{id}\config\` | Excel de configuración |
| `Projects\{id}\models\*` | `Backend\Projects\{id}\models\` | Modelos 3D (GLB/GLTF) |
| `active-project.json` | `Backend\active-project.json` | Selector de proyecto activo |
| `integrity-state.json` | `Backend\integrity-state.json` | Estado de integridad |
| `appsettings.json` | `Backend\appsettings.json` | Solo si no existe en destino |
| *Generado* | `Backend\deploy-version.json` | Metadatos del deploy (versión, fecha, firma) |
| *Generado* | `Backend\certificate.pfx` | Certificado SSL (si no existe) |

### 📂 Carpetas que se limpian automáticamente
El script elimina estas carpetas residuales de versiones anteriores:
- `Backend\publish\` - Carpeta de compilación local
- `Backend\backups\` - Legacy (ahora en Projects/{id}/)
- `Backend\ExcelConfigs\` - Legacy (ahora en Projects/{id}/config)
- `Backend\Projects\_template\` - Solo para desarrollo
- `Backend\wwwroot\audit\` - Legacy (ahora en Projects/{id}/audit)
- `Backend\wwwroot\sbom\` - Legacy (ahora en Projects/{id}/sbom)

---

### Método 2: Instalación Manual

#### Paso 1: Copiar archivos

1. En el PC de desarrollo, compilar:
```powershell
# Backend
cd "c:\...\SW.PC.API.Backend_"
dotnet publish -c Release -o .\publish --self-contained -r win-x64

# Frontend
cd "c:\...\SW.PC.REACT.Frontend\my-3d-app"
npm run build
```

2. Copiar al PC de producción:
   - `.\publish\*` → `C:\Aquafrisch Supervisor\Backend\`
   - `.\build\*` → `C:\Aquafrisch Supervisor\Backend\wwwroot\`
   - `.\Projects\{id}\` → `C:\Aquafrisch Supervisor\Backend\Projects\{id}\`
   - `active-project.json` → `C:\Aquafrisch Supervisor\Backend\`

#### Paso 2: Configurar Proyecto Activo
Crear/editar `Backend\active-project.json`:
```json
{
  "activeProject": "A70.AMITWP"
}
```

#### Paso 3: Configurar Firewall

En PowerShell (como Administrador):
```powershell
New-NetFirewallRule -DisplayName "Aquafrisch Supervisor HTTP" -Direction Inbound -Port 5000 -Protocol TCP -Action Allow
New-NetFirewallRule -DisplayName "Aquafrisch Supervisor HTTPS" -Direction Inbound -Port 5001 -Protocol TCP -Action Allow
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
| **`SW.PC.API.Backend_\Projects\{id}\models\*`** | **`Backend\Projects\{id}\models\`** | **Sobrescribe siempre** |
| **`SW.PC.API.Backend_\Projects\{id}\config\*`** | **`Backend\Projects\{id}\config\`** | **Sobrescribe siempre** |
| `SW.PC.API.Backend_\active-project.json` | `Backend\active-project.json` | Sobrescribe siempre |
| `SW.PC.API.Backend_\integrity-state.json` | `Backend\integrity-state.json` | Solo si NO existe |
| (generado) | `Backend\deploy-version.json` | Genera siempre (metadatos del deploy) |
| (generado) | `Backend\certificate.pfx` | Solo si NO existe |

### Gestión de Base de Datos:

| Escenario | Comportamiento |
|-----------|----------------|
| **Primera instalación** | Copia `Projects/{id}/data/project.db` desde desarrollo |
| **Actualización** | Preserva la DB existente (usuarios, sesiones, logs) |
| **Backups** | Se almacenan en `Projects/{id}/backups/` (si habilitado en Excel) |

### Diagrama de flujo (Multi-Proyecto):

```
PC DESARROLLO                              PC PRODUCCIÓN (192.168.2.161)
═════════════                              ═══════════════════════════════

SW.PC.API.Backend_\
├── publish\                    ────────►  C:\Aquafrisch Supervisor\Backend\
│   └── *.exe, *.dll                       ├── SW.PC.API.Backend.exe
│                                          ├── *.dll
│                                          │
├── Projects\                              ├── Projects\
│   └── A70.AMITWP\             ────────►  │   └── A70.AMITWP\
│       ├── config\                        │       ├── config\
│       │   └── ProjectConfig.xlsm         │       │   └── ProjectConfig.xlsm
│       ├── models\                        │       ├── models\
│       │   └── *.glb                      │       │   └── *.glb
│       └── data\                          │       └── data\
│           └── project.db (1ª vez)        │           └── project.db
│                                          │
├── active-project.json         ────────►  ├── active-project.json
│                                          │
└── integrity-state.json        ────────►  └── integrity-state.json (1ª vez)

my-3d-app\
└── build\                      ────────►  Backend\wwwroot\
    ├── index.html                         ├── index.html
    ├── static\                            ├── static\
    └── locales\                           └── locales\

(Generado por script)           ────────►  ├── deploy-version.json
(Generado si no existe)         ────────►  └── certificate.pfx
```

### Carpetas Limpiadas Automáticamente:

El script elimina estas carpetas residuales antes de copiar:
- `Backend\publish\`
- `Backend\backups\`
- `Backend\ExcelConfigs\`
- `Backend\Projects\_template\`
- `Backend\wwwroot\audit\`
- `Backend\wwwroot\sbom\`


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
