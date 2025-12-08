# 📋 ARQUITECTURA DE LOGS - EU CRA / CADRA / Alstom

## Sistema SCADA/HMI Industrial - Aquafrisch Supervisor

**Documento**: Especificación técnica del sistema de logging  
**Versión**: 1.2  
**Fecha**: 8 Diciembre 2025  
**Referencia**: EU CRA Anexo I, Parte I, punto 2l / IEC 62443 / CADRA

---

## 🎯 OBJETIVO

Definir una arquitectura de logs que cumpla con:
1. **EU CRA** - Requisitos de trazabilidad y ciberseguridad
2. **CADRA/Alstom** - Requisitos del sector ferroviario
3. **Operativa** - Útil para diagnóstico y resolución de problemas

---

## ⚠️ DECISIÓN IMPORTANTE: ¿Qué necesitas implementar?

### Para cumplir EU CRA (OBLIGATORIO):
✅ **Solo necesitas NIVEL 1 (AUDIT LOG)** - Ya implementado

### Para mejor operativa (OPCIONAL):
✅ **NIVEL 2 (OPERATION LOG)** - **IMPLEMENTADO** (Vista + Backend + Help)
🟢 **NIVEL 3 (SYSTEM LOG)** - Ya tienes ILogger, mejora opcional con Serilog

### ✅ CONCLUSIÓN VERIFICACIÓN CRA (8 Diciembre 2025)

**El NIVEL 1 (Audit Log) CUMPLE COMPLETAMENTE con el EU CRA.**

El Reglamento (UE) 2024/2847, Anexo I, Parte I, punto 2(l) requiere:
> *"registrar o supervisar datos, funciones o actividades pertinentes para la seguridad interna, con inclusión del acceso a los datos, servicios o funciones, o de su modificación"*

**Lo que YA TIENES implementado cubre esto:**
| Requisito CRA | Implementación | Estado |
|---------------|----------------|--------|
| Acceso a datos/servicios | `UserLogin`, `UserLogout`, `SessionStart` | ✅ |
| Modificación de datos | `FileUpload`, `BackupCreate`, `BackupRestore` | ✅ |
| Acceso a funciones | `GitPush`, `GitPull`, `ExcelConfigLoad` | ✅ |
| Seguridad interna | `UserPasswordChange`, `SecurityViolation`, `IntegrityCheck` | ✅ |
| Integridad de logs | SHA256 + cadena hash | ✅ |
| Retención | Configurable (recomendado: 1825 días = 5 años) | ✅ |

**⚠️ ÚNICO CAMBIO RECOMENDADO:** 
Actualizar `AuditLogRetentionDays` en Excel de 30 a **1825** (5 años mínimo CRA).

### Recomendación:

```
┌─────────────────────────────────────────────────────────────────┐
│  ESTADO ACTUAL (Dic 2025):                                      │
│  ✅ Audit Log (Nivel 1) → CUMPLE EU CRA - 21 acciones activas   │
│  ✅ Operation Log (Nivel 2) → IMPLEMENTADO - Vista operativa    │
│  🟡 System Log (Nivel 3) → ILogger funciona (mejora opcional)   │
│                                                                 │
│  FUTURO (cuando implementes vistas):                            │
│  🟡 Añadir audit log a nuevas funcionalidades                   │
│  🟡 Añadir operation log para acciones de operador              │
│  🟢 Opcionalmente configurar Serilog para archivos              │
└─────────────────────────────────────────────────────────────────┘
```

---

## 📊 ESTADO DE IMPLEMENTACIÓN

### Resumen Rápido

| Funcionalidad | App Implementada | Audit Log |
|---------------|------------------|-----------|
| Autenticación (Login/Logout) | ✅ | ✅ |
| Gestión de Usuarios | ✅ | ✅ |
| SBOM | ✅ | ✅ |
| Vulnerability Scanner | ✅ | ✅ |
| Git Integrity | ✅ | ✅ |
| Certificados | ✅ | ✅ |
| PLC Connection | ✅ | 🔴 Pendiente |
| PLC Variable Write | ✅ (parcial) | 🔴 Pendiente |
| **Alarmas** | 🔴 Vista pendiente | 🔴 Pendiente |
| **Recetas** | 🔴 Vista pendiente | 🔴 Pendiente |
| **Estadísticas** | 🔴 Vista pendiente | 🔴 Pendiente |
| **Process Control** | 🔴 Pendiente | 🔴 Pendiente |
| **Setpoints** | 🔴 Pendiente | 🔴 Pendiente |
| **Backup/Restore** | 🔴 Pendiente | 🔴 Pendiente |
| Exportación | 🔴 Parcial | 🔴 Pendiente |

