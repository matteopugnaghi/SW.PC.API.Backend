# Especificación de Integración — Aquafrisch Supervisor ↔ RhB IT

**Entra ID (SSO) · Cumplimiento RhB IT Standards · Integración Modbus**

> Documento de especificación e integración para el cliente **Rhätische Bahn (RhB)**, proyecto
> *Drehgestell-Waschhalle Landquart*. Sirve además como **evidencia de diseño de seguridad** para
> **EU CRA** e **IEC 62443** (identidad, autenticación y control de uso).

<!-- INTERNO (no exportar al cliente): documentos hermanos correo-rhb.md (transmisión + seguimiento), SKILL.md (decisiones), gap-analysis-rhb.md. -->

---

## 0. Control del documento

| Campo | Valor |
|---|---|
| Código del documento | **06.7-C07-20** |
| Título | Especificación_de_Integración_RhB_IT_Entra_ID_Cumplimiento_Modbus |
| Versión | 0.3 (borrador para revisión) |
| Estado | 🟡 Borrador, no enviado |
| Autor | Pugnaghi Matteo — Aquafrisch S.L.U. |
| Fecha | 2026-06-27 |
| Cliente / Proyecto | Rhätische Bahn (RhB) — Drehgestell-Waschhalle Landquart |
| Idioma | Español (se añadirá alemán: frase ES / frase DE) |
| Clasificación | Confidencial |
| Referencia cliente | RhB IT Standards v9.0.4 |

### Histórico de versiones
| Versión | Fecha | Cambios |
|---|---|---|
| 0.1 | 2026-06-27 | Versión inicial consolidada (Entra ID + Cumplimiento + Modbus). |
| 0.2 | 2026-06-27 | Código y título del documento; retiradas referencias internas; limpieza de redacción; ajuste del cap. 7 (Plan). |
| 0.3 | 2026-06-27 | Ajustes de redacción: autor; estructura por capítulos A/B/C; protocolo ADS de Beckhoff; modo kiosko explicado; voz directa en las peticiones a RhB; hosting como hecho; retirada la columna «Firma»; uniformidad de voz en A.2.8; clarificaciones («a través de qué interfaz» en Modbus, kiosko en 8.1). |

---

## 1. Propósito y alcance

Estamos adaptando **Aquafrisch Supervisor** a los *RhB IT Standards v9.0.4*. Este documento describe el
**contexto y la arquitectura** del sistema (sección 4) y se estructura en tres capítulos: el **Capítulo A**
especifica el **diseño de autenticación SSO con Microsoft Entra ID** y los datos que necesitamos de RhB; el
**Capítulo B** recoge los **demás puntos de cumplimiento** de los RhB IT Standards; y el **Capítulo C**
plantea la **integración Modbus**. Los **puntos abiertos** que requieren confirmación o información de RhB
están marcados a lo largo del documento y consolidados en el **capítulo 8**.

> **Estructura:** secciones numeradas **0–4** (introducción) y **5–8** (entorno de pruebas, trazabilidad,
> plan y formulario de respuesta), con tres **capítulos temáticos A, B y C** (Entra ID, Cumplimiento y
> Modbus) como cuerpo central.

---

## 2. Referencias normativas

- **RhB IT Standards v9.0.4** (Rhätische Bahn) — requisitos técnicos y operativos.
- **EU Cyber Resilience Act (CRA)** — Anexo I (requisitos esenciales de ciberseguridad), Anexo VII (documentación técnica).
- **IEC 62443-4-1** — Ciclo de vida de desarrollo seguro de productos.
- **IEC 62443-4-2 / 62443-3-3** — Requisitos técnicos de seguridad (FR1 Identification & Authentication Control, FR2 Use Control).
- **Microsoft Entra ID** — OpenID Connect, OAuth 2.0, MSAL, Microsoft Graph.

---

## 3. Definiciones y glosario

