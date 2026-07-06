---
name: entra-id-sso
description: 'Integrar Microsoft Entra ID (Azure AD) como Identity Provider / SSO en el Aquafrisch Supervisor para el cliente Rhätische Bahn (RhB), siguiendo el documento "RhB IT Standards v9.0.4". Es una NUEVA capa de autenticación que COEXISTE con la auth local (JWT + SQLite), gated por un flag de Excel `System Config` (propuesto: `EntraIdEnabled`) exactamente igual que el patrón OPC-UA/Modbus (disabled = se usa solo auth local). Cubre: OIDC/OAuth2 Authorization Code + PKCE en el SPA React (MSAL), validación de tokens Entra en el backend ASP.NET Core (JwtBearer), mapeo de grupos/app-roles Entra → SystemRole, break-glass admin local offline, MS Graph API granular, y el impacto sobre EU CRA (audit, brute-force, password policy). USE WHEN: implementar login SSO Entra, app registration, mapeo de roles Entra, validación de tokens, flag EntraIdEnabled, fallback offline, o discutir/clarificar requisitos RhB de SSO. DO NOT USE FOR: cambiar la auth local existente cuando el flag está OFF, OPC-UA/Modbus, export wizard, ni instalar libs no-Microsoft. ESTADO: EN DISCUSIÓN — no implementar hasta cerrar las "Decisiones abiertas". Trigger phrases: "Entra ID", "Azure AD", "SSO", "single sign on", "RhB IT Standards", "OIDC Aquafrisch", "MSAL", "login corporativo", "identity provider", "EntraIdEnabled".'
argument-hint: 'Indica fase y si discutimos requisitos o implementamos, p.ej. "discutir topología de red RhB" o "fase 1 backend JwtBearer Entra"'
---

# Entra ID / SSO — Implementation Skill (Aquafrisch Supervisor × RhB)

> **ESTADO: 🟡 EN DISCUSIÓN.** Este documento es un **artefacto vivo**. Recoge requisitos, arquitectura
> propuesta, problemáticas y **decisiones aún abiertas**. **No se implementa nada** hasta que las
> decisiones marcadas `❓ ABIERTA` pasen a `✅ CERRADA`. Cada decisión cerrada se anota en la tabla.

## 1. Contexto y tensión central

El **Aquafrisch Supervisor** es un SCADA/HMI **on-premise, self-contained**, que corre en un **IPC
industrial táctil** (pantalla touch, sin teclado físico cómodo) junto a un PLC TwinCAT (ADS). Hoy autentica con un sistema **propio y offline**:

- JWT firmado localmente (`AuthenticationService.GenerateJwtToken`, HMAC-SHA256, secreto en config).
- Usuarios/roles/sesiones en **SQLite por proyecto** (`Users`, `Roles`, `UserRoles`, `UserSessions`, `LoginAttempts`).
- RBAC propio: enum `SystemRole` (`SuperAdmin`, `Administrator`, `Operator`, `Maintenance`, `Viewer`, `Auditor`)
  + `RequireModulePermissionAttribute`. Roles visibles en UI (ES): Administrador, Mantenimiento, Operador, Visor, Auditor. **SuperAdmin oculto al cliente.**
- Endurecido para **EU CRA**: anti-fuerza-bruta (rate limit `auth` 10/min), lockout, política de contraseñas, audit log, cambio de contraseña obligatorio.

El cliente **Rhätische Bahn (RhB)** exige, vía *RhB IT Standards v9.0.4*, lo **opuesto** en materia de identidad:
**SSO contra Microsoft Entra ID** (IDP central en la nube), sin gestión local de usuarios/contraseñas.

> 🌐 **Topología de red confirmada (plano proyecto Landquart, *DREHGESTELL-WASCHHALLE*):** el IPC (Beckhoff
> CP2233) tiene **dos NIC** — **LAN1** → red RhB (IP/máscara/GW/DNS **asignados por RhB**; por aquí va el
> acceso de clientes y la salida a Entra) y **LAN2** → enlace **dedicado y aislado al PLC** (Beckhoff CX7000,
> `192.168.1.161/30` ↔ IPC `192.168.1.162`, *nicht exponiert*). Confirma la **arquitectura dual** y que el
> PLC está fuera del alcance de Entra.