---

## 📊 TIPOS DE LOGS - ARQUITECTURA DE 3 NIVELES

### Estado de Implementación

| Nivel | Nombre | Obligatorio CRA | Estado | Servicio |
|-------|--------|-----------------|--------|----------|
| **1** | 🔐 AUDIT LOG | ✅ SÍ | ✅ IMPLEMENTADO | `AuditLogService.cs` |
| **2** | 📋 OPERATION LOG | 🟡 Recomendado | ✅ IMPLEMENTADO | `OperationLogService.cs` |
| **3** | 🔧 SYSTEM LOG | ❌ No | 🟡 PARCIAL | `ILogger` de .NET |

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                           ARQUITECTURA DE LOGS                               │
├─────────────────────────────────────────────────────────────────────────────┤
│                                                                             │
│  NIVEL 1: 🔐 AUDIT LOG (CRA/CADRA - Obligatorio) ✅ IMPLEMENTADO           │
│  ════════════════════════════════════════════════════════════════          │
│  • Firma SHA256 + cadena de hashes                                          │
│  • Retención: MÍNIMO 5 años (CRA Art. 13.8)                                │
│  • Envío a SOC externo (opcional)                                           │
│  • Almacenamiento: wwwroot/audit/                                           │
│  • Formato: JSON firmado                                                    │
│  • Servicio: AuditLogService.cs ✅                                          │
│                                                                             │
│  NIVEL 2: 📋 OPERATION LOG (Proceso/Operador) 🔴 PROPUESTA FUTURA          │
│  ════════════════════════════════════════════════════════════════          │
│  • Acciones de operadores en la máquina (setpoints, comandos)               │
│  • Retención: 1-5 años (configurable)                                       │
│  • Almacenamiento: wwwroot/logs/operations/                                 │
│  • Formato: JSON (sin firma, más ligero)                                    │
│  • Servicio: OperationLogService.cs (PENDIENTE CREAR)                       │
│                                                                             │
│  NIVEL 3: 🔧 SYSTEM LOG (Técnico/Debug) 🟡 PARCIAL (ILogger)               │
│  ════════════════════════════════════════════════════════════════          │
│  • Logs técnicos de servicios (conexiones, errores, debug)                  │
│  • Retención: 30 días (configurable)                                        │
│  • Almacenamiento: Console + archivos (configurar Serilog)                  │
│  • Formato: Texto/JSON                                                      │
│  • Ya usa: ILogger<T> de .NET (sale a consola)                              │
│                                                                             │
└─────────────────────────────────────────────────────────────────────────────┘
```

### Resumen de Contenido por Nivel

| Nivel | Qué Registra | Quién lo Usa | Ejemplos |
|-------|--------------|--------------|----------|
| **1 AUDIT** | Acciones de SEGURIDAD | Auditor, CSIRT, SOC | Login, UserCreated, ConfigChange |
| **2 OPERATION** | Acciones de PROCESO | Supervisor, Ingeniero | SetpointChange, RecipeExecute, AlarmAck |
| **3 SYSTEM** | Info TÉCNICA | Desarrollador, Soporte | DB Connected, PLC Error, Model Loaded |

---

## 🔐 NIVEL 1: AUDIT LOG (EU CRA / CADRA)

### Referencia Normativa
- **EU CRA Anexo I, Parte I, punto 2l**: *"registrar o supervisar datos, funciones o actividades pertinentes"*
- **IEC 62443-3-3**: Security levels for audit logging
- **CADRA**: Trazabilidad de acciones de seguridad

### Eventos Obligatorios (EU CRA)

#### ✅ IMPLEMENTADOS (App + Audit Log funcionando)

| Categoría | Acción | Servicio | Archivo |
|-----------|--------|----------|---------|
| **Authentication** | Login | AuthenticationService | AuditLogModels.cs |
| **Authentication** | Logout | AuthenticationService | |
| **Authentication** | LoginFailed | AuthenticationService | |
| **Authentication** | AccountLocked | AuthenticationService | |
| **Authentication** | AccountUnlocked | AuthenticationService | |
| **Authentication** | PasswordChanged | AuthenticationService | |
| **Authentication** | PasswordChangeFailed | AuthenticationService | |
| **Authentication** | PasswordReset | RecoveryController | |
| **Authentication** | LogoutAllSessions | AuthenticationService | |
| **Authentication** | PermissionDenied | UsersController, AuthController | |
| **User Management** | UserCreated | AuthenticationService | |
| **User Management** | UserUpdated | AuthenticationService | |
| **User Management** | UserDeleted | AuthenticationService | |
| **User Management** | AdminCreated | AuthenticationService | |
| **User Management** | RoleChanged | AuthenticationService | |
| **Integrity** | IntegrityVerify | IntegrityController | |
| **Integrity** | IntegrityAutoVerify | IntegrityVerificationService | |
| **SBOM** | SbomGenerate | SbomService | |
| **SBOM** | SbomExport | SbomController | |
| **Vulnerability** | VulnerabilityScan | VulnerabilityService | |
| **Certificate** | CertificateGenerate | IntegrityController | |

**TOTAL IMPLEMENTADO: 21 acciones**

---

#### 🟡 ESTRUCTURA LISTA - Pendiente Audit Log (App parcialmente implementada)

| Categoría | Acción | Dónde Implementar | Estado App |
|-----------|--------|-------------------|------------|
| **Configuration** | ConfigChange | ExcelConfigService | ✅ App funciona |
| **Configuration** | ConfigLoad | ExcelConfigService | ✅ App funciona |
| **Configuration** | ExcelConfigLoad | ExcelConfigService | ✅ App funciona |
| **PLC** | PlcConnect | TwinCATService | ✅ App funciona |
| **PLC** | PlcDisconnect | TwinCATService | ✅ App funciona |
| **PLC** | PlcVariableWrite | TwinCATService/ScadaHub | ✅ Parcial |
| **System** | SystemStart | Program.cs | ✅ App funciona |
| **System** | SystemStop | Program.cs | ✅ App funciona |

**Acción**: Añadir `_auditLog.LogAsync()` en estos servicios

---

#### 🔴 PENDIENTE TODO (Vistas + Backend + Audit Log)

##### Alarmas (AlarmsView.js pendiente)
| Acción | Descripción | Prioridad CRA |
|--------|-------------|---------------|
| AlarmTriggered | Alarma activada | 🟡 Info |
| **AlarmAcknowledge** | Reconocimiento de alarma | 🔴 CRÍTICO |
| AlarmReset | Reset de alarma | 🟡 Media |
| AlarmSilence | Silenciar alarmas | 🟡 Media |
| AlarmConfigChange | Cambio configuración | 🔴 Alta |
| AlarmHistoryExport | Exportar histórico | 🟢 Baja |

##### Recetas (RecipesView.js pendiente)
| Acción | Descripción | Prioridad CRA |
|--------|-------------|---------------|
| RecipeCreate | Crear receta | 🟡 Media |
| RecipeUpdate | Modificar receta | 🟡 Media |
| RecipeDelete | Eliminar receta | 🔴 Alta |
| **RecipeLoad** | Cargar en máquina | 🔴 CRÍTICO |
| **RecipeExecute** | Ejecutar receta | 🔴 CRÍTICO |
| RecipePause | Pausar receta | 🟡 Media |
| RecipeResume | Reanudar receta | 🟡 Media |
| **RecipeAbort** | Abortar receta | 🔴 CRÍTICO |
| RecipeComplete | Receta completada | 🟢 Info |
| RecipeExport/Import | Import/Export | 🟢 Baja |

##### Estadísticas (StatisticsView.js pendiente)
| Acción | Descripción | Prioridad CRA |
|--------|-------------|---------------|
| StatisticsView | Ver estadísticas | 🟢 Info |
| StatisticsExport | Exportar estadísticas | 🟡 Media |
| ReportGenerate | Generar reporte | 🟡 Media |
| ReportExport | Exportar reporte | 🟡 Media |

##### Control de Proceso (pendiente)
| Acción | Descripción | Prioridad CRA |
|--------|-------------|---------------|
| **ProcessStart** | Arranque proceso | 🔴 CRÍTICO |
| **ProcessStop** | Parada proceso | 🔴 CRÍTICO |
| **ProcessEmergencyStop** | Parada emergencia | 🔴 CRÍTICO |
| ProcessModeChange | Cambio modo | 🔴 Alta |
| CommandExecute | Comando manual | 🔴 Alta |

##### Setpoints (pendiente)
| Acción | Descripción | Prioridad CRA |
|--------|-------------|---------------|
| **SetpointChange** | Cambio setpoint | 🔴 CRÍTICO |
| SetpointOverride | Override manual | 🔴 Alta |
| LimitChange | Cambio límites | 🔴 Alta |

##### Backup (pendiente)
| Acción | Descripción | Prioridad CRA |
|--------|-------------|---------------|
| BackupCreate | Crear backup | 🟡 Media |
| **BackupRestore** | Restaurar backup | 🔴 CRÍTICO |
| BackupDelete | Eliminar backup | 🟡 Media |

##### PLC Avanzado (pendiente)
| Acción | Descripción | Prioridad CRA |
|--------|-------------|---------------|
| **PlcModeChange** | Cambio RUN/STOP | 🔴 CRÍTICO |
| PlcProgramDownload | Descarga programa | 🔴 CRÍTICO |
| PlcFirmwareUpdate | Update firmware | 🔴 CRÍTICO |

### Formato de Registro (Audit Log)

```json
{
  "id": "uuid-v4",
  "timestamp": "2025-12-08T10:30:00.000Z",
  "category": "Authentication",
  "action": "Login",
  "result": "Success",
  "userId": "12",
  "userName": "Operator1",
  "ipAddress": "192.168.1.100",
  "details": "Login exitoso desde HMI principal",
  "affectedItemCount": null,
  "durationMs": 125.5,
  "signature": "sha256:abc123...",  // Firma del contenido
  "previousHash": "sha256:xyz789..."  // Cadena de integridad
}
```

### Retención

| Tipo | Mínimo Legal | Recomendado | Configuración |
|------|--------------|-------------|---------------|
| Audit Log (Seguridad) | **5 años** (CRA) | 10 años | `AuditLogRetentionDays` en Excel |
| Audit Log (Operaciones) | 1 año | 5 años | Mismo parámetro |

---

## 📋 NIVEL 2: OPERATION LOG (Acciones de Proceso) 🔴 PROPUESTA FUTURA

> **ESTADO**: No implementado. Es una PROPUESTA para cuando se implementen las vistas de Alarmas, Recetas, etc.
> 
> **¿Es obligatorio para CRA?**: NO directamente, pero es MUY RECOMENDADO para trazabilidad industrial.

### Propósito
Registrar todas las acciones que un operador realiza en la máquina para:
- Diagnóstico de problemas de proceso
- Auditoría de producción
- Análisis de incidentes

### Diferencia con Audit Log

| Aspecto | AUDIT LOG (Nivel 1) | OPERATION LOG (Nivel 2) |
|---------|---------------------|-------------------------|
| **Propósito** | Seguridad/Ciberseguridad | Proceso/Producción |
| **Obligatorio CRA** | ✅ SÍ | 🟡 Recomendado |
| **Firma SHA256** | ✅ SÍ | ❌ No necesario |
| **Envío a SOC** | ✅ Opcional | ❌ No |
| **Retención** | 5-10 años | 1-5 años |
| **Quién lo lee** | Auditor, CSIRT | Supervisor, Ingeniero |
| **Ejemplo** | Login, UserCreated | SetpointChange, RecipeExecute |

### Eventos a Registrar (cuando se implemente)

### Eventos a Registrar

| Categoría | Evento | Datos | Ejemplo |
|-----------|--------|-------|---------|
| **Recetas** | Carga | recipeId, recipeName, user | "Operator1 cargó Receta A" |
| **Recetas** | Ejecución | recipeId, startTime, params | "Iniciada Receta A con temp=80°C" |
| **Recetas** | Pausa/Reanudación | recipeId, reason | "Pausada por falta de material" |
| **Recetas** | Cancelación | recipeId, reason, user | "Cancelada por Operator2" |
| **Recetas** | Finalización | recipeId, duration, result | "Completada en 45 min - OK" |
| **Alarmas** | Reconocimiento | alarmId, alarmText, user | "Operator1 reconoció ALTA TEMP" |
| **Alarmas** | Silenciar | duration, user | "Alarmas silenciadas 5 min" |
| **Setpoints** | Cambio | variable, oldValue, newValue, user | "Temp: 75→80°C por Operator1" |
| **Comandos** | Start/Stop | machine, user | "Operator1 arrancó Bomba 1" |
| **Comandos** | Manual Override | variable, value, user | "Override: Válvula 3 ABIERTA" |
| **Modo** | Cambio Modo | oldMode, newMode, user | "AUTO → MANUAL por Operator1" |

### Formato de Registro (Operation Log)

```json
{
  "id": "uuid-v4",
  "timestamp": "2025-12-08T10:35:00.000Z",
  "type": "SetpointChange",
  "operator": "Operator1",
  "operatorRole": "Operator",
  "station": "HMI-01",
  "variable": "GVL.Temperature_SP",
  "oldValue": 75.0,
  "newValue": 80.0,
  "unit": "°C",
  "reason": null,
  "context": {
    "activeRecipe": "Receta A",
    "machineState": "Running"
  }
}
```

### Retención
- **Mínimo**: 1 año
- **Recomendado**: 5 años (igual que período de soporte)
- **Configurable**: `OperationLogRetentionDays` en Excel

---

## 🔧 NIVEL 3: SYSTEM LOG (Técnico) 🟡 PARCIAL - USA ILogger

> **ESTADO**: Parcialmente implementado. Ya usamos `ILogger<T>` de .NET que escribe a consola.
> 
> **¿Es obligatorio para CRA?**: NO. Es para debug y soporte técnico.
> 
> **Mejora futura**: Configurar Serilog para escribir a archivos JSON con rotación.

### Implementación Actual

```csharp
// Ya existe en todos los servicios
private readonly ILogger<TwinCATService> _logger;

