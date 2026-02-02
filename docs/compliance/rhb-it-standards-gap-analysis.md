# Aquafrisch Supervisor vs. RhB IT Standards v9.0.4

## 1. Alcance y enfoque
Este documento revisa los cambios introducidos en RhB IT Standards v9.0.4 (comparados con la version v8.1 ya traducida) y evalua si Aquafrisch Supervisor los cumple a nivel de software, procesos o documentacion. Se detallan brechas y acciones propuestas.

## 2. Resumen ejecutivo
- Requisitos aplicables: 2.1 implica documentacion interna; 2.4 (MS Entra ID) es el principal ajuste tecnico futuro; 4.2, 6 y 6.2 no aplican al alcance actual (sin app movil ni integracion IoT propia); 7.x solo aplicaria si RhB exige modalidad cloud; 9.2.4 requiere aclarar integracion near real-time.
- Brechas criticas: falta politica escrita de uso de IA en desarrollo; no existe integracion actual con MS Entra ID ni con la plataforma IoT (Cumulocity); ausencia de cifrado en reposo y evidencias ISO 27001/auditorias para escenarios cloud; falta un mecanismo CDC/documentado para integraciones near real-time.
- Acciones inmediatas sugeridas: emitir politica interna de IA y registrar uso de herramientas; preparar plan de federacion con MS Entra ID (registro, scopes, MFA); responder formalmente a RhB que la entrega actual es on-premise con controles de antivirus, firewall y backup; definir hoja de ruta para cifrado en reposo y exportaciones si se pide integracion cloud.

## 3. Detalle por requisito

### 2.1 General requirements for client and server software (.18)
- Aplicabilidad: Alta (afecta al proceso de desarrollo actual, no al producto desplegado).
- Estado actual: No existe politica formal que garantice que los prompts enviados a herramientas de IA generativas no se almacenan ni reutilizan para entrenamiento.
- Gap: Politica y evidencia operativa (documentacion interna únicamente).
- Accion recomendada: Redactar y aprobar una politica interna que limite el uso de herramientas IA a servicios que garanticen privacidad (por ejemplo GitHub Copilot con retention desactivado), documentar configuraciones y formar al equipo.

### 2.4 Authorizations / Single Sign On (.0X)
- Aplicabilidad: Media (entrara en vigor si RhB requiere federacion con MS Entra ID).
- Estado actual: No se solicitan permisos al Graph API; autenticacion basada en JWT propio.
- Gap: No existe hoy integracion con el directorio central de RhB; habra que seguir principios de privilegio minimo cuando se configure.
- Accion recomendada: Preparar plan de federacion MS Entra ID (Enterprise App + registro de API con scopes granulares, MFA obligatorio, revisiones periodicas de permisos y segregacion de roles de servicio). Documentar este plan para poder responder inmediatamente a RhB.

### 4.2 Mobile applications (.1 - .5)
- Aplicabilidad: No aplica (Aquafrisch Supervisor no dispone de app movil; la interfaz es web y se despliega en estaciones industriales Windows).
- Estado actual: N/A.
- Gap: Nulo en el alcance actual; solo deberia considerarse si RhB solicita app movil dedicada.
- Accion recomendada: Registrar en backlog si se planifica canal movil y, llegado el caso, respetar Microsoft Intune, Apple Business Manager y la restriccion de VPN.

### 6 Internet of Things (IoT)
#### 6.1 Variant 1 / Variant 2
- Aplicabilidad: No aplica en la entrega base (no se integra actualmente con la plataforma Cumulocity de RhB).
- Estado actual: Integracion directa con TwinCAT PLC via ADS; no existe envio de datos a Cumulocity ni recepcion de ordenes desde esa plataforma.
- Gap: Solo aparecera si RhB exige integracion con Cumulocity; habria que soportar push/pull y SSO.
- Accion recomendada: Mantener esta condicion marcada como "no aplica" en la respuesta oficial y, si RhB solicita integracion, planificar un conector que traduzca variables PLC a la API de Cumulocity respetando near real-time.

#### 6.2 Additional integration requirements (.1 - .2)
- Aplicabilidad: Solo si RhB requiere integracion IoT.
- Estado actual: Integridad entre PLC y backend garantizada mediante validaciones de negocio, pero sin cifrado nativo (TwinCAT ADS no cifra). HTTPS habilitado para API/web. No hay checksum dedicado para trafico PLC.
- Gap: Nulo en el escenario actual; en caso de integracion IoT habria que garantizar cifrado e integridad end to end.
- Accion recomendada: Documentar que actualmente no aplica. Si se aborda, se recomienda encapsular ADS en VPN/IPSec o tunel TLS y firmar mensajes antes de reenviarlos a la plataforma RhB.

### 7 Cloud services
#### 7.1 General
- Aplicabilidad: Baja. Aquafrisch Supervisor se entrega e instala on-premise en infraestructura de RhB.
- Estado actual: Monitorizacion y renovacion de certificados gestionados manualmente; antivirus, firewall y backup quedan bajo la infraestructura de RhB.
- Gap: En el escenario on-prem no se requieren procesos adicionales; si se ofreciera modalidad SaaS, habria que documentar monitorizacion, certificados y responsabilidades.
- Accion recomendada: Responder a RhB indicando el modo on-premise y adjuntar los procedimientos actuales; dejar plan cloud preparado por si se solicitara en el futuro.

