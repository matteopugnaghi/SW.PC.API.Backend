# Comunicación con RhB IT — Integración Entra ID (Aquafrisch Supervisor)

> **Hub de comunicación con RhB** para la integración Entra ID. Aquí viven: (1) el correo
> (borrador ES → versión final DE), (2) el **seguimiento de las respuestas** de RhB mapeadas a las
> decisiones `D0b`–`D13`, y (3) la **bitácora cronológica** del intercambio hasta la implementación.
> Documento hermano de [`SKILL.md`](./SKILL.md) (arquitectura y decisiones).

## Estado del correo

| Campo | Valor |
|---|---|
| Entregable | Documento **bilingüe ES/DE** → `Especificacion_Integracion_Aquafrisch_RhB.docx` + Excel `RhB_Modbus_V1` |
| Versión alemana | ✅ hecha (frase ES / frase DE); la revisa **Walter** antes de llegar a RhB |
| Estado | 📤 **ENVIADO a Walter (2026-06-27)** — revisa el alemán y **lo reenvía él a RhB** (nosotros en CC); ⏳ esperando respuesta |
| Fecha de envío | 2026-06-27 (a Walter) |
| Destinatario | RhB IT (vía Walter) |

---

## 1. Correo de transmisión — borrador (ES)

> El detalle técnico vive ahora en el **documento** [`documento-integracion-rhb.md`](./documento-integracion-rhb.md).
> Este correo es solo la **nota de transmisión** que lo acompaña.

**Asunto:** Aquafrisch Supervisor — Especificación de integración con RhB IT (Entra ID, cumplimiento y Modbus)

Estimados [nombre / equipo de RhB IT]:

Estamos adaptando el **Aquafrisch Supervisor** a sus *RhB IT Standards v9.0.4*. Adjuntamos la
**Especificación de Integración**, que cubre: **(A)** la autenticación **SSO con Microsoft Entra ID**,
**(B)** los **puntos de cumplimiento** de sus estándares (hosting, base de datos, correo, red, backup,
monitorización) y **(C)** la **integración Modbus**.

Para avanzar, precisamos su **confirmación / información** en los **puntos abiertos** consolidados en el
**capítulo 8** del documento, así como el **entorno de pruebas** (capítulo 5). En particular: alcance del SSO,
conectividad de red, registro de la aplicación en su tenant, grupos/roles, MFA, certificado HTTPS, datos de
correo (relay y contenido), parámetros de red, monitorización/backup y **el rol y las variables Modbus**.

Quedamos a su disposición para cualquier aclaración. Muchas gracias.

Un cordial saludo,
[Nombre] · [Empresa / Aquafrisch]

---

## 2. Versión final (DE) — pendiente

> Se traducirá aquí cuando el borrador ES esté aprobado.

---

## 3. Seguimiento de respuestas de RhB

> Cuando RhB responda, copiar su respuesta en la columna correspondiente y actualizar el estado.
> Estados: ⏳ pendiente · ✅ respondido · ❓ ambiguo (re-preguntar).

