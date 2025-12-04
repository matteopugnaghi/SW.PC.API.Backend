# SW.PC.API.Backend - Industrial SCADA/HMI Backend

ASP.NET Core 8.0 Web API para sistema SCADA/HMI industrial con visualización 3D, integración TwinCAT PLC y cumplimiento EU Cyber Resilience Act (CRA).

## 🏭 Arquitectura

```
PC Industrial → Backend (Port 5000) → TwinCAT PLC (ADS)
                   ↓
              React Frontend (Port 3001) ← SignalR Real-time
```

**One Backend Per Industrial Installation** - Cada PC ejecuta un backend independiente gestionando un único proyecto configurado via Excel.

---

## 🚀 Inicio Rápido

```powershell
# Restaurar dependencias
dotnet restore

# Compilar
dotnet build

# Ejecutar
dotnet run

# Modo desarrollo (auto-reload)
dotnet watch run
```

**URLs:**
- API: `http://localhost:5000`
- Swagger: `http://localhost:5000` (raíz)

---

## 📋 API Endpoints

### Models API
| Método | Endpoint | Descripción |
|--------|----------|-------------|
| GET | `/api/models` | Lista todos los modelos 3D |
| GET | `/api/models/{id}` | Metadata de modelo específico |
| GET | `/api/models/{id}/download` | Descargar archivo del modelo |
| GET | `/api/models/file/{filename}` | Acceso directo a archivo |

### Configuration API
| Método | Endpoint | Descripción |
|--------|----------|-------------|
| GET | `/api/config` | Configuración completa |
| POST | `/api/config` | Actualizar configuración |
| GET | `/api/config/colors` | Configuración de colores |
| GET | `/api/config/viewer` | Configuración del visor |

### Git Management API (EU CRA Compliance)
| Método | Endpoint | Descripción |
|--------|----------|-------------|
| GET | `/api/git/status` | Estado de todos los repositorios |
| GET | `/api/git/status/{repo}` | Estado de repositorio específico |
| GET | `/api/git/history/{repo}` | Historial de commits |
| POST | `/api/git/commit/{repo}` | Crear commit |
| POST | `/api/git/push/{repo}` | Push a remoto |
| POST | `/api/git/commit-push/{repo}` | Commit + Push + Certificate |
| GET | `/api/git/backup/{repo}` | Descargar ZIP backup con certificado |
| GET | `/api/git/backup-log` | Historial de backups |
| GET | `/api/git/deployment-certificates` | Certificados de deployment |
| GET | `/api/git/deployment-certificates/download` | Descargar todos los certificados JSON |

### Release Management API (CalVer)
| Método | Endpoint | Descripción |
|--------|----------|-------------|
| GET | `/api/git/tags/{repo}` | Lista todos los tags |
| GET | `/api/git/release-info/{repo}` | Info de release actual + sugerida |
| POST | `/api/git/create-release/{repo}` | Crear tag CalVer + push |

### SSH Signing API (EU CRA)
| Método | Endpoint | Descripción |
|--------|----------|-------------|
| GET | `/api/git/ssh-signing/status` | Estado de configuración SSH signing |
| POST | `/api/git/ssh-signing/configure` | Configurar Git para SSH signing |
| POST | `/api/git/ssh-signing/generate-key` | Generar nueva clave SSH Ed25519 |

### Integrity API (EU CRA)
| Método | Endpoint | Descripción |
|--------|----------|-------------|
| GET | `/api/integrity/status` | Estado de integridad del software |
| POST | `/api/integrity/verify` | Verificación manual de integridad |
| POST | `/api/integrity/certificate/generate` | Generar certificado de integridad |
| POST | `/api/integrity/certificate/download` | Descargar certificado JSON |

---

## 🔐 EU Cyber Resilience Act (CRA) Compliance

### Funcionalidades Implementadas

#### 1. **Trazabilidad de Cambios**
- Autor obligatorio en cada commit: `[Autor: Nombre] mensaje`
- Log de todas las operaciones Git
- Historial completo accesible

#### 2. **Deployment Certificates**
- Certificado automático en cada Push
- Incluye: ID único, timestamp, operador, máquina, commit hash, branch
- Hash de integridad SHA256
- Exportable como JSON para auditoría

#### 3. **Release Management (CalVer)**
- Formato: `YYYY.MM.increment` (ej: 2025.12.01, 2025.12.02)
- Incremento automático dentro del mes
- Reset a .01 en nuevo mes
- Autor obligatorio para trazabilidad

#### 4. **SSH Signing (Firma Criptográfica)**
- Detección automática de claves SSH existentes
- Generación de clave Ed25519 si no existe
- Configuración automática de Git para firmar commits
- Verificación local de firmas (`git log --show-signature`)

#### 5. **Software Integrity Verification**
- Verificación automática periódica (configurable)
- Detección de cambios no autorizados
- Certificados de integridad firmados
- Estado CLEAN/DIRTY por componente

