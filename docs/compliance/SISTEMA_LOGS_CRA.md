# 📋 Sistema de Logs - EU CRA Compliance

Este documento describe el sistema de logging de 3 niveles implementado para cumplimiento con la **EU Cyber Resilience Act (CRA)** y normativas **CADRA/Alstom**.

## 🏗️ Arquitectura de 3 Niveles

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                           SISTEMA DE LOGS                                    │
├─────────────────────────────────────────────────────────────────────────────┤
│                                                                              │
│  ┌─────────────┐    ┌─────────────┐    ┌─────────────┐                      │
│  │     L1      │    │     L2      │    │     L3      │                      │
│  │ AUDIT LOG   │    │ OPERATION   │    │  SESSION    │                      │
│  │   (CRA)     │    │    LOG      │    │ EVENT LOG   │                      │
│  │   🟣        │    │    🔵       │    │    🔵       │                      │
│  └─────────────┘    └─────────────┘    └─────────────┘                      │
│        │                  │                  │                               │
│        ▼                  ▼                  ▼                               │
│  Eventos de          Acciones de       Eventos de                           │
│  Seguridad           Operador          Sesión UI                            │
│                                                                              │
└─────────────────────────────────────────────────────────────────────────────┘
```

---

## 📊 Nivel 1: AUDIT LOG (L1) - EU CRA Compliance

### Propósito
Registro de **eventos de seguridad** para cumplimiento normativo. Incluye firma SHA256 para garantizar integridad.

### Categorías Implementadas

| Categoría | Descripción | Estado |
|-----------|-------------|--------|
| `Integrity` | Verificación de integridad del software | ✅ |
| `Sbom` | Generación de Software Bill of Materials | ✅ |
| `Vulnerability` | Escaneo de vulnerabilidades | ✅ |
| `Authentication` | Login, logout, gestión de usuarios | ✅ |
| `Git` | Operaciones de control de versiones | ✅ |
| `Configuration` | Cambios de configuración del sistema | ✅ |
| `Certificate` | Generación/verificación de certificados | ✅ |
| `System` | Inicio/parada del sistema | ✅ |

### Acciones por Categoría

#### Integrity
- `IntegrityVerify` - Verificación manual de integridad
- `IntegrityAutoVerify` - Verificación automática periódica (cada 2 min)

#### Sbom
- `SbomGenerate` - Generación de SBOM

#### Vulnerability
- `VulnerabilityScan` - Escaneo de vulnerabilidades

#### Authentication
- `Login` - Inicio de sesión
- `Logout` - Cierre de sesión
- `PasswordChange` - Cambio de contraseña
- `UserCreate` - Creación de usuario
- `UserDelete` - Eliminación de usuario
- `UserUpdate` - Actualización de usuario
- `PasswordReset` - Reset de contraseña

#### Git
- `GitCommit` - Commit en repositorio
- `GitPush` - Push a remoto

#### Configuration
- `ConfigChange` - Cambio de configuración

#### Certificate
- `CertificateGenerate` - Generación de certificado de integridad

#### System
- `SystemStart` - Inicio del sistema
- `SystemStop` - Parada del sistema

### Resultados Posibles
- `Success` ✅ - Operación exitosa
- `Warning` ⚠️ - Completado con advertencias
- `Failure` ❌ - Operación fallida
- `Error` ❌ - Error del sistema

### Estructura de Entrada

```json
{
  "id": "guid",
  "timestamp": "2025-12-08T23:26:06Z",
  "category": "Git",
  "action": "GitCommit",
  "result": "Success",
  "userId": "user-id",
  "userName": "Matteo",
  "ipAddress": "192.168.1.100",
  "details": "Commit en frontend: feat(logs): add L1/L2/L3...",
  "durationMs": 45,
  "signature": "sha256-hash...",
  "previousHash": "sha256-previous..."
}
```

### API Endpoints

| Método | Endpoint | Descripción |
|--------|----------|-------------|
| GET | `/api/audit/status` | Estado del sistema de auditoría |
| GET | `/api/audit/recent?count=50` | Logs recientes |
| GET | `/api/audit/logs?From=&To=&Category=` | Consulta con filtros |
| GET | `/api/audit/export?from=&to=` | Exportar logs (JSON) |
| GET | `/api/audit/summary` | Resumen de auditoría |

### Almacenamiento
- **Ubicación**: `wwwroot/audit/audit_YYYY-MM-DD.json`
- **Retención**: Configurable (default 30 días)
- **Firma**: SHA256 por entrada + hash encadenado

---

## 📊 Nivel 2: OPERATION LOG (L2)

### Propósito
Registro de **acciones del operador** en el sistema HMI/SCADA.

### Categorías

| Categoría | Descripción |
|-----------|-------------|
| `Navigation` | Cambios de vista/pantalla |
| `Alarm` | Reconocimiento de alarmas |
| `Recipe` | Carga/ejecución de recetas |
| `Setpoint` | Cambios de consignas |
| `Process` | Start/Stop de proceso |

### Estructura de Entrada

```json
{
  "id": "guid",
  "timestamp": "2025-12-08T23:26:06Z",
  "category": "Navigation",
  "action": "ViewChange",
  "user": "operador1",
  "description": "Cambió a vista Principal",
  "metadata": {}
}
```

### API Endpoints

| Método | Endpoint | Descripción |
|--------|----------|-------------|
| GET | `/api/operationlogs` | Logs de operación |
| GET | `/api/operationlogs?startDate=&endDate=` | Filtrado por fecha |

---

## 📊 Nivel 3: SESSION EVENT LOG (L3)

### Propósito
Eventos de **sesión de usuario** en el frontend (navegación, UI).

### Categorías
- Eventos de navegación
- Interacciones de UI
- Errores de cliente

### Almacenamiento
- Solo en memoria del frontend (sesión actual)
- No persiste en backend

---

## 🖥️ Vista en InfoPanel (Frontend)

### Ubicación
Panel derecho → Sección expandible de logs

### Características

#### Badges de Nivel
- **L1** 🟣 (púrpura) - Audit Log / CRA
- **L2** 🔵 (cyan) - Operation Log
- **L3** 🔵 (cyan) - Session Event Log

#### Filtros de Fecha (L1 y L2)
- **Hoy** - Eventos del día actual
- **Semana** - Últimos 7 días
- **Mes** - Últimos 30 días
- **Personalizado** - Rango de fechas manual

#### Modal Expandido
- Tabla completa con scroll
- Columnas: Fecha/Hora, Categoría/Acción, Resultado, Usuario, Detalles, Firma
- Máximo 400px de altura con scroll

### Estadísticas Mostradas (L1)
- Total de eventos
- Eventos de hoy
- Eventos por categoría
- Eventos por resultado (Success/Warning/Failure)

---

## 🔐 Verificación de Integridad Automática

El sistema ejecuta verificación de integridad cada **2 minutos** (configurable).

### Componentes Verificados

| Componente | Descripción | Verificación |
|------------|-------------|--------------|
| Backend | ASP.NET Core API | Git status (clean/dirty) |
| Frontend | React/Babylon.js | Git status (clean/dirty) |
| TwinCAT PLC | Código PLC | Git status (clean/dirty) |

### Mensajes de Log

#### Éxito
```
✅ Integrity verification PASSED - Backend: verified | Frontend: verified | TwinCAT: verified
```

#### Warning
```
⚠️ Integrity verification with WARNINGS - Backend: MODIFIED (3 files) | Frontend: verified | TwinCAT: UNKNOWN (repo not found)
```

### Configuración de Rutas Git

Las rutas se configuran en **Excel** (`ProjectConfig.xlsm` → hoja "3) System Config"):

| Parámetro | Descripción |
|-----------|-------------|
| `GitRepoBackend` | Ruta al repositorio Backend |
| `GitRepoFrontend` | Ruta al repositorio Frontend |
| `GitRepoTwinCatPlc` | Ruta al repositorio TwinCAT |

---

## 📡 Envío Externo (SOC)

### Configuración
El sistema puede enviar logs a un SOC externo (Security Operations Center).

| Parámetro Excel | Descripción |
|-----------------|-------------|
| `AuditLog_ExternalEnabled` | Habilitar envío externo |
| `AuditLog_ExternalUrl` | URL del endpoint SOC |
| `AuditLog_ExternalApiKey` | API Key para autenticación |

### Formato de Envío
```json
{
  "source": "AquafrischSupervisor",
  "machineId": "MACHINE-001",
  "timestamp": "2025-12-08T23:26:06Z",
  "entry": { ... }
}
```

---

## 🔧 Servicios Backend

### AuditLogService
- **Archivo**: `Services/AuditLogService.cs`
- **Tipo**: Singleton
- **Funciones**: Logging, firma SHA256, flush periódico, envío externo

### OperationLogService
- **Archivo**: `Services/OperationLogService.cs`
- **Tipo**: Singleton
- **Funciones**: Logging de operaciones de operador

### IntegrityVerificationService
- **Archivo**: `Services/IntegrityVerificationService.cs`
- **Tipo**: BackgroundService
- **Funciones**: Verificación periódica de integridad Git

---

## 📋 Controladores

| Controlador | Ruta | Descripción |
|-------------|------|-------------|
| `AuditController` | `/api/audit/*` | Audit Log (L1) |
| `OperationLogsController` | `/api/operationlogs/*` | Operation Log (L2) |

---

## ✅ Checklist de Cumplimiento CRA

- [x] Logging de eventos de seguridad
- [x] Firma SHA256 por entrada
- [x] Hash encadenado (integridad de cadena)
- [x] Verificación de integridad de software
- [x] Trazabilidad de usuarios
- [x] Retención configurable
- [x] Exportación de logs
- [x] Soporte para envío a SOC externo
- [x] SBOM (Software Bill of Materials)
- [x] Escaneo de vulnerabilidades

---

## 📚 Referencias

- **EU Cyber Resilience Act (CRA)**: Regulación europea de ciberseguridad para productos digitales
- **CADRA**: Estándar Alstom de ciberseguridad ferroviaria
- **NIST Cybersecurity Framework**: Marco de referencia para seguridad

---

*Última actualización: 2025-12-08*