| Término | Significado |
|---|---|
| **Entra ID** | Proveedor de identidad en la nube de Microsoft (antes Azure AD). |
| **OIDC / OpenID Connect** | Protocolo estándar de inicio de sesión web sobre OAuth 2.0. |
| **Authorization Code + PKCE** | Flujo OIDC seguro recomendado para aplicaciones de navegador (SPA). |
| **MFA** | Autenticación multifactor (un segundo factor además de la contraseña). |
| **Break-glass** | Cuenta local de emergencia que permite operar sin conexión al IDP. |
| **RBAC** | Control de acceso basado en roles. |
| **SPA** | Single-Page Application (aplicación web que corre en el navegador). |
| **object ID** | Identificador inmutable de un usuario en Entra. |
| **UNC** | Ruta de red Windows (`\\servidor\carpeta`). |
| **Modbus server/client** | Servidor/esclavo expone registros; cliente/maestro lee/escribe registros de otros. |

---

## 4. Contexto del sistema y arquitectura

- **Aquafrisch Supervisor** es una **aplicación web (SCADA/HMI)** que supervisa y controla la instalación
  de lavado.
- Funciona en un **PC industrial (IPC) con pantalla táctil** en la máquina, y puede **accederse en remoto**
  desde equipos de RhB mediante navegador.
- **Arquitectura dual:** el **PLC** y su control funcionan en **su propio entorno**; el **backend** del
  Supervisor (servidor web) funciona en un **entorno separado** y se comunica con el PLC **a través del
  protocolo ADS de Beckhoff** (control de máquina y supervisión **desacoplados**).
- **Red del IPC (según el plano eléctrico del proyecto):** el IPC dispone de **dos interfaces**:
  - **LAN1** → **red de RhB** (IP, máscara, gateway y DNS **los asigna RhB**). Por aquí van el **acceso de
    los clientes** y la **salida hacia Entra**.
  - **LAN2** → enlace **dedicado y aislado al PLC** (punto a punto, `192.168.1.162/30` ↔ PLC
    `192.168.1.161`, no expuesto).

---

## Capítulo A — Autenticación SSO con Microsoft Entra ID

### A.1 Planteamiento

- **Inicio de sesión vía Entra ID:** los usuarios iniciarán sesión en la aplicación con su identidad de RhB
  (Entra ID), mediante **OpenID Connect** (flujo *Authorization Code + PKCE*). La persona se autentica en la
  **página de Microsoft/Entra** y la aplicación recibe un **token firmado** con su identidad y su rol;
  nosotros **no gestionamos ni almacenamos su contraseña**.
- **Roles:** el Supervisor tiene 5 roles — **Administrador, Mantenimiento, Operador, Visor, Auditor**.
- **Acceso de emergencia (break-glass):** la máquina debe **seguir operando aunque no haya Internet o Entra
  no esté disponible**; por eso el Supervisor conserva **una cuenta de administrador local** que funciona
  **sin conexión**, como **respaldo**.
- **Contraseña:** el usuario **siempre introduce contraseña** (IPC compartido por turnos). No se usan métodos
  sin contraseña.

### A.2 Puntos a confirmar / información requerida de RhB

> Los puntos marcados *«↳ responder en el formulario 8.x»* requieren **respuesta de RhB** (capítulo 8).
> Los marcados *«informativo»* son solo para su conocimiento.

1. *(↳ responder en el formulario 8.1)* **Alcance del inicio de sesión (propuesta — confírmennos su acuerdo):** Entra ID cubrirá **el acceso a la
   aplicación Aquafrisch**. El **inicio de sesión de Windows del IPC funciona en modo kiosko**: el equipo
   arranca con una **cuenta local anónima y sin contraseña** que **lanza directamente la aplicación a pantalla
   completa**, sin escritorio ni acceso al sistema operativo (es el esquema habitual en un panel de máquina;
   proponemos **mantenerlo como hoy**). La identificación de cada persona se realiza **al entrar en la
   aplicación** mediante Entra ID. Los equipos remotos acceden por navegador y se autentican con su cuenta de
   RhB (Entra). **¿De acuerdo con este alcance?**
2. *(↳ responder en el formulario 8.2)* **Conectividad de red (LAN1):** necesitamos confirmar **dos cosas**:
   - **a) Salida del IPC hacia Microsoft Entra** (para el login): conectar con `login.microsoftonline.com`,
     `*.msftauth.net` (y `graph.microsoft.com` si consultamos el perfil). ¿**Directa**, vía **proxy** (díganos
     dirección y autoricen esos dominios) o **aislada**?
   - **b) Entrada de los clientes de RhB al IPC por HTTPS** (el Supervisor es una web que sirve el propio
     IPC): ¿**misma red/VLAN**, **ruteado con firewall** (abrir HTTPS hacia el IPC) o vía **Citrix**?
