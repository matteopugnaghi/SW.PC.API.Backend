# 🤖 Guía de Desarrollo con Asistentes IA

**Propósito**: Este documento proporciona contexto del sistema para asistentes de IA (GitHub Copilot, Claude, ChatGPT, etc.) para facilitar el desarrollo asistido.

**Versión**: 3.0  
**Fecha**: Diciembre 2025

---

## 🏭 Descripción del Sistema

Sistema de automatización industrial dual-stack: **ASP.NET Core backend + React/Babylon.js frontend** para visualización SCADA/HMI 3D con integración TwinCAT PLC.

## 🏗️ Arquitectura General

**Multi-Project Architecture** - Un único código fuente soporta múltiples instalaciones industriales. Cada proyecto tiene su propia configuración, modelos 3D y base de datos.

```
PC Industrial → Backend (HTTP: 5000 / HTTPS: 5001) → TwinCAT PLC (ADS)
                   ↓
              React Frontend (Port 3001) ← SignalR Real-time
                   ↓
              Projects/{projectId}/ ← Config, Models, Data per project
```

**Seguridad**: Despliegue self-contained con soporte HTTPS (certificado auto-firmado, recomendado para producción).

---

## 📂 Sistema Multi-Proyecto

### Selección de Proyecto (`active-project.json`)
```json
{
  "activeProject": "default"     // Modo legacy: usa ExcelConfigs/, wwwroot/models/, Data/
}
// O
{
  "activeProject": "cliente-abc" // Multi-proyecto: usa Projects/cliente-abc/
}
```

### Estructura de Carpetas por Proyecto
```
Projects/
├── cliente-abc/
│   ├── config/
│   │   └── ProjectConfig.xlsm    ← Configuración Excel
│   ├── models/
│   │   └── *.glb                 ← Modelos 3D
│   ├── data/
│   │   └── project.db            ← Base de datos SQLite
│   ├── backups/                  ← Backups automáticos
│   └── README.md
└── _template/                    ← Plantilla para nuevos proyectos
```

### APIs de Gestión de Proyectos
| Endpoint | Método | Descripción |
|----------|--------|-------------|
| `/api/projects` | GET | Lista todos los proyectos disponibles |
| `/api/projects/active` | GET | Info del proyecto activo y rutas |
| `/api/projects/{id}/create` | POST | Crear estructura de nuevo proyecto |
| `/api/projects/backup` | POST | Crear backup del proyecto activo |
| `/api/projects/backups` | GET | Listar backups disponibles |
| `/api/projects/backup/{id}/download` | GET | Descargar backup ZIP |

### Servicios Clave
- **ProjectContextService** (`Services/ProjectContextService.cs`) - Contexto global del proyecto (Singleton)
- **RequestProjectContextService** (`Services/RequestProjectContextService.cs`) - Contexto por request (Scoped)
- **ProjectDbContextFactory** (`Data/ProjectDbContextFactory.cs`) - Factory para DbContext con ruta de BD por proyecto
- **ProjectContextMiddleware** (`Middleware/ProjectContextMiddleware.cs`) - Lee header X-Project-Id en modo Development
- **ProjectsController** (`Controllers/ProjectsController.cs`) - API REST para gestión de proyectos

### Modo Multi-Tenant en Desarrollo
En modo Development, el frontend puede cambiar entre proyectos enviando el header `X-Project-Id`:
- Frontend almacena proyecto seleccionado en `localStorage` y `api._selectedProjectId`
- Todas las llamadas API incluyen header `X-Project-Id` via `api.getProjectHeaders()`
- Middleware del backend lee el header y configura `IRequestProjectContext` para ese request
- Cada proyecto tiene su propia base de datos (usuarios, sesiones, logs de auditoría independientes)

**Importante**: En Producción, el header `X-Project-Id` se ignora por seguridad - el proyecto siempre se lee de `active-project.json`.

---

## 🔧 Stack Tecnológico

### Backend (SW.PC.API.Backend_/)
- **ASP.NET Core 8.0** con autenticación JWT
- **SignalR Hub** (`/hubs/scada`) para datos PLC en tiempo real
- **TwinCAT.Ads** integración (simulado para desarrollo)
- **Configuración Excel** via EPPlus (`ProjectConfig.xlsm` por proyecto)
- **SQLite** base de datos (`project.db` por proyecto)

### Frontend (my-3d-app/)
- **React 19.2** con Babylon.js 8.33 para renderizado 3D
- **SignalR client** para actualizaciones en tiempo real
- **i18next** para internacionalización (ES/EN)
- **Sistema multi-vista**: Main, Alarmas, Estadísticas, Recetas

