# 📋 RhB IT Standards v9.0.4 - Análisis de Cumplimiento

> **Cliente:** Rhätische Bahn (RhB)  
> **Documento base:** IT_Standards_9.0.4.docx  
> **Fecha de análisis:** 2 de febrero de 2026  
> **Preparado para:** Reunión técnica con cliente

---

## 📑 Índice

1. [Resumen Ejecutivo](#1-resumen-ejecutivo)
2. [Explicación de Todos los Requisitos](#2-explicación-de-todos-los-requisitos)
3. [Matriz de Cumplimiento](#3-matriz-de-cumplimiento)
4. [Preguntas Críticas para el Cliente](#4-preguntas-críticas-para-el-cliente)
5. [Consideraciones de Responsabilidad y Mantenimiento](#5-consideraciones-de-responsabilidad-y-mantenimiento)
6. [Anexo: Plataformas RhB (Anexo A del documento)](#6-anexo-plataformas-rhb)
7. [Comparativa Versiones: v8.1 vs v9.0.4](#7-comparativa-versiones-v81-contrato-2019-vs-v904-actual)
8. [Puntos NO Negociables (Posición STAUFF)](#8-puntos-no-negociables-posición-stauff)

---

## 1. Resumen Ejecutivo

### 🎯 Objetivo del Documento RhB IT Standards

El documento RhB IT Standards v9.0.4 establece los **requisitos técnicos y operativos obligatorios** que debe cumplir cualquier software o hardware con componentes de software que se instale en la infraestructura de Rhätische Bahn.

### 📊 Clasificación de Requisitos

| Tipo | Símbolo | Significado |
|------|---------|-------------|
| **MUSS (Obligatorio)** | `X` después del número | Incumplimiento = **RECHAZO** del software |
| **SOLL (Recomendado)** | Sin marca | Incumplimiento = Evaluación caso por caso con RhB IT |

### 📈 Estado Global de Nuestro Software

| Categoría | Cumple | Parcial | No Cumple | N/A |
|-----------|--------|---------|-----------|-----|
| Requisitos MUSS (Obligatorios) | 10 | 3 | 1 | 4 |
| Requisitos SOLL (Recomendados) | 8 | 4 | 2 | 3 |
| **Total** | **18** | **7** | **3** | **7** |

### ⚠️ Gap Principal

**SSO con Microsoft Entra ID (OIDC/OAuth 2.0)** - Actualmente usamos autenticación JWT propia. RhB requiere integración obligatoria con su Identity Provider (MS Entra ID).

---

## 2. Explicación de Todos los Requisitos

### 📘 Capítulo 1: Información Básica del Documento

Este capítulo introductorio explica:
- El documento aplica a **software nuevo** y **actualizaciones** de software existente
- Incluye plugins, add-ins y extensiones de terceros
- Los requisitos marcados con **X** son obligatorios (MUSS-Kriterien)
- Los requisitos sin marca son recomendados (SOLL-Kriterien)

**Relevancia para nuestra máquina:** El software de la lavadora debe cumplir todos los requisitos MUSS para ser aceptado por RhB IT.

---

### 📘 Capítulo 2: Requisitos Generales de Software

#### 2.1 Requisitos Generales para Software Cliente y Servidor

| # | Requisito | Explicación | Obligatorio |
|---|-----------|-------------|-------------|
| .1 | Web-Apps preferidas | RhB prefiere aplicaciones web sobre aplicaciones de escritorio (Fat Client) | No |
| .2 X | **Responsive** | La aplicación web DEBE funcionar correctamente en cualquier dispositivo (PC, tablet, smartphone) | **SÍ** |
| .3 X | **Usar plataformas RhB** | Si RhB ya tiene una plataforma para cierta funcionalidad (ver Anexo A), DEBEMOS usarla, no crear la nuestra | **SÍ** |
| .4 | Compatibilidad con dependencias | Mantener compatibilidad con versiones actuales de librerías y frameworks | No |
| .5 X | **Resellers** | Si somos revendedores, debemos demostrarlo y tener acceso directo al fabricante | **SÍ** |
| .6 | Nomenclatura | RhB IT nombra servidores, clientes, rutas, usuarios (excepto Cloud) | No |
| .7 X | **E-Mail via RhB** | El envío de emails desde aplicaciones On-Premise DEBE usar el servidor de correo de RhB (Exchange Online) | **SÍ** |
| .8 X | **DMARC/DKIM/SPF** | Para apps Cloud, se deben implementar estas medidas técnicas para envío de email | **SÍ** |
| .9 X | **Hostnames, no IPs** | Toda comunicación DEBE usar nombres de host, NUNCA direcciones IP hardcodeadas | **SÍ** |
| .10 | No Phone-Home | Prohibido el acceso automático a Internet sin proxy. Si existe, debe ser desactivable | No |
| .11 | Auto-Update declarado | Las funciones de actualización automática deben declararse y acordarse con RhB IT | No |
| .12 X | **Compatible con Endpoint Protection** | El software DEBE funcionar normalmente con el antivirus de RhB instalado y activo | **SÍ** |
| .13 | Java declarado | Si usamos Java, debe declararse y acordarse con RhB IT | No |
| .14 X | **Licencia Java** | Si usamos Oracle Java, el proveedor DEBE licenciarlo. Alternativa: OpenJDK | **SÍ** |
| .15 | Comunicación cifrada | Toda comunicación entre sistemas DEBE ser cifrada (HTTPS/TLS) | No |
| .16 X | **Protección de datos** | Cumplimiento TOTAL de la ley suiza de protección de datos (o equivalente como GDPR) | **SÍ** |
| .17 X | **Contrato de procesamiento de datos** | Obligatorio si la aplicación procesa datos personales (HR, CRM) | **SÍ** |
| .18 | IA en desarrollo | Si usamos IA para desarrollar, los prompts NO pueden almacenarse para entrenamiento | No |
| .19 | Código en Git RhB | El código fuente debe subirse al repositorio Git de RhB (Atlassian Bitbucket) | No |

> **🔴 POSICIÓN STAUFF sobre §2.1.19:** Ver [Sección 8 - Puntos NO Negociables](#8-puntos-no-negociables-posición-stauff). **NO se entrega código fuente.**

#### 2.2 Documentación

| # | Requisito | Explicación | Obligatorio |
|---|-----------|-------------|-------------|
| .1 | Requisitos HW/SW/Red | Entregar documento con requisitos de hardware, software y red, incluyendo diagrama de arquitectura | No |
| .2 | **SBOM** | Entregar Software Bill of Materials (lista de dependencias) con cada versión | No |
| .3 | Detailkonzept | Documentar el concepto detallado en el sistema de documentación de RhB (Confluence) | No |
| .4 | Documentación pre-producción | La documentación debe estar completa y aceptada por RhB IT antes de ir a producción | No |
| .5 | Contenido documentación | Incluir: Betriebshandbuch (manual de operaciones), manuales de usuario y administración, historial de versiones, protocolos de test, requisitos de instalación | No |
| .6 | Actualización continua | Mantener documentación actualizada con cada cambio del sistema | No |

#### 2.3 Impresión

| # | Requisito | Explicación | Obligatorio |
|---|-----------|-------------|-------------|
| .2 | Follow-Me Printing | Soportar el sistema de impresión Windows con Follow-Me (Ricoh Streamline) | No |
| .6 | Driver universal | Usar el driver universal Ricoh PCL6 V4.37 64-bit | No |

#### 2.4 Autorizaciones / Single Sign On (SSO)

| # | Requisito | Explicación | Obligatorio |
|---|-----------|-------------|-------------|
| .1 X | **SSO obligatorio** | Después de iniciar sesión en Windows, el usuario NO debe autenticarse de nuevo en la aplicación | **SÍ** |
| .2 | Estándares de autenticación | Usar OAuth 2.0, OpenID Connect o SAML 2.0 contra MS Entra ID | No |
| .3 X | **APIs autenticadas** | Los servicios técnicos (APIs, colas) TAMBIÉN deben autenticarse via IDP | **SÍ** |
| .4 | MS Graph API | Se puede usar para obtener información adicional del usuario | No |
| .5 | RBAC | Implementar autorización basada en roles | No |
| .6 | Sin privilegios de admin | La ejecución NO debe requerir derechos de administrador | No |
| .7 | Directorio centralizado | RhB usa MS Entra ID para gestión de usuarios, grupos y contraseñas | No |
| .8 | Autenticación interna declarada | Si hay autenticación interna adicional (fuera de AD), debe declararse | No |
| .9 X | **MS Graph granular** | Los permisos de MS Graph deben ser MÍNIMOS y específicos. Prohibidos permisos globales de escritura | **SÍ** |
| .10 | Otros OS | Si se usan otros OS además de Windows, acordar SSO con RhB IT | No |

#### 2.5 Almacenamiento de Archivos / Shares

| # | Requisito | Explicación | Obligatorio |
|---|-----------|-------------|-------------|
| .1 | SharePoint preferido | Evitar almacenamiento local, preferir SharePoint | No |
| .2 | Declarar shares | Documentar shares necesarios con requisitos de permisos y backup | No |

#### 2.6 Licenciamiento

| # | Requisito | Explicación | Obligatorio |
|---|-----------|-------------|-------------|
| .1 X | **No dongles hardware** | Prohibido usar dongles físicos para licencias. Usar claves software/certificados | **SÍ** |
| .2 | Licencia offline | La verificación de licencia debe funcionar sin conexión a Internet | No |
| .3 | Cambios en licencia | Notificar cambios en modelo de licencia con 3 meses de antelación | No |

---

### 📘 Capítulo 3: Software de Servidor

#### 3.1 Sandboxed Services (Contenedores)

| # | Requisito | Explicación | Obligatorio |
|---|-----------|-------------|-------------|
| .1 | Plataforma contenedores | RhB usa RedHat OpenShift 4.X | No |
| .2 | Linux preferido | Contenedores basados en Linux. Si solo funciona en Windows, declararlo | No |
| .3 X | **Requisitos contenedor** | Imagen OCI, x86-64, Linux ELF, logs a stdout/stderr, sin root, puertos >1024, multi-instancia | **SÍ** |

#### 3.2 Virtualización

| # | Requisito | Explicación | Obligatorio |
|---|-----------|-------------|-------------|
| .1 X | **No Bare Metal** | Prohibido instalar en hardware físico directamente. Debe ser VM o contenedor | **SÍ** |
| .2 X | **VMware vSphere** | Debe ser compatible con VMware vSphere ESX 8.X | **SÍ** |
| .3 X | **Windows Server 2022** | Sistema operativo servidor debe ser Windows Server 2022 (inglés + language pack alemán) | **SÍ** |

#### 3.3 Monitoring / Métricas

| # | Requisito | Explicación | Obligatorio |
|---|-----------|-------------|-------------|
| .1 | Interfaces estándar | Proveer WMI, SNMP o Webservice para monitorización | No |
| .2 | Filtro de mensajes | Definir filtro para mensajes operativos relevantes | No |
| .3 | Parámetros a monitorizar | Documentar qué parámetros deben monitorizarse | No |

#### 3.4 Backup

| # | Requisito | Explicación | Obligatorio |
|---|-----------|-------------|-------------|
| .1-.6 | Backup con Veeam | RhB usa Veeam VBR para backups. Contenedores: Veeam K10 | No |

#### 3.5 Disponibilidad

| # | Requisito | Explicación | Obligatorio |
|---|-----------|-------------|-------------|
| .1 | Requisitos de disponibilidad | Documentar cómo se garantiza la disponibilidad requerida | No |

#### 3.6 Aplicaciones Web

##### 3.6.1 Requisitos Básicos

| # | Requisito | Explicación | Obligatorio |
|---|-----------|-------------|-------------|
| .1 X | **Sin plugins** | Prohibido requerir plugins como Silverlight, Java Applets, etc. | **SÍ** |
| .2 X | **SSO web** | Si requiere autenticación, DEBE soportar SSO | **SÍ** |
| .3 | No datos sensibles en URL | Prohibido mostrar usuarios, contraseñas en la URL | No |
| .4 | PWA | Las web apps deben poder configurarse como PWA (incluir manifest, iconos) | No |

##### 3.6.2 Browser

| # | Requisito | Explicación | Obligatorio |
|---|-----------|-------------|-------------|
| .1 | Microsoft Edge | Soportar Microsoft Edge (versión actual) | No |
| .2 X | **Compatibilidad total** | DEBE funcionar correctamente en los browsers soportados | **SÍ** |
| .3 | Compatibilidad futura | Mantener compatibilidad con nuevas versiones de browsers | No |
| .4 X | **Móviles** | Si es accesible desde móviles, DEBE mostrarse correctamente | **SÍ** |

#### 3.7 Servicios Windows

| # | Requisito | Explicación | Obligatorio |
|---|-----------|-------------|-------------|
| .1 | Windows Server Core | Preferir instalación sin GUI cuando sea posible | No |
| .2 X | **Administración remota** | DEBE poder administrarse completamente sin RDP | **SÍ** |
| .3 X | **Boot/Patch resistant** | DEBE funcionar automáticamente después de reboot, patches, service packs | **SÍ** |
| .4 X | **Service Users** | Usar cuentas de servicio, no usuarios interactivos | **SÍ** |
| .5 | Firewall/DCOM/WMI | Solicitar configuraciones especiales a RhB IT | No |
| .6 X | **Rutas UNC** | DEBE soportar rutas UNC sin mapeo de letras de unidad | **SÍ** |
| .7 X | **RDP FIPS Compliant** | Nivel de cifrado RDP debe ser FIPS Compliant | **SÍ** |
| .8 X | **Sin NetBIOS** | NetBIOS está desactivado y no debe usarse | **SÍ** |

---

### 📘 Capítulo 4: Software Cliente

#### 4.1 Software Windows

| # | Requisito | Explicación | Obligatorio |
|---|-----------|-------------|-------------|
| .1 X | **Windows 10/11 64-bit** | Compatible con Windows 10/11 64-bit con parches actuales | **SÍ** |
| .2 | 64-bit nativo | Soportar arquitectura 64-bit nativamente | No |
| .3 X | **Requisitos HW** | Documentar requisitos de hardware del cliente | **SÍ** |
| .4 X | **Instalación SYSTEM** | Instalable con usuario SYSTEM (para despliegue vía Intune) | **SÍ** |
| .5 X | **Versionado Microsoft** | Paquetes versionados según estándar Microsoft (con GUID único) | **SÍ** |
| .6 X | **Sin elevación post-instalación** | Prohibido requerir derechos elevados en primer uso | **SÍ** |
| .7 | Usuario estándar | Funcionar con usuario de dominio sin derechos de admin local | No |
| .8 | Sin servicios servidor en cliente | Prohibido IIS, MSMQ, Tomcat, Apache en el cliente | No |
| .9 | Configuración en ProgramData | Guardar config general en C:\ProgramData\%Software% | No |
| .10 | Config usuario en AppData | Guardar config de usuario en AppData\Roaming | No |
| .11 | Sin VPN desde cliente | No requerir VPN para acceder a recursos internos RhB | No |
| .12 | Distribución vía Intune | Software distribuido automáticamente vía Microsoft Intune | No |
| .13 | Sin bootstrappers | No instalar dependencias de terceros dinámicamente | No |
| .14 | Formato .msi o .intunewin | Preferir estos formatos de instalación | No |

#### 4.2 Aplicaciones Móviles (NUEVO en v9)

| # | Requisito | Explicación | Obligatorio |
|---|-----------|-------------|-------------|
| .1 | Android e iOS | Compatible con ambos sistemas operativos | No |
| .2 X | **Distribución vía Intune** | DEBE poder distribuirse vía Microsoft Intune | **SÍ** |
| .3 X | **SSO móvil** | DEBE cumplir requisitos de SSO también en móvil | **SÍ** |
| .4 X | **Formato de entrega** | Android: .aab/.apk (max 200MB). iOS: Apple Business Manager | **SÍ** |
| .5 X | **Sin VPN móvil** | Prohibido requerir VPN desde aplicaciones móviles | **SÍ** |

---

### 📘 Capítulo 5: Máquinas con Hardware Integrado

**⚠️ MUY RELEVANTE PARA NUESTRA LAVADORA**

| # | Requisito | Explicación | Obligatorio |
|---|-----------|-------------|-------------|
| .1 | Servidores/PCs de RhB IT | Preferir usar hardware proporcionado por RhB IT | No |
| .2 | Gestionado por RhB IT | RhB IT gestiona: integración en red, updates, antivirus, distribución de software | No |
| .3 | Hardware específico | Si se necesita hardware específico (parte de la máquina), declararlo | No |
| .4 | Aprobación obligatoria | **Toda máquina con conexión de red debe ser aprobada por RhB IT** | No |

#### 5.2 Integración en el Entorno RhB

| # | Requisito | Explicación | Obligatorio |
|---|-----------|-------------|-------------|
| .1 X | **Sin acceso a sistemas RhB** | Hardware externo NO puede acceder a Fileshare, Backup, etc. de RhB | **SÍ** |
| .2 X | **Zona de red separada** | DEBE conectarse en una zona de red específica y aislada | **SÍ** |
| .3 X | **Documentar comunicación** | Toda comunicación entre zonas DEBE documentarse antes de producción | **SÍ** |
| .4 X | **Internet solo vía proxy** | Si necesita Internet, SOLO a través del proxy RhB. Destinos específicos declarados | **SÍ** |
| .5 X | **DNS de RhB** | DEBE usar los servidores DNS de RhB | **SÍ** |
| .6 X | **Email solo vía RhB** | Envío de email SOLO a través del servidor de correo RhB (relay) | **SÍ** |

#### 5.3 Patching y Antivirus

| # | Requisito | Explicación | Obligatorio |
|---|-----------|-------------|-------------|
| .1 X | **Compatibilidad con updates** | Si RhB IT gestiona el sistema, la aplicación DEBE soportar patches y updates de OS | **SÍ** |
| .2 X | **Patches por proveedor** | Si es hardware externo, el PROVEEDOR es responsable de mantener parches actualizados | **SÍ** |
| .3 X | **Antivirus obligatorio** | El PROVEEDOR debe instalar, configurar y mantener actualizado el antivirus en hardware externo | **SÍ** |

#### 5.4 Mantenimiento Remoto

| # | Requisito | Explicación | Obligatorio |
|---|-----------|-------------|-------------|
| .1 X | **Solo métodos RhB** | Acceso remoto SOLO mediante métodos definidos por RhB IT (Cap. 10.3/10.4) | **SÍ** |
| .2 X | **Sin módems adicionales** | Prohibido instalar módems, routers 4G, o cualquier otro acceso remoto alternativo | **SÍ** |

---

### 📘 Capítulo 6: Internet of Things (IoT) - NUEVO en v9

| # | Requisito | Explicación | Obligatorio |
|---|-----------|-------------|-------------|
| 6.1.1 | Variante 1: Device only | Preferida: dispositivos que se integran directamente con la plataforma IoT de RhB (Cumulocity) | No |
| 6.1.2 | Variante 2: Device + Software | Si no es posible Variante 1, el proveedor trae dispositivos + software IoT que se integra con Cumulocity | No |
| .2 X | **Requisitos software IoT** | Push datos a RhB, Pull config de RhB, usar API de Cumulocity, cumplir SSO | **SÍ** |
| 6.2.1 X | **Integridad de datos** | DEBE garantizarse la integridad de datos entre dispositivo y plataforma | **SÍ** |
| 6.2.2 | Cifrado | La transmisión DEBE ser cifrada | No |

---

### 📘 Capítulo 7: Servicios Cloud - NUEVO en v9

#### 7.1 General

| # | Requisito | Explicación | Obligatorio |
|---|-----------|-------------|-------------|
| .1 | Requisitos web apps | Aplican también los requisitos de capítulos anteriores | No |
| .2 | Monitoring por proveedor | El proveedor es responsable del monitoring de sus componentes | No |
| .3 | Gestión de certificados | El proveedor gestiona certificados y su renovación | No |
| .4 | Cliente adicional declarado | Si el SaaS requiere instalación de cliente, declararlo y justificarlo | No |

#### 7.2 Integración

| # | Requisito | Explicación | Obligatorio |
|---|-----------|-------------|-------------|
| .1 X | **Modelo de referencia Cloud** | Seguir el modelo de integración Cloud de RhB | **SÍ** |
| .2 X | **SSO via Entra ID + MFA** | Autenticación OBLIGATORIA vía MS Entra ID con MFA, para usuarios Y service accounts | **SÍ** |
| .3 X | **HTTPS con certificado público** | Acceso cliente DEBE ser HTTPS con certificado público válido | **SÍ** |
| .4 X | **Interfaces cifradas y autenticadas** | Todas las interfaces deben ser cifradas + IP estática + autenticación (OAuth/certificado) | **SÍ** |
| .5 X | **Export de datos periódico** | Además de backup, proveer datos RhB regularmente en formato legible (CSV, XML) vía SFTP | **SÍ** |
| .6 | Integración monitoring | Proveer forma de integrar en monitoring RhB (status page HTTPS) | No |
| .7 | VPN para otros casos | Para conexiones no autenticadas, puede establecerse VPN permanente (IPsec) | No |

#### 7.3 Portabilidad

| # | Requisito | Explicación | Obligatorio |
|---|-----------|-------------|-------------|
| .1 | Migración sencilla | El proveedor debe facilitar migración a otro cloud (ej: containerización) | No |

#### 7.4 Datos / Protección de Datos

| # | Requisito | Explicación | Obligatorio |
|---|-----------|-------------|-------------|
| .1 X | **Datacenter en Suiza o GDPR** | Centro de datos preferiblemente en Suiza. Mínimo: país con legislación equivalente (GDPR) | **SÍ** |
| .2 X | **Soporte GDPR** | El proveedor debe ayudar con solicitudes de protección de datos y garantizar borrado completo | **SÍ** |
| .3 | Datos cifrados en reposo | Los datos en cloud deben almacenarse cifrados | No |
| .4 X | **Datos personales siempre cifrados** | Datos personales NUNCA en texto plano (ni en logs). Solo interfaces cifradas (no email) | **SÍ** |
| .5 X | **Propiedad de datos** | La propiedad de los datos DEBE permanecer en RhB | **SÍ** |
| .6 X | **Contrato de procesamiento** | Obligatorio para sistemas con datos personales (HR, CRM) | **SÍ** |

#### 7.5 Seguridad

| # | Requisito | Explicación | Obligatorio |
|---|-----------|-------------|-------------|
| .1 X | **ISO 27001** | El proveedor DEBE demostrar cumplimiento ISO 27001 o equivalente | **SÍ** |
| .2 X | **Auditorías independientes** | El proveedor DEBE someterse a auditorías regulares por expertos independientes | **SÍ** |
| .3 X | **Derecho de auditoría** | RhB DEBE tener derecho a auditar al proveedor | **SÍ** |
| .4 X | **Plan de contingencia** | El proveedor DEBE demostrar planes de emergencia y capacidad de recuperación | **SÍ** |

---

### 📘 Capítulo 8: Bases de Datos

| # | Requisito | Explicación | Obligatorio |
|---|-----------|-------------|-------------|
| .1 | Sin DB centralizada | RhB no ofrece base de datos como servicio compartido | No |
| .2 X | **Licencia DB incluida** | El proveedor DEBE incluir licencias de BD en su oferta | **SÍ** |
| .3 | Edición gratuita en Test/Int | Usar ediciones gratuitas cuando sea posible en entornos no productivos | No |
| .4 | Soporte 3 años mínimo | La versión de BD debe tener soporte del fabricante por mínimo 3 años | No |
| .5 | Elección de BD por RhB | Si el sistema soporta varias BD, RhB elige basándose en costos | No |
| .6 X | **Access prohibido** | Microsoft Access NO está permitido como base de datos | **SÍ** |
| .7 X | **Proveedor responsable de BD** | El proveedor es responsable de instalar/actualizar/mantener/soportar la BD durante todo el ciclo de vida | **SÍ** |

#### 8.1 MS-SQL

| # | Requisito | Explicación | Obligatorio |
|---|-----------|-------------|-------------|
| .1 X | **Recovery model simple en Test/Int** | En Test/Integración, usar modelo de recuperación "simple" para evitar que los logs llenen el disco | **SÍ** |
| .2 X | **Rol sysadmin para backup** | El service account de Veeam necesita rol sysadmin para backups consistentes | **SÍ** |

---

### 📘 Capítulo 9: Interfaces

#### 9.1 Principio

| # | Requisito | Explicación | Obligatorio |
|---|-----------|-------------|-------------|
| .1 | Datos del sistema maestro | Obtener datos del sistema donde se originan | No |
| .2 | Datos de usuario de AD | Se pueden obtener de Azure AD aunque no sea el sistema origen | No |
| .3 | Métodos soportados | 1) Token, 2) SCIM, 3) MS Graph API | No |
| .4 X | **LDAP prohibido** | El protocolo LDAP ya NO está permitido | **SÍ** |

#### 9.2 Conexiones de Datos

| # | Requisito | Explicación | Obligatorio |
|---|-----------|-------------|-------------|
| .1 | Middleware RhB | RhB usa Red Hat Application Foundations (Apache Camel) | No |
| .2 | Decisión de RhB IT | RhB IT decide si una interfaz usa middleware o es directa | No |
| .3 | API preferida | Preferir REST o SOAP para lectura/escritura de datos | No |
| .4 | API como estándar | La API es parte del producto estándar, no extra | No |
| .5 | Otras tecnologías | Declarar y aprobar con RhB IT | No |
| .7 X | **Autenticación en interfaces** | Cumplir requisitos de autenticación del capítulo 2.4 | **SÍ** |

#### 9.2.4 Near Real-Time (NUEVO en v9)

| # | Requisito | Explicación | Obligatorio |
|---|-----------|-------------|-------------|
| .1 | CDC / Debezium | Para datos near real-time, usar Change Data Capture o data streams | No |
| .2 | Real-time específico | Para requisitos de tiempo real estricto, acordar con RhB IT | No |

#### 9.3 Interfaces Físicas

| # | Requisito | Explicación | Obligatorio |
|---|-----------|-------------|-------------|
| .1 X | **Sin serial/USB en servidor** | Prohibido usar interfaces serie, paralelo o USB en servidores. Solo TCP/IP/Ethernet | **SÍ** |

---

### 📘 Capítulo 10: Implementación / Operación

#### 10.1 Entornos Test / Integración / Producción

| # | Requisito | Explicación | Obligatorio |
|---|-----------|-------------|-------------|
| .2 | Entorno Test | Para pruebas iniciales, instalado por proveedor | No |
| .4 X | **Entorno Integración** | Para aceptación en entorno similar a producción. Mismos pasos que producción | **SÍ** |
| .5 X | **Entorno Producción** | Solo para uso productivo. Sin pruebas. Sin cambios sin testear primero | **SÍ** |
| .6 | Automatización | El proveedor crea mecanismos para sincronizar datos Prod→Int y versiones Int→Prod | No |
| .7 X | **No datos de Int a Prod** | Prohibido transferir datos de Integración a Producción. Sin accesos cross-entorno | **SÍ** |
| .8 X | **Formación sin impacto** | Las formaciones NO deben afectar producción (no emails, no datos modificados) | **SÍ** |

#### 10.2 Separación de Tareas Proveedor / RhB IT

| # | Requisito | Explicación | Obligatorio |
|---|-----------|-------------|-------------|
| .1 X | **RhB IT prepara servidor** | RhB IT crea y configura servidor hasta nivel de OS | **SÍ** |
| .2 | Componentes OS adicionales | El proveedor puede instalar componentes OS adicionales previa consulta | No |
| .3 X | **RhB IT gestiona sistemas** | RhB IT responsable de: AD, Endpoint Protection, licencias Windows, firewall | **SÍ** |
| .4 X | **Proveedor instala su app** | El proveedor instala y documenta su software (aplicación, jobs, services) | **SÍ** |
| .6 X | **Proveedor responsable de BD** | Instalación de BD es responsabilidad del proveedor | **SÍ** |
| .7 X | **Updates de app = Change Request** | Actualizaciones de la aplicación requieren Change Request | **SÍ** |
| .8 X | **RhB IT actualiza OS** | RhB IT es responsable de updates del sistema operativo | **SÍ** |

#### 10.2.2 Operación

| # | Requisito | Explicación | Obligatorio |
|---|-----------|-------------|-------------|
| .1 X | **Soporte disponible** | El proveedor DEBE ofrecer soporte por teléfono, email o portal web | **SÍ** |
| .2 X | **Changes según RhB IT** | Después de la fase de proyecto, los cambios siguen las normas de RhB IT | **SÍ** |
| .4 X | **Mantener componentes actualizados** | El proveedor DEBE mantener todos los componentes actualizados (incluidos plugins, librerías, frameworks) | **SÍ** |
| .5 X | **Documentar cambios** | Todos los cambios DEBEN documentarse en el Betriebshandbuch | **SÍ** |
| .6 X | **Lifecycle BD por proveedor** | El proveedor gestiona el ciclo de vida de la BD (tareas regulares, exports, etc.) | **SÍ** |
| .8 X | **Housekeeping** | El proveedor es responsable del housekeeping (limpieza logs, espacio disco, etc.) | **SÍ** |

#### 10.3 Acceso Remoto

| # | Requisito | Explicación | Obligatorio |
|---|-----------|-------------|-------------|
| .1 | Métodos permitidos | "Citrix Desktop" (estándar) o "VPN (SSL/IKEv1)" (excepcional para 24x7) | No |
| .2 | Cliente Citrix | Se puede usar cliente HTML5 o instalado | No |
| .3 X | **Acuerdo de confidencialidad** | Para usar acceso remoto, el proveedor DEBE firmar NDA | **SÍ** |

#### 10.4 Soporte Remoto

| # | Requisito | Explicación | Obligatorio |
|---|-----------|-------------|-------------|
| .1 X | **Solo software de RhB** | Para soporte remoto en clientes RhB, SOLO usar software provisto por RhB (MS Teams, Citrix Director) | **SÍ** |
| .2 X | **NDA firmado** | Requiere acuerdo de confidencialidad firmado | **SÍ** |

---

## 3. Matriz de Cumplimiento

### ✅ CUMPLIMOS

| # | Requisito | Evidencia |
|---|-----------|-----------|
| 2.1.1 | Web-App preferida | ✅ React SPA |
| 2.1.9 X | Usar hostnames | ✅ `window.location.hostname` |
| 2.1.15 | Comunicación cifrada | ✅ HTTPS, WSS (SignalR) |
| 2.2.2 | SBOM | ✅ Integrado: `api.getSbomStatus()`, `generateSbom()`, `downloadSbom()` |
| 2.4.5 | RBAC | ✅ `PermissionsContext.js` |
| 2.4.6 | Sin privilegios admin | ✅ Webapp no requiere elevación |
| 2.6.1 X | Sin dongles hardware | ✅ N/A |
| 3.6.1.1 X | Sin plugins | ✅ React + BabylonJS puro |
| 3.6.1.3 | No datos sensibles en URL | ✅ JWT en headers |
| 9.1.4 X | Sin LDAP | ✅ No usa LDAP |
| 9.2.2.1 | REST API | ✅ `api.js` usa REST |
| 9.3.1 X | Sin serial/USB en servidor | ✅ Solo TCP/IP |

### ⚠️ CUMPLIMIENTO PARCIAL

| # | Requisito | Estado | Acción Necesaria |
|---|-----------|--------|------------------|
| 2.1.2 X | Responsive | Parcial | Verificar en móviles reales |
| 2.1.10 | No Phone-Home | ⚠️ | Google Fonts externo - servir localmente |
| 2.1.12 X | Endpoint Protection | ⚠️ | Testear con antivirus RhB |
| 3.6.1.4 | PWA | Parcial | Completar manifest.json |
| 3.6.2.1 | Microsoft Edge | ⚠️ | Testear explícitamente |
| 3.6.2.4 X | Móviles | ⚠️ | Verificar visualización |
| 3.7.3 X | Boot/Patch resistant | Backend | Configurar Windows Service |

### ❌ NO CUMPLIMOS

| # | Requisito | Gap | Impacto | Solución |
|---|-----------|-----|---------|----------|
| **2.4.1 X** | **SSO Entra ID** | Login propio con JWT | 🔴 **BLOQUEANTE** | Implementar MSAL.js |
| **2.4.3 X** | **APIs autenticadas via IDP** | APIs usan JWT propio | 🔴 **BLOQUEANTE** | Backend validar tokens Entra ID |
| 2.2.1-5 | Documentación formal | No existe Betriebshandbuch | 🟠 Alto | Crear documentación |

### ⚪ NO APLICA / RECHAZADO

| # | Requisito | Razón |
|---|-----------|-------|
| **2.1.19** | **Código en Git RhB** | 🔴 **NO SE ENTREGA CÓDIGO FUENTE** - Ver Sección 8 |
| 4.2 | Apps móviles | ¿Habrá app nativa móvil? |
| 6 | IoT | ¿Habrá sensores IoT? |
| 7 | Cloud | ¿Despliegue Cloud o On-Premise? |
| 3.1 | Contenedores | ¿Contenerizan el backend? |

---










## 4. Preguntas para el Cliente

> **Nota:** Estas preguntas se derivan directamente de los requisitos del documento RhB IT Standards v9.0.4. Se han priorizado según su obligatoriedad.

---

### 🔴 PREGUNTAS BLOQUEANTES

*Sin esta información no podemos cumplir los requisitos obligatorios (MUSS)*

#### Autenticación - SSO con MS Entra ID (§2.4.1-3 X - OBLIGATORIO)

| # | Pregunta | Motivo |
|---|----------|--------|
| 1 | **¿Nos pueden proporcionar los datos de registro de aplicación en MS Entra ID?** (Client ID, Tenant ID) | Obligatorio para implementar SSO según §2.4.1 X |
| 1 | **¿Existir un entorno d epruebas?** (Client ID, Tenant ID) | Para poder probar antes de instalar la maquina |
| 2 | **¿Qué scopes/permisos de MS Graph necesitamos solicitar?** | Según §2.4.9 X deben ser mínimos y específicos |
| 3 | **¿Los roles de usuario vendrán como grupos de AD en el token?** | Para mapear con nuestro sistema RBAC existente |
| 3 | **¿Los nombres roles los podemos proporcionar nosotros o vienen de RhB?** | Para mapear con nuestro sistema RBAC existente |

#### Red y Proxy (§5.2.4 X - OBLIGATORIO)

| # | Pregunta | Motivo |
|---|----------|--------|
| 4 | **¿Cuál es la URL/configuración del proxy de RhB?** | §5.2.4 X: Internet solo vía proxy RhB. Necesario para actualizaciones de antivirus |
| 5 | **¿Cuáles son los servidores DNS de RhB a configurar?** | §5.2.5 X: Obligatorio usar DNS de RhB |

#### Email - Solo si aplica (§5.2.6 X - OBLIGATORIO si hay alertas)

| # | Pregunta | Motivo |
|---|----------|--------|
| 6 | **¿Se requiere que la máquina envíe emails de alertas/notificaciones?** | Si SÍ → necesitamos configuración del relay Exchange (§2.1.7 X, §5.2.6 X) |

#### Comunicación entre zonas (§5.2.3 X - OBLIGATORIO)

| # | Pregunta | Motivo |
|---|----------|--------|
| 7 | **¿Qué puertos/protocolos deben estar abiertos entre la zona de la máquina y otras zonas RhB?** | §5.2.3 X: Toda comunicación debe documentarse antes de producción |

---

### 🟠 PREGUNTAS IMPORTANTES

*Necesarias para planificación y operación según especificaciones*

#### Acceso Remoto para Soporte (§10.3-10.4 - OBLIGATORIO)

| # | Pregunta | Motivo |
|---|----------|--------|
| 8 | **¿Qué método de acceso remoto usaremos: Citrix Desktop o VPN SSL?** | §10.3.1: Solo estos métodos permitidos |
| 9 | **¿Tienen plantilla estándar de NDA para firmar?** | §10.3.3 X: Obligatorio firmar antes de tener acceso remoto |
| 10 | **¿Cuál es el proceso para solicitar acceso remoto?** | Para planificar tiempos de soporte |

#### Documentación (§2.2 - RECOMENDADO)

| # | Pregunta | Motivo |
|---|----------|--------|
| 11 | **¿Tienen plantilla de Betriebshandbuch o seguimos formato libre?** | §2.2.5: Obligatorio entregar manual de operaciones |
| 12 | **¿En qué idioma debe estar la documentación?** (alemán/inglés) | Para preparar documentación correctamente |
| 13 | **¿La documentación debe ir en Confluence de RhB o aceptan nuestro formato?** | §2.2.3 recomienda Confluence, pero es SOLL no MUSS |

#### Change Management (§10.2.2.7 X - OBLIGATORIO)

| # | Pregunta | Motivo |
|---|----------|--------|
| 14 | **¿Cuál es el proceso de Change Request para actualizaciones de la aplicación?** | §10.2.2.7 X: Updates requieren Change Request obligatorio |
| 15 | **¿Hay ventanas de mantenimiento definidas?** | Para planificar actualizaciones |

---

### 🟡 PREGUNTAS DE CLARIFICACIÓN

*Para definir alcance y evitar malentendidos*

#### Datos y Privacidad (§2.1.16-17)

| # | Pregunta | Motivo |
|---|----------|--------|
| 16 | **¿Qué datos se consideran "personales" en el contexto de la lavadora?** (¿operadores, logs de acciones?) | §2.1.16-17 X: Determina si necesitamos contrato de procesamiento de datos |

#### Uso de la Aplicación

| # | Pregunta | Motivo |
|---|----------|--------|
| 17 | **¿La aplicación se usará SOLO desde el IPC de la máquina o también desde otros dispositivos?** | §3.6.2.4 X: Si se accede desde móviles, debe mostrarse correctamente |

#### Integración (OPCIONAL - solo si lo ofrecemos)

| # | Pregunta | Motivo |
|---|----------|--------|
| 18 | **¿Necesitan que los datos de la lavadora se envíen a algún sistema RhB?** (SCADA, Cumulocity IoT, etc.) | Capítulo 6: Solo preguntar si queremos ofrecer esta integración |

---

### ✅ NO NECESITAMOS PREGUNTAR (Ya definido)

| Tema | Nuestra posición | Referencia |
|------|------------------|------------|
| **¿Quién gestiona el IPC?** | STAUFF (hardware externo) | §5.1.3-4, §5.3.2-3 |
| **¿Dónde se despliega el backend?** | En el IPC de la máquina | Decisión de producto fija |
| **¿Especificaciones del IPC?** | Las definimos nosotros | Hardware nuestro |
| **¿Quién es responsable de antivirus/patches?** | STAUFF | §5.3.2-3 X |
| **¿Se entrega código fuente?** | NO | Posición STAUFF (ver Sección 8) |

---

### 📋 Resumen: 18 preguntas totales

| Prioridad | Cantidad | Descripción |
|-----------|----------|-------------|
| 🔴 Bloqueantes | 7 | Sin esto no cumplimos requisitos obligatorios |
| 🟠 Importantes | 8 | Necesarias para operación correcta |
| 🟡 Clarificación | 3 | Para definir alcance |


















## 5. Consideraciones de Responsabilidad y Mantenimiento

### 📋 Matriz de Responsabilidades (RACI)

| Componente | Proveedor (Nosotros) | RhB IT | Notas |
|------------|---------------------|--------|-------|
| **Hardware IPC** | | | |
| - Suministro | R | A | Nosotros proveemos, RhB aprueba |
| - Instalación física | R | I | |
| - Garantía hardware | R | I | Duración según contrato |
| **Sistema Operativo** | | | |
| - Instalación inicial | R | A | Según imagen RhB o propia |
| - Patches de seguridad | **¿?** | **¿?** | **PREGUNTAR** |
| - Updates de versión | **¿?** | **¿?** | **PREGUNTAR** |
| **Antivirus / Endpoint Protection** | | | |
| - Instalación | **¿?** | **¿?** | **PREGUNTAR** |
| - Licencia | **¿?** | **¿?** | **PREGUNTAR** |
| - Actualizaciones firmas | **¿?** | **¿?** | **PREGUNTAR** |
| - Mantenimiento | **¿?** | **¿?** | **PREGUNTAR** |
| **Firewall** | | | |
| - Configuración inicial | C | R | RhB configura su firewall |
| - Mantenimiento | I | R | RhB mantiene |
| - Reglas específicas app | R | A | Nosotros solicitamos, RhB aprueba |
| **Aplicación (Backend)** | | | |
| - Instalación | R | I | |
| - Actualizaciones | R | A | Change Request obligatorio |
| - Bugs | R | I | |
| - Soporte L1 | R | I | |
| - Soporte L2/L3 | R | I | |
| **Base de Datos** | | | |
| - Instalación | R | I | |
| - Licencia | R | - | Incluida en nuestro precio |
| - Backup | C | R | RhB con Veeam, nosotros config |
| - Mantenimiento | R | I | |
| - Housekeeping | R | I | |
| **Documentación** | | | |
| - Betriebshandbuch | R | A | |
| - Actualizaciones | R | I | Con cada cambio |

**Leyenda:** R = Responsable, A = Aprueba, C = Consultado, I = Informado

---

### 💰 Consideraciones Económicas Post-Garantía

#### Escenario 1: Hardware gestionado por RhB IT

Si RhB IT gestiona el hardware (IPC conectado a su dominio):

| Concepto | Durante Garantía | Post-Garantía |
|----------|------------------|---------------|
| Antivirus | RhB IT | RhB IT |
| Patches OS | RhB IT | RhB IT |
| Firewall | RhB IT | RhB IT |
| Updates App | Nosotros (incluido) | **Contrato de mantenimiento** |
| Soporte App | Nosotros (incluido) | **Contrato de mantenimiento** |

#### Escenario 2: Hardware externo (más probable para máquina industrial)

Si el IPC es "RhB IT fremd" (hardware externo no gestionado por RhB IT):

| Concepto | Durante Garantía | Post-Garantía | Responsable |
|----------|------------------|---------------|-------------|
| **Antivirus** | Incluido | **Contrato** | **NOSOTROS** |
| - Licencia anual | ✅ | 💰 | |
| - Actualizaciones firmas | ✅ | 💰 | |
| **Patches OS** | Incluido | **Contrato** | **NOSOTROS** |
| - Windows Updates | ✅ | 💰 | |
| - Compatibilidad app | ✅ | 💰 | |
| **Updates App** | Incluido | **Contrato** | NOSOTROS |
| **Soporte App** | Incluido | **Contrato** | NOSOTROS |
| **Firewall** | N/A | N/A | RhB IT (su infra) |

#### ⚠️ PUNTOS CRÍTICOS A ACLARAR

1. **¿Quién proporciona la licencia del antivirus?**
   - Según §5.3.3 X: "El proveedor debe instalar, configurar y mantener actualizado el antivirus"
   - **Interpretación:** Nosotros somos responsables del antivirus en hardware externo
   - **Implicación:** Debemos incluir licencia de antivirus en el precio

2. **¿Qué antivirus es compatible/aceptado?**
   - RhB usa Endpoint Protection específica (Anexo A)
   - **Preguntar:** ¿Podemos usar el mismo? ¿O uno compatible?

3. **¿Actualizaciones de Windows son compatibles con nuestra app?**
   - Según §5.3.1 X: La aplicación DEBE soportar patches y updates
   - **Implicación:** Debemos testear cada Windows Update

4. **¿Quién paga el mantenimiento del antivirus post-garantía?**
   - **Opción A:** Incluirlo en contrato de mantenimiento anual
   - **Opción B:** Cliente lo gestiona directamente
   - **Recomendación:** Incluir en contrato de mantenimiento

---

### 📝 Propuesta de Modelo de Mantenimiento

#### Durante Garantía (Incluido en precio máquina)

- ✅ Instalación y configuración completa
- ✅ Antivirus (licencia + actualizaciones)
- ✅ Patches de Windows (aplicación y testing)
- ✅ Actualizaciones de la aplicación (bugs, mejoras menores)
- ✅ Soporte técnico (L1, L2, L3)
- ✅ Acceso remoto para diagnóstico
- ✅ Backup de configuración

#### Post-Garantía: Contrato de Mantenimiento Anual

**Opción A: Mantenimiento Completo**
- ✅ Todo lo anterior
- ✅ Nuevas versiones de la aplicación
- 💰 Precio: X% del valor de la máquina/año

**Opción B: Mantenimiento Básico**
- ✅ Antivirus (licencia + actualizaciones)
- ✅ Patches de Windows críticos
- ✅ Soporte técnico (horario limitado)
- ❌ Nuevas versiones (bajo presupuesto separado)
- 💰 Precio: Y% del valor de la máquina/año

**Opción C: Solo Soporte**
- ❌ Sin mantenimiento preventivo
- ✅ Soporte bajo demanda (tarifa horaria)
- ⚠️ Cliente asume riesgo de seguridad

---

### 🔒 Riesgos de Seguridad Sin Mantenimiento

| Riesgo | Probabilidad | Impacto | Mitigación |
|--------|--------------|---------|------------|
| Antivirus desactualizado | Alta | Crítico | Contrato mantenimiento |
| Windows sin parchear | Alta | Crítico | Contrato mantenimiento |
| Vulnerabilidades en app | Media | Alto | Actualizaciones regulares |
| Incompatibilidad tras update | Media | Medio | Testing antes de deploy |
| Ransomware | Media | Crítico | Antivirus + Backup |

---

## 6. Anexo: Plataformas RhB

### Plataformas y Componentes Actuales (Anexo A v9.0.4)

| Categoría | Plataforma/Producto |
|-----------|---------------------|
| **Identity Provider** | Microsoft Entra ID |
| **Virtualización** | VMware vSphere ESX 8.X |
| **Contenedores** | RedHat OpenShift 4.X |
| **OS Servidor** | Windows Server 2022 (EN + DE pack) |
| **Backup** | Veeam VBR (VMs), Veeam K10 (Containers) |
| **Monitoring** | Zabbix |
| **Distribución SW** | Microsoft Intune |
| **OS Cliente** | Windows 10/11 64-bit, Citrix XenDesktop |
| **Browser** | Microsoft Edge (versión actual) |
| **Email** | Exchange Online |
| **Middleware** | Red Hat Application Foundations (Apache Camel) |
| **Documentación** | MS SharePoint, Atlassian Confluence |
| **Archivado** | Windream |
| **Workflow** | AgilePoint |
| **RPA** | UiPath |
| **IoT** | Cumulocity IoT Platform |
| **Analytics** | MS Azure Analytics, Power BI |
| **SMS** | IP Plus SMS Gateway |
| **Firma electrónica** | Skribble |
| **Impresión** | Ricoh Streamline + Driver PCL6 V4.37 |
| **Git** | Atlassian Bitbucket |
| **Remote Support** | MS Teams, Citrix Director |

---

## 7. Comparativa Versiones: v8.1 (Contrato 2019) vs v9.0.4 (Actual)

> **IMPORTANTE:** Esta sección documenta las diferencias entre la versión del documento que formaba parte del contrato original (v8.1, octubre 2017) y la versión actual (v9.0.4, febrero 2025). Los requisitos nuevos o significativamente modificados justifican costes adicionales no contemplados en el alcance original.

---

### 7.1 Requisitos Completamente NUEVOS (No existían en el contrato)

Estos son **costes adicionales 100% justificados** porque no estaban en el documento del contrato:

| # | Requisito NUEVO | Sección v9.0.4 | Impacto | Descripción del Cambio |
|---|-----------------|----------------|---------|------------------------|
| **1** | **SSO con MS Entra ID (OAuth 2.0/OIDC)** | §2.4.1-3 X | 🔴 **CRÍTICO** | En v8.1 era Active Directory con LDAP. Ahora LDAP está **PROHIBIDO** y se exige OAuth 2.0/OpenID Connect con MS Entra ID |
| **2** | **MS Graph API con permisos granulares** | §2.4.9 X | 🔴 **ALTO** | Requisito completamente nuevo añadido en v9.0.4. Los permisos deben ser mínimos y específicos |
| **3** | **SBOM (Software Bill of Materials)** | §2.2.2 | 🟠 MEDIO | No existía en v8.1. Ahora obligatorio entregar SBOM con cada versión del software |
| **4** | **Capítulo 4.2: Aplicaciones Móviles** | §4.2 completo | 🟠 MEDIO | Capítulo completamente nuevo. Requisitos para Android/iOS, distribución vía Intune, SSO móvil |
| **5** | **Capítulo 6: Internet of Things (IoT)** | §6 completo | 🟠 MEDIO | Capítulo completamente nuevo. Integración con Cumulocity IoT Platform |
| **6** | **Capítulo 7: Cloud Services** | §7 completo | 🔴 **ALTO** | Capítulo completamente nuevo. Incluye requisitos de ISO 27001, auditorías independientes, derecho de auditoría, MFA obligatorio |
| **7** | **Near real-time interfaces (CDC/Debezium)** | §9.2.4 | 🟠 MEDIO | Sección completamente nueva para interfaces de tiempo casi-real |
| **8** | **Requisitos de IA en desarrollo** | §2.1.18 | 🟡 BAJO | Nuevo en v9.0.3. Restricciones sobre uso de IA en desarrollo |
| **9** | **PWA obligatorio para web apps** | §3.6.1.4 | 🟡 BAJO | Las web apps deben poder configurarse como PWA |
| **10** | **DMARC/DKIM/SPF para Cloud** | §2.1.8 X | 🟠 MEDIO | Medidas técnicas obligatorias para envío de email desde Cloud |
| **11** | **Contrato procesamiento datos personales** | §2.1.17 X, §7.4.6 X | 🟠 MEDIO | Ahora explícitamente obligatorio para sistemas con datos personales |
| **12** | **Export periódico datos legibles** | §7.2.5 X | 🟠 MEDIO | Además de backup, proveer datos regularmente en CSV/XML vía SFTP |
| **13** | **Documentación en Confluence RhB** | §2.2.3-4 | 🟠 MEDIO | Documentación debe estar en el sistema central de RhB (Confluence) |

> **NOTA sobre §2.1.19 (Código fuente en Bitbucket):** Este requisito **NO APLICA** - Ver [Sección 8 - Puntos NO Negociables](#8-puntos-no-negociables-posición-stauff)

---

### 7.2 Requisitos Significativamente MODIFICADOS

Estos requisitos existían pero han cambiado sustancialmente, requiriendo **trabajo adicional**:

| # | Requisito | v8.1 (Contrato 2019) | v9.0.4 (Actual) | Impacto |
|---|-----------|----------------------|-----------------|---------|
| **1** | **Identity Provider** | Microsoft Active Directory | **Microsoft Entra ID** | 🔴 Cambio completo de tecnología de autenticación |
| **2** | **Protocolo autenticación** | LDAP permitido (§7.1.2) | **LDAP PROHIBIDO** (§9.1.4 X) | 🔴 Reescritura completa del módulo de autenticación |
| **3** | **Estándares auth** | AD + Kerberos | **OAuth 2.0 / OpenID Connect / SAML 2.0** | 🔴 Nueva implementación con MSAL |
| **4** | **Virtualización** | VMware vSphere 6.5 | **VMware vSphere ESX 8.X** | 🟠 Verificar compatibilidad, posibles ajustes |
| **5** | **OS Servidor** | Windows Server 2012 R2 / 2016 | **Windows Server 2022** | 🟠 Migración y testing de compatibilidad |
| **6** | **Contenedores** | OpenShift 3.6 (opcional) | **OpenShift 4.X** + requisitos estrictos (OCI, sin root, logs stdout, puertos >1024) | 🟠 Si se containeriza |
| **7** | **Browser soportado** | IE11, Firefox 55+, Chrome 58+ | **Solo Microsoft Edge (versión actual)** | 🟡 Testing específico en Edge |
| **8** | **Middleware** | Oracle Service Bus 12c | **Red Hat Application Foundations (Apache Camel)** | 🟠 Si hay integración con middleware |
| **9** | **Monitoring** | System Center Operations Manager (SCOM) 2012 R2 | **Zabbix** | 🟠 Implementar métricas para Zabbix |
| **10** | **Distribución SW** | System Center Configuration Manager (SCCM) | **Microsoft Intune** | 🟠 Nuevo formato de empaquetado si aplica |
| **11** | **Backup** | Veeam 9.5 | **Veeam VBR + Veeam K10** (contenedores) | 🟡 Verificar compatibilidad |
| **12** | **Remote Support** | Citrix Director + Remote Control Viewer | **MS Teams + Citrix Director** | 🟡 Cambio menor |
| **13** | **Impresión** | Equitrac 5.6, Ricoh PCL6 V4.11 | **Ricoh Streamline, Ricoh PCL6 V4.37** | 🟡 Si hay funciones de impresión |
| **14** | **OS Cliente** | Windows 7 & 10 | **Windows 10/11 64-bit** | 🟡 Testing |
| **15** | **Administración remota servidor** | No especificado claramente | **DEBE poder administrarse sin RDP** (§3.7.2 X) | 🟠 Posible trabajo adicional |

---

### 7.3 Requisitos que YA EXISTÍAN (Incluidos en precio original)

Estos requisitos **NO justifican coste adicional** porque ya estaban en v8.1:

| Requisito | Referencia v8.1 | Referencia v9.0.4 |
|-----------|-----------------|-------------------|
| Web-Apps preferidas sobre Fat Client | §2.1.1 | §2.1.1 |
| Sin plugins (Silverlight, Java Applets) | §3.7.1.2 | §3.6.1.1 X |
| RBAC (autorización basada en roles) | §2.4.1.1 | §2.4.5 |
| Sin dongles hardware para licencias | §2.6.1 | §2.6.1 X |
| Usar hostnames, no IPs | §2.1.6-7 | §2.1.9 X |
| No Phone-Home / No acceso directo Internet | §2.1.8 | §2.1.10 |
| Compatible con antivirus | §2.1.9 | §2.1.12 X |
| Boot/Patch resistant | §3.8.1 | §3.7.3 X |
| Usar Service Users para tasks/services | §3.8.2 | §3.7.4 X |
| Soportar rutas UNC | §3.8.7 | §3.7.6 X |
| FIPS Compliant RDP | §3.8.9 | §3.7.7 X |
| Sin NetBIOS | §3.8.10 | §3.7.8 X |
| Sin interfaces serial/USB en servidor | §7.3.1 | §9.3.1 X |
| Microsoft Access prohibido | §6.1.8 | §8.6 X |
| Licencia DB incluida por proveedor | §6.1.2 | §8.2 X |
| Recovery model "simple" en Test/Int | §6.1.11 | §8.1.1 X |
| Entornos Test/Integración/Producción | §8.1 | §10.1 |
| Betriebshandbuch obligatorio | §2.2.4 | §2.2.5 |
| Remote via Citrix/VPN | §8.3 | §10.3 |
| NDA para acceso remoto | §8.3.4 | §10.3.3 X |
| Proveedor responsable de BD | §6.1.10 | §8.7 X |
| Housekeeping por proveedor | §8.2.2.1 | §10.2.2.8 X |

---

### 7.4 Cambios en Plataformas RhB (Anexo A)

| Componente | v8.1 (2017) | v9.0.4 (2025) | Impacto |
|------------|-------------|---------------|---------|
| **Identity Provider** | Microsoft Active Directory | **Microsoft Entra ID** | 🔴 CRÍTICO |
| **Virtualización** | VMware vSphere ESX 6.5 | VMware vSphere ESX 8.X | 🟠 Testing |
| **Contenedores** | RedHat OpenShift 3.6 | RedHat OpenShift 4.X | 🟠 Si aplica |
| **OS Servidor** | Windows Server 2012 R2 / 2016 | Windows Server 2022 | 🟠 Migración |
| **OS Cliente** | Windows 7 & 10 | Windows 10/11 64-bit | 🟡 Testing |
| **Browser** | IE11, Firefox 55+, Chrome 58+ | Microsoft Edge | 🟡 Testing |
| **Middleware** | Oracle Service Bus 12c | Red Hat Apache Camel | 🟠 Si integración |
| **Monitoring** | SCOM 2012 R2 | Zabbix | 🟠 Nuevas métricas |
| **Distribución** | SCCM | Microsoft Intune | 🟠 Si aplica |
| **Backup** | Veeam 9.5 | Veeam VBR + K10 | 🟡 Verificar |
| **Impresión** | Equitrac 5.6 | Ricoh Streamline | 🟡 Si aplica |
| **Driver impresora** | Ricoh PCL6 V4.11 | Ricoh PCL6 V4.37 | 🟡 Si aplica |
| **Remote Support** | Citrix Director + Remote Control Viewer | MS Teams + Citrix Director | 🟡 Menor |
| **Documentación** | (no especificado) | MS SharePoint + Confluence | 🟠 Nuevo |
| **IoT Platform** | No existía | Cumulocity IoT | 🟠 Si aplica |
| **Analytics** | No existía | MS Azure Analytics + Power BI | 🟠 Si aplica |
| **SMS Gateway** | No existía | IP Plus SMS Gateway | 🟡 Si aplica |
| **Firma electrónica** | No existía | Skribble | 🟡 Si aplica |
| **Workflow** | No existía | AgilePoint | 🟡 Si aplica |
| **RPA** | No existía | UiPath | 🟡 Si aplica |

---

### 7.5 Resumen Ejecutivo para Negociación Comercial

#### Cambios que Justifican Costes Adicionales

| # | Concepto | Motivo | Esfuerzo Estimado |
|---|----------|--------|-------------------|
| **1** | **Migración autenticación AD → Entra ID** | LDAP prohibido en v9.0.4 (§9.1.4 X). Requiere implementar OAuth 2.0/OIDC con MSAL | 2-4 semanas |
| **2** | **Compatibilidad Windows Server 2022** | Cambio de OS servidor obligatorio | 1 semana |
| **3** | **Compatibilidad VMware 8.X** | Nueva versión de virtualización | 1 semana |
| **4** | **Testing Microsoft Edge** | Único browser soportado (antes IE11/Firefox/Chrome) | 2-3 días |
| **5** | **Integración Zabbix** | Nuevo sistema de monitoring (antes SCOM) | 3-5 días |
| **6** | **Documentación Confluence** | Nuevo requisito de documentación centralizada | 1 semana |
| **7** | **SBOM con cada versión** | Nuevo requisito (§2.2.2) | ✅ Ya implementado |

---

### 7.6 Tabla de Costes Adicionales (Plantilla)

| Item | Descripción | Justificación | Horas | €/hora | Total € |
|------|-------------|---------------|-------|--------|---------|
| 1 | Migración autenticación AD → Entra ID (OAuth 2.0/OIDC) | LDAP prohibido en v9.0.4, no existía en v8.1 | ___ | ___ | ___ |
| 2 | Testing compatibilidad Windows Server 2022 | Cambio de versión OS servidor | ___ | ___ | ___ |
| 3 | Testing compatibilidad VMware 8.X | Cambio de versión virtualización | ___ | ___ | ___ |
| 4 | Testing Microsoft Edge | Nuevo browser único (antes IE11/FF/Chrome) | ___ | ___ | ___ |
| 5 | Integración métricas Zabbix | Nuevo sistema monitoring (antes SCOM) | ___ | ___ | ___ |
| 6 | Documentación en Confluence RhB | Nuevo requisito documentación centralizada | ___ | ___ | ___ |
| 7 | Gestión proyecto y coordinación | Overhead por cambios no previstos | ___ | ___ | ___ |
| | **SUBTOTAL** | | | | ___ |
| | IVA (XX%) | | | | ___ |
| | **TOTAL** | | | | **___** |

---

### 7.7 Referencias Documentales

| Documento | Versión | Fecha | Uso |
|-----------|---------|-------|-----|
| IT_Standards_8.1.docx | v8.1 | 11.10.2017 | Especificaciones del contrato original (2019) |
| IT_Standards_9.0.4.docx | v9.0.4 | 13.02.2025 | Especificaciones actuales requeridas |
| IT_Standards_9.0.4_Addendum_EN.docx | v9.0.4 | 13.02.2025 | Traducción inglés de cambios |

---

## 8. Puntos NO Negociables (Posición STAUFF)

> **⚠️ IMPORTANTE:** Los siguientes puntos representan la posición firme de STAUFF y deben comunicarse claramente al cliente desde el inicio.

---

### 🔴 8.1 Código Fuente - NO SE ENTREGA

#### Requisito RhB (§2.1.19):
> *"Die RhB stellt bei Bedarf das Quellcode Repository zur Verfügung (Bitbucket @ RhB)"*  
> ("RhB proporciona, si es necesario, el repositorio de código fuente")

#### **POSICIÓN STAUFF: NO SE ENTREGA CÓDIGO FUENTE**

| Aspecto | Posición |
|---------|----------|
| **Decisión** | 🔴 **NO se entrega código fuente bajo ninguna circunstancia** |
| **Motivo principal** | Propiedad intelectual y secreto comercial de STAUFF |
| **Modelo de negocio** | Licenciamiento de software / Servicio - NO venta de producto |
| **Interpretación del requisito** | El requisito dice "bei Bedarf" (si es necesario). Consideramos que NO es necesario para la operación del sistema |

#### **Justificación Legal y Comercial:**

1. **Propiedad Intelectual**
   - El código fuente es secreto comercial protegido
   - Desarrollado con inversión propia de STAUFF
   - No forma parte del alcance contractual de venta de máquina

2. **Modelo de Negocio**
   - Vendemos **licencia de uso** del software, no el software en sí
   - El cliente recibe: aplicación funcional + documentación + soporte
   - El cliente NO recibe: código fuente, derecho de modificación, derecho de redistribución

3. **Riesgo Comercial**
   - Entrega de código = Pérdida de ventaja competitiva
   - Posibilidad de competencia desleal con nuestro propio código
   - Imposibilidad de proteger mejoras futuras

#### **Alternativas Ofrecidas:**

| Alternativa | Descripción | Incluido |
|-------------|-------------|----------|
| ✅ **Documentación técnica completa** | API documentation, arquitectura, flujos de datos, configuración | SÍ |
| ✅ **Binarios/builds versionados** | Aplicación compilada lista para despliegue | SÍ |
| ✅ **Scripts de deployment** | PowerShell, configuración IIS, Docker si aplica | SÍ |
| ✅ **SBOM (Software Bill of Materials)** | Lista completa de dependencias y versiones | SÍ |
| 💰 **Software Escrow** | Código depositado en tercero neutral, accesible SOLO si STAUFF quiebra o deja de dar soporte | Coste adicional, negociable |

#### **Software Escrow (si el cliente insiste):**

Si RhB requiere garantía de acceso al código en caso de discontinuidad:

| Concepto | Descripción |
|----------|-------------|
| **Qué es** | Tercero neutral (ej: Iron Mountain, NCC Group) custodia el código |
| **Condiciones de liberación** | Solo si STAUFF: quiebra, deja de dar soporte, incumple SLA grave |
| **Coste** | Setup inicial + mantenimiento anual (a cargo de RhB si lo requieren) |
| **Actualización** | Con cada versión mayor del software |

#### **Si el cliente insiste en código fuente:**

> ⚠️ **Esto cambia completamente el modelo de negocio:**

| Escenario | Implicación |
|-----------|-------------|
| **Precio** | Multiplicador significativo (3-5x) sobre precio de licencia |
| **Contrato** | Venta de producto con cesión de IP, no licenciamiento |
| **Soporte** | Opcional, no obligatorio |
| **Futuras versiones** | No incluidas - desarrollo independiente |
| **NDA** | RhB firma compromiso de protección del código |

---

### Tabla Resumen: Puntos NO Negociables

| # | Punto | Posición STAUFF | Alternativa Ofrecida |
|---|-------|-----------------|---------------------|
| **1** | **Código fuente** | 🔴 **NO se entrega** | Documentación + Binarios + Escrow opcional |
| **2** | Acceso remoto propio | Aceptamos métodos RhB (Citrix/VPN) | N/A |
| **3** | Antivirus en HW externo | Aceptamos responsabilidad (incluido en precio) | N/A |

---

## 📎 Documentos de Referencia

- IT_Standards_9.0.4.docx (documento original RhB)
- IT_Standards_9.0.4_Addendum_EN.docx (traducción inglés)
- RhB Software Development Compliance Checklist (resumen cliente)
- IT_Standards_8.1.docx (versión contrato original 2019)

---

## ✍️ Notas de la Reunión

*(Espacio para notas durante la reunión con el cliente)*

**Fecha:** ____________________

**Asistentes:** ____________________

**Decisiones tomadas:**

1. ____________________
2. ____________________
3. ____________________

**Próximos pasos:**

1. ____________________
2. ____________________
3. ____________________

---

*Documento generado el 2 de febrero de 2026*

---

## Texto Sugerido para Comunicación Comercial

**Asunto:** Costes adicionales por actualización RhB IT Standards v8.1 → v9.0.4

Estimados Sres.,

Tras analizar detalladamente el documento RhB IT Standards v9.0.4 (febrero 2025) en comparación con la versión v8.1 (octubre 2017) que formaba parte de las especificaciones técnicas del contrato firmado en 2019, hemos identificado requisitos técnicos nuevos o significativamente modificados que no estaban contemplados en el alcance original del proyecto.

**CAMBIOS PRINCIPALES QUE REQUIEREN TRABAJO ADICIONAL:**

**1. AUTENTICACIÓN (Cambio crítico)**
- Contrato original (v8.1): Microsoft Active Directory con LDAP permitido
- Versión actual (v9.0.4): LDAP expresamente PROHIBIDO (§9.1.4 X)
- Nuevo requisito: OAuth 2.0 / OpenID Connect con Microsoft Entra ID (§2.4.1-3 X)
- Impacto: Reimplementación completa del sistema de autenticación

**2. PLATAFORMA DE SERVIDOR**
- Contrato original: Windows Server 2012 R2 / 2016
- Versión actual: Windows Server 2022 obligatorio
- Impacto: Testing y ajustes de compatibilidad

**3. VIRTUALIZACIÓN**
- Contrato original: VMware vSphere 6.5
- Versión actual: VMware vSphere ESX 8.X
- Impacto: Verificación de compatibilidad

**4. NUEVOS CAPÍTULOS NO EXISTENTES EN CONTRATO**
- Capítulo 7: Cloud Services (ISO 27001, auditorías, MFA)
- Capítulo 6: Internet of Things (IoT)
- Capítulo 4.2: Aplicaciones Móviles
- Sección 9.2.4: Interfaces near real-time

**5. DOCUMENTACIÓN**
- Nuevo: Documentación obligatoria en Confluence de RhB
- Nuevo: SBOM obligatorio con cada versión

Estos cambios representan un esfuerzo adicional que no estaba contemplado en el precio original del proyecto. Adjuntamos presupuesto detallado para su consideración.

Quedamos a su disposición para una reunión de aclaración.