3. *(↳ responder en el formulario 8.3)* **Registro de la aplicación (App Registration):** **deben registrar la aplicación en su tenant** y
   **facilitarnos (a) Directory (tenant) ID; (b) Application (client) ID; (c) Redirect URIs**. El tipo es
   **SPA** (web en el navegador) que además **expone una API** (backend que valida tokens). **Indíquennos** si
   se accede por **Citrix** para añadir esa URL.
4. *(informativo — sin respuesta)* **Permisos sobre el directorio (mínimos):** solo **`User.Read`** (perfil básico del usuario que entra).
   **No** se piden permisos de escritura ni globales, ni acceso a **recursos de Azure** — solo lectura mínima
   del **directorio de identidades** vía Microsoft Graph.
5. *(↳ responder en el formulario 8.4)* **Grupos/roles de Entra:** **definan los grupos** que representan los 5 roles. **Ideal:** mismos nombres
   que nuestros roles (correspondencia **1:1**); si no, **facilítennos sus nombres/GUID** y los mapeamos.
6. *(↳ responder en el formulario 8.5)* **Pantalla de inicio de sesión (dos posibilidades — díganos cuál):** en ambas, la autenticación final
   (contraseña + MFA) se hace **en la página de Microsoft**, y en ambas queda el **acceso local** discreto.
   **Ninguna requiere permisos adicionales** (solo `User.Read`):
   - **Opción A — Botón «Iniciar sesión con RhB»:** pulsa → en Microsoft teclea **email + contraseña + MFA**.
   - **Opción C — Lista que se rellena con el uso:** la 1ª vez como A; luego su nombre aparece en una lista
     (identificado por el **object ID** inmutable) y solo teclea **contraseña + MFA**. *Rename* → se
     autocorrige al siguiente login; *baja en Entra* → Microsoft lo rechaza y se purga de la lista. La lista
     es solo un atajo; **Entra siempre decide quién entra**.
   - **Nos inclinamos por la opción C** (misma seguridad, menos tecleo).
7. *(↳ responder en el formulario 8.6)* **MFA y pantalla táctil:** la contraseña se mantiene; para reducir el **segundo factor** en un IPC
   compartido, ¿pueden aplicar **Conditional Access** (no re-pedir MFA cada turno en el dispositivo de
   confianza) o **aprobación push** (1 toque) en vez de código tecleado?
8. *(↳ responder en el formulario 8.7)* **Certificado HTTPS:** hoy **self-signed**. Para que Edge no muestre advertencias, hay **dos opciones**:
   **(a)** distribuir **nuestra CA raíz** (Intune/GPO), o **(b)** proporcionarnos un **certificado** de su
   entidad de confianza. ¿Cuál prefieren?
9. *(informativo)* **Protección de interfaces internas:** la **API REST** y el **canal de tiempo real (SignalR)** que usa el
   propio frontend se **protegerán con el token de Entra del usuario** (cumple RhB 2.4.3). Informativo.

---

## Capítulo B — Cumplimiento general (RhB IT Standards, más allá de Entra)

Para su información y, donde corresponda, para que nos indiquen sus preferencias:

- *(informativo)* **Arquitectura/Hosting:** sistema **dual** (PLC y backend en entornos separados). Lo señalamos respecto al
  punto de virtualización de sus estándares; es la arquitectura del producto.
- *(informativo)* **Base de datos:** **SQLite**, base de datos **embebida** (sin servidor aparte ni coste de licencia; **no
  es MS Access**). Informativo.
- *(↳ responder en el formulario 8.8)* **Envío de correos:** servidor SMTP **configurable**; lo apuntaremos a su **Exchange Online**.
  **Necesitamos los datos del relay SMTP** (host, puerto, autenticación). Además, **indíquennos qué contenido
  desean recibir** — p.ej. **qué estadísticas**, **histórico de alarmas**, informes… (los destinatarios se
  configuran después).