_logger.LogInformation("✅ Connected to PLC at {NetId}:{Port}", netId, port);
_logger.LogWarning("⚠️ PLC connection lost");
_logger.LogError(ex, "❌ Error reading variable {Var}", variableName);
```

**Salida actual**: Consola del servidor (visible en terminal de VS Code)

### Propósito
Logs técnicos para diagnóstico de problemas del sistema, NO para auditoría de seguridad.

### Categorías

| Categoría | Contenido | Ejemplo |
|-----------|-----------|---------|
| **Database** | Conexión, queries, errores | "DB conectada en 125ms" |
| **TwinCAT** | Conexión ADS, errores | "PLC conectado 192.168.1.1:851" |
| **SignalR** | Conexiones, desconexiones | "Cliente web conectado" |
| **Models** | Carga de modelos 3D | "Cargado BOMBA_01.glb (2.5MB)" |
| **Excel** | Carga de configuración | "Config cargada desde ProjectConfig.xlsm" |
| **HTTP** | Requests, latencias | "GET /api/models 200 OK 45ms" |
| **Memory** | Uso de recursos | "RAM: 450MB, CPU: 12%" |
| **Network** | Conectividad | "Internet: OK, GitHub: OK" |

### Formato

```json
{
  "timestamp": "2025-12-08T10:40:00.000Z",
  "level": "Info",  // Debug, Info, Warning, Error
  "source": "TwinCATService",
  "message": "Connected to PLC at 192.168.1.1:851",
  "data": {
    "netId": "192.168.1.1.1.1",
    "port": 851,
    "connectionTimeMs": 125
  }
}
```

### Retención
- **Por defecto**: 30 días
- **Debug logs**: 7 días
- **Configurable**: `SystemLogRetentionDays` en Excel

---

## 🗂️ ESTRUCTURA DE ALMACENAMIENTO

```
wwwroot/
├── audit/                          # NIVEL 1: Audit Log (CRA)
│   ├── audit_2025-12-08.json       # Un archivo por día
│   ├── audit_2025-12-07.json
│   └── index.json                  # Índice de archivos
│
├── logs/
│   ├── operations/                 # NIVEL 2: Operation Log
│   │   ├── ops_2025-12-08.json
│   │   ├── ops_2025-12-07.json
│   │   └── index.json
│   │
│   └── system/                     # NIVEL 3: System Log
│       ├── sys_2025-12-08.json
│       ├── sys_2025-12-07.json
│       └── index.json
│
└── sbom/                           # SBOMs (separado)
    └── ...
