# Preparación reunión KW29 — Integración RhB (Entra ID · Cumplimiento · Modbus)

> **Uso interno.** Hoja de preparación para la **reunión Teams de la semana 29** (~13–17 jul 2026) que
> organizará RhB (Roland Pethö) con **Aquafrisch + AZsystems + F-IT RhB**. Objetivo: que las preguntas ya
> enviadas queden **respondidas del todo** y cerrar los **matices** que no están explícitos en el formulario.
> Documento hermano de [`SKILL.md`](./SKILL.md), [`documento-integracion-rhb.md`](./documento-integracion-rhb.md) y [`correo-rhb.md`](./correo-rhb.md).

## 0. Estado de partida

- El **cuestionario ya está entregado**: es el **formulario del documento (cap. 8, bloques 8.1–8.13)**,
  enviado vía Walter → RhB. El correo de Roland (02.07) confirma que lo están procesando y que **la Excel
  Modbus/alarmas `RhB_Modbus_V1` la rellena AZsystems** (integrador del GBA).
- Esta reunión NO es para reenviar preguntas, sino para **cerrar respuestas** y los matices de abajo.
- **Contexto duro:** FAT 22.07, expedición la semana siguiente. La máquina viaja **Entra-ready** (flag
  `EntraIdEnabled=OFF`, funciona con auth local); la **activación de Entra es puesta en marcha posterior**.

## 1. Ya preguntado en el formulario — confirmar que llega COMPLETO

| Form | Tema | Qué necesitamos que quede cerrado |
|---|---|---|
| 8.3 | App Registration | Tenant ID, Client ID, Redirect URIs (incl. **URL de Citrix**) |
| 8.4 | Grupos/roles | Los 5 roles… **ver matiz §2 (App Roles, no grupos)** |
| 8.6 | MFA / Conditional Access | Política real para IPC compartido de taller |
| 8.7 | Certificado HTTPS | CA de RhB **o** certificado suyo + **hostname/DNS estable** |
| 8.9 | Red / firewall | IP/máscara/GW/DNS de LAN1, reglas HTTPS in/out |
| 8.12 | Modbus | Lo rellena **AZsystems** (rol, registros, señales GBA) |
| 8.13 | Entorno de pruebas / contacto | Tenant de test + contacto técnico (U. Hörler) |

## 2. Matices a rematar (NO están explícitos en el formulario)

**Para el CISO (C. Eugster) — el comodín, conseguir sus requisitos pronto:**
- ¿Acepta **cuenta break-glass local** (admin offline) para disponibilidad? (tensión con su política de cuentas locales)
- Método **MFA** viable en táctil de taller (push / FIDO2 / dispositivo de confianza) y **Conditional Access** que no re-pida MFA cada turno.
- ¿Exige **envío de logs de auth a su SIEM**? ¿Política de sesión / caducidad?
- ¿Requisitos de TLS / cifrado propios?

**Para RhB IT (U. Hörler / T. Richter):**
- **Exigir App Roles, no grupos** (evita el *groups overage* → nos permite quedarnos solo con `User.Read`).
- Proxy de salida a Entra **sin inspección TLS** en los dominios de Entra.
- **Cómo se publica exactamente en Citrix** (app publicada vs escritorio; qué navegador) → afecta Redirect URIs y SSO.
- **Acceso remoto para el comisionado** una vez la máquina esté en Suiza (cómo entramos a rematar Entra).
- **Navegador del kiosko** (compatibilidad MSAL: cookies de terceros / popups).
- **NTP** contra su red (evitar fallos de token por desfase de reloj).

**Para AZsystems (Corsin Alig) — Modbus/GBA:**
- Rol (servidor/cliente/ambos), mapa de registros, **endianness / orden de palabras**, tipos, **escalado/unidades**, cadencia, alarmas.
- Semántica exacta de las señales del GBA (demanda de calor, consigna temp., avisos de avería, detector de agua…).

## 3. Nuestros deberes (RhB los espera de NOSOTROS)

- **Esquema de topología de red** a entregar a RhB (lo pidieron en el acta KOSI Nr.6; resp. Zambrano).
- Nº exacto de **IPs (3-4)** y **tomas LAN**.
- Crear un **tenant Entra de desarrollo de Aquafrisch** para construir/probar el flujo sin depender de RhB.

## 4. Regla-decisión que debemos fijar (deny-by-default)

- Usuario válido en Entra pero **sin rol mapeado = SIN acceso** (o Visor). **Nunca** mapear a SuperAdmin (rol oculto).

---

> **Prioridad:** los puntos del **CISO** y de **Citrix/certificado/DNS** son los que más pueden bloquear;
> llevarlos como los primeros a cerrar en la reunión.
