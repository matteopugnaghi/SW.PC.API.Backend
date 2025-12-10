# 💾 DATA MANAGEMENT - Sistema de Backup y Restauración

## Versión: 1.0.0
## Fecha: 2025-12-10
## Cumplimiento: EU CRA Anexo I, Parte I, 2f (Integridad de Datos)

---

## 📋 Resumen

Sistema completo de gestión de datos para backup, restauración, exportación e importación de proyectos industriales. Diseñado para cumplir con los requisitos del EU Cyber Resilience Act sobre integridad y recuperación de datos.

---

## 🏗️ Arquitectura

```
┌─────────────────────────────────────────────────────────────┐
│                    FRONTEND (React)                         │
│  ┌─────────────────────────────────────────────────────┐   │
│  │           DataManagementModal.js                     │   │
│  │  • Lista de backups                                  │   │
│  │  • Crear backup (nombre, descripción, opciones)     │   │
│  │  • Restaurar backup                                  │   │
│  │  • Exportar/Importar (Drag & Drop)                  │   │
│  │  • Eliminar backups                                  │   │
│  └─────────────────────────────────────────────────────┘   │
└─────────────────────────────────────────────────────────────┘
                              │
                              ▼ API REST
┌─────────────────────────────────────────────────────────────┐
│                    BACKEND (ASP.NET Core)                   │
│  ┌─────────────────────────────────────────────────────┐   │
│  │           BackupController.cs                        │   │
│  │  GET    /api/backup          → Lista backups        │   │
│  │  GET    /api/backup/status   → Estado del sistema   │   │
│  │  GET    /api/backup/{id}     → Info de un backup    │   │
│  │  POST   /api/backup          → Crear backup         │   │
│  │  POST   /api/backup/restore  → Restaurar            │   │
│  │  GET    /api/backup/{id}/export → Descargar ZIP     │   │
│  │  POST   /api/backup/import   → Importar ZIP         │   │
│  │  DELETE /api/backup/{id}     → Eliminar backup      │   │
│  │  GET    /api/backup/{id}/verify → Verificar integ.  │   │
│  └─────────────────────────────────────────────────────┘   │
│                              │                              │
│                              ▼                              │
│  ┌─────────────────────────────────────────────────────┐   │
│  │              BackupService.cs                        │   │
│  │  • Gestión de archivos ZIP                          │   │
│  │  • Copia segura de SQLite (FileShare.ReadWrite)     │   │
│  │  • Manifest JSON con metadatos                      │   │
│  │  • Certificados de integridad                       │   │
│  │  • Soporte formato antiguo y nuevo                  │   │
│  └─────────────────────────────────────────────────────┘   │
└─────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌─────────────────────────────────────────────────────────────┐
│                   ALMACENAMIENTO                            │
│                                                             │
│  Projects/{projectId}/backups/                              │
│  ├── backup_projectId_20251210_103000.zip                  │
│  ├── backup_projectId_20251210_140000.zip                  │
│  └── ...                                                    │
│                                                             │
│  Contenido de cada ZIP:                                     │
│  ├── manifest.json          ← Metadatos del backup         │
│  ├── backup_certificate.json ← Firma de integridad         │
│  ├── config/                 ← Configuración Excel         │
│  │   └── ProjectConfig.xlsm                                │
│  ├── models/                 ← Modelos 3D                  │
│  │   └── *.glb                                             │
│  └── data/                   ← Base de datos               │
│      └── project.db                                         │
└─────────────────────────────────────────────────────────────┘
```

---

## 📦 Contenido de un Backup

### 1. Manifest (manifest.json)
```json
{
  "manifestVersion": "1.0",
  "backupInfo": {
    "id": "backup_A70.AMITWP_20251210_103000",
    "projectId": "A70.AMITWP",
    "name": "[A70.AMITWP] Backup - 2025-12-10 10:30",
    "description": "Backup antes de actualización",
    "createdAt": "2025-12-10T10:30:00Z",
    "createdBy": "admin",
    "type": "Manual",
    "appVersion": "1.0.0"
  },
  "contents": {
    "hasConfig": true,
    "hasModels": true,
    "hasDatabase": true,
    "configFiles": ["ProjectConfig.xlsm"],
    "modelFiles": ["modelo1.glb", "modelo2.glb"],
    "databaseFile": "project.db"
  }
}
```

### 2. Certificado de Integridad (backup_certificate.json)
```json
{
  "version": "1.0",
  "generatedAt": "2025-12-10T10:30:00Z",
  "backupId": "backup_A70.AMITWP_20251210_103000",
  "hashes": {
    "manifest": "sha256:abc123...",
    "config": "sha256:def456...",
    "models": "sha256:ghi789...",
    "database": "sha256:jkl012..."
  },
  "signature": "sha256:xyz..."
}
```

---

## 🔧 API Endpoints

### GET /api/backup
Lista todos los backups del proyecto activo.