```

---

## ⚙️ CONFIGURACIÓN EN EXCEL (SystemConfig)

### Nuevos Parámetros a Añadir

| Parámetro | Tipo | Default | Descripción |
|-----------|------|---------|-------------|
| `AuditLogRetentionDays` | int | 1825 (5 años) | Retención audit log |
| `OperationLogEnabled` | bool | true | Habilitar operation log |
| `OperationLogRetentionDays` | int | 365 (1 año) | Retención operation log |
| `SystemLogEnabled` | bool | true | Habilitar system log |
| `SystemLogRetentionDays` | int | 30 | Retención system log |
| `SystemLogLevel` | string | "Info" | Debug/Info/Warning/Error |
| `LogToConsole` | bool | true | También a consola |
| `LogExternalEnabled` | bool | false | Enviar a SOC externo |
| `LogExternalUrl` | string | "" | URL del SOC |

---

## 📊 VISUALIZACIÓN EN FRONTEND

### Panel INFO - Tabs Propuestos

```
┌─────────────────────────────────────────────────────────────┐
│  📋 LOGS                                                    │
├─────────────────────────────────────────────────────────────┤
│  [🔐 AUDIT] [📋 OPERATIONS] [🔧 SYSTEM]                     │
├─────────────────────────────────────────────────────────────┤
│                                                             │
│  🔐 AUDIT LOG (últimos eventos de seguridad)               │
│  ─────────────────────────────────────────────              │
│  10:30:15 ✅ Login: Operator1 desde 192.168.1.100          │
│  10:28:45 🔒 PasswordChanged: Admin cambió pass            │
│  10:25:00 🔄 IntegrityVerify: Backend CLEAN                │
│  10:20:00 📋 SbomGenerate: 45 componentes                   │
│                                                             │
│  [📤 EXPORT] [🔍 FILTER] [📊 SUMMARY]                       │
│                                                             │
└─────────────────────────────────────────────────────────────┘
```

### Panel OPERATIONS (para Supervisor/Admin)

```
┌─────────────────────────────────────────────────────────────┐
│  📋 OPERATION LOG (últimas acciones de proceso)            │
├─────────────────────────────────────────────────────────────┤
│                                                             │
│  10:35:00 🌡️ Operator1: Temp SP 75→80°C                    │
│  10:32:00 ▶️ Operator1: Inició Receta A                     │
│  10:30:00 ✓ Operator1: Reconoció alarma ALTA PRESIÓN       │
│  10:28:00 🔄 Operator1: Modo AUTO→MANUAL                    │
│  10:25:00 ⏹️ Operator2: Paró Bomba 2                        │
│                                                             │
│  [📤 EXPORT] [🔍 FILTER BY USER] [📊 STATISTICS]           │
│                                                             │
└─────────────────────────────────────────────────────────────┘
```

---

## 🔄 PLAN DE IMPLEMENTACIÓN

### ✅ FASE 0: Estructura Base (COMPLETADA)
- [x] `AuditLogService.cs` - Servicio de logging con SHA256
- [x] `AuditLogModels.cs` - Enums con TODAS las categorías/acciones
- [x] `AuditController.cs` - API endpoints
- [x] Documentación `ARQUITECTURA_LOGS.md`

### 🟡 FASE 1: Añadir Audit a Servicios Existentes (PRÓXIMO)
Servicios que YA funcionan pero les falta el `_auditLog.LogAsync()`:

| Servicio | Acciones a Añadir | Prioridad |
|----------|-------------------|-----------|
| TwinCATService | PlcConnect, PlcDisconnect, PlcVariableWrite | 🔴 Alta |
| ExcelConfigService | ConfigLoad, ConfigChange | 🟡 Media |
| Program.cs | SystemStart | 🟢 Baja |

**Ejemplo de código a añadir:**
```csharp
// En TwinCATService.WriteVariable()
await _auditLog.LogAsync(
    AuditCategory.Plc,
    AuditAction.PlcVariableWrite,
    AuditResult.Success,
    $"Variable {variableName} cambiada: {oldValue} → {newValue}",
    userId, userName
);
```

### 🔴 FASE 2: Implementar Vistas Pendientes (FUTURO)
Cuando implementes cada vista, añadir audit logging:

| Vista | Backend Necesario | Audit Actions |
|-------|-------------------|---------------|
| **AlarmsView.js** | AlarmService, AlarmController | AlarmAcknowledge, AlarmReset, etc. |
| **RecipesView.js** | RecipeService, RecipeController | RecipeLoad, RecipeExecute, etc. |
| **StatisticsView.js** | StatisticsService, ReportController | StatisticsView, ReportGenerate |
| **BackupView.js** | BackupService | BackupCreate, BackupRestore |

### 🔴 FASE 3: Operation Log Service (FUTURO)
Crear servicio separado para logs de operaciones de proceso (menos críticos que audit):

```csharp
// Nuevo servicio: OperationLogService.cs
public interface IOperationLogService
{
    Task LogOperationAsync(OperationType type, string description, 
        string operatorName, object? data = null);
}
```

---

## 📋 CHECKLIST DE CUMPLIMIENTO

### EU CRA (Anexo I, Parte I, punto 2l)

#### ✅ Implementado
- [x] Registrar accesos al sistema (login/logout)
- [x] Registrar intentos de acceso fallidos
- [x] Registrar cambios de credenciales
- [x] Registrar cambios de usuarios/roles
- [x] Registrar verificaciones de integridad
- [x] Firma de integridad en logs (SHA256)
- [x] Cadena de hashes para detectar manipulación
- [x] Retención configurable
- [x] Export de logs para auditoría

#### 🟡 Estructura lista, pendiente implementar
- [ ] Registrar cambios de configuración ← Añadir en ExcelConfigService
- [ ] Registrar escrituras a PLC/variables ← Añadir en TwinCATService
- [ ] Registrar conexión/desconexión PLC ← Añadir en TwinCATService

#### 🔴 Pendiente (vistas no implementadas)
- [ ] Registrar acciones sobre alarmas ← Cuando implementes AlarmsView
- [ ] Registrar uso de recetas ← Cuando implementes RecipesView
- [ ] Registrar cambios de setpoints ← Cuando implementes control de proceso
- [ ] Registrar backups/restores ← Cuando implementes BackupView

### CADRA/Alstom

#### ✅ Implementado
- [x] Trazabilidad de acciones de usuarios
- [x] Separación por niveles de privilegio
- [x] Logs enviables a SOC externo (configurable)
- [x] Timestamps sincronizados (UTC)

#### 🔴 Pendiente
- [ ] Acciones de operadores de proceso ← Cuando implementes vistas

---

## ⚙️ CONFIGURACIÓN RECOMENDADA EN EXCEL

### Retención de Logs

| Parámetro | Valor Actual | Valor Recomendado CRA | Descripción |
|-----------|--------------|----------------------|-------------|
| `AuditLogRetentionDays` | 30 | **1825 (5 años)** | Período de soporte CRA |
| `AuditLogEnabled` | true | true | Mantener habilitado |
| `AuditLogSignatureEnabled` | true | true | SHA256 obligatorio |
| `AuditLogExternalEnabled` | false | true (producción) | Para SOC externo |

**⚠️ ACCIÓN REQUERIDA**: Cambiar `AuditLogRetentionDays` de 30 a 1825 en Excel

---

## 📝 HISTORIAL DE CAMBIOS

| Versión | Fecha | Cambios |
|---------|-------|---------|
| 1.0 | 8 Dic 2025 | Documento inicial |
| 1.1 | 8 Dic 2025 | Actualizado con estado real de implementación. Separado lo implementado de lo pendiente. Añadidas todas las acciones futuras en AuditLogModels.cs |

---

**Documento preparado para cumplimiento con:**
- Reglamento (UE) 2024/2847 - Cyber Resilience Act
- IEC 62443 - Industrial Automation Security
- CADRA/Alstom - Requisitos sector ferroviario
