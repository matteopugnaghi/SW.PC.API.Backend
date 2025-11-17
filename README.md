# SW.PC.API.Backend - ASP.NET Core Web API for 3D Model Viewer

A C# ASP.NET Core Web API backend that serves 3D models and configuration for a React frontend 3D viewer application.

## Features

- **3D Model Management**: Serve GLB, GLTF, OBJ, and STL files
- **Configuration API**: Color management and viewer settings
- **CORS Support**: Configured for React frontend integration
- **Swagger Documentation**: Interactive API documentation
- **File Upload Support**: Static file serving for 3D models

## API Endpoints

### Models API
- `GET /api/models` - List all available 3D models
- `GET /api/models/{id}` - Get specific model metadata
- `GET /api/models/{id}/download` - Download model file
- `GET /api/models/file/{filename}` - Direct file access
- `GET /api/models/{id}/thumbnail` - Model thumbnail (placeholder)

### Configuration API
- `GET /api/config` - Get complete application configuration
- `POST /api/config` - Update complete configuration
- `GET /api/config/colors` - Get color configuration only
- `POST /api/config/colors` - Update color configuration
- `GET /api/config/viewer` - Get viewer configuration

## Project Structure

```
SW.PC.API.Backend/
├── Controllers/           # API Controllers
│   ├── ModelsController.cs
│   └── ConfigController.cs
├── Services/             # Business Logic
│   ├── ModelService.cs
│   └── ConfigurationService.cs
├── Models/               # Data Models
│   ├── Model3D.cs
│   └── AppConfiguration.cs
├── wwwroot/              # Static Files
│   └── models/          # 3D Model Files
├── .vscode/             # VS Code Configuration
│   ├── tasks.json
│   └── launch.json
└── Program.cs           # Application Entry Point
```

## Getting Started

### Prerequisites

- .NET 8.0 SDK
- Visual Studio 2022 or VS Code with C# extension

### Installation

1. Clone or download this repository
2. Restore NuGet packages:
   ```powershell
   dotnet restore
   ```

3. Build the project:
   ```powershell
   dotnet build
   ```

### Running the Application

#### Development Mode
```powershell
dotnet run
```

#### Watch Mode (Auto-restart on changes)
```powershell
dotnet watch run
```

The API will be available at:
- HTTP: `http://localhost:5000`
- HTTPS: `https://localhost:5001`
- Swagger UI: `http://localhost:5000` (root path)

### VS Code Tasks

Use `Ctrl+Shift+P` and type "Tasks: Run Task" to access:
- **build** - Build the project
- **run** - Run the application
- **watch** - Run with file watching
- **clean** - Clean build artifacts
- **restore** - Restore NuGet packages

### Debugging in VS Code

1. Open VS Code in the project folder
2. Install the C# extension if not already installed
3. Press `F5` to start debugging
4. The application will launch and open in your browser

## Configuration

### CORS Settings
The application is configured to allow requests from:
- `http://localhost:3001` (React development server)
- `http://localhost:3000` (Alternative React port)

### Model Files
Place your 3D model files in `wwwroot/models/` directory. Supported formats:
- `.glb` - Binary GLTF
- `.gltf` - JSON GLTF
- `.obj` - Wavefront OBJ
- `.stl` - Stereolithography

### Application Configuration
Configuration is stored in `app-config.json` and includes:
- Color management settings
- Camera configuration
- Lighting configuration
- UI preferences

## Testing the API

Use the included `SW.PC.API.Backend.http` file with VS Code REST Client extension to test all endpoints.

## Integration with React Frontend

The backend is designed to work with a React + Babylon.js frontend. Ensure your frontend makes requests to:
```
http://localhost:5000/api/models    # For model listing
http://localhost:5000/api/config    # For configuration
```

## Development Notes

- The project uses ASP.NET Core 8.0 with minimal APIs
- Swagger documentation is available at the root URL in development
- Static file serving is enabled for the wwwroot directory
- Logging is configured for development and production environments

## License

This project is part of the SW.PC software suite.