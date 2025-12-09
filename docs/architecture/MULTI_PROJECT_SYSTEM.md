# 📂 Sistema Multi-Proyecto

**Versión**: 3.0 (Multi-Tenant Completo con Frontend)  
**Fecha**: Diciembre 2025  
**Estado**: ✅ Implementado y Probado

---

## 🎯 Objetivo

El sistema multi-proyecto permite que **un único código fuente** soporte múltiples instalaciones industriales. Cada proyecto tiene sus propios:

- 📊 Archivo de configuración Excel (`ProjectConfig.xlsm`)
- 🎨 Modelos 3D (`.glb`, `.gltf`)
- 🗄️ Base de datos SQLite (`project.db`) - **usuarios, sesiones, auditoría independientes**
- 💾 Backups automáticos

### Modos de operación

| Modo | Entorno | Descripción |
|------|---------|-------------|
| **Single-Project** | Producción | Un proyecto fijo por `active-project.json` |
| **Multi-Tenant** | Desarrollo | Cada request puede especificar un proyecto diferente via header |

---

## 🏗️ Arquitectura Completa

### Backend
```
SW.PC.API.Backend_/
├── active-project.json              ← Selector de proyecto global
├── Middleware/
│   └── ProjectContextMiddleware.cs  ← Lee header X-Project-Id
├── Services/
│   ├── ProjectContextService.cs     ← Contexto global (Singleton)
│   └── RequestProjectContextService.cs  ← Contexto por request (Scoped)
├── Data/
│   ├── AquafrischDbContext.cs       ← DbContext con rutas dinámicas
│   └── ProjectDbContextFactory.cs   ← Factory para crear DbContext por proyecto
├── Controllers/
│   ├── ProjectsController.cs        ← API gestión de proyectos
│   ├── PumpElementsController.cs    ← Usa IRequestProjectContext
│   ├── ModelsController.cs          ← Usa ModelService con proyecto
│   └── AuthController.cs            ← Autentica contra DB del proyecto
├── Projects/
│   ├── cliente-abc/
│   │   ├── config/
│   │   │   └── ProjectConfig.xlsm
│   │   ├── models/
│   │   │   └── *.glb
│   │   ├── data/
│   │   │   └── project.db           ← Base de datos independiente
│   │   └── backups/
│   └── _template/                   ← Plantilla para nuevos proyectos
├── ExcelConfigs/                    ← Configuración legacy (modo default)
├── wwwroot/models/                  ← Modelos legacy (modo default)
└── Data/Aquafrisch.db              ← Base de datos legacy (modo default)
```

### Frontend
```
my-3d-app/
├── src/
│   ├── components/
│   │   ├── Login.js                 ← Incluye ProjectSelector
│   │   └── ProjectSelector.js       ← Selector visual de proyectos
│   ├── services/
│   │   └── api.js                   ← Envía header X-Project-Id
│   └── BabylonScene.js              ← Carga modelos del proyecto
```

---

## 🔄 Flujo Completo de Selección de Proyecto

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                           FLUJO MULTI-PROYECTO                              │
└─────────────────────────────────────────────────────────────────────────────┘

1. FRONTEND - Pantalla de Login
   ┌─────────────────────────────────┐
   │  🔐 LOGIN                       │
   │  ─────────────────────────────  │
   │  📁 Proyecto: [cliente-abc ▼]   │  ← ProjectSelector (solo en desarrollo)
   │  Usuario: [___________]         │
   │  Contraseña: [___________]      │
   │  [Iniciar Sesión]               │
   └─────────────────────────────────┘
   
2. FRONTEND - api.js
   ┌─────────────────────────────────┐
   │  localStorage: selectedProjectId│
   │         ↓                       │
   │  getProjectHeaders() {          │
   │    return {                     │
   │      'X-Project-Id': 'cliente-abc'
   │    }                            │
   │  }                              │
   └─────────────────────────────────┘

3. HTTP REQUEST
   ┌─────────────────────────────────┐
   │  POST /api/auth/login           │
   │  Headers:                       │
   │    X-Project-Id: cliente-abc    │
   │    Content-Type: application/json
   └─────────────────────────────────┘

4. BACKEND - Middleware
   ┌─────────────────────────────────┐
   │  ProjectContextMiddleware       │
   │         ↓                       │
   │  Lee header X-Project-Id        │
   │         ↓                       │
   │  requestContext.SetProject()    │
   └─────────────────────────────────┘

5. BACKEND - IRequestProjectContext
   ┌─────────────────────────────────┐
   │  ProjectId: "cliente-abc"       │
   │  DatabasePath: Projects/cliente-abc/data/project.db
   │  ExcelConfigPath: Projects/cliente-abc/config/ProjectConfig.xlsm
   │  ModelsPath: Projects/cliente-abc/models/
   └─────────────────────────────────┘

6. BACKEND - Servicios
   ┌─────────────────────────────────┐
   │  AuthService → project.db       │  ← Usuarios de ESE proyecto
   │  ExcelConfigService → ProjectConfig.xlsm  ← Config de ESE proyecto
   │  ModelService → /models/        │  ← Modelos de ESE proyecto
   └─────────────────────────────────┘