- *(↳ responder en el formulario 8.9)* **Red y direccionamiento (LAN1):** dado que **IP/máscara/gateway/DNS de LAN1 los asigna RhB**, necesitamos
  esos **parámetros**, el **nombre de host/DNS** de publicación (influye en el certificado HTTPS) y las
  **reglas de firewall**: **HTTPS entrante** (cliente → IPC) y **saliente** (IPC → Entra).
- *(↳ responder en el formulario 8.11)* **Código fuente:** podemos entregarles el **código fuente del software del PLC**.
- *(↳ responder en el formulario 8.11)* **Antivirus:** según nos indicaron, **lo instalan ustedes**; lo recordamos para coordinarlo y acordar
  exclusiones si hiciera falta.
- *(↳ responder en el formulario 8.10)* **Monitorización:** el Supervisor **puede exponer un endpoint de estado por HTTP** (p. ej. `/health`) que su
  **Zabbix** puede consultar para vigilar, por ejemplo: **disponibilidad del servicio** (200 si operativo),
  **conexión con el PLC**, **estado de la base de datos** y de los **servicios externos** activos (p.ej.
  **Modbus**). Acordaremos qué parámetros incluir.
- *(↳ responder en el formulario 8.10)* **Copias de seguridad:** el Supervisor **genera sus propias copias firmadas** (ZIP: configuración, base de
  datos y modelos) con **restauración y verificación**. Podemos **depositarlas en una carpeta de su red
  (UNC)** para que su **Veeam** las recoja; indíquennos **carpeta y permisos**. Si prefieren respaldar el
  equipo de otra forma, díganos cómo.

---

## Capítulo C — Integración Modbus

### C.1 Contexto

El Supervisor puede integrar **Modbus TCP** con **doble rol** (se habilita por configuración, por proyecto):
- **Servidor / esclavo:** expone datos del PLC (leídos por el Supervisor) como registros Modbus para que
  **otros sistemas de RhB** los consuman.
- **Cliente / maestro:** lee/escribe registros en **dispositivos Modbus externos**.

Para definir la integración **necesitamos que nos indiquen el rol y las variables**.

### C.2 Información requerida de RhB

> *(↳ responder en el formulario 8.12.)* Para las **variables**, el **mapa de registros** y las **alarmas**,
> adjuntamos el **Excel `RhB_Modbus_V1`** (hojas `Modbus_Variables` y `Modbus_Alarms`): **rellénenlo** y
> devuélvanlo junto con el formulario.

1. **Rol del Supervisor:** ¿**servidor/esclavo** (exponemos datos del PLC a un sistema de RhB),
   **cliente/maestro** (leemos/escribimos en dispositivos Modbus externos), o **ambos**?
2. **Interlocutor:** ¿qué sistema de RhB consumirá nuestros datos (si servidor) o a qué **dispositivo(s)** nos
   conectamos (si cliente)? **IP(s) y puerto** (por defecto 502).
3. **Variables / señales:** ¿qué datos hay que intercambiar? Lista con **dirección** (lectura/escritura),
   **tipo** y **unidades/escalado**.
4. **Mapa de registros Modbus:** direcciones (coils, discrete inputs, input/holding registers) — ¿las
   **definen ustedes** o las **proponemos** nosotros?
5. **Alarmas:** ¿hay alarmas a exponer/consumir por Modbus?
6. **Cadencia / tiempo real:** cada cuánto se leen/escriben los datos.
7. **Red:** ¿a través de qué interfaz va el Modbus (LAN1 hacia un sistema de RhB, o una red dedicada)?

---

## 5. Entorno de pruebas (Entra ID)

Nos ofrecieron un entorno de prueba; para desarrollar y validar necesitamos:

> *(↳ responder en el formulario 8.13.)*

- **Tenant ID + Client ID** de un registro **de pruebas**, con **redirect URIs** (desarrollo `http://localhost`
  y equipo de test).
- **Usuarios de prueba** con credenciales, **uno por cada uno de los 5 roles**.
- **Grupos/roles de prueba** asignados a los 5 roles, con el registro **configurado para incluir el grupo/rol
  en el token** (claim *groups* o *app roles*) — para validar el **mapeo de roles**.
- **Tipo de registro y permiso:** **SPA (cliente público, con PKCE)** que expone la **API**, con **`User.Read`**
  consentido. **Sin secreto ni certificado de cliente** (no usamos cliente confidencial).