> 🎯 **Tensión central a resolver:** una máquina industrial pensada para operar **aislada y offline**
> debe autenticar contra un **IDP en la nube (Entra)**. Toda la arquitectura depende de **cómo se
> resuelve esto** (red, usuarios, fallback). → ver Decisiones abiertas D1–D4.

> ✅ **D0 — RESUELTO por el cliente: Entra ID es OBLIGATORIO.** RhB confirma que el Supervisor se trata
> como **software accedido por su personal**: además del HMI táctil local, **se accede en remoto desde
> otros clientes RhB** (Cap. 2/3 → **2.4.1 X SSO Entra es Muss**). Ya **no** aplica la vía de exención
> como máquina (Cap. 5). **Lo único que falta aclarar (D0b):** si Entra cubre **solo el login de
> Aquafrisch** o **también el login de Windows** del IPC.

## 2. Principio rector — COEXISTENCIA gated (no reemplazo)

RhB es **un** cliente/proyecto. Otros proyectos (multi-proyecto) **no** tienen Entra. Por tanto Entra
**no reemplaza** la auth local: se añade como **modo opt-in por proyecto**, gated por Excel
`System Config`, **idéntico al patrón OPC-UA / Modbus** del repo:

| Flag Excel (propuesto) | OFF (default) | ON |
|---|---|---|
| `EntraIdEnabled` | Solo auth local JWT/SQLite (como hoy). El servicio Entra se registra como stub `DisabledEntraIdService`. | **Login Entra (OIDC)** — botón «Iniciar sesión con RhB» (A) o lista que se rellena con el uso (C) según D13 — + enlace discreto **«Acceso local»** (cuentas locales, break-glass offline, D4). Solo `User.Read`. |

- Mantener la convención del repo: si `EntraIdEnabled=FALSE`, **el servicio Entra no existe** (stub deshabilitado, sin dependencias activas).
- El frontend muestra el botón Entra **solo** cuando el backend reporta `entraIdEnabled=true` (vía `SystemFeaturesController`, igual que `enableFileExport`).

## 3. Requisitos RhB relevantes (mapeo al documento)

Capítulos del *RhB IT Standards v9.0.4* que aplican (la app es Web on-prem + interfaces). `X` = Muss (obligatorio).

| Ref | Requisito | Impacto en Aquafrisch |
|---|---|---|
| **2.4.1 X** | SSO como **Auto-Login** (no segundo login tras login de Windows) | Cliente remoto RhB → **SSO silencioso best-effort** (`ssoSilent`, no garantizado). IPC kiosk local → login en la app. Alcance Windows = D0b |
| **2.4.2** | IDP = **MS Entra ID**. Estándares: **OAuth 2.0, OpenID Connect, SAML 2.0** | Elegimos **OIDC (Auth Code + PKCE)** → D5 |
| **2.4.3 X** | Servicios técnicos (APIs, colas) también protegidos por el IDP | SignalR hub + API: validar tokens Entra → D7 |
| **2.4.4** | Info extra vía **MS Graph API** | Solo si claims del token no bastan (grupos/roles) |
| **2.4.5** | Concepto **RBAC** | Ya existe; mapear Entra → `SystemRole` → D6 |
| **2.4.6** | Ejecutar sin derechos de admin tras instalar | Ya cumplido (self-contained) |
| **2.4.9 X** | Permisos Graph **granulares** (sin permisos globales de escritura) | Solo **`User.Read`** (opciones A/C; B descartada, sin `GroupMember.Read.All`) |
| **2.4.10** | SSO en SO no-Windows: coordinar con RhB IT | El IPC es Windows; N/A salvo Citrix |
| **3.6.1.1 X** | Web app sin plug-ins cliente | Ya cumplido (React) |
| **3.6.1.2 X** | Web app soporta SSO del usuario logueado | Núcleo de esta skill |
| **3.6.1.4** | PWA configurable | Fuera de alcance inicial |
| **7.2.2 X** | SSO vía **Entra ID con MFA**, usuarios **y service accounts** | MFA en IPC compartido es problemático → D8 |
| **7.2.3 X** | Acceso cliente vía **HTTPS (cert público)** | Hoy es cert self-signed → D9 |
| **9.1.2** | Datos de usuario desde Azure AD vía **token → SCIM → Graph** (en ese orden de preferencia) | Preferir **claims del token** |
| **9.1.4 X** | **LDAP prohibido** | No usar LDAP/AD clásico; solo Entra moderno |
| **2.1.7 X** | Email on-prem vía servidor RhB (Exchange Online) | Afecta al export wizard, no al SSO directamente |
| **2.2 / SBOM** | Entregar SBOM con cada versión; documentar en Confluence | Añadir libs MS al SBOM → D10 |