---

## 🚀 Flujos de Trabajo de Desarrollo

### Desarrollo Backend
```powershell
# Compilar y ejecutar (usar tareas de VS Code)
dotnet build      # o Ctrl+Shift+P → "Tasks: Run Task" → "build"
dotnet run        # Backend en http://localhost:5000
dotnet watch run  # Auto-reload durante desarrollo
```

### Desarrollo Frontend
```powershell
cd my-3d-app
npm start              # Modo estándar (puerto 3000)
npm run start:dev      # Modo desarrollo (puerto 3001)
npm run start:backend  # Modo integración con backend
```

### Pruebas de Integración
1. Iniciar backend: `dotnet run` (puerto 5000 HTTP, 5001 HTTPS)
2. Iniciar frontend: `npm run start:dev` (puerto 3001)
3. Verificar logs de consola para estado de conexión SignalR
4. Swagger UI: `http://localhost:5000`
5. **Producción**: Usar HTTPS (puerto 5001) para comunicación segura

---

## 📋 Sistema de Configuración

### Configuración basada en Excel
- **ProjectConfig.xlsm** define toda la configuración del sistema
- **Ubicación**: `Projects/{projectId}/config/` (multi-proyecto) o `ExcelConfigs/` (legacy)
- **Hojas**: `General`, `PLC_Variables`, `HMI_Screens`, `3D_Models`, `System Config`
- **Servicio**: `ExcelConfigService.cs` carga configuraciones según proyecto activo

### Modos de Configuración
| Modo | `activeProject` | Ruta Config | Ruta Modelos | Base de Datos |
|------|-----------------|-------------|--------------|---------------|
| Legacy | `"default"` | `ExcelConfigs/` | `wwwroot/models/` | `Data/Aquafrisch.db` |
| Multi-Project | `"proyecto-x"` | `Projects/proyecto-x/config/` | `Projects/proyecto-x/models/` | `Projects/proyecto-x/data/project.db` |

### Modelos Clave (`Models/`)
- `ProjectConfiguration` - Estructura principal del proyecto desde Excel
- `PlcVariable` - Definiciones de variables TwinCAT con metadata de binding
- `Model3DConfig` - Configuración de modelos 3D con bindings a variables PLC
- `HMIScreen` - Definiciones de pantallas con layout de componentes

---

## 🔄 Patrones de Comunicación en Tiempo Real

### SignalR Hub (`Hubs/ScadaHub.cs`)
```csharp
// Patrón de suscripción del cliente
await Clients.Caller.SendAsync("PlcDataUpdate", data);
await Groups.AddToGroupAsync(Context.ConnectionId, $"var_{variableName}");
```

### Servicio SignalR del Frontend (`services/signalr.js`)
```javascript
// Auto-conexión con lógica de reconexión
connection.start().then(() => {
    console.log('✅ SignalR conectado exitosamente');
});
```

---

## 🎮 Arquitectura de Escena 3D (`BabylonScene.js`)

### Componentes Clave
- **Sistema multi-cámara**: Vistas libre, orbital, cenital
- **Carga dinámica de modelos** desde API del backend
- **Animaciones en tiempo real** controladas por cambios en variables PLC
- **GUI interactiva** con selector de vistas y controles

### Patrón de Integración de Modelos 3D
1. Modelos almacenados en `wwwroot/models/` (formato GLB/GLTF)
2. API sirve metadata de modelos via `/api/models`
3. Frontend carga via loaders de Babylon.js
4. Actualizaciones en tiempo real via suscripciones a variables SignalR

---

## 🔧 Patrones de Capa de Servicios

### Integración TwinCAT (`Services/TwinCATService.cs`)
- **Implementación mock** para desarrollo (variables simuladas)
- **Configuración ADS** via appsettings.json
- **Event-driven** notificaciones de cambio de variables
- **Background service** (`PlcNotificationService`) para monitoreo continuo

### Patrón de Servicio de Modelos (`Services/ModelService.cs`)
```csharp
// Patrón de interfaz de servicio estándar
Task<IEnumerable<Model3D>> GetAllModelsAsync();
Task<Model3D?> GetModelByIdAsync(string id);
```

---

## 🌐 Configuración CORS & API

### Configuración CORS Multi-puerto (`Program.cs`)
```csharp
policy.WithOrigins("http://localhost:3001", "http://localhost:3000", "http://localhost:5173")
      .AllowCredentials();  // Requerido para SignalR
```

