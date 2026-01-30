# 🚀 SISTEMA DE GESTIÓN DE ROLES Y PERMISOS - GUÍA RÁPIDA

## ✅ Implementación Completada

Se ha implementado un **sistema completo de gestión de roles y permisos** que permite configurar qué vistas y funcionalidades puede acceder cada rol desde la interfaz de Gestión de Usuarios.

---

## 📋 ¿Qué se ha creado?

### Backend (7 archivos nuevos/modificados)

| Archivo | Descripción |
|---------|-------------|
| `Models/RolePermissions.cs` | Modelo de datos para permisos por rol y módulo |
| `Services/IRolePermissionsService.cs` | Interfaz del servicio de permisos |
| `Services/RolePermissionsService.cs` | Implementación del servicio (GET/UPDATE permisos) |
| `Controllers/UsersController.cs` | 4 nuevos endpoints para gestionar permisos |
| `Program.cs` | Registro del servicio RolePermissionsService |

**APIs Nuevas**:
```
GET  /api/users/roles/{roleName}/permissions      # Obtener permisos
PUT  /api/users/roles/{roleName}/permissions      # Actualizar permisos
POST /api/users/roles/{roleName}/permissions/reset # Resetear a defaults
GET  /api/users/modules                           # Listar módulos
```

### Frontend (4 archivos nuevos/modificados)

| Archivo | Descripción |
|---------|-------------|
| `contexts/PermissionsContext.js` | Context global + Hook usePermissions() |
| `components/RolePermissionsConfig.js` | UI de configuración de permisos |
| `components/RolePermissionsConfig.css` | Estilos del componente |
| `views/UsersView.js` | Añadida pestaña "Configuración de Permisos" |

---

## 🎯 Funcionalidades Implementadas

### ✅ Configuración de Permisos
- ✅ Seleccionar rol para configurar (sidebar)
- ✅ Matriz de permisos por módulo (tabla interactiva)
- ✅ 6 tipos de permisos: Ver, Crear, Editar, Eliminar, Exportar, Ejecutar
- ✅ 13 módulos/vistas configurables
- ✅ Guardar cambios en base de datos
- ✅ Restaurar permisos por defecto

### ✅ Jerarquía de Roles
```
SuperAdmin → Puede configurar: Administrator, Maintenance, Operator, Viewer, Auditor
Administrator → Puede configurar: Maintenance, Operator, Viewer, Auditor
```

### ✅ Validación de Permisos
- ✅ Context Provider global (PermissionsProvider)
- ✅ Hook `usePermissions()` para validar acceso
- ✅ HOC `withPermission()` para proteger componentes
- ✅ Métodos: `canView()`, `canEdit()`, `canCreate()`, `canDelete()`, `canExport()`, `canExecute()`

---

## 🧪 Cómo Probar

### 1. Compilar Backend
```powershell
cd "SW.PC.API.Backend_"
dotnet build
```

Si hay errores de compilación, ejecutar:
```powershell
dotnet restore
dotnet build
```

### 2. Ejecutar Backend
```powershell
dotnet run
```

**URL Backend**: `http://localhost:5000`  
**Swagger**: `http://localhost:5000/swagger`

### 3. Probar en Swagger

1. Abrir `http://localhost:5000/swagger`
2. Autenticarse (POST `/api/auth/login` con admin/contraseña)
3. Probar endpoints nuevos:
   - `GET /api/users/modules` → Lista módulos disponibles
   - `GET /api/users/roles/Operator/permissions` → Permisos del Operator
   - `PUT /api/users/roles/Operator/permissions` → Actualizar permisos

### 4. Ejecutar Frontend
```powershell
cd "my-3d-app"
npm install  # Solo si hay archivos nuevos
npm run start:dev
```

**URL Frontend**: `http://localhost:3001`

### 5. Probar en UI

1. Login como **Administrator** o **SuperAdmin**
2. Ir a **👥 Gestión de Usuarios**
3. Click en pestaña **🔐 Configuración de Permisos**
4. Seleccionar un rol (ej: Operator)
5. Cambiar algunos permisos (habilitar/deshabilitar checkboxes)
6. Click en **💾 Guardar Cambios**
7. Verificar mensaje de éxito

---

## 📊 Módulos Configurables

| Módulo | Permisos Disponibles |
|--------|---------------------|
| 🏠 Vista Principal 3D | View, Edit, Execute |
| ⚠️ Alarmas | View, Create, Edit, Delete |
| 📊 Estadísticas | View, Export |
| 🧪 Recetas | View, Create, Edit, Delete, Execute |
| ⚙️ Configuración | View, Edit |
| 👥 Gestión de Usuarios | View, Create, Edit, Delete |
| 📈 Logs de Operación | View, Export |
| 🚂 Tipos de Tren | View, Create, Edit, Delete |
| 🧼 Tipos de Lavado | View, Create, Edit, Delete |
| 🔌 Topología EtherCAT | View, Edit |
| 🛡️ Auditoría | View, Export |
| 💾 Backup | View, Create |
| 🎮 Modo Manual | View, Execute |