| Punto correo | Decisión | Qué preguntamos / informamos | Respuesta de RhB | Estado |
|---|---|---|---|---|
| 3.1 | **D0b** | Propuesta: Entra **solo app** (Windows IPC = kiosko); ¿de acuerdo? | — | ⏳ |
| 3.2 | **D1** | Conectividad LAN1: **salida** a Entra + **entrada** clientes HTTPS (directo/proxy/aislado; misma red/VLAN/Citrix) | — | ⏳ |
| 3.3 | **D11** | App Registration (Tenant/Client ID, redirect URIs) | — | ⏳ |
| 3.4 | **Graph** | Solo **`User.Read`** (ni A ni C requieren más; B descartada) | — | ⏳ |
| 3.5 | **D6** | **Ideal**: grupos con los **mismos nombres** que los 5 roles (1:1); si no, nombres/GUID y mapeamos | — | ⏳ |
| 3.6 | **D13** | UX login: **A botón** vs **C lista que se rellena** (nos inclinamos por C) — RhB confirma | — | ⏳ |
| 3.7 | **D8** | Conditional Access / MFA push (táctil) | — | ⏳ |
| 3.8 | **D9** | Certificado: self-signed + CA raíz vs cert RhB | — | ⏳ |
| 3.9 | **D7** | API + SignalR protegidos con el token de Entra del usuario (2.4.3) — informativo; service accounts fuera de alcance | — | ✅ |
| 4 | **Entorno test** | Tenant/usuarios/grupos/red/VM/contacto | — | ⏳ |
| 5·Hosting | — | Arquitectura dual (PLC/​backend separados) — info | — | ⏳ |
| 5·BD | — | SQLite embebida (no Access) — info | — | ⏳ |
| 5·Email | — | **Datos del relay SMTP** Exchange Online | — | ⏳ |
| 5·Red/DNS | — | Parámetros LAN1 (IP/máscara/GW/DNS), hostname, reglas firewall (HTTPS in/out), cómo conectan clientes | — | ⏳ |
| 5·Código | — | Ofrecemos solo **código del SW del PLC** | — | ⏳ |
| 5·Antivirus | — | Lo instalan ellos (recordatorio/coordinar) | — | ⏳ |
| 5·Monitor/Backup | — | Integración Zabbix/Veeam y quién opera | — | ⏳ |
| C·Modbus rol | — | ¿**Servidor / cliente / ambos**? | — | ⏳ |
| C·Modbus datos | — | Interlocutor/IP, **variables**, mapa de registros, alarmas, cadencia, red | — | ⏳ |

---

## 4. Bitácora cronológica