- **Política MFA / Conditional Access de prueba** (relajada para validar el flujo).
- **Acceso para probar:** ¿desde nuestras instalaciones contra su tenant de pruebas, o **dentro de su red**
  (VPN/Citrix)? ¿Se requiere **acuerdo de confidencialidad**?
- **Especificaciones del equipo/VM** si debe ejecutarse en su infraestructura.
- **Persona de contacto técnico** de RhB IT.

---

## 6. Trazabilidad de requisitos (CRA / IEC 62443 / RhB)

> Mapeo orientativo de las decisiones de diseño a los marcos de cumplimiento (evidencia, no certificación).

| Tema del documento | RhB IT Standards | IEC 62443-4-2 (FR) | EU CRA (Anexo I) |
|---|---|---|---|
| SSO Entra ID / OIDC | 2.4, 7.2 | FR1 – IAC (CR1.1 identificación y autenticación de usuarios) | Autenticación segura |
| MFA | 7.2.2 | FR1 – IAC (CR1.11 autenticación multifactor) | Autenticación reforzada |
| Roles (RBAC, 5 roles) | 2.4.5 | FR2 – UC (CR2.1 aplicación de autorización) | Control de acceso |
| Permisos Graph mínimos | 2.4.9 | FR1 – IAC (mínimo privilegio) | Minimización de acceso |
| Break-glass / disponibilidad | — | FR7 – Resource Availability | Disponibilidad / resiliencia |
| Validación de tokens (API/SignalR) | 2.4.3 | FR1 – IAC (servicios técnicos) | Autenticación de interfaces |
| HTTPS / certificados | 7.2.3 | FR3/FR4 – Integridad/Confidencialidad | Cifrado en tránsito |
| Sin LDAP (solo Entra) | 9.1.4 | FR1 – IAC | — |
| Audit log local | — | FR6 – Timely Response to Events | Registro de eventos |
| SBOM | 2.2.2 | IEC 62443-4-1 (SR/SI) | Anexo I/VII (SBOM, doc. técnica) |
| Modbus (integración) | 9 (interfaces) | FR1/FR3 (autenticación/integridad de interfaces) | Interfaces seguras |

---

## 7. Plan (resumen)

Tras la confirmación de los puntos abiertos (cap. 8) y la provisión del entorno de pruebas (cap. 5):
desarrollo y validación en el entorno de test → despliegue.

---

## 8. Formulario de respuesta de RhB

> **A RhB:** respondan cada punto en su área **«Respuesta de RhB»** (pueden extenderse libremente). Para
> datos largos (App Registration, parámetros de red, variables Modbus) escriban en el área **o adjunten un
> anexo** y referencien aquí su nombre. Al final, completen el **bloque de validación**. Una vez
> cumplimentado y devuelto, este formulario constituye el **registro de acuerdo** y forma parte de la
> evidencia de cumplimiento.

### 8.1 · Alcance del SSO  *(ref. A.2.1)*
**Qué necesitamos:** su **conformidad** con que Entra ID se use **solo para entrar en la aplicación** Aquafrisch, manteniéndose el inicio de sesión de Windows del IPC como hoy (modo kiosko: arranque directo en la aplicación, sin escritorio).
**Respuesta de RhB:**
_(escriba aquí — puede extenderse libremente o adjuntar anexo)_

### 8.2 · Conectividad de red (LAN1)  *(ref. A.2.2)*
**Qué necesitamos:** **(a)** cómo **sale el IPC hacia Entra** — directa a Internet / vía proxy (con su dirección y autorizando los dominios) / aislada; y **(b)** cómo **llegan los clientes al IPC** — misma red o VLAN / a través de firewall (abrir HTTPS) / por Citrix.
**Respuesta de RhB:**
_(escriba aquí)_

### 8.3 · App Registration  *(ref. A.2.3)*
**Qué necesitamos:** tras registrar la aplicación en su tenant, el **Directory (tenant) ID**, el **Application (client) ID** y las **Redirect URIs** autorizadas (incluida la URL de **Citrix** si el acceso será por Citrix).
**Respuesta de RhB:**
_(escriba aquí o adjunte anexo)_

