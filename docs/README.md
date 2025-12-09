# 📚 Documentación del Sistema SCADA/HMI 3D

**Sistema**: SCADA/HMI Industrial con visualización 3D Babylon.js  
**Versión Docs**: 1.0  
**Última actualización**: Diciembre 2025

---

## 📋 Índice General

Esta carpeta contiene toda la documentación técnica del sistema organizada por categorías.

---

## 🏗️ Architecture - Arquitectura del Sistema
> Documentación de diseño y arquitectura técnica

| Documento | Descripción |
|-----------|-------------|
| [MULTI_PROJECT_SYSTEM.md](architecture/MULTI_PROJECT_SYSTEM.md) | 📌 **Sistema Multi-Proyecto** - Gestión de múltiples instalaciones |
| [ARQUITECTURA_DESPLIEGUE.md](architecture/ARQUITECTURA_DESPLIEGUE.md) | Arquitectura general del sistema y despliegue |
| [ARQUITECTURA_LOGS.md](architecture/ARQUITECTURA_LOGS.md) | Sistema de logging y auditoría |
| [MODELOS_3D_IMPLEMENTATION.md](architecture/MODELOS_3D_IMPLEMENTATION.md) | Implementación de modelos 3D con Babylon.js |

---

## 🇪🇺 Compliance - Cumplimiento Normativo (EU CRA)
> Documentación de cumplimiento con Cyber Resilience Act y CADRA/Alstom

| Documento | Descripción |
|-----------|-------------|
| [ROADMAP_CUMPLIMIENTO_CRA.md](compliance/ROADMAP_CUMPLIMIENTO_CRA.md) | 📌 **Roadmap principal** de cumplimiento EU CRA |
| [GESTION_USUARIOS_EU_CRA.md](compliance/GESTION_USUARIOS_EU_CRA.md) | Sistema de gestión de usuarios según CRA |
| [SISTEMA_LOGS_CRA.md](compliance/SISTEMA_LOGS_CRA.md) | Sistema de logs para cumplimiento CRA |
| [SECURITY.md](compliance/SECURITY.md) | Política de seguridad del proyecto |

### 📁 Terceros
| Documento | Descripción |
|-----------|-------------|
| [INDICE_TERCEROS.md](compliance/terceros/INDICE_TERCEROS.md) | Índice de documentación de terceros |
| [beckhoff/](compliance/terceros/beckhoff/) | Documentación específica Beckhoff/TwinCAT |

---

## 👨‍💻 Development - Guías de Desarrollo
> Guías para desarrolladores del sistema

| Documento | Descripción |
|-----------|-------------|
| [BACKEND_API_EXAMPLE.md](development/BACKEND_API_EXAMPLE.md) | Ejemplos de uso de la API Backend |
| [INTEGRACION_BACKEND.md](development/INTEGRACION_BACKEND.md) | Guía de integración con el Backend |
| [INTEGRACION_FRONTEND_PUMPS.md](development/INTEGRACION_FRONTEND_PUMPS.md) | Integración de bombas en Frontend |
| [IMPLEMENTACION_PUMP_ELEMENTS.md](development/IMPLEMENTACION_PUMP_ELEMENTS.md) | Implementación de elementos de bomba |
| [TROUBLESHOOTING_ANIMACION_PLC.md](development/TROUBLESHOOTING_ANIMACION_PLC.md) | Solución de problemas de animación PLC |

---

## ⚙️ Configuration - Configuración
> Documentación de configuración del sistema (Excel, variables PLC)

| Documento | Descripción |
|-----------|-------------|
| [MAPEO_COLUMNAS_EXCEL.md](configuration/MAPEO_COLUMNAS_EXCEL.md) | Mapeo de columnas del archivo Excel de configuración |
| [ESTRUCTURA_EXCEL_15_COLUMNAS_REFERENCIA.md](configuration/ESTRUCTURA_EXCEL_15_COLUMNAS_REFERENCIA.md) | Referencia de estructura Excel con 15 columnas |
| [SYSTEM_CONFIG_SHEET.md](configuration/SYSTEM_CONFIG_SHEET.md) | Configuración de la hoja SystemConfig |
| [SYSTEM_CONFIG_IMPLEMENTATION.md](configuration/SYSTEM_CONFIG_IMPLEMENTATION.md) | Implementación de SystemConfig en código |

