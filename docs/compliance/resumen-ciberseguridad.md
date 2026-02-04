# Resumen de Ciberseguridad Implementada

## 1. Visión general
Aquafrisch Supervisor incorpora medidas de ciberseguridad alineadas con el Reglamento (UE) 2024/2847 (Cyber Resilience Act, CRA) y con los requisitos adicionales de CADRA/Alstom. 
A diciembre de 2025 el desarrollo cubre prácticamente la totalidad de las obligaciones de software (≈95%) y mantiene preparado el marco para las obligaciones que dependen de plataformas externas.

## 2. Protección del acceso y las identidades
- **Autenticación robusta**: acceso mediante credenciales cifradas con BCrypt y sesiones protegidas con tokens JWT.
- **Control de roles**: jerarquía de cinco niveles (SuperAdmin, Admin, Operator, Viewer, Auditor) con permisos separados por responsabilidad.
- **Gestión de sesiones**: bloqueo automático tras intentos fallidos repetidos y sesiones únicas por rol para evitar uso simultáneo no autorizado.
- **Recuperación segura**: procedimiento de recuperación de contraseña sin conexión a internet, pensado para entornos restringidos.

## 3. Integridad del software y trazabilidad
- **Firma y auditoría de código**: integración con Git usando firmas GPG/SSH para garantizar la procedencia del código.
- **Registro de auditoría**: más de 60 eventos controlados con cadena de hashes SHA256 para asegurar que las acciones críticas quedan documentadas y no pueden alterarse.
- **Verificación continua**: servicio de integridad que revisa periódicamente el estado del backend, frontend y PLC para detectar modificaciones no autorizadas.

## 4. Gestión de vulnerabilidades y componentes
- **Inventario de software (SBOM)**: generación automática en formato CycloneDX de todos los componentes del backend (.NET) y frontend (npm), con histórico disponible en el panel de control.
- **Escaneo de vulnerabilidades**: servicio que consulta OSV, NVD y GitHub Advisory Database para detectar incidencias en librerías externas.
- **Canal de seguridad**: punto de contacto dedicado (SupportEmail) visible en la interfaz para reportar vulnerabilidades y recibir soporte.
- **Panel de estado**: indicadores en tiempo real que muestran la conexión con servicios externos, la validez de los análisis y la vigencia del soporte.

## 5. Protección de datos y documentación de soporte
- **Retención controlada de logs**: política de conservación que mantiene trazabilidad cumpliendo el CRA.
- **Información al usuario**: documentación y paneles que exponen la versión instalada, período de soporte (10 años) y contactos de seguridad.
- **Backups automáticos por proyecto**: copias de seguridad que preservan configuraciones, modelos y bases de datos de cada instalación.

## 6. Elementos preparados a la espera de requisitos externos
- **Notificación a ENISA/CSIRT (Art. 14)**: infraestructura de configuración y paneles lista, pero pendiente de activarse cuando la plataforma oficial europea entre en servicio (prevista septiembre 2026).
- **Integración de reportes automáticos**: parámetros de Excel para enviar informes de vulnerabilidad configurados; la automatización se activará junto con el canal oficial europeo.

## 7. Trabajo pendiente planificado
| Acción | Objetivo | Estado |
|--------|----------|--------|
| Aviso anticipado de fin de soporte | Notificación proactiva 6 meses antes | Mejora opcional |
| Cifrado adicional de campos en Excel | Endurecer la protección de configuraciones | Mejora opcional |
| Política de divulgación coordinada | Documento SECURITY_POLICY.md con procesos y SLA | En curso (febrero 2026) |
| Evaluación formal de riesgos de ciberseguridad | Documento detallado según Art. 13.2 | Programada (marzo 2026) |
| Documentación técnica CRA (Anexo VII) | Dossier completo para organismos notificados | Programada (junio 2026) |
| Manual de seguridad para usuarios finales | Guía práctica (Anexo II) | Programada (junio 2026) |
| Declaración UE de conformidad | Documento final (Anexo V) | Programada (septiembre 2026) |

## 8. Compromisos de soporte

## 9. Resiliencia y continuidad operativa

### 9.1 Zonas y particionamiento por criticidad
- **Capacidad**: el sistema soporta la **segmentación de datos, aplicaciones y servicios** según su criticidad, facilitando la implementación de un **modelo de zonas** (alineado con IEC 62443).
- **Cómo se aplica**:
	- Aislamiento por proyecto (multi-tenant): cada proyecto tiene **configuración, modelos y base de datos** independientes (Projects/{id}/...).
	- Separación de roles y superficies: `SuperAdmin`, `Admin`, `Operator`, `Viewer`, `Auditor` con **permisos diferenciados**.
	- Segmentación de API y orígenes: **CORS** multi-puerto y rutas dedicadas para funciones críticas (p.ej., `/hubs/*`, `/api/audit/*`).
- **Referencia**: ver [docs/compliance/SISTEMA_LOGS_CRA.md](docs/compliance/SISTEMA_LOGS_CRA.md) y arquitectura multi-proyecto en documentación principal.

### 9.2 Modo degradado ante eventos DoS
- **Capacidad**: la plataforma puede operar en **modo degradado** priorizando funciones esenciales del HMI/SCADA cuando se detecta indisponibilidad o presión anómala (p.ej., DoS sobre servicios externos).
- **Medidas típicas**:
	- Priorizar lectura/visualización de proceso y **pausar tareas no esenciales** (vulnerability scans, exportaciones pesadas).
	- **Reintentos y backoff** en SignalR/HTTP; tolerancia temporal a desconexiones del backend/PLC.
	- Desactivar integraciones externas y mantener **operación local** con datos cacheados.
- **Gobernanza**: configurable por instalación; se documenta en el **plan de operación** del cliente.

### 9.3 Límites de recursos en funciones de seguridad
- **Objetivo**: evitar **agotamiento de recursos** por funciones de seguridad.
- **Controles**:
	- Auditoría (L1) con **rotación diaria** y **retención configurable** (por defecto 30 días), purga automática y envío opcional a SOC.
	- **Flush por lotes** y frecuencia controlada en servicios de auditoría e integridad.
	- Programación de integridad periódica (cada 2 min, configurable) y ventana de ejecución supervisada.
- **Referencia**: ver [docs/compliance/SISTEMA_LOGS_CRA.md](docs/compliance/SISTEMA_LOGS_CRA.md) — secciones L1/L2/L3 y mitigaciones de capacidad.

### 9.4 Recuperación a estado seguro conocido
- **Capacidad**: tras una **disrupción o fallo**, el sistema puede **recuperarse y reconstituirse** a un **estado seguro conocido**.
- **Cómo se garantiza**:
	- **Backups automáticos por proyecto** (config, modelos y base de datos) y procedimientos de **restore** documentados.
	- **Verificación de integridad** posterior al arranque (backend/frontend/PLC) y bloqueo de cambios no autorizados.
	- Despliegues autocontenidos con mapeo de ficheros y **firma de versiones**.
- **Referencia**: ver [docs/architecture/DATA_MANAGEMENT.md](docs/architecture/DATA_MANAGEMENT.md) y guía de despliegue en `Deploy-Manual-Remote.ps1`.

