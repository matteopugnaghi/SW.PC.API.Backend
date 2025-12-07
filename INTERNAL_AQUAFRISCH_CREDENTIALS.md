# 🔐 DOCUMENTO INTERNO - AQUAFRISCH
## Credenciales de Fabricante - CONFIDENCIAL

> ⚠️ **DOCUMENTO INTERNO - NO ENTREGAR AL CLIENTE**
> 
> Este documento contiene credenciales de acceso de nivel fabricante.
> Solo personal autorizado de Aquafrisch debe tener acceso a esta información.

---

## 🏭 Información del Sistema

| Campo | Valor |
|-------|-------|
| **Producto** | Aquafrisch Supervisor |
| **Versión** | 1.0.0 |
| **Fecha** | Diciembre 2024 |
| **Cumplimiento** | EU CRA, IEC 62443 |

---

## 🔑 CREDENCIALES SUPERADMIN (FABRICANTE)

> **⚠️ NUNCA COMPARTIR CON EL CLIENTE**

| Campo | Valor |
|-------|-------|
| **Usuario** | `superadmin` |
| **Contraseña** | `Aquafrisch@SuperAdmin2024!` |
| **Rol** | SuperAdmin (Nivel 0) |
| **Email** | superadmin@aquafrisch.com |

### Permisos SuperAdmin

```
✅ Acceso TOTAL al sistema
✅ Gestión de TODOS los usuarios (incluidos SuperAdmin)
✅ Acceso a PLC y TwinCAT
✅ Actualización de firmware
✅ Acceso al código fuente
✅ Configuración del sistema
✅ Auditoría completa
✅ Backup y restauración
✅ Licencias
```

---

## 👤 CREDENCIALES ADMINISTRATOR (CLIENTE)

> Estas credenciales SÍ se entregan al cliente en documento separado

| Campo | Valor |
|-------|-------|
| **Usuario** | `admin` |
| **Contraseña** | `Admin@Aquafrisch2024!` |
| **Rol** | Administrator (Nivel 1) |
| **Email** | admin@[dominio-cliente].com |

---

## 🔐 RECUPERACIÓN DE CONTRASEÑAS OFFLINE

### Sistema de Códigos Determinísticos

El sistema permite recuperar contraseñas **SIN INTERNET** usando códigos
generados matemáticamente. El mismo algoritmo existe en:
- Backend (`RecoveryCodeService.cs`)
- Herramienta interna (`Tools/GenerateRecoveryCode.ps1`)

### Secreto Compartido

```
AQUAFRISCH_SECRET = "AQF-2024-S3CR3T-K3Y-N0T-SH4R3"
```

> ⚠️ **CRÍTICO**: Este secreto NUNCA debe compartirse. Si se compromete,
> cambiar en AMBOS lugares y recompilar el backend.

### Cómo Generar un Código

1. Abrir PowerShell en la carpeta `Tools/`
2. Ejecutar:
```powershell
.\GenerateRecoveryCode.ps1 -InstallationId "AQFR-2024-001" -Username "admin"
```

3. Dictar el código al usuario por teléfono
4. El código es válido por 24-48 horas

### Flujo de Recuperación

```
┌─────────────────────────────────────────────────────────────────┐
│  FLUJO DE RECUPERACIÓN SIN INTERNET                            │
├─────────────────────────────────────────────────────────────────┤
│                                                                 │
│  1. Usuario → Llama a Aquafrisch                               │
│     "Soy [username] de instalación [AQFR-XXXX-XXX]"            │
│                                                                 │
│  2. Aquafrisch → Genera código con herramienta                 │
│     .\GenerateRecoveryCode.ps1 -InstallationId X -Username Y   │
│                                                                 │
│  3. Aquafrisch → Dicta código por teléfono                     │
│     "Tu código es: AQFR-XXXX-XXXX-XXXX"                        │
│                                                                 │
│  4. Usuario → Introduce código en pantalla recovery            │
│                                                                 │
│  5. Sistema → Valida y permite nueva contraseña                │
│                                                                 │
└─────────────────────────────────────────────────────────────────┘
```

### Jerarquía de Reset

| Quién necesita reset | Quién puede hacerlo |
|---------------------|---------------------|
| Usuario (Operator, etc.) | Administrator o Aquafrisch |
| Administrator | Solo Aquafrisch (código recovery) |
| SuperAdmin | Solo Aquafrisch interno |

---

## 🛡️ Política de Seguridad Interna

### Acceso SuperAdmin

1. **Solo técnicos autorizados** de Aquafrisch pueden usar SuperAdmin
2. **Registrar cada acceso** en el sistema de tickets interno
3. **Cambiar contraseña** si se sospecha compromiso
4. **Nunca usar** para operaciones que puede hacer Administrator

### Cambio de Contraseñas

Si es necesario cambiar la contraseña del SuperAdmin:

1. Acceder con credenciales actuales
2. Ir a configuración de usuario
3. Cambiar contraseña
4. Actualizar este documento
5. Notificar al equipo de soporte

### En Caso de Compromiso

1. **Cambiar inmediatamente** contraseña SuperAdmin
2. **Revisar logs** de auditoría
3. **Verificar** no hay usuarios no autorizados
4. **Notificar** al responsable de seguridad
5. **Documentar** el incidente

---

## 📋 Registro de Accesos SuperAdmin

| Fecha | Técnico | Motivo | Ticket |
|-------|---------|--------|--------|
| | | | |
| | | | |
| | | | |

---

## 📞 Contactos Internos

| Rol | Nombre | Contacto |
|-----|--------|----------|
| Responsable Seguridad | | |
| Soporte Técnico | | |
| Desarrollo | | |

---

## 📝 Historial de Cambios

| Versión | Fecha | Descripción | Autor |
|---------|-------|-------------|-------|
| 1.0 | Dic 2024 | Creación inicial | Sistema |

---

> **🔒 CLASIFICACIÓN: CONFIDENCIAL - SOLO USO INTERNO AQUAFRISCH**