Plataformas RhB fijadas (Anhang A): IDP = **MS Entra ID** · OS cliente = Windows 10/11 64-bit o Citrix
XenDesktop · Browser = **MS Edge** · Verteilsystem = Intune · GIT = Bitbucket · Doku = Confluence.

## 4. Arquitectura propuesta (borrador, sujeta a D1–D10)

```
┌─────────────────────────── IPC Industrial (zona OT RhB) ───────────────────────────┐
│                                                                                     │
│  React SPA (MS Edge)                         ASP.NET Core Backend (self-contained)  │
│  ┌───────────────────────┐                   ┌───────────────────────────────────┐  │
│  │ Login.js              │                   │ AuthController (local, hoy)       │  │
│  │  ├ usuario local ─────│── /api/auth/login─│  + AuthenticationService (JWT)    │  │
│  │  └ usuario Entra ─────┼─┐  (selector D13) │                                   │  │
│  │     (MSAL.js, gated)  │ │  OIDC Auth Code │ EntraAuthController (NUEVO, gated) │  │
│  └───────────────────────┘ │  + PKCE         │  ├ valida token Entra (JwtBearer) │  │
│                            │                 │  ├ mapea grupos/app-roles→SystemRole│ │
│                            │                 │  └ emite sesión interna / claims  │  │
│                            ▼                 └──────────────┬────────────────────┘  │
│                   ¿Proxy RhB? (D1)                          │ break-glass local (D4)│
└────────────────────────────┼───────────────────────────────┼───────────────────────┘
                             ▼ (HTTPS, si hay salida)
                 ┌──────────────────────────┐
                 │  Microsoft Entra ID (nube)│  login.microsoftonline.com
                 │  App Registration RhB     │  (tenant RhB)
                 │  - Client ID / Tenant ID  │
                 │  - Redirect URI (SPA)     │
                 │  - App roles / grupos     │
                 │  - Scopes Graph mínimos   │
                 └──────────────────────────┘
```

Flujo OIDC propuesto (D5): **Authorization Code Flow + PKCE** (estándar SPA + API).
- Frontend: **MSAL.js** (`@azure/msal-browser` / `@azure/msal-react`) obtiene access token Entra.
- Backend: **Microsoft.Identity.Web** / `AddJwtBearer` con authority del tenant RhB valida el token.
- Mapeo de roles: claims `roles` (app roles) o `groups` del token → `SystemRole` (D6). Graph solo si falta info.

> 🔑 **Ámbito acordado (Fase 0):** Entra ID es **obligatorio** (cliente confirmó) y gobierna al menos el
> **acceso a la app Aquafrisch**. Hay **dos escenarios de acceso**:
> - **Cliente remoto RhB** (PC del empleado, Windows iniciado con su cuenta Entra): intentaremos **SSO
>   silencioso** (`ssoSilent`) reutilizando su sesión Entra → **best-effort, no garantizado**; si no es
>   posible, se muestra la pantalla de login.
> - **HMI local del IPC** (cuenta Windows kiosk genérica): la persona se identifica **en la app** (login
>   A botón o C lista, D13).
>
> **Pendiente (D0b):** confirmar con RhB si Entra debe cubrir **también el login de Windows** del IPC
> (Entra-join + login personal) o si el SO sigue con cuentas locales kiosk. Además, usuarios **Entra y
> locales COEXISTEN**: un admin local entra **también sin conexión a Entra** (resiliencia offline).

## 5. Decisiones abiertas (cerrar antes de implementar)