---

## 🚀 Deployment - Despliegue e Instalación
> Guías de despliegue, instalación y actualizaciones

| Documento | Descripción |
|-----------|-------------|
| [INSTALACION_PRODUCCION.md](deployment/INSTALACION_PRODUCCION.md) | 📌 **Guía completa de instalación** (Self-contained + HTTPS) |
| [README_KIOSK.md](deployment/README_KIOSK.md) | Configuración modo Kiosk para IPC |
| [COMO_USAR_NUEVA_VERSION.md](deployment/COMO_USAR_NUEVA_VERSION.md) | Guía para actualizar a nueva versión |

### 🔐 Notas de Seguridad
- **Deployment Self-Contained**: Incluye .NET Runtime, no requiere instalación adicional
- **HTTPS habilitado**: Puerto 5001 con certificado SSL auto-firmado (10 años validez)
- **Puertos**: HTTP 5000 (desarrollo), HTTPS 5001 (producción recomendado)

---

## 📖 User Guides - Manuales de Usuario
> Documentación para usuarios finales (EU CRA Anexo II)

| Documento | Descripción |
|-----------|-------------|
| [MANUAL_USUARIO_RECUPERACION.md](user-guides/MANUAL_USUARIO_RECUPERACION.md) | Manual de recuperación de contraseña |
| [VULNERABILITY_REPORT.md](user-guides/VULNERABILITY_REPORT.md) | Cómo reportar vulnerabilidades |

---

## 🔒 Internal - Documentación Interna
> ⚠️ **Solo uso interno Aquafrisch - NO compartir con clientes**

| Documento | Descripción |
|-----------|-------------|
| [INTERNAL_AQUAFRISCH_CREDENTIALS.md](internal/INTERNAL_AQUAFRISCH_CREDENTIALS.md) | Credenciales internas del sistema |
| [CLIENTE_CREDENCIALES_INICIALES.md](internal/CLIENTE_CREDENCIALES_INICIALES.md) | Credenciales iniciales para clientes |
| [DOCUMENTACION_INTERNA_AQUAFRISCH.md](internal/DOCUMENTACION_INTERNA_AQUAFRISCH.md) | Documentación interna de procesos |

---

## 📝 Changelog - Historial de Cambios
> Registro de cambios y estado de integración

| Documento | Descripción |
|-----------|-------------|
| [ESTADO_INTEGRACION.md](changelog/ESTADO_INTEGRACION.md) | Estado actual de integración Backend/Frontend |
| [RESUMEN_TRABAJO_NOCTURNO.md](changelog/RESUMEN_TRABAJO_NOCTURNO.md) | Resumen de trabajo de desarrollo |

---

## 🔗 Referencias Rápidas

### Archivos README de componentes (permanecen en raíz)
- `../README.md` - README principal del Backend
- `../../SW.PC.REACT.Frontend/my-3d-app/README.md` - README del Frontend

### Configuración de desarrollo
- `../.github/copilot-instructions.md` - Instrucciones para GitHub Copilot

---

## 📊 Estructura de Carpetas

```
docs/
├── README.md                    ← Este archivo
├── architecture/                # 🏗️ Arquitectura
├── compliance/                  # 🇪🇺 Cumplimiento EU CRA
│   └── terceros/               # Docs de terceros
│       └── beckhoff/
├── development/                 # 👨‍💻 Guías desarrollo
├── configuration/               # ⚙️ Configuración
├── deployment/                  # 🚀 Despliegue
├── user-guides/                 # 📖 Manuales usuario
├── internal/                    # 🔒 Interno (NO compartir)
└── changelog/                   # 📝 Historial
```

---

## 📌 Convenciones de Nomenclatura

- **Archivos**: `NOMBRE_DESCRIPTIVO.md` (mayúsculas con guiones bajos)
- **Carpetas**: `nombre-carpeta/` (minúsculas con guiones)
- **Idioma**: Español (contenido) / Inglés (nombres carpetas estándar)
- **Formato**: Markdown (.md) para documentación técnica

---

*Documentación mantenida por el equipo de desarrollo Aquafrisch*
