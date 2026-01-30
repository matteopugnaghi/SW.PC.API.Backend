# 🔐 Sistema de Gestión de Roles y Permisos

## 📋 Descripción General

El sistema implementa un control de acceso granular basado en roles (RBAC - Role-Based Access Control) que permite configurar qué vistas y funcionalidades puede acceder cada rol del sistema.

**Fecha de implementación**: Enero 2026  
**Cumplimiento**: EU CRA (Cyber Resilience Act)  
**Principio**: Mínimo privilegio (Least Privilege)

---

## 🏗️ Arquitectura

### Backend
```
Models/RolePermissions.cs
├── ModulePermissions          # Permisos organizados por módulo/vista
├── ViewPermission             # Permisos individuales (View, Create, Edit, Delete, Export, Execute)
└── DefaultRolePermissions     # Factory de permisos por defecto

Services/RolePermissionsService.cs
├── GetRolePermissionsAsync()      # Obtiene permisos de un rol
├── UpdateRolePermissionsAsync()   # Actualiza permisos
├── HasPermissionAsync()           # Verifica permiso específico
└── ResetToDefaultPermissionsAsync() # Restaura defaults

Controllers/UsersController.cs
├── GET  /api/users/roles/{roleName}/permissions      # Obtener permisos
├── PUT  /api/users/roles/{roleName}/permissions      # Actualizar permisos
├── POST /api/users/roles/{roleName}/permissions/reset # Resetear a defaults
└── GET  /api/users/modules                           # Listar módulos disponibles
```

### Frontend
```
contexts/PermissionsContext.js
├── PermissionsProvider         # Context Provider global
├── usePermissions()            # Hook para validar permisos
└── withPermission()            # HOC para proteger componentes

components/RolePermissionsConfig.js
├── Selector de roles           # UI para seleccionar rol
├── Matriz de permisos          # Tabla interactiva de permisos
└── Botones de acción           # Guardar / Resetear

views/UsersView.js
├── Pestaña "Gestión de Usuarios"       # Gestión existente
└── Pestaña "Configuración de Permisos" # Nueva pestaña
```

---

## 📊 Módulos/Vistas Disponibles

| Módulo | Clave | Categoría | Permisos Disponibles |
|--------|-------|-----------|---------------------|
| 🏠 Vista Principal 3D | `MainView` | Operación | View, Edit, Execute |
| ⚠️ Alarmas | `AlarmsView` | Operación | View, Create, Edit, Delete |
| 📊 Estadísticas | `StatisticsView` | Reportes | View, Export |
| 🧪 Recetas | `RecipesView` | Configuración | View, Create, Edit, Delete, Execute |
| ⚙️ Configuración | `SettingsView` | Configuración | View, Edit |
| 👥 Gestión de Usuarios | `UsersView` | Administración | View, Create, Edit, Delete |
| 📈 Logs de Operación | `OperationLogsView` | Reportes | View, Export |
| 🚂 Tipos de Tren | `TrainTypesView` | Configuración | View, Create, Edit, Delete |
| 🧼 Tipos de Lavado | `WashTypesView` | Configuración | View, Create, Edit, Delete |
| 🔌 Topología EtherCAT | `EtherCATView` | Mantenimiento | View, Edit |
| 🛡️ Auditoría | `AuditView` | Administración | View, Export |
| 💾 Backup y Restauración | `BackupView` | Administración | View, Create |
| 🎮 Modo Manual | `ManualModeView` | Operación | View, Execute |

---

## 🎯 Permisos por Tipo

| Permiso | Clave | Descripción |
|---------|-------|-------------|
| Ver | `canView` | Acceso a la vista (obligatorio para cualquier otro permiso) |
| Crear | `canCreate` | Crear nuevos elementos |
| Editar | `canEdit` | Modificar elementos existentes |
| Eliminar | `canDelete` | Borrar elementos |
| Exportar | `canExport` | Exportar datos (CSV, Excel, PDF) |
| Ejecutar | `canExecute` | Ejecutar acciones críticas (comandos PLC, recetas, etc.) |