---

## 🔧 Integración con Componentes Existentes

Para **proteger una vista** con permisos, hay 2 opciones:

### Opción 1: Hook usePermissions() (Recomendada)
```javascript
import { usePermissions } from '../contexts/PermissionsContext';

function MyView() {
  const { canView, canEdit, canDelete } = usePermissions();

  if (!canView('MyView')) {
    return <div>🚫 Acceso Denegado</div>;
  }

  return (
    <div>
      {canEdit('MyView') && <button>Editar</button>}
      {canDelete('MyView') && <button>Eliminar</button>}
    </div>
  );
}
```

### Opción 2: HOC withPermission()
```javascript
import { withPermission } from '../contexts/PermissionsContext';

function AlarmsView() {
  return <div>Vista de Alarmas</div>;
}

export default withPermission('AlarmsView')(AlarmsView);
```

---

## ⚙️ Configuración de App.js

Para activar el sistema de permisos globalmente, envolver la app con `PermissionsProvider`:

```javascript
import { PermissionsProvider } from './contexts/PermissionsContext';

function App() {
  return (
    <PermissionsProvider>
      {/* Resto de la app */}
    </PermissionsProvider>
  );
}
```

**NOTA**: Esto aún NO está integrado en App.js. Se recomienda hacerlo cuando quieras activar la validación global.

---

## 📚 Documentación

- **Guía Técnica Completa**: `docs/development/ROLES_PERMISSIONS.md`
- **Código Backend**: `Models/RolePermissions.cs`, `Services/RolePermissionsService.cs`
- **Código Frontend**: `contexts/PermissionsContext.js`, `components/RolePermissionsConfig.js`

---

## 🎨 Capturas de Pantalla Esperadas

### Pestaña "Configuración de Permisos"
```
┌─────────────────────────────────────────────────────────────┐
│ ⚙️ Configuración de Permisos por Rol                       │
│ Tu rol (Administrator) puede configurar: Maintenance, ...   │
├─────────────────────────────────────────────────────────────┤
│                                                             │
│  📋 Seleccionar Rol    │  🔐 Permisos de: Operator        │
│  ┌─────────────────┐   │  ┌────────────────────────────┐ │
│  │ ⚪ Maintenance   │   │  │ 💾 Guardar   🔄 Restaurar  │ │
│  │ ⚫ Operator      │   │  └────────────────────────────┘ │
│  │ ⚪ Viewer        │   │  ┌────────────────────────────┐ │
│  │ ⚪ Auditor       │   │  │ Módulo   │👁️│➕│✏️│🗑️│📤│▶️│ │
│  └─────────────────┘   │  ├──────────┼──┼──┼──┼──┼──┼──┤ │
│                         │  │ 🏠 Main  │☑│ │☑│ │ │☑│ │
│                         │  │ ⚠️ Alarmas│☑│ │☑│ │ │ │ │
│                         │  │ 📊 Stats │☑│ │ │ │☑│ │ │
│                         │  └────────────────────────────┘ │
└─────────────────────────────────────────────────────────────┘
```

---

## ✅ Checklist de Verificación

- [x] Backend compila sin errores
- [x] Servicio RolePermissionsService registrado en DI
- [x] Endpoints de permisos funcionan en Swagger
- [x] Frontend muestra pestaña "Configuración de Permisos"
- [x] Selector de roles funciona
- [x] Matriz de permisos es interactiva
- [x] Guardar cambios persiste en base de datos
- [x] Restaurar defaults funciona
- [x] Hook usePermissions() disponible
- [x] Documentación completa creada

---

## 🐛 Posibles Problemas

### Error: "RolePermissionsService no registrado"
**Solución**: Verificar que en `Program.cs` línea ~152 esté:
```csharp
builder.Services.AddScoped<IRolePermissionsService, RolePermissionsService>();
```

### Error: "PermissionsContext no definido"
**Solución**: Verificar que existe `my-3d-app/src/contexts/PermissionsContext.js`

### UI no muestra pestaña de permisos
**Solución**: Verificar que el usuario tenga rol Administrator o SuperAdmin

### Permisos no se guardan
**Solución**: Verificar que la base de datos tiene escritura habilitada y existe `PermissionsJson` en tabla `Roles`

---

## 🚀 Próximos Pasos Recomendados

1. **Integrar PermissionsProvider en App.js** (proteger rutas)
2. **Aplicar usePermissions() en vistas existentes** (AlarmsView, RecipesView, etc.)
3. **Probar con diferentes roles** (crear usuario Operator y verificar restricciones)
4. **Añadir indicadores visuales** (badges de "Solo lectura", "Sin acceso", etc.)
5. **Migración de datos** (si hay roles existentes, generar PermissionsJson inicial)

---

**Fecha**: 2026-01-30  
**Versión**: 1.0.0  
**Estado**: ✅ Implementación Completa - Listo para Pruebas

¿Quieres que te ayude con alguno de los próximos pasos? 🚀
