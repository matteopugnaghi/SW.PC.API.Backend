# Industrial SCADA/HMI System - AI Development Guide

This is a dual-stack industrial automation system: **ASP.NET Core backend + React/Babylon.js frontend** for 3D SCADA visualization with TwinCAT PLC integration.

## 🏭 Architecture Overview

**One Backend Per Industrial Installation** - Each PC runs an independent backend managing a single project configured via Excel.

```
PC Industrial → Backend (Port 5000) → TwinCAT PLC (ADS)
                   ↓
              React Frontend (Port 3001) ← SignalR Real-time
```

## 🔧 Technology Stack

### Backend (SW.PC.API.Backend_/)
- **ASP.NET Core 8.0** with JWT authentication
- **SignalR Hub** (`/hubs/scada`) for real-time PLC data
- **TwinCAT.Ads** integration (simulated for development)
- **Excel configuration** via EPPlus (`ExcelConfigs/ProjectConfig.xlsx`)
- **Entity Framework Core** with SQL Server

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
1. Start backend: `dotnet run` (port 5000)
2. Start frontend: `npm run start:dev` (port 3001)
3. Check console logs for SignalR connection status
4. Swagger UI: `http://localhost:5000`

## 📋 Configuration System

### Excel-Based Project Configuration (`ExcelConfigs/`)
- **ProjectConfig.xlsx** defines the entire system configuration
- **Sheets**: `General`, `PLC_Variables`, `HMI_Screens`, `3D_Models`
- Service: `ExcelConfigService.cs` loads configurations
- Pattern: Each installation = One Excel file = One project

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

- **Backend entry**: `Program.cs` (DI container, CORS, SignalR setup)
- **PLC simulation**: `Services/TwinCATService.cs` (replace with real ADS for production)
- **Excel parsing**: `Services/ExcelConfigService.cs` (project configuration loader)
- **3D scene**: `my-3d-app/src/BabylonScene.js` (Babylon.js integration)
- **API integration**: `my-3d-app/src/services/api.js` & `signalr.js`
- **Project docs**: `ARQUITECTURA_DESPLIEGUE.md`, `MODELOS_3D_IMPLEMENTATION.md`

## ⚠️ Development Notes

- **Database disabled** temporarily (EF Core culture issues)
- **TwinCAT simulation** active (real PLC integration available)
- **Excel configuration** system ready but partially commented
- **Multi-language support** implemented (ES/EN via i18next)