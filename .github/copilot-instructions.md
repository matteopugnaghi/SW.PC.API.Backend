# Industrial SCADA/HMI System - AI Development Guide

This is a dual-stack industrial automation system: **ASP.NET Core backend + React/Babylon.js frontend** for 3D SCADA visualization with TwinCAT PLC integration.

## 🏭 Architecture Overview

**Multi-Project Architecture** - One shared codebase supports multiple industrial installations. Each project has its own configuration, 3D models, and database.

```
PC Industrial → Backend (HTTP: 5000 / HTTPS: 5001) → TwinCAT PLC (ADS)
                   ↓
              React Frontend (Port 3001) ← SignalR Real-time
                   ↓
              Projects/{projectId}/ ← Config, Models, Data per project
```

**Security**: Self-contained deployment with HTTPS support (self-signed certificate, recommended for production).

## 📂 Multi-Project System

### Project Selection (`active-project.json`)
```json
{
  "activeProject": "default"     // Legacy mode: uses ExcelConfigs/, wwwroot/models/, Data/
}
// OR
{
  "activeProject": "cliente-abc" // Multi-project: uses Projects/cliente-abc/
}
```

### Project Folder Structure
```
Projects/
├── cliente-abc/
│   ├── config/
│   │   └── ProjectConfig.xlsm    ← Excel configuration
│   ├── models/
│   │   └── *.glb                 ← 3D models
│   ├── data/
│   │   └── project.db            ← SQLite database
│   ├── backups/                  ← Automatic backups
│   └── README.md
└── _template/                    ← Template for new projects
```

### Project Management APIs
| Endpoint | Method | Description |
|----------|--------|-------------|
| `/api/projects` | GET | List all available projects |
| `/api/projects/active` | GET | Get active project info and paths |
| `/api/projects/{id}/create` | POST | Create new project structure |
| `/api/projects/backup` | POST | Create backup of active project |
| `/api/projects/backups` | GET | List available backups |
| `/api/projects/backup/{id}/download` | GET | Download backup ZIP |

### Key Services
- **ProjectContextService** (`Services/ProjectContextService.cs`) - Manages global project context (Singleton)
- **RequestProjectContextService** (`Services/RequestProjectContextService.cs`) - Per-request project context (Scoped)
- **ProjectDbContextFactory** (`Data/ProjectDbContextFactory.cs`) - Creates DbContext with project-specific database path
- **ProjectContextMiddleware** (`Middleware/ProjectContextMiddleware.cs`) - Reads X-Project-Id header in Development mode
- **ProjectsController** (`Controllers/ProjectsController.cs`) - REST API for project management

### Multi-Tenant Development Mode
In Development mode, the frontend can switch between projects by sending the `X-Project-Id` header:
- Frontend stores selected project in `localStorage` and `api._selectedProjectId`
- All API calls include `X-Project-Id` header via `api.getProjectHeaders()`
- Backend middleware reads header and sets `IRequestProjectContext` for that request
- Each project has its own database (users, sessions, audit logs are independent)

**Important**: In Production, the `X-Project-Id` header is ignored for security - project is always read from `active-project.json`.

## 🔧 Technology Stack

### Backend (SW.PC.API.Backend_/)
- **ASP.NET Core 8.0** with JWT authentication
- **SignalR Hub** (`/hubs/scada`) for real-time PLC data
- **TwinCAT.Ads** integration (simulated for development)
- **Excel configuration** via EPPlus (per-project `ProjectConfig.xlsm`)
- **SQLite** database (per-project `project.db`)

### Frontend (my-3d-app/)
- **React 19.2** with Babylon.js 8.33 for 3D rendering
- **SignalR client** for real-time updates
- **i18next** for internationalization (ES/EN)
- **Multi-view system**: Main, Alarms, Statistics, Recipes

## 🚀 Essential Development Workflows

### Backend Development
```powershell
# Build and run (use VS Code tasks)
dotnet build      # or Ctrl+Shift+P → "Tasks: Run Task" → "build"
dotnet run        # Backend runs on http://localhost:5000
dotnet watch run  # Auto-reload during development
```

### Frontend Development
```powershell
cd my-3d-app
npm start              # Standard mode (port 3000)
npm run start:dev      # Development mode (port 3001)
npm run start:backend  # Backend integration mode
```

### Integration Testing
1. Start backend: `dotnet run` (port 5000 HTTP, 5001 HTTPS)
2. Start frontend: `npm run start:dev` (port 3001)
3. Check console logs for SignalR connection status
4. Swagger UI: `http://localhost:5000`
5. **Production**: Use HTTPS (port 5001) for secure communication

## 📋 Configuration System

### Excel-Based Project Configuration
- **ProjectConfig.xlsm** defines the entire system configuration
- **Location**: `Projects/{projectId}/config/` (multi-project) or `ExcelConfigs/` (legacy)
- **Sheets**: `General`, `PLC_Variables`, `HMI_Screens`, `3D_Models`, `System Config`
- **Service**: `ExcelConfigService.cs` loads configurations based on active project

### Configuration Modes
| Mode | `activeProject` | Config Path | Models Path | Database |
|------|-----------------|-------------|-------------|----------|
| Legacy | `"default"` | `ExcelConfigs/` | `wwwroot/models/` | `Data/Aquafrisch.db` |
| Multi-Project | `"proyecto-x"` | `Projects/proyecto-x/config/` | `Projects/proyecto-x/models/` | `Projects/proyecto-x/data/project.db` |

### Key Models (`Models/`)
- `ProjectConfiguration` - Main project structure from Excel
- `PlcVariable` - TwinCAT variable definitions with binding metadata
- `Model3DConfig` - 3D model configuration with PLC variable bindings
- `HMIScreen` - Screen definitions with component layout