| # | Decisión | Estado | Notas / opciones |
|---|---|---|---|
| **D0** | **¿Entra obligatorio?** | ✅ CERRADA (cliente) | **SÍ, obligatorio.** RhB accede **también en remoto** desde otros clientes (no solo HMI local) ⇒ software Cap. 2/3 ⇒ 2.4.1 X Muss. La exención como máquina (Cap. 5) **no aplica**. |
| **D0b** | **Alcance de Entra**: proponemos **solo login de la app** (Windows del IPC sigue kiosko); pedir **acuerdo** a RhB | 📧 PARA RHB (propuesta) | Reformulado: en el correo se plantea como **propuesta + «¿de acuerdo?»**, no pregunta abierta. Si RhB quisiera también Windows ⇒ Entra-join del IPC + login personal. |
| **D1** | **Topología de red**: salida LAN1 a Entra y entrada de clientes HTTPS | 📧 PARA RHB | **Plano del proyecto (Landquart) confirma dual-NIC:** IPC con **LAN1** a red RhB (IP/máscara/GW/DNS **asignados por RhB**) y **LAN2** `/30` **dedicada y aislada al PLC** (`192.168.1.162` ↔ PLC `192.168.1.161`, no expuesto). Falta confirmar: salida a Entra (directo/proxy/aislado), parámetros LAN1, hostname, **firewall HTTPS in/out**, y cómo conectan los clientes (misma red/VLAN/Citrix). |
| **D2** | **¿Quién hace login (OS) en el IPC local?** | 🟡 DEPENDE DE D0b | Propuesta: HMI local mantiene cuentas Windows kiosk/admin/advanced; Entra en la app. Pero si D0b = "también Windows", el SO del IPC pasaría a Entra. |
| **D3** | **Auto-Login vs login interactivo** | ✅ CERRADA (matizada) | **Cliente remoto RhB:** intentaremos **SSO silencioso** (MSAL `ssoSilent`) reutilizando su sesión Entra — **best-effort, no garantizado** (1ª vez / MFA / config) → si falla, se muestra la pantalla de login. **HMI local kiosk:** login en la app (A botón o C lista, D13). |
| **D4** | **Coexistencia / fallback offline** | ✅ CERRADA | **Usuarios Entra y locales COEXISTEN.** Un admin local de Aquafrisch entra **también sin conexión a Entra**. (Pendiente confirmar matices con RhB.) |
| **D5** | **Protocolo**: OIDC vs SAML | ✅ CERRADA (técnica) | **OIDC (Auth Code + PKCE)**. SAML solo si RhB lo exige expresamente. |
| **D6** | **Mapeo de roles** Entra → `SystemRole` | ✅ CERRADA (dirección) | **Aquafrisch es autoritativo.** **Ideal:** RhB crea los grupos con los **mismos nombres** que los 5 roles (Admin/Manten./Operador/Visor/Auditor) → correspondencia **1:1**. **Plan B:** RhB da nombres/GUID y **mapeamos** nosotros. Pedido en el correo (punto 5). |
| **D7** | **Servicios técnicos** (API REST + SignalR) bajo Entra (2.4.3 X) | ✅ CERRADA (técnica) | Protegeremos la API y el hub `/hubs/scada` con el **token de Entra del usuario** (mismo del login) → cumple 2.4.3. **Service accounts / client-credentials = FUERA de alcance** (no hay consumo M2M; se añadiría solo si surge una integración futura). |
| **D8** | **MFA** en IPC compartido (7.2.2) | 📧 PARA RHB | **Contraseña obligatoria (cliente); passwordless descartado** (kiosk compartido). Para reducir fricción en táctil: Conditional Access / device trust para **no re-pedir MFA cada turno** en el dispositivo de confianza, o **MFA push** (1 toque en el móvil) en vez de código tecleado. Lo define RhB IT. |
| **D9** | **HTTPS cert público** (7.2.3) vs self-signed actual | 📧 PARA RHB | ¿RhB provee cert? ¿Acceso solo LAN? Afecta despliegue. |
| **D10** | **Librerías nuevas** (MSAL, Microsoft.Identity.Web) y SBOM/CRA | ✅ CERRADA (recomendado) | **Permitido** (1st-party MS, MIT). Declarar en SBOM. |
| **D11** | **App Registration**: la crea RhB IT en su tenant y nos pasa Tenant/Client ID + redirect URIs | 📧 PARA RHB | Input imprescindible del cliente. Va en el correo. |
| **D12** | **Alcance multi-proyecto**: `EntraIdEnabled` por-proyecto, default OFF | ✅ CERRADA | **Software único** para todas las máquinas; cada feature se habilita por Excel por proyecto. `EntraIdEnabled=FALSE` ⇒ Entra **completamente deshabilitado** y 100% retrocompatible con instalaciones antiguas. |
| **D13** | **UX de login**: A (botón) vs C (lista que se rellena con el uso) | 📧 PARA RHB (recomendamos **C**) | Se exponen **A y C** (ambas **solo `User.Read`**, sin `GroupMember.Read.All`). **A) botón** → teclea email+pass+MFA en Microsoft. **C) lista** que se llena tras el 1er login (key = `object ID` inmutable; rename autocorrige; baja → Entra rechaza + purga). **Misma seguridad** (Entra siempre manda), C más cómoda en táctil. En ambas, **acceso local** discreto (break-glass, D4). **B (lista de TODOS vía Graph) DESCARTADA** (exigía `GroupMember.Read.All` + identidad de app + caché). |

