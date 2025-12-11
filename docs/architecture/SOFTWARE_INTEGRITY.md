# 🔐 SOFTWARE INTEGRITY - Panel de Integridad del Sistema

## Versión: 1.0.0
## Fecha: 2025-01-01
## Cumplimiento: EU CRA Anexo I, Parte II, 1a (Verificación de Integridad)

---

## 📋 Resumen

El panel **SOFTWARE INTEGRITY** proporciona verificación en tiempo real de la integridad de todos los componentes del sistema. Diseñado para cumplir con los requisitos del EU Cyber Resilience Act sobre protección contra manipulaciones no autorizadas del software.

---

## 🎨 Interfaz de Usuario

### Acceso
Ubicado en el panel inferior izquierdo de la interfaz SCADA (InfoPanel).

### Vista Principal
```
┌──────────────────────────────────────────────────┐
│           🔐 SOFTWARE INTEGRITY                  │
├──────────────────────────────────────────────────┤
│  SYSTEM STATUS: 🟢 OPERATIONAL                   │
├──────────────────────────────────────────────────┤
│  Backend   🟢 ━━━━━━━━━━━━━━━━ ✓ CLEAN          │
│  Frontend  🟢 ━━━━━━━━━━━━━━━━ ✓ CLEAN          │
│  TwinCAT   🟢 ━━━━━━━━━━━━━━━━ ✓ CLEAN          │
├──────────────────────────────────────────────────┤
│  [🔏 SIGNED]  [🚀 DEPLOYED]                      │
├──────────────────────────────────────────────────┤
│  [📋 LOGS]  [📦 SBOM]  [💾 DATA]                 │
└──────────────────────────────────────────────────┘
```

---

## 🚦 Indicadores de Estado (Luces)

### Colores de Estado por Componente

| Color | Icono | Estado | Descripción |
|-------|-------|--------|-------------|
| 🟢 Verde | ✓ | **CLEAN** | Todos los archivos verificados, sin modificaciones |
| 🟡 Amarillo | ⚠ | **MODIFIED** | Archivos modificados localmente (cambios detectados) |
| 🔴 Rojo | ✗ | **ERROR** | Error en verificación (repositorio no encontrado, etc.) |
| 🔵 Azul | 🚀 | **DEPLOYED** | Sistema desplegado sin repositorio Git (producción) |
| ⚪ Gris | ? | **UNKNOWN** | Estado no determinado |

### Detalle de Estados

#### 🟢 CLEAN (Verde)
```
Estado ideal en desarrollo.
- Git repositorio presente y accesible
- Todos los archivos coinciden con último commit
- Sin cambios staged o unstaged
```

#### 🟡 MODIFIED (Amarillo)
```
Hay cambios locales no commiteados.
- En desarrollo: Indica trabajo en progreso
- En producción: ⚠️ Potencial manipulación no autorizada
```

#### 🔴 ERROR (Rojo)
```
No se pudo verificar la integridad.
- Repositorio Git no encontrado
- Error de acceso a archivos
- Permisos insuficientes
```

#### 🚀 DEPLOYED (Azul)
```
Estado normal en producción.
- Sistema desplegado sin repositorio Git
- Usa deploy-version.json para tracking
- Integridad verificada por firma del deploy
```

---

## 🏷️ Badges de Estado

### Badge de Firma Digital

| Badge | Significado |
|-------|-------------|
| 🔏 **SIGNED** | Deploy firmado digitalmente (SHA256 verificado) |
| ⚠️ **UNSIGNED** | Deploy sin firma o firma no verificada |

**¿Cuándo aparece SIGNED?**
- El script de deploy (`Deploy-Manual-Remote.ps1`) genera automáticamente:
  ```json
  // deploy-version.json
  {
    "IsSigned": true,
    "SignatureStatus": "signed",
    "DeployedAt": "2025-01-01T10:30:00Z",
    "DeployedFrom": "DEV-PC",
    ...
  }
  ```

### Badge de Verificación