| Fecha | Evento |
|---|---|
| 2026-06-26 | Borrador del correo creado (ES). Pendiente de revisión interna y traducción a DE. |
| 2026-06-26 | Correo **reescrito** para mayor claridad (contexto + explicación de break-glass y OIDC) e **integrados** los puntos de cumplimiento no-Entra (sección 5: hosting dual, SQLite, relay SMTP, IP/DNS, código del PLC, antivirus, Zabbix/Veeam). Un solo correo. |
| 2026-06-26 | Punto 1 reformulado como **propuesta** (Entra solo app, Windows kiosko) + petición de acuerdo. Puntos 2–9 **ampliados** con más detalle y precisión. |
| 2026-06-26 | Integrada la **topología de red** del plano (proyecto Landquart): IPC dual-NIC — **LAN1** a red RhB (IP/DNS asignados por RhB), **LAN2** /30 dedicada y aislada al PLC. Punto 2 ahora cubre salida a Entra **y** entrada de clientes; sección 5 pide parámetros LAN1 + firewall. |
| 2026-06-26 | Punto 4 (permisos): añadida aclaración de que son **permisos de Microsoft Graph** (lectura del directorio), **no** acceso a **recursos de Azure** (RBAC). |
| 2026-06-26 | Punto 5 (roles): reformulado — pedir como **ideal** que RhB nombre los grupos **igual** que los 5 roles (1:1); plan B = nombres/GUID y mapeamos nosotros. |
| 2026-06-26 | Punto 6 (login): aclarado que en **A/B/C** la autenticación final es siempre en la página de Microsoft; añadida referencia a **captura adjunta**. ⚠️ **Pendiente adjuntar** screenshot de la pantalla de login antes de enviar. |
| 2026-06-26 | **Decisión: opción A** (botón «Iniciar sesión con RhB» + enlace discreto «Acceso local»). Se descartan B/C. ⇒ permisos simplificados a **solo `User.Read`** (puntos 2, 4 y 6 ajustados). |
| 2026-06-26 | **Reconsiderado:** exponer a RhB **A y B** y que confirmen (B = elegir nombre de lista, más cómodo en táctil, requiere `GroupMember.Read.All`; A = botón, solo `User.Read`). **Nos inclinamos por B.** Puntos 2/4/6 y tablas revertidos a esta versión. |
| 2026-06-26 | **Cambio a A vs C** (B descartada por riesgo/permiso). C = lista que se **rellena con el uso** (1ª vez como A; luego solo contraseña; key `object ID`; rename autocorrige; baja → Entra rechaza + purga). **Ambas solo `User.Read`.** Recomendamos **C**. Punto 6 reescrito paso a paso con casuísticas. |
| 2026-06-26 | Punto 9 **simplificado**: se mantiene proteger API+SignalR con el **token de Entra del usuario** (cumple 2.4.3 X, informativo) y se **quita** la parte especulativa de **service accounts / client-credentials** (no hay consumo M2M; fuera de alcance). |
| 2026-06-26 | Punto 1: **quitada** la promesa de «inicio de sesión automático sin doble login» para clientes remotos (SSO silencioso es **best-effort**, no garantizable). Skill suavizada (D3, problemática 3, callout, fila 2.4.1) a SSO **best-effort**. |
| 2026-06-26 | Sección 4 (entorno test) actualizada tras A/C: registro **SPA público + PKCE** con solo **`User.Read`**, **sin secreto/cert de cliente**; añadido que el token debe **incluir el grupo/rol** para validar el mapeo. |
| 2026-06-26 | Sección 5 (monitorización): **corregido** dato inexacto — NO existe `GET /health` (solo `HEAD /api/models` 200). Reformulado a «podemos exponer un endpoint de estado (p. ej. /health)». Mismo arreglo en gap-analysis. |
| 2026-06-26 | Sección 5: **separados** monitorización y backup. Backup verificado en código (`BackupController`/`IBackupService`: ZIP firmado config+BD+modelos, restore/verify). Ofrecemos **depositar copias en carpeta de red (UNC)** para Veeam. |
| 2026-06-27 | Punto 2 (conectividad) **reescrito más claro**: (a) IPC sale hacia Entra para el login; (b) usuarios RhB entran al IPC por HTTPS (la app web la sirve el IPC). Explicado el porqué de cada dirección (la (ii)/(b) no se entendía). |
| 2026-06-27 | Punto 3 (App Registration): añadidas **aclaraciones entre paréntesis** a SPA (app web en el navegador), API (backend que valida tokens) y URL Citrix. Se mantiene el término técnico + versión sencilla. |
| 2026-06-27 | Punto 4 (permisos): corregido **reenvío hacia adelante** — antes citaba «opciones A o C» (aún no explicadas); ahora dice «las opciones de pantalla de inicio de sesión (ver punto 6)». |
| 2026-06-27 | Sección 5: SMTP — añadido pedir **qué correos quieren recibir** + destinatarios; Código fuente — **quitado** paréntesis (menos detalle); Monitorización — **propuesto** qué puede vigilar `/health` (disponibilidad, PLC, BD, servicios externos) en vez de solo preguntar. |
| 2026-06-27 | Sección 5 (ajuste): SMTP — pedir **qué contenido** (estadísticas/histórico alarmas), destinatarios se configuran luego; Monitorización — **quitado OPC-UA** (este proyecto solo **Modbus**). |
| 2026-06-27 | **Convertido a DOCUMENTO único** [`documento-integracion-rhb.md`](./documento-integracion-rhb.md) con 3 capítulos (A Entra · B Cumplimiento · C **Modbus** nuevo) + control de doc + referencias + glosario + **trazabilidad CRA/IEC 62443** + puntos abiertos. Este `correo-rhb.md` queda como **nota de transmisión** + seguimiento + bitácora. Añadidas filas de seguimiento Modbus. |
| 2026-06-27 | Documento cap. 8 convertido en **formulario de respuesta de RhB** (columnas Respuesta/Responsable/Fecha) → sirve de **registro de acuerdo / evidencia**. Añadida fila para la elección de pantalla de login (A vs C). |
| 2026-06-27 | Cap. 8 rehecho como **bloques por punto** (8.1–8.13) con **área de respuesta amplia** + **bloque de validación** (responsable/cargo/fecha/firma). Añadidas **referencias cruzadas** «↳ responder en 8.x» al inicio de cada pregunta de los cap. A/B/C; puntos 4 y 9 marcados informativos. |
| 2026-06-27 | Remates pre-envío en el documento: **Código 06.7-C07-20** + **Título** Especificación_de_Integración_RhB_IT_Entra_ID_Cumplimiento_Modbus; v0.2; **retiradas referencias internas** (a comentario no exportable); limpiada intro A.2; **quitada del Plan** la frase de retrocompatibilidad (no interesa a RhB). Alemán pendiente (formato frase ES / frase DE). Captura de login: NO se incluye. |
| 2026-06-27 | Cap. B unificado con el cap. A: «↳ responder en el formulario 8.x» (frase completa) en las preguntas y «(informativo)» en los bullets sin respuesta (Hosting, Base de datos). |
| 2026-06-27 | Formulario (cap. 8): reescritos los **13 «Qué necesitamos»** como prompts **claros y autosuficientes** (opción 1), manteniendo «(ref.)» a cada capítulo para el detalle. |
| 2026-06-27 | Creada **versión bilingüe** [`documento-integracion-rhb-bilingue.md`](./documento-integracion-rhb-bilingue.md) (frase ES / *frase DE en cursiva*), v0.3. El ES-only [`documento-integracion-rhb.md`](./documento-integracion-rhb.md) queda como fuente. |
| 2026-06-27 | Ajustes de redacción en **ambos** documentos (v0.3 ES / v0.4 bilingüe): autor = Pugnaghi Matteo — Aquafrisch S.L.U.; estructura por **capítulos A/B/C**; **ADS de Beckhoff** en la arquitectura; **modo kiosko** explicado; **voz directa** en las peticiones a RhB; hosting como **hecho** (sin ofrecer coordinación); retirada la columna **«Firma»**. |
| 2026-06-27 | **Flujo de envío definido:** el documento que se envía a RhB es el **bilingüe**. ANTES lo revisa **Walter** (especialista en alemán) → creado [`correo-walter-revision-aleman.md`](./correo-walter-revision-aleman.md) con el checklist de lo que debe comprobar. |
| 2026-06-27 | Proofreading ES (uniformidad A.2.8 a infinitivo; «a través de qué interfaz» en Modbus; kiosko aclarado en 8.1). **Modbus:** cap. C y 8.12 referencian ahora las **hojas Excel `Modbus_Variables` / `Modbus_Alarms`** que se **adjuntan** al envío (RhB las rellena y devuelve). |
| 2026-07-02 | **Respuesta RhB** (cadena reenviada por Walter): no rellenan adjuntos aún; la **Excel Modbus/alarmas la rellena AZsystems** (Corsin Alig, integrador del GBA); **reunión Teams en KW29** (~13-17 jul) con Aquafrisch+AZsystems+F-IT RhB; Curdin/Roland ausentes hasta 13.07; Roland Pethö sustituye a Curdin. |
| 2026-07-02 | **Fecha dura:** FAT **22.07** y expedición la semana siguiente. Creado correo de **alcance para el FAT** bilingüe [`correo-fat-alcance-rhb.md`](./correo-fat-alcance-rhb.md): la máquina sale funcionando con auth local, **Entra-ready** (flag `EntraIdEnabled` OFF); activación Entra = paso de puesta en marcha posterior (necesita App Registration/grupos/MFA/cert de RhB). Va a Walter → RhB. |
| 2026-07-02 | Correo de alcance FAT **✅ ENVIADO**. Siguiente: **arrancar desarrollo Entra** (Fase 1 gated scaffolding, sin dependencias de RhB) en cuanto el usuario dé luz verde. |
| 2026-07-03 | Cuestionario **ya entregado** (form cap. 8). Creada hoja de **preparación reunión KW29** [`reunion-kw29-preparacion.md`](./reunion-kw29-preparacion.md): confirma lo ya preguntado + **matices a rematar** (CISO, Citrix, cert/DNS, App Roles, acceso remoto comisionado, Modbus/AZsystems) + **nuestros deberes** (esquema de red, IPs/LAN, tenant de dev). |