## 6. Problemáticas anticipadas (riesgos)

1. **Air-gap vs nube (P-D1).** Si la zona OT bloquea Internet salvo proxy explícito, hay que abrir
   `login.microsoftonline.com` + `*.msftauth.net` + Graph en el proxy RhB, o el SSO no funciona.
   Air-gap puro ⇒ OIDC interactivo inviable ⇒ replantear (¿broker on-prem? ¿solo break-glass?).
2. **Disponibilidad / safety.** El IPC controla túneles de lavado. Si Entra/Internet cae, **no puede**
   quedar nadie sin poder operar. Obliga a break-glass local (D4) + cache de token. Contradice "SSO puro".
3. **Auto-Login (2.4.1) — best-effort, no garantizado.** Para **clientes remotos RhB** intentaremos **SSO
   silencioso** (`ssoSilent`) reutilizando su sesión Entra; **funciona si el navegador/dispositivo tiene
   sesión Entra usable**, pero **no es silencioso** la 1ª vez ni cuando MFA/Conditional Access exige
   interacción → fallback a la pantalla de login. **No prometerlo categóricamente al cliente.** Validar en test.
4. **MFA en planta.** Pedir MFA en cada cambio de turno es inviable operativamente (D8).
5. **Roles: Aquafrisch autoritativo (acordado).** RhB mapea sus grupos Entra a los 5 roles de Aquafrisch.
   El reto técnico es traducir el claim `roles`/`groups` del token al `SystemRole` correcto y mantener
   la coexistencia con los roles locales cuando el usuario entra en modo local (offline).
6. **EU CRA solapado.** Con SSO ON, brute-force/lockout/password-policy pasan a ser de Entra; pero
   **audit log local debe seguir** registrando quién entró (trazabilidad on-prem). Definir qué se mantiene.
7. **Tokens Entra ≠ JWT local.** El backend hoy valida su propio JWT. Habrá **dos validadores**
   (JwtBearer Entra + validación local). Cuidado con `[Authorize]` y el pipeline de claims/roles.
8. **Renovación de tokens offline.** Refresh contra Entra requiere red. Definir TTL de sesión interna
   tras login Entra para no exigir red continua.
9. **SBOM/licencias.** MSAL y Microsoft.Identity.Web son MIT/1st-party MS, pero hay que añadirlas al
   SBOM y a la doc CRA (D10).
10. **Citrix/PWA.** Si RhB usa Citrix XenDesktop para acceder, el flujo de redirect/redirect URI cambia.
11. **Lista de usuarios Entra en el login.** Opción **B** (listar TODOS desde Graph) **DESCARTADA**: exigiría
    `GroupMember.Read.All` (application) + identidad de app + caché offline. La elegida **C** construye la
    lista con los usuarios que **ya han entrado** (datos del propio token; key = `object ID` inmutable;
    rename autocorrige; baja → Entra rechaza + purga) → **sin permisos extra, riesgo bajo**.