## 🔄 Real-time Communication Patterns

### SignalR Hub (`Hubs/ScadaHub.cs`)
```csharp
// Client subscription pattern
await Clients.Caller.SendAsync("PlcDataUpdate", data);
await Groups.AddToGroupAsync(Context.ConnectionId, $"var_{variableName}");
```

### Frontend SignalR Service (`services/signalr.js`)
```javascript
// Auto-connection with reconnection logic
connection.start().then(() => {
    console.log('✅ SignalR conectado exitosamente');
});
```

## 🎮 3D Scene Architecture (`BabylonScene.js`)

### Key Components
- **Multi-camera system**: Free, orbital, top-down views
- **Dynamic model loading** from backend API
- **Real-time animations** driven by PLC variable changes
- **Interactive GUI** with view switcher and controls

### 3D Model Integration Pattern
1. Models stored in `wwwroot/models/` (GLB/GLTF format)
2. API serves model metadata via `/api/models`
3. Frontend loads via Babylon.js loaders
4. Real-time updates via SignalR variable subscriptions

## 🔧 Service Layer Patterns

### TwinCAT Integration (`Services/TwinCATService.cs`)
- **Mock implementation** for development (simulated variables)
- **ADS Configuration** via appsettings.json
- **Event-driven** variable change notifications
- **Background service** (`PlcNotificationService`) for continuous monitoring

### Model Service Pattern (`Services/ModelService.cs`)
```csharp
// Standard service interface pattern
Task<IEnumerable<Model3D>> GetAllModelsAsync();
Task<Model3D?> GetModelByIdAsync(string id);
```

## 🌐 CORS & API Configuration

### Multi-port CORS Setup (`Program.cs`)
```csharp
policy.WithOrigins("http://localhost:3001", "http://localhost:3000", "http://localhost:5173")
      .AllowCredentials();  // Required for SignalR
```

### JWT Authentication with SignalR
- Token passed via query string for WebSocket connections
- Path-based routing: `/hubs/*` uses access_token parameter

## 📁 Critical File Locations

### Backend Multi-Project Files
- **Backend entry**: `Program.cs` (DI container, CORS, SignalR setup)
- **Global project context**: `Services/ProjectContextService.cs` (Singleton, reads active-project.json)
- **Request project context**: `Services/RequestProjectContextService.cs` (Scoped, per-request)
- **Project middleware**: `Middleware/ProjectContextMiddleware.cs` (reads X-Project-Id header)
- **Database factory**: `Data/ProjectDbContextFactory.cs` (creates DbContext per project)
- **Active project selector**: `active-project.json` (determines which project is active in production)
- **PLC simulation**: `Services/TwinCATService.cs` (replace with real ADS for production)
- **Excel parsing**: `Services/ExcelConfigService.cs` (project configuration loader, cache per file path)
- **Deploy script**: `Deploy-Manual-Remote.ps1` (automated production deployment)

### Frontend Multi-Project Files
- **API service**: `my-3d-app/src/services/api.js` (includes `getProjectHeaders()` for X-Project-Id)
- **Project selector**: `my-3d-app/src/components/ProjectSelector.js` (UI for selecting project)
- **Login page**: `my-3d-app/src/components/Login.js` (includes ProjectSelector, sends auth headers)
- **3D scene**: `my-3d-app/src/BabylonScene.js` (Babylon.js integration)

## 🚀 Production Deployment

### Deploy Command
```powershell
.\Deploy-Manual-Remote.ps1  # Deploys to 192.168.2.161
```

### File Mapping: Development → Production

| Source (Development) | Destination (Production) | Notes |
|---------------------|--------------------------|-------|
| `publish\*` | `Backend\*.exe,dll` | Self-contained (includes .NET) |
| `Projects\{id}\*` | `Backend\Projects\{id}\` | **Project-specific files** |
| `wwwroot\models\*` | `Backend\wwwroot\models\` | Legacy mode 3D models |
| `ExcelConfigs\*` | `Backend\ExcelConfigs\` | Legacy mode configuration |
| `Data\Aquafrisch.db` | `Backend\Data\` | Legacy mode database |
| `my-3d-app\build\*` | `Backend\wwwroot\` | React frontend (html, js, css) |
| `active-project.json` | `Backend\active-project.json` | **Project selector** |

> **Important**: In multi-project mode, 3D models come from `Projects/{id}/models/`.
> In legacy mode (default), they come from `wwwroot/models/`.

### Production URLs
- HTTP: `http://192.168.2.161:5000`
- HTTPS: `https://192.168.2.161:5001` (recommended)

## 📚 Documentation Structure (`docs/`)

All technical documentation is organized in the `docs/` folder:

| Folder | Content |
|--------|---------|
| `docs/architecture/` | System architecture, logs, 3D models |
| `docs/compliance/` | EU CRA compliance, security, terceros |
| `docs/development/` | API examples, integration guides, troubleshooting |
| `docs/configuration/` | Excel mapping, SystemConfig |
| `docs/deployment/` | Deploy manuals, Kiosk setup |
| `docs/user-guides/` | End-user manuals (EU CRA Anexo II) |
| `docs/internal/` | ⚠️ Internal only - credentials, processes |
| `docs/changelog/` | Integration status, work logs |

**Key docs**: `docs/README.md` (index), `docs/compliance/ROADMAP_CUMPLIMIENTO_CRA.md`

## ⚠️ Development Notes

- **SQLite database**: `Data/Aquafrisch.db` (users, sessions, audit logs)
- **TwinCAT simulation** active (real PLC integration available)
- **Excel configuration** system fully operational
- **Multi-language support** implemented (ES/EN via i18next)
- **Self-contained deployment**: Includes .NET 8.0 runtime, no installation needed on production PC