#### 6. **Backup con Certificado**
- ZIP con código fuente + certificado de integridad
- Excluye: node_modules, bin, obj, .git
- Nombre: `backup_{repo}_{planta}_{fecha}.zip`
- Log de backups con historial

---

## 🔑 SSH Signing - Guía de Configuración

### Verificar si tienes clave SSH
```powershell
ls ~/.ssh/
```

### Crear nueva clave (si no existe)
```powershell
ssh-keygen -t ed25519 -C "tu.email@empresa.com"
```

### Configurar Git para firmar
```powershell
git config --global gpg.format ssh
git config --global user.signingkey ~/.ssh/id_ed25519.pub
git config --global commit.gpgsign true
git config --global tag.gpgsign true
```

### Verificar firma de commit
```powershell
git log --show-signature -1
```

### Subir clave a Azure DevOps
1. User Settings → SSH public keys
2. Add key
3. Pegar contenido de `~/.ssh/id_ed25519.pub`

> **Nota:** Azure DevOps acepta commits firmados pero NO muestra badge "Verified" en la UI.

---

## 📁 Estructura del Proyecto

```
SW.PC.API.Backend/
├── Controllers/
│   ├── ConfigController.cs      # Configuración de la aplicación
│   ├── GitController.cs         # Git Management + EU CRA
│   ├── IntegrityController.cs   # Verificación de integridad
│   ├── ModelsController.cs      # Gestión de modelos 3D
│   ├── PumpElementsController.cs
│   └── StaticFilesController.cs
├── Services/
│   ├── GitOperationsService.cs      # Operaciones Git + SSH Signing
│   ├── SoftwareIntegrityService.cs  # Verificación de integridad
│   ├── MetricsService.cs            # Métricas del sistema
│   ├── TwinCATService.cs            # Integración PLC (simulado/real)
│   ├── ExcelConfigService.cs        # Configuración desde Excel
│   └── ModelService.cs              # Gestión de modelos 3D
├── Models/
│   ├── ExcelModels.cs           # Modelos de configuración Excel
│   ├── TwinCATModels.cs         # Modelos de variables PLC
│   ├── Model3D.cs               # Modelo de objetos 3D
│   └── AppConfiguration.cs      # Configuración de la app
├── Hubs/
│   └── ScadaHub.cs              # SignalR para tiempo real
├── ExcelConfigs/
│   └── ProjectConfig.xlsm       # Configuración del proyecto
├── wwwroot/
│   └── models/                  # Archivos de modelos 3D
├── CRA_COMPLIANCE/              # Documentación EU CRA
├── Program.cs                   # Entry point + DI
├── appsettings.json            # Configuración
└── app-config.json             # Configuración de la aplicación
```

---

## ⚙️ Configuración

### appsettings.json
```json
{
  "TwinCAT": {
    "AmsNetId": "127.0.0.1.1.1",
    "Port": 851,
    "UseSimulation": true
  },
  "Integrity": {
    "VerificationIntervalSeconds": 120,
    "AutoVerificationEnabled": true
  }
}
```

### CORS (Program.cs)
Configurado para permitir:
- `http://localhost:3000`
- `http://localhost:3001`
- `http://localhost:5173`

### Archivos Ignorados (.gitignore)
```
integrity-state.json
deployment-certificates.json
backup-log.json
```

---

## 🧪 Testing

### REST Client (VS Code)
Usa el archivo `SW.PC.API.Backend.http` con la extensión REST Client.

### Ejemplos cURL

```bash
# Estado de repositorios
curl http://localhost:5000/api/git/status

# Info de release
curl http://localhost:5000/api/git/release-info/backend

# Estado SSH signing
curl http://localhost:5000/api/git/ssh-signing/status

# Verificar integridad
curl -X POST http://localhost:5000/api/integrity/verify \
  -H "Content-Type: application/json" \
  -d '{"verifiedBy": "Admin"}'
```

---

## 📊 Integración con Frontend

El backend está diseñado para trabajar con React + Babylon.js:

```javascript
// API Base
const API_BASE = 'http://localhost:5000';

// Endpoints principales
fetch(`${API_BASE}/api/models`);           // Modelos 3D
fetch(`${API_BASE}/api/config`);           // Configuración
fetch(`${API_BASE}/api/git/status`);       // Estado Git

// SignalR Hub
const connection = new signalR.HubConnectionBuilder()
  .withUrl(`${API_BASE}/hubs/scada`)
  .build();
```

---

## 📝 Notas de Desarrollo

- **TwinCAT Simulation**: Por defecto está activada la simulación PLC
- **Database**: Temporalmente deshabilitada (EF Core culture issues)
- **Excel Config**: Sistema de configuración via Excel implementado
- **Multi-language**: Soporte i18next (ES/EN) en frontend

---

## 📜 Licencia

Parte del software suite SW.PC para automatización industrial.

**EU CRA Compliance**: Este software implementa los requisitos del EU Cyber Resilience Act para trazabilidad, integridad y gestión segura del ciclo de vida del software.
