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
- **Período garantizado**: soporte de 10 años (2025-2035), superior al mínimo CRA de 5 años.
- **Respuesta a incidentes**: acuse de recibo en 24 h y primera evaluación en 72 h una vez se active la política de divulgación.

---
**Conclusión**: Aquafrisch Supervisor ya dispone de los controles esenciales de ciberseguridad exigidos por el CRA. Solo quedan acciones complementarias de documentación y los mecanismos de notificación a ENISA/CSIRT, que se habilitarán en cuanto exista infraestructura oficial. Mientras tanto, el sistema se mantiene preparado y documentado para una activación inmediata.