### Autenticación JWT con SignalR
- Token pasado via query string para conexiones WebSocket
- Routing basado en ruta: `/hubs/*` usa parámetro access_token

---

## 📁 Ubicaciones de Archivos Críticos

### Archivos Multi-Proyecto del Backend
- **Entry point del backend**: `Program.cs` (contenedor DI, CORS, setup SignalR)
- **Contexto global del proyecto**: `Services/ProjectContextService.cs` (Singleton, lee active-project.json)
- **Contexto del proyecto por request**: `Services/RequestProjectContextService.cs` (Scoped, por-request)
- **Middleware del proyecto**: `Middleware/ProjectContextMiddleware.cs` (lee header X-Project-Id)
- **Factory de base de datos**: `Data/ProjectDbContextFactory.cs` (crea DbContext por proyecto)
- **Selector de proyecto activo**: `active-project.json` (determina proyecto activo en producción)
- **Simulación PLC**: `Services/TwinCATService.cs` (reemplazar con ADS real para producción)
- **Parsing Excel**: `Services/ExcelConfigService.cs` (cargador de configuración, cache por ruta de archivo)
- **Script de despliegue**: `Deploy-Manual-Remote.ps1` (despliegue automatizado a producción)

### Archivos Multi-Proyecto del Frontend
- **Servicio API**: `my-3d-app/src/services/api.js` (incluye `getProjectHeaders()` para X-Project-Id)
- **Selector de proyecto**: `my-3d-app/src/components/ProjectSelector.js` (UI para seleccionar proyecto)
- **Página de login**: `my-3d-app/src/components/Login.js` (incluye ProjectSelector, envía auth headers)
- **Escena 3D**: `my-3d-app/src/BabylonScene.js` (integración Babylon.js)

---

## 🚀 Despliegue en Producción

### Comando de Despliegue
```powershell
.\Deploy-Manual-Remote.ps1  # Despliega a 192.168.2.161
```

### Mapeo de Archivos: Desarrollo → Producción

| Origen (Desarrollo) | Destino (Producción) | Notas |
|---------------------|----------------------|-------|
| `publish\*` | `Backend\*.exe,dll` | Self-contained (incluye .NET) |
| `Projects\{id}\*` | `Backend\Projects\{id}\` | **Archivos específicos del proyecto** |
| `wwwroot\models\*` | `Backend\wwwroot\models\` | Modelos 3D modo legacy |
| `ExcelConfigs\*` | `Backend\ExcelConfigs\` | Configuración modo legacy |
| `Data\Aquafrisch.db` | `Backend\Data\` | Base de datos modo legacy |
| `my-3d-app\build\*` | `Backend\wwwroot\` | Frontend React (html, js, css) |
| `active-project.json` | `Backend\active-project.json` | **Selector de proyecto** |

> **Importante**: En modo multi-proyecto, los modelos 3D vienen de `Projects/{id}/models/`.
> En modo legacy (default), vienen de `wwwroot/models/`.

### URLs de Producción
- HTTP: `http://192.168.2.161:5000`
- HTTPS: `https://192.168.2.161:5001` (recomendado)

---

## 📚 Estructura de Documentación (`docs/`)

Toda la documentación técnica está organizada en la carpeta `docs/`:

| Carpeta | Contenido |
|---------|-----------|
| `docs/architecture/` | Arquitectura del sistema, logs, modelos 3D |
| `docs/compliance/` | Cumplimiento EU CRA, seguridad, terceros |
| `docs/development/` | Ejemplos de API, guías de integración, troubleshooting |
| `docs/configuration/` | Mapeo Excel, SystemConfig |
| `docs/deployment/` | Manuales de despliegue, configuración Kiosk |
| `docs/user-guides/` | Manuales de usuario (EU CRA Anexo II) |
| `docs/internal/` | ⚠️ Solo interno - credenciales, procesos |
| `docs/changelog/` | Estado de integración, logs de trabajo |

**Docs clave**: `docs/README.md` (índice), `docs/compliance/ROADMAP_CUMPLIMIENTO_CRA.md`

---

## ⚠️ Notas de Desarrollo

- **Base de datos SQLite**: `Data/Aquafrisch.db` (usuarios, sesiones, logs de auditoría)
- **Simulación TwinCAT** activa (integración PLC real disponible)
- **Sistema de configuración Excel** completamente operativo
- **Soporte multi-idioma** implementado (ES/EN via i18next)
- **Despliegue self-contained**: Incluye runtime .NET 8.0, no requiere instalación en PC de producción
