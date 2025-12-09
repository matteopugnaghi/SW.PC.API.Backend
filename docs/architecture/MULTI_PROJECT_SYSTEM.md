# 📂 Sistema Multi-Proyecto

**Versión**: 1.0  
**Fecha**: Diciembre 2025  
**Estado**: ✅ Implementado

---

## 🎯 Objetivo

El sistema multi-proyecto permite que **un único código fuente** soporte múltiples instalaciones industriales. Cada proyecto tiene sus propios:

- 📊 Archivo de configuración Excel (`ProjectConfig.xlsm`)
- 🎨 Modelos 3D (`.glb`, `.gltf`)
- 🗄️ Base de datos SQLite (`project.db`)
- 💾 Backups automáticos

---

## 🏗️ Arquitectura

```
SW.PC.API.Backend_/
├── active-project.json          ← Selector de proyecto activo
├── Projects/
│   ├── cliente-abc/
│   │   ├── config/
│   │   │   └── ProjectConfig.xlsm
│   │   ├── models/
│   │   │   └── *.glb
│   │   ├── data/
│   │   │   └── project.db
│   │   ├── backups/
│   │   └── README.md
│   ├── cliente-xyz/
│   │   └── ...
│   └── _template/               ← Plantilla para nuevos proyectos
├── ExcelConfigs/                ← Configuración legacy (modo default)
├── wwwroot/models/              ← Modelos legacy (modo default)
└── Data/                        ← Base de datos legacy (modo default)
```

---

## ⚙️ Configuración

### Archivo `active-project.json`

```json
{
  "activeProject": "default"
}
```

| Valor | Modo | Descripción |
|-------|------|-------------|
| `"default"` | Legacy | Usa rutas tradicionales (`ExcelConfigs/`, `wwwroot/models/`, `Data/`) |
| `"cliente-abc"` | Multi-Proyecto | Usa `Projects/cliente-abc/config/`, `models/`, `data/` |

### Cambiar proyecto activo

1. Editar `active-project.json`
2. Reiniciar el backend

---

## 🛠️ APIs de Gestión de Proyectos

### Listar proyectos disponibles
```http
GET /api/projects
```

**Respuesta:**
```json
[
  {
    "id": "default",
    "name": "Default (Legacy Mode)",
    "hasConfig": true,
    "hasModels": true,
    "hasDatabase": true,
    "isActive": true
  },
  {
    "id": "cliente-abc",
    "name": "cliente abc",
    "hasConfig": true,
    "hasModels": true,
    "hasDatabase": true,
    "isActive": false
  }
]
```

### Obtener proyecto activo
```http
GET /api/projects/active
```

**Respuesta:**
```json
{
  "projectId": "cliente-abc",
  "isMultiProjectMode": true,
  "paths": {
    "basePath": "Projects/cliente-abc/",
    "configPath": "Projects/cliente-abc/config/",
    "modelsPath": "Projects/cliente-abc/models/",
    "dataPath": "Projects/cliente-abc/data/",
    "backupsPath": "Projects/cliente-abc/backups/",
    "excelConfigPath": "Projects/cliente-abc/config/ProjectConfig.xlsm",
    "databasePath": "Projects/cliente-abc/data/project.db"
  }
}
```

### Crear nuevo proyecto
```http
POST /api/projects/{projectId}/create
```

**Respuesta:**
```json
{
  "success": true,
  "message": "Project 'nuevo-cliente' created successfully",
  "projectId": "nuevo-cliente",
  "nextSteps": [
    "1. Copy ProjectConfig.xlsm to Projects/nuevo-cliente/config/",
    "2. Copy 3D models to Projects/nuevo-cliente/models/",
    "3. Update active-project.json with: {\"activeProject\": \"nuevo-cliente\"}",
    "4. Restart the backend"
  ]
}
```

### Crear backup
```http
POST /api/projects/backup
```

### Listar backups
```http
GET /api/projects/backups
```

### Descargar backup
```http
GET /api/projects/backup/{backupId}/download
```

---

## 📋 Rutas según modo

| Recurso | Modo Legacy (`default`) | Modo Multi-Proyecto |
|---------|------------------------|---------------------|
| Excel Config | `ExcelConfigs/ProjectConfig.xlsm` | `Projects/{id}/config/ProjectConfig.xlsm` |
| Modelos 3D | `wwwroot/models/` | `Projects/{id}/models/` |
| Base de datos | `Data/Aquafrisch.db` | `Projects/{id}/data/project.db` |
| Backups | `backups/` | `Projects/{id}/backups/` |

---

## 🔧 Servicios involucrados

### ProjectContextService
**Archivo**: `Services/ProjectContextService.cs`

Servicio singleton que gestiona el contexto del proyecto activo:
- Lee `active-project.json` al iniciar
- Proporciona rutas correctas según el modo
- Lista proyectos disponibles
- Crea estructura de nuevos proyectos

### ExcelConfigService
**Archivo**: `Services/ExcelConfigService.cs`

Carga configuración Excel desde la ruta del proyecto activo.

### ModelService
**Archivo**: `Services/ModelService.cs`

Sirve modelos 3D desde la carpeta del proyecto activo.

### ProjectsController
**Archivo**: `Controllers/ProjectsController.cs`

API REST para gestión de proyectos.

---

## 🚀 Workflow de nuevo proyecto

### 1. Crear estructura
```powershell
# Via API
Invoke-RestMethod -Uri "http://localhost:5000/api/projects/nuevo-cliente/create" -Method Post

# O manualmente
New-Item -ItemType Directory -Path "Projects/nuevo-cliente/config"
New-Item -ItemType Directory -Path "Projects/nuevo-cliente/models"
New-Item -ItemType Directory -Path "Projects/nuevo-cliente/data"
New-Item -ItemType Directory -Path "Projects/nuevo-cliente/backups"
```

### 2. Copiar archivos
```powershell
# Configuración Excel
Copy-Item "ExcelConfigs/ProjectConfig.xlsm" "Projects/nuevo-cliente/config/"

# Modelos 3D
Copy-Item "wwwroot/models/*" "Projects/nuevo-cliente/models/" -Recurse

# Base de datos (opcional, copia de plantilla)
Copy-Item "Data/Aquafrisch.db" "Projects/nuevo-cliente/data/project.db"
```

### 3. Activar proyecto
```json
// active-project.json
{
  "activeProject": "nuevo-cliente"
}
```

### 4. Reiniciar backend
```powershell
dotnet run
```

---

## 🔐 Consideraciones de seguridad

- La carpeta `Projects/` está excluida de Git (excepto `_template/`)
- Cada proyecto tiene su propia base de datos de usuarios
- Los backups incluyen todos los archivos del proyecto

---

## 📦 Despliegue en producción

### Estructura en producción
```
C:\Aquafrisch\Backend\
├── SW.PC.API.Backend.exe
├── active-project.json          ← Configurar proyecto activo
├── Projects/
│   └── instalacion-madrid/
│       ├── config/ProjectConfig.xlsm
│       ├── models/*.glb
│       ├── data/project.db
│       └── backups/
└── wwwroot/
```

### Deploy-Manual-Remote.ps1
El script de despliegue copia automáticamente:
- Carpeta `Projects/` completa
- Archivo `active-project.json`

---

## ✅ Checklist de verificación

- [ ] `active-project.json` tiene el proyecto correcto
- [ ] Carpeta `Projects/{id}/config/` contiene `ProjectConfig.xlsm`
- [ ] Carpeta `Projects/{id}/models/` contiene los modelos 3D
- [ ] API `/api/projects/active` devuelve las rutas correctas
- [ ] Frontend carga los modelos correctamente
