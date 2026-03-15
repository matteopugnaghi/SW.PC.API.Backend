# Guia de Despliegue y Copia Manual - Aquafrisch Supervisor

> Documento interno para ingenieros y personal tecnico.
> Cubre todos los escenarios de despliegue, actualizacion y copia de proyectos.

---

## Indice

1. [Estructura en un PC de Produccion](#1-estructura-en-un-pc-de-produccion)
2. [Despliegue Completo (Primera Instalacion)](#2-despliegue-completo-primera-instalacion)
3. [Actualizar Solo Codigo (Backend + Frontend)](#3-actualizar-solo-codigo-backend--frontend)
4. [Copiar Proyecto entre PCs](#4-copiar-proyecto-entre-pcs)
5. [Despliegue al Servidor de Empresa](#5-despliegue-al-servidor-de-empresa)
6. [Scripts Automaticos](#6-scripts-automaticos)
7. [Gestion de deploy-version.json](#7-gestion-de-deploy-versionjson)
8. [Problemas Comunes](#8-problemas-comunes)

---

## 1. Estructura en un PC de Produccion

Despues de un despliegue completo, la estructura en el PC del cliente es:

```
C:\Aquafrisch Supervisor\
├── Backend\
│   ├── SW.PC.API.Backend.exe       ← Ejecutable del servidor
│   ├── *.dll                       ← Runtime .NET 8.0 + dependencias
│   ├── appsettings.json            ← Configuracion general
│   ├── appsettings.Production.json ← Configuracion de produccion
│   ├── active-project.json         ← Proyecto activo (cual proyecto usar)
│   ├── certificate.pfx             ← Certificado SSL (HTTPS)
│   ├── deploy-version.json         ← Version del codigo instalado
│   ├── wwwroot\                    ← Frontend compilado (React)
│   │   ├── index.html
│   │   ├── static\css\
│   │   ├── static\js\
│   │   └── ...
│   └── Projects\
│       └── {nombre-proyecto}\      ← Datos del proyecto
│           ├── config\
│           │   └── ProjectConfig.xlsm   ← Configuracion Excel
│           ├── models\
│           │   └── *.glb                ← Modelos 3D
│           ├── data\
│           │   └── project.db           ← Base de datos (usuarios, sesiones)
│           ├── backups\                 ← Backups automaticos
│           ├── sbom\                    ← SBOM (EU CRA Compliance)
│           ├── audit\                   ← Logs de auditoria
│           ├── translations\            ← Traducciones
│           └── README.md
├── Start-Supervisor.bat            ← Modo consola (solo depuracion)
└── Installers\
    └── aspnetcore-runtime-*.exe    ← Instalador .NET (opcional)
```

### Separacion Codigo vs Proyecto

| Tipo | Ruta | Se actualiza con codigo | Se copia con proyecto |
|------|------|:-----------------------:|:---------------------:|
| Backend (exe, dlls) | `Backend\*.exe, *.dll` | SI | NO |
| Frontend (React) | `Backend\wwwroot\` | SI | NO |
| Version del servidor | `Backend\deploy-version.json` | SI | NO |
| Certificado SSL | `Backend\certificate.pfx` | NO (se preserva) | NO |
| Proyecto activo | `Backend\active-project.json` | Configurable | NO |
| Config Excel | `Projects\{id}\config\` | NO | SI |
| Modelos 3D | `Projects\{id}\models\` | NO | SI |
| Base de datos | `Projects\{id}\data\` | NO | **CUIDADO** |
| SBOM | `Projects\{id}\sbom\` | NO | SI |
| Traducciones | `Projects\{id}\translations\` | NO | SI |
| Backups | `Projects\{id}\backups\` | NO | NO (local) |
| Audit logs | `Projects\{id}\audit\` | NO | NO (local) |

---

## 2. Despliegue Completo (Primera Instalacion)

### 2.1 Con Script Automatico (recomendado)

Desde el PC de desarrollo, con acceso de red al PC destino:

```powershell
.\Deploy-Manual-Remote.ps1 -ProjectId "nombre-proyecto"
```

Esto compila, copia todo (codigo + proyecto) y registra el servicio Windows.

### 2.2 Manual (sin acceso de red)

#### Paso 1: Compilar en el PC de desarrollo

```powershell
# Backend
cd SW.PC.API.Backend_
dotnet publish -c Release -o publish --self-contained true -r win-x64

# Frontend
cd ..\SW.PC.REACT.Frontend\my-3d-app
npm run build
```

#### Paso 2: Preparar USB

Copiar al USB:

```
USB/
├── publish\                    ← Backend compilado (de dotnet publish)
├── build\                      ← Frontend compilado (de npm run build)
├── Projects\
│   └── {nombre-proyecto}\      ← Carpeta del proyecto completa
│       ├── config\
│       ├── models\
│       └── data\               ← Solo si es primera instalacion
├── active-project.json         ← Preparar con el nombre del proyecto
└── Start-Supervisor.bat        ← Script de inicio para depuracion
```

Contenido de `active-project.json`:
```json
{
  "activeProject": "nombre-proyecto"
}
```

#### Paso 3: En el PC del cliente

1. Crear carpeta `C:\Aquafrisch Supervisor\Backend\`
2. Copiar `publish\*` → `C:\Aquafrisch Supervisor\Backend\`
3. Copiar `build\*` → `C:\Aquafrisch Supervisor\Backend\wwwroot\`
4. Copiar `Projects\` → `C:\Aquafrisch Supervisor\Backend\Projects\`
5. Copiar `active-project.json` → `C:\Aquafrisch Supervisor\Backend\`
6. Copiar `Start-Supervisor.bat` → `C:\Aquafrisch Supervisor\`

#### Paso 4: Registrar como Servicio Windows (como Administrador)

```cmd
sc create AquafrischSupervisor binPath= "C:\Aquafrisch Supervisor\Backend\SW.PC.API.Backend.exe" start= auto DisplayName= "Aquafrisch Supervisor"
sc description AquafrischSupervisor "Aquafrisch Supervisor - SCADA/HMI Backend"
sc failure AquafrischSupervisor reset= 86400 actions= restart/10000/restart/30000/restart/60000
sc start AquafrischSupervisor
```

#### Paso 5: Abrir puertos en el Firewall (como Administrador)

```powershell
New-NetFirewallRule -DisplayName 'Aquafrisch Supervisor HTTP' -Direction Inbound -Port 5000 -Protocol TCP -Action Allow
New-NetFirewallRule -DisplayName 'Aquafrisch Supervisor HTTPS' -Direction Inbound -Port 5001 -Protocol TCP -Action Allow
```

#### Paso 6: Verificar

- HTTP: `http://localhost:5000`
- HTTPS: `https://localhost:5001` (recomendado)

---

## 3. Actualizar Solo Codigo (Backend + Frontend)

**Caso de uso**: El proyecto (Excel, modelos 3D, base de datos) ya esta configurado correctamente en el PC del cliente. Solo se quiere actualizar la version del software.

### 3.1 Con Script Automatico

```powershell
.\Deploy-Manual-Remote.ps1 -ProjectId "nombre-proyecto" -CodeOnly
```

El flag `-CodeOnly`:
- Actualiza Backend (exe, dlls, runtime)
- Actualiza Frontend (wwwroot)
- Actualiza `deploy-version.json`
- **NO toca** `Projects/` (config, modelos, DB intactos)
- **NO toca** `certificate.pfx`

### 3.2 Manual (USB o acceso directo)

#### Paso 1: Compilar en el PC de desarrollo

```powershell
# Backend
cd SW.PC.API.Backend_
dotnet publish -c Release -o publish --self-contained true -r win-x64

# Frontend
cd ..\SW.PC.REACT.Frontend\my-3d-app
npm run build
```

#### Paso 2: Copiar al USB

```
USB/
├── publish\     ← Backend compilado
└── build\       ← Frontend compilado
```

#### Paso 3: En el PC del cliente

1. **Parar el servicio**:
   ```cmd
   sc stop AquafrischSupervisor
   ```
   Esperar ~5 segundos a que se liberen los archivos.

2. **Copiar Backend** (sobrescribir):
   ```
   USB\publish\*  →  C:\Aquafrisch Supervisor\Backend\
   ```

3. **Copiar Frontend** (sobrescribir):
   ```
   USB\build\*  →  C:\Aquafrisch Supervisor\Backend\wwwroot\
   ```

4. **Arrancar el servicio**:
   ```cmd
   sc start AquafrischSupervisor
   ```

#### Que NO tocar

```
C:\Aquafrisch Supervisor\Backend\
├── active-project.json     ❌ NO TOCAR (ya configurado)
├── certificate.pfx         ❌ NO TOCAR (certificado SSL del servidor)
├── deploy-version.json     ⚠️  Se puede actualizar (ver seccion 7)
└── Projects\               ❌ NO TOCAR NADA AQUI DENTRO
    └── {proyecto}\
        ├── config\         ❌ Configuracion Excel del cliente
        ├── models\         ❌ Modelos 3D del cliente
        ├── data\           ❌ Base de datos (usuarios, sesiones, alarmas)
        ├── backups\        ❌ Backups del cliente
        └── ...             ❌ Todo lo demas
```

### 3.3 Verificacion post-actualizacion

1. Abrir `https://{IP-del-PC}:5001` en el navegador
2. Iniciar sesion (los usuarios se mantienen)
3. En el panel de informacion, verificar que la version del Backend y Frontend se ha actualizado
4. Comprobar que la configuracion 3D carga correctamente

---

## 4. Copiar Proyecto entre PCs

### 4.1 De Produccion → Servidor Empresa (o viceversa)

**Que copiar:**

```
Projects/{nombre-proyecto}/
├── config\          ✅ COPIAR (Excel, configuracion)
├── models\          ✅ COPIAR (modelos 3D .glb)
├── sbom\            ✅ COPIAR (compliance EU CRA)
├── translations\    ✅ COPIAR (traducciones)
├── README.md        ✅ COPIAR (documentacion)
├── data\            ⚠️  VER NOTA ABAJO
├── backups\         ❌ NO COPIAR (backups locales)
├── audit\           ❌ NO COPIAR (logs locales)
└── logs\            ❌ NO COPIAR (logs locales)
```

> **IMPORTANTE sobre `data/project.db`:**
> - En **primera instalacion** (no existe DB en destino): SI copiar
> - Si ya **existe DB en destino**: NO copiar (contiene usuarios, sesiones, alarmas del servidor destino)
> - Si copias la DB de otro PC, los usuarios de ese PC sobrescribiran los del servidor destino y no podras iniciar sesion con las credenciales anteriores

### 4.2 Copiar de Produccion → PC de Desarrollo

```powershell
# Opcion 1: Via red
Copy-Item -Path "\\192.168.2.161\C$\Aquafrisch Supervisor\Backend\Projects\{proyecto}" `
          -Destination ".\Projects\{proyecto}" -Recurse

# Opcion 2: Via USB (copiar carpeta del proyecto)
```

Luego en desarrollo puedes trabajar con ese proyecto seleccionandolo en `active-project.json`:
```json
{
  "activeProject": "nombre-proyecto"
}
```

### 4.3 Copiar de PC de Desarrollo → Produccion (solo config)

Si solo quieres actualizar la configuracion Excel y/o modelos 3D sin tocar la base de datos:

```
USB/
├── config\ProjectConfig.xlsm    ← Copiar a Projects/{proyecto}/config/
└── models\*.glb                 ← Copiar a Projects/{proyecto}/models/
```

En el PC del cliente:
1. Parar el servicio: `sc stop AquafrischSupervisor`
2. Copiar los archivos a la carpeta correspondiente
3. Arrancar: `sc start AquafrischSupervisor`

---

## 5. Despliegue al Servidor de Empresa

El servidor de empresa (`192.168.2.199`) funciona en modo **Development** con multiples proyectos.

### Diferencias con Produccion

| Aspecto | Produccion | Servidor Empresa |
|---------|-----------|-----------------|
| Entorno | Production | Development |
| Proyectos | 1 por PC | Multiples |
| Selector de proyecto | No (fijo en active-project.json) | Si (UI + header X-Project-Id) |
| Git en servidor | No | No |
| Script | `Deploy-Manual-Remote.ps1` | `Deploy-Servidor-Empresa.ps1` |

### Despliegue

```powershell
# Desde el PC de desarrollo
.\Deploy-Servidor-Empresa.ps1

# O el wrapper .bat (doble-click)
.\Deploy-Empresa.bat
```

Este script:
1. Compila Backend y Frontend
2. Copia SOLO codigo (no toca proyectos)
3. Genera `deploy-version.json` en `Backend/`
4. Registra/actualiza el servicio Windows

### Copiar proyecto al servidor empresa

Copiar la carpeta del proyecto manualmente:
```
Mi PC:   Projects\{proyecto}\  →  \\192.168.2.199\C$\Aquafrisch Supervisor\Backend\Projects\{proyecto}\
```

O via unidad mapeada:
```
Mi PC:   Projects\{proyecto}\  →  E:\Aquafrisch Supervisor\Backend\Projects\{proyecto}\
```

> **Nota**: En el servidor empresa, la DB se crea automaticamente al seleccionar un proyecto nuevo. Es seguro copiar la carpeta completa la primera vez. Para actualizaciones posteriores, solo copiar `config\` y `models\`.

---

## 6. Scripts Automaticos

### Deploy-Manual-Remote.ps1 (Produccion)

```powershell
# Primera instalacion completa
.\Deploy-Manual-Remote.ps1 -ProjectId "A70.AMITWP"

# Solo actualizar codigo (preservar proyecto)
.\Deploy-Manual-Remote.ps1 -ProjectId "A70.AMITWP" -CodeOnly

# Saltar compilacion (ya compilado)
.\Deploy-Manual-Remote.ps1 -ProjectId "A70.AMITWP" -SkipBackendBuild -SkipFrontendBuild

# IP diferente
.\Deploy-Manual-Remote.ps1 -TargetIP "192.168.2.200" -ProjectId "cliente-xyz"
```

### Deploy-Servidor-Empresa.ps1 (Empresa)

```powershell
# Despliegue normal
.\Deploy-Servidor-Empresa.ps1

# Saltar compilacion
.\Deploy-Servidor-Empresa.ps1 -SkipBuild
```

### Comandos remotos utiles para el servicio

```cmd
:: Ver estado del servicio
sc \\192.168.2.161 query AquafrischSupervisor

:: Parar el servicio
sc \\192.168.2.161 stop AquafrischSupervisor

:: Arrancar el servicio
sc \\192.168.2.161 start AquafrischSupervisor

:: Eliminar el servicio (para reinstalar)
sc \\192.168.2.161 delete AquafrischSupervisor
```

---

## 7. Gestion de deploy-version.json

### Ubicacion

```
C:\Aquafrisch Supervisor\Backend\deploy-version.json
```

Siempre en la raiz de `Backend/`, **nunca dentro de `Projects/`**.

### Contenido

```json
{
  "ProjectId": "A70.AMITWP",
  "DeployedAt": "2026-03-15 10:30:00",
  "DeployedFrom": "PC-DESARROLLO",
  "DeployedBy": "mpugnaghi",
  "Backend": {
    "ComponentName": "Backend",
    "Version": "2026.02.02",
    "CommitSha": "dcf22bc",
    "Branch": "master",
    "IsSigned": false,
    "SignatureStatus": "UNSIGNED"
  },
  "Frontend": {
    "ComponentName": "Frontend",
    "Version": "2026.01.15",
    "CommitSha": "abc1234",
    "Branch": "master",
    "IsSigned": false,
    "SignatureStatus": "UNSIGNED"
  }
}
```

### Comportamiento

| Accion | Efecto en deploy-version.json |
|--------|-------------------------------|
| Deploy completo | Se genera automaticamente |
| Deploy con `-CodeOnly` | Se genera automaticamente |
| Copiar carpeta de proyecto | **No se toca** (esta fuera de Projects/) |
| Restaurar backup | **No se toca** (se salta en restore) |
| Actualizacion manual (USB) | Se puede copiar manualmente si se desea |

### Como se muestra en la interfaz

- **Panel de Informacion** → Software Integrity → Muestra version, commit, firma
- **Panel Git** → Certificados de Deploy → Detalle completo por componente
- Badge: `DEPLOYED` (azul) cuando existe deploy-version.json

---

## 8. Problemas Comunes

### No puedo iniciar sesion despues de copiar un proyecto

**Causa**: Se copio `data/project.db` de otro PC, sobrescribiendo los usuarios locales.

**Solucion**:
1. Parar el servicio: `sc stop AquafrischSupervisor`
2. Eliminar la DB: `del "C:\Aquafrisch Supervisor\Backend\Projects\{proyecto}\data\project.db"`
3. Arrancar: `sc start AquafrischSupervisor`
4. La DB se recrea automaticamente con usuarios por defecto:
   - `superadmin` / `Aquafrisch@SuperAdmin2024!`
   - `admin` / `Admin@Aquafrisch2024!`

### El servicio no arranca

**Posibles causas**:
- Puerto 5000/5001 ocupado por otro proceso
- Falta certificado SSL (`certificate.pfx`)
- `active-project.json` apunta a un proyecto que no existe

**Diagnostico**: Ejecutar manualmente para ver errores:
```cmd
sc stop AquafrischSupervisor
cd "C:\Aquafrisch Supervisor\Backend"
SW.PC.API.Backend.exe
```

### La version aparece como "unknown"

**Causa**: No existe `deploy-version.json` en `Backend/`.

**Solucion**: Volver a desplegar con el script (genera el fichero automaticamente), o crearlo manualmente.

### Error 1219 al conectar por red

**Causa**: Ya hay una conexion abierta al PC destino (ej: unidad mapeada).

**Solucion**: Desconectar todas las conexiones previas:
```cmd
net use \\192.168.2.161\C$ /delete /y
net use E: /delete /y
```

### Error al sobrescribir DLLs (archivos bloqueados)

**Causa**: El servicio sigue corriendo y tiene los archivos bloqueados.

**Solucion**: Asegurarse de parar el servicio y esperar:
```cmd
sc stop AquafrischSupervisor
timeout /t 5
```

Si persiste, matar el proceso:
```cmd
taskkill /IM SW.PC.API.Backend.exe /F
```

---

## Resumen Rapido

| Quiero... | Hago... |
|-----------|---------|
| **Instalar por primera vez** | `Deploy-Manual-Remote.ps1 -ProjectId "X"` o manual con USB |
| **Actualizar solo codigo** | `Deploy-Manual-Remote.ps1 -ProjectId "X" -CodeOnly` o copiar publish/ + build/ |
| **Actualizar config Excel** | Copiar `ProjectConfig.xlsm` a `Projects/{id}/config/` y reiniciar servicio |
| **Actualizar modelos 3D** | Copiar `*.glb` a `Projects/{id}/models/` y reiniciar servicio |
| **Copiar proyecto a otro PC** | Copiar carpeta `Projects/{id}/` **sin** `data/`, `backups/`, `audit/` |
| **Copiar proyecto (primera vez)** | Copiar carpeta `Projects/{id}/` completa |
| **Actualizar servidor empresa** | `Deploy-Servidor-Empresa.ps1` (solo codigo, nunca proyectos) |
| **Ver version instalada** | Panel Info → Software Integrity, o leer `Backend/deploy-version.json` |