12. **HMI local táctil (sin teclado cómodo).** En el IPC de planta se escribe sobre pantalla **táctil**:
    teclear email + contraseña + MFA por turno es lento e incómodo. **La contraseña es obligatoria
    (decisión del cliente)** — es lo que garantiza la identidad en un kiosk compartido; **passwordless se
    descarta** (Windows Hello/FIDO2 encajan mal en un kiosk anónimo compartido). Mitigación (no eliminación):
    **minimizar tecleo** (pick-from-list + `login_hint` para no escribir el email), **teclado en pantalla**,
    y para el **2º factor** — distinto de la contraseña — **Conditional Access** (no re-pedir MFA cada turno
    en el dispositivo de confianza) o **MFA push** (1 toque) en vez de código tecleado. Afecta a D8 y D13.
13. **Navegador kiosko → página de Microsoft (crítico, probar en test).** En **las 3 opciones** A/B/C el flujo
    OIDC **redirige siempre a la página de Microsoft** (ahí se teclea contraseña + MFA). No usamos ROPC
    porque **no soporta MFA** (RhB lo exige). Por tanto, el **navegador en modo kiosko** debe: (a) poder
    **navegar a los dominios de Entra** (`login.microsoftonline.com`, `*.msftauth.net` — mismos del allowlist
    del punto 2), (b) usar el flujo de **redirección** (no *popup*, que el kiosko suele bloquear), y (c)
    permitir la **redirect URI** de la app. **Verificar en el entorno de pruebas.**

## 7. Plan de fases (propuesto, se ajusta tras D1–D12)

- **Fase 0 — Clarificación (AHORA):** cerrar D1–D12 con RhB/usuario. Sin código.
- **Fase 1 — Backend gated + stub:** flag `EntraIdEnabled`, `IEntraIdService` + `DisabledEntraIdService`,
  `SystemFeaturesController` expone `entraIdEnabled`, DI condicional en `Program.cs` (patrón OPC-UA).
- **Fase 2 — Validación de token Entra:** `AddJwtBearer` con authority RhB, `EntraAuthController`,
  emisión de sesión interna; break-glass local intacto.
- **Fase 3 — Frontend MSAL:** login Entra en `Login.js` (gated) — **botón «Iniciar sesión con RhB»** (A) o
  **lista «recientes»** que se rellena tras cada login con `login_hint` (C), según D13 — + acceso local
  discreto, Auth Code+PKCE (redirect), persistir usuarios vistos por **`object ID`**, integrar con `PermissionsContext`.
- **Fase 4 — Mapeo de roles:** grupos/app-roles Entra → `SystemRole`; Graph mínimo si hace falta.
- **Fase 5 — Endurecimiento CRA + docs:** audit log, SBOM, manual de instalación Entra (parámetros), Confluence.

## 8. Acuerdos cerrados (Fase 0)

1. **Coexistencia gated.** Entra es **opt-in por proyecto** vía Excel `System Config → EntraIdEnabled`
   (default **OFF**). OFF ⇒ Entra completamente deshabilitado, comportamiento **idéntico al actual** y
   retrocompatible con todas las instalaciones antiguas. Filosofía: **un único software** para todas las
   máquinas, funcionalidades habilitadas por Excel por proyecto.
2. **Entra obligatorio (cliente).** RhB accede **también en remoto** desde otros clientes ⇒ Entra es Muss.
   Cubre al menos el **acceso a la app Aquafrisch**. **Pendiente D0b:** si además cubre el login de Windows.
3. **Coexistencia de usuarios.** Con `EntraIdEnabled=ON` conviven **usuarios Entra + usuarios locales**.
   Un admin local de Aquafrisch puede entrar **también sin conexión a Entra** (resiliencia offline).