---

## 👤 Permisos por Rol (Defaults)

### SuperAdmin (Fabricante)
```json
{
  "MainView": { "canView": true, "canCreate": true, "canEdit": true, "canDelete": true, "canExport": true, "canExecute": true },
  "AlarmsView": { "canView": true, "canCreate": true, "canEdit": true, "canDelete": true, "canExport": true, "canExecute": true },
  // ... TODOS los permisos en TODOS los módulos
}
```

### Administrator (Cliente)
- ✅ **Gestión completa** de usuarios (excepto SuperAdmin)
- ✅ **Configuración** de recetas, alarmas, tipos de lavado
- ✅ **Visualización** de estadísticas, logs, auditoría
- ❌ **NO puede** modificar PLC/TwinCAT ni firmware
- ❌ **NO ve** usuarios SuperAdmin

### Operator
- ✅ **Control** de proceso (vista 3D, modo manual)
- ✅ **Reconocimiento** de alarmas
- ✅ **Ejecución** de recetas
- ❌ **NO puede** configurar sistema ni gestionar usuarios

### Maintenance
- ✅ **Configuración técnica** completa
- ✅ **Gestión** de recetas, tipos de lavado, tipos de tren
- ✅ **Diagnóstico** EtherCAT
- ✅ **Backup** del sistema
- ❌ **NO puede** gestionar usuarios ni seguridad

### Viewer
- ✅ **Solo lectura** de todas las vistas operativas
- ✅ **Exportación** de reportes
- ❌ **NO puede** modificar nada

### Auditor
- ✅ **Acceso total** a logs de auditoría y seguridad
- ✅ **Visualización** de usuarios (sin modificar)
- ✅ **Exportación** de reportes de auditoría
- ❌ **NO puede** controlar proceso ni configurar sistema

---

## 🔐 Jerarquía de Gestión

### SuperAdmin puede configurar permisos de:
- Administrator
- Maintenance
- Operator
- Viewer
- Auditor

### Administrator puede configurar permisos de:
- Maintenance
- Operator
- Viewer
- Auditor

**RESTRICCIÓN IMPORTANTE**: Administrator NO puede modificar permisos de Administrator ni SuperAdmin.

---

## 💻 Uso del Sistema

### 1. Acceder a Configuración de Permisos

1. Login como **SuperAdmin** o **Administrator**
2. Navegar a **👥 Gestión de Usuarios**
3. Click en pestaña **🔐 Configuración de Permisos**

### 2. Configurar Permisos de un Rol

1. Seleccionar rol en el panel izquierdo
2. Habilitar/deshabilitar permisos con checkboxes
3. Click en **💾 Guardar Cambios**

**NOTA**: Si deshabilitas `canView` en un módulo, todos los demás permisos se deshabilitan automáticamente.

### 3. Restaurar Permisos por Defecto

1. Seleccionar rol
2. Click en **🔄 Restaurar Defaults**
3. Confirmar acción

---

## 🔧 Uso para Desarrolladores

### En Backend (C#)

#### Verificar Permiso desde Servicio
```csharp
// Inyectar servicio
private readonly IRolePermissionsService _permissionsService;

// Verificar permiso
bool canEdit = await _permissionsService.HasPermissionAsync("Operator", "MainView", "edit");
if (!canEdit) {
    return Forbid();
}
```

#### Obtener Permisos de un Rol
```csharp
var permissions = await _permissionsService.GetRolePermissionsAsync("Operator");
if (permissions.Modules.AlarmsView.CanEdit) {
    // Permitir edición de alarmas
}
```

### En Frontend (React)

#### Usar Hook de Permisos
```javascript
import { usePermissions } from '../contexts/PermissionsContext';

function MyComponent() {
  const { canView, canEdit, canDelete } = usePermissions();

  if (!canView('AlarmsView')) {
    return <div>🚫 Acceso Denegado</div>;
  }

  return (
    <div>
      <h1>Alarmas</h1>
      {canEdit('AlarmsView') && <button>Editar</button>}
      {canDelete('AlarmsView') && <button>Eliminar</button>}
    </div>
  );
}
```