```

---

## 🔧 Implementación Backend

### 1. ProjectContextMiddleware
**Archivo**: `Middleware/ProjectContextMiddleware.cs`

```csharp
public async Task InvokeAsync(HttpContext context, IRequestProjectContext requestContext)
{
    // En producción: SIEMPRE usar proyecto global (seguridad)
    if (!_environment.IsDevelopment())
    {
        await _next(context);
        return;
    }
    
    // En desarrollo: permitir selección via header
    if (context.Request.Headers.TryGetValue("X-Project-Id", out var projectId))
    {
        requestContext.SetProject(projectId.ToString());
    }

    await _next(context);
}
```

### 2. IRequestProjectContext
**Archivo**: `Services/RequestProjectContextService.cs`

Servicio **Scoped** (uno por request) que proporciona:

| Propiedad | Descripción |
|-----------|-------------|
| `ProjectId` | ID del proyecto actual |
| `DatabasePath` | Ruta a `project.db` |
| `ExcelConfigPath` | Ruta a `ProjectConfig.xlsm` |
| `ModelsPath` | Ruta a carpeta de modelos |
| `ConfigPath` | Ruta a carpeta de config |
| `BackupsPath` | Ruta a carpeta de backups |

### 3. ProjectDbContextFactory
**Archivo**: `Data/ProjectDbContextFactory.cs`

Factory que crea `DbContext` con la ruta de base de datos correcta:

```csharp
public AquafrischDbContext CreateDbContext()
{
    var requestContext = _serviceProvider.GetService<IRequestProjectContext>();
    
    string dbPath = requestContext?.DatabasePath ?? _globalContext.DatabasePath;
    
    _logger.LogInformation("📁 DbContextFactory: Proyecto: {ProjectId}, Path: {Path}", 
        requestContext?.ProjectId, dbPath);
    
    return CreateDbContextForPath(dbPath);
}
```

**Características:**
- Crea automáticamente la base de datos si no existe
- Siembra datos iniciales (usuario admin, roles)
- Cachea DbContext por ruta para evitar recreaciones innecesarias

### 4. AuthenticationService modificado
**Archivo**: `Services/AuthenticationService.cs`

```csharp
public class AuthenticationService : IAuthenticationService, IDisposable
{
    private readonly IProjectDbContextFactory _dbContextFactory;
    private AquafrischDbContext? _context;
    
    // Lazy initialization del DbContext
    private AquafrischDbContext Context => _context ??= _dbContextFactory.CreateDbContext();
    
    // Todos los métodos usan Context en lugar de _context directamente
    public async Task<User?> ValidateUserAsync(string username, string password)
    {
        var user = await Context.Users.FirstOrDefaultAsync(u => u.Username == username);
        // ...
    }
}
```

### 5. Controladores que usan el proyecto

**PumpElementsController.cs**:
```csharp
private readonly IRequestProjectContext _projectContext;

private string GetExcelPath() => _projectContext.ExcelConfigPath;

[HttpGet]
public async Task<ActionResult<List<PumpElement3D>>> GetAllPumpElements()
{
    var excelPath = GetExcelPath();
    _logger.LogInformation("📁 Loading pump elements from: {ExcelPath}", excelPath);
    var elements = await _pumpElementService.LoadPumpElementsAsync(excelPath);
    return Ok(elements);
}
```

---

## 🎨 Implementación Frontend

### 1. ProjectSelector Component
**Archivo**: `components/ProjectSelector.js`

- Solo visible en modo desarrollo (`isDevelopmentMode === true`)
- Aparece en la pantalla de Login con `variant="login"`
- Guarda selección en `localStorage`

```jsx
<ProjectSelector 
  variant="login"
  onProjectChange={(projectId) => {
    window.location.reload(); // Recargar para aplicar nuevo proyecto
  }}
/>
```

**Variantes de estilo:**
- `variant="default"`: Compacto, para header
- `variant="login"`: Grande, para pantalla de login

### 2. api.js - Headers de proyecto
**Archivo**: `services/api.js`

```javascript
// Almacena el proyecto seleccionado
_selectedProjectId: null,

// Obtiene headers con el proyecto
getProjectHeaders() {
  const headers = {};
  if (this._selectedProjectId) {
    headers['X-Project-Id'] = this._selectedProjectId;
  }
  return headers;
},

// Selecciona proyecto
setProject(projectId) {
  this._selectedProjectId = projectId;
  localStorage.setItem('selectedProjectId', projectId);
},

// Ejemplo de llamadas con headers
async getPumpElements() {
  const response = await fetch(`${API_BASE_URL}/api/pumpelements`, {
    headers: this.getProjectHeaders()
  });
  return response.json();
},

async getStateColors() {
  const response = await fetch(`${API_BASE_URL}/api/pumpelements/state-colors`, {
    headers: this.getProjectHeaders()
  });
  return response.json();
},