| Badge | Significado |
|-------|-------------|
| ✓ **VERIFIED** | Componentes verificados vía Git (desarrollo) |
| 🚀 **DEPLOYED** | Componentes verificados vía deploy-version.json (producción) |
| ⚠️ **N/A** | Estado no disponible o error |

---

## 📊 SYSTEM STATUS (Estado del Sistema)

### Cálculo del Estado Global

El estado del sistema se calcula automáticamente basándose en los tres componentes:

```javascript
// Lógica de cálculo
function calculateSystemStatus(backend, frontend, twincat) {
    const allStates = [backend, frontend, twincat];
    
    // Si todos están CLEAN o DEPLOYED → OPERATIONAL
    if (allStates.every(s => s === 'clean' || s === 'deployed')) {
        return { text: 'OPERATIONAL', color: 'green', icon: '🟢' };
    }
    
    // Si alguno tiene ERROR → ERROR
    if (allStates.some(s => s === 'error')) {
        return { text: 'ERROR', color: 'red', icon: '🔴' };
    }
    
    // Si alguno está MODIFIED → WARNING
    if (allStates.some(s => s === 'modified')) {
        return { text: 'WARNING', color: 'yellow', icon: '🟡' };
    }
    
    // Default: UNKNOWN
    return { text: 'UNKNOWN', color: 'gray', icon: '⚪' };
}
```

### Estados Posibles

| Estado | Icono | Condición |
|--------|-------|-----------|
| 🟢 OPERATIONAL | ✓ | Todos los componentes CLEAN o DEPLOYED |
| 🟡 WARNING | ⚠ | Al menos un componente MODIFIED |
| 🔴 ERROR | ✗ | Al menos un componente con ERROR |
| ⚪ UNKNOWN | ? | Estado indeterminado |

---

## 🔄 Verificación de Integridad

### En Desarrollo (con Git)
```
┌─────────────────────────────────────────────────────┐
│                    Git Repository                    │
│  HEAD: abc123...                                     │
│                                                      │
│  ┌─────────┐   git status   ┌──────────────────┐   │
│  │ Backend │ ──────────────→ │ Clean / Modified │   │
│  │  Files  │                 │     staged       │   │
│  └─────────┘                 └──────────────────┘   │
└─────────────────────────────────────────────────────┘
```

### En Producción (sin Git)
```
┌─────────────────────────────────────────────────────┐
│            deploy-version.json                       │
│  {                                                   │
│    "IsSigned": true,                                 │
│    "SignatureStatus": "signed",                      │
│    "DeployedAt": "2025-01-01T10:30:00Z"             │
│  }                                                   │
│                                                      │
│  Resultado: 🔏 SIGNED + 🚀 DEPLOYED                  │
└─────────────────────────────────────────────────────┘
```

---

## 📋 Resumen del Panel

### Sección "Verification Summary"

| Estado | Icono | Texto |
|--------|-------|-------|
| ✅ Todo OK | ✓ | "ALL VERIFIED" |
| ✅ Producción OK | ✓ | "DEPLOYED - All components deployed" |
| ⚠️ Modificado | ⚠ | "MODIFIED - {n} files changed" |
| ❌ Error | ✗ | "ERROR - Verification failed" |

---

## 🛠️ Botones de Acción

### [📋 LOGS] - Ver Logs de Auditoría
Abre el modal de logs del sistema (EU CRA compliance).
- Filtros por tipo de evento
- Exportación JSON/CSV
- Visualización detallada

### [📦 SBOM] - Software Bill of Materials
Muestra las dependencias del sistema (EU CRA Anexo I).
- Lista de paquetes NuGet (backend)
- Lista de paquetes NPM (frontend)
- Versiones y licencias

### [💾 DATA] - Gestión de Datos
Abre el modal de backup y restauración.
- Crear backup manual
- Restaurar desde backup
- Exportar/Importar proyectos
- Ver backups existentes

---

## ⚙️ Configuración

### Verificación Automática
El sistema verifica la integridad automáticamente cada **2 minutos**.

**Configuración** (`appsettings.json`):
```json
{
  "IntegrityVerification": {
    "AutoVerifyIntervalMinutes": 2,
    "Enabled": true
  }
}
```