#### Proteger Componente Completo
```javascript
import { withPermission } from '../contexts/PermissionsContext';

function AlarmsView() {
  return <div>Vista de Alarmas</div>;
}

export default withPermission('AlarmsView')(AlarmsView);
```

#### Verificar Rol Específico
```javascript
const { hasRole, hasAnyRole } = usePermissions();

if (hasRole('SuperAdmin')) {
  // Mostrar opciones de SuperAdmin
}

if (hasAnyRole(['Administrator', 'SuperAdmin'])) {
  // Mostrar opciones de admin
}
```

---

## 📦 Base de Datos

Los permisos se almacenan en la tabla `Roles`:

```sql
CREATE TABLE Roles (
    Id INTEGER PRIMARY KEY,
    Name TEXT NOT NULL,
    Description TEXT,
    SystemRole INTEGER NOT NULL,
    PermissionsJson TEXT,  -- ← JSON con permisos del rol
    IsSystemRole INTEGER DEFAULT 1
);
```

**Ejemplo de PermissionsJson**:
```json
{
  "MainView": {
    "canView": true,
    "canCreate": false,
    "canEdit": true,
    "canDelete": false,
    "canExport": false,
    "canExecute": true
  },
  "AlarmsView": {
    "canView": true,
    "canCreate": false,
    "canEdit": true,
    "canDelete": false,
    "canExport": false,
    "canExecute": false
  }
}
```

---

## 🛡️ Seguridad y Auditoría

### Logs de Auditoría

Todas las modificaciones de permisos se registran en el sistema de auditoría:

```
Categoría: Configuration
Acción: Modified
Descripción: "Permisos del rol Operator actualizados"
Usuario: Administrator
Timestamp: 2026-01-30 14:30:00
```

### Restricciones de Seguridad

1. **Administrator NO puede**:
   - Ver usuarios SuperAdmin
   - Modificar permisos de SuperAdmin
   - Modificar permisos de Administrator
   - Asignar rol SuperAdmin

2. **SuperAdmin puede TODO**:
   - Ver y modificar cualquier rol
   - Asignar cualquier rol (incluido SuperAdmin)

3. **Validación en Backend**:
   - Middleware verifica permisos antes de ejecutar acciones
   - Endpoint `/api/users/roles/{role}/permissions` valida jerarquía

---

## 📝 Notas de Implementación

### Multi-Proyecto
- Cada proyecto tiene su propia configuración de permisos
- Los permisos se almacenan en `Projects/{projectId}/data/project.db`
- No se comparten permisos entre proyectos

### Performance
- Los permisos se cachean en el frontend (Context)
- Se recarga solo al cambiar de usuario o al modificar permisos
- Backend usa `IRequestProjectContext` para acceso rápido por request

### Compatibilidad
- Sistema compatible con EU CRA (Cyber Resilience Act)
- Implementa principio de "mínimo privilegio"
- Auditoría completa de cambios de permisos

---

## 🚀 Roadmap Futuro

- [ ] **Permisos por usuario individual** (override de permisos de rol)
- [ ] **Grupos de usuarios** con permisos compartidos
- [ ] **Permisos temporales** (expires_at)
- [ ] **Delegación de permisos** (un admin delega temporalmente)
- [ ] **Permisos basados en horario** (solo acceso 8am-6pm)
- [ ] **IP whitelisting** por rol

---

## 📞 Soporte

**Documentación técnica**: `docs/development/ROLES_PERMISSIONS.md` (este archivo)  
**Código Backend**: `Models/RolePermissions.cs`, `Services/RolePermissionsService.cs`  
**Código Frontend**: `contexts/PermissionsContext.js`, `components/RolePermissionsConfig.js`  
**API Endpoints**: `Controllers/UsersController.cs` (líneas 350-450)

---

**Última actualización**: 2026-01-30  
**Versión**: 1.0.0  
**Autor**: Sistema AI de Aquafrisch