### 8.4 · Grupos / roles de Entra  *(ref. A.2.5)*
**Qué necesitamos:** los **grupos (o app-roles)** de Entra para los 5 roles y **sus nombres** —idealmente iguales a Administrador / Mantenimiento / Operador / Visor / Auditor— o sus **GUID** si usan otra denominación.
**Respuesta de RhB:**
_(escriba aquí o adjunte anexo)_

### 8.5 · Pantalla de inicio de sesión  *(ref. A.2.6)*
**Qué necesitamos:** su **elección** de pantalla de login — **Opción A** (botón «Iniciar sesión con RhB») u **Opción C** (lista de usuarios que se rellena con el uso). Ambas se explican en el cap. A (punto 6); recomendamos **C**.
**Respuesta de RhB:**
_(escriba aquí)_

### 8.6 · MFA / Conditional Access  *(ref. A.2.7)*
**Qué necesitamos:** la **política para el segundo factor (MFA)** en este IPC compartido — **Conditional Access** (no re-pedir MFA cada turno en el dispositivo de confianza) y/o **aprobación push** en el móvil en lugar de código tecleado.
**Respuesta de RhB:**
_(escriba aquí)_

### 8.7 · Certificado HTTPS  *(ref. A.2.8)*
**Qué necesitamos:** su **decisión** sobre el certificado HTTPS — **(a)** distribuir **nuestra CA raíz** en sus equipos (Intune/GPO), o **(b)** **proporcionarnos un certificado** de su entidad de confianza para el nombre del host.
**Respuesta de RhB:**
_(escriba aquí)_

### 8.8 · Correo (SMTP)  *(ref. Cap. B)*
**Qué necesitamos:** los datos del **relay SMTP** de su Exchange Online (**host, puerto y método de autenticación**) y **qué contenido** desean recibir por correo (p.ej. qué **estadísticas**, **histórico de alarmas**, informes).
**Respuesta de RhB:**
_(escriba aquí o adjunte anexo)_

### 8.9 · Red y direccionamiento (LAN1)  *(ref. Cap. B)*
**Qué necesitamos:** los **parámetros de red de LAN1** (IP, máscara, gateway, DNS), el **nombre de host/DNS** con el que se publicará el Supervisor, y las **reglas de firewall** (HTTPS entrante cliente→IPC y saliente IPC→Entra).
**Respuesta de RhB:**
_(escriba aquí o adjunte anexo)_

### 8.10 · Monitorización y backup  *(ref. Cap. B)*
**Qué necesitamos:** **(a)** cómo integrar la **monitorización** con Zabbix y qué parámetros vigilar; y **(b)** para el **backup**, si depositamos nuestras copias en una **carpeta de red (UNC)** para que las recoja Veeam (con **carpeta y permisos**) o prefieren otra forma.
**Respuesta de RhB:**
_(escriba aquí)_

### 8.11 · Código fuente y antivirus  *(ref. Cap. B)*
**Qué necesitamos:** su **confirmación** de que (a) basta con la entrega del **código fuente del software del PLC**, y (b) el **antivirus lo instalan ustedes** (indicando si harán falta **exclusiones**).
**Respuesta de RhB:**
_(escriba aquí)_

### 8.12 · Integración Modbus  *(ref. Cap. C)*
**Qué necesitamos:** la **definición de la integración Modbus** — **rol** del Supervisor (servidor / cliente / ambos), **interlocutor** con su **IP y puerto** (por defecto 502), **cadencia** y **red**. Para las **variables, el mapa de registros y las alarmas**, **rellenen el Excel adjunto `RhB_Modbus_V1`** (hojas `Modbus_Variables` y `Modbus_Alarms`).
**Respuesta de RhB:**
_(escriba aquí; adjunte el Excel `RhB_Modbus_V1` cumplimentado)_

### 8.13 · Entorno de pruebas  *(ref. Cap. 5)*
**Qué necesitamos:** el **entorno de pruebas** — registro/tenant de test, **usuarios de prueba** (uno por rol) y **grupos** de test, el **modo de acceso** (desde nuestras instalaciones o dentro de su red) y la **persona de contacto técnico**.
**Respuesta de RhB:**
_(escriba aquí o adjunte anexo)_

---

### Validación de la respuesta

| Respondido por | Cargo / Departamento | Fecha |
|---|---|---|
|  |  |  |

> Si distintas personas responden distintos apartados, indíquenlo junto a cada respuesta.
