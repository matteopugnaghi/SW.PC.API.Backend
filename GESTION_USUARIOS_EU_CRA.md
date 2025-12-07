# 🔐 Gestión de Usuarios - EU CRA Compliance

## Aquafrisch Supervisor - Sistema SCADA/HMI

> **Documento técnico de referencia para la gestión de usuarios conforme al Reglamento Europeo de Ciberresiliencia (EU CRA)**

---

## 📋 Índice

1. [Introducción](#introducción)
2. [Arquitectura de Seguridad](#arquitectura-de-seguridad)
3. [Jerarquía de Roles](#jerarquía-de-roles)
4. [Credenciales por Defecto](#credenciales-por-defecto)
5. [Gestión de Usuarios](#gestión-de-usuarios)
6. [Política de Contraseñas](#política-de-contraseñas)
7. [Sesiones y Bloqueos](#sesiones-y-bloqueos)
8. [Auditoría](#auditoría)
9. [Cumplimiento EU CRA](#cumplimiento-eu-cra)
10. [Preguntas Frecuentes](#preguntas-frecuentes)

---

## 📖 Introducción

El sistema **Aquafrisch Supervisor** implementa un modelo de seguridad de múltiples niveles diseñado para cumplir con:

- **EU CRA** (Cyber Resilience Act) - Reglamento Europeo de Ciberresiliencia
- **IEC 62443** - Seguridad para Sistemas de Automatización Industrial
- **CADRA/Alstom** - Requisitos de ciberseguridad industrial

### Principio Fundamental

```
El FABRICANTE (Aquafrisch) mantiene acceso de nivel superior para:
  ✅ Actualizaciones de seguridad
  ✅ Mantenimiento del sistema
  ✅ Soporte técnico

El CLIENTE gestiona los usuarios de SU instalación:
  ✅ Crear/modificar operadores
  ✅ Asignar permisos según necesidades
  ✅ Mantener control de acceso local
```

---

## 🏗️ Arquitectura de Seguridad

### Modelo de 3 Niveles

```
┌─────────────────────────────────────────────────────────────────┐
│  NIVEL 0: SUPERADMIN (Fabricante - Aquafrisch)                 │
│  ═══════════════════════════════════════════════════════════   │
│  👤 Usuario: superadmin                                         │
│  🔒 Acceso: TOTAL (PLC, TwinCAT, firmware, código, todos)       │
│  ⚠️ NO SE ENTREGA AL CLIENTE                                   │
│  📌 Uso: Mantenimiento, actualizaciones, soporte técnico        │
└─────────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌─────────────────────────────────────────────────────────────────┐
│  NIVEL 1: ADMINISTRATOR (Cliente - Responsable Seguridad)      │
│  ═══════════════════════════════════════════════════════════   │
│  👤 Usuario: admin (entregado al cliente)                       │
│  🔒 Acceso: Gestión usuarios, config operativa, recetas         │
│  ❌ SIN ACCESO: PLC, TwinCAT, firmware, código                  │
│  📌 Uso: Administración diaria de la instalación                │
└─────────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌─────────────────────────────────────────────────────────────────┐
│  NIVELES 2-5: USUARIOS OPERATIVOS                              │
│  ═══════════════════════════════════════════════════════════   │
│  👤 Operator, Maintenance, Viewer, Auditor                      │
│  🔒 Acceso: Según rol asignado                                  │
│  📌 Uso: Operación diaria del sistema                           │
└─────────────────────────────────────────────────────────────────┘
```

---

## 👥 Jerarquía de Roles

### Nivel 0: SuperAdmin (Solo Fabricante)

| Característica | Valor |
|----------------|-------|
| **Usuario** | `superadmin` |
| **Propósito** | Acceso total del fabricante |
| **Visibilidad** | OCULTO para Administrators |

**Permisos:**
- ✅ Acceso TOTAL al sistema
- ✅ Modificación de PLC/TwinCAT
- ✅ Actualización de firmware
- ✅ Gestión de TODOS los usuarios (incluyendo otros SuperAdmin)
- ✅ Acceso al código fuente
- ✅ Configuración de sistema y licencias
- ✅ Purga de logs de auditoría
- ✅ Mantenimiento del sistema

**Restricciones:**
- ⚠️ Credenciales NO se entregan al cliente
- ⚠️ Uso exclusivo del personal de Aquafrisch

---

### Nivel 1: Administrator (Cliente)

| Característica | Valor |
|----------------|-------|
| **Usuario** | `admin` (se entrega al cliente) |
| **Propósito** | Responsable de seguridad del cliente |
| **Visibilidad** | NO puede ver usuarios SuperAdmin |

**Permisos:**
- ✅ Gestión de usuarios de SU instalación
- ✅ Crear, modificar, eliminar usuarios (excepto SuperAdmin)
- ✅ Asignar roles (excepto SuperAdmin)
- ✅ Ver logs de auditoría
- ✅ Configuración operativa
- ✅ Gestión de recetas y alarmas
- ✅ Crear backups

**Restricciones:**
- ❌ NO puede ver usuarios SuperAdmin
- ❌ NO puede modificar PLC/TwinCAT
- ❌ NO puede actualizar firmware
- ❌ NO puede acceder al código fuente
- ❌ NO puede restaurar backups del sistema

---

### Nivel 2: Operator

| Característica | Valor |
|----------------|-------|
| **Propósito** | Operador de proceso |
| **Ámbito** | Control de operaciones diarias |

**Permisos:**
- ✅ Control de operaciones de proceso
- ✅ Lectura/escritura de variables PLC
- ✅ Reconocimiento de alarmas
- ✅ Ejecución de recetas
- ✅ Visualización de datos y reportes

**Restricciones:**
- ❌ Sin acceso a configuración
- ❌ Sin gestión de usuarios
- ❌ Sin modificación de recetas

---

### Nivel 3: Maintenance

| Característica | Valor |
|----------------|-------|
| **Propósito** | Personal de mantenimiento |
| **Ámbito** | Configuración técnica |

**Permisos:**
- ✅ Configuración técnica del sistema
- ✅ Diagnósticos y calibración
- ✅ Gestión completa de recetas
- ✅ Configuración de alarmas
- ✅ Lectura/escritura/config de PLC

**Restricciones:**
- ❌ Sin acceso a seguridad
- ❌ Sin gestión de usuarios

---

### Nivel 4: Viewer

| Característica | Valor |
|----------------|-------|
| **Propósito** | Usuario de solo lectura |
| **Ámbito** | Visualización únicamente |

**Permisos:**
- ✅ Visualización de datos de proceso
- ✅ Lectura de alarmas
- ✅ Consulta de reportes
- ✅ Lectura de recetas

**Restricciones:**
- ❌ Sin capacidad de modificación
- ❌ Solo lectura en todo el sistema

---

### Nivel 5: Auditor

| Característica | Valor |
|----------------|-------|
| **Propósito** | Auditor de seguridad |
| **Ámbito** | Compliance y seguridad |

**Permisos:**
- ✅ Acceso completo a logs de auditoría
- ✅ Exportación de reportes de seguridad
- ✅ Revisión de compliance
- ✅ Lectura de información de usuarios
- ✅ Lectura de configuración de seguridad

**Restricciones:**
- ❌ Sin capacidad de modificación
- ❌ Solo lectura de seguridad

---

## 🔑 Credenciales por Defecto

### Para el FABRICANTE (Aquafrisch) - NO COMPARTIR

```
Usuario:     superadmin
Contraseña:  Aquafrisch@SuperAdmin2024!
```

### Para el CLIENTE - Entregar al Responsable de Seguridad

```
Usuario:     admin
Contraseña:  Admin@Aquafrisch2024!
```

> ⚠️ **IMPORTANTE**: El usuario `admin` debe cambiar su contraseña en el primer inicio de sesión.

---

## 👤 Gestión de Usuarios

### Crear un Nuevo Usuario (Solo Admin/SuperAdmin)

1. Acceder al menú **"Gestión de Usuarios"**
2. Click en **"➕ Nuevo Usuario"**
3. Completar los campos:
   - **Usuario**: Nombre único de login
   - **Contraseña**: Cumplir política de seguridad
   - **Nombre Completo**: Nombre real del usuario
   - **Email**: Correo electrónico (opcional)
   - **Roles**: Seleccionar uno o más roles
4. Click en **"✅ Crear Usuario"**

### Modificar un Usuario

1. En la tabla de usuarios, click en **"✏️ Editar"**
2. Modificar los campos necesarios
3. Click en **"✅ Guardar Cambios"**

### Resetear Contraseña

1. Click en **"🔑 Reset"** en el usuario deseado
2. Introducir nueva contraseña temporal
3. El usuario deberá cambiarla en su próximo login

### Desbloquear Usuario

Si un usuario se bloquea por intentos fallidos:
1. Click en **"🔓 Desbloquear"**
2. El usuario podrá intentar login nuevamente

### Eliminar Usuario

1. Click en **"🗑️ Eliminar"**
2. Confirmar la acción
3. ⚠️ No se puede eliminar el último Administrator

---

## 🔒 Política de Contraseñas

### Requisitos Mínimos

| Requisito | Valor |
|-----------|-------|
| Longitud mínima | 12 caracteres |
| Mayúsculas | Al menos 1 |
| Minúsculas | Al menos 1 |
| Números | Al menos 1 |
| Caracteres especiales | Al menos 1 |

### Patrones Prohibidos

Las siguientes secuencias están bloqueadas:
- `123456`
- `password`
- `qwerty`
- `abc123`
- `admin`

### Ejemplos de Contraseñas Válidas

```
✅ Aquafrisch@2024!
✅ Supervisor#Planta1
✅ M1ContraseñaSegura!
```

---

## ⏱️ Sesiones y Bloqueos

### Bloqueo de Cuenta

| Parámetro | Valor |
|-----------|-------|
| Intentos fallidos permitidos | 6 |
| Tiempo de bloqueo | 15 minutos |
| Desbloqueo automático | Sí (tras 15 min) |
| Desbloqueo manual | Por Admin/SuperAdmin |

### Control de Sesiones

| Parámetro | Valor |
|-----------|-------|
| Sesiones concurrentes máximas | 2 por usuario |
| Timeout por inactividad | 15 minutos |
| Timeout de sesión | 30 minutos |
| Roles con sesión única | Operator |

### Comportamiento de Sesión Única (Operator)

Solo puede haber **UN operador activo** a la vez en el sistema:
- Si un nuevo Operator intenta login mientras otro está activo → Se rechaza
- Esto garantiza trazabilidad de acciones del operador

---

## 📋 Auditoría

### Eventos Registrados

Todas las siguientes acciones se registran con firma SHA-256:

- ✅ Login exitoso/fallido
- ✅ Logout
- ✅ Cambio de contraseña
- ✅ Reset de contraseña
- ✅ Bloqueo/desbloqueo de cuenta
- ✅ Creación de usuario
- ✅ Modificación de usuario
- ✅ Eliminación de usuario
- ✅ Asignación/revocación de roles
- ✅ Denegación de permisos

### Información Registrada

```json
{
  "timestamp": "2024-12-06T10:30:00Z",
  "category": "Authentication",
  "action": "Login",
  "result": "Success",
  "userId": "5",
  "username": "operador1",
  "ipAddress": "192.168.1.100",
  "details": "Login exitoso desde estación HMI-01",
  "signature": "a1b2c3d4..."
}
```

---

## 📜 Cumplimiento EU CRA

### Artículos Implementados

| Artículo | Requisito | Implementación |
|----------|-----------|----------------|
| Art. 10 | Separación de privilegios | Jerarquía de 6 niveles |
| Art. 10 | Control de acceso | RBAC implementado |
| Anexo I | Trazabilidad | Logs firmados SHA-256 |
| Anexo I | Política de contraseñas | 12+ caracteres, complejidad |
| Anexo I | Bloqueo de cuenta | 6 intentos, 15 min |

### Principios de Diseño

1. **Mínimo Privilegio**: Cada usuario tiene solo los permisos necesarios
2. **Separación de Funciones**: Fabricante vs Cliente claramente diferenciados
3. **Defensa en Profundidad**: Múltiples capas de seguridad
4. **Trazabilidad**: Todas las acciones son auditadas
5. **Resiliencia**: Recuperación ante ataques de fuerza bruta

---

## ❓ Preguntas Frecuentes

### ¿Por qué no puedo ver algunos usuarios?

Si eres **Administrator**, no puedes ver usuarios con rol **SuperAdmin**. Esto es intencional para proteger las credenciales del fabricante.

### ¿Por qué no puedo asignar el rol SuperAdmin?

Solo usuarios con rol **SuperAdmin** pueden crear o asignar ese rol. Esto es una medida de seguridad del sistema.

### ¿Qué hago si olvido la contraseña de admin?

Contacta con el soporte técnico de Aquafrisch. Un técnico con acceso SuperAdmin puede resetear tu contraseña.

### ¿Por qué mi cuenta está bloqueada?

Las cuentas se bloquean automáticamente tras 6 intentos fallidos de login. Espera 15 minutos o contacta a tu administrador para desbloqueo inmediato.

### ¿Puedo tener múltiples sesiones abiertas?

Sí, hasta 2 sesiones concurrentes por usuario. Excepto los Operadores, que solo pueden tener 1 sesión activa.

### ¿Cómo accede Aquafrisch para soporte técnico?

El personal de Aquafrisch tiene acceso mediante el usuario **superadmin**, que es invisible para los administradores del cliente. Este acceso se usa exclusivamente para mantenimiento y actualizaciones.

---

## 📞 Soporte Técnico

Para asistencia con la gestión de usuarios:

- **Email**: soporte@aquafrisch.com
- **Teléfono**: +34 XXX XXX XXX
- **Portal**: support.aquafrisch.com

---

*Documento generado para Aquafrisch Supervisor v2.0*  
*Conforme a EU CRA (Cyber Resilience Act)*  
*Última actualización: Diciembre 2024*