**Response:**
```json
{
  "backups": [...],
  "totalCount": 5,
  "config": {
    "maxBackups": 10,
    "autoBackupEnabled": false,
    "retentionDays": 30
  }
}
```

### POST /api/backup
Crea un nuevo backup.

**Request Body:**
```json
{
  "name": "Pre-Actualización",
  "description": "Backup antes de actualizar a v2.0",
  "includeConfig": true,
  "includeModels": true,
  "includeDatabase": true
}
```

**Response:**
```json
{
  "success": true,
  "message": "Backup created successfully",
  "backupId": "backup_A70.AMITWP_20251210_103000",
  "backupInfo": {...}
}
```

### POST /api/backup/restore
Restaura desde un backup existente.

**Request Body:**
```json
{
  "backupId": "backup_A70.AMITWP_20251210_103000",
  "restoreConfig": true,
  "restoreModels": true,
  "restoreDatabase": true,
  "createBackupFirst": true
}
```

### GET /api/backup/{id}/export
Descarga el archivo ZIP del backup.

### POST /api/backup/import
Importa un backup desde archivo ZIP (multipart/form-data).

### DELETE /api/backup/{id}
Elimina un backup.

---

## 📋 Nomenclatura de Backups

### Formato del Nombre
```
[{ProjectId}] {Nombre} - {YYYY-MM-DD HH:mm}
```

### Ejemplos
- `[A70.AMITWP] Backup - 2025-12-10 10:30`
- `[A70.AMITWP] Pre-Actualización - 2025-12-10 14:00`
- `[default] Backup Inicial - 2025-12-10 08:00`

### Formato del Archivo
```
backup_{projectId}_{YYYYMMDD_HHmmss}.zip
```

---

## 🔄 Compatibilidad de Formatos

El sistema soporta tanto el formato nuevo como el antiguo:

### Formato Antiguo (pre v1.0)
```
backup_20251210_103000.zip
```

### Formato Nuevo (v1.0+)
```
backup_A70.AMITWP_20251210_103000.zip
```

La búsqueda de backups es flexible y encuentra ambos formatos.

---

## 💾 Copia Segura de SQLite

Para evitar errores de "database is locked" al copiar la base de datos mientras está en uso:

```csharp
// Abrir archivo fuente permitiendo lectura/escritura compartida
using var sourceStream = new FileStream(
    sourcePath, 
    FileMode.Open, 
    FileAccess.Read, 
    FileShare.ReadWrite  // ← Clave para SQLite
);

// Copiar a archivo temporal
using var tempStream = File.Create(tempPath);
await sourceStream.CopyToAsync(tempStream);
```

---

## 🎨 Interfaz de Usuario (Frontend)

### DataManagementModal
Modal accesible desde el botón "DATA" en SOFTWARE INTEGRITY.

**Características:**
- Lista de backups con información detallada
- Crear backup con nombre y descripción opcionales
- Restaurar con selección de componentes
- Export/Download de backups
- Import con Drag & Drop
- Eliminación con confirmación

### Drag & Drop Import
```javascript
const handleDrop = async (e) => {
  e.preventDefault();
  const file = e.dataTransfer.files[0];
  if (file && file.name.endsWith('.zip')) {
    await api.importBackup(file);
  }
};
```

---

## 📊 Configuración (Excel SystemConfig)

| Parámetro | Descripción | Default |
|-----------|-------------|---------|
| BackupEnabled | Habilita sistema de backup | true |
| BackupMaxCount | Número máximo de backups | 10 |
| BackupRetentionDays | Días de retención | 30 |
| BackupAutoEnabled | Backup automático | false |
| BackupAutoIntervalHours | Intervalo auto-backup | 24 |

---

## 🔐 Seguridad y Auditoría

### Registro en Audit Log
Todas las operaciones de backup se registran:
- `Backup/Create` - Creación de backup
- `Backup/Restore` - Restauración
- `Backup/Delete` - Eliminación
- `Backup/Export` - Exportación
- `Backup/Import` - Importación

### Verificación de Integridad
- Hash SHA256 de cada componente
- Firma del manifest completo
- Verificación opcional al restaurar

---

## 📁 Archivos Relacionados

### Backend
- `Controllers/BackupController.cs` - API REST
- `Services/BackupService.cs` - Lógica de negocio
- `Services/BackupCertificateService.cs` - Certificados
- `Models/BackupModels.cs` - DTOs

### Frontend
- `components/DataManagementModal.js` - UI
- `services/api.js` - Cliente API

---

## 📚 Referencias

- [EU CRA Anexo I, Parte I, 2f](https://eur-lex.europa.eu/) - Integridad de Datos
- [MULTI_PROJECT_SYSTEM.md](./MULTI_PROJECT_SYSTEM.md) - Sistema multi-proyecto
- [ARQUITECTURA_LOGS.md](./ARQUITECTURA_LOGS.md) - Sistema de logs