4. **Protocolo = OIDC** (Authorization Code + PKCE) con MSAL (frontend) + Microsoft.Identity.Web (backend).
5. **Roles: Aquafrisch autoritativo.** RhB **mapea sus grupos/roles Entra a los 5 roles de Aquafrisch**.
6. **Libs MS permitidas** (MSAL, Microsoft.Identity.Web), declaradas en SBOM.
7. **UX de login (A vs C → confirmar RhB, D13).** Se exponen a RhB **A) botón** «Iniciar sesión con RhB» y
   **C) lista que se rellena con el uso** (ambas **solo `User.Read`**; **B descartada** por requerir
   `GroupMember.Read.All`). **Nos inclinamos por C** (comodidad táctil, misma seguridad). En ambas, **acceso
   local** discreto para break-glass.
8. **Contraseña obligatoria (cliente).** El usuario **siempre introduce contraseña** (garantiza la identidad
   en el kiosk compartido). **Passwordless descartado.** La fricción del táctil se **mitiga** (login_hint,
   teclado en pantalla, MFA push/Conditional Access para el 2º factor), **no se elimina**.

## 9. Documento entregable y envío (✅ ENVIADO a Walter 2026-06-27)

> 📄 **Documento entregable (deliverable):** [`documento-integracion-rhb.md`](./documento-integracion-rhb.md)
> (ES, fuente) y **[`documento-integracion-rhb-bilingue.md`](./documento-integracion-rhb-bilingue.md)** (ES/DE,
> frase a frase — **es el que se envía a RhB**) — especificación formal con 3 capítulos (**A** Entra ID SSO ·
> **B** Cumplimiento · **C** Modbus) + **trazabilidad CRA / IEC 62443** + **formulario de respuesta** (cap. 8,
> bloques 8.1–8.13 que RhB rellena = evidencia/registro de acuerdo). **Word generado** (pandoc):
> `Especificacion_Integracion_Aquafrisch_RhB.docx`.
>
> 📧 **Flujo de envío (✅ enviado 2026-06-27):** Walter (especialista en alemán) — ver
> [`correo-walter-revision-aleman.md`](./correo-walter-revision-aleman.md) — **revisa el alemán y lo reenvía
> él directamente a RhB** (con nosotros en CC). Adjuntos: el **Word** + el **Excel `RhB_Modbus_V1`** (hojas
> `Modbus_Variables` / `Modbus_Alarms`, que RhB rellena). El **markdown del DMS es el maestro**; Word/PDF son
> copias regenerables.
>
> 📨 La **nota de transmisión** (correo corto) + el **seguimiento de respuestas de RhB** + bitácora viven en
> [`correo-rhb.md`](./correo-rhb.md). Cuando RhB responda, volcar cada respuesta en su tabla de seguimiento
> y cerrar la decisión correspondiente aquí.
>
> 📅 **Preparación de la reunión KW29** (confirmar lo ya preguntado + matices a rematar + deberes nuestros):
> [`reunion-kw29-preparacion.md`](./reunion-kw29-preparacion.md).
>
> 📋 **Cumplimiento RhB más allá de Entra:** ver [`gap-analysis-rhb.md`](./gap-analysis-rhb.md) — brechas
> del Supervisor frente a TODO el *RhB IT Standards v9.0.4* (hosting bare-metal, SQLite vs MS-SQL, email
> Exchange, hostnames, Zabbix, Veeam, antivirus, código en Bitbucket, etc.).

Lo que necesitamos / debemos declarar al cliente para cerrar D0b, D1, D8, D9, D11 y D13:

> ⚠️ La **redacción definitiva** de estos puntos (y su estado) vive en [`correo-rhb.md`](./correo-rhb.md)
> §3 (Entra), §4 (entorno de pruebas) y §5 (cumplimiento) — **fuente de verdad**. Este resumen solo lista
> qué queda pendiente de RhB para no duplicar texto.

- **Pendiente de RhB:** **D0b** (¿Entra también en el login de Windows del IPC?), **D1** (red/topología LAN1:
  salida a Entra + entrada de clientes + proxy/firewall), **D11** (App Registration: Tenant/Client ID +
  redirect URIs, Citrix si aplica), **D6** (grupos/roles Entra — ideal **1:1** con los 5 roles), **D8**
  (MFA/Conditional Access — reducir 2º factor en táctil), **D9** (cert HTTPS: hoy self-signed → CA raíz o
  cert RhB), **D13** (UX login: **A botón** vs **C lista que se rellena** — recomendamos C, solo `User.Read`).
