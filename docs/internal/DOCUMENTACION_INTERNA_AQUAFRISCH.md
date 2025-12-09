# 🔐 Documentación Interna Aquafrisch - Sistema de Soporte y Recuperación

> ⚠️ **DOCUMENTO CONFIDENCIAL - SOLO USO INTERNO AQUAFRISCH**
> No compartir con clientes. Contiene información de seguridad sensible.

---

## 📋 Índice

1. [Visión General](#visión-general)
2. [Sistema de Recuperación de Contraseñas](#sistema-de-recuperación-de-contraseñas)
3. [Sistema de Soporte Remoto (Challenge-Response)](#sistema-de-soporte-remoto-challenge-response)
4. [Herramientas PowerShell](#herramientas-powershell)
5. [Configuración por Instalación](#configuración-por-instalación)
6. [Troubleshooting](#troubleshooting)

---

## 🎯 Visión General

El sistema Aquafrisch Supervisor incluye dos mecanismos de soporte remoto que funcionan **SIN INTERNET**:

| Sistema | Propósito | Herramienta | Validez |
|---------|-----------|-------------|---------|
| **Recovery** | Cambiar contraseña olvidada | `GenerateRecoveryCode.ps1` | 24-48 horas |
| **Support** | Desbloquear herramientas del sistema | `GenerateSupportCode.ps1` | 2 horas |

Ambos sistemas usan **algoritmos determinísticos** - el código se calcula matemáticamente tanto en el backend como en las herramientas internas, sin necesidad de comunicación.

---

## 🔑 Sistema de Recuperación de Contraseñas

### Cuándo Usar
- Usuario olvidó su contraseña
- Usuario bloqueado por demasiados intentos fallidos
- Necesidad de resetear acceso sin tener que ir físicamente

### Flujo de Trabajo

```
┌─────────────────────────────────────────────────────────────────┐
│  USUARIO                           TÉCNICO AQUAFRISCH           │
├─────────────────────────────────────────────────────────────────┤
│  1. Click "¿Olvidaste tu           │                            │
│     contraseña?" en login          │                            │
│                                    │                            │
│  2. Llama a Aquafrisch             │                            │
│     📞 Proporciona:                │                            │
│     - ID Instalación               │  3. Ejecuta                │
│     - Nombre de usuario            │     GenerateRecoveryCode   │
│                                    │                            │
│                                    │  4. Dicta código AQFR-...  │
│  5. Introduce código en modal      │                            │
│     + nueva contraseña             │                            │
│                                    │                            │
│  6. ✅ Acceso recuperado           │                            │
└─────────────────────────────────────────────────────────────────┘
```

### Datos Necesarios del Usuario

1. **ID de Instalación**: Visible en el modal de soporte (ej: `AQF-ALSTOM-001`)
2. **Nombre de Usuario**: El que aparece en la lista de login (ej: `operador1`, `matteo`)

> ⚠️ **NO se necesita Challenge** para recuperación de contraseña

### Ejecutar GenerateRecoveryCode.ps1

```powershell
# Opción 1: Doble-click en el archivo
# Opción 2: Click derecho → "Ejecutar con PowerShell"
# Opción 3: Desde terminal
cd C:\Aquafrisch\Tools
.\GenerateRecoveryCode.ps1
```

El script es **interactivo** - preguntará:
1. ID de Instalación
2. Nombre de usuario

### Ejemplo de Ejecución

```
================================================================
   GENERADOR DE CODIGOS DE RECUPERACION - AQUAFRISCH
   HERRAMIENTA INTERNA - NO COMPARTIR CON CLIENTES
================================================================

  PASO 1: ID de Instalacion del cliente
  (Ejemplo: AQFR-2024-001, DEMO-001, etc.)

  Installation ID: AQF-ALSTOM-001

  PASO 2: Nombre de usuario que olvido su contrasena

  Username: operador1

================================================================

   CODIGO DE RECUPERACION:

   AQFR-3YSA-JVQY-QYXB

================================================================

  Valido hasta: 2025-12-08 23:59
  Solo para usuario: operador1

  DICTE AL USUARIO:

  Tu codigo de recuperacion es:
  AQFR-3YSA-JVQY-QYXB

  Con este codigo puedes cambiar tu contrasena
  desde la pantalla de login.
```

### Validez del Código

- **Válido**: Día de generación + día siguiente (hasta 23:59)
- **Específico**: Solo funciona para el usuario indicado
- **Único**: Diferente cada día

---

## 🔧 Sistema de Soporte Remoto (Challenge-Response)

### Cuándo Usar
- Usuario necesita acceso a herramientas del sistema (TeamViewer, reinicio, etc.)
- Diagnóstico remoto
- Mantenimiento que requiere privilegios elevados

### Flujo de Trabajo

```
┌─────────────────────────────────────────────────────────────────┐
│  USUARIO                           TÉCNICO AQUAFRISCH           │
├─────────────────────────────────────────────────────────────────┤
│  1. Click "Llamar a Aquafrisch"    │                            │
│     en la aplicación               │                            │
│                                    │                            │
│  2. Llama a Aquafrisch             │                            │
│     📞 Proporciona:                │                            │
│     - ID Instalación               │  3. Ejecuta                │
│     - Código Challenge             │     GenerateSupportCode    │
│                                    │                            │
│                                    │  4. Verifica Challenge     │
│                                    │  5. Dicta código Response  │
│                                    │                            │
│  6. Introduce Response en modal    │                            │
│                                    │                            │
│  7. ✅ Herramientas desbloqueadas  │                            │
│     por 30 minutos                 │                            │
└─────────────────────────────────────────────────────────────────┘
```

### Datos Necesarios del Usuario

1. **ID de Instalación**: Visible en el modal (ej: `AQF-ALSTOM-001`)
2. **Código Challenge**: Generado por el sistema (ej: `AQFS-A7B3-C9D2`)

### Ejecutar GenerateSupportCode.ps1

```powershell
cd C:\Aquafrisch\Tools
.\GenerateSupportCode.ps1
```

### Verificación de Challenge

El script muestra el Challenge esperado. **VERIFICAR** que coincide con lo que dice el usuario antes de dar el Response.

```
  Challenge esperado: AQFS-A7B3-C9D2
  Challenge del usuario: ____________

  Si NO COINCIDE, el usuario puede estar intentando acceso no autorizado.
```

### Validez del Código Response

- **Válido**: 2 horas desde generación
- **Sesión**: 30 minutos de herramientas desbloqueadas
- **Auditoría**: Todas las acciones quedan registradas

---

## 🛠️ Herramientas PowerShell

### Ubicación
```
C:\Aquafrisch\Tools\
├── GenerateRecoveryCode.ps1    # Recuperación de contraseñas
├── GenerateSupportCode.ps1     # Soporte remoto (challenge-response)
└── recovery_codes_log.txt      # Log de códigos generados (auto)
```

### Secretos (NO COMPARTIR)

| Sistema | Secreto | Archivo |
|---------|---------|---------|
| Recovery | `AQF-2024-S3CR3T-K3Y-N0T-SH4R3` | RecoveryCodeService.cs |
| Support | `AQF-2024-SUPP0RT-T00LS-K3Y` | SupportController.cs |

> ⚠️ Si se compromete un secreto, debe cambiarse en TODOS los lugares:
> - Backend (Services/Controllers)
> - Herramientas PowerShell
> - Reinstalar en todas las instalaciones

---

## ⚙️ Configuración por Instalación

### Installation ID

El **Installation ID** se configura en el Excel de cada instalación:

**Archivo**: `ExcelConfigs/ProjectConfig.xlsm`
**Hoja**: `System Config`
**Campo**: `InstallationId`

Formato recomendado: `AQF-{CLIENTE}-{NUMERO}`

Ejemplos:
- `AQF-ALSTOM-001`
- `AQF-TISSEO-042`
- `AQF-METRO-BCN-003`

### Configuración de Contraseñas

En el mismo Excel, hoja `System Config`:

| Campo | Descripción | Valor por defecto |
|-------|-------------|-------------------|
| `AuthPasswordMinLength` | Longitud mínima contraseña | 12 |
| `AuthRequireUppercase` | Requiere mayúsculas | true |
| `AuthRequireLowercase` | Requiere minúsculas | true |
| `AuthRequireDigit` | Requiere números | true |
| `AuthRequireSpecialChar` | Requiere caracteres especiales | false |

---

## 🔍 Troubleshooting

### "Código inválido o expirado"

**Causas posibles**:

1. **Installation ID incorrecto**
   - Verificar que el ID en el modal coincide con el Excel
   - El ID es case-insensitive pero debe ser exacto

2. **Username incorrecto** (solo Recovery)
   - El username debe ser exactamente como aparece en el sistema
   - Es case-insensitive (matteo = MATTEO = Matteo)

3. **Código expirado**
   - Recovery: Válido hasta medianoche del día siguiente
   - Support: Válido 2 horas

4. **Fecha del sistema incorrecta**
   - El PC del cliente tiene fecha/hora incorrecta
   - Verificar con el usuario

5. **Secreto desincronizado**
   - El backend tiene un secreto diferente al de las herramientas
   - Verificar versión del backend instalado

### El modal muestra "NO DISPONIBLE" como Installation ID

- El backend no puede leer el Excel
- Verificar que `ProjectConfig.xlsm` existe en `ExcelConfigs/`
- Verificar que la hoja `System Config` tiene el campo `InstallationId`

### El código PowerShell muestra caracteres raros

- Problema de codificación UTF-8
- Usar la versión sin emojis de los scripts
- Ejecutar desde PowerShell 7 si es posible

---

## 📞 Contacto Interno

Para problemas con las herramientas de soporte:
- **Email interno**: soporte-dev@aquafrisch.com
- **Slack**: #aquafrisch-supervisor-dev

---

*Última actualización: Diciembre 2025*
*Versión del documento: 1.0*