async getModels() {
  const response = await fetch(`${API_BASE_URL}/api/models`, {
    headers: this.getProjectHeaders()
  });
  return response.json();
},
```

### 3. Login.js - Autenticación con proyecto
**Archivo**: `components/Login.js`

```javascript
import api from '../services/api';
import ProjectSelector from './ProjectSelector';

// Helper para headers con proyecto
const getAuthHeaders = (contentType = true) => {
  const headers = { ...api.getProjectHeaders() };
  if (contentType) headers['Content-Type'] = 'application/json';
  return headers;
};

// En el render, mostrar ProjectSelector si es desarrollo
{isDevelopmentMode && (
  <div className="project-selector-login">
    <ProjectSelector 
      variant="login"
      onProjectChange={(projectId) => {
        // El selector ya guarda en api y localStorage
      }}
    />
  </div>
)}

// Login enviando header de proyecto
const response = await fetch(`${API_BASE_URL}/api/auth/login`, {
  method: 'POST',
  headers: getAuthHeaders(),
  body: JSON.stringify({ username, password }),
});
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
  "globalProjectId": "default",
  "isMultiProjectMode": true,
  "isDevelopmentMode": true,
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

# Base de datos (se crea automáticamente al primer login)
```

### 3. Activar proyecto (Producción)
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

| Aspecto | Desarrollo | Producción |
|---------|------------|------------|
| Header `X-Project-Id` | ✅ Se procesa | ❌ Se ignora |
| ProjectSelector visible | ✅ En Login | ❌ Oculto |
| Proyecto fijo | Via header dinámico | Via `active-project.json` |
| Usuarios independientes | ✅ Cada proyecto tiene su DB | ✅ Cada proyecto tiene su DB |

- La carpeta `Projects/` está excluida de Git (excepto `_template/`)
- Cada proyecto tiene su propia base de datos de usuarios
- Los backups incluyen todos los archivos del proyecto
- En producción, **no se puede cambiar de proyecto via header** (seguridad)

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
│       ├── data/project.db      ← Base de datos de ESA instalación
│       └── backups/
└── wwwroot/                     ← Frontend React compilado
```

### Deploy-Manual-Remote.ps1
El script de despliegue copia automáticamente:
- Carpeta `Projects/` completa
- Archivo `active-project.json`

---

## ✅ Checklist de verificación

### Backend
- [ ] `active-project.json` tiene el proyecto correcto
- [ ] Carpeta `Projects/{id}/config/` contiene `ProjectConfig.xlsm`
- [ ] Carpeta `Projects/{id}/models/` contiene los modelos 3D
- [ ] Carpeta `Projects/{id}/data/` existe (DB se crea automáticamente)
- [ ] API `/api/projects/active` devuelve las rutas correctas

### Frontend
- [ ] `ProjectSelector` aparece en Login (modo desarrollo)
- [ ] Las llamadas API incluyen header `X-Project-Id`
- [ ] Al cambiar proyecto, se recargan usuarios/modelos/config correctos

### Logs a verificar en consola del backend
```
🔄 Middleware: Request with X-Project-Id header: cliente-abc
📁 DbContextFactory: Proyecto: cliente-abc, Path: Projects/cliente-abc/data/project.db
📁 Loading pump elements from: Projects/cliente-abc/config/ProjectConfig.xlsm
```

---

## 🐛 Troubleshooting

### El proyecto no cambia
1. Verificar que el header `X-Project-Id` se envía (DevTools → Network → Headers)
2. Verificar que el backend está en modo Development
3. Verificar logs del backend para ver qué proyecto usa

### Base de datos no cambia
1. Verificar que `ProjectDbContextFactory` recibe el `IRequestProjectContext` correcto
2. El servicio `AuthenticationService` debe usar `IProjectDbContextFactory`
3. Verificar que el `DbContext` no está cacheado de otro proyecto

### Excel no cambia
1. Verificar que `PumpElementsController` usa `_projectContext.ExcelConfigPath`
2. Verificar que el archivo Excel existe en `Projects/{id}/config/`
3. El cache de `ExcelConfigService` es por ruta de archivo (no global)

### Modelos 3D no cambian
1. Verificar que `ModelService` usa `_projectContext.ModelsPath`
2. Verificar que los modelos existen en `Projects/{id}/models/`

---

## 📝 Resumen de archivos clave

### Backend
| Archivo | Propósito |
|---------|-----------|
| `Data/ProjectDbContextFactory.cs` | Factory para DbContext por proyecto |
| `Services/RequestProjectContextService.cs` | Contexto de proyecto por request |
| `Middleware/ProjectContextMiddleware.cs` | Lee header X-Project-Id |
| `Services/AuthenticationService.cs` | Usa ProjectDbContextFactory |
| `Controllers/PumpElementsController.cs` | Usa IRequestProjectContext |

### Frontend
| Archivo | Propósito |
|---------|-----------|
| `src/components/ProjectSelector.js` | UI selector de proyectos |
| `src/components/Login.js` | Incluye ProjectSelector, envía headers |
| `src/services/api.js` | `getProjectHeaders()` en todas las llamadas |