### API Endpoints

| Endpoint | Método | Descripción |
|----------|--------|-------------|
| `/api/integrity/status` | GET | Estado completo de integridad |
| `/api/integrity/verify` | POST | Forzar verificación manual |
| `/api/integrity/certificate` | GET | Obtener certificado de integridad |

---

## 📁 Archivos Relacionados

### Backend
- `Services/SoftwareIntegrityService.cs` - Lógica de verificación
- `Services/IntegrityVerificationService.cs` - Background service
- `Controllers/IntegrityController.cs` - API REST
- `deploy-version.json` - Metadatos de deploy (producción)
- `integrity-state.json` - Estado de integridad persistido

### Frontend
- `components/InfoPanel.js` - Panel de integridad (UI)
- `components/SbomViewModal.js` - Modal SBOM
- `components/AuditLogModal.js` - Modal de logs
- `components/DataManagementModal.js` - Modal de backups

---

## 🔐 Cumplimiento EU CRA

Este panel cumple con:

| Requisito | Artículo | Implementación |
|-----------|----------|----------------|
| Verificación de integridad | Anexo I, II, 1a | Verificación automática cada 2 min |
| Detección de manipulaciones | Anexo I, II, 1b | Comparación con baseline (Git/deploy-version) |
| Registro de eventos | Anexo I, II, 3 | Audit log de verificaciones |
| SBOM | Anexo I, VII | Lista de dependencias exportable |

---

## ⚠️ Mensajes en Audit Log

### "Automatic integrity verification with WARNINGS"

En producción es **normal** ver este mensaje en el Audit Log:

```
⚠️ Automatic integrity verification with WARNINGS - Backend: deployed | Frontend: deployed | TwinCAT: verified
```

**¿Por qué aparece como WARNING?**

El sistema de verificación automática (`IntegrityVerificationService`) está diseñado para ser cauteloso:

| Estado | Nivel de Log | Contexto |
|--------|-------------|----------|
| `clean` | ✅ INFO | **Desarrollo**: Git verificó todos los archivos |
| `deployed` | ⚠️ WARNING | **Producción**: Sin Git, usa deploy-version.json |
| `modified` | ⚠️ WARNING | Cambios detectados (investigar en producción) |
| `error` | ❌ ERROR | No se pudo verificar |

**¿Es un problema?**

**NO** - En producción, `deployed` es el estado correcto y esperado. El WARNING es informativo, indica que:
- El sistema detecta que está en modo producción (sin repositorio Git)
- La integridad se verifica mediante `deploy-version.json` en lugar de Git
- Los componentes fueron desplegados correctamente desde desarrollo

**¿Cuándo preocuparse?**

| Mensaje | Acción |
|---------|--------|
| `Backend: deployed` | ✅ Normal en producción |
| `Frontend: deployed` | ✅ Normal en producción |
| `TwinCAT: verified` | ✅ Normal (verificado externamente) |
| `Backend: modified` | ⚠️ **Investigar** - archivos cambiados post-deploy |
| `Backend: error` | ❌ **Investigar** - problema de verificación |

### Resumen de Estados por Entorno

| Entorno | Estado Esperado | Nivel Log |
|---------|----------------|-----------|
| **Desarrollo** | `clean` | INFO |
| **Producción** | `deployed` | WARNING (informativo) |
| **Problema** | `modified` / `error` | WARNING / ERROR |

> **Nota técnica**: El sistema usa WARNING para `deployed` porque no puede verificar archivos en tiempo real como hace Git. Sin embargo, la firma digital (`IsSigned: true`) garantiza que el deploy es auténtico.

---

## 📚 Referencias

- [SISTEMA_LOGS_CRA.md](../compliance/SISTEMA_LOGS_CRA.md) - Sistema de logs
- [DATA_MANAGEMENT.md](./DATA_MANAGEMENT.md) - Sistema de backups
- [INSTALACION_PRODUCCION.md](../deployment/INSTALACION_PRODUCCION.md) - Deploy
- [EU CRA Regulation](https://eur-lex.europa.eu/) - Cyber Resilience Act