#### 7.2 Integration
- Aplicabilidad: Media. Aunque el despliegue es on-prem, RhB podria exigir SSO via MS Entra ID o integraciones con sus sistemas.
- Estado actual: Autenticacion propia (no MS Entra), APIs HTTPS con JWT, transferencias cifradas disponibles. Exportaciones de datos y monitorizacion se gestionan manualmente.
- Gap: Falta integracion MS Entra ID (con MFA), endpoints estandar de exportacion y pagina de estado para Zabbix.
- Accion recomendada: Priorizar el plan de MS Entra ID (registro de aplicacion, scopes, MFA obligatorio); definir mecanismo de exportacion (CSV/XML) y endpoint de estado HTTP para Zabbix en caso de ser requerido.

#### 7.3 Portability
- Aplicabilidad: Baja en despliegues on-prem tradicionales, pero relevante si RhB exige compatibilidad con OpenShift.
- Estado actual: Despliegue actual es servicio Windows self-contained; no hay imagen container certificada.
- Gap: Solo aplica si se solicita portabilidad a OpenShift o nube.
- Accion recomendada: Mantener registrado como potencial mejora y evaluar creacion de imagen container si RhB lo pide.

#### 7.4 Data / data protection (.1 - .6)
- Aplicabilidad: Media. Aunque el sistema se instala on-prem, RhB puede solicitar garantia de cifrado y procesos GDPR.
- Estado actual: Almacenamiento principal en SQLite sin cifrado nativo; backups por proyecto en disco; datos personales limitados (usuarios, logs). Borrado completo gestionado manualmente.
- Gap: Falta cifrado en reposo y procedimiento formal de borrado/propiedad de datos.
- Accion recomendada: Documentar que RhB controla el servidor (antivirus, firewall, backups) y planificar cifrado en reposo (SQLite Encryption o migracion a SQL Server) junto con procedimientos GDPR si se requieren.

#### 7.5 Security (.1 - .4)
- Aplicabilidad: Media. RhB puede solicitar evidencias incluso para despliegues on-prem.
- Estado actual: No hay certificacion ISO 27001 declarada ni evidencia de auditorias externas recurrentes; planes de continuidad documentados parcialmente en ROADMAP CRA.
- Gap: Necesidad de dossier de controles y evidencia de auditorias; derechos de auditoria y DRP formales.
- Accion recomendada: Preparar dossier de controles (incluyendo antivirus, firewall, backup gestionados por RhB), documentar plan de recuperacion y acordar derechos de auditoria sin necesidad de modalidad cloud.

### 9.2.4 Near real-time interface requirements
- Aplicabilidad: Alta si RhB requiere exportar datos operativos casi en tiempo real.
- Estado actual: Backend expone SignalR para la interfaz interna y APIs REST; base de datos SQLite no soporta CDC nativo ni Debezium.
- Gap: No existe flujo estandarizado para que RhB consuma datos near real-time.
- Accion recomendada: Documentar que actualmente no se ofrece y, de solicitarse, implementar conector push (Kafka/Event Hub o API streaming) o migrar a base de datos con CDC compatible.

### Appendix A (plataformas RhB)
- MS Entra ID: Integracion pendiente (ver 7.2).
- VMware vSphere ESX 8.X: Compatible; el backend se puede desplegar en Windows Server/VM.
- RedHat OpenShift 4.X: No se dispone de contenedor soportado oficialmente.
- Windows Server 2022: Compatible (runtime .NET 8 self-contained sobre Windows).
- Veeam Backup: Se pueden integrar backups generados manualmente; falta script oficial.
- Zabbix: No hay integracion directa para metricas.
- Microsoft Intune: N/A (no app movil).
- Microsoft Edge: Frontend soporta Edge actual.
- Atlassian Bitbucket/Confluence: Actualmente se usa Git (no se especifica repositorio). Ajuste documental si migran.
- Cumulocity IoT: Integracion pendiente.

## 4. Controles operativos actuales
- Antivirus y firewall: los nodos Windows Server/IPC donde se instala Aquafrisch Supervisor quedan bajo la politica de seguridad de RhB; se recomienda confirmar con su equipo la inclusion en su suite antivirus y reglas de firewall corporativas.
- Backups: el sistema genera copias por proyecto (SQLite, configuraciones y modelos); RhB puede integrarlas con Veeam siguiendo sus procedimientos.
- Certificados y HTTPS: el backend soporta HTTPS y se entrega con certificado propio; RhB puede sustituirlo por certificados oficiales y renovar segun su calendario.
- Actualizaciones: las actualizaciones del software se publican empaquetadas para instalacion manual; RhB controla la ventana de despliegue.

## 5. Prioridades recomendadas
1. Formalizar politicas y evidencia para requisitos de proceso (IA en desarrollo, auditorias, seguridad de datos) y responder oficialmente que el despliegue es on-prem.
2. Definir hoja de ruta tecnica para integrarse con los servicios centrales de RhB (MS Entra ID como prioridad, seguido de Cumulocity IoT, exportaciones de datos y monitorizacion Zabbix si se solicitan).
3. Fortalecer la seguridad de datos: cifrado en reposo, procesos de borrado seguro, acuerdos legales de propiedad de datos.
4. Preparar alternativas de despliegue compatibles con el stack de RhB (contenedores OpenShift, scripts de backup Veeam, endpoints de estado) solo si RhB lo requiere.

## 6. Referencias internas
- ROADMAP_CUMPLIMIENTO_CRA.md (estado CRA).
- ARQUITECTURA_LOGS.md (detalles de auditoria y logs).
- Manuales de despliegue y scripts (Deploy-Manual-Remote.ps1, backups/).
