# Correo FAT — Alcance de la integración IT / Entra ID (bilingüe ES/DE)

> **Uso interno.** Correo para fijar expectativas **antes del FAT (22.07.2026)**: qué estará listo a la
> entrega y qué depende de datos aún pendientes de RhB. Va a **Walter** (revisa el alemán) → **RhB**
> (Roland Pethö / Curdin Arquint, cc AZsystems). Formato frase ES / *frase DE en cursiva*.
> ✅ **Enviado 2026-07-02.**

---

**Asunto:** FAT 22.07 — alcance de la integración IT / Entra ID para la entrega de la DG-Waschanlage
***Betreff:** FAT 22.07 — Umfang der IT-/Entra-ID-Integration für die Auslieferung der DG-Waschanlage*

Estimados Roland, Curdin y equipo (cc AZsystems, Walter):
*Guten Tag Roland, Curdin und Team (cc AZsystems, Walter):*

De cara al **FAT del 22 de julio** y a la expedición la semana siguiente, queremos **aclarar por adelantado qué estará listo en esa fecha y qué depende de información todavía pendiente por vuestra parte**, para que no haya sorpresas.
*Im Hinblick auf den **FAT am 22. Juli** und die Auslieferung in der Woche darauf möchten wir **vorab klären, was zu diesem Zeitpunkt bereit sein wird und was noch von Angaben Ihrerseits abhängt**, damit es keine Überraschungen gibt.*

**Lo que estará operativo en el FAT y para la entrega (sin depender de datos externos):**
***Was zum FAT und zur Auslieferung betriebsbereit ist (ohne Abhängigkeit von externen Angaben):***

- La máquina funciona **al 100 % con su autenticación local** (usuarios y roles propios), como está hoy.
  *Die Anlage funktioniert **zu 100 % mit ihrer lokalen Authentifizierung** (eigene Benutzer und Rollen), wie heute.*
- La integración con **Microsoft Entra ID (SSO) ya va incorporada en el software**, pero **desactivada mediante un parámetro de configuración** (`EntraIdEnabled`). El equipo sale **«Entra-ready»**: activar el SSO es un cambio de configuración, no un desarrollo.
  *Die Integration mit **Microsoft Entra ID (SSO) ist bereits in der Software enthalten**, jedoch über einen **Konfigurationsparameter** (`EntraIdEnabled`) **deaktiviert**. Das Gerät wird **«Entra-ready»** ausgeliefert: die Aktivierung des SSO ist eine Konfigurationsänderung, keine Entwicklung.*

**Lo que necesita vuestra información para poder activarse y probarse (no es posible antes del FAT):**
***Was Ihre Angaben benötigt, um aktiviert und getestet werden zu können (vor dem FAT nicht möglich):***

- **App Registration** en vuestro tenant → **Tenant ID, Client ID y Redirect URIs** (incluida la URL de **Citrix**).
  ***App Registration** in Ihrem Tenant → **Tenant ID, Client ID und Redirect URIs** (einschliesslich der **Citrix**-URL).*
- **Grupos/roles** de Entra para los 5 perfiles.
  ***Entra-Gruppen/-Rollen** für die 5 Profile.*
- Política de **MFA / Conditional Access** y **certificado HTTPS**.
  *Richtlinie für **MFA / Conditional Access** und **HTTPS-Zertifikat**.*

Sin un tenant real de RhB **no es posible validar un inicio de sesión Entra real**. Por eso proponemos tratar la **activación del SSO como un paso de puesta en marcha posterior** (remoto o en Suiza), una vez dispongamos de esos datos — lo que encaja con la **reunión de la semana 29** que vais a organizar. La lista **Modbus / alarmas** la completará **AZsystems** como integrador del GBA.
*Ohne einen echten RhB-Tenant ist es **nicht möglich, eine echte Entra-Anmeldung zu validieren**. Daher schlagen wir vor, die **Aktivierung des SSO als späteren Inbetriebnahmeschritt** zu behandeln (remote oder in der Schweiz), sobald uns diese Angaben vorliegen — was zur **Sitzung in KW 29** passt, die Sie organisieren werden. Die **Modbus-/Alarmliste** wird **AZsystems** als Integrator des GBA ausfüllen.*

Si os resulta útil, en el propio FAT podemos **demostrar el flujo de login Entra contra un tenant de pruebas nuestro**, para que veáis que la integración está lista a falta únicamente de vuestros parámetros.
*Falls es für Sie hilfreich ist, können wir am FAT den **Entra-Anmeldeablauf gegen einen eigenen Test-Tenant vorführen**, damit Sie sehen, dass die Integration bereit ist und lediglich Ihre Parameter fehlen.*

**¿Nos podéis confirmar qué esperáis ver en el FAT respecto a la parte IT / Entra?** Así lo preparamos en consecuencia.
***Können Sie uns bestätigen, was Sie am FAT bezüglich des IT-/Entra-Teils sehen möchten?** So bereiten wir es entsprechend vor.*

Un cordial saludo,
*Freundliche Grüsse,*

[Matteo Pugnaghi / Gerson Zambrano — Aquafrisch S.L.U.]