- **Informativo / cerrado:** D7 (API+SignalR con token de Entra del usuario, 2.4.3), SQLite (no Access),
  email vía relay Exchange (2.1.7 X), hosting dual, código del PLC, antivirus (lo instala RhB), Zabbix/Veeam.
- **Auto-Login (2.4.1 X):** SSO silencioso **best-effort** para clientes remotos (no garantizado); no se
  promete categóricamente en el correo.

### Entorno de pruebas — lo que necesitamos que RhB nos provea

RhB ya nos ofreció un **entorno de prueba**; hay que **pedirlo formalmente** y detallar qué necesitamos
para poder desarrollar y validar la integración Entra (Cap. 10.1 Test/Integration):

- **Tenant / App Registration de TEST:** `Tenant ID` + `Client ID` de un registro de pruebas, con
  **redirect URIs** para nuestro entorno de desarrollo (`http://localhost:*`) y para el host/IPC de test.
- **Usuarios de prueba:** varias cuentas Entra de test **con credenciales utilizables**, **una por cada uno
  de los 5 roles** (Administrador, Mantenimiento, Operador, Visor, Auditor).
- **Grupos / app-roles de prueba:** ya creados y **mapeados a los 5 roles**, con los usuarios de test asignados.
- **Secreto o certificado de cliente** (si se usa Graph / cliente confidencial) + **admin consent** de los
  permisos Graph mínimos (`User.Read`, opcional `GroupMember.Read.All`).
- **Política MFA / Conditional Access de test** aplicable a esas cuentas (idealmente **relajada** para poder
  probar el flujo completo sin bloqueos).
- **Acceso de red para probar:** ¿podemos probar **desde nuestras instalaciones** contra su tenant de test
  (lo más simple), o debe hacerse **dentro de su red**? Si es dentro: vía de acceso (**VPN / Citrix**, Cap. 10.3)
  y, en su caso, firma de **Vertraulichkeitserklärung** (Cap. 10.3.3 / 10.4.2).
- **VM / entorno de test** si la prueba debe correr en su infraestructura (Cap. 10.1): specs (Windows
  Server/Client, ¿IPC táctil de test?).
- **Contacto técnico de RhB IT** para la integración (dudas de configuración, consentimientos, redirect URIs).

## 10. Anclas de código (estado actual, para Fase 1+)

- Auth local: `Services/AuthenticationService.cs` (`LoginAsync`, `GenerateJwtToken`, `ValidateTokenAsync`).
- Controller: `Controllers/AuthController.cs` (`/api/auth/login|refresh|validate|change-password`).
- Modelos: `Models/AuthModels.cs` (`User`, `Role`, `UserRole`, `UserSession`, `LoginAttempt`, `LoginRequest/Response`).
- RBAC: `Models` → enum `SystemRole`; `Authorization/RequireModulePermissionAttribute.cs`.
- Flags de features: `Controllers/SystemFeaturesController.cs` (patrón `enableFileExport` → replicar `entraIdEnabled`).
- DI gated (patrón a imitar): registro condicional OPC-UA en `Program.cs` (`OpcUaEnabled` → real vs `DisabledOpcUaServerService`).
- Frontend: `src/components/Login.js`, `src/services/api.js`, `src/contexts/PermissionsContext`.

## 11. Reglas de la skill

- **No tocar la auth local** cuando `EntraIdEnabled=OFF`. Es el modo default de todos los proyectos no-RhB.
- **No usar LDAP** (9.1.4 X). Solo Entra moderno (OIDC/OAuth2).
- **Permisos Graph mínimos** (2.4.9 X). Nunca permisos globales de escritura sobre el tenant.
- **Break-glass siempre** salvo que el usuario decida lo contrario (disponibilidad de máquina crítica).
- **Mantener audit log local** aunque el login venga de Entra.
- **Declarar en SBOM** toda lib añadida (MSAL, Microsoft.Identity.Web) — CRA.
- Mientras `ESTADO: EN DISCUSIÓN`, **no generar código de producción**; solo prototipos si el usuario lo pide explícitamente.